namespace CrystalTerror.Helpers;

/// <summary>
/// Centralized helper for loading and saving plugin configuration.
/// Uses ECommons Svc for service access.
/// </summary>
internal static class ConfigHelper
{
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
    /// </summary>
    public static bool SaveAndSync(Configuration config, List<StoredCharacter> characters)
    {
        try
        {
            var originalCount = characters.Count;
            Svc.Log.Debug($"[ConfigHelper] Saving config with {originalCount} characters");

            // Deduplicate characters before persisting
            var deduped = DeduplicateCharacters(characters);
            
            // Safety check: if deduplication removed too many characters, something went wrong
            if (deduped.Count == 0 && originalCount > 0)
            {
                Svc.Log.Error($"[ConfigHelper] Deduplication would remove all {originalCount} characters! Aborting save to prevent data loss.");
                return false;
            }
            
            // Log if any characters were removed by deduplication
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
            
            // Update the list safely - clear and refill only after we're sure dedup succeeded
            characters.Clear();
            characters.AddRange(deduped);
            config.Characters = deduped;
            
            Svc.PluginInterface.SavePluginConfig(config);
            Svc.Log.Debug($"[ConfigHelper] Config saved successfully with {deduped.Count} characters");
            return true;
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[ConfigHelper] Failed to save configuration: {ex.Message}");
            return false;
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
    /// Saves the configuration without syncing the character list.
    /// </summary>
    public static bool Save(Configuration config)
    {
        try
        {
            Svc.PluginInterface.SavePluginConfig(config);
            return true;
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[ConfigHelper] Failed to save configuration: {ex.Message}");
            return false;
        }
    }
}
