namespace CrystalTerror.Helpers;

/// <summary>
/// Calculator for global crystal/shard capacity and counts across a character and their retainers.
/// Used by the automatic venture system to determine optimal venture assignments.
/// </summary>
public static class VentureCapacityCalculator
{
    /// <summary>
    /// Maximum count per element×type per storage slot (character or retainer).
    /// </summary>
    public const long MaxPerSlot = 9999;

    /// <summary>
    /// All elements in standard order.
    /// </summary>
    public static readonly Element[] AllElements = 
    { 
        Element.Fire, Element.Ice, Element.Wind, 
        Element.Earth, Element.Lightning, Element.Water 
    };

    /// <summary>
    /// Crystal types that can be obtained via ventures (excludes Cluster).
    /// </summary>
    public static readonly CrystalType[] VentureTypes = 
    { 
        CrystalType.Shard, CrystalType.Crystal 
    };

    /// <summary>
    /// Calculate the total count of each element×type across character inventory and all retainer inventories.
    /// </summary>
    /// <param name="character">The character to analyze.</param>
    /// <returns>Dictionary mapping (Element, CrystalType) to total count.</returns>
    public static Dictionary<(Element, CrystalType), long> CalculateGlobalCounts(StoredCharacter character)
    {
        var counts = new Dictionary<(Element, CrystalType), long>();

        foreach (var element in AllElements)
        {
            foreach (var type in VentureTypes)
            {
                long total = 0;

                // Add character's inventory
                if (character.Inventory != null)
                    total += character.Inventory.GetCount(element, type);

                // Add all retainers' inventories
                foreach (var retainer in character.Retainers ?? Enumerable.Empty<Retainer>())
                {
                    if (retainer.Inventory != null && !retainer.IsIgnored)
                        total += retainer.Inventory.GetCount(element, type);
                }

                counts[(element, type)] = total;
            }
        }

        return counts;
    }

    /// <summary>
    /// Calculate the maximum capacity for each element×type based on character + retainer count.
    /// Capacity = 9999 × (1 character + N retainers).
    /// </summary>
    /// <param name="character">The character to analyze.</param>
    /// <returns>Dictionary mapping (Element, CrystalType) to maximum capacity.</returns>
    public static Dictionary<(Element, CrystalType), long> CalculateGlobalCapacity(StoredCharacter character)
    {
        var capacities = new Dictionary<(Element, CrystalType), long>();

        // Count non-ignored retainers
        int retainerCount = character.Retainers?.Count(r => !r.IsIgnored) ?? 0;
        long capacity = MaxPerSlot * (1 + retainerCount);

        foreach (var element in AllElements)
        {
            foreach (var type in VentureTypes)
            {
                capacities[(element, type)] = capacity;
            }
        }

        return capacities;
    }

    /// <summary>
    /// Calculate pending venture rewards based on active ventures on retainers.
    /// Returns +rewardAmount for each retainer with an active crystal/shard venture.
    /// </summary>
    /// <param name="character">The character to analyze.</param>
    /// <param name="rewardAmount">Expected reward per venture (default 120).</param>
    /// <returns>Dictionary mapping (Element, CrystalType) to pending reward count.</returns>
    public static Dictionary<(Element, CrystalType), long> GetPendingVentureRewards(StoredCharacter character, int rewardAmount)
    {
        var pending = new Dictionary<(Element, CrystalType), long>();

        // Initialize all to 0
        foreach (var element in AllElements)
        {
            foreach (var type in VentureTypes)
            {
                pending[(element, type)] = 0;
            }
        }

        // Check each retainer for active ventures
        foreach (var retainer in character.Retainers ?? Enumerable.Empty<Retainer>())
        {
            if (retainer.IsIgnored || !retainer.HasActiveVenture)
                continue;

            var ventureMapping = VentureIdToElementType(retainer.CurrentVentureId);
            if (ventureMapping.HasValue)
            {
                var (element, type) = ventureMapping.Value;
                pending[(element, type)] += rewardAmount;
            }
        }

        return pending;
    }

    /// <summary>
    /// Calculate effective counts = global counts + pending venture rewards.
    /// This represents the expected count after all active ventures complete.
    /// </summary>
    /// <param name="character">The character to analyze.</param>
    /// <param name="rewardAmount">Expected reward per venture (default 120).</param>
    /// <returns>Dictionary mapping (Element, CrystalType) to effective count.</returns>
    public static Dictionary<(Element, CrystalType), long> CalculateEffectiveCounts(StoredCharacter character, int rewardAmount)
    {
        var global = CalculateGlobalCounts(character);
        var pending = GetPendingVentureRewards(character, rewardAmount);

        var effective = new Dictionary<(Element, CrystalType), long>();
        foreach (var key in global.Keys)
        {
            effective[key] = global[key] + pending.GetValueOrDefault(key, 0);
        }

        return effective;
    }

    /// <summary>
    /// Check if a type is "full" based on effective count, capacity, and threshold.
    /// </summary>
    /// <param name="effectiveCount">Current effective count (including pending rewards).</param>
    /// <param name="capacity">Maximum capacity for this type.</param>
    /// <param name="threshold">User-defined threshold (0 = no threshold, use capacity only).</param>
    /// <param name="rewardAmount">Expected reward for next venture.</param>
    /// <returns>True if assigning another venture would exceed capacity or threshold.</returns>
    public static bool IsTypeFull(long effectiveCount, long capacity, long threshold, int rewardAmount)
    {
        // Determine the limit: threshold if set, otherwise capacity
        long limit = threshold > 0 ? Math.Min(threshold, capacity) : capacity;

        // Full if adding another venture reward would exceed the limit
        return effectiveCount + rewardAmount > limit;
    }

    /// <summary>
    /// Map a VentureId to its corresponding (Element, CrystalType).
    /// Returns null for non-crystal/shard ventures (e.g., QuickExploration).
    /// </summary>
    /// <param name="ventureId">The venture ID to map.</param>
    /// <returns>The element and type, or null if not a crystal/shard venture.</returns>
    public static (Element, CrystalType)? VentureIdToElementType(uint? ventureId)
    {
        if (!ventureId.HasValue)
            return null;

        return (VentureId)ventureId.Value switch
        {
            VentureId.Fire_Shard => (Element.Fire, CrystalType.Shard),
            VentureId.Fire_Crystal => (Element.Fire, CrystalType.Crystal),
            VentureId.Ice_Shard => (Element.Ice, CrystalType.Shard),
            VentureId.Ice_Crystal => (Element.Ice, CrystalType.Crystal),
            VentureId.Wind_Shard => (Element.Wind, CrystalType.Shard),
            VentureId.Wind_Crystal => (Element.Wind, CrystalType.Crystal),
            VentureId.Earth_Shard => (Element.Earth, CrystalType.Shard),
            VentureId.Earth_Crystal => (Element.Earth, CrystalType.Crystal),
            VentureId.Lightning_Shard => (Element.Lightning, CrystalType.Shard),
            VentureId.Lightning_Crystal => (Element.Lightning, CrystalType.Crystal),
            VentureId.Water_Shard => (Element.Water, CrystalType.Shard),
            VentureId.Water_Crystal => (Element.Water, CrystalType.Crystal),
            _ => null
        };
    }

    /// <summary>
    /// Generate the key string for per-type settings dictionary.
    /// </summary>
    public static string GetPerTypeKey(Element element, CrystalType type)
    {
        return $"{element}_{type}";
    }

    /// <summary>
    /// Parse a per-type key string back to Element and CrystalType.
    /// </summary>
    public static (Element element, CrystalType type)? ParsePerTypeKey(string key)
    {
        var parts = key.Split('_');
        if (parts.Length != 2)
            return null;

        if (Enum.TryParse<Element>(parts[0], out var element) &&
            Enum.TryParse<CrystalType>(parts[1], out var type))
        {
            return (element, type);
        }

        return null;
    }
}
