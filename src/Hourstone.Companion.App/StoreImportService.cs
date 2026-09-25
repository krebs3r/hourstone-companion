using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hourstone.Companion.Core;
using Microsoft.Data.Sqlite;

namespace Hourstone.Companion.App;

public sealed record StoreImportCandidate(bool Available, string SourceDirectory, string? DeviceName = null, int SourceCount = 0, string? Error = null);
public sealed record StoreImportResult(bool Succeeded, string? BackupPath = null, string? NewDeviceId = null, string? Error = null);

/// <summary>Copies a consistent database into a private profile, then commits only after validation and explicit handover.</summary>
public static class StoreImportService
{
    const string SetupMarker = "store-setup.json";
    public static bool RequiresSetup => AppDistribution.Current.IsStore && !AppRuntime.IsSmokeTest && !File.Exists(Path.Combine(UserSettings.DataDirectory, SetupMarker));
    public static StoreImportCandidate Discover() => Discover(UserSettings.LegacyDataDirectory);
    public static StoreImportCandidate Discover(string sourceDirectory)
    {
        try
        {
            var path = Path.Combine(sourceDirectory, "companion.sqlite");
            if (!File.Exists(path)) return new(false, sourceDirectory);
            using var connection = Open(path, SqliteOpenMode.ReadOnly);
            var configuration = ReadConfiguration(connection);
            return new(true, sourceDirectory, configuration.DeviceName, configuration.Sources.Count);
        }
        catch (Exception ex) when (Recoverable(ex)) { return new(false, sourceDirectory, Error: ex.Message); }
    }
    public static Task<StoreImportResult> ImportAsync(bool startWithWindows, CancellationToken cancellationToken = default)
    {
        if (!AppDistribution.Current.IsStore || !AppRuntime.OwnsInstance)
            return Task.FromResult(new StoreImportResult(false, Error: "Close the other Hourstone Companion before continuing."));
        return ImportWithStartupAsync(UserSettings.LegacyDataDirectory, UserSettings.DataDirectory,
            startWithWindows, new StoreStartupController(), cancellationToken);
    }
    internal static async Task<StoreImportResult> ImportWithStartupAsync(string source, string target,
        bool startWithWindows, IStoreStartupController startup, CancellationToken cancellationToken = default,
        Action? beforeCommit = null)
    {
        var previousStartup = AppStartupState.Disabled;
        var startupChanged = false;
        PreparedImport? prepared = null;
        try
        {
            EnsureEmptyDestination(target);
            previousStartup = await startup.GetStateAsync();
            prepared = await Task.Run(() => Prepare(source, target, startWithWindows, cancellationToken), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            startupChanged = true;
            var effectiveStartup = await startup.SetEnabledAsync(startWithWindows);
            cancellationToken.ThrowIfCancellationRequested();
            (UserSettings.Load(prepared.Work) with { Autostart = effectiveStartup == AppStartupState.Enabled }).Save(prepared.Work);
            beforeCommit?.Invoke();
            return Commit(prepared);
        }
        catch (Exception ex) when (Recoverable(ex) || ex is OperationCanceledException)
        {
            var errors = new List<string> { ex.Message };
            if (startupChanged)
                try { await startup.SetEnabledAsync(previousStartup == AppStartupState.Enabled); }
                catch (Exception rollback) when (Recoverable(rollback)) { errors.Add(rollback.Message); }
            return new(false, Error: string.Join(Environment.NewLine, errors));
        }
        finally { prepared?.Dispose(); }
    }
    public static Task<StoreImportResult> StartFreshAsync(bool startWithWindows)
    {
        if (!AppDistribution.Current.IsStore || !AppRuntime.OwnsInstance)
            return Task.FromResult(new StoreImportResult(false, Error: "Close the other Hourstone Companion before continuing."));
        return StartFreshWithStartupAsync(UserSettings.DataDirectory, startWithWindows, new StoreStartupController());
    }
    internal static async Task<StoreImportResult> StartFreshWithStartupAsync(string target,
        bool startWithWindows, IStoreStartupController startup, Action? beforeCommit = null)
    {
        var previousStartup = AppStartupState.Disabled;
        var startupChanged = false;
        try
        {
            EnsureEmptyDestination(target);
            previousStartup = await startup.GetStateAsync();
            startupChanged = true;
            var effectiveStartup = await startup.SetEnabledAsync(startWithWindows);
            return StartFreshProfile(target, effectiveStartup == AppStartupState.Enabled, beforeCommit);
        }
        catch (Exception ex) when (Recoverable(ex))
        {
            var errors = new List<string> { ex.Message };
            if (startupChanged)
                try { await startup.SetEnabledAsync(previousStartup == AppStartupState.Enabled); }
                catch (Exception rollback) when (Recoverable(rollback)) { errors.Add(rollback.Message); }
            return new(false, Error: string.Join(Environment.NewLine, errors));
        }
    }
    // Pure file/SQLite entry point used by migration tests. It never changes autostart or starts synchronization.
    public static StoreImportResult StartFreshProfile(string targetDirectory, bool startWithWindows = false, Action? beforeCommit = null)
    {
        var target = Path.GetFullPath(targetDirectory);
        EnsureEmptyDestination(target); SafeFiles.EnsureNoReparsePoints(target);
        var path = Path.Combine(target, "preferences.json");
        var previous = File.Exists(path) ? File.ReadAllBytes(path) : null;
        try
        {
            beforeCommit?.Invoke();
            Directory.CreateDirectory(target);
            (UserSettings.Load(target) with { Autostart = startWithWindows }).Save(target);
            WriteMarker(target, "fresh", null);
            return new(true);
        }
        catch
        {
            if (previous is null) File.Delete(path); else File.WriteAllBytes(path, previous);
            throw;
        }
    }
    public static StoreImportResult ImportProfile(string sourceDirectory, string targetDirectory, bool startWithWindows = false, Action? beforeCommit = null)
    {
        using var prepared = Prepare(sourceDirectory, targetDirectory, startWithWindows, CancellationToken.None);
        beforeCommit?.Invoke();
        return Commit(prepared);
    }
    static PreparedImport Prepare(string sourceDirectory, string targetDirectory, bool startWithWindows, CancellationToken cancellationToken)
    {
        var source = Path.GetFullPath(sourceDirectory); var target = Path.GetFullPath(targetDirectory);
        if (StringComparer.OrdinalIgnoreCase.Equals(source.TrimEnd(Path.DirectorySeparatorChar), target.TrimEnd(Path.DirectorySeparatorChar)))
            throw new IOException("The existing profile cannot be used as the import destination.");
        EnsureEmptyDestination(target);
        var sourceDatabase = Path.Combine(source, "companion.sqlite");
        SafeFiles.EnsureNoReparsePoints(sourceDatabase);
        SafeFiles.EnsureNoReparsePoints(target);
        if (!File.Exists(sourceDatabase)) throw new FileNotFoundException("The existing Hourstone profile is unavailable.", sourceDatabase);
        Directory.CreateDirectory(target);
        var work = Path.Combine(target, ".import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        var prepared = new PreparedImport(work, target);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var backup = Path.Combine(work, "original.sqlite");
            using (var from = Open(sourceDatabase, SqliteOpenMode.ReadOnly))
            using (var to = Open(backup, SqliteOpenMode.ReadWriteCreate)) from.BackupDatabase(to);
            var preferences = Path.Combine(source, "preferences.json");
            var settings = File.Exists(preferences)
                ? JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(preferences)) ?? throw new InvalidDataException("Invalid existing preferences.")
                : new UserSettings();
            if (File.Exists(preferences)) File.Copy(preferences, Path.Combine(work, "original-preferences.json"));
            File.Copy(backup, Path.Combine(work, "companion.sqlite"));
            using (var database = Open(Path.Combine(work, "companion.sqlite"), SqliteOpenMode.ReadWrite))
            {
                if (!string.Equals(Scalar(database, "PRAGMA integrity_check;") as string, "ok", StringComparison.Ordinal))
                    throw new InvalidDataException("The existing database did not pass its integrity check.");
                var configuration = ReadConfiguration(database);
                prepared.NewDeviceId = Guid.NewGuid().ToString("D");
                configuration = configuration with { DeviceId = prepared.NewDeviceId };
                using var transaction = database.BeginTransaction();
                Set(database, transaction, "configuration", JsonSerializer.Serialize(configuration, JsonContract.Options));
                Set(database, transaction, "revision", "0");
                transaction.Commit();
            }
            // Applies the current supported schema to the copy and validates configuration; no SyncNowAsync call here.
            using (var validator = new CompanionService(Path.Combine(work, "companion.sqlite"))) _ = validator.GetConfiguration();
            SqliteConnection.ClearAllPools();
            (settings with { Autostart = startWithWindows }).Save(work);
            cancellationToken.ThrowIfCancellationRequested();
            return prepared;
        }
        catch { prepared.Dispose(); throw; }
    }
    static StoreImportResult Commit(PreparedImport prepared)
    {
        EnsureEmptyDestination(prepared.Target);
        var destinationDatabase = Path.Combine(prepared.Target, "companion.sqlite");
        var preferencesPath = Path.Combine(prepared.Target, "preferences.json");
        var oldPreferences = File.Exists(preferencesPath) ? File.ReadAllBytes(preferencesPath) : null;
        var copiedDatabase = false; var copiedPreferences = false;
        var backupDirectory = Path.Combine(prepared.Target, "import-backups", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(backupDirectory);
            File.Copy(Path.Combine(prepared.Work, "original.sqlite"), Path.Combine(backupDirectory, "companion.sqlite"));
            var originalPreferences = Path.Combine(prepared.Work, "original-preferences.json");
            if (File.Exists(originalPreferences)) File.Copy(originalPreferences, Path.Combine(backupDirectory, "preferences.json"));
            File.Move(Path.Combine(prepared.Work, "companion.sqlite"), destinationDatabase); copiedDatabase = true;
            copiedPreferences = true;
            File.Copy(Path.Combine(prepared.Work, "preferences.json"), preferencesPath, true);
            WriteMarker(prepared.Target, "imported", prepared.NewDeviceId);
            return new(true, backupDirectory, prepared.NewDeviceId);
        }
        catch
        {
            if (copiedDatabase) foreach (var suffix in new[] { "", "-wal", "-shm" }) File.Delete(destinationDatabase + suffix);
            if (copiedPreferences) { if (oldPreferences is null) File.Delete(preferencesPath); else File.WriteAllBytes(preferencesPath, oldPreferences); }
            throw;
        }
        finally { prepared.Dispose(); }
    }
    static void EnsureEmptyDestination(string target)
    {
        if (File.Exists(Path.Combine(target, SetupMarker)) || File.Exists(Path.Combine(target, "companion.sqlite")))
            throw new IOException("This Store profile is already set up. Existing data will not be replaced.");
    }
    static void WriteMarker(string directory, string mode, string? deviceId)
    {
        var path = Path.Combine(directory, SetupMarker); var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(new { formatVersion = 1, mode, deviceId, completedAt = DateTimeOffset.UtcNow }));
        File.Move(temporary, path, false);
    }
    static CompanionConfiguration ReadConfiguration(SqliteConnection connection)
        => JsonSerializer.Deserialize<CompanionConfiguration>((string?)Scalar(connection, "SELECT value FROM settings WHERE key='configuration';") ?? throw new InvalidDataException("Existing profile has no configuration."), JsonContract.Options)
            ?? throw new InvalidDataException("Invalid existing configuration.");
    static SqliteConnection Open(string path, SqliteOpenMode mode)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = mode, Pooling = false }.ToString());
        try { connection.Open(); return connection; } catch { connection.Dispose(); throw; }
    }
    static object? Scalar(SqliteConnection connection, string sql)
    { using var command = connection.CreateCommand(); command.CommandText = sql; return command.ExecuteScalar(); }
    static void Set(SqliteConnection connection, SqliteTransaction transaction, string key, string value)
    {
        using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "INSERT INTO settings(key,value) VALUES($key,$value) ON CONFLICT(key) DO UPDATE SET value=excluded.value";
        command.Parameters.AddWithValue("$key", key); command.Parameters.AddWithValue("$value", value); command.ExecuteNonQuery();
    }
    static bool Recoverable(Exception ex) => ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or SqliteException or InvalidOperationException or System.Runtime.InteropServices.COMException;
    sealed class PreparedImport(string work, string target) : IDisposable
    {
        public string Work { get; } = work;
        public string Target { get; } = target;
        public string? NewDeviceId { get; set; }
        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(Work) && Path.GetFullPath(Work).StartsWith(Path.GetFullPath(Target) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                // Staging cleanup must not turn a committed handover into a reported failure.
                // A temporarily locked private staging folder can safely be left for later cleanup.
                try { Directory.Delete(Work, true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
