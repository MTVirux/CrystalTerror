using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace CrystalTerror
{
    [Serializable]
    public class PluginConfig : IPluginConfiguration
    {
        /// <summary>Schema/version for Dalamud config migrations.</summary>
        public int Version { get; set; } = 1;

        /// <summary>Persisted list of known characters.</summary>
        public List<StoredCharacter> Characters { get; set; } = new List<StoredCharacter>();
        
        /// <summary>Enables edit mode in the UI to reorder characters.</summary>
        public bool EditMode { get; set; } = false;

        /// <summary>Controls how characters are sorted in the main UI.</summary>
        public CharacterSort CharacterSort { get; set; } = CharacterSort.Alphabetical;

        /// <summary>Which elements to include when importing and showing inventories.</summary>
        public List<Element> EnabledElements { get; set; } = new List<Element>
        {
            Element.Fire,
            Element.Ice,
            Element.Wind,
            Element.Lightning,
            Element.Earth,
            Element.Water
        };

        /// <summary>Which crystal sizes/types to include when importing and showing inventories.</summary>
        public List<CrystalType> EnabledTypes { get; set; } = new List<CrystalType>
        {
            CrystalType.Shard,
            CrystalType.Crystal,
            CrystalType.Cluster
        };

        /// <summary>If true, suppress warnings about missing or disabled external plugins in the UI.</summary>
        public bool IgnoreMissingPluginWarnings { get; set; } = false;

        /// <summary>Warning thresholds and colors for inventory caps.</summary>
        public WarningSettings Warnings { get; set; } = new WarningSettings();
    }

    [Serializable]
    public class WarningSettings
    {
        public WarningLevel Level1 { get; set; } = new WarningLevel { Threshold = 8000, Color = new float[] { 1f, 0.9f, 0.0f, 1f }, Enabled = true };
        public WarningLevel Level2 { get; set; } = new WarningLevel { Threshold = 9000, Color = new float[] { 1f, 0.6f, 0.15f, 1f }, Enabled = true };
        public WarningLevel Level3 { get; set; } = new WarningLevel { Threshold = 9800, Color = new float[] { 1f, 0.15f, 0.15f, 1f }, Enabled = true };
    }

    [Serializable]
    public class WarningLevel
    {
        // Whether this warning level is active and should be considered when coloring counts
        public bool Enabled { get; set; } = true;
        // Threshold near the cap (0..9999)
        public int Threshold { get; set; }
        // RGBA floats 0..1
        public float[] Color { get; set; } = new float[] { 1f, 1f, 1f, 1f };
    }
}
