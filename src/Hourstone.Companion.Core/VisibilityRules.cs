namespace Hourstone.Companion.Core;

/// <summary>Monotone remove/restore acknowledgments, independent of playtime and wall clocks.</summary>
public static class VisibilityRules
{
    public const long MaximumSequence = 9007199254740991L;
    public const int MaximumActors = 1024;
    public const int MaximumStates = 10000;

    public static string Identity(CharacterVisibility value) => value.Region == "unknown"
        ? $"unknown|{value.SourceId}|{value.Flavor}|{value.Guid}"
        : $"{value.Region}|{value.Flavor}|{value.Guid}";

    public static CharacterVisibility FromObservation(Observation observation)
    {
        ObservationRules.Validate(observation);
        return new CharacterVisibility { SourceId = observation.SourceId, Region = observation.Region, Flavor = observation.Flavor, Guid = observation.Guid };
    }

    public static void Validate(CharacterVisibility state)
    {
        if (state is null || !ObservationRules.ValidSourceId(state.SourceId) ||
            !ObservationRules.Regions.Contains(state.Region, StringComparer.Ordinal) ||
            !ObservationRules.Flavors.Contains(state.Flavor, StringComparer.Ordinal) || !ObservationRules.ValidText(state.Guid, 128) ||
            state.Removed is null || state.Restored is null || state.Removed.Count > MaximumActors || state.Restored.Count > MaximumActors)
            throw new InvalidDataException("Invalid character visibility identity or actor count.");
        foreach (var (actor, sequence) in state.Removed)
            if (!ObservationRules.ValidSourceId(actor) || sequence is < 1 or > MaximumSequence)
                throw new InvalidDataException("Invalid remove actor or sequence.");
        foreach (var (actor, sequence) in state.Restored)
            if (!ObservationRules.ValidSourceId(actor) || sequence is < 1 or > MaximumSequence ||
                !state.Removed.TryGetValue(actor, out var removed) || sequence > removed)
                throw new InvalidDataException("A restore must acknowledge an observed remove sequence.");
    }

    public static bool IsRemoved(CharacterVisibility state)
    {
        Validate(state);
        return state.Removed.Any(pair => pair.Value > state.Restored.GetValueOrDefault(pair.Key));
    }

    public static IReadOnlyList<CharacterVisibility> Merge(IEnumerable<CharacterVisibility> states)
    {
        var merged = new Dictionary<string, CharacterVisibility>(StringComparer.Ordinal);
        foreach (var state in states)
        {
            Validate(state); var identity = Identity(state);
            if (!merged.TryGetValue(identity, out var old))
            {
                if (merged.Count == MaximumStates) throw new InvalidDataException("Too many character visibility states.");
                merged.Add(identity, Clone(state)); continue;
            }
            var removed = MergeMap(old.Removed, state.Removed); var restored = MergeMap(old.Restored, state.Restored);
            var chosen = ObservationRules.CompareUtf8(old.SourceId, state.SourceId) >= 0 ? old : state;
            var result = chosen with { Removed = removed, Restored = restored };
            Validate(result); merged[identity] = result;
        }
        return merged.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => Clone(pair.Value)).ToArray();
    }

    public static CharacterVisibility Remove(CharacterVisibility state, string actor)
    {
        Validate(state);
        if (!ObservationRules.ValidSourceId(actor)) throw new InvalidDataException("Invalid visibility actor.");
        var next = Clone(state); var sequence = next.Removed.GetValueOrDefault(actor);
        if (sequence == 0 && next.Removed.Count == MaximumActors)
            throw new InvalidDataException($"Visibility actor limit reached ({MaximumActors} actors per character).");
        if (sequence == MaximumSequence) throw new InvalidDataException("Remove sequence limit reached.");
        next.Removed[actor] = sequence + 1; Validate(next); return Clone(next);
    }

    public static CharacterVisibility Restore(CharacterVisibility state)
    {
        Validate(state);
        return Clone(state with { Restored = new Dictionary<string, long>(state.Removed, StringComparer.Ordinal) });
    }

    private static CharacterVisibility Clone(CharacterVisibility state) => state with
    { Removed = Sorted(state.Removed), Restored = Sorted(state.Restored) };

    private static Dictionary<string, long> Sorted(Dictionary<string, long> values) =>
        values.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    private static Dictionary<string, long> MergeMap(Dictionary<string, long> left, Dictionary<string, long> right)
    {
        var result = new Dictionary<string, long>(left, StringComparer.Ordinal);
        foreach (var (actor, sequence) in right) result[actor] = Math.Max(result.GetValueOrDefault(actor), sequence);
        return result;
    }
}
