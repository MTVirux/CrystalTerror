using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;

namespace CrystalTerror
{
    /// <summary>
    /// Helper utilities for determining optimal ventures based on crystal/shard inventory.
    /// </summary>
    public static class VentureHelper
    {
        // Job IDs for gathering classes
        private const int JobMiner = 16;
        private const int JobBotanist = 17;
        private const int JobFisher = 18;

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
        /// Determine the venture ID for the crystal/shard type with the lowest count in the retainer's inventory.
        /// Returns null if no suitable venture is found based on configuration, or if all enabled types are above the threshold.
        /// </summary>
        /// <param name="retainer">The retainer whose inventory to analyze.</param>
        /// <param name="config">Configuration to determine which crystal types are enabled.</param>
        /// <param name="log">Optional logger for detailed output.</param>
        /// <returns>VentureId for the lowest crystal/shard, or null if none applicable or all above threshold.</returns>
        public static VentureId? DetermineLowestCrystalVenture(Retainer retainer, Configuration config, IPluginLog? log = null)
        {
            if (retainer == null) throw new ArgumentNullException(nameof(retainer));
            if (config == null) throw new ArgumentNullException(nameof(config));

            var inv = retainer.Inventory;
            if (inv == null)
            {
                log?.Debug($"[VentureHelper] Retainer {retainer.Name} has no inventory data.");
                return null;
            }

            log?.Debug($"[VentureHelper] Analyzing inventory for {retainer.Name} (Job: {ClassJobExtensions.GetAbreviation(retainer.Job)}, Level: {retainer.Level}, Gathering: {retainer.Gathering})");

            // Build list of candidates based on config
            var candidates = new List<(Element element, CrystalType type, long count, VentureId ventureId)>();

            var elements = new[] { Element.Fire, Element.Ice, Element.Wind, Element.Earth, Element.Lightning, Element.Water };

            foreach (var element in elements)
            {
                // Add shards if enabled and retainer is eligible
                if (config.AutoVentureShardsEnabled && IsRetainerEligibleForVenture(retainer, CrystalType.Shard))
                {
                    var shardCount = inv.GetCount(element, CrystalType.Shard);
                    var shardVenture = GetVentureId(element, CrystalType.Shard);
                    if (shardVenture.HasValue)
                        candidates.Add((element, CrystalType.Shard, shardCount, shardVenture.Value));
                }

                // Add crystals if enabled and retainer is eligible (requires level > 26 and Gathering > 90)
                if (config.AutoVentureCrystalsEnabled && IsRetainerEligibleForVenture(retainer, CrystalType.Crystal))
                {
                    var crystalCount = inv.GetCount(element, CrystalType.Crystal);
                    var crystalVenture = GetVentureId(element, CrystalType.Crystal);
                    if (crystalVenture.HasValue)
                        candidates.Add((element, CrystalType.Crystal, crystalCount, crystalVenture.Value));
                }
            }

            if (candidates.Count == 0)
            {
                log?.Debug($"[VentureHelper] No eligible venture candidates for {retainer.Name} (Shards: {config.AutoVentureShardsEnabled}, Crystals: {config.AutoVentureCrystalsEnabled})");
                return null;
            }

            log?.Debug($"[VentureHelper] Found {candidates.Count} candidate(s) for {retainer.Name}");
            foreach (var candidate in candidates.OrderBy(c => c.count))
            {
                log?.Debug($"  - {candidate.element} {candidate.type}: {candidate.count} (Venture ID: {(uint)candidate.ventureId})");
            }

            // Check threshold: if all enabled types are above threshold, skip this retainer
            if (config.AutoVentureThreshold > 0)
            {
                var allAboveThreshold = candidates.All(c => c.count >= config.AutoVentureThreshold);
                if (allAboveThreshold)
                {
                    // All crystal/shard types are above threshold, return null to skip venture assignment
                    log?.Debug($"[VentureHelper] All crystal/shard types for {retainer.Name} are above threshold ({config.AutoVentureThreshold}). Lowest count: {candidates.Min(c => c.count)}");
                    log?.Debug($"  Counts: {string.Join(", ", candidates.OrderBy(c => c.element).ThenBy(c => c.type).Select(c => $"{c.element} {c.type}={c.count}"))}");
                    return null;
                }
            }

            // Find the candidate with the lowest count
            var lowest = candidates.OrderBy(c => c.count).ThenBy(c => c.type).First();
            log?.Information($"[VentureHelper] Selected {lowest.element} {lowest.type} for {retainer.Name} (count: {lowest.count}, venture: {GetVentureName(lowest.ventureId)})");
            return lowest.ventureId;
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
                (Element.Lightning, CrystalType.Shard) => VentureId.Lightning_Shard,
                (Element.Lightning, CrystalType.Crystal) => VentureId.Lightning_Crystal,
                (Element.Earth, CrystalType.Shard) => VentureId.Earth_Shard,
                (Element.Earth, CrystalType.Crystal) => VentureId.Earth_Crystal,
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
}
