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
                
                Svc.PluginInterface.SavePluginConfig(config);
                _lastSaveTime = now;
                
                Svc.Log.Debug($"[ConfigHelper] SaveAndSync: Completed successfully with {characters.Count} characters");
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
