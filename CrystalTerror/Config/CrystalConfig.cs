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

        /// <summary>How characters should be ordered in the UI lists.</summary>
        public CharacterSortMode CharacterOrder { get; set; } = CharacterSortMode.AlphabeticalAsc;

        /// <summary>How worlds should be ordered in the UI lists (separate from character ordering).</summary>
        public WorldSortMode WorldOrder { get; set; } = WorldSortMode.None;

        /// <summary>Custom manual ordering for worlds. Each entry is a world name, top-to-bottom order.</summary>
        public System.Collections.Generic.List<string> CustomWorldOrder { get; set; } = new System.Collections.Generic.List<string>();

        /// <summary>Custom manual ordering for characters. Each entry may be a canonical key (`Name@World`) or display name.</summary>
        public System.Collections.Generic.List<string> CustomCharacterOrder { get; set; } = new System.Collections.Generic.List<string>();

        /// <summary>Dummy save called by Dalamud to persist configuration.</summary>
        public void Save() { }
    }

    /// <summary>Available character ordering modes for the UI.</summary>
    public enum CharacterSortMode
    {
        AlphabeticalAsc = 0,
        AlphabeticalDesc = 1,
        LastUpdatedDesc = 2,
        LastUpdatedAsc = 3,
        TotalCrystalsDesc = 4,
        TotalCrystalsAsc = 5,
        WorldAsc = 6,
        WorldDesc = 7,
        Custom = 8,
        AutoRetainer = 9,
    }

    /// <summary>Available world ordering modes for the UI.</summary>
    public enum WorldSortMode
    {
        None = 0,
        WorldAsc = 1,
        WorldDesc = 2,
        Custom = 3,
    }
}
