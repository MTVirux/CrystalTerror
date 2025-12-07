namespace CrystalTerror.Helpers
{
    using System;
    using System.Collections.Generic;
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
                return PluginInterfaceService.Interface.GetPluginConfig() as Configuration ?? new Configuration();
            }
            catch (Exception ex)
            {
                LogService.Log.Warning($"[ConfigHelper] Failed to load configuration: {ex.Message}");
                return new Configuration();
            }
        }

        /// <summary>
        /// Saves the plugin configuration to Dalamud's config system.
        /// Syncs the in-memory character list to the config before saving.
        /// </summary>
        /// <param name="config">The configuration to save.</param>
        /// <param name="characters">The in-memory character list to sync to the config.</param>
        /// <returns>True if save was successful, false otherwise.</returns>
        public static bool Save(Configuration config, List<StoredCharacter> characters)
        {
            try
            {
                // Sync in-memory characters to config before saving
                config.Characters = characters;
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
