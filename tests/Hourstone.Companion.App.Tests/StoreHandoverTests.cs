using System.IO;
using System.Security;
using System.Windows;
using Hourstone.Companion.App;
using Hourstone.Companion.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Hourstone.Companion.App.Tests;

public sealed class StoreHandoverTests : IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(), "hourstone-handover-" + Guid.NewGuid().ToString("N"));
    string Source => Path.Combine(root, "source");
    string Target => Path.Combine(root, "target");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedCommitRestoresStoreStartupAndAllowsRetry(bool import)
    {
        Directory.CreateDirectory(Source);
        using (var service = new CompanionService(Path.Combine(Source, "companion.sqlite"))) { }
        new UserSettings { Autostart = true, Language = "en" }.Save(Source);
        SqliteConnection.ClearAllPools();
        var originalSettings = File.ReadAllBytes(Path.Combine(Source, "preferences.json"));
        var originalDatabase = File.ReadAllBytes(Path.Combine(Source, "companion.sqlite"));
        var startup = new FakeStartup { State = AppStartupState.Enabled };
        var result = import
            ? await StoreImportService.ImportWithStartupAsync(Source, Target, false, startup, beforeCommit: Fail)
            : await StoreImportService.StartFreshWithStartupAsync(Target, false, startup, Fail);
        Assert.False(result.Succeeded);
        Assert.Equal(AppStartupState.Enabled, startup.State);
        Assert.Equal(new[] { false, true }, startup.Requests);
        Assert.False(File.Exists(Path.Combine(Target, "store-setup.json")));
        Assert.False(File.Exists(Path.Combine(Target, "companion.sqlite")));
        Assert.Equal(originalSettings, File.ReadAllBytes(Path.Combine(Source, "preferences.json")));
        Assert.Equal(originalDatabase, File.ReadAllBytes(Path.Combine(Source, "companion.sqlite")));
        result = import
            ? await StoreImportService.ImportWithStartupAsync(Source, Target, false, startup)
            : await StoreImportService.StartFreshWithStartupAsync(Target, false, startup);
        Assert.True(result.Succeeded);
        var requests = startup.Requests.Count;
        var repeated = await StoreImportService.StartFreshWithStartupAsync(Target, true, startup);
        Assert.False(repeated.Succeeded);
        Assert.Equal(requests, startup.Requests.Count);
    }

    [Theory]
    [InlineData(false, AppStartupState.DisabledByUser)]
    [InlineData(true, AppStartupState.DisabledByPolicy)]
    public async Task WindowsRefusalIsStoredAsDisabledWithoutFailingSetup(bool import, AppStartupState restriction)
    {
        Directory.CreateDirectory(Source);
        using (var service = new CompanionService(Path.Combine(Source, "companion.sqlite"))) { }
        var startup = new FakeStartup { State = restriction, Restricted = true };
        var result = import
            ? await StoreImportService.ImportWithStartupAsync(Source, Target, true, startup)
            : await StoreImportService.StartFreshWithStartupAsync(Target, true, startup);
        Assert.True(result.Succeeded);
        Assert.False(UserSettings.Load(Target).Autostart);
        Assert.Equal(restriction, startup.State);
        Assert.Equal(new[] { true }, startup.Requests);
    }

    [Fact]
    public async Task StartupExceptionRollsBackBeforeRetry()
    {
        var startup = new FakeStartup { FailNext = true };
        var result = await StoreImportService.StartFreshWithStartupAsync(Target, true, startup);
        Assert.False(result.Succeeded);
        Assert.False(File.Exists(Path.Combine(Target, "store-setup.json")));
        Assert.Equal(AppStartupState.Disabled, startup.State);
        Assert.Equal(new[] { true, false }, startup.Requests);
        Assert.True((await StoreImportService.StartFreshWithStartupAsync(Target, true, startup)).Succeeded);
    }

    [Fact]
    public async Task CancellationBeforeCommitRestoresStartupAndKeepsSource()
    {
        Directory.CreateDirectory(Source);
        using (var service = new CompanionService(Path.Combine(Source, "companion.sqlite"))) { }
        using var cancellation = new CancellationTokenSource();
        var startup = new FakeStartup { AfterSet = cancellation.Cancel };
        var result = await StoreImportService.ImportWithStartupAsync(Source, Target, true, startup, cancellation.Token);
        Assert.False(result.Succeeded);
        Assert.Equal(AppStartupState.Disabled, startup.State);
        Assert.False(File.Exists(Path.Combine(Target, "store-setup.json")));
        Assert.True(File.Exists(Path.Combine(Source, "companion.sqlite")));
    }

    [Theory]
    [InlineData("prepare")]
    [InlineData("fresh")]
    public void ManualHandoverRequiresConfirmationOnlyWhenPreviousEditionWasDetected(string stage)
    {
        var model = new StoreSetupViewModel();
        model.SetState(stage, false);
        Assert.True(model.CanContinue);
        Assert.Equal(Visibility.Collapsed, model.HandoverVisibility);
        model.RequiresLegacyHandover = true;
        Assert.False(model.CanContinue);
        Assert.Equal(Visibility.Visible, model.HandoverVisibility);
        model.LegacyHandoverConfirmed = true;
        Assert.True(model.CanContinue);
        model.SetState("running", false);
        Assert.False(model.CanContinue);
        model.SetState("done", false);
        Assert.True(model.CanContinue);
        Assert.False(new StoreSetupViewModel { RequiresLegacyHandover = true }.LegacyHandoverConfirmed);
    }

    [Fact]
    public void DetectionIsReadOnlyAndUnknownStateOffersGuidance()
    {
        Assert.False(StartupService.NeedsLegacyHandover(new(false, Source), () => null));
        Assert.True(StartupService.NeedsLegacyHandover(new(true, Source), () => throw new Exception("Must not be read")));
        Assert.True(StartupService.NeedsLegacyHandover(new(false, Source), () => "old launcher --background"));
        Assert.True(StartupService.NeedsLegacyHandover(new(false, Source), () => throw new SecurityException()));
        Assert.True(StartupService.NeedsLegacyHandover(new(false, Source, Error: "Unreadable profile"), () => null));
    }

    static void Fail() => throw new IOException("Synthetic commit failure");
    sealed class FakeStartup : IStoreStartupController
    {
        public AppStartupState State = AppStartupState.Disabled;
        public bool Restricted, FailNext;
        public Action? AfterSet;
        public List<bool> Requests { get; } = [];
        public Task<AppStartupState> GetStateAsync() => Task.FromResult(State);
        public Task<AppStartupState> SetEnabledAsync(bool enabled)
        {
            Requests.Add(enabled);
            if (FailNext) { FailNext = false; throw new IOException("Synthetic startup failure"); }
            if (!Restricted) State = enabled ? AppStartupState.Enabled : AppStartupState.Disabled;
            AfterSet?.Invoke();
            return Task.FromResult(State);
        }
    }
    public void Dispose() { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
}
