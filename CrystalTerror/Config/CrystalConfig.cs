using System;
using Dalamud.Configuration;

namespace CrystalTerror
{
    [Serializable]
    public class CrystalConfig : IPluginConfiguration
    {
        public int Version { get; set; } = 1;

        public bool ShowPlayer { get; set; } = true;
        public bool ShowRetainers { get; set; } = true;
        public System.Collections.Generic.Dictionary<string, bool> ElementsEnabled { get; set; } = new()
        {
            { "Fire", true },
            { "Ice", true },
            { "Wind", true },
            { "Earth", true },
            { "Lightning", true },
            { "Water", true },
        };

        public System.Collections.Generic.Dictionary<string, bool> TypesEnabled { get; set; } = new()
        {
            { "Shard", true },
            { "Crystal", true },
            { "Cluster", true },
        };

        // Map of retainerId -> display name (persisted for stable labeling)
        public System.Collections.Generic.Dictionary<ulong, string> RetainerNames { get; set; } = new();

        // When true, disabled/unavailable retainers are skipped from the UI
        public bool SkipDisabledRetainers { get; set; } = true;

        // When true, subscribe to Dalamud inventory events to trigger immediate scans
        public bool UseInventoryEvents { get; set; } = true;

        // Persisted scan results across characters
        public StoredCharactersContainer StoredCharacters { get; set; } = new StoredCharactersContainer();

        // IPluginConfiguration
        public void Save() { }
    }
}
