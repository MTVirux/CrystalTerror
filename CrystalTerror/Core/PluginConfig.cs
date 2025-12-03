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
    }
}
