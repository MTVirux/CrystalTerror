using System;

namespace CrystalTerror
{
    [Serializable]
    public class StoredCharacter
    {
        public string Name { get; set; } = string.Empty;
        public System.DateTime LastUpdatedUtc { get; set; } = System.DateTime.MinValue;
        // Aggregate counts for the character's own inventory
        public System.Collections.Generic.Dictionary<string, long> PlayerCounts { get; set; } = new();
        // Per-retainer counts: retainer display name -> counts
        public System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, long>> RetainerCounts { get; set; } = new();
    }
}
