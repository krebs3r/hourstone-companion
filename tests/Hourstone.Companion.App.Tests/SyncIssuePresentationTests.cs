using Hourstone.Companion.Core;
using Xunit;

namespace Hourstone.Companion.App.Tests;

public sealed class SyncIssuePresentationTests
{
    private static readonly SyncIssue CloudIssue = new("cloud_file_not_local", "Unlocalized core detail.")
    {
        FilePath = @"C:\Proton Drive\HourstoneSync\device-laptop.json"
    };
    private static SyncResult Result(params SyncIssue[] issues) => SyncResult.Empty with { Issues = issues };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CloudDiagnosticsNameTheFileCauseAndBothPcsOfflineAction(bool english)
    {
        var diagnostic = SourceStatusPresentation.Diagnostics(Result(CloudIssue), [], english);
        Assert.Contains("cloud_file_not_local:", diagnostic);
        Assert.Contains(CloudIssue.FilePath!, diagnostic);
        Assert.Contains(english ? "not yet fully available locally" : "noch nicht vollständig lokal verfügbar", diagnostic);
        Assert.Contains(english ? "Always keep on this device" : "Immer auf diesem Gerät behalten", diagnostic);
        Assert.Contains(english ? "on both PCs" : "auf beiden PCs", diagnostic);
        Assert.Contains(english ? "wait for the download" : "warte auf den Download", diagnostic);
        Assert.Contains(english ? "Last valid data is kept" : "Gültige Daten bleiben erhalten", diagnostic);
        Assert.Contains(english ? "retry automatically" : "automatisch erneut", diagnostic);
        Assert.DoesNotContain(CloudIssue.Message, diagnostic);
        Assert.DoesNotContain("snapshot_rejected", diagnostic);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReadErrorsPreserveTechnicalCauseWithoutClaimingInvalidContents(bool english)
    {
        const string cause = "The process cannot access the file because another process is using it.";
        var issue = new SyncIssue("snapshot_read_failed", cause) { FilePath = CloudIssue.FilePath };
        var diagnostic = SourceStatusPresentation.Diagnostics(Result(issue), [], english);
        Assert.Contains(issue.FilePath!, diagnostic);
        Assert.Contains(cause, diagnostic);
        Assert.Contains(english ? "could not be read" : "konnte nicht gelesen werden", diagnostic);
        Assert.Contains(english ? "Check file access" : "Prüfe den Dateizugriff", diagnostic);
        Assert.Contains(english ? "retry automatically" : "automatisch erneut", diagnostic);
        Assert.DoesNotContain(english ? "invalid" : "ungültig", diagnostic);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedInitialJoinExplainsThatFolderSelectionMustBeRetried(bool english)
    {
        var diagnostic = SyncIssuePresentation.Diagnostics(CloudIssue, english, joiningSyncFolder: true);
        Assert.NotNull(diagnostic);
        Assert.Contains(CloudIssue.FilePath!, diagnostic);
        Assert.Contains(english ? "Always keep on this device" : "Immer auf diesem Gerät behalten", diagnostic);
        Assert.Contains(english ? "select the sync folder again" : "Wähle den Sync-Ordner anschließend erneut aus", diagnostic);
        Assert.DoesNotContain(english ? "retry automatically" : "automatisch erneut", diagnostic);
        Assert.DoesNotContain(CloudIssue.Message, diagnostic);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RejectedSnapshotsRetainValidationDetailsAndFile(bool english)
    {
        var issue = new SyncIssue("snapshot_rejected", "Unsupported schema version: 99.") { FilePath = CloudIssue.FilePath };
        var diagnostic = SourceStatusPresentation.Diagnostics(Result(issue), [], english);
        Assert.Contains(issue.FilePath!, diagnostic);
        Assert.Contains(issue.Message, diagnostic);
        Assert.Contains(english ? "invalid or unsupported" : "ungültigen oder nicht unterstützten", diagnostic);
        Assert.DoesNotContain(english ? "Always keep" : "Immer auf diesem Gerät", diagnostic);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CloudBannerExplainsAvailabilityAndRefersToDiagnosticPath(bool english)
    {
        var notice = SyncIssuePresentation.Notice(Result(CloudIssue), english);
        Assert.NotNull(notice);
        Assert.Contains(english ? "Always keep on this device" : "Immer auf diesem Gerät behalten", notice);
        Assert.Contains(english ? "on both PCs" : "auf beiden PCs", notice);
        Assert.Contains(english ? "Synchronization → Local diagnostics" : "Synchronisierung → Lokale Diagnose", notice);
        Assert.DoesNotContain(CloudIssue.FilePath!, notice);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LocalAccountNeedsKeepPriorityOverCloudNotice(bool english)
    {
        var result = Result(CloudIssue, new SyncIssue("source_not_initialized", "Needs a save.", "local-account")) with
        {
            LocalSourceStatuses = [new("local-account", @"C:\WoW\_retail_", "LOCAL_ACCOUNT", "retail", LocalSourceReadiness.AwaitingGameSave, "0.3.1")]
        };
        var notice = SyncIssuePresentation.Notice(result, english);
        Assert.NotNull(notice);
        Assert.Contains(english ? "1 account source(s)" : "1 Account-Quelle(n)", notice);
        Assert.Contains("Clients", notice);
        Assert.DoesNotContain(english ? "Always keep" : "Immer auf diesem Gerät", notice);
        Assert.Contains(CloudIssue.FilePath!, SourceStatusPresentation.Diagnostics(result, [], english));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RecoveryRemovesTheCloudNoticeAndDiagnostics(bool english)
    {
        Assert.Null(SyncIssuePresentation.Notice(Result(), english));
        Assert.DoesNotContain("cloud_file_not_local", SourceStatusPresentation.Diagnostics(Result(), [], english));
        var remaining = Result(new SyncIssue("snapshot_rejected", "Invalid JSON."));
        var notice = SyncIssuePresentation.Notice(remaining, english);
        Assert.NotNull(notice);
        Assert.Contains(english ? "Sync needs attention" : "Der Abgleich benötigt Aufmerksamkeit", notice);
        Assert.DoesNotContain(english ? "Always keep" : "Immer auf diesem Gerät", notice);
    }
}
