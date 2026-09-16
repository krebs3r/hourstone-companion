using System;
using System.IO;
using System.Text.Json;
namespace Hourstone.Companion.App;

public sealed record UserSettings
{
    public string Theme { get; init; } = "dark";
    public string Language { get; init; } = "de";
    public bool Autostart { get; init; } = true;
    public static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Hourstone", "Companion");
    static string FilePath => Path.Combine(DataDirectory, "preferences.json");
    public static UserSettings Load() { try { return JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(FilePath)) ?? new(); } catch (IOException) { return new(); } catch (JsonException) { return new(); } }
    public void Save() { Directory.CreateDirectory(DataDirectory); var tmp = FilePath + ".tmp"; File.WriteAllText(tmp, JsonSerializer.Serialize(this)); File.Move(tmp, FilePath, true); }
}
