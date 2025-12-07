namespace CrystalTerror.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using CrystalTerror.Services;

    /// <summary>
    /// Centralized helper for loading and saving plugin configuration.
    /// </summary>
    internal static class ConfigHelper
    {
        /// <summary>
        /// Loads the plugin configuration from Dalamud's config system.
        /// Returns a new Configuration instance if none exists or if loading fails.
        /// </summary>
        /// <returns>The loaded Configuration or a new instance.</returns>
        public static Configuration Load()
        {
            try
            {
               
                var cfg = PluginInterfaceService.Interface.GetPluginConfig() as Configuration ?? new Configuration();

                // If there are persisted characters, deduplicate and repair owner references
                try
                {
                    if (cfg.Characters != null && cfg.Characters.Count > 1)
                    {
                        cfg.Characters = DeduplicateCharacters(cfg.Characters);
                        RetainerHelper.RepairOwnerReferences(cfg.Characters);
                    }
                }
                catch (Exception ex)
                {
                    LogService.Log.Warning($"[ConfigHelper] Failed to dedupe/repair characters on load: {ex.Message}");
                }

                return cfg;
            }
            catch (Exception ex)
            {
                LogService.Log.Warning($"[ConfigHelper] Failed to load configuration: {ex.Message}");
                return new Configuration();
            }
        }

        /// <summary>
        /// Saves the plugin configuration to Dalamud's config system and syncs characters.
        /// Syncs the in-memory character list to the config before saving.
        /// </summary>
        /// <param name="config">The configuration to save.</param>
        /// <param name="characters">The in-memory character list to sync to the config.</param>
        /// <returns>True if save was successful, false otherwise.</returns>
        public static bool SaveAndSync(Configuration config, List<StoredCharacter> characters)
        {
            try
            {
                // Sync in-memory characters to config before saving
                LogService.Log.Information("[ConfigHelper] Syncing characters to config before save.");
                // Log all characters (pre-dedupe):
                foreach (var character in characters)
                {
                    LogService.Log.Information($"[ConfigHelper] Character: {character.Name}:{character.World} with {character.Retainers.Count} retainers.");
                }

                // Deduplicate characters before persisting and keep the in-memory list in sync.
                var deduped = DeduplicateCharacters(characters);
                characters.Clear();
                characters.AddRange(deduped);
                config.Characters = deduped;
                PluginInterfaceService.Interface.SavePluginConfig(config);
                return true;
            }
            catch (Exception ex)
            {
                LogService.Log.Error($"[ConfigHelper] Failed to save configuration: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Removes duplicate characters (case-insensitive Name+World) from the provided list.
        /// If duplicates are found the most recently-updated entry is kept.
        /// </summary>
        private static List<StoredCharacter> DeduplicateCharacters(IEnumerable<StoredCharacter> characters)
        {
            if (characters == null)
                return new List<StoredCharacter>();

            var grouped = characters
                .Where(c => c != null)
                .GroupBy(c => (c.Name ?? string.Empty).Trim().ToLowerInvariant() + ":" + (c.World ?? string.Empty).Trim().ToLowerInvariant());

            var result = new List<StoredCharacter>();
            foreach (var g in grouped)
            {
                var keep = g
                    .OrderByDescending(c => c.LastUpdateUtc)
                    .ThenByDescending(c => c.Retainers?.Count ?? 0)
                    .First();
                result.Add(keep);
            }

            if (result.Count != characters.Count())
                LogService.Log.Information($"[ConfigHelper] Deduplicated characters: original={characters.Count()}, deduped={result.Count}");

            // Repair owner references on the deduped list before returning so callers get fully repaired objects
            try
            {
                RetainerHelper.RepairOwnerReferences(result);
            }
            catch (Exception ex)
            {
                LogService.Log.Warning($"[ConfigHelper] Failed to repair owner references after dedupe: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Saves the plugin configuration to Dalamud's config system without syncing characters.
        /// Use this when only non-character settings have changed.
        /// </summary>
        /// <param name="config">The configuration to save.</param>
        /// <returns>True if save was successful, false otherwise.</returns>
        public static bool Save(Configuration config)
        {
            try
            {
                PluginInterfaceService.Interface.SavePluginConfig(config);
                return true;
            }
            catch (Exception ex)
            {
                LogService.Log.Error($"[ConfigHelper] Failed to save configuration: {ex.Message}");
                return false;
            }
        }
    }
}
