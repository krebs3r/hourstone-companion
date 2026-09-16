using System.Globalization;
using System.Text;

namespace Hourstone.Companion.Core;

/// <summary>A bounded parser for WoW's literal SavedVariables format. Never executes Lua.</summary>
public static class SavedVariablesReader
{
    public const int MaximumFileBytes = 8 * 1024 * 1024;
    public static ParsedSavedVariables Read(string text, SourceConfiguration source)
    {
        var root = new LiteralParser(text).Parse();
        if (!root.TryGetValue("version", out var schema) || schema is not double version || version is not (1 or 2))
            throw new InvalidDataException("Unsupported Hourstone SavedVariables schema (supported: 1 and 2).");
        var sourceId = root.GetValueOrDefault("sourceId") as string ?? "";
        if (!ObservationRules.ValidSourceId(sourceId)) sourceId = ""; // The addon repairs this on its next in-game save.
        var effectiveId = sourceId.Length > 0 ? sourceId : source.SourceId;
        if (root.GetValueOrDefault("characters") is not Dictionary<string, object?> characters)
            throw new InvalidDataException("SavedVariables must contain a characters table.");
        if (characters.Count > ObservationRules.MaximumObservations) throw new InvalidDataException("Too many characters.");
        var result = new List<Observation>();
        foreach (var (key, value) in characters)
        {
            if (value is not Dictionary<string, object?> row) throw new InvalidDataException("Invalid character table.");
            // Legacy/import markers are explicitly excluded. Schema 2 keeps imports out of characters entirely.
            if (row.GetValueOrDefault("imported") is true || row.GetValueOrDefault("isImported") is true) continue;
            var owner = String(row, "sourceId", false);
            if (owner.Length > 0 && !StringComparer.Ordinal.Equals(owner, effectiveId)) continue;
            if (!row.TryGetValue("seconds", out var secondsValue) || secondsValue is null) continue;
            if (secondsValue is not double seconds) throw new InvalidDataException("Character seconds is not a number.");
            var flavor = String(row, "flavor", false);
            if (flavor.Length == 0) flavor = source.Flavor;
            if (flavor != source.Flavor) continue; // A source contributes only its own client family.
            var region = String(row, "region", false);
            if (region.Length == 0) region = "unknown"; // Configuration metadata cannot confirm an individual legacy character region.
            var guid = String(row, "guid", false);
            if (guid.Length == 0 && key.StartsWith(flavor + ":", StringComparison.Ordinal)) guid = key[(flavor.Length + 1)..];
            var serverSeconds = version >= 2 ? OptionalNumber(row, "serverSeconds") : null;
            var serverAt = version >= 2 ? OptionalNumber(row, "serverAt") : null;
            var level = Number(row, "level");
            if (level != Math.Truncate(level) || level > 1000) throw new InvalidDataException("Invalid character level.");
            var observation = new Observation
            {
                SourceId = effectiveId,
                Region = region,
                Flavor = flavor,
                Guid = guid,
                Name = String(row, "name"),
                Realm = String(row, "realm"),
                Class = String(row, "class"),
                Level = (int)level,
                Seconds = seconds,
                UpdatedAt = Number(row, "updatedAt"),
                ServerSeconds = serverSeconds,
                ServerAt = serverAt,
                Guild = version >= 2 ? OptionalString(row, "guild") : null,
                GuildUpdatedAt = version >= 2 ? OptionalNumber(row, "guildUpdatedAt") : null
            };
            ObservationRules.Validate(observation);
            result.Add(observation);
        }
        return new ParsedSavedVariables((int)version, sourceId.Length > 0 ? sourceId : null, ObservationRules.Merge(result));
    }

    private static string String(Dictionary<string, object?> table, string name, bool required = true)
    {
        if (!table.TryGetValue(name, out var value) || value is null)
            return required ? throw new InvalidDataException($"Missing {name}.") : "";
        return value as string ?? throw new InvalidDataException($"Invalid {name}.");
    }
    private static string? OptionalString(Dictionary<string, object?> table, string name) =>
        !table.TryGetValue(name, out var value) || value is null ? null :
        value as string ?? throw new InvalidDataException($"Invalid {name}.");
    private static double Number(Dictionary<string, object?> table, string name) =>
        table.GetValueOrDefault(name) is double value && ObservationRules.Finite(value)
            ? value : throw new InvalidDataException($"Invalid {name}.");
    private static double? OptionalNumber(Dictionary<string, object?> table, string name) =>
        !table.TryGetValue(name, out var value) || value is null ? null : Number(table, name);

    private sealed class LiteralParser(string text)
    {
        private int position;
        private int nodes;
        public Dictionary<string, object?> Parse()
        {
            if (Encoding.UTF8.GetByteCount(text) > MaximumFileBytes) Fail("File exceeds size limit");
            Skip();
            if (Identifier() != "HourstoneDB") Fail("Expected HourstoneDB assignment");
            Expect('=');
            var value = Value(0);
            Skip();
            if (Peek() == ';') { position++; Skip(); }
            if (position != text.Length || value is not Dictionary<string, object?>) Fail("Unexpected executable Lua or invalid root");
            return (Dictionary<string, object?>)value!;
        }
        private object? Value(int depth)
        {
            if (++nodes > 500000 || depth > 32) Fail("Structural limit exceeded");
            Skip(); var c = Peek();
            if (c == '{') return Table(depth + 1);
            if (c is '\'' or '"') return Quoted();
            if (c == '[' && LongDelimiter(position, out _, out _)) return LongString();
            if (c == '-' || c == '+' || char.IsAsciiDigit(c)) return Numeric();
            return Identifier() switch { "true" => true, "false" => false, "nil" => null, _ => throw Error("Only literal values are allowed") };
        }
        private Dictionary<string, object?> Table(int depth)
        {
            Expect('{'); var result = new Dictionary<string, object?>(StringComparer.Ordinal); var arrayIndex = 1;
            while (true)
            {
                Skip(); if (Peek() == '}') { position++; return result; }
                string key; object? value;
                if (Peek() == '[' && !LongDelimiter(position, out _, out _))
                {
                    position++; var literal = Value(depth); Expect(']'); Expect('=');
                    key = literal switch { string s => s, double n when n == Math.Truncate(n) => n.ToString("R", CultureInfo.InvariantCulture), _ => throw Error("Invalid table key") };
                    value = Value(depth);
                }
                else if (char.IsAsciiLetter(Peek()) || Peek() == '_')
                {
                    var saved = position; var id = Identifier(); Skip();
                    if (Peek() == '=') { position++; key = id; value = Value(depth); }
                    else { position = saved; key = (arrayIndex++).ToString(CultureInfo.InvariantCulture); value = Value(depth); }
                }
                else { key = (arrayIndex++).ToString(CultureInfo.InvariantCulture); value = Value(depth); }
                if (!result.TryAdd(key, value)) Fail("Duplicate table key");
                Skip();
                if (Peek() is ',' or ';') { position++; continue; }
                if (Peek() != '}') Fail("Expected table separator");
            }
        }
        private double Numeric()
        {
            var start = position;
            if (Peek() is '+' or '-') position++;
            while (char.IsAsciiDigit(Peek())) position++;
            if (Peek() == '.') { position++; while (char.IsAsciiDigit(Peek())) position++; }
            if (Peek() is 'e' or 'E') { position++; if (Peek() is '+' or '-') position++; while (char.IsAsciiDigit(Peek())) position++; }
            if (!double.TryParse(text[start..position], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value))
                Fail("Invalid numeric literal");
            return value;
        }
        private string Quoted()
        {
            var quote = text[position++]; using var bytes = new MemoryStream(); var utf8 = new UTF8Encoding(false, true);
            void Append(string value) { var encoded = utf8.GetBytes(value); bytes.Write(encoded); }
            while (position < text.Length)
            {
                var c = text[position++]; if (c == quote) return utf8.GetString(bytes.ToArray());
                if (c is '\r' or '\n') Fail("Unescaped newline in string");
                if (c != '\\')
                {
                    if (char.IsHighSurrogate(c) && position < text.Length && char.IsLowSurrogate(text[position])) Append(new string([c, text[position++]]));
                    else Append(c.ToString());
                    continue;
                }
                if (position >= text.Length) Fail("Unterminated string escape");
                c = text[position++];
                if (char.IsAsciiDigit(c))
                {
                    var number = c - '0'; var count = 1;
                    while (count < 3 && char.IsAsciiDigit(Peek())) { number = number * 10 + text[position++] - '0'; count++; }
                    if (number > 255) Fail("Invalid decimal escape"); bytes.WriteByte((byte)number);
                }
                else Append((c switch { 'a' => '\a', 'b' => '\b', 'f' => '\f', 'n' => '\n', 'r' => '\r', 't' => '\t', 'v' => '\v', '\\' => '\\', '\'' => '\'', '"' => '"', '\n' => '\n', _ => throw Error("Invalid string escape") }).ToString());
            }
            throw Error("Unterminated string");
        }
        private string Identifier()
        {
            Skip(); var start = position;
            if (!(char.IsAsciiLetter(Peek()) || Peek() == '_')) Fail("Expected identifier");
            position++; while (char.IsAsciiLetterOrDigit(Peek()) || Peek() == '_') position++;
            return text[start..position];
        }
        private bool LongDelimiter(int offset, out int equals, out int length)
        {
            equals = 0; length = 0; if (offset >= text.Length || text[offset] != '[') return false;
            var cursor = offset + 1; while (cursor < text.Length && text[cursor] == '=') { equals++; cursor++; }
            if (cursor >= text.Length || text[cursor] != '[') return false;
            length = cursor - offset + 1; return true;
        }
        private string LongString()
        {
            if (!LongDelimiter(position, out var equals, out var length)) throw Error("Invalid long string");
            position += length; var endToken = "]" + new string('=', equals) + "]";
            var end = text.IndexOf(endToken, position, StringComparison.Ordinal);
            if (end < 0) throw Error("Unterminated long string");
            var value = text[position..end]; position = end + endToken.Length;
            if (value.StartsWith("\r\n", StringComparison.Ordinal)) return value[2..];
            return value.StartsWith('\n') ? value[1..] : value;
        }
        private void Skip()
        {
            while (position < text.Length)
            {
                if (char.IsWhiteSpace(Peek()) || Peek() == '\uFEFF') { position++; continue; }
                if (Peek() == '-' && position + 1 < text.Length && text[position + 1] == '-')
                {
                    position += 2;
                    if (LongDelimiter(position, out _, out _)) _ = LongString();
                    else while (position < text.Length && Peek() != '\n') position++;
                    continue;
                }
                return;
            }
        }
        private char Peek() => position < text.Length ? text[position] : '\0';
        private void Expect(char c) { Skip(); if (Peek() != c) Fail($"Expected {c}"); position++; }
        private InvalidDataException Error(string message) => new($"{message} at character {position}.");
        private void Fail(string message) => throw Error(message);
    }
}

public sealed record ParsedSavedVariables(int SchemaVersion, string? SourceId, IReadOnlyList<Observation> Observations);
