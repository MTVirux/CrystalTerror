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
        public required string Name { get; set; }
        /// <summary>Character world/server.</summary>
        public required string World { get; set; }
        /// <summary>Service account identifier for this character's account.</summary>
        public required int ServiceAccount { get; set; } = 1;
        /// <summary>UTC timestamp of the last update to this stored data.</summary>
        public required DateTime LastUpdateUtc { get; set; }
        /// <summary>List of retainers associated with this character.</summary>
        public required List<Retainer> Retainers { get; set; }
        /// <summary>Inventory for this character.</summary>
        public required Inventory Inventory { get; set; }

        /// <summary>If true, this character is hidden from the main window UI.</summary>
        public bool IsIgnored { get; set; } = false;
    }
}
