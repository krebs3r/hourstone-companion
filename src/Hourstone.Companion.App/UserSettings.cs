using System;
using System.IO;
using System.Text.Json;
namespace Hourstone.Companion.App;

public sealed record UserSettings
{
    public string Theme { get; init; } = "dark";
    public string Language { get; init; } = "de";
    public bool Autostart { get; init; } = true;
    public static string LegacyDataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Hourstone", "Companion");
    public static string DataDirectory => AppDistribution.Current.DataDirectory;
    public static UserSettings Load() => Load(DataDirectory);
    public static UserSettings Load(string directory)
    {
        try { return JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(Path.Combine(directory, "preferences.json"))) ?? new(); }
        catch (IOException) { return new(); }
        catch (JsonException) { return new(); }
    }
    public void Save() => Save(DataDirectory);
    public void Save(string directory)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "preferences.json");
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this));
        File.Move(temporary, path, true);
    }
}
