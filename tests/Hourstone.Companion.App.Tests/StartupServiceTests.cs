using System.IO;
using Hourstone.Companion.App;
using Xunit;

namespace Hourstone.Companion.App.Tests;

public sealed class StartupServiceTests : IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(), "Hourstone launcher tests", Guid.NewGuid().ToString("N"));
    public StartupServiceTests() => Directory.CreateDirectory(root);

    [Fact]
    public void CurrentPackageTitleLauncherWinsOverLegacyName()
    {
        var current = Path.Combine(root, "Hourstone Companion.exe");
        File.WriteAllBytes(current, []);
        File.WriteAllBytes(Path.Combine(root, "Hourstone.Companion.exe"), []);
        Assert.Equal(current, StartupService.GetDirectLauncherPath(root));
    }

    [Fact]
    public void ExistingLegacyRootLauncherRemainsSupported()
    {
        var legacy = Path.Combine(root, "Hourstone.Companion.exe");
        File.WriteAllBytes(legacy, []);
        Assert.Equal(legacy, StartupService.GetDirectLauncherPath(root));
    }

    [Fact]
    public void MissingRootLauncherDoesNotUseVersionSpecificExecutableOrDirectory()
    {
        var current = Path.Combine(root, "current");
        Directory.CreateDirectory(current);
        File.WriteAllBytes(Path.Combine(current, "Hourstone.Companion.exe"), []);
        Directory.CreateDirectory(Path.Combine(root, "Hourstone Companion.exe"));
        Assert.Throws<FileNotFoundException>(() => StartupService.GetDirectLauncherPath(root));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void MissingInstallationDirectoryCannotResolveAgainstWorkingDirectory(string? directory)
        => Assert.Throws<IOException>(() => StartupService.GetDirectLauncherPath(directory));

    public void Dispose() => Directory.Delete(root, true);
}
