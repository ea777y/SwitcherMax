using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;

namespace SwitcherMax;

public sealed class HotkeySpec
{
    public bool Enabled { get; set; } = true;
    public bool Ctrl { get; set; } = true;
    public bool Alt { get; set; } = true;
    public bool Shift { get; set; }
    public bool Win { get; set; }
    public int Key { get; set; } = (int)Keys.Q;

    public uint Modifiers => (uint)((Ctrl ? 2 : 0) | (Alt ? 1 : 0) | (Shift ? 4 : 0) | (Win ? 8 : 0));
    public bool IsValid => !Enabled || (Modifiers != 0 && IsSupportedKey(Key));
    private static bool IsSupportedKey(int key) =>
        (key >= (int)Keys.A && key <= (int)Keys.Z) ||
        (key >= (int)Keys.F1 && key <= (int)Keys.F12) ||
        (key >= (int)Keys.D0 && key <= (int)Keys.D9);
    public string Description => $"{(Ctrl ? "Ctrl+" : "")}{(Alt ? "Alt+" : "")}{(Shift ? "Shift+" : "")}{(Win ? "Win+" : "")}{(Keys)Key}";
    public HotkeySpec Clone() => (HotkeySpec)MemberwiseClone();
}

public sealed class UserSettings
{
    public HotkeySpec Auto { get; set; } = new();
    public HotkeySpec EnglishToRussian { get; set; } = new() { Key = (int)Keys.E };
    public HotkeySpec RussianToEnglish { get; set; } = new() { Key = (int)Keys.R };
    public bool SmartPunctuation { get; set; } = true;
    public bool StartWithWindows { get; set; }
    [JsonIgnore]
    public HotkeySpec[] Hotkeys => [Auto, EnglishToRussian, RussianToEnglish];
}

public static class SettingsStore
{
    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SwitcherMax");
    public static string FilePath => Path.Combine(Folder, "settings.json");

    public static UserSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new UserSettings();
            var config = JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(FilePath));
            if (config is null || config.Auto is null || config.EnglishToRussian is null || config.RussianToEnglish is null)
                throw new InvalidDataException("Settings are incomplete.");
            return config;
        }
        catch (Exception ex)
        {
            // Never silently overwrite a broken user configuration.
            throw new InvalidDataException($"Не удалось прочитать настройки: {FilePath}\n{ex.Message}", ex);
        }
    }

    public static void Save(UserSettings config)
    {
        Directory.CreateDirectory(Folder);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, FilePath, true);
    }
}
