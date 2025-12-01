using System;

namespace CrystalTerror
{
    [Serializable]
    public class StoredCharactersContainer
    {
        // key: character name (as used in UI), value: stored data
        public System.Collections.Generic.Dictionary<string, StoredCharacter> ByCharacter { get; set; } = new();
    }
}
