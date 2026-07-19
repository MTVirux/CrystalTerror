namespace CrystalTerror.Helpers;

/// <summary>
/// Centralized helper for loading and saving plugin configuration.
/// Uses ECommons Svc for service access.
/// Thread-safe operations to prevent data loss during concurrent access.
/// </summary>
internal static class ConfigHelper
{
    /// <summary>
    /// Lock object for thread-safe access to character list operations.
    /// Prevents race conditions during concurrent imports, merges, and saves.
    /// </summary>
    private static readonly object _characterLock = new();

    /// <summary>
    /// Timestamp of last save operation (for throttling).
    /// </summary>
    private static DateTime _lastSaveTime = DateTime.MinValue;

    /// <summary>
    /// Minimum interval between saves in milliseconds (similar to AutoRetainer's approach).
    /// </summary>
    private const int MinSaveIntervalMs = 1000;

    /// <summary>
    /// Serializes the background config writer state. Distinct from <see cref="_characterLock"/>
    /// so the (potentially slow) disk write never blocks character-list mutations.
    /// </summary>
    private static readonly object _writerLock = new();

    /// <summary>Latest config snapshot awaiting a background write (latest-wins coalescing).</summary>
    private static Configuration? _pendingSnapshot;

    /// <summary>Handle to the in-flight background writer task (for shutdown flushing).</summary>
    private static Task _writerTask = Task.CompletedTask;

    /// <summary>True while a background writer task is draining pending snapshots.</summary>
    private static bool _writerRunning;

    /// <summary>
    /// Loads the plugin configuration from Dalamud's config system.
    /// Returns a new Configuration instance if none exists or if loading fails.
    /// </summary>
    public static Configuration Load()
    {
        try
        {
            var cfg = Svc.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

            // Deduplicate and repair owner references for persisted characters
            try
            {
                if (cfg.Characters != null && cfg.Characters.Count > 0)
                {
                    cfg.Characters = DeduplicateCharacters(cfg.Characters);
                    RetainerHelper.RepairOwnerReferences(cfg.Characters);
                    
                    // Migrate characters without ContentId if they match current player
                    MigrateCharacterContentIds(cfg.Characters);
                }
            }
            catch (Exception ex)
            {
                Svc.Log.Warning($"[ConfigHelper] Failed to process characters on load: {ex.Message}");
            }

            // v2: seed per-job fallback ventures from the legacy single fallback
            try
            {
                if (cfg.Version < 2)
                {
                    var legacyFallback = cfg.AutoVentureFallbackVentureId;
                    cfg.SetFallbackVentureId(16, legacyFallback);
                    cfg.SetFallbackVentureId(17, legacyFallback);
                    cfg.SetFallbackVentureId(18, legacyFallback);
                    cfg.Version = 2;
                    Svc.Log.Debug($"[ConfigHelper] Migrated fallback venture {legacyFallback} to per-job (config v2)");
                }
            }
            catch (Exception ex)
            {
                Svc.Log.Warning($"[ConfigHelper] Failed to migrate per-job fallback ventures: {ex.Message}");
            }

            return cfg;
        }
        catch (Exception ex)
        {
            Svc.Log.Warning($"[ConfigHelper] Failed to load configuration: {ex.Message}");
            return new Configuration();
        }
    }

    /// <summary>
    /// Migrates characters that don't have ContentId by matching them with the current player.
    /// </summary>
    private static void MigrateCharacterContentIds(List<StoredCharacter> characters)
    {
        if (!Player.Available || Player.CID == 0)
            return;

        foreach (var c in characters)
        {
            if (c.ContentId == 0 &&
                string.Equals(c.Name?.Trim(), Player.Name, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(c.World?.Trim(), Player.HomeWorldName, StringComparison.OrdinalIgnoreCase))
            {
                c.ContentId = Player.CID;
                c.HomeWorldId = Player.HomeWorld.RowId;
                Svc.Log.Debug($"[ConfigHelper] Migrated CID for {c.Name}@{c.World} to {c.ContentId:X16}");
            }
        }
    }

    /// <summary>
    /// Saves the plugin configuration and syncs the character list.
    /// Thread-safe with locking and throttling to prevent race conditions.
    /// </summary>
    /// <param name="config">The configuration to save.</param>
    /// <param name="characters">The character list to sync (will be updated in-place atomically).</param>
    /// <param name="force">If true, bypasses throttling and forces an immediate save.</param>
    public static bool SaveAndSync(Configuration config, List<StoredCharacter> characters, bool force = false)
    {
        lock (_characterLock)
        {
            try
            {
                // Throttle saves unless forced (similar to AutoRetainer's EzThrottler pattern)
                var now = DateTime.UtcNow;
                if (!force && (now - _lastSaveTime).TotalMilliseconds < MinSaveIntervalMs)
                {
                    Svc.Log.Debug($"[ConfigHelper] Save throttled (last save {(now - _lastSaveTime).TotalMilliseconds:F0}ms ago)");
                    return true; // Not an error, just throttled
                }

                var originalCount = characters.Count;
                Svc.Log.Debug($"[ConfigHelper] SaveAndSync: Starting with {originalCount} characters (force={force})");

                // Deduplicate characters before persisting
                var deduped = DeduplicateCharacters(characters);
                
                // Safety check: if deduplication removed too many characters, something went wrong
                if (deduped.Count == 0 && originalCount > 0)
                {
                    Svc.Log.Error($"[ConfigHelper] CRITICAL: Deduplication would remove all {originalCount} characters! Aborting save to prevent data loss.");
                    return false;
                }
                
                // Log if any characters were removed by deduplication (this shouldn't happen normally)
                if (deduped.Count < originalCount)
                {
                    Svc.Log.Warning($"[ConfigHelper] Deduplication reduced character count from {originalCount} to {deduped.Count}");
                    foreach (var c in characters)
                    {
                        if (!deduped.Any(d => d.ContentId != 0 ? d.ContentId == c.ContentId : d.Matches(c)))
                        {
                            Svc.Log.Warning($"[ConfigHelper] Character removed by deduplication: {c.Name}@{c.World} (CID={c.ContentId:X16})");
                        }
                    }
                }

                // ATOMIC LIST UPDATE: Instead of Clear+AddRange (which has a race window),
                // we update the list contents atomically while holding the lock.
                // First, build a set of characters to keep vs remove
                var dedupedSet = new HashSet<StoredCharacter>(deduped);
                
                // Remove characters not in deduped set (in reverse order to avoid index issues)
                for (int i = characters.Count - 1; i >= 0; i--)
                {
                    if (!dedupedSet.Contains(characters[i]))
                    {
                        characters.RemoveAt(i);
                    }
                }
                
                // Add any new characters from deduped that aren't already present
                foreach (var d in deduped)
                {
                    if (!characters.Contains(d))
                    {
                        characters.Add(d);
                    }
                }
                
                // Sync to config
                config.Characters = new List<StoredCharacter>(characters);
                _lastSaveTime = now;

                if (force)
                {
                    // Durable synchronous write (used on shutdown). Flush the async writer first so
                    // we don't race the background thread writing the same file.
                    FlushPendingWrites();
                    Svc.PluginInterface.SavePluginConfig(config);
                    Svc.Log.Debug($"[ConfigHelper] SaveAndSync: forced synchronous save with {characters.Count} characters");
                    return true;
                }

                // Hand the (potentially slow) disk write to a background writer so it never blocks
                // the framework thread. Serialize an isolated snapshot under the lock so the writer
                // can't race main-thread mutations of the character graph.
                QueueBackgroundSave(CloneConfiguration(config));
                Svc.Log.Debug($"[ConfigHelper] SaveAndSync: queued background save with {characters.Count} characters");
                return true;
            }
            catch (Exception ex)
            {
                Svc.Log.Error($"[ConfigHelper] SaveAndSync failed: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }
    }

    /// <summary>
    /// Deep-clones a configuration via JSON round-trip so the background writer serializes a stable
    /// snapshot. Safe because the character graph's only cyclic reference (Retainer.OwnerCharacter)
    /// is [JsonIgnore], so the clone serializes identically to the live object.
    /// </summary>
    private static Configuration CloneConfiguration(Configuration config)
    {
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(config);
        return Newtonsoft.Json.JsonConvert.DeserializeObject<Configuration>(json) ?? config;
    }

    /// <summary>
    /// Queues a snapshot for background writing. Latest-wins: rapid saves coalesce to the newest
    /// snapshot, and writes never overlap (a single writer task drains the queue).
    /// </summary>
    private static void QueueBackgroundSave(Configuration snapshot)
    {
        lock (_writerLock)
        {
            _pendingSnapshot = snapshot;
            if (!_writerRunning)
            {
                _writerRunning = true;
                _writerTask = Task.Run(DrainSaveQueue);
            }
        }
    }

    private static void DrainSaveQueue()
    {
        while (true)
        {
            Configuration? toWrite;
            lock (_writerLock)
            {
                toWrite = _pendingSnapshot;
                _pendingSnapshot = null;
                if (toWrite == null)
                {
                    _writerRunning = false;
                    return;
                }
            }

            try
            {
                Svc.PluginInterface.SavePluginConfig(toWrite);
                Svc.Log.Debug("[ConfigHelper] Background config save completed");
            }
            catch (Exception ex)
            {
                Svc.Log.Error($"[ConfigHelper] Background save failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Cancels any queued snapshot and waits (bounded) for an in-flight background write to finish,
    /// so a forced synchronous save can take over the file without racing the writer thread.
    /// </summary>
    private static void FlushPendingWrites()
    {
        Task task;
        lock (_writerLock)
        {
            _pendingSnapshot = null;
            task = _writerTask;
        }

        try { task.Wait(TimeSpan.FromSeconds(5)); } catch { }
    }

    /// <summary>
    /// Removes duplicate characters. Now uses ContentId as primary dedup key when available.
    /// Also tracks Name+World to catch duplicates between CID and non-CID entries.
    /// </summary>
    private static List<StoredCharacter> DeduplicateCharacters(IEnumerable<StoredCharacter> characters)
    {
        if (characters == null)
            return new List<StoredCharacter>();

        var result = new List<StoredCharacter>();
        var seenCIDs = new HashSet<ulong>();
        var seenNameWorlds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Sort by ContentId presence first (prefer entries with CID), then by LastUpdateUtc descending
        var sorted = characters
            .Where(c => c != null)
            .OrderByDescending(c => c.ContentId != 0 ? 1 : 0) // Prefer entries with ContentId
            .ThenByDescending(c => c.LastUpdateUtc)
            .ThenByDescending(c => c.Retainers?.Count ?? 0);

        foreach (var c in sorted)
        {
            var nameWorldKey = $"{c.Name?.Trim() ?? ""}@{c.World?.Trim() ?? ""}";
            
            // Check ContentId first (most reliable)
            if (c.ContentId != 0)
            {
                if (seenCIDs.Contains(c.ContentId))
                {
                    Svc.Log.Debug($"[ConfigHelper] Skipping duplicate CID: {c.Name}@{c.World} (CID={c.ContentId:X16})");
                    continue;
                }
                
                // Also check if we've already seen this Name+World (could be a legacy entry that was upgraded)
                if (seenNameWorlds.Contains(nameWorldKey))
                {
                    Svc.Log.Debug($"[ConfigHelper] Skipping duplicate by Name+World (has CID): {c.Name}@{c.World} (CID={c.ContentId:X16})");
                    continue;
                }
                
                seenCIDs.Add(c.ContentId);
                seenNameWorlds.Add(nameWorldKey); // Track Name+World too to prevent legacy duplicates
            }
            else
            {
                // No ContentId - check Name+World
                if (seenNameWorlds.Contains(nameWorldKey))
                {
                    Svc.Log.Debug($"[ConfigHelper] Skipping duplicate by Name+World (no CID): {c.Name}@{c.World}");
                    continue;
                }
                seenNameWorlds.Add(nameWorldKey);
            }

            result.Add(c);
        }

        if (result.Count != characters.Count())
        {
            Svc.Log.Debug($"[ConfigHelper] Deduplicated: {characters.Count()} -> {result.Count}");
        }

        RetainerHelper.RepairOwnerReferences(result);
        return result;
    }

    /// <summary>
    /// Gets the character lock object for external synchronization.
    /// Use this when performing bulk operations on the character list.
    /// </summary>
    public static object CharacterLock => _characterLock;

    /// <summary>
    /// Saves the configuration without syncing the character list.
    /// Thread-safe with locking.
    /// </summary>
    public static bool Save(Configuration config)
    {
        lock (_characterLock)
        {
            try
            {
                Svc.PluginInterface.SavePluginConfig(config);
                Svc.Log.Debug("[ConfigHelper] Config saved (settings only)");
                return true;
            }
            catch (Exception ex)
            {
                Svc.Log.Error($"[ConfigHelper] Failed to save configuration: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Forces an immediate save, bypassing throttling.
    /// Use for critical saves like plugin shutdown.
    /// </summary>
    public static bool ForceSave(Configuration config, List<StoredCharacter> characters)
    {
        Svc.Log.Information("[ConfigHelper] Force save requested");
        return SaveAndSync(config, characters, force: true);
    }
}
