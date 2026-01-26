using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using System.Text;
using System.IO;
using Newtonsoft.Json;

namespace CrystalTerror.Helpers;

/// <summary>
/// Helper utilities for character-related import functions.
/// Provides import from the currently logged-in character and mass import from AutoRetainer IPC.
/// Uses ECommons Player helper for reliable player data access.
/// </summary>
public static class CharacterHelper
{
    /// <summary>
    /// Create a <see cref="StoredCharacter"/> representing the currently logged-in player.
    /// Returns null if the client is not logged in or LocalPlayer is unavailable.
    /// Uses ECommons.GameHelpers.Player for reliable data access.
    /// </summary>
    public static StoredCharacter? ImportCurrentCharacter()
    {
        // Use ECommons Player.Available and Player.CID for reliable login state detection
        if (!Player.Available || Player.CID == 0) 
            return null;

        var playerObject = Player.Object;
        if (playerObject == null) 
            return null;

        // Get player name using ECommons - more reliable than direct access
        var playerName = Player.Name;
        if (string.IsNullOrWhiteSpace(playerName))
        {
            // Fallback to object name
            playerName = playerObject.Name.TextValue ?? string.Empty;
        }

        // Get world name using ECommons - already handles the sheet lookup
        var worldName = Player.HomeWorldName;
        var homeWorldId = Player.HomeWorld.RowId;

        // Create the character with ContentId as primary identifier
        var sc = new StoredCharacter
        {
            ContentId = Player.CID,
            Name = playerName.Trim(),
            World = worldName.Trim(),
            HomeWorldId = homeWorldId,
            ServiceAccount = 1,
            LastUpdateUtc = DateTime.UtcNow,
            Retainers = new List<Retainer>(),
            Inventory = new Inventory()
        };

        try
        {
            Svc.Log.Debug($"[CrystalTerror] Importing character: {sc.Name}@{sc.World} (CID={sc.ContentId:X16})");
        }
        catch { }

        // Populate character crystal inventory from InventoryManager
        PopulateCharacterCrystals(sc);

        // Populate retainers from in-memory client structs
        PopulateRetainers(sc);

        return sc;
    }

    /// <summary>
    /// Imports the current character, merges into the character list, and saves the config.
    /// This is a convenience method that combines the common pattern of import + merge + save.
    /// </summary>
    /// <param name="characters">The character list to merge into.</param>
    /// <param name="config">The configuration to save.</param>
    /// <param name="policy">The merge policy to use (defaults to Overwrite).</param>
    /// <returns>True if a character was successfully imported and saved.</returns>
    public static bool ImportCurrentCharacterAndSave(List<StoredCharacter> characters, Configuration config, MergePolicy policy = MergePolicy.Overwrite)
    {
        var character = ImportCurrentCharacter();
        if (character == null)
            return false;

        MergeInto(characters, new[] { character }, policy);
        ConfigHelper.SaveAndSync(config, characters);
        return true;
    }

    /// <summary>
    /// Imports characters from AutoRetainer, merges into the character list, and saves the config.
    /// This is a convenience method that combines the common pattern of import + merge + save.
    /// </summary>
    /// <param name="characters">The character list to merge into.</param>
    /// <param name="config">The configuration to save.</param>
    /// <param name="policy">The merge policy to use (defaults to Merge).</param>
    /// <returns>The number of characters imported.</returns>
    public static int ImportFromAutoRetainerAndSave(List<StoredCharacter> characters, Configuration config, MergePolicy policy = MergePolicy.Merge)
    {
        var imported = ImportFromAutoRetainer();
        if (imported.Count == 0)
            return 0;

        MergeInto(characters, imported, policy);
        ConfigHelper.SaveAndSync(config, characters);
        return imported.Count;
    }

    /// <summary>
    /// Populates the character's crystal inventory from the game's InventoryManager.
    /// </summary>
    private static void PopulateCharacterCrystals(StoredCharacter sc)
    {
        try
        {
            unsafe
            {
                var invMgr = InventoryManager.Instance();
                if (invMgr == null) return;

                var crystalContainer = invMgr->GetInventoryContainer(InventoryType.Crystals);
                if (crystalContainer == null) return;

                // Crystal item IDs in FFXIV - organized by type first, then element:
                // Shards: 2-7 (Fire, Ice, Wind, Earth, Lightning, Water)
                // Crystals: 8-13 (Fire, Ice, Wind, Earth, Lightning, Water)
                // Clusters: 14-19 (Fire, Ice, Wind, Earth, Lightning, Water)
                var elements = new Element[] { Element.Fire, Element.Ice, Element.Wind, Element.Earth, Element.Lightning, Element.Water };
                var crystalTypes = new CrystalType[] { CrystalType.Shard, CrystalType.Crystal, CrystalType.Cluster };
                var baseItemIds = new uint[] { 2, 8, 14 };

                for (int typeIndex = 0; typeIndex < crystalTypes.Length; ++typeIndex)
                {
                    for (int elementIndex = 0; elementIndex < elements.Length; ++elementIndex)
                    {
                        uint itemId = baseItemIds[typeIndex] + (uint)elementIndex;
                        int count = invMgr->GetItemCountInContainer(itemId, InventoryType.Crystals);
                        sc.Inventory.SetCount(elements[elementIndex], crystalTypes[typeIndex], count);
                    }
                }

                try
                {
                    var dict = sc.Inventory.ToDictionary();
                    var parts = dict.Select(kv => $"{kv.Key}={kv.Value}");
                    Svc.Log.Debug("[CrystalTerror] Character crystals: " + string.Join(", ", parts));
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Debug($"[CrystalTerror] Failed to read character crystals: {ex.Message}");
        }
    }

    /// <summary>
    /// Populates retainer data from client structs and ItemFinderModule.
    /// </summary>
    private static void PopulateRetainers(StoredCharacter sc)
    {
        try
        {
            unsafe
            {
                var mgr = RetainerManager.Instance();
                if (mgr == null) return;

                for (int i = 0; i < 10; ++i)
                {
                    var rptr = mgr->GetRetainerBySortedIndex((uint)i);
                    if (rptr == null) continue;
                    if (rptr->RetainerId == 0) continue;

                    // Read retainer name from the struct
                    string rname = ReadRetainerName(rptr);

                    // Get retainer stats (iLvl, Gathering, Perception)
                    var (ilvl, gathering, perception) = GetRetainerStats(rname);

                    // Get venture data from client struct
                    uint ventureId = rptr->VentureId;
                    long ventureEndsAt = rptr->VentureComplete;

                    var ret = RetainerHelper.Create(
                        sc, 
                        rname, 
                        rptr->RetainerId, 
                        (int)rptr->ClassJob, 
                        rptr->Level, 
                        ilvl, 
                        gathering, 
                        perception,
                        ventureId,
                        ventureEndsAt
                    );

                    // Populate retainer crystal inventory from ItemFinderModule cache
                    PopulateRetainerCrystals(ret, rptr->RetainerId);

                    sc.Retainers.Add(ret);
                }
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Debug($"[CrystalTerror] Failed to read retainers: {ex.Message}");
        }
    }

    /// <summary>
    /// Reads the retainer name from the RetainerManager struct.
    /// </summary>
    private static unsafe string ReadRetainerName(FFXIVClientStructs.FFXIV.Client.Game.RetainerManager.Retainer* rptr)
    {
        try
        {
            byte* namePtr = (byte*)rptr + 0x08;
            int len = 0;
            while (len < 32 && namePtr[len] != 0) ++len;
            return len > 0 ? Encoding.UTF8.GetString(namePtr, len) : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Gets retainer stats from direct calculation or AutoRetainer IPC.
    /// </summary>
    private static (int ilvl, int gathering, int perception) GetRetainerStats(string retainerName)
    {
        int ilvl = 0, gathering = 0, perception = 0;

        // Try to calculate from equipped gear first
        try
        {
            var (calculatedIlvl, calculatedGathering, calculatedPerception) = RetainerStatsHelper.CalculateRetainerStats();
            if (calculatedIlvl.HasValue)
            {
                return (calculatedIlvl.Value, calculatedGathering, calculatedPerception);
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Debug($"[CrystalTerror] Failed to calculate retainer stats from gear: {ex.Message}");
        }

        // Fall back to AutoRetainer IPC
        try
        {
            var getAdditional = Svc.PluginInterface.GetIpcSubscriber<ulong, string, object>("AutoRetainer.GetAdditionalRetainerData");
            if (getAdditional != null)
            {
                var additionalData = getAdditional.InvokeFunc(Player.CID, retainerName);
                if (additionalData != null)
                {
                    dynamic adata = additionalData;
                    try { ilvl = (int?)(adata.Ilvl ?? 0) ?? 0; } catch { }
                    try { gathering = (int?)(adata.Gathering ?? 0) ?? 0; } catch { }
                    try { perception = (int?)(adata.Perception ?? 0) ?? 0; } catch { }
                }
            }
        }
        catch { }

        return (ilvl, gathering, perception);
    }

    /// <summary>
    /// Populates retainer crystal inventory from ItemFinderModule cache.
    /// </summary>
    private static unsafe void PopulateRetainerCrystals(Retainer ret, ulong retainerId)
    {
        try
        {
            var ifm = ItemFinderModule.Instance();
            if (ifm == null) return;

            if (!ifm->RetainerInventories.TryGetValuePointer(retainerId, out var invPtr) || invPtr == null)
                return;

            ItemFinderRetainerInventory* inv = (ItemFinderRetainerInventory*)(*invPtr);
            if (inv == null) return;

            // Crystal quantities: 18 entries stored by type first, then element
            // Indices 0-5: Shards, 6-11: Crystals, 12-17: Clusters
            var crystals = inv->CrystalQuantities;
            var elements = new Element[] { Element.Fire, Element.Ice, Element.Wind, Element.Earth, Element.Lightning, Element.Water };
            var crystalTypes = new CrystalType[] { CrystalType.Shard, CrystalType.Crystal, CrystalType.Cluster };

            for (int typeIndex = 0; typeIndex < crystalTypes.Length; ++typeIndex)
            {
                for (int elementIndex = 0; elementIndex < elements.Length; ++elementIndex)
                {
                    int idx = typeIndex * 6 + elementIndex;
                    ushort val = crystals[idx];
                    ret.Inventory.SetCount(elements[elementIndex], crystalTypes[typeIndex], val);
                }
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Debug($"[CrystalTerror] Failed to read retainer crystals: {ex.Message}");
        }
    }

    /// <summary>
    /// Mass-import stored characters using AutoRetainer IPC. Groups retainers by owner name+world
    /// and constructs a <see cref="StoredCharacter"/> for each owner.
    /// </summary>
    public static List<StoredCharacter> ImportFromAutoRetainer()
    {
        var outChars = new List<StoredCharacter>();

        var retInfos = AutoRetainerHelper.GetAllRetainersViaAutoRetainer();
        if (retInfos == null || retInfos.Count == 0) return outChars;

        var grouped = retInfos.GroupBy(r => new { 
            OwnerName = r.OwnerName ?? string.Empty, 
            OwnerWorld = r.OwnerWorld ?? string.Empty,
            OwnerCID = r.OwnerCID
        });

        foreach (var g in grouped)
        {
            var sc = new StoredCharacter
            {
                ContentId = g.Key.OwnerCID,
                Name = g.Key.OwnerName,
                World = g.Key.OwnerWorld,
                HomeWorldId = 0, // Will be populated when character logs in
                ServiceAccount = 1,
                LastUpdateUtc = DateTime.UtcNow,
                Retainers = new List<Retainer>(),
                Inventory = new Inventory()
            };

            foreach (var ri in g)
            {
                var r = RetainerHelper.Create(sc, ri.Name ?? string.Empty, ri.Atid, ri.Job, ri.Level, ri.Ilvl, ri.Gathering, ri.Perception, ri.VentureID, ri.VentureEndsAt);
                sc.Retainers.Add(r);
            }

            outChars.Add(sc);
        }

        return outChars;
    }

    /// <summary>
    /// Defines how imported character data should be merged with existing data.
    /// </summary>
    public enum MergePolicy
    {
        /// <summary>Preserve existing data unchanged; only add new characters.</summary>
        Skip,

        /// <summary>Replace all existing data (inventory, retainers) with imported data.</summary>
        Overwrite,

        /// <summary>Merge retainer lists, updating existing and adding new retainers.</summary>
        Merge
    }

    /// <summary>
    /// Merge imported characters into an existing target list using the given policy.
    /// Now uses ContentId as the primary identifier when available.
    /// </summary>
    public static void MergeInto(List<StoredCharacter> target, IEnumerable<StoredCharacter> imported, MergePolicy policy = MergePolicy.Merge)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        if (imported == null) return;

        var originalCount = target.Count;
        Svc.Log.Debug($"[CrystalTerror] MergeInto: Starting with {originalCount} characters, policy={policy}");

        foreach (var sc in imported)
        {
            // Find existing character by ContentId first, then by Name+World
            var existing = FindExistingCharacter(target, sc);

            if (existing == null)
            {
                RetainerHelper.SetOwnerForRetainers(sc);
                target.Add(sc);
                Svc.Log.Debug($"[CrystalTerror] Added new character: {sc.Name}@{sc.World} (CID={sc.ContentId:X16})");
                continue;
            }

            Svc.Log.Debug($"[CrystalTerror] Merging into existing character: {existing.Name}@{existing.World} (CID={existing.ContentId:X16})");

            // Update existing character's ContentId if it was missing
            if (existing.ContentId == 0 && sc.ContentId != 0)
            {
                existing.ContentId = sc.ContentId;
                Svc.Log.Debug($"[CrystalTerror] Updated CID for {existing.Name}@{existing.World} to {sc.ContentId:X16}");
            }

            // Update HomeWorldId if missing
            if (existing.HomeWorldId == 0 && sc.HomeWorldId != 0)
            {
                existing.HomeWorldId = sc.HomeWorldId;
            }

            switch (policy)
            {
                case MergePolicy.Skip:
                    Svc.Log.Debug($"[CrystalTerror] Skipping merge for {existing.Name}@{existing.World} (policy=Skip)");
                    break;

                case MergePolicy.Overwrite:
                    MergeOverwrite(existing, sc);
                    break;

                case MergePolicy.Merge:
                    MergeMerge(existing, sc);
                    break;
            }
        }

        Svc.Log.Debug($"[CrystalTerror] MergeInto: Finished with {target.Count} characters (was {originalCount})");
    }

    /// <summary>
    /// Finds an existing character in the list by ContentId or Name+World.
    /// </summary>
    private static StoredCharacter? FindExistingCharacter(List<StoredCharacter> target, StoredCharacter sc)
    {
        // Try ContentId first (most reliable)
        if (sc.ContentId != 0)
        {
            var byId = target.FirstOrDefault(x => x.ContentId == sc.ContentId);
            if (byId != null) return byId;
        }

        // Fall back to Name+World
        return target.FirstOrDefault(x => 
            string.Equals(x.Name?.Trim(), sc.Name?.Trim(), StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.World?.Trim(), sc.World?.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Overwrites existing character data with imported data.
    /// Preserves existing inventory data when imported data has empty inventories.
    /// </summary>
    private static void MergeOverwrite(StoredCharacter existing, StoredCharacter sc)
    {
        // Preserve retainer stats and inventories when new values are 0/empty (gear not accessible or AutoRetainer import)
        foreach (var newRetainer in sc.Retainers)
        {
            var existingRetainer = existing.Retainers.FirstOrDefault(er =>
                (er.Atid != 0 && er.Atid == newRetainer.Atid) ||
                string.Equals(er.Name, newRetainer.Name, StringComparison.OrdinalIgnoreCase));

            if (existingRetainer != null)
            {
                PreserveNonZeroStats(existingRetainer, newRetainer);
                
                // Preserve existing inventory if new one is empty
                if (newRetainer.Inventory == null || newRetainer.Inventory.IsEmpty())
                {
                    newRetainer.Inventory = existingRetainer.Inventory ?? new Inventory();
                }
            }
        }

        // Update name/world in case they changed (character transfer, etc.)
        if (!string.IsNullOrEmpty(sc.Name)) existing.Name = sc.Name;
        if (!string.IsNullOrEmpty(sc.World)) existing.World = sc.World;
        if (sc.HomeWorldId != 0) existing.HomeWorldId = sc.HomeWorldId;

        existing.Retainers = sc.Retainers;
        RetainerHelper.SetOwnerForRetainers(existing);
        
        // Only update character inventory if incoming has actual data
        if (sc.Inventory != null && !sc.Inventory.IsEmpty())
        {
            existing.Inventory = sc.Inventory;
        }
        else if (existing.Inventory == null)
        {
            existing.Inventory = new Inventory();
        }
        
        existing.LastUpdateUtc = sc.LastUpdateUtc;
        if (sc.ServiceAccount != 0) existing.ServiceAccount = sc.ServiceAccount;
    }

    /// <summary>
    /// Merges imported character data with existing data.
    /// Preserves existing inventory data when imported data has empty inventories.
    /// </summary>
    private static void MergeMerge(StoredCharacter existing, StoredCharacter sc)
    {
        foreach (var r in sc.Retainers)
        {
            var match = existing.Retainers.FirstOrDefault(er =>
                (er.Atid != 0 && er.Atid == r.Atid) ||
                string.Equals(er.Name, r.Name, StringComparison.OrdinalIgnoreCase));

            if (match == null)
            {
                r.OwnerCharacter = existing;
                existing.Retainers.Add(r);
            }
            else
            {
                // Update match with new data, preserving non-zero stats
                match.Name = r.Name;
                match.Atid = r.Atid;
                if (r.Job.HasValue) match.Job = r.Job;
                match.Level = r.Level;
                PreserveNonZeroStats(match, r);
                
                // Only update inventory if the incoming one has actual data
                // This prevents AutoRetainer imports from wiping existing crystal counts
                if (r.Inventory != null && !r.Inventory.IsEmpty())
                {
                    match.Inventory = r.Inventory;
                }
            }
        }

        // Update name/world if changed
        if (!string.IsNullOrEmpty(sc.Name)) existing.Name = sc.Name;
        if (!string.IsNullOrEmpty(sc.World)) existing.World = sc.World;
        if (sc.HomeWorldId != 0) existing.HomeWorldId = sc.HomeWorldId;

        // Only update character inventory if incoming has actual data
        if (sc.Inventory != null && !sc.Inventory.IsEmpty())
        {
            existing.Inventory = sc.Inventory;
        }
        else if (existing.Inventory == null)
        {
            existing.Inventory = new Inventory();
        }

        if (sc.LastUpdateUtc > existing.LastUpdateUtc)
            existing.LastUpdateUtc = sc.LastUpdateUtc;

        if (sc.ServiceAccount != 0)
            existing.ServiceAccount = sc.ServiceAccount;
    }

    /// <summary>
    /// Preserves non-zero stats from existing retainer when new stats are zero.
    /// </summary>
    private static void PreserveNonZeroStats(Retainer existing, Retainer incoming)
    {
        if (incoming.Ilvl == 0 && existing.Ilvl > 0)
            incoming.Ilvl = existing.Ilvl;
        if (incoming.Gathering == 0 && existing.Gathering > 0)
            incoming.Gathering = existing.Gathering;
        if (incoming.Perception == 0 && existing.Perception > 0)
            incoming.Perception = existing.Perception;
    }
}
