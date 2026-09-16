using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hourstone.Companion.Core;

public sealed record Observation
{
    [JsonRequired] public string SourceId { get; init; } = "";
    [JsonRequired] public string Region { get; init; } = "unknown";
    [JsonRequired] public string Flavor { get; init; } = "";
    [JsonRequired] public string Guid { get; init; } = "";
    [JsonRequired] public string Name { get; init; } = "";
    [JsonRequired] public string Realm { get; init; } = "";
    [JsonRequired, JsonPropertyName("class")] public string Class { get; init; } = "";
    [JsonRequired] public int Level { get; init; }
    [JsonRequired] public double Seconds { get; init; }
    [JsonRequired] public double UpdatedAt { get; init; }
    public double? ServerSeconds { get; init; }
    public double? ServerAt { get; init; }
    [JsonIgnore] public bool Confirmed => ServerSeconds.HasValue && ServerAt.HasValue;
}

public sealed record DeviceSnapshot
{
    [JsonRequired] public int FormatVersion { get; init; } = 1;
    [JsonRequired] public string GroupId { get; init; } = "";
    [JsonRequired] public string DeviceId { get; init; } = "";
    [JsonRequired] public string DeviceName { get; init; } = "";
    [JsonRequired] public long Revision { get; init; }
    [JsonRequired] public List<Observation> Observations { get; init; } = [];
}

public sealed record SourceConfiguration
{
    public string SourceId { get; init; } = "";
    public string WoWRoot { get; init; } = "";
    public string ClientDirectory { get; init; } = "";
    public string AccountName { get; init; } = "";
    public string Region { get; init; } = "unknown";
    public string Flavor { get; init; } = "";
    public bool Enabled { get; init; } = true;
    [JsonIgnore] public string SavedVariablesPath => Path.Combine(ClientDirectory, "WTF", "Account", AccountName, "SavedVariables", "Hourstone.lua");
    [JsonIgnore] public string AddonTocPath => Path.Combine(ClientDirectory, "Interface", "AddOns", "Hourstone", "Hourstone.toc");
    [JsonIgnore] public string DataAddonDirectory => Path.Combine(ClientDirectory, "Interface", "AddOns", "Hourstone_Sync");
}

public sealed record DiscoveredSource
{
    public required SourceConfiguration Configuration { get; init; }
    public string SourceId => Configuration.SourceId;
    public string WoWRoot => Configuration.WoWRoot;
    public string ClientDirectory => Configuration.ClientDirectory;
    public string AccountName => Configuration.AccountName;
    public string Region => Configuration.Region;
    public string Flavor => Configuration.Flavor;
    public string SavedVariablesPath => Configuration.SavedVariablesPath;
    public string DataAddonDirectory => Configuration.DataAddonDirectory;
}

public sealed record CompanionConfiguration
{
    public string DeviceId { get; init; } = "";
    public string DeviceName { get; init; } = Environment.MachineName;
    public string? CloudFolder { get; init; }
    public bool CloudPaused { get; init; }
    public string? GroupId { get; init; }
    public List<SourceConfiguration> Sources { get; init; } = [];
}

public sealed record DeviceStatus(string DeviceId, string DeviceName, long Revision, int CharacterCount, DateTimeOffset LastSeen, bool IsLocal);
public enum LocalSourceReadiness { AddonMissing, AddonOutdated, AwaitingGameSave, Ready, ReadFailed }
public sealed record LocalSourceStatus(string SourceId, string ClientDirectory, string AccountName, string Flavor,
    LocalSourceReadiness Readiness, string? DetectedAddonVersion, string? Message = null);
public sealed record SyncIssue(string Code, string Message, string? SourceId = null);
public sealed record SyncResult(DateTimeOffset CompletedAt, int CharacterCount, int SourceCount, int DeviceCount, IReadOnlyList<SyncIssue> Issues)
{
    public IReadOnlyList<LocalSourceStatus> LocalSourceStatuses { get; init; } = [];
    public bool CloudPublished { get; init; }
    public bool AddonReady { get; init; }
    public bool Success => Issues.Count == 0;
    public static SyncResult Empty { get; } = new(DateTimeOffset.MinValue, 0, 0, 0, []);
}

public static class JsonContract
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
        MaxDepth = 16
    };
}
