using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace Hourstone.Companion.Core;

internal sealed class CompanionStore : IDisposable
{
    private readonly SqliteConnection connection;
    public CompanionStore(string path)
    {
        var fullPath = Path.GetFullPath(path); SafeFiles.EnsureNoReparsePoints(fullPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = fullPath, Mode = SqliteOpenMode.ReadWriteCreate }.ToString());
        connection.Open();
        if (Convert.ToInt32(Scalar("PRAGMA user_version;")) > 1) { connection.Dispose(); throw new InvalidDataException("This Companion database was created by a newer version."); }
        Execute("PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;");
        Execute("CREATE TABLE IF NOT EXISTS settings (key TEXT PRIMARY KEY, value TEXT NOT NULL); CREATE TABLE IF NOT EXISTS sources (source_id TEXT PRIMARY KEY, json TEXT NOT NULL); CREATE TABLE IF NOT EXISTS snapshots (group_id TEXT NOT NULL, device_id TEXT NOT NULL, revision INTEGER NOT NULL, hash TEXT NOT NULL, json TEXT NOT NULL, last_seen TEXT NOT NULL, PRIMARY KEY(group_id,device_id));");
        var version = Scalar("PRAGMA user_version;");
        if (Convert.ToInt32(version) > 1) throw new InvalidDataException("This Companion database was created by a newer version.");
        Execute("PRAGMA user_version=1;");
    }
    public string? Get(string key) => Scalar("SELECT value FROM settings WHERE key=$key", ("$key", key)) as string;
    public void Set(string key, string value) => Execute("INSERT INTO settings(key,value) VALUES($key,$value) ON CONFLICT(key) DO UPDATE SET value=excluded.value", ("$key", key), ("$value", value));
    public IReadOnlyList<Observation> ReadSource(string sourceId)
    {
        var json = Scalar("SELECT json FROM sources WHERE source_id=$id", ("$id", sourceId)) as string;
        return json is null ? [] : JsonSerializer.Deserialize<List<Observation>>(json, JsonContract.Options) ?? [];
    }
    public void WriteSource(string sourceId, IReadOnlyList<Observation> values) => Execute("INSERT INTO sources(source_id,json) VALUES($id,$json) ON CONFLICT(source_id) DO UPDATE SET json=excluded.json", ("$id", sourceId), ("$json", JsonSerializer.Serialize(values, JsonContract.Options)));
    public void RetainSources(IEnumerable<string> sourceIds)
    {
        var keep = sourceIds.ToHashSet(StringComparer.Ordinal); var obsolete = new List<string>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT source_id FROM sources"; using var reader = command.ExecuteReader();
            while (reader.Read()) if (!keep.Contains(reader.GetString(0))) obsolete.Add(reader.GetString(0));
        }
        foreach (var source in obsolete) Execute("DELETE FROM sources WHERE source_id=$id", ("$id", source));
    }
    public IReadOnlyList<CachedSnapshot> ReadSnapshots(string groupId)
    {
        using var command = connection.CreateCommand(); command.CommandText = "SELECT json,hash,last_seen FROM snapshots WHERE group_id=$group"; command.Parameters.AddWithValue("$group", groupId);
        using var reader = command.ExecuteReader(); var result = new List<CachedSnapshot>();
        while (reader.Read()) result.Add(new CachedSnapshot(ObservationRules.ParseSnapshot(reader.GetString(0), groupId), reader.GetString(1), DateTimeOffset.Parse(reader.GetString(2), System.Globalization.CultureInfo.InvariantCulture)));
        return result;
    }
    public void WriteSnapshot(DeviceSnapshot snapshot, string hash)
    {
        var json = ObservationRules.CanonicalSnapshot(snapshot);
        Execute("INSERT INTO snapshots(group_id,device_id,revision,hash,json,last_seen) VALUES($group,$device,$revision,$hash,$json,$seen) ON CONFLICT(group_id,device_id) DO UPDATE SET revision=excluded.revision,hash=excluded.hash,json=excluded.json,last_seen=excluded.last_seen", ("$group", snapshot.GroupId), ("$device", snapshot.DeviceId), ("$revision", snapshot.Revision), ("$hash", hash), ("$json", json), ("$seen", DateTimeOffset.UtcNow.ToString("O")));
    }
    public void ClearSnapshots() => Execute("DELETE FROM snapshots;");
    public void Transaction(Action action)
    {
        Execute("BEGIN IMMEDIATE;");
        try { action(); Execute("COMMIT;"); }
        catch { Execute("ROLLBACK;"); throw; }
    }
    private object? Scalar(string sql, params (string, object)[] values)
    { using var command = Command(sql, values); return command.ExecuteScalar(); }
    private void Execute(string sql, params (string, object)[] values)
    { using var command = Command(sql, values); command.ExecuteNonQuery(); }
    private SqliteCommand Command(string sql, (string, object)[] values)
    {
        var command = connection.CreateCommand(); command.CommandText = sql;
        foreach (var (name, value) in values) command.Parameters.AddWithValue(name, value);
        return command;
    }
    public void Dispose() => connection.Dispose();
}
internal sealed record CachedSnapshot(DeviceSnapshot Snapshot, string Hash, DateTimeOffset LastSeen);
