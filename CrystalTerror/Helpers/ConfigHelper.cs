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
            Svc.Log.Debug($"[ConfigHelper] Saving config with {characters.Count} characters");

            // Deduplicate characters before persisting
            var deduped = DeduplicateCharacters(characters);
            characters.Clear();
            characters.AddRange(deduped);
            config.Characters = deduped;
            
            Svc.PluginInterface.SavePluginConfig(config);
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
    /// </summary>
    private static List<StoredCharacter> DeduplicateCharacters(IEnumerable<StoredCharacter> characters)
    {
        if (characters == null)
            return new List<StoredCharacter>();

        var result = new List<StoredCharacter>();
        var seenCIDs = new HashSet<ulong>();
        var seenNameWorlds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Sort by LastUpdateUtc descending so we keep the most recent
        var sorted = characters
            .Where(c => c != null)
            .OrderByDescending(c => c.LastUpdateUtc)
            .ThenByDescending(c => c.Retainers?.Count ?? 0);

        foreach (var c in sorted)
        {
            // Check ContentId first (most reliable)
            if (c.ContentId != 0)
            {
                if (seenCIDs.Contains(c.ContentId))
                    continue;
                seenCIDs.Add(c.ContentId);
            }
            else
            {
                // Fall back to Name+World
                var key = $"{c.Name?.Trim() ?? ""}@{c.World?.Trim() ?? ""}";
                if (seenNameWorlds.Contains(key))
                    continue;
                seenNameWorlds.Add(key);
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
