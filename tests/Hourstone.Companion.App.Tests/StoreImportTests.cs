using System.Text.Json;
using System.IO;
using Hourstone.Companion.App;
using Hourstone.Companion.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Hourstone.Companion.App.Tests;

public sealed class StoreImportTests : IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(), "hourstone-import-tests-" + Guid.NewGuid().ToString("N"));
    string Source => Path.Combine(root, "source");
    string Target => Path.Combine(root, "target");
    public StoreImportTests() => Directory.CreateDirectory(Source);
    [Fact]
    public void WalBackedImportUsesNewIdentityAndLeavesOriginalConfigurationUntouched()
    {
        using (var service = new CompanionService(Path.Combine(Source, "companion.sqlite"))) { }
        new UserSettings { Theme = "light", Language = "en", Autostart = true }.Save(Source);
        using var source = Connect(Path.Combine(Source, "companion.sqlite"));
        Execute(source, "PRAGMA journal_mode=WAL; PRAGMA wal_autocheckpoint=0;");
        var before = (string)Scalar(source, "SELECT value FROM settings WHERE key='configuration'")!;
        Execute(source, "INSERT INTO settings(key,value) VALUES('revision','73') ON CONFLICT(key) DO UPDATE SET value='73';");
        Assert.True(File.Exists(Path.Combine(Source, "companion.sqlite-wal")));
        var result = StoreImportService.ImportProfile(Source, Target);
        Assert.True(result.Succeeded);
        Assert.Equal(before, Scalar(source, "SELECT value FROM settings WHERE key='configuration'"));
        Assert.Equal("73", Scalar(source, "SELECT value FROM settings WHERE key='revision'"));
        using var target = Connect(Path.Combine(Target, "companion.sqlite"));
        var imported = JsonSerializer.Deserialize<CompanionConfiguration>((string)Scalar(target, "SELECT value FROM settings WHERE key='configuration'")!, JsonContract.Options)!;
        var original = JsonSerializer.Deserialize<CompanionConfiguration>(before, JsonContract.Options)!;
        Assert.NotEqual(original.DeviceId, imported.DeviceId);
        Assert.Equal(imported.DeviceId, result.NewDeviceId);
        Assert.Equal("0", Scalar(target, "SELECT value FROM settings WHERE key='revision'"));
        Assert.Equal("light", UserSettings.Load(Target).Theme);
        Assert.Equal("en", UserSettings.Load(Target).Language);
        Assert.False(UserSettings.Load(Target).Autostart);
        Assert.True(File.Exists(Path.Combine(result.BackupPath!, "companion.sqlite")));
        using var backup = Connect(Path.Combine(result.BackupPath!, "companion.sqlite"));
        Assert.Equal("73", Scalar(backup, "SELECT value FROM settings WHERE key='revision'"));
        Assert.Equal(before, Scalar(backup, "SELECT value FROM settings WHERE key='configuration'"));
    }
    [Fact]
    public void FailureBeforeCommitLeavesTargetEmptyAndImportCanBeRetried()
    {
        using (var service = new CompanionService(Path.Combine(Source, "companion.sqlite"))) { }
        Assert.Throws<IOException>(() => StoreImportService.ImportProfile(Source, Target, beforeCommit: () => throw new IOException("handover rejected")));
        Assert.False(File.Exists(Path.Combine(Target, "companion.sqlite")));
        Assert.False(File.Exists(Path.Combine(Target, "store-setup.json")));
        Assert.Empty(Directory.GetDirectories(Target, ".import-*"));
        Assert.True(StoreImportService.ImportProfile(Source, Target).Succeeded);
    }
    [Fact]
    public void CompletedImportCannotBeRepeatedOrOverwriteExistingProfile()
    {
        using (var service = new CompanionService(Path.Combine(Source, "companion.sqlite"))) { }
        var result = StoreImportService.ImportProfile(Source, Target);
        Assert.Throws<IOException>(() => StoreImportService.ImportProfile(Source, Target));
        using var target = Connect(Path.Combine(Target, "companion.sqlite"));
        var configuration = JsonSerializer.Deserialize<CompanionConfiguration>((string)Scalar(target, "SELECT value FROM settings WHERE key='configuration'")!, JsonContract.Options)!;
        Assert.Equal(result.NewDeviceId, configuration.DeviceId);
    }
    [Fact]
    public void LockedStagingCleanupDoesNotUndoACompletedHandover()
    {
        using (var service = new CompanionService(Path.Combine(Source, "companion.sqlite"))) { }
        FileStream? lockedStagingFile = null;
        try
        {
            var result = StoreImportService.ImportProfile(Source, Target, beforeCommit: () =>
            {
                var staging = Assert.Single(Directory.GetDirectories(Target, ".import-*"));
                lockedStagingFile = new FileStream(Path.Combine(staging, "cleanup-lock.tmp"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            });
            Assert.True(result.Succeeded);
            Assert.True(File.Exists(Path.Combine(Target, "store-setup.json")));
            Assert.True(File.Exists(Path.Combine(Target, "companion.sqlite")));
            Assert.True(File.Exists(Path.Combine(result.BackupPath!, "companion.sqlite")));
        }
        finally { lockedStagingFile?.Dispose(); }
    }
    [Fact]
    public void UnsupportedDatabaseFailsWithoutTargetActivation()
    {
        using (var service = new CompanionService(Path.Combine(Source, "companion.sqlite"))) { }
        using (var connection = Connect(Path.Combine(Source, "companion.sqlite"))) Execute(connection, "PRAGMA user_version=999;");
        Assert.Throws<InvalidDataException>(() => StoreImportService.ImportProfile(Source, Target));
        Assert.False(File.Exists(Path.Combine(Target, "companion.sqlite")));
        Assert.False(File.Exists(Path.Combine(Target, "store-setup.json")));
    }
    [Fact]
    public void FreshProfileFailurePreservesSettingsAndCanBeRetried()
    {
        new UserSettings { Theme = "light", Language = "en", Autostart = false }.Save(Target);
        var before = File.ReadAllBytes(Path.Combine(Target, "preferences.json"));
        Assert.Throws<IOException>(() => StoreImportService.StartFreshProfile(Target, true, () => throw new IOException("startup change failed")));
        Assert.Equal(before, File.ReadAllBytes(Path.Combine(Target, "preferences.json")));
        Assert.False(File.Exists(Path.Combine(Target, "store-setup.json")));
        Assert.True(StoreImportService.StartFreshProfile(Target, true).Succeeded);
        Assert.True(UserSettings.Load(Target).Autostart);
        Assert.Equal("en", UserSettings.Load(Target).Language);
        Assert.Throws<IOException>(() => StoreImportService.StartFreshProfile(Target));
    }
    [Fact]
    public void SourceIdsSurviveNewDeviceIdentity()
    {
        var wow = Path.Combine(root, "World of Warcraft");
        using (var service = new CompanionService(Path.Combine(Source, "companion.sqlite")))
        {
            var config = service.GetConfiguration();
            service.SaveConfiguration(config with { Sources = [new SourceConfiguration { SourceId = "retail-stable-source", WoWRoot = wow, ClientDirectory = Path.Combine(wow, "_retail_"), AccountName = "TEST", Flavor = "retail", Region = "eu" }] });
        }
        StoreImportService.ImportProfile(Source, Target);
        using var imported = new CompanionService(Path.Combine(Target, "companion.sqlite"));
        Assert.Equal("retail-stable-source", Assert.Single(imported.GetConfiguration().Sources).SourceId);
        Assert.False(Directory.Exists(wow)); // Import never writes an addon or runs synchronization.
    }
    [Theory]
    [InlineData(Windows.ApplicationModel.StartupTaskState.DisabledByUser, AppStartupState.DisabledByUser)]
    [InlineData(Windows.ApplicationModel.StartupTaskState.DisabledByPolicy, AppStartupState.DisabledByPolicy)]
    [InlineData(Windows.ApplicationModel.StartupTaskState.EnabledByPolicy, AppStartupState.Enabled)]
    public void WindowsStartupRestrictionsRemainVisible(Windows.ApplicationModel.StartupTaskState state, AppStartupState expected)
        => Assert.Equal(expected, StartupService.Map(state));
    [Theory]
    [InlineData(true, false, false, DistributionKind.Store)]
    [InlineData(true, true, true, DistributionKind.Store)]
    [InlineData(false, true, true, DistributionKind.VelopackPortable)]
    [InlineData(false, true, false, DistributionKind.VelopackInstalled)]
    [InlineData(false, false, false, DistributionKind.Development)]
    public void PackageIdentityAlwaysWinsOverUpdaterDetection(bool packaged, bool installed, bool portable, DistributionKind expected)
        => Assert.Equal(expected, AppDistribution.Detect(packaged, installed, portable));
    static SqliteConnection Connect(string path)
    { var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString()); connection.Open(); return connection; }
    static object? Scalar(SqliteConnection connection, string sql)
    { using var command = connection.CreateCommand(); command.CommandText = sql; return command.ExecuteScalar(); }
    static void Execute(SqliteConnection connection, string sql)
    { using var command = connection.CreateCommand(); command.CommandText = sql; command.ExecuteNonQuery(); }
    public void Dispose() { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
}
