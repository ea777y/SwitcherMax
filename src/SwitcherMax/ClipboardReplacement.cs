using System.Drawing;
using System.Windows.Forms;

namespace SwitcherMax;

/// <summary>
/// Copies selected text with Ctrl+C, pastes corrected text with Ctrl+V, and restores the
/// clipboard only if no other program/user has modified it in the meantime. STA thread only.
/// This is deliberately conservative: unsupported original clipboard formats ABORT the action.
/// </summary>
internal sealed class ClipboardBackup
{
    private readonly DataObject _snapshot = new();
    private readonly bool _wasEmpty;
    public uint OriginalSequence { get; }
    private ClipboardBackup(bool wasEmpty, uint sequence) { _wasEmpty = wasEmpty; OriginalSequence = sequence; }

    public static ClipboardBackup Capture()
    {
        uint firstSeq = WindowsInterop.GetClipboardSequenceNumber();
        var original = Clipboard.GetDataObject();
        var formats = original?.GetFormats(false) ?? [];
        var backup = new ClipboardBackup(formats.Length == 0, firstSeq);
        foreach (var format in formats)
        {
            object? value = original!.GetData(format, false);
            if (value is null)
                throw new NotSupportedException($"Исходный буфер содержит недоступный формат: {format}.");
            object copy = value switch
            {
                string s => s,
                byte[] bytes => (byte[])bytes.Clone(),
                string[] files => (string[])files.Clone(),
                MemoryStream ms => new MemoryStream(ms.ToArray()),
                Stream stream => CloneStream(stream),
                Bitmap bmp => (Bitmap)bmp.Clone(),
                _ => throw new NotSupportedException($"Формат буфера {format} ({value.GetType().Name}) не поддерживает безопасное копирование.")
            };
            backup._snapshot.SetData(format, false, copy);
        }
        if (WindowsInterop.GetClipboardSequenceNumber() != firstSeq)
            throw new InvalidOperationException("Буфер обмена изменился во время резервного копирования.");
        return backup;
    }

    private static MemoryStream CloneStream(Stream source)
    {
        if (!source.CanRead || !source.CanSeek)
            throw new NotSupportedException("Невозможно надёжно скопировать поток буфера обмена.");
        var oldPos = source.Position;
        try
        {
            source.Position = 0;
            var buffer = new MemoryStream();
            source.CopyTo(buffer);
            buffer.Position = 0;
            return buffer;
        }
        finally { source.Position = oldPos; }
    }

    public void Restore()
    {
        if (_wasEmpty) Clipboard.Clear();
        else Clipboard.SetDataObject(_snapshot, true, 10, 100);
    }
}

internal static class ClipboardReplacement
{
    /// <returns>Human-readable result. No selected text is written to disk or logs.</returns>
    public static async Task<string> ReplaceAsync(ConversionMode mode, bool smartPunctuation)
    {
        IntPtr originalWindow = WindowsInterop.GetForegroundWindow();
        if (originalWindow == IntPtr.Zero) return "Не определено активное окно.";

        // A Windows hotkey may fire while its Ctrl/Alt key is still physically held.
        // Wait to avoid injecting Ctrl+C as Alt+C or disrupting the target application.
        bool released = false;
        for (int i = 0; i < 60; i++)
        {
            if (WindowsInterop.AllModifiersReleased()) { released = true; break; }
            await Task.Delay(25);
        }
        if (!released) return "Отпусти клавиши горячей комбинации и повтори.";
        if (WindowsInterop.GetForegroundWindow() != originalWindow)
            return "Активное окно изменилось; текст не затронут.";

        // All operations after the first clipboard mutation run on the WinForms STA thread.
        ClipboardBackup backup;
        try { backup = ClipboardBackup.Capture(); }
        catch (Exception ex) { return "Операция отменена: не удалось безопасно сохранить буфер. " + ex.Message; }

        bool clipboardOwned = false;
        uint lastOwnSequence = 0;
        string result = "Текст не изменён.";
        try
        {
            if (WindowsInterop.GetClipboardSequenceNumber() != backup.OriginalSequence)
                return "Буфер обмена изменился до начала операции; текст не затронут.";
            Clipboard.Clear();
            clipboardOwned = true;
            lastOwnSequence = WindowsInterop.GetClipboardSequenceNumber();
            WindowsInterop.SendCtrl('C');
            bool copied = false;
            for (int i = 0; i < 24; i++)
            {
                await Task.Delay(40);
                uint now = WindowsInterop.GetClipboardSequenceNumber();
                if (now != lastOwnSequence) { copied = true; lastOwnSequence = now; break; }
            }
            if (!copied) return "Выделенный текст не обнаружен (или приложение не поддерживает Ctrl+C).";
            if (WindowsInterop.GetForegroundWindow() != originalWindow)
                return "Активное окно изменилось; вставка отменена.";
            if (!Clipboard.ContainsText(TextDataFormat.UnicodeText))
                return "В выделении нет обычного текста Unicode.";
            var selected = Clipboard.GetText(TextDataFormat.UnicodeText);
            if (selected.Length == 0) return "Выделенный текст пуст.";
            var converted = Conversion.Convert(selected, mode, smartPunctuation);
            if (converted is null)
                return "Преобразование не требуется или направление неоднозначно. Используй принудительную комбинацию.";

            if (WindowsInterop.GetClipboardSequenceNumber() != lastOwnSequence)
            {
                clipboardOwned = false; // Another process has changed the clipboard; do not overwrite it.
                return "Буфер обмена изменён другой программой; вставка отменена.";
            }
            if (WindowsInterop.GetForegroundWindow() != originalWindow)
                return "Активное окно изменилось; вставка отменена.";

            Clipboard.SetText(converted, TextDataFormat.UnicodeText);
            lastOwnSequence = WindowsInterop.GetClipboardSequenceNumber();
            WindowsInterop.SendCtrl('V');
            // Many applications retrieve clipboard data asynchronously; short delay reduces
            // the risk of restoring the clipboard before a paste finishes.
            await Task.Delay(Math.Clamp(converted.Length / 120, 400, 1600));
            result = "Команда вставки отправлена. Результат зависит от приложения.";
        }
        catch (Exception ex)
        {
            result = "Ошибка преобразования: " + ex.Message;
        }
        finally
        {
            if (clipboardOwned)
            {
                if (WindowsInterop.GetClipboardSequenceNumber() == lastOwnSequence)
                {
                    try { backup.Restore(); }
                    catch (Exception ex)
                    {
                        // Important: Never claim success when clipboard restoration failed.
                        result += " ВНИМАНИЕ: исходный буфер не восстановлен: " + ex.Message;
                    }
                }
                else
                {
                    result += " Буфер изменён извне — новое содержимое сохранено, старое не восстановлено.";
                }
            }
        }
        return result;
    }
}
