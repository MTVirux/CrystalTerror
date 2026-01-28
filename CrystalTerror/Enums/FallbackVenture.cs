namespace CrystalTerror;

/// <summary>
/// Options for what to do when all crystal/shard types are above threshold (full).
/// </summary>
public enum FallbackVentureMode
{
    /// <summary>Assign a specific venture when crystals are full. Venture ID is stored separately.</summary>
    SpecificVenture,
    
    /// <summary>Skip venture assignment entirely when crystals are full (let AutoRetainer decide).</summary>
    Skip,
}
