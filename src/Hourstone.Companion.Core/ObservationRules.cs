using System.Text.Json;

namespace Hourstone.Companion.Core;

public static class ObservationRules
{
    public const double MaximumNumber = 9007199254740991d;
    public const int MaximumObservations = 10000;
    public static readonly string[] Regions = ["us", "kr", "eu", "tw", "cn", "unknown"];
    public static readonly string[] Flavors = ["retail", "mists", "tbc", "era"];
    public static bool Finite(double value) => double.IsFinite(value) && value >= 0 && value <= MaximumNumber;
    public static bool ValidSourceId(string? value) => value is { Length: > 0 and <= 128 } && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    public static void Validate(Observation item)
    {
        if (item is null || !ValidSourceId(item.SourceId) || !Regions.Contains(item.Region, StringComparer.Ordinal) || !Flavors.Contains(item.Flavor, StringComparer.Ordinal))
            throw new InvalidDataException("Invalid observation source, region or client family.");
        if (!ValidText(item.Guid, 128) ||
            !ValidText(item.Name, 128) || !ValidText(item.Realm, 128) || !ValidText(item.Class, 32) || item.Level is < 0 or > 1000)
            throw new InvalidDataException("Invalid character identity.");
        if ((item.Guild is null) != (item.GuildUpdatedAt is null) ||
            item.Guild is not null && ((item.Guild.Length > 0 && !ValidText(item.Guild, 128)) || !Finite(item.GuildUpdatedAt!.Value)))
            throw new InvalidDataException("Invalid guild membership or incomplete guild timestamp.");
        if (!Finite(item.Seconds) || !Finite(item.UpdatedAt) || item.ServerSeconds.HasValue != item.ServerAt.HasValue)
            throw new InvalidDataException("Invalid playtime values or incomplete server baseline.");
        if (item.Confirmed && (!Finite(item.ServerSeconds!.Value) || !Finite(item.ServerAt!.Value) || item.Seconds < item.ServerSeconds.Value || item.UpdatedAt < item.ServerAt.Value))
            throw new InvalidDataException("Invalid confirmed server baseline.");
    }
    public static bool ValidText(string? value, int limit) => value is { Length: > 0 } && System.Text.Encoding.UTF8.GetByteCount(value) <= limit && !value.Any(c => c < 32 || c == 127);
    public static string Identity(Observation value) => value.Region == "unknown"
        ? $"unknown|{value.SourceId}|{value.Flavor}|{value.Guid}"
        : $"{value.Region}|{value.Flavor}|{value.Guid}";
    public static int Compare(Observation left, Observation right)
    {
        var comparison = left.Confirmed.CompareTo(right.Confirmed);
        if (comparison != 0) return comparison;
        if (left.Confirmed)
        {
            comparison = left.ServerAt!.Value.CompareTo(right.ServerAt!.Value); if (comparison != 0) return comparison;
            comparison = left.ServerSeconds!.Value.CompareTo(right.ServerSeconds!.Value); if (comparison != 0) return comparison;
        }
        comparison = left.UpdatedAt.CompareTo(right.UpdatedAt); if (comparison != 0) return comparison;
        comparison = left.Seconds.CompareTo(right.Seconds); if (comparison != 0) return comparison;
        foreach (var pair in new[] { (left.SourceId, right.SourceId), (left.Name, right.Name), (left.Realm, right.Realm), (left.Class, right.Class) })
        { comparison = CompareUtf8(pair.Item1, pair.Item2); if (comparison != 0) return comparison; }
        return left.Level.CompareTo(right.Level);
    }
    public static int CompareUtf8(string left, string right) => System.Text.Encoding.UTF8.GetBytes(left).AsSpan().SequenceCompareTo(System.Text.Encoding.UTF8.GetBytes(right));
    public static IReadOnlyList<Observation> Merge(IEnumerable<Observation> observations)
    {
        var byIdentity = new Dictionary<string, Observation>(StringComparer.Ordinal);
        foreach (var observation in observations)
        {
            Validate(observation); var identity = Identity(observation);
            if (!byIdentity.TryGetValue(identity, out var old)) byIdentity[identity] = observation;
            else
            {
                var winner = Compare(observation, old) > 0 ? observation : old;
                var guild = old.Guild is null ? observation : observation.Guild is null ? old :
                    observation.GuildUpdatedAt > old.GuildUpdatedAt ||
                    observation.GuildUpdatedAt == old.GuildUpdatedAt && CompareUtf8(observation.Guild, old.Guild) > 0 ? observation : old;
                byIdentity[identity] = winner with { Guild = guild.Guild, GuildUpdatedAt = guild.GuildUpdatedAt };
            }
        }
        return byIdentity.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Value).ToList();
    }
    public static void ValidateSnapshot(DeviceSnapshot snapshot, string groupId)
    {
        if (snapshot is null || snapshot.FormatVersion is not (1 or 2 or 3) || !System.Guid.TryParseExact(snapshot.GroupId, "D", out _) || snapshot.GroupId != groupId ||
            !System.Guid.TryParseExact(snapshot.DeviceId, "D", out _) || !ValidText(snapshot.DeviceName, 128) || snapshot.Revision <= 0 ||
            snapshot.Observations is null || snapshot.Observations.Count > MaximumObservations)
            throw new InvalidDataException("Unsupported or invalid device snapshot.");
        if (snapshot.FormatVersion == 3 ? snapshot.Visibility is null : snapshot.Visibility is not null)
            throw new InvalidDataException("Visibility requires snapshot format 3 and a complete visibility list.");
        if (snapshot.Visibility is not null)
        {
            if (snapshot.Visibility.Count > VisibilityRules.MaximumStates) throw new InvalidDataException("Too many visibility states.");
            var identities = new HashSet<string>(StringComparer.Ordinal);
            foreach (var state in snapshot.Visibility)
            {
                VisibilityRules.Validate(state);
                if (!identities.Add(VisibilityRules.Identity(state))) throw new InvalidDataException("Duplicate visibility identity in device snapshot.");
            }
        }
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var observation in snapshot.Observations)
        {
            Validate(observation);
            if (snapshot.FormatVersion == 1 && (observation.Guild is not null || observation.GuildUpdatedAt is not null))
                throw new InvalidDataException("Guild membership requires snapshot format 2.");
            if (!keys.Add(observation.SourceId + "|" + Identity(observation))) throw new InvalidDataException("Duplicate observation in device snapshot.");
        }
    }
    public static DeviceSnapshot ParseSnapshot(string json, string groupId)
    {
        RejectDuplicateJsonProperties(json);
        var snapshot = JsonSerializer.Deserialize<DeviceSnapshot>(json, JsonContract.Options) ?? throw new InvalidDataException("Empty snapshot.");
        ValidateSnapshot(snapshot, groupId); return snapshot;
    }
    public static string CanonicalSnapshot(DeviceSnapshot snapshot) => JsonSerializer.Serialize(snapshot with
    {
        Observations = snapshot.Observations.OrderBy(o => o.SourceId, StringComparer.Ordinal).ThenBy(Identity, StringComparer.Ordinal).ToList(),
        Visibility = snapshot.Visibility is null ? null : VisibilityRules.Merge(snapshot.Visibility).ToList()
    }, JsonContract.Options);
    internal static void RejectDuplicateJsonProperties(string json)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
        Visit(document.RootElement);
        static void Visit(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject())
                { if (!names.Add(property.Name)) throw new InvalidDataException("Duplicate JSON property."); Visit(property.Value); }
            }
            else if (element.ValueKind == JsonValueKind.Array) foreach (var item in element.EnumerateArray()) Visit(item);
        }
    }
}
