using System.Globalization;

namespace Hourstone.Companion.Core;

public static partial class SavedVariablesReader
{
    private sealed record ProgressReadResult(ProgressReadStatus Status, IReadOnlyList<ProgressObservation> Values, IReadOnlyList<string> Issues);
    private static ProgressReadResult ReadProgress(object? value, SourceConfiguration source, string sourceId)
    {
        if (value is null || source.Flavor != "retail") return new(ProgressReadStatus.Unavailable, [], []);
        var issues = new List<string>();
        void Issue(string message) { if (issues.Count < 32) issues.Add(message); }
        try
        {
            var cache = Table(value, "progress");
            if (cache.GetValueOrDefault("version") is not double version || version != Math.Truncate(version) || version < 1)
                throw new InvalidDataException("Invalid progress cache version.");
            if (version != 1) return new(ProgressReadStatus.Unsupported, [], ["Diese Fortschrittsdaten stammen von einer neueren Addon-Version; der letzte gültige Stand bleibt verfügbar."]);
            var records = Table(cache.GetValueOrDefault("characters"), "progress.characters");
            if (records.Count > ProgressRules.MaximumObservations) throw new InvalidDataException("Too many local progress observations.");
            var values = new List<ProgressObservation>();
            foreach (var raw in records.Values)
            {
                try
                {
                    var record = Table(raw, "progress character");
                    if (record.GetValueOrDefault("imported") is true || record.GetValueOrDefault("isImported") is true) continue;
                    var owner = String(record, "sourceId", false);
                    if (owner.Length > 0 && owner != sourceId) continue;
                    if (String(record, "flavor") != "retail") continue;
                    var item = new ProgressObservation
                    {
                        SourceId = sourceId, Region = String(record, "region"), Flavor = "retail", Guid = String(record, "guid")
                    };
                    ProgressRules.Validate(item);
                    T? Family<T>(object? family, string name, Func<Dictionary<string, object?>, T> read) where T : class
                    {
                        if (family is null) return null;
                        try { return read(Table(family, name)); }
                        catch (InvalidDataException ex) { Issue($"{item.Guid} / {name}: {ex.Message}"); return null; }
                    }
                    Dictionary<string, object?>? rows = null;
                    if (record.GetValueOrDefault("vault") is { } vault)
                    {
                        try { rows = Table(Table(vault, "vault").GetValueOrDefault("rows"), "vault.rows"); }
                        catch (InvalidDataException ex) { Issue($"{item.Guid} / vault: {ex.Message}"); }
                    }
                    item = item with
                    {
                        Keystone = Family(record.GetValueOrDefault("keystone"), "keystone", ReadKey),
                        Weekly = Family(record.GetValueOrDefault("weekly"), "weekly", ReadWeek),
                        Vault = rows is null ? null : new VaultProgress { Rows = new VaultRows
                        {
                            Dungeon = Family(rows.GetValueOrDefault("dungeon"), "vault.dungeon", ReadRow),
                            Raid = Family(rows.GetValueOrDefault("raid"), "vault.raid", ReadRow),
                            World = Family(rows.GetValueOrDefault("world"), "vault.world", ReadRow)
                        } }
                    };
                    values.Add(ProgressRules.Normalize(item));
                }
                catch (InvalidDataException ex) { Issue(ex.Message); }
            }
            return new(ProgressReadStatus.Ready, ProgressRules.MergeSources(values), issues);
        }
        catch (InvalidDataException ex) { return new(ProgressReadStatus.Invalid, [], [ex.Message]); }
    }
    private static Dictionary<string, object?> Table(object? value, string name) => value as Dictionary<string, object?> ?? throw new InvalidDataException($"Invalid {name} table.");
    private static long Integer(Dictionary<string, object?> table, string name)
    {
        var number = Number(table, name);
        if (number != Math.Truncate(number)) throw new InvalidDataException($"Invalid integer {name}.");
        return (long)number;
    }
    private static KeystoneProgress ReadKey(Dictionary<string, object?> row)
    {
        if (row.GetValueOrDefault("present") is not bool present) throw new InvalidDataException("Missing keystone presence.");
        var result = new KeystoneProgress
        {
            Present = present, MapID = present ? Integer(row, "mapID") : null, Level = present ? Integer(row, "level") : null,
            Name = present ? OptionalString(row, "name") : null, UpdatedAt = Integer(row, "updatedAt"), ResetAt = Integer(row, "resetAt")
        };
        ProgressRules.Validate(result); return result;
    }
    private static WeeklyProgress ReadWeek(Dictionary<string, object?> row)
    {
        var result = new WeeklyProgress { Level = Integer(row, "level"), SeasonID = Integer(row, "seasonID"), UpdatedAt = Integer(row, "updatedAt"), ResetAt = Integer(row, "resetAt") };
        ProgressRules.Validate(result); return result;
    }
    private static VaultRowProgress ReadRow(Dictionary<string, object?> row)
    {
        var slots = Table(row.GetValueOrDefault("slots"), "vault.slots");
        if (slots.Count != 3) throw new InvalidDataException("Vault rows require exactly three slots.");
        var result = new VaultRowProgress { UpdatedAt = Integer(row, "updatedAt"), ResetAt = Integer(row, "resetAt"), Slots = [] };
        for (var index = 1; index <= 3; index++)
        {
            var slot = Table(slots.GetValueOrDefault(index.ToString(CultureInfo.InvariantCulture)), "vault slot");
            result.Slots.Add(new VaultSlot { Progress = Integer(slot, "progress"), Threshold = Integer(slot, "threshold"), Level = Integer(slot, "level"), DifficultyName = OptionalString(slot, "difficultyName") });
        }
        ProgressRules.Validate(result); return result;
    }
}
