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
    }
}
