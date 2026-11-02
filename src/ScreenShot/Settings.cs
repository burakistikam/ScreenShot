using System;
using System.IO;
using System.Text.Json;

namespace ScreenShot;

internal sealed class Settings
{
    public uint Modifiers { get; set; }
    public uint Key { get; set; } = 0x2C;
    public string ShortcutLabel { get; set; } = "Print Screen";
    public string SaveFolder { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
    private static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScreenShot", "settings.json");
    internal static Settings Load()
    {
        try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }
    internal void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, FilePath, true);
    }
}
