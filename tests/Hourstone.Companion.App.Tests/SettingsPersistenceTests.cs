using Hourstone.Companion.App;
using System.IO;
using Xunit;

namespace Hourstone.Companion.App.Tests;

public sealed class SettingsPersistenceTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Hourstone.Settings.Tests-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("dark")]
    [InlineData("light")]
    [InlineData("system")]
    public void PreviousVersionPreferencesSurviveLoadSaveAndReload(string theme)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "preferences.json"),
            "{\"Theme\":\"" + theme + "\",\"Language\":\"en\",\"Autostart\":false}");
        var old = UserSettings.Load(directory);
        Assert.Equal(theme, old.Theme);
        Assert.Equal("en", old.Language);
        Assert.False(old.Autostart);
        (old with { Language = "de" }).Save(directory);
        var restarted = UserSettings.Load(directory);
        Assert.Equal(theme, restarted.Theme);
        Assert.Equal("de", restarted.Language);
        Assert.False(restarted.Autostart);
    }

    [Fact]
    public void FailedReplacementKeepsPreviouslySavedSettingsAndCanBeRetried()
    {
        var saved = new UserSettings { Theme = "light", Language = "en", Autostart = false };
        saved.Save(directory);
        var changed = saved with { Theme = "dark" };
        using (var held = new FileStream(Path.Combine(directory, "preferences.json"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var failure = Record.Exception(() => changed.Save(directory));
            Assert.True(failure is IOException or UnauthorizedAccessException);
            Assert.Equal(saved, UserSettings.Load(directory));
        }
        changed.Save(directory);
        Assert.Equal(changed, UserSettings.Load(directory));
    }

    public void Dispose()
    {
        var resolved = Path.GetFullPath(directory);
        if (!resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)
            || !Path.GetFileName(resolved).StartsWith("Hourstone.Settings.Tests-", StringComparison.Ordinal))
            throw new InvalidOperationException("Unexpected settings test directory.");
        if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
    }
}
