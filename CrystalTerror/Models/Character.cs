namespace CrystalTerror;

/// <summary>
/// Persisted data for a single character: identity, account info, timestamp and retainers.
/// Uses ContentId (CID) as the primary identifier for reliable persistence.
/// </summary>
[Serializable]
public class StoredCharacter
{
    /// <summary>
    /// Unique Content ID from the game. This is the primary identifier for the character.
    /// A value of 0 indicates the character was imported before CID tracking was implemented.
    /// </summary>
    public ulong ContentId { get; set; } = 0;

    /// <summary>Character display name.</summary>
    public required string Name { get; set; }
    
    /// <summary>Character world/server name.</summary>
    public required string World { get; set; }
    
    /// <summary>Home world row ID for reliable world identification.</summary>
    public uint HomeWorldId { get; set; } = 0;
    
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

    /// <summary>
    /// Gets a unique key for this character based on ContentId (preferred) or Name@World (fallback).
    /// </summary>
    public string UniqueKey => ContentId != 0 
        ? $"CID:{ContentId:X16}" 
        : $"{Name?.Trim() ?? "Unknown"}@{World?.Trim() ?? "Unknown"}";

    /// <summary>
    /// Checks if this character matches another by ContentId (if available) or Name+World.
    /// </summary>
    public bool Matches(StoredCharacter other)
    {
        if (other == null) return false;
        
        // If both have ContentIds, compare by ContentId (most reliable)
        if (ContentId != 0 && other.ContentId != 0)
            return ContentId == other.ContentId;
        
        // Fall back to Name+World comparison
        return string.Equals(Name?.Trim(), other.Name?.Trim(), StringComparison.OrdinalIgnoreCase) &&
               string.Equals(World?.Trim(), other.World?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if this character matches the given ContentId.
    /// </summary>
    public bool MatchesCID(ulong contentId)
    {
        return contentId != 0 && ContentId == contentId;
    }
}
