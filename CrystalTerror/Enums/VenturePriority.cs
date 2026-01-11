namespace CrystalTerror;

/// <summary>
/// Priority preference for automatic venture assignment when counts are tied.
/// </summary>
public enum VenturePriority
{
    /// <summary>No preference - use natural ordering (element order, then type).</summary>
    Balanced,
    
    /// <summary>Prefer crystal ventures over shard ventures when counts are equal.</summary>
    PreferCrystals,
    
    /// <summary>Prefer shard ventures over crystal ventures when counts are equal.</summary>
    PreferShards,
}
