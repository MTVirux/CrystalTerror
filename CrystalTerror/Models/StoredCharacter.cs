using System;

namespace CrystalTerror
{
    [Serializable]
    /// <summary>
    /// Persisted data for a single character: name, timestamp and inventory counts.
    /// </summary>
    public class StoredCharacter
    {
        /// <summary>Character display name (may include world suffix in canonical form).</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>UTC timestamp of the last update to this stored data.</summary>
        public System.DateTime LastUpdatedUtc { get; set; } = System.DateTime.MinValue;
        /// <summary>Aggregate item counts for the character's own inventory (by item key).</summary>
        public System.Collections.Generic.Dictionary<string, long> PlayerCounts { get; set; } = new();
        /// <summary>Per-retainer counts keyed by retainer display name.</summary>
        public System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, long>> RetainerCounts { get; set; } = new();
    }
}
