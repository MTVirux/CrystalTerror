using System;
using System.Collections.Generic;

namespace CrystalTerror
{
    [Serializable]
    /// <summary>
    /// Persisted data for a single character: identity, account info, timestamp and retainers.
    /// </summary>
    public class StoredCharacter
    {
        /// <summary>Character display name.</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>Character world/server.</summary>
        public string World { get; set; } = string.Empty;
        /// <summary>Service account identifier for this character's account.</summary>
        public int ServiceAccount { get; set; } = 0;
        /// <summary>UTC timestamp of the last update to this stored data.</summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.MinValue;
        /// <summary>List of retainers associated with this character.</summary>
        public List<Retainer> Retainers { get; set; } = new();
        /// <summary>Inventory for this character.</summary>
        public Inventory Inventory { get; set; } = new Inventory();
    }
}
