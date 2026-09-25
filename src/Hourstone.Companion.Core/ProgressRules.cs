namespace Hourstone.Companion.Core;

/// <summary>Progress families are independent whole-record registers. Source provenance is retained in transport.</summary>
public static class ProgressRules
{
    public const int MaximumObservations = 10000;
    public const long MaximumNumber = 9007199254740991;
    private static readonly IComparer<string> Utf8Order = Comparer<string>.Create(ObservationRules.CompareUtf8);
    public static bool SafeInteger(long value) => value is >= 0 and <= MaximumNumber;
    public static string Identity(ProgressObservation value) => value.Region == "unknown"
        ? $"unknown|{value.SourceId}|{value.Flavor}|{value.Guid}" : $"{value.Region}|{value.Flavor}|{value.Guid}";
    public static ProgressState State(long updatedAt, long resetAt, long now) => updatedAt <= now && now < resetAt ? ProgressState.Known : ProgressState.Stale;
    public static ProgressState State(StampedProgress? family, DateTimeOffset now) => family is null ? ProgressState.Unknown : State(family.UpdatedAt, family.ResetAt, now.ToUnixTimeSeconds());

    public static void Validate(ProgressObservation item)
    {
        if (item is null || !ObservationRules.ValidSourceId(item.SourceId) || !ObservationRules.Regions.Contains(item.Region, StringComparer.Ordinal) ||
            item.Flavor != "retail" || !ObservationRules.ValidText(item.Guid, 128)) throw new InvalidDataException("Invalid progress identity.");
        if (item.Keystone is not null) Validate(item.Keystone);
        if (item.Weekly is not null) Validate(item.Weekly);
        if (item.Vault is not null)
        {
            if (item.Vault.Rows is null) throw new InvalidDataException("Missing vault rows.");
            foreach (var row in new[] { item.Vault.Rows.Dungeon, item.Vault.Rows.Raid, item.Vault.Rows.World }) if (row is not null) Validate(row);
        }
    }
    public static void Validate(KeystoneProgress item)
    {
        ValidateStamp(item);
        if (!item.Present) return;
        if (item.Present && (item.MapID is null or <= 0 || item.Level is null or <= 0 || !SafeInteger(item.MapID.Value) || !SafeInteger(item.Level.Value)))
            throw new InvalidDataException("A present keystone requires a positive map ID and level.");
        if (item.Name is not null && !ObservationRules.ValidText(item.Name, 1024)) throw new InvalidDataException("Invalid keystone name.");
        if (item.MapID is not null && !SafeInteger(item.MapID.Value) || item.Level is not null && !SafeInteger(item.Level.Value)) throw new InvalidDataException("Invalid keystone values.");
    }
    public static void Validate(WeeklyProgress item)
    {
        ValidateStamp(item);
        if (!SafeInteger(item.Level) || !SafeInteger(item.SeasonID)) throw new InvalidDataException("Invalid weekly progress.");
    }
    public static void Validate(VaultRowProgress item)
    {
        ValidateStamp(item);
        if (item.Slots is null || item.Slots.Count != 3) throw new InvalidDataException("A vault row requires exactly three slots.");
        foreach (var slot in item.Slots)
            if (slot is null || !SafeInteger(slot.Progress) || !SafeInteger(slot.Threshold) || slot.Threshold == 0 || !SafeInteger(slot.Level) ||
                slot.DifficultyName is not null && !ObservationRules.ValidText(slot.DifficultyName, 1024)) throw new InvalidDataException("Invalid vault slot.");
    }
    private static void ValidateStamp(StampedProgress value)
    {
        if (!SafeInteger(value.UpdatedAt) || !SafeInteger(value.ResetAt) || value.ResetAt <= value.UpdatedAt) throw new InvalidDataException("Invalid progress observation time or reset.");
    }
    public static ProgressObservation Normalize(ProgressObservation item)
    {
        Validate(item);
        var rows = item.Vault?.Rows;
        return item with
        {
            Keystone = item.Keystone is { Present: false } key ? key with { MapID = null, Level = null, Name = null } : item.Keystone,
            Vault = rows is null || rows.Dungeon is null && rows.Raid is null && rows.World is null ? null : item.Vault
        };
    }

    /// <summary>Safe for persistence and further merging: every family retains its actual source.</summary>
    public static IReadOnlyList<ProgressObservation> MergeSources(IEnumerable<ProgressObservation> values)
    {
        var bySource = new Dictionary<string, List<ProgressObservation>>(StringComparer.Ordinal);
        foreach (var raw in values)
        {
            var item = Normalize(raw); var key = item.SourceId + "|" + Identity(item);
            if (!bySource.TryGetValue(key, out var items))
            {
                if (bySource.Count == MaximumObservations) throw new InvalidDataException("Too many combined progress observations.");
                bySource.Add(key, items = []);
            }
            // Collapse immediately, keeping bounded memory even for repeated sources.
            if (items.Count == 0) items.Add(item); else items[0] = ProjectGroup([items[0], item]);
        }
        return bySource.Values.Select(items => items[0]).OrderBy(item => item.SourceId, Utf8Order).ThenBy(Identity, Utf8Order).ToArray();
    }

    /// <summary>Terminal display projection. Never persist or republish this mixed-source result as a local observation.</summary>
    public static IReadOnlyList<ProgressObservation> Project(IEnumerable<ProgressObservation> values) =>
        MergeSources(values).GroupBy(Identity, StringComparer.Ordinal).OrderBy(group => group.Key, Utf8Order).Select(ProjectGroup).ToArray();

    private static ProgressObservation ProjectGroup(IEnumerable<ProgressObservation> values)
    {
        var items = values.ToArray();
        var representative = items.Aggregate((a, b) => ObservationRules.CompareUtf8(a.SourceId, b.SourceId) >= 0 ? a : b);
        var rows = new VaultRows
        {
            Dungeon = Pick(items, item => item.Vault?.Rows.Dungeon, CompareRow),
            Raid = Pick(items, item => item.Vault?.Rows.Raid, CompareRow),
            World = Pick(items, item => item.Vault?.Rows.World, CompareRow)
        };
        return representative with
        {
            Keystone = Pick(items, item => item.Keystone, CompareKey), Weekly = Pick(items, item => item.Weekly, CompareWeek),
            Vault = rows.Dungeon is null && rows.Raid is null && rows.World is null ? null : new() { Rows = rows }
        };
    }
    private static T? Pick<T>(ProgressObservation[] items, Func<ProgressObservation, T?> select, Func<T, T, int> tie) where T : StampedProgress
    {
        T? winner = null; var owner = "";
        foreach (var item in items)
        {
            var family = select(item); if (family is null) continue;
            var compare = winner is null ? 1 : family.UpdatedAt.CompareTo(winner.UpdatedAt);
            if (compare == 0) compare = ObservationRules.CompareUtf8(item.SourceId, owner);
            if (compare == 0) compare = tie(family, winner!);
            if (compare > 0) { winner = family; owner = item.SourceId; }
        }
        return winner;
    }
    private static int CompareKey(KeystoneProgress a, KeystoneProgress b)
    {
        var c = a.ResetAt.CompareTo(b.ResetAt); if (c != 0) return c;
        c = a.Present.CompareTo(b.Present); if (c != 0) return c;
        c = (a.MapID ?? 0).CompareTo(b.MapID ?? 0); if (c != 0) return c;
        c = (a.Level ?? 0).CompareTo(b.Level ?? 0); return c != 0 ? c : ObservationRules.CompareUtf8(a.Name ?? "", b.Name ?? "");
    }
    private static int CompareWeek(WeeklyProgress a, WeeklyProgress b)
    {
        var c = a.ResetAt.CompareTo(b.ResetAt); if (c != 0) return c;
        c = a.Level.CompareTo(b.Level); return c != 0 ? c : a.SeasonID.CompareTo(b.SeasonID);
    }
    private static int CompareRow(VaultRowProgress a, VaultRowProgress b)
    {
        var c = a.ResetAt.CompareTo(b.ResetAt); if (c != 0) return c;
        for (var i = 0; i < 3; i++)
        {
            var left = a.Slots[i]; var right = b.Slots[i];
            c = left.Progress.CompareTo(right.Progress); if (c != 0) return c;
            c = left.Threshold.CompareTo(right.Threshold); if (c != 0) return c;
            c = left.Level.CompareTo(right.Level); if (c != 0) return c;
            c = ObservationRules.CompareUtf8(left.DifficultyName ?? "", right.DifficultyName ?? ""); if (c != 0) return c;
        }
        return 0;
    }
}
