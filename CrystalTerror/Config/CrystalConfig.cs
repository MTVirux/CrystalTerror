using System;
using Dalamud.Configuration;

namespace CrystalTerror
{
    [Serializable]
    /// <summary>
    /// Persisted plugin configuration stored by Dalamud.
    /// </summary>
    public class CrystalConfig : IPluginConfiguration
    {
        /// <summary>Configuration version for migration purposes.</summary>
        public int Version { get; set; } = 1;

        /// <summary>Show the current player's row in the UI.</summary>
        public bool ShowPlayer { get; set; } = true;
        /// <summary>Show retainer rows in the UI.</summary>
        public bool ShowRetainers { get; set; } = true;
        /// <summary>Map of element name to enabled/disabled filter state.</summary>
        public System.Collections.Generic.Dictionary<string, bool> ElementsEnabled { get; set; } = new()
        {
            { "Fire", true },
            { "Ice", true },
            { "Wind", true },
            { "Earth", true },
            { "Lightning", true },
            { "Water", true },
        };

        /// <summary>Map of type (Shard/Crystal/Cluster) to enabled/disabled state.</summary>
        public System.Collections.Generic.Dictionary<string, bool> TypesEnabled { get; set; } = new()
        {
            { "Shard", true },
            { "Crystal", true },
            { "Cluster", true },
        };

        /// <summary>Map of retainer ID to persisted display name for stable labeling.</summary>
        public System.Collections.Generic.Dictionary<ulong, string> RetainerNames { get; set; } = new();

        /// <summary>When true, disabled or unavailable retainers will be omitted from the UI.</summary>
        public bool SkipDisabledRetainers { get; set; } = true;

        /// <summary>When true, the plugin subscribes to Dalamud inventory events to trigger immediate scans.</summary>
        public bool UseInventoryEvents { get; set; } = true;

        /// <summary>Persisted scan results stored across characters and retainers.</summary>
        public StoredCharactersContainer StoredCharacters { get; set; } = new StoredCharactersContainer();

        /// <summary>Dummy save called by Dalamud to persist configuration.</summary>
        public void Save() { }
    }
}
