namespace CrystalTerror;

/// <summary>
/// Represents a retainer owned by a character: name, world and its inventory.
/// </summary>
[Serializable]
public class Retainer
{
    /// <summary>Retainer display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Persisted retainer identifier (AutoRetainer ID).</summary>
    public ulong Atid { get; set; }

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

    private bool _enableAutoVenture = true;
    private bool _isIgnored = false;

    /// <summary>
    /// If true, this retainer will be included in automatic venture handling.
    /// Enabling this will also clear any ignore flag.
    /// </summary>
    public bool EnableAutoVenture
    {
        get => _enableAutoVenture;
        set
        {
            _enableAutoVenture = value;
            if (_enableAutoVenture)
            {
                // Enabling auto ventures should automatically unignore the retainer
                _isIgnored = false;
            }
        }
    }

    /// <summary>
    /// If true, this retainer is hidden from the main window UI and excluded from automatic ventures.
    /// </summary>
    public bool IsIgnored
    {
        get => _isIgnored;
        set
        {
            _isIgnored = value;
            if (_isIgnored)
            {
                _enableAutoVenture = false;
            }
        }
    }

    /// <summary>Item counts for the retainer's inventory.</summary>
    public Inventory Inventory { get; set; } = new Inventory();

    private StoredCharacter _ownerCharacter = default!;
    
    /// <summary>Reference to the character that owns this retainer. Required — never null.</summary>
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

    /// <summary>
    /// Parameterless constructor for serializers/deserializers.
    /// </summary>
    public Retainer() { }
}
