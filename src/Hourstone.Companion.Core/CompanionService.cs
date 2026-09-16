using System.Text.Json;

namespace Hourstone.Companion.Core;

public sealed class CompanionService : IDisposable
{
    private readonly object gate = new();
    private readonly SemaphoreSlim syncGate = new(1, 1);
    private readonly CompanionStore store;
    private CompanionConfiguration configuration;
    private IReadOnlyList<Observation> characters = [];
    private IReadOnlyList<DeviceStatus> devices = [];
    private SyncResult lastResult = SyncResult.Empty;
    private bool disposed;
    public SyncResult LastResult { get { lock (gate) return lastResult; } }
    public CompanionService(string databasePath, string? deviceName = null)
    {
        store = new CompanionStore(databasePath);
        var json = store.Get("configuration");
        configuration = json is null ? new CompanionConfiguration
        { DeviceId = Guid.NewGuid().ToString("D"), DeviceName = CleanDeviceName(deviceName ?? Environment.MachineName) }
            : JsonSerializer.Deserialize<CompanionConfiguration>(json, JsonContract.Options) ?? throw new InvalidDataException("Invalid local configuration.");
        ValidateConfiguration(configuration); PersistConfiguration();
        RefreshViews();
    }
    public CompanionConfiguration GetConfiguration() { lock (gate) return configuration with { Sources = [.. configuration.Sources] }; }
    public void SaveConfiguration(CompanionConfiguration value)
    {
        lock (gate)
        {
            ThrowIfDisposed(); ValidateConfiguration(value);
            if (value.DeviceId != configuration.DeviceId || value.GroupId != configuration.GroupId || value.CloudFolder != configuration.CloudFolder)
                throw new InvalidDataException("Device and sync-group identity can only be changed through the dedicated sync-folder methods.");
            configuration = value with { Sources = [.. value.Sources] };
            store.Transaction(() => { PersistConfiguration(); store.RetainSources(configuration.Sources.Where(s => s.Enabled).Select(s => s.SourceId)); });
            RefreshViews();
        }
    }
    public IReadOnlyList<DiscoveredSource> DiscoverSources(IEnumerable<string> wowRoots) => SourceDiscovery.Discover(wowRoots, GetConfiguration().DeviceId);
    public IReadOnlyList<DeviceStatus> GetDevices() { lock (gate) return devices.ToArray(); }
    public IReadOnlyList<Observation> GetCharacters() { lock (gate) return characters.ToArray(); }
    public Task JoinSyncFolderAsync(string folder, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            ThrowIfDisposed();
            var selected = Path.GetFullPath(folder);
            var cloud = Path.GetFileName(selected.TrimEnd(Path.DirectorySeparatorChar)).Equals("HourstoneSync", StringComparison.OrdinalIgnoreCase)
                ? selected : Path.Combine(selected, "HourstoneSync");
            SafeFiles.EnsureNoReparsePoints(cloud); Directory.CreateDirectory(cloud);
            var groupPath = Path.Combine(cloud, "group.json");
            if (!File.Exists(groupPath))
            {
                var metadata = new SyncGroup { GroupId = Guid.NewGuid().ToString("D") };
                var temporary = Path.Combine(cloud, ".group-" + Guid.NewGuid().ToString("N") + ".tmp");
                try
                {
                    File.WriteAllText(temporary, JsonSerializer.Serialize(metadata, JsonContract.Options), new System.Text.UTF8Encoding(false));
                    try { File.Move(temporary, groupPath, false); } catch (IOException) when (File.Exists(groupPath)) { }
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
            var group = ReadGroup(cloud);
            configuration = configuration with { CloudFolder = cloud, GroupId = group.GroupId, CloudPaused = false };
            PersistConfiguration(); RefreshViews();
        }
    }, cancellationToken);
    public void PauseCloud(bool paused)
    { lock (gate) { ThrowIfDisposed(); configuration = configuration with { CloudPaused = paused }; PersistConfiguration(); } }
    public void DetachSyncFolder()
    {
        lock (gate)
        {
            ThrowIfDisposed(); configuration = configuration with { CloudFolder = null, GroupId = null, CloudPaused = false };
            store.Transaction(() => { PersistConfiguration(); store.ClearSnapshots(); }); RefreshViews();
        }
    }
    public async Task<SyncResult> SyncNowAsync(CancellationToken cancellationToken = default)
    {
        await syncGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return await Task.Run(() => { lock (gate) return SyncCore(cancellationToken); }, cancellationToken).ConfigureAwait(false); }
        finally { syncGate.Release(); }
    }
    private SyncResult SyncCore(CancellationToken cancellationToken)
    {
        ThrowIfDisposed(); var issues = new List<SyncIssue>(); var selected = configuration.Sources.Where(s => s.Enabled).ToArray();
        store.RetainSources(selected.Select(s => s.SourceId));
        var sourceStatuses = new List<LocalSourceStatus>();
        foreach (var source in selected)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var status = AddonReadiness.Inspect(source);
            var metadataIssue = status.Readiness switch
            {
                LocalSourceReadiness.AddonMissing => "addon_missing",
                LocalSourceReadiness.AddonOutdated => "addon_outdated",
                LocalSourceReadiness.ReadFailed => "addon_read_failed",
                _ => null
            };
            if (metadataIssue is not null) issues.Add(new SyncIssue(metadataIssue, status.Message!, source.SourceId));
            try
            {
                SourceDiscovery.Validate(source);
                var parsed = File.Exists(source.SavedVariablesPath)
                    ? SavedVariablesReader.Read(SafeFiles.StableRead(source.SavedVariablesPath), source) : null;
                if (parsed?.SourceId is null)
                {
                    if (status.Readiness == LocalSourceReadiness.Ready)
                    {
                        status = status with { Readiness = LocalSourceReadiness.AwaitingGameSave, Message = AddonReadiness.AwaitingSaveMessage(source) };
                        issues.Add(new SyncIssue("source_not_initialized", status.Message, source.SourceId));
                    }
                }
                else
                {
                    var effective = source;
                    if (source.SourceId != parsed.SourceId)
                    {
                        if (configuration.Sources.Any(s => s.SourceId == parsed.SourceId && s != source)) throw new InvalidDataException("The same logical source is configured more than once.");
                        effective = source with { SourceId = parsed.SourceId };
                        configuration = configuration with { Sources = configuration.Sources.Select(s => s == source ? effective : s).ToList() };
                        PersistConfiguration(); status = status with { SourceId = effective.SourceId };
                    }
                    // Metadata diagnoses do not discard or block otherwise valid saved observations.
                    store.WriteSource(effective.SourceId, parsed.Observations);
                }
            }
            catch (Exception ex) when (IsRecoverable(ex))
            {
                var message = $"{source.Flavor} / {source.AccountName}: Quelle konnte nicht vollständig gelesen werden; der letzte gültige Stand bleibt verfügbar: " + ex.Message;
                status = status with { Readiness = LocalSourceReadiness.ReadFailed, Message = message };
                issues.Add(new SyncIssue("source_read_failed", message, source.SourceId));
            }
            sourceStatuses.Add(status);
        }
        selected = configuration.Sources.Where(s => s.Enabled).ToArray(); store.RetainSources(selected.Select(s => s.SourceId));
        var own = selected.SelectMany(s => store.ReadSource(s.SourceId)).OrderBy(o => o.SourceId, StringComparer.Ordinal).ThenBy(ObservationRules.Identity, StringComparer.Ordinal).ToList();
        var groupId = configuration.GroupId ?? Guid.Empty.ToString("D");
        var local = EnsureLocalSnapshot(own, groupId);
        var canPublish = true; var cloudPublished = false;
        if (configuration.CloudFolder is not null && !configuration.CloudPaused)
        {
            try
            {
                var group = ReadGroup(configuration.CloudFolder);
                if (group.GroupId != groupId) throw new InvalidDataException("The selected sync folder now belongs to a different group.");
                canPublish = ReadCloudSnapshots(configuration.CloudFolder, local, issues, cancellationToken);
                if (canPublish) { SafeFiles.AtomicWriteOwned(configuration.CloudFolder, configuration.DeviceId + ".json", ObservationRules.CanonicalSnapshot(local)); cloudPublished = true; }
            }
            catch (Exception ex) when (IsRecoverable(ex))
            { issues.Add(new SyncIssue("cloud_unavailable", "Sync-Ordner nicht verfügbar; lokale Daten und zuletzt empfangene Geräte bleiben verfügbar: " + ex.Message)); }
        }
        RefreshViews();
        // Removed accounts must also disappear from generated routing tables. Known client outputs are retained locally for this cleanup.
        var knownClients = JsonSerializer.Deserialize<List<string>>(store.Get("managedClients") ?? "[]", JsonContract.Options) ?? [];
        var currentClients = selected.Select(s => Path.GetFullPath(s.ClientDirectory)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var managedClients = currentClients.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var client in currentClients.Concat(knownClients).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var sources = selected.Where(s => StringComparer.OrdinalIgnoreCase.Equals(Path.GetFullPath(s.ClientDirectory), client)).Select(s => s.SourceId);
                // Do not recreate an old installation that the user has removed.
                if (!currentClients.Contains(client, StringComparer.OrdinalIgnoreCase) && !Directory.Exists(Path.Combine(client, "Interface", "AddOns", "Hourstone_Sync"))) continue;
                DataAddonWriter.Write(client, sources, characters);
            }
            catch (Exception ex) when (IsRecoverable(ex)) { managedClients.Add(client); issues.Add(new SyncIssue("addon_write_failed", "Sync-Datenaddon konnte nicht aktualisiert werden: " + ex.Message)); }
        }
        store.Set("managedClients", JsonSerializer.Serialize(managedClients.Order(StringComparer.OrdinalIgnoreCase), JsonContract.Options));
        lastResult = new SyncResult(DateTimeOffset.UtcNow, characters.Count, selected.Length, devices.Count, issues)
        { LocalSourceStatuses = sourceStatuses, CloudPublished = cloudPublished, AddonReady = selected.Length > 0 && sourceStatuses.All(source => source.Readiness == LocalSourceReadiness.Ready) && !issues.Any(issue => issue.Code == "addon_write_failed") };
        return lastResult;
    }
    private DeviceSnapshot EnsureLocalSnapshot(List<Observation> own, string groupId)
    {
        var current = store.ReadSnapshots(groupId).SingleOrDefault(s => s.Snapshot.DeviceId == configuration.DeviceId)?.Snapshot;
        var candidate = new DeviceSnapshot { GroupId = groupId, DeviceId = configuration.DeviceId, DeviceName = configuration.DeviceName, Revision = current?.Revision ?? 1, Observations = own };
        ObservationRules.ValidateSnapshot(candidate, groupId);
        if (current is not null && ObservationRules.CanonicalSnapshot(candidate) == ObservationRules.CanonicalSnapshot(current)) return current;
        var lastRevision = long.TryParse(store.Get("revision"), out var prior) ? prior : 0;
        if (lastRevision == long.MaxValue) throw new InvalidDataException("Device revision limit reached.");
        candidate = candidate with { Revision = checked(lastRevision + 1) };
        store.Transaction(() => { store.Set("revision", candidate.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture)); store.WriteSnapshot(candidate, SafeFiles.Sha256(ObservationRules.CanonicalSnapshot(candidate))); });
        return candidate;
    }
    private bool ReadCloudSnapshots(string folder, DeviceSnapshot local, List<SyncIssue> issues, CancellationToken cancellationToken)
    {
        var paths = Directory.EnumerateFiles(folder, "*.json", SearchOption.TopDirectoryOnly).Take(513).ToArray();
        if (paths.Length > 512) throw new InvalidDataException("Too many files in sync folder.");
        var received = new List<(DeviceSnapshot Snapshot, string Hash)>(); var publish = true; long totalBytes = 0;
        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested(); totalBytes += new FileInfo(path).Length; if (totalBytes > 64L * 1024 * 1024) throw new InvalidDataException("Sync folder exceeds the total snapshot size limit."); if (Path.GetFileName(path).Equals("group.json", StringComparison.OrdinalIgnoreCase)) continue;
            if (Path.GetFileName(path).StartsWith("group", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var metadata = ParseGroup(SafeFiles.StableRead(path, 64 * 1024));
                    if (metadata.GroupId != local.GroupId) { issues.Add(new SyncIssue("group_conflict", "Der Cloud-Ordner enthält widersprüchliche Gruppendateien. Synchronisierung bleibt angehalten.")); return false; }
                    continue;
                }
                catch (Exception ex) when (IsRecoverable(ex)) { issues.Add(new SyncIssue("group_conflict", "Ungültige Konfliktkopie der Sync-Gruppe: " + ex.Message)); return false; }
            }
            try
            {
                var snapshot = ObservationRules.ParseSnapshot(SafeFiles.StableRead(path), local.GroupId);
                if (Path.GetFileName(path).Equals(configuration.DeviceId + ".json", StringComparison.OrdinalIgnoreCase) && snapshot.DeviceId != configuration.DeviceId)
                {
                    publish = false; issues.Add(new SyncIssue("device_file_conflict", "Die Datei dieses Geräts enthält die Identität eines anderen Geräts und wird nicht überschrieben.")); continue;
                }
                received.Add((snapshot, SafeFiles.Sha256(ObservationRules.CanonicalSnapshot(snapshot))));
            }
            catch (Exception ex) when (IsRecoverable(ex))
            {
                issues.Add(new SyncIssue("snapshot_rejected", "Ungültiger oder unbekannter Geräte-Snapshot wurde nicht übernommen: " + ex.Message));
                if (Path.GetFileName(path).Equals(configuration.DeviceId + ".json", StringComparison.OrdinalIgnoreCase)) publish = false;
            }
        }
        var existing = store.ReadSnapshots(local.GroupId).ToDictionary(s => s.Snapshot.DeviceId, StringComparer.Ordinal);
        foreach (var device in received.GroupBy(s => s.Snapshot.DeviceId, StringComparer.Ordinal))
        {
            var conflict = device.GroupBy(s => s.Snapshot.Revision).Any(revision => revision.Select(s => s.Hash).Distinct(StringComparer.Ordinal).Count() > 1);
            existing.TryGetValue(device.Key, out var cached);
            if (!conflict && cached is not null) conflict = device.Any(s => s.Snapshot.Revision == cached.Snapshot.Revision && s.Hash != cached.Hash);
            if (conflict)
            {
                issues.Add(new SyncIssue("revision_conflict", "Widersprüchliche Inhalte mit gleicher Geräte-Revision; der letzte gültige Stand bleibt erhalten."));
                if (device.Key == configuration.DeviceId) publish = false;
                continue;
            }
            var newest = device.OrderByDescending(s => s.Snapshot.Revision).First();
            if (device.Key == configuration.DeviceId)
            {
                if (newest.Snapshot.Revision > local.Revision)
                { publish = false; issues.Add(new SyncIssue("device_identity_conflict", "Der Cloud-Ordner enthält eine neuere Revision dieses Geräts. Die Geräteidentität muss geprüft werden.")); }
                continue; // Received observations are never promoted into this device's own contribution.
            }
            if (cached is null || newest.Snapshot.Revision > cached.Snapshot.Revision) store.WriteSnapshot(newest.Snapshot, newest.Hash);
        }
        return publish;
    }
    private void RefreshViews()
    {
        var selected = configuration.Sources.Where(s => s.Enabled).ToList(); var own = selected.SelectMany(s => store.ReadSource(s.SourceId)).ToList();
        var groupId = configuration.GroupId ?? Guid.Empty.ToString("D"); var cached = store.ReadSnapshots(groupId);
        var remote = cached.Where(s => s.Snapshot.DeviceId != configuration.DeviceId).ToArray();
        characters = ObservationRules.Merge(own.Concat(remote.SelectMany(s => s.Snapshot.Observations)));
        var localRevision = long.TryParse(store.Get("revision"), out var revision) ? revision : 0;
        devices = new[] { new DeviceStatus(configuration.DeviceId, configuration.DeviceName, localRevision, ObservationRules.Merge(own).Count, DateTimeOffset.UtcNow, true) }
            .Concat(remote.Select(s => new DeviceStatus(s.Snapshot.DeviceId, s.Snapshot.DeviceName, s.Snapshot.Revision, ObservationRules.Merge(s.Snapshot.Observations).Count, s.LastSeen, false))).ToArray();
    }
    private void PersistConfiguration() => store.Set("configuration", JsonSerializer.Serialize(configuration, JsonContract.Options));
    private static void ValidateConfiguration(CompanionConfiguration value)
    {
        if (!Guid.TryParseExact(value.DeviceId, "D", out _) || !ObservationRules.ValidText(value.DeviceName, 128) || value.Sources is null || value.Sources.Count > 128)
            throw new InvalidDataException("Invalid companion configuration.");
        if ((value.GroupId is null) != (value.CloudFolder is null) || value.GroupId is not null && !Guid.TryParseExact(value.GroupId, "D", out _)) throw new InvalidDataException("Invalid cloud group configuration.");
        var ids = new HashSet<string>(StringComparer.Ordinal); var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in value.Sources)
        {
            SourceDiscovery.Validate(source);
            if (!ids.Add(source.SourceId) || !paths.Add(Path.GetFullPath(source.SavedVariablesPath))) throw new InvalidDataException("Duplicate selected source.");
        }
    }
    private static SyncGroup ReadGroup(string folder)
    {
        if (!Directory.Exists(folder)) throw new DirectoryNotFoundException("The selected sync folder is unavailable.");
        return ParseGroup(SafeFiles.StableRead(Path.Combine(folder, "group.json"), 64 * 1024));
    }
    private static SyncGroup ParseGroup(string json)
    {
        ObservationRules.RejectDuplicateJsonProperties(json);
        var group = JsonSerializer.Deserialize<SyncGroup>(json, JsonContract.Options);
        if (group is null || group.FormatVersion != 1 || !Guid.TryParseExact(group.GroupId, "D", out _)) throw new InvalidDataException("Unsupported sync-group metadata.");
        return group;
    }
    private static string CleanDeviceName(string name) => ObservationRules.ValidText(name, 128) ? name : "Hourstone PC";
    private static bool IsRecoverable(Exception ex) => ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or System.Text.DecoderFallbackException or ArgumentException;
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);
    public void Dispose() { lock (gate) { if (!disposed) { disposed = true; store.Dispose(); } } }
    private sealed record SyncGroup { [System.Text.Json.Serialization.JsonRequired] public int FormatVersion { get; init; } = 1; [System.Text.Json.Serialization.JsonRequired] public string GroupId { get; init; } = ""; }
}
