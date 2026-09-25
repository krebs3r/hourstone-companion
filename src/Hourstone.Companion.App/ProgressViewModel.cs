using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using Hourstone.Companion.Core;

namespace Hourstone.Companion.App;

/// <summary>The Retail-only projection; playtime filters and hidden characters never leak into it.</summary>
public sealed class ProgressViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    void Changed([CallerMemberName] string? property = null) => PropertyChanged?.Invoke(this, new(property));
    readonly Func<long> now;
    List<Observation> characters = [];
    Dictionary<string, ProgressObservation> progress = new(StringComparer.Ordinal);
    bool english, light;
    string search = "", realm = "";
    ProgressCharacterRow? selected;
    public ProgressViewModel() : this(() => DateTimeOffset.UtcNow.ToUnixTimeSeconds()) { }
    public ProgressViewModel(Func<long> clock) => now = clock;
    public ObservableCollection<ProgressCharacterRow> Rows { get; } = [];
    public ObservableCollection<string> Realms { get; } = [];
    public string Search { get => search; set { search = value ?? ""; RebuildRows(); Changed(); } }
    public string SelectedRealm { get => realm; set { realm = value ?? ""; RebuildRows(); Changed(); } }
    public ProgressCharacterRow? Selected
    {
        get => selected;
        set { selected = value is not null && Rows.Contains(value) ? value : null; Changed(); Changed(nameof(DetailsVisibility)); }
    }
    public Visibility DetailsVisibility => Selected is null ? Visibility.Collapsed : Visibility.Visible;
    public Visibility EmptyVisibility => Rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public string EmptyMessage => characters.Count == 0 ? (english ? "No Retail characters recorded yet." : "Noch keine Retail-Charaktere vorhanden.") : (english ? "No matching characters." : "Keine passenden Charaktere.");
    public string Subtitle => english ? "Your Retail characters at a glance." : "Deine Retail-Charaktere im Überblick.";
    public string Title => english ? "Retail progress" : "Retail-Fortschritt";
    public string SavedState => english ? "Saved progress of your characters" : "Gespeicherter Stand deiner Charaktere";
    public string SearchLabel => english ? "Search characters" : "Charakter suchen";
    public string AllRealms => english ? "All realms" : "Alle Realms";
    public string CharacterLabel => english ? "Character" : "Charakter";
    public string KeystoneLabel => english ? "Keystone" : "Schlüsselstein";
    public string WeeklyLabel => english ? "Weekly best" : "Wochenbestwert";
    public string DungeonLabel => "Dungeons";
    public string RaidLabel => english ? "Raids" : "Schlachtzüge";
    public string WorldLabel => english ? "World" : "Welt";
    public string UnlockedLabel => english ? "Unlocked" : "Freigeschaltet";
    public string PendingLabel => english ? "Still locked" : "Noch offen";
    public string UnknownLabel => english ? "Unknown" : "Unbekannt";
    public string StaleLabel => english ? "Outdated" : "Veraltet";
    public string CloseDetails => english ? "Close details" : "Details schließen";
    public string Hint => english ? "Select a character for details. New data is read after logout or /reload." : "Wähle einen Charakter für Details. Neue Daten werden nach dem Ausloggen oder /reload übernommen.";
    public void SetAppearance(bool isEnglish, bool isLight)
    {
        english = isEnglish; light = isLight; RebuildRealms(); Changed(null);
    }
    public void SetCharacters(IEnumerable<Observation> values)
    {
        characters = values.Where(c => c.Flavor == "retail").ToList(); RebuildRealms();
    }
    public void SetProgress(IEnumerable<ProgressObservation> values)
    {
        progress = values.Where(p => p.Flavor == "retail").GroupBy(ProgressRules.Identity, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        RebuildRows();
    }
    void RebuildRealms()
    {
        var oldRealm = realm;
        Realms.Clear(); Realms.Add(AllRealms);
        foreach (var value in characters.Select(c => c.Realm).Distinct(StringComparer.Ordinal).Order(StringComparer.CurrentCulture)) Realms.Add(value);
        realm = Realms.Contains(oldRealm) ? oldRealm : AllRealms;
        Changed(nameof(SelectedRealm)); RebuildRows();
    }
    void RebuildRows()
    {
        var identity = selected?.Identity;
        Rows.Clear();
        foreach (var c in characters.Where(c => (realm.Length == 0 || realm == AllRealms || c.Realm == realm) &&
            (c.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase) || c.Realm.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
             CharacterRow.ClassName(c.Class, english).Contains(search, StringComparison.CurrentCultureIgnoreCase) || (c.Guild?.Contains(search, StringComparison.CurrentCultureIgnoreCase) ?? false)))
            .OrderBy(c => c.Name, StringComparer.CurrentCulture).ThenBy(c => c.Realm, StringComparer.CurrentCulture))
        {
            progress.TryGetValue(ObservationRules.Identity(c), out var p);
            Rows.Add(new(c, p, english, light, now()));
        }
        Selected = Rows.FirstOrDefault(r => r.Identity == identity);
        Changed(nameof(EmptyVisibility)); Changed(nameof(EmptyMessage));
    }

    public static IReadOnlyList<ProgressObservation> DemoProgress(IEnumerable<Observation> observations)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return observations.Where(c => c.Flavor == "retail").Take(3).Select((c, i) =>
        {
            var at = i == 2 ? timestamp - 604800 : timestamp - 120;
            var reset = i == 2 ? timestamp - 86400 : timestamp + 172800;
            VaultRowProgress Row(int count, long[] thresholds, string difficulty) => new()
            {
                UpdatedAt = at, ResetAt = reset,
                Slots = thresholds.Select((threshold, slot) => new VaultSlot { Progress = count, Threshold = threshold, Level = count == 0 ? 0 : slot == 0 ? 11 : 10, DifficultyName = difficulty, Unlocked = count >= threshold }).ToList()
            };
            return new ProgressObservation
            {
                SourceId = c.SourceId, Region = c.Region, Flavor = c.Flavor, Guid = c.Guid,
                Keystone = new() { Present = i != 1, MapID = i == 1 ? null : 500, Level = i == 1 ? null : 10, Name = i == 1 ? null : "Dunkelflammenspalt", UpdatedAt = at, ResetAt = reset },
                Weekly = new() { Level = i == 1 ? 0 : 11, SeasonID = 1, UpdatedAt = at, ResetAt = reset },
                Vault = new() { Rows = new() { Dungeon = Row(i == 1 ? 0 : 4, [1, 4, 8], "Mythisch+"), Raid = Row(i == 1 ? 0 : 5, [2, 4, 6], "Heroisch"), World = Row(i == 1 ? 0 : 2, [2, 4, 8], "Welt") } }
            };
        }).ToArray();
    }
}

public sealed class ProgressCharacterRow
{
    public Observation Character { get; }
    public string Identity => ObservationRules.Identity(Character);
    public string Name => Character.Name;
    public string Realm => Character.Realm;
    public string ClassIconPath => IconCatalog.ClassIconPath(Character.Class);
    public string Description { get; }
    public string Keystone { get; }
    public string KeystoneName { get; }
    public string KeystoneStatus { get; }
    public string KeystoneWarning { get; }
    public string Weekly { get; }
    public string WeeklyStatus { get; }
    public string WeeklyWarning { get; }
    public string DetailsHint { get; }
    public Visibility HintVisibility => DetailsHint.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    public IReadOnlyList<ProgressSlotView> Dungeons { get; }
    public IReadOnlyList<ProgressSlotView> Raids { get; }
    public IReadOnlyList<ProgressSlotView> World { get; }
    public IReadOnlyList<ProgressVaultRowView> VaultRows { get; }
    public ProgressCharacterRow(Observation character, ProgressObservation? value, bool english, bool light, long now)
    {
        Character = character;
        Description = character.Realm + " · " + CharacterRow.ClassName(character.Class, english);
        var unknown = english ? "Unknown" : "Unbekannt";
        var stale = english ? "Outdated" : "Veraltet";
        bool Current(long updated, long reset) => updated <= now && now < reset;
        string Stamp(long updated, long reset) => (Current(updated, reset) ? "" : stale + " · ") +
            DateTimeOffset.FromUnixTimeSeconds(Math.Clamp(updated, 0, 253402300799)).ToLocalTime().ToString("g", CultureInfo.GetCultureInfo(english ? "en-US" : "de-DE"));
        Keystone = value?.Keystone is { } key ? key.Present ? "+" + key.Level : english ? "None" : "Keiner" : unknown;
        KeystoneName = value?.Keystone is { Present: true } k ? k.Name ?? (english ? "Dungeon" : "Dungeon") + " #" + k.MapID : "";
        KeystoneStatus = value?.Keystone is { } ks ? Stamp(ks.UpdatedAt, ks.ResetAt) : unknown;
        KeystoneWarning = value?.Keystone is { } kw && !Current(kw.UpdatedAt, kw.ResetAt) ? stale : "";
        Weekly = value?.Weekly is { } week ? week.Level > 0 ? "+" + week.Level : english ? "None" : "Keiner" : unknown;
        WeeklyStatus = value?.Weekly is { } ws ? Stamp(ws.UpdatedAt, ws.ResetAt) : unknown;
        WeeklyWarning = value?.Weekly is { } ww && !Current(ww.UpdatedAt, ww.ResetAt) ? stale : "";
        Dungeons = Slots(value?.Vault?.Rows.Dungeon, "dungeon"); Raids = Slots(value?.Vault?.Rows.Raid, "raid"); World = Slots(value?.Vault?.Rows.World, "world");
        string RowStamp(VaultRowProgress? row) => row is null ? unknown : Stamp(row.UpdatedAt, row.ResetAt);
        VaultRows = [new("Dungeons", Dungeons, RowStamp(value?.Vault?.Rows.Dungeon)), new(english ? "Raids" : "Schlachtzüge", Raids, RowStamp(value?.Vault?.Rows.Raid)), new(english ? "World" : "Welt", World, RowStamp(value?.Vault?.Rows.World))];
        var dates = new List<(long Updated, long Reset)>();
        if (value?.Keystone is { } pk) dates.Add((pk.UpdatedAt, pk.ResetAt));
        if (value?.Weekly is { } pw) dates.Add((pw.UpdatedAt, pw.ResetAt));
        foreach (var row in new[] { value?.Vault?.Rows.Dungeon, value?.Vault?.Rows.Raid, value?.Vault?.Rows.World }) if (row is not null) dates.Add((row.UpdatedAt, row.ResetAt));
        DetailsHint = dates.Count == 0 ? english ? "No progress recorded yet. Log in with this character using Hourstone 0.3.1 or later, then log out or /reload." : "Noch keine Fortschrittsdaten. Melde dich mit diesem Charakter und Hourstone 0.3.1 oder neuer an, anschließend ausloggen oder /reload ausführen."
            : dates.Any(d => !Current(d.Updated, d.Reset)) ? english ? "Some saved values are outdated. Current progress has not been recorded for those areas yet." : "Einige gespeicherte Werte sind veraltet. Für diese Bereiche ist der aktuelle Fortschritt noch nicht bekannt." : "";
        IReadOnlyList<ProgressSlotView> Slots(VaultRowProgress? row, string family) => Enumerable.Range(0, 3).Select(index =>
            new ProgressSlotView(row?.Slots.ElementAtOrDefault(index), row?.UpdatedAt, row?.ResetAt, family, english, light, now)).ToArray();
    }
}
public sealed record ProgressVaultRowView(string Name, IReadOnlyList<ProgressSlotView> Slots, string RecordedAt);
public sealed class ProgressSlotView
{
    public string Symbol { get; }
    public string Background { get; }
    public string Foreground { get; }
    public string Progress { get; }
    public string Difficulty { get; }
    public string Tooltip { get; }
    public string Status { get; }
    public bool Known { get; }
    public bool IsStale { get; }
    public ProgressSlotView(VaultSlot? slot, long? updated, long? reset, string family, bool english, bool light, long now)
    {
        Known = slot is not null;
        IsStale = Known && (updated > now || reset <= now);
        bool unlocked = slot is not null && slot.Progress >= slot.Threshold;
        Status = !Known ? english ? "Unknown" : "Unbekannt" : IsStale ? english ? "Outdated" : "Veraltet" : unlocked ? english ? "Unlocked" : "Freigeschaltet" : english ? "Still locked" : "Noch offen";
        Symbol = !Known ? "?" : IsStale ? "◷" : unlocked ? "✓" : "·";
        Background = IsStale ? light ? "#F7E9CF" : "#493E2C" : unlocked ? light ? "#D6EFE2" : "#264539" : light ? "#EDF1F5" : "#243646";
        Foreground = IsStale ? light ? "#8A5709" : "#F1C784" : unlocked ? light ? "#226B45" : "#8CD8AD" : light ? "#536B7E" : "#A6BFD8";
        var unit = family switch { "dungeon" => "Dungeons", "raid" => english ? "bosses" : "Bosse", _ => english ? "activities" : "Aktivitäten" };
        Progress = slot is null ? english ? "Not recorded" : "Noch nicht erfasst" : $"{slot.Progress} / {slot.Threshold} {unit}";
        Difficulty = FormatDifficulty(slot, family, english);
        Tooltip = Progress + " · " + Status + (Difficulty.Length == 0 ? "" : "\n" + Difficulty);
        if (updated.HasValue) Tooltip += "\n" + (english ? "Recorded: " : "Stand: ") + DateTimeOffset.FromUnixTimeSeconds(Math.Clamp(updated.Value, 0, 253402300799)).ToLocalTime().ToString("g", CultureInfo.GetCultureInfo(english ? "en-US" : "de-DE"));
    }
    static string FormatDifficulty(VaultSlot? slot, string family, bool english)
    {
        if (slot is null) return "";
        var name = slot.DifficultyName ?? "";
        if (slot.Level <= 0 || family == "raid" && name.Length > 0) return name;
        var level = family == "dungeon" ? "+" + slot.Level : (english ? "Level " : "Stufe ") + slot.Level;
        return name.Length == 0 ? level : name + " · " + level;
    }
}
