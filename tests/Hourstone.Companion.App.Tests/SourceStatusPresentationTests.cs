using Hourstone.Companion.Core;
using Xunit;

namespace Hourstone.Companion.App.Tests;

public sealed class SourceStatusPresentationTests
{
    private static LocalSourceStatus Status(LocalSourceReadiness readiness, string id = "private-source-id", string flavor = "retail", string account = "TEST_ACCOUNT", string? version = "0.2.1") =>
        new(id, @"C:\Synthetic\WoW\_retail_", account, flavor, readiness, version);
    private static SourceConfiguration Source(LocalSourceStatus status) => new()
    {
        SourceId = status.SourceId, ClientDirectory = status.ClientDirectory, WoWRoot = @"C:\Synthetic\WoW",
        AccountName = status.AccountName, Flavor = status.Flavor, Region = "eu"
    };
    private static SyncResult Result(IReadOnlyList<LocalSourceStatus> statuses, params SyncIssue[] issues) =>
        new(DateTimeOffset.UtcNow, 0, statuses.Count, 1, issues) { LocalSourceStatuses = statuses };

    [Theory]
    [InlineData(LocalSourceReadiness.AddonMissing, "Hourstone fehlt", "Hourstone missing")]
    [InlineData(LocalSourceReadiness.AddonOutdated, "Hourstone aktualisieren", "Update Hourstone")]
    [InlineData(LocalSourceReadiness.AwaitingGameSave, "Warte auf Speicherung in WoW", "Waiting for a WoW save")]
    [InlineData(LocalSourceReadiness.Ready, "Bereit", "Ready")]
    [InlineData(LocalSourceReadiness.ReadFailed, "Lesen fehlgeschlagen", "Read failed")]
    public void EveryReadinessStateHasGermanAndEnglishTitle(LocalSourceReadiness readiness, string german, string english)
    {
        Assert.Equal(german, SourceStatusPresentation.Title(Status(readiness), false));
        Assert.Equal(english, SourceStatusPresentation.Title(Status(readiness), true));
    }
    [Theory]
    [InlineData(LocalSourceReadiness.AddonMissing, false)]
    [InlineData(LocalSourceReadiness.AddonMissing, true)]
    [InlineData(LocalSourceReadiness.AddonOutdated, false)]
    [InlineData(LocalSourceReadiness.AddonOutdated, true)]
    [InlineData(LocalSourceReadiness.AwaitingGameSave, false)]
    [InlineData(LocalSourceReadiness.AwaitingGameSave, true)]
    public void SetupInstructionsNameTheRequiredAddonAndActualInGameSave(LocalSourceReadiness readiness, bool english)
    {
        var instruction = SourceStatusPresentation.Instruction(Status(readiness, version: "0.1.1"), english);
        Assert.Contains("0.2.1", instruction); Assert.Contains("WoW", instruction); Assert.Contains("/reload", instruction);
        Assert.Contains(english ? "log in with this account" : "logge dich mit diesem Account ein", instruction);
        Assert.Contains(english ? "Restarting the Companion does not replace this step" : "Ein Neustart des Companions ersetzt diesen Schritt nicht", instruction);
        if (readiness == LocalSourceReadiness.AddonOutdated) Assert.Contains("0.1.1", instruction);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IdenticalInitializationIssuesIdentifyTheirDifferentClientsAndAccounts(bool english)
    {
        var mists = Status(LocalSourceReadiness.AwaitingGameSave, "opaque-mists-id", "mists", "MISTS_ACCOUNT");
        var era = Status(LocalSourceReadiness.AwaitingGameSave, "opaque-era-id", "era", "ERA_ACCOUNT");
        var result = Result([mists, era], new SyncIssue("source_not_initialized", "old untranslated generic message", mists.SourceId), new SyncIssue("source_not_initialized", "old untranslated generic message", era.SourceId));
        var diagnostics = SourceStatusPresentation.Diagnostics(result, [Source(mists), Source(era)], english);
        Assert.Contains("Mists Classic · MISTS_ACCOUNT", diagnostics); Assert.Contains("Classic Era · ERA_ACCOUNT", diagnostics);
        Assert.Equal(2, diagnostics.Split(Environment.NewLine).Length); Assert.Contains("[source_not_initialized]", diagnostics);
        Assert.DoesNotContain(mists.SourceId, diagnostics); Assert.DoesNotContain(era.SourceId, diagnostics);
        Assert.DoesNotContain("old untranslated generic message", diagnostics);
        Assert.Contains(english ? "Waiting for a WoW save" : "Warte auf Speicherung in WoW", diagnostics);
    }
    [Fact]
    public void ReadErrorsRetainCodeAndTechnicalDetailsAlongsideFriendlyContext()
    {
        var status = Status(LocalSourceReadiness.ReadFailed, account: "LOCKED_ACCOUNT", version: "0.2.1");
        const string detail = "IOException: The process cannot access Hourstone.lua because another process is using it.";
        var result = Result([status], new SyncIssue("source_read_failed", detail, status.SourceId));
        var diagnostics = SourceStatusPresentation.Diagnostics(result, [Source(status)], true);
        Assert.Contains("Retail · LOCKED_ACCOUNT", diagnostics); Assert.Contains("Read failed", diagnostics);
        Assert.Contains("last valid data is kept", diagnostics); Assert.Contains("[source_read_failed]", diagnostics); Assert.Contains(detail, diagnostics);
        Assert.DoesNotContain(status.SourceId, diagnostics);
    }
    [Fact]
    public void KnownSourceConfigurationProvidesContextBeforeStatusIsAvailable()
    {
        var status = Status(LocalSourceReadiness.AddonMissing, flavor: "tbc", account: "TBC_ACCOUNT");
        var result = Result([], new SyncIssue("addon_missing", "generic core text", status.SourceId));
        var diagnostics = SourceStatusPresentation.Diagnostics(result, [Source(status)], true);
        Assert.Contains("TBC Anniversary · TBC_ACCOUNT", diagnostics); Assert.Contains("Hourstone missing", diagnostics); Assert.Contains("0.2.1", diagnostics);
        Assert.DoesNotContain("generic core text", diagnostics); Assert.DoesNotContain(status.SourceId, diagnostics);
    }
    [Fact]
    public void CloudAndUnidentifiedSourceIssuesKeepTheirCodeAndMessage()
    {
        var result = Result([], new SyncIssue("cloud_unavailable", "Cloud folder unavailable."), new SyncIssue("custom_source_issue", "Custom technical detail.", "unknown-private-id"));
        var diagnostics = SourceStatusPresentation.Diagnostics(result, [], true);
        Assert.Equal("cloud_unavailable: Cloud folder unavailable." + Environment.NewLine + "custom_source_issue: Custom technical detail.", diagnostics);
        Assert.DoesNotContain("unknown-private-id", diagnostics);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnconfiguredAndReadyStatesAreDistinguished(bool english)
    {
        Assert.Equal(english ? "No local clients configured yet." : "Noch keine lokalen Clients eingerichtet.", SourceStatusPresentation.Diagnostics(SyncResult.Empty, [], english));
        var status = Status(LocalSourceReadiness.Ready, account: "READY_ACCOUNT", version: "0.2.1");
        var ready = SourceStatusPresentation.Diagnostics(Result([status]), [Source(status)], english);
        Assert.Contains("Retail · READY_ACCOUNT", ready); Assert.Contains(english ? "Ready" : "Bereit", ready); Assert.Contains("0.2.1", ready);
        Assert.DoesNotContain(english ? "No local clients" : "Noch keine lokalen Clients", ready);
        Assert.Equal(english ? "No check has run yet." : "Es wurde noch keine Prüfung durchgeführt.", SourceStatusPresentation.Diagnostics(SyncResult.Empty, [Source(status)], english));
    }
    [Fact]
    public void MultipleIssuesForOneSourceAreAllRetainedWithTheirOwnReadinessExplanation()
    {
        var status = Status(LocalSourceReadiness.ReadFailed);
        var result = Result([status], new SyncIssue("addon_missing", "generic missing", status.SourceId), new SyncIssue("source_read_failed", "Unexpected end of Lua table.", status.SourceId));
        var diagnostics = SourceStatusPresentation.Diagnostics(result, [Source(status)], true);
        Assert.Equal(2, diagnostics.Split(Environment.NewLine).Length); Assert.Contains("Hourstone missing", diagnostics); Assert.Contains("Read failed", diagnostics);
        Assert.Contains("[addon_missing]", diagnostics); Assert.Contains("[source_read_failed]", diagnostics); Assert.Contains("Unexpected end of Lua table.", diagnostics);
    }
    [Fact]
    public void ReadySourcesRemainVisibleAlongsideAnotherAccountsIssue()
    {
        var ready = Status(LocalSourceReadiness.Ready, "ready-id", account: "READY_ACCOUNT");
        var missing = Status(LocalSourceReadiness.AddonMissing, "missing-id", account: "MISSING_ACCOUNT");
        var result = Result([ready, missing], new SyncIssue("addon_missing", "generic", missing.SourceId));
        var diagnostics = SourceStatusPresentation.Diagnostics(result, [Source(ready), Source(missing)], false);
        Assert.Contains("Retail · READY_ACCOUNT: Bereit", diagnostics); Assert.Contains("Retail · MISSING_ACCOUNT: Hourstone fehlt", diagnostics);
    }
    [Fact]
    public void UnknownInstalledVersionIsLocalizedAndNeverInvented()
    {
        var status = Status(LocalSourceReadiness.AddonOutdated, version: null);
        Assert.Contains("Installierte Version: unbekannt", SourceStatusPresentation.Instruction(status, false));
        Assert.Contains("Installed version: unknown", SourceStatusPresentation.Instruction(status, true));
    }
}
