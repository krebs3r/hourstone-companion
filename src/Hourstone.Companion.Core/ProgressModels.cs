using System.Text.Json.Serialization;

namespace Hourstone.Companion.Core;

public sealed record ProgressObservation
{
    [JsonRequired] public string SourceId { get; init; } = "";
    [JsonRequired] public string Region { get; init; } = "unknown";
    [JsonRequired] public string Flavor { get; init; } = "retail";
    [JsonRequired] public string Guid { get; init; } = "";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public KeystoneProgress? Keystone { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WeeklyProgress? Weekly { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public VaultProgress? Vault { get; init; }
}

public abstract record StampedProgress
{
    [JsonRequired] public long UpdatedAt { get; init; }
    [JsonRequired] public long ResetAt { get; init; }
}

public sealed record KeystoneProgress : StampedProgress
{
    [JsonRequired] public bool Present { get; init; }
    [JsonPropertyName("mapID"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public long? MapID { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public long? Level { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Name { get; init; }
}

public sealed record WeeklyProgress : StampedProgress
{
    [JsonRequired] public long Level { get; init; }
    [JsonRequired, JsonPropertyName("seasonID")] public long SeasonID { get; init; }
}

public sealed record VaultProgress
{
    [JsonRequired] public VaultRows Rows { get; init; } = new();
}

public sealed record VaultRows
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public VaultRowProgress? Dungeon { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public VaultRowProgress? Raid { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public VaultRowProgress? World { get; init; }
}

public sealed record VaultRowProgress : StampedProgress
{
    [JsonRequired] public List<VaultSlot> Slots { get; init; } = [];
}

public sealed record VaultSlot
{
    [JsonRequired] public long Progress { get; init; }
    [JsonRequired] public long Threshold { get; init; }
    [JsonRequired] public long Level { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? DifficultyName { get; init; }
    // Transport never gets to assert an unlock independently of progress and threshold.
    public bool Unlocked { get => Progress >= Threshold; init { } }
}

public enum ProgressState { Unknown, Known, Stale, Unavailable }
public enum ProgressReadStatus { Unavailable, Ready, Unsupported, Invalid }
