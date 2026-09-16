using Hourstone.Companion.Core;
using Xunit;

namespace Hourstone.Companion.Core.Tests;

public sealed class SourceReadinessTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Hourstone.Readiness.Tests-" + Guid.NewGuid().ToString("N"));
    private readonly List<CompanionService> services = [];
    private CompanionService Service()
    {
        var service = new CompanionService(Path.Combine(root, "app", "state.db"), "Synthetic diagnostics PC");
        services.Add(service); return service;
    }
    private SourceConfiguration Source(string account = "TEST_ACCOUNT", string id = "hs-test") => Sample.Source(id) with
    {
        WoWRoot = Path.Combine(root, "WoW"), ClientDirectory = Path.Combine(root, "WoW", "_retail_"), AccountName = account
    };
    private static void WriteToc(SourceConfiguration source, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(source.AddonTocPath)!); File.WriteAllText(source.AddonTocPath, text);
    }
    private static void WriteSaved(SourceConfiguration source, int schema = 2, double seconds = 120)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(source.SavedVariablesPath)!);
        File.WriteAllText(source.SavedVariablesPath, Sample.Lua(source, Sample.Item(source.SourceId, seconds), schema));
    }
    private static void Select(CompanionService service, params SourceConfiguration[] sources) =>
        service.SaveConfiguration(service.GetConfiguration() with { Sources = [.. sources] });
    [Theory]
    [InlineData(1, LocalSourceReadiness.AwaitingGameSave)]
    [InlineData(2, LocalSourceReadiness.Ready)]
    [InlineData(3, LocalSourceReadiness.Ready)]
    public async Task SavedVariableSchemasReportWhetherAnInGameSaveIsNeeded(int schema, LocalSourceReadiness expected)
    {
        var service = Service(); var source = Source(); WriteToc(source, "## Version: 0.2.2\n"); WriteSaved(source, schema); Select(service, source);
        var result = await service.SyncNowAsync(); var status = Assert.Single(result.LocalSourceStatuses);
        Assert.Equal(expected, status.Readiness); Assert.Equal("0.2.2", status.DetectedAddonVersion);
        Assert.Equal(source.SourceId, status.SourceId); Assert.Equal(source.ClientDirectory, status.ClientDirectory);
        Assert.Equal(source.AccountName, status.AccountName); Assert.Equal("retail", status.Flavor);
        Assert.Equal(expected == LocalSourceReadiness.Ready, result.AddonReady);
    }
    [Fact]
    public async Task MissingAddonIsDistinguishedWithoutDiscardingValidSavedObservations()
    {
        var service = Service(); var source = Source(); WriteSaved(source); Select(service, source);
        var original = File.ReadAllBytes(source.SavedVariablesPath); var result = await service.SyncNowAsync();
        Assert.Equal(LocalSourceReadiness.AddonMissing, Assert.Single(result.LocalSourceStatuses).Readiness);
        Assert.Null(result.LocalSourceStatuses[0].DetectedAddonVersion); Assert.Contains(result.Issues, issue => issue.Code == "addon_missing" && issue.SourceId == source.SourceId);
        Assert.Single(service.GetCharacters()); Assert.False(result.AddonReady); Assert.Equal(original, File.ReadAllBytes(source.SavedVariablesPath));
    }
    [Fact]
    public async Task OutdatedAddonVersionIsReportedAndValidObservationsRemainAvailable()
    {
        var service = Service(); var source = Source(); WriteToc(source, "## Title: Hourstone\n## Version: 0.1.1\n"); WriteSaved(source); Select(service, source);
        var result = await service.SyncNowAsync(); var status = Assert.Single(result.LocalSourceStatuses);
        Assert.Equal(LocalSourceReadiness.AddonOutdated, status.Readiness); Assert.Equal("0.1.1", status.DetectedAddonVersion);
        Assert.Contains(result.Issues, issue => issue.Code == "addon_outdated" && issue.SourceId == source.SourceId);
        Assert.Single(service.GetCharacters()); Assert.False(result.AddonReady);
        WriteToc(source, "## Version: 0.2.2\n"); var upgraded = await service.SyncNowAsync();
        Assert.Equal(LocalSourceReadiness.Ready, Assert.Single(upgraded.LocalSourceStatuses).Readiness); Assert.True(upgraded.AddonReady);
    }
    [Fact]
    public async Task OldAddonAndSchemaOneExplainUpdateBeforeRequestingTheFirstGameSave()
    {
        var service = Service(); var source = Source(); WriteToc(source, "## Version: 0.1.1\n"); WriteSaved(source, schema: 1); Select(service, source);
        var old = await service.SyncNowAsync(); Assert.Equal(LocalSourceReadiness.AddonOutdated, Assert.Single(old.LocalSourceStatuses).Readiness);
        Assert.DoesNotContain(old.Issues, issue => issue.Code == "source_not_initialized");
        WriteToc(source, "## Version: 0.2.2\n"); var awaiting = await service.SyncNowAsync();
        Assert.Equal(LocalSourceReadiness.AwaitingGameSave, Assert.Single(awaiting.LocalSourceStatuses).Readiness);
        Assert.Contains(awaiting.Issues, issue => issue.Code == "source_not_initialized");
    }
    [Theory]
    [InlineData("\"invalid:source\"")]
    [InlineData("42")]
    [InlineData("nil")]
    public async Task InvalidSourceIdRequiresAGameSaveRatherThanACompanionRestart(string luaId)
    {
        var service = Service(); var source = Source(); WriteToc(source, "## Version: 0.2.2\n"); WriteSaved(source); Select(service, source);
        var lua = File.ReadAllText(source.SavedVariablesPath).Replace("sourceId=\"hs-test\"", "sourceId=" + luaId, StringComparison.Ordinal);
        File.WriteAllText(source.SavedVariablesPath, lua); var result = await service.SyncNowAsync();
        var status = Assert.Single(result.LocalSourceStatuses); Assert.Equal(LocalSourceReadiness.AwaitingGameSave, status.Readiness);
        Assert.Contains("/reload", status.Message); Assert.Contains(source.AccountName, status.Message);
        Assert.Contains("Ein Neustart des Companions ersetzt diesen Schritt nicht", status.Message);
        Assert.Contains(result.Issues, issue => issue.Code == "source_not_initialized"); Assert.Empty(service.GetCharacters());
        Assert.Equal(lua, File.ReadAllText(source.SavedVariablesPath));
    }
    [Fact]
    public async Task MissingSavedVariablesWaitForFirstInGameSaveAndRecoverWithActualSourceIdentity()
    {
        var service = Service(); var pending = Source(id: "pending-source"); WriteToc(pending, "## Version: 0.2.2\n"); Select(service, pending);
        Assert.Equal(LocalSourceReadiness.AwaitingGameSave, Assert.Single((await service.SyncNowAsync()).LocalSourceStatuses).Readiness);
        var actual = pending with { SourceId = "hs-real-game-source" }; WriteSaved(actual, seconds: 220);
        var ready = await service.SyncNowAsync(); var status = Assert.Single(ready.LocalSourceStatuses);
        Assert.Equal(LocalSourceReadiness.Ready, status.Readiness); Assert.Equal(actual.SourceId, status.SourceId);
        Assert.Equal(actual.SourceId, Assert.Single(service.GetConfiguration().Sources).SourceId);
        Assert.Equal(220, Assert.Single(service.GetCharacters()).Seconds); Assert.True(ready.AddonReady);
    }
    [Fact]
    public async Task AccountsWithinTheSameClientHaveIndependentReadinessAndContext()
    {
        var service = Service(); var ready = Source("READY_ACCOUNT", "hs-ready"); var waiting = Source("WAITING_ACCOUNT", "pending-waiting");
        WriteToc(ready, "## Version: 0.2.2\n"); WriteSaved(ready); WriteSaved(waiting, schema: 1); Select(service, ready, waiting);
        var result = await service.SyncNowAsync(); Assert.Equal(2, result.LocalSourceStatuses.Count);
        var readyStatus = result.LocalSourceStatuses.Single(item => item.AccountName == "READY_ACCOUNT");
        var waitingStatus = result.LocalSourceStatuses.Single(item => item.AccountName == "WAITING_ACCOUNT");
        Assert.Equal(LocalSourceReadiness.Ready, readyStatus.Readiness); Assert.Equal(LocalSourceReadiness.AwaitingGameSave, waitingStatus.Readiness);
        Assert.Equal("0.2.2", readyStatus.DetectedAddonVersion); Assert.Equal("0.2.2", waitingStatus.DetectedAddonVersion);
        Assert.Contains("WAITING_ACCOUNT", waitingStatus.Message); Assert.Single(service.GetCharacters()); Assert.False(result.AddonReady);
        Select(service, ready, waiting with { Enabled = false });
        Assert.Equal("READY_ACCOUNT", Assert.Single((await service.SyncNowAsync()).LocalSourceStatuses).AccountName);
    }
    [Fact]
    public async Task SourceReadFailureRetainsLastGoodDataAndDetectedVersion()
    {
        var service = Service(); var source = Source(); WriteToc(source, "## Version: 0.2.2\n"); WriteSaved(source, seconds: 420); Select(service, source);
        await service.SyncNowAsync(); File.WriteAllText(source.SavedVariablesPath, "HourstoneDB = { characters = {");
        var failed = await service.SyncNowAsync(); var status = Assert.Single(failed.LocalSourceStatuses);
        Assert.Equal(LocalSourceReadiness.ReadFailed, status.Readiness); Assert.Equal("0.2.2", status.DetectedAddonVersion);
        Assert.Contains(source.AccountName, status.Message); Assert.Contains(failed.Issues, issue => issue.Code == "source_read_failed");
        Assert.Equal(420, Assert.Single(service.GetCharacters()).Seconds); Assert.False(failed.AddonReady);
        WriteSaved(source, seconds: 520); Assert.Equal(LocalSourceReadiness.Ready, Assert.Single((await service.SyncNowAsync()).LocalSourceStatuses).Readiness);
        Assert.Equal(520, Assert.Single(service.GetCharacters()).Seconds);
    }
    [Theory]
    [InlineData("## Version: 0.2.0\n", LocalSourceReadiness.AddonOutdated, "0.2.0")]
    [InlineData("## Title: Hourstone\n", LocalSourceReadiness.AddonOutdated, null)]
    [InlineData("## Version: 0.2.1\n", LocalSourceReadiness.AddonOutdated, "0.2.1")]
    [InlineData("## Version: unreleased\n", LocalSourceReadiness.AddonOutdated, "unreleased")]
    [InlineData("## Version: 0.2.2-beta\n", LocalSourceReadiness.AddonOutdated, "0.2.2-beta")]
    [InlineData("  ## Version: v0.2.2\n", LocalSourceReadiness.Ready, "v0.2.2")]
    [InlineData("## Version: 0.2.2\n## Version: 9.0.0\n", LocalSourceReadiness.ReadFailed, null)]
    public void VersionMetadataIsReadConservatively(string toc, LocalSourceReadiness expected, string? version)
    {
        var source = Source(); WriteToc(source, toc); var status = AddonReadiness.Inspect(source);
        Assert.Equal(expected, status.Readiness); Assert.Equal(version, status.DetectedAddonVersion);
    }
    [Fact]
    public async Task OversizedOrInvalidUtf8TocReportsReadFailureWithoutLosingSourceData()
    {
        var service = Service(); var source = Source(); WriteSaved(source); Select(service, source);
        WriteToc(source, "## Version: 0.2.2\n" + new string('x', AddonReadiness.MaximumTocBytes));
        var oversized = await service.SyncNowAsync(); Assert.Equal(LocalSourceReadiness.ReadFailed, Assert.Single(oversized.LocalSourceStatuses).Readiness);
        Assert.Contains(oversized.Issues, issue => issue.Code == "addon_read_failed"); Assert.Single(service.GetCharacters());
        File.WriteAllBytes(source.AddonTocPath, [0xFF, 0xFE, 0xFA]);
        Assert.Equal(LocalSourceReadiness.ReadFailed, Assert.Single((await service.SyncNowAsync()).LocalSourceStatuses).Readiness); Assert.Single(service.GetCharacters());
    }
    [Theory]
    [InlineData("_anniversary_")]
    [InlineData("_classic_anniversary_")]
    public void AnniversaryClientWithoutCharacterRowsIsDiscoveredAsTbc(string clientName)
    {
        var service = Service(); var source = Source(id: "hs-anniversary");
        source = source with { ClientDirectory = Path.Combine(source.WoWRoot, clientName), Flavor = "tbc" };
        WriteToc(source, "## Version: 0.2.2\n"); Directory.CreateDirectory(Path.GetDirectoryName(source.SavedVariablesPath)!);
        File.WriteAllText(source.SavedVariablesPath, "HourstoneDB={version=2,sourceId=\"hs-anniversary\",characters={}}");
        var discovered = Assert.Single(service.DiscoverSources([source.WoWRoot]));
        Assert.Equal("tbc", discovered.Flavor); Assert.Equal("hs-anniversary", discovered.SourceId); Assert.Equal(source.ClientDirectory, discovered.ClientDirectory);
    }
    public void Dispose()
    {
        foreach (var service in services) service.Dispose(); Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        var full = Path.GetFullPath(root);
        if (full.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) && Path.GetFileName(full).StartsWith("Hourstone.Readiness.Tests-", StringComparison.Ordinal) && Directory.Exists(full)) Directory.Delete(full, true);
    }
}
