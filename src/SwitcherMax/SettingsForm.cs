using System.Windows.Forms;
using Microsoft.Win32;

namespace SwitcherMax;

internal sealed class HotkeyEditor : FlowLayoutPanel
{
    private readonly CheckBox _enabled = new() { Text = "Вкл.", AutoSize = true };
    private readonly CheckBox _ctrl = new() { Text = "Ctrl", AutoSize = true };
    private readonly CheckBox _alt = new() { Text = "Alt", AutoSize = true };
    private readonly CheckBox _shift = new() { Text = "Shift", AutoSize = true };
    private readonly CheckBox _win = new() { Text = "Win", AutoSize = true };
    private readonly ComboBox _key = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 75 };

    public HotkeyEditor(string label, HotkeySpec initial)
    {
        Width = 715; Height = 48; WrapContents = false; AutoSize = false;
        Controls.Add(new Label { Text = label, Width = 164, TextAlign = System.Drawing.ContentAlignment.MiddleLeft, Height = 30 });
        foreach (var box in new[] { _enabled, _ctrl, _alt, _shift, _win })
        {
            box.Margin = new Padding(7, 7, 2, 0);
            Controls.Add(box);
        }
        for (int v = (int)Keys.A; v <= (int)Keys.Z; v++) AddKey((Keys)v);
        for (int v = (int)Keys.D0; v <= (int)Keys.D9; v++) AddKey((Keys)v);
        for (int v = (int)Keys.F1; v <= (int)Keys.F12; v++) AddKey((Keys)v);
        Controls.Add(_key);
        _enabled.Checked = initial.Enabled;
        _ctrl.Checked = initial.Ctrl;
        _alt.Checked = initial.Alt;
        _shift.Checked = initial.Shift;
        _win.Checked = initial.Win;
        foreach (KeyOption item in _key.Items)
            if ((int)item.Key == initial.Key) { _key.SelectedItem = item; break; }
        if (_key.SelectedIndex < 0) _key.SelectedIndex = 0;
    }
    private void AddKey(Keys value) => _key.Items.Add(new KeyOption(value));
    public HotkeySpec Value => new()
    {
        Enabled = _enabled.Checked, Ctrl = _ctrl.Checked, Alt = _alt.Checked,
        Shift = _shift.Checked, Win = _win.Checked, Key = (int)((KeyOption)_key.SelectedItem!).Key
    };
    private sealed record KeyOption(Keys Key)
    {
        public override string ToString() => (int)Key >= (int)Keys.D0 && (int)Key <= (int)Keys.D9 ? ((int)Key - (int)Keys.D0).ToString() : Key.ToString();
    }
}

internal sealed class SettingsForm : Form
{
    private readonly HotkeyEditor _auto;
    private readonly HotkeyEditor _enRu;
    private readonly HotkeyEditor _ruEn;
    private readonly CheckBox _smart = new() { Text = "Сохранять знаки препинания в конце слов (умный режим)", AutoSize = true };
    private readonly CheckBox _startup = new() { Text = "Запускать вместе с Windows", AutoSize = true };
    public UserSettings? ResultSettings { get; private set; }

    public SettingsForm(UserSettings current)
    {
        Text = "SwitcherMax — настройки";
        Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? System.Drawing.SystemIcons.Application;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        ClientSize = new System.Drawing.Size(747, 384);
        Font = new System.Drawing.Font("Segoe UI", 9);
        _auto = new HotkeyEditor("Автоопределение", current.Auto);
        _enRu = new HotkeyEditor("Английская → русская", current.EnglishToRussian);
        _ruEn = new HotkeyEditor("Русская → английская", current.RussianToEnglish);
        _smart.Checked = current.SmartPunctuation;
        _startup.Checked = current.StartWithWindows;
        var pane = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(12), AutoScroll = false };
        pane.Controls.Add(new Label { Text = "Глобальные сочетания (обычные клавиши, без Pause / Break / Scroll Lock):", AutoSize = true, Margin = new Padding(0, 0, 0, 10) });
        pane.Controls.Add(_auto); pane.Controls.Add(_enRu); pane.Controls.Add(_ruEn);
        _smart.Margin = new Padding(5, 14, 0, 0);
        _startup.Margin = new Padding(5, 9, 0, 0);
        pane.Controls.Add(_smart); pane.Controls.Add(_startup);
        pane.Controls.Add(new Label { Text = "Совет: Ctrl+Alt может конфликтовать с AltGr. При необходимости используй Ctrl+Shift+буква.", AutoSize = true, Margin = new Padding(5, 10, 0, 6) });
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Width = 715, Height = 42 };
        var save = new Button { Text = "Сохранить", Width = 116, Height = 30 };
        var cancel = new Button { Text = "Отмена", Width = 95, Height = 30, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(save); buttons.Controls.Add(cancel); pane.Controls.Add(buttons);
        save.Click += (_, _) => Save();
        Controls.Add(pane);
        AcceptButton = save; CancelButton = cancel;
    }
    private void Save()
    {
        var candidate = new UserSettings
        {
            Auto = _auto.Value, EnglishToRussian = _enRu.Value, RussianToEnglish = _ruEn.Value,
            SmartPunctuation = _smart.Checked, StartWithWindows = _startup.Checked
        };
        if (candidate.Hotkeys.Any(k => !k.IsValid))
        {
            MessageBox.Show("Для активной горячей клавиши нужен модификатор (Ctrl, Alt, Shift или Win) и допустимая клавиша.", "SwitcherMax");
            return;
        }
        var duplicates = candidate.Hotkeys.Where(h => h.Enabled)
            .GroupBy(h => (h.Modifiers, h.Key)).Any(g => g.Count() > 1);
        if (duplicates)
        {
            MessageBox.Show("Несколько действий назначены на одинаковую комбинацию.", "SwitcherMax");
            return;
        }
        ResultSettings = candidate;
        DialogResult = DialogResult.OK;
        Close();
    }
}
