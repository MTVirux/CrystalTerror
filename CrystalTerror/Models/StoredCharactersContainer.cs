using System;

namespace CrystalTerror
{
    [Serializable]
    /// <summary>
    /// Container holding stored data for all characters known to the plugin.
    /// </summary>
    public class StoredCharactersContainer
    {
        /// <summary>Map of canonical character key to its stored <see cref="StoredCharacter"/> data.</summary>
        public System.Collections.Generic.Dictionary<string, StoredCharacter> ByCharacter { get; set; } = new();
    }
}
