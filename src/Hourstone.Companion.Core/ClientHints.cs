using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hourstone.Companion.Core;

/// <summary>
/// Display-only hint published beside a device snapshot: which of the device's sources are WoW: Forever installations.
/// Older Companions read only *.json files and never see it. It does not change playtime or merging; readers turn it into the
/// optional foreverSources list of their generated addon data (docs/sync-protocol-v4-forever.md).
/// </summary>
public sealed record ClientHints
{
    public const string Extension = ".clients";
    public const int MaximumBytes = 64 * 1024;
    [JsonRequired] public int FormatVersion { get; init; } = 1;
    [JsonRequired] public string GroupId { get; init; } = "";
    [JsonRequired] public string DeviceId { get; init; } = "";
    [JsonRequired] public List<string> ForeverSources { get; init; } = [];
    public static string Canonical(ClientHints value) =>
        JsonSerializer.Serialize(value with { ForeverSources = [.. value.ForeverSources.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)] }, JsonContract.Options);
    public static ClientHints Parse(string json, string groupId, string deviceId)
    {
        ObservationRules.RejectDuplicateJsonProperties(json);
        var value = JsonSerializer.Deserialize<ClientHints>(json, JsonContract.Options);
        if (value is null || value.FormatVersion != 1 || value.GroupId != groupId || !Guid.TryParseExact(value.DeviceId, "D", out _)
            || !string.Equals(value.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase) || value.ForeverSources is null || value.ForeverSources.Count > 128
            || !value.ForeverSources.All(ObservationRules.ValidSourceId) || value.ForeverSources.Distinct(StringComparer.Ordinal).Count() != value.ForeverSources.Count)
            throw new InvalidDataException("Invalid client hints.");
        return value;
    }
}
