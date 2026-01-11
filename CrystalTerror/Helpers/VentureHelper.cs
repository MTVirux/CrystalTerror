using Dalamud.Plugin.Services;

namespace CrystalTerror.Helpers;

/// <summary>
/// Helper utilities for determining optimal ventures based on crystal/shard inventory.
/// Uses global capacity calculations to maximize storage across character + all retainers.
/// </summary>
public static class VentureHelper
{
    // Job IDs for gathering classes
    private const int JobMiner = 16;
    private const int JobBotanist = 17;
    private const int JobFisher = 18;

    /// <summary>
    /// Check if a retainer is a gathering class (MIN, BTN, or FSH).
    /// </summary>
    public static bool IsGatheringRetainer(Retainer retainer)
    {
        if (retainer?.Job == null)
            return false;

        var jobId = retainer.Job.Value;
        return jobId == JobMiner || jobId == JobBotanist || jobId == JobFisher;
    }

    /// <summary>
    /// Check if a retainer is a Fisher.
    /// </summary>
    public static bool IsFisher(Retainer retainer)
    {
        return retainer?.Job == JobFisher;
    }

    /// <summary>
    /// Check if a retainer is eligible for venture override based on job and stats.
    /// </summary>
    /// <param name="retainer">The retainer to check.</param>
    /// <param name="ventureType">The type of venture being assigned (Shard or Crystal).</param>
    /// <returns>True if the retainer is eligible for this venture type.</returns>
    public static bool IsRetainerEligibleForVenture(Retainer retainer, CrystalType ventureType)
    {
        if (retainer == null || retainer.Job == null)
            return false;

        var jobId = retainer.Job.Value;

        // Only MIN, BTN, or FSH retainers can be assigned crystal/shard ventures
        if (jobId != JobMiner && jobId != JobBotanist && jobId != JobFisher)
            return false;

        // Additional requirements for Crystal ventures (not Shards)
        if (ventureType == CrystalType.Crystal)
        {
            // Crystals require level > 26 and Gathering > 90
            if (retainer.Level <= 26 || retainer.Gathering <= 90)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Determine the venture ID for the crystal/shard type with the lowest effective count.
    /// Uses global capacity calculations across character + all retainers.
    /// Returns QuickExploration if all enabled types are at capacity/threshold.
    /// Returns null if retainer is ineligible (e.g., FSH disabled, non-gathering class).
    /// </summary>
    /// <param name="character">The character who owns this retainer (for global counts).</param>
    /// <param name="retainer">The retainer to assign a venture to.</param>
    /// <param name="config">Configuration for venture settings.</param>
    /// <param name="log">Optional logger for detailed output.</param>
    /// <returns>VentureId for optimal venture, QuickExploration if all full, or null if ineligible.</returns>
    public static VentureId? DetermineLowestCrystalVenture(StoredCharacter character, Retainer retainer, Configuration config, IPluginLog? log = null)
    {
        if (character == null) throw new ArgumentNullException(nameof(character));
        if (retainer == null) throw new ArgumentNullException(nameof(retainer));
        if (config == null) throw new ArgumentNullException(nameof(config));

        var jobAbbr = ClassJobExtensions.GetAbbreviation(retainer.Job);
        log?.Debug($"[VentureHelper] Analyzing venture for {retainer.Name} (Job: {jobAbbr}, Level: {retainer.Level}, Gathering: {retainer.Gathering})");

        // Check if retainer is a gathering class
        if (!IsGatheringRetainer(retainer))
        {
            log?.Debug($"[VentureHelper] {retainer.Name} is not a gathering retainer (Job: {jobAbbr}), skipping");
            return null;
        }

        // Check FSH eligibility
        if (IsFisher(retainer) && !config.AutoVentureFSHEnabled)
        {
            log?.Debug($"[VentureHelper] {retainer.Name} is FSH and FSH is disabled, skipping");
            return null;
        }

        // Calculate global capacity metrics
        var effectiveCounts = VentureCapacityCalculator.CalculateEffectiveCounts(character, config.AutoVentureRewardAmount);
        var capacities = VentureCapacityCalculator.CalculateGlobalCapacity(character);
        var pendingRewards = VentureCapacityCalculator.GetPendingVentureRewards(character, config.AutoVentureRewardAmount);

        log?.Debug($"[VentureHelper] Global capacity: {capacities.Values.FirstOrDefault()} per type (1 char + {character.Retainers?.Count(r => !r.IsIgnored) ?? 0} retainers)");

        // Build list of candidates
        var candidates = new List<(Element element, CrystalType type, long effectiveCount, long capacity, long threshold, bool isFull, VentureId ventureId)>();

        foreach (var element in VentureCapacityCalculator.AllElements)
        {
            foreach (var type in VentureCapacityCalculator.VentureTypes)
            {
                // Check global type toggles
                if (type == CrystalType.Shard && !config.AutoVentureShardsEnabled)
                    continue;
                if (type == CrystalType.Crystal && !config.AutoVentureCrystalsEnabled)
                    continue;

                // Check per-type enabled setting
                var perTypeSetting = config.GetPerTypeSetting(element, type);
                if (!perTypeSetting.Enabled)
                    continue;

                // Check retainer eligibility for this type
                if (!IsRetainerEligibleForVenture(retainer, type))
                    continue;

                // Get venture ID
                var ventureId = GetVentureId(element, type);
                if (!ventureId.HasValue)
                    continue;

                // Get counts
                var effectiveCount = effectiveCounts.GetValueOrDefault((element, type), 0);
                var capacity = capacities.GetValueOrDefault((element, type), 0);
                
                // Determine threshold: per-type if set, otherwise global
                var threshold = perTypeSetting.Threshold > 0 ? perTypeSetting.Threshold : config.AutoVentureThreshold;

                // Check if type is full
                var isFull = VentureCapacityCalculator.IsTypeFull(effectiveCount, capacity, threshold, config.AutoVentureRewardAmount);

                candidates.Add((element, type, effectiveCount, capacity, threshold, isFull, ventureId.Value));
            }
        }

        if (candidates.Count == 0)
        {
            log?.Debug($"[VentureHelper] No eligible candidates for {retainer.Name}");
            return null;
        }

        // Log all candidates
        log?.Debug($"[VentureHelper] {candidates.Count} candidate(s) for {retainer.Name}:");
        foreach (var c in candidates.OrderBy(x => x.effectiveCount))
        {
            var pending = pendingRewards.GetValueOrDefault((c.element, c.type), 0);
            var status = c.isFull ? "FULL" : "available";
            log?.Debug($"  - {c.element} {c.type}: {c.effectiveCount} effective ({c.effectiveCount - pending} current + {pending} pending), capacity={c.capacity}, threshold={c.threshold}, {status}");
        }

        // Filter out full types
        var availableCandidates = candidates.Where(c => !c.isFull).ToList();

        if (availableCandidates.Count == 0)
        {
            // All types are full - assign Quick Exploration
            log?.Information($"[VentureHelper] All enabled types are at capacity/threshold for {retainer.Name}, assigning Quick Exploration");
            return VentureId.QuickExploration;
        }

        // Sort by effective count (lowest first), then apply priority tiebreaker
        var sorted = availableCandidates
            .OrderBy(c => c.effectiveCount)
            .ThenBy(c => GetPriorityOrder(c.type, config.AutoVenturePriority))
            .ThenBy(c => c.element) // Final tiebreaker: element order
            .ToList();

        var selected = sorted.First();
        log?.Information($"[VentureHelper] Selected {selected.element} {selected.type} for {retainer.Name} (effective: {selected.effectiveCount}, venture: {GetVentureName(selected.ventureId)})");
        
        return selected.ventureId;
    }

    /// <summary>
    /// Get priority order for sorting based on VenturePriority setting.
    /// Lower values = higher priority.
    /// </summary>
    private static int GetPriorityOrder(CrystalType type, VenturePriority priority)
    {
        return priority switch
        {
            VenturePriority.PreferCrystals => type == CrystalType.Crystal ? 0 : 1,
            VenturePriority.PreferShards => type == CrystalType.Shard ? 0 : 1,
            _ => 0 // Balanced - no preference
        };
    }

    /// <summary>
    /// Legacy method for backward compatibility. Uses retainer-only counting.
    /// Consider using the overload that takes StoredCharacter for global counts.
    /// </summary>
    [Obsolete("Use DetermineLowestCrystalVenture(StoredCharacter, Retainer, Configuration, IPluginLog?) for global capacity calculations")]
    public static VentureId? DetermineLowestCrystalVenture(Retainer retainer, Configuration config, IPluginLog? log = null)
    {
        // If we don't have the owner character, we can't do global calculations
        // Fall back to using the retainer's owner if available
        if (retainer?.OwnerCharacter != null)
        {
            return DetermineLowestCrystalVenture(retainer.OwnerCharacter, retainer, config, log);
        }

        log?.Warning($"[VentureHelper] No owner character for {retainer?.Name}, cannot perform global capacity calculation");
        return null;
    }

    /// <summary>
    /// Map an Element and CrystalType to the corresponding VentureId.
    /// Returns null for Clusters (not obtainable via ventures).
    /// </summary>
    public static VentureId? GetVentureId(Element element, CrystalType type)
    {
        if (type == CrystalType.Cluster)
            return null; // Clusters cannot be obtained via ventures

        return (element, type) switch
        {
            (Element.Fire, CrystalType.Shard) => VentureId.Fire_Shard,
            (Element.Fire, CrystalType.Crystal) => VentureId.Fire_Crystal,
            (Element.Ice, CrystalType.Shard) => VentureId.Ice_Shard,
            (Element.Ice, CrystalType.Crystal) => VentureId.Ice_Crystal,
            (Element.Wind, CrystalType.Shard) => VentureId.Wind_Shard,
            (Element.Wind, CrystalType.Crystal) => VentureId.Wind_Crystal,
            (Element.Earth, CrystalType.Shard) => VentureId.Earth_Shard,
            (Element.Earth, CrystalType.Crystal) => VentureId.Earth_Crystal,
            (Element.Lightning, CrystalType.Shard) => VentureId.Lightning_Shard,
            (Element.Lightning, CrystalType.Crystal) => VentureId.Lightning_Crystal,
            (Element.Water, CrystalType.Shard) => VentureId.Water_Shard,
            (Element.Water, CrystalType.Crystal) => VentureId.Water_Crystal,
            _ => null
        };
    }

    /// <summary>
    /// Get a human-readable name for a venture ID.
    /// </summary>
    public static string GetVentureName(VentureId ventureId)
    {
        return ventureId switch
        {
            VentureId.Fire_Shard => "Fire Shard",
            VentureId.Fire_Crystal => "Fire Crystal",
            VentureId.Ice_Shard => "Ice Shard",
            VentureId.Ice_Crystal => "Ice Crystal",
            VentureId.Wind_Shard => "Wind Shard",
            VentureId.Wind_Crystal => "Wind Crystal",
            VentureId.Lightning_Shard => "Lightning Shard",
            VentureId.Lightning_Crystal => "Lightning Crystal",
            VentureId.Earth_Shard => "Earth Shard",
            VentureId.Earth_Crystal => "Earth Crystal",
            VentureId.Water_Shard => "Water Shard",
            VentureId.Water_Crystal => "Water Crystal",
            _ => "Unknown"
        };
    }
}
