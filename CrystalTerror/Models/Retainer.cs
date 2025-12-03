using System;

namespace CrystalTerror
{
    [Serializable]
    /// <summary>
    /// Represents a retainer owned by a character: name, world and its inventory.
    /// </summary>
    public class Retainer
    {
        /// <summary>Retainer display name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>World/server the retainer is on.</summary>
        public string World { get; set; } = string.Empty;

        /// <summary>Item counts for the retainer's inventory.</summary>
        public Inventory Inventory { get; set; } = new Inventory();

        /// <summary>Reference to the character that owns this retainer.</summary>
        public StoredCharacter OwnerCharacter { get; set; } = null;
    }
}
