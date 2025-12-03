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

        /// <summary>Persisted AllaganTools/retainer identifier (atid).</summary>
        public ulong atid { get; set; }

        /// <summary>Item counts for the retainer's inventory.</summary>
        public Inventory Inventory { get; set; } = new Inventory();

        /// <summary>Reference to the character that owns this retainer. Required — never null.</summary>
        private StoredCharacter _ownerCharacter = default!;
        public StoredCharacter OwnerCharacter
        {
            get => _ownerCharacter;
            set => _ownerCharacter = value ?? throw new ArgumentNullException(nameof(value), "OwnerCharacter cannot be null.");
        }

        /// <summary>
        /// Create a retainer with the required owner reference.
        /// </summary>
        /// <param name="owner">The owning <see cref="StoredCharacter"/>. Must not be null.</param>
        public Retainer(StoredCharacter owner)
        {
            OwnerCharacter = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        // Parameterless constructor kept for serializers/deserializers.
        public Retainer() { }
    }
}
