using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SwitcherMax;

internal static class WindowsInterop
{
    public const int WM_HOTKEY = 0x0312;
    private const uint MOD_NOREPEAT = 0x4000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, INPUT[] inputs, int size);
    [DllImport("user32.dll")]
    public static extern uint GetClipboardSequenceNumber();

    [StructLayout(LayoutKind.Sequential)] private struct INPUT
    {
        public uint type;
        public InputUnion union;
    }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mouse;
        [FieldOffset(0)] public KEYBDINPUT keyboard;
        [FieldOffset(0)] public HARDWAREINPUT hardware;
    }
    [StructLayout(LayoutKind.Sequential)] private struct MOUSEINPUT
    {
        public int dx, dy;
        public uint mouseData, dwFlags, time;
        public UIntPtr dwExtraInfo;
    }
    [StructLayout(LayoutKind.Sequential)] private struct KEYBDINPUT
    {
        public ushort wVk, wScan;
        public uint dwFlags, time;
        public UIntPtr dwExtraInfo;
    }
    [StructLayout(LayoutKind.Sequential)] private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL, wParamH;
    }
    private const uint KEYEVENTF_KEYUP = 0x0002;

    public static void Register(IntPtr handle, int id, HotkeySpec key)
    {
        if (!RegisterHotKey(handle, id, key.Modifiers | MOD_NOREPEAT, (uint)key.Key))
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Не удаётся зарегистрировать {key.Description}. Комбинация уже занята?");
    }
    public static void Unregister(IntPtr handle, int id) => UnregisterHotKey(handle, id);

    public static bool AllModifiersReleased()
    {
        int[] modifiers = [(int)Keys.ControlKey, (int)Keys.Menu, (int)Keys.ShiftKey, (int)Keys.LWin, (int)Keys.RWin];
        return modifiers.All(k => (GetAsyncKeyState(k) & 0x8000) == 0);
    }

    // SendInput is triggered exclusively by a registered user hotkey. No keyboard hook/keylogger.
    public static void SendCtrl(char key)
    {
        var vk = (ushort)char.ToUpperInvariant(key);
        var inputs = new[] { Keyboard(0x11, false), Keyboard(vk, false), Keyboard(vk, true), Keyboard(0x11, true) };
        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent != (uint)inputs.Length)
        {
            // A partial SendInput sequence must not leave Ctrl logically pressed.
            var release = new[] { Keyboard(0x11, true) };
            SendInput(1, release, Marshal.SizeOf<INPUT>());
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows отклонила эмуляцию клавиш.");
        }
    }
    private static INPUT Keyboard(ushort vk, bool up) => new()
    {
        type = 1,
        union = new InputUnion { keyboard = new KEYBDINPUT { wVk = vk, dwFlags = up ? KEYEVENTF_KEYUP : 0 } }
    };
}

internal sealed class HotkeyWindow : NativeWindow, IDisposable
{
    private readonly Action<ConversionMode> _trigger;
    private readonly HashSet<int> _registered = new();
    public HotkeyWindow(Action<ConversionMode> trigger)
    {
        _trigger = trigger;
        CreateHandle(new CreateParams { Caption = "SwitcherMax event window" });
    }
    public void Configure(UserSettings settings)
    {
        Clear();
        try
        {
            for (int index = 0; index < settings.Hotkeys.Length; index++)
            {
                var key = settings.Hotkeys[index];
                if (!key.Enabled) continue;
                int id = index + 101;
                WindowsInterop.Register(Handle, id, key);
                _registered.Add(id);
            }
        }
        catch { Clear(); throw; }
    }
    public void Clear()
    {
        foreach (var id in _registered) WindowsInterop.Unregister(Handle, id);
        _registered.Clear();
    }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WindowsInterop.WM_HOTKEY)
        {
            switch (m.WParam.ToInt32())
            {
                case 101: _trigger(ConversionMode.Auto); break;
                case 102: _trigger(ConversionMode.EnglishToRussian); break;
                case 103: _trigger(ConversionMode.RussianToEnglish); break;
            }
        }
        base.WndProc(ref m);
    }
    public void Dispose() { Clear(); DestroyHandle(); }
}
