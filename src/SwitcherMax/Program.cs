using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SwitcherMax;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var singleton = new Mutex(true, @"Local\SwitcherMax-SingleInstance", out bool first);
        if (!first)
        {
            MessageBox.Show("SwitcherMax уже запущен. Проверь значок в системном трее.", "SwitcherMax");
            return;
        }
        ApplicationConfiguration.Initialize();
        try { Application.Run(new SwitcherApplicationContext()); }
        catch (Exception ex) { MessageBox.Show(ex.ToString(), "SwitcherMax — ошибка запуска", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}

internal sealed class SwitcherApplicationContext : ApplicationContext
{
    private const string StartupValue = "SwitcherMax";
    private const string StartupKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private readonly NotifyIcon _tray;
    private BrandedToast? _notice;
    private readonly HotkeyWindow _hotkeys;
    private readonly ToolStripMenuItem _pause;
    private UserSettings _settings;
    private bool _paused;
    private bool _busy;

    public SwitcherApplicationContext()
    {
        // All awaited clipboard operations must return to this STA UI thread.
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        _settings = SettingsStore.Load();
        _hotkeys = new HotkeyWindow(mode => _ = OnHotkeyAsync(mode));
        var menu = new ContextMenuStrip();
        menu.Items.Add("Настройки...", null, (_, _) => OpenSettings());
        _pause = new ToolStripMenuItem("Приостановить", null, (_, _) => TogglePause());
        menu.Items.Add(_pause);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, _) => ExitThread());
        _tray = new NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "SwitcherMax — исправление раскладки",
            Visible = true,
            ContextMenuStrip = menu
        };
        _tray.DoubleClick += (_, _) => OpenSettings();
        try { _hotkeys.Configure(_settings); }
        catch (Exception ex)
        {
            _paused = true;
            _pause.Checked = true;
            _pause.Text = "Возобновить";
            MessageBox.Show(ex.Message + "\nИзмени комбинации в настройках.", "SwitcherMax — горячая клавиша занята", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            OpenSettings();
        }
    }

    private async Task OnHotkeyAsync(ConversionMode mode)
    {
        if (_busy || _paused) return;
        _busy = true;
        try
        {
            var message = await ClipboardReplacement.ReplaceAsync(mode, _settings.SmartPunctuation);
            // Avoid a balloon on every successful replacement, to keep the tool unobtrusive.
            if (!message.StartsWith("Команда вставки отправлена", StringComparison.Ordinal)
                || message.Contains("ВНИМАНИЕ", StringComparison.Ordinal)
                || message.Contains("Буфер изменён", StringComparison.Ordinal))
                ShowNotice(message);
        }
        catch (Exception ex) { ShowNotice("Операция отменена: " + ex.Message); }
        finally { _busy = false; }
    }

    private void TogglePause()
    {
        if (!_paused)
        {
            _hotkeys.Clear(); _paused = true;
        }
        else
        {
            try { _hotkeys.Configure(_settings); _paused = false; }
            catch (Exception ex) { ShowNotice(ex.Message); }
        }
        _pause.Checked = _paused;
        _pause.Text = _paused ? "Возобновить" : "Приостановить";
    }

    private void OpenSettings()
    {
        using var form = new SettingsForm(_settings);
        if (form.ShowDialog() != DialogResult.OK || form.ResultSettings is null) return;
        var candidate = form.ResultSettings;
        var previous = _settings;
        try
        {
            if (!_paused) _hotkeys.Configure(candidate);
            ChangeStartup(candidate.StartWithWindows);
            SettingsStore.Save(candidate);
            _settings = candidate;
            ShowNotice("Настройки сохранены.");
        }
        catch (Exception ex)
        {
            try
            {
                if (!_paused) _hotkeys.Configure(previous);
                ChangeStartup(previous.StartWithWindows);
            }
            catch { _hotkeys.Clear(); _paused = true; _pause.Checked = true; _pause.Text = "Возобновить"; }
            MessageBox.Show("Изменения отменены: " + ex.Message, "SwitcherMax", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void ChangeStartup(bool enable)
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(StartupKey, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(StartupKey, writable: true);
        if (enable)
        {
            string appPath = Environment.ProcessPath ?? throw new InvalidOperationException("Неизвестен путь к EXE.");
            if (!appPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Для автозапуска требуется опубликованный EXE.");
            runKey.SetValue(StartupValue, $"\"{appPath}\"", RegistryValueKind.String);
        }
        else runKey.DeleteValue(StartupValue, throwOnMissingValue: false);
    }
    private static Icon LoadTrayIcon()
    {
        using var stream = typeof(Program).Assembly.GetManifestResourceStream("SwitcherMax.TrayIcon.ico")
            ?? throw new InvalidOperationException("Не найден встроенный ресурс иконки трея.");
        return new Icon(stream);
    }
    private void ShowNotice(string text)
    {
        // Windows 11 can show an unrelated icon on NotifyIcon.ShowBalloonTip.
        // Use our own non-activating branded popup instead.
        if (_notice is { IsDisposed: false }) _notice.Close();
        var toast = new BrandedToast(text, _tray.Icon ?? SystemIcons.Application);
        _notice = toast;
        toast.FormClosed += (_, _) =>
        {
            if (ReferenceEquals(_notice, toast)) _notice = null;
            toast.Dispose();
        };
        toast.Show();
    }
    protected override void ExitThreadCore()
    {
        if (_notice is { IsDisposed: false }) _notice.Close();
        _hotkeys.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        base.ExitThreadCore();
    }
}
