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

        /// <summary>Persisted retainer identifier (atid).</summary>
        public ulong atid { get; set; }

        /// <summary>Retainer job/class id (if available from AutoRetainer offline data). Null = unknown.</summary>
        public int? Job { get; set; } = null;

        /// <summary>Retainer level.</summary>
        public int Level { get; set; } = 0;

        /// <summary>Retainer item level / average gear level.</summary>
        public int Ilvl { get; set; } = 0;

        /// <summary>Gathering stat for non-combat retainers (when available from AutoRetainer).</summary>
        public int Gathering { get; set; } = 0;

        /// <summary>Perception stat for non-combat retainers (when available from AutoRetainer).</summary>
        public int Perception { get; set; } = 0;

        /// <summary>If true, this retainer will be included in automatic venture handling. Defaults to true.</summary>
        public bool EnableAutoVenture { get; set; } = true;

        /// <summary>Item counts for the retainer's inventory.</summary>
        public Inventory Inventory { get; set; } = new Inventory();

        /// <summary>Reference to the character that owns this retainer. Required — never null.</summary>
        private StoredCharacter _ownerCharacter = default!;
            [Newtonsoft.Json.JsonIgnore]
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
