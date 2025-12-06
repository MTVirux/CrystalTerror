using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Data;
using Lumina.Excel.Sheets;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using System.Text;
using System.IO;
using Newtonsoft.Json;

namespace CrystalTerror
{
    /// <summary>
    /// Helper utilities for character-related import functions.
    /// Provides import from the currently logged-in character and mass import from AutoRetainer IPC.
    /// </summary>
    public static class CharacterHelper
    {
        /// <summary>
        /// Create a <see cref="StoredCharacter"/> representing the currently logged-in player.
        /// Returns null if the client is not logged in or LocalPlayer is unavailable.
        /// </summary>
        public static StoredCharacter? ImportCurrentCharacter()
        {
            // Use ContentId to determine login state and IObjectTable to access the local player.
            if (Services.PlayerService.State.ContentId == 0) return null;

            var local = Services.PlayerService.Objects.LocalPlayer;
            if (local == null) return null;

            // Default to the numeric home world id
            var worldStr = string.Empty;
            try
            {
                worldStr = local.HomeWorld.RowId.ToString();
            }
            catch
            {
                worldStr = string.Empty;
            }

            // Try to resolve the human-readable world name from game data
            try
            {
                var sheet = Services.DataService.Manager.GetExcelSheet<Lumina.Excel.Sheets.World>();
                if (sheet != null)
                {
                    var row = sheet.GetRowOrDefault((uint)local.HomeWorld.RowId);
                    if (row.HasValue)
                    {
                        worldStr = row.Value.Name.ExtractText();
                    }
                }
            }
            catch
            {
                // ignore and keep numeric id
            }

            var sc = new StoredCharacter
            {
                Name = local.Name.TextValue ?? string.Empty,
                World = worldStr,
                ServiceAccount = 1,
                LastUpdateUtc = DateTime.UtcNow,
                Retainers = new List<Retainer>(),
                Inventory = new Inventory()
            };

            // Try to populate character crystal inventory from InventoryManager
            try
            {
                unsafe
                {
                    var invMgr = FFXIVClientStructs.FFXIV.Client.Game.InventoryManager.Instance();
                    if (invMgr != null)
                    {
                        var crystalContainer = invMgr->GetInventoryContainer(FFXIVClientStructs.FFXIV.Client.Game.InventoryType.Crystals);
                        if (crystalContainer != null)
                        {
                            // Crystal item IDs in FFXIV
                            // The game stores crystals by type first, then element:
                            // Shards: 2-7 (Fire, Ice, Wind, Lightning, Earth, Water)
                            // Crystals: 8-13 (Fire, Ice, Wind, Lightning, Earth, Water)
                            // Clusters: 14-19 (Fire, Ice, Wind, Lightning, Earth, Water)
                            var elements = new Element[] { Element.Fire, Element.Ice, Element.Wind, Element.Lightning, Element.Earth, Element.Water };
                            var crystalTypes = new CrystalType[] { CrystalType.Shard, CrystalType.Crystal, CrystalType.Cluster };
                            
                            // Item IDs: Shards start at 2, Crystals at 8, Clusters at 14
                            var baseItemIds = new uint[] { 2, 8, 14 };

                            for (int ti = 0; ti < crystalTypes.Length; ++ti)
                            {
                                for (int ei = 0; ei < elements.Length; ++ei)
                                {
                                    uint itemId = baseItemIds[ti] + (uint)ei;
                                    int count = invMgr->GetItemCountInContainer(itemId, FFXIVClientStructs.FFXIV.Client.Game.InventoryType.Crystals);
                                    try { sc.Inventory.SetCount(elements[ei], crystalTypes[ti], count); } catch { }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // ignore if InventoryManager not available or access fails
            }

            // Try to populate retainers from in-memory client structs (when available).
            try
            {
                unsafe
                {
                    var mgr = RetainerManager.Instance();
                    if (mgr != null)
                    {
                        for (int i = 0; i < 10; ++i)
                        {
                            var rptr = mgr->GetRetainerBySortedIndex((uint)i);
                            if (rptr == null) continue;

                            // If no valid retainer id, skip
                            if (rptr->RetainerId == 0) continue;

                            // read fixed-size name at offset 0x08, up to 32 bytes
                            string rname = string.Empty;
                            try
                            {
                                byte* namePtr = (byte*)rptr + 0x08;
                                int len = 0;
                                while (len < 32 && namePtr[len] != 0) ++len;
                                if (len > 0)
                                    rname = Encoding.UTF8.GetString(namePtr, len);
                            }
                            catch
                            {
                                rname = string.Empty;
                            }

                            // Try to calculate retainer stats from equipped gear if accessible
                            int ilvl = 0;
                            int gathering = 0;
                            int perception = 0;
                            bool statsObtained = false;
                            
                            try
                            {
                                var (calculatedIlvl, calculatedGathering, calculatedPerception) = RetainerStatsHelper.CalculateRetainerStats();
                                if (calculatedIlvl.HasValue)
                                {
                                    // Gear is accessible, use calculated values
                                    ilvl = calculatedIlvl.Value;
                                    gathering = calculatedGathering;
                                    perception = calculatedPerception;
                                    statsObtained = true;
                                }
                            }
                            catch
                            {
                                // Calculation failed, gear not accessible
                            }
                            
                            // If gear not accessible, try to get stats from AutoRetainer
                            if (!statsObtained)
                            {
                                try
                                {
                                    var getAdditional = Services.PluginInterfaceService.Interface.GetIpcSubscriber<ulong, string, object>("AutoRetainer.GetAdditionalRetainerData");
                                    if (getAdditional != null)
                                    {
                                        var additionalData = getAdditional.InvokeFunc(Services.PlayerService.State.ContentId, rname);
                                        if (additionalData != null)
                                        {
                                            dynamic adata = additionalData;
                                            try { ilvl = (int?)(adata.Ilvl ?? 0) ?? 0; } catch { }
                                            try { gathering = (int?)(adata.Gathering ?? 0) ?? 0; } catch { }
                                            try { perception = (int?)(adata.Perception ?? 0) ?? 0; } catch { }
                                            statsObtained = true;
                                        }
                                    }
                                }
                                catch
                                {
                                    // AutoRetainer not available or data not found
                                }
                            }

                            var ret = RetainerHelper.Create(sc, string.IsNullOrEmpty(rname) ? string.Empty : rname, rptr->RetainerId, (int)rptr->ClassJob, rptr->Level, ilvl, gathering, perception);

                            // Try to populate crystal/shard/cluster counts from the client's ItemFinder cache
                            try
                            {
                                var ifm = ItemFinderModule.Instance();
                                if (ifm != null)
                                {
                                    // The map stores pointers to ItemFinderRetainerInventory; try to get by retainer id
                                    if (ifm->RetainerInventories.TryGetValuePointer(rptr->RetainerId, out var invPtr) && invPtr != null)
                                    {
                                        // invPtr is a pointer to a Pointer<ItemFinderRetainerInventory> (i.e. Pointer<ItemFinderRetainerInventory>*).
                                        // Dereference to obtain the underlying ItemFinderRetainerInventory* via the Pointer<T> implicit conversion.
                                        ItemFinderRetainerInventory* inv = (ItemFinderRetainerInventory*)(*invPtr);
                                        if (inv != null)
                                        {
                                            // Crystal quantities: 18 entries stored by type first, then element
                                            // Order: All shards (Fire-Water), All crystals (Fire-Water), All clusters (Fire-Water)
                                            // Indices 0-5: Shards, 6-11: Crystals, 12-17: Clusters
                                            var crystals = inv->CrystalQuantities;
                                            var elements = new Element[] { Element.Fire, Element.Ice, Element.Wind, Element.Lightning, Element.Earth, Element.Water };
                                            var crystalTypes = new CrystalType[] { CrystalType.Shard, CrystalType.Crystal, CrystalType.Cluster };
                                            
                                            for (int ti = 0; ti < crystalTypes.Length; ++ti)
                                            {
                                                for (int ei = 0; ei < elements.Length; ++ei)
                                                {
                                                    int idx = ti * 6 + ei;
                                                    ushort val = crystals[idx];
                                                    try { ret.Inventory.SetCount(elements[ei], crystalTypes[ti], val); } catch { }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            catch
                            {
                                // ignore any errors reading ItemFinderModule
                            }

                            sc.Retainers.Add(ret);
                        }
                    }
                }
            }
            catch
            {
                // ignore if FFXIVClientStructs not available or access fails
            }
            return sc;
        }

        /// <summary>
        /// Mass-import stored characters using AutoRetainer IPC. Groups retainers by owner name+world
        /// and constructs a <see cref="StoredCharacter"/> for each owner. Returns an empty list
        /// if AutoRetainer IPC is unavailable or no retainers found.
        /// </summary>
        public static List<StoredCharacter> ImportFromAutoRetainer()
        {
            var outChars = new List<StoredCharacter>();

            var retInfos = AutoRetainerHelper.GetAllRetainersViaAutoRetainer();
            if (retInfos == null || retInfos.Count == 0) return outChars;

            var grouped = retInfos.GroupBy(r => new { OwnerName = r.OwnerName ?? string.Empty, OwnerWorld = r.OwnerWorld ?? string.Empty });

            foreach (var g in grouped)
            {
                var sc = new StoredCharacter
                {
                    Name = g.Key.OwnerName,
                    World = g.Key.OwnerWorld,
                    ServiceAccount = 1,
                    LastUpdateUtc = DateTime.UtcNow,
                    Retainers = new List<Retainer>(),
                    Inventory = new Inventory()
                };

                foreach (var ri in g)
                {
                    var r = RetainerHelper.CreateFromAutoRetainer(sc, ri.Name, ri.Atid, ri.Job, ri.Level, ri.Ilvl, ri.Gathering, ri.Perception);
                    sc.Retainers.Add(r);
                }

                outChars.Add(sc);
            }

            return outChars;
        }

        public enum MergePolicy
        {
            Skip,
            Overwrite,
            Merge
        }

        /// <summary>
        /// Merge imported characters into an existing target list using the given policy.
        /// - Skip: do not change existing entries.
        /// - Overwrite: replace existing entries with imported ones.
        /// - Merge: merge retainers/inventory when possible.
        /// </summary>
        public static void MergeInto(List<StoredCharacter> target, IEnumerable<StoredCharacter> imported, MergePolicy policy = MergePolicy.Skip)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (imported == null) return;

            foreach (var sc in imported)
            {
                var existing = target.FirstOrDefault(x => string.Equals(x.Name, sc.Name, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(x.World, sc.World, StringComparison.OrdinalIgnoreCase));

                if (existing == null)
                {
                    // Ensure owner pointers for retainers are set and add
                    RetainerHelper.SetOwnerForRetainers(sc);
                    target.Add(sc);
                    continue;
                }

                switch (policy)
                {
                    case MergePolicy.Skip:
                        break;
                    case MergePolicy.Overwrite:
                        // Preserve existing retainer stats when gear wasn't accessible during import (new values are 0)
                        foreach (var newRetainer in sc.Retainers)
                        {
                            var existingRetainer = existing.Retainers.FirstOrDefault(er => 
                                (er.atid != 0 && er.atid == newRetainer.atid) || 
                                string.Equals(er.Name, newRetainer.Name, StringComparison.OrdinalIgnoreCase));
                            
                            if (existingRetainer != null)
                            {
                                // If new stats are 0 but existing stats are non-zero, preserve them
                                // This happens when gear isn't accessible (retainer not summoned)
                                if (newRetainer.Ilvl == 0 && existingRetainer.Ilvl > 0)
                                    newRetainer.Ilvl = existingRetainer.Ilvl;
                                if (newRetainer.Gathering == 0 && existingRetainer.Gathering > 0)
                                    newRetainer.Gathering = existingRetainer.Gathering;
                                if (newRetainer.Perception == 0 && existingRetainer.Perception > 0)
                                    newRetainer.Perception = existingRetainer.Perception;
                            }
                        }
                        
                        existing.Retainers = sc.Retainers;
                        RetainerHelper.SetOwnerForRetainers(existing);
                        existing.Inventory = sc.Inventory;
                        existing.LastUpdateUtc = sc.LastUpdateUtc;
                        existing.ServiceAccount = sc.ServiceAccount;
                        break;
                    case MergePolicy.Merge:
                        foreach (var r in sc.Retainers)
                        {
                            var match = existing.Retainers.FirstOrDefault(er => (er.atid != 0 && er.atid == r.atid) || string.Equals(er.Name, r.Name, StringComparison.OrdinalIgnoreCase));
                            if (match == null)
                            {
                                // ensure owner is set to existing character
                                r.OwnerCharacter = existing;
                                existing.Retainers.Add(r);
                            }
                            else
                            {
                                // Update match with new data
                                match.Name = r.Name;
                                match.atid = r.atid;
                                if (r.Job.HasValue) match.Job = r.Job;
                                match.Level = r.Level;
                                match.Ilvl = r.Ilvl;
                                match.Gathering = r.Gathering;
                                match.Perception = r.Perception;
                                if (r.Inventory != null) match.Inventory = r.Inventory;
                            }
                        }

                        if (existing.Inventory == null && sc.Inventory != null)
                            existing.Inventory = sc.Inventory;

                        if (sc.LastUpdateUtc > existing.LastUpdateUtc)
                            existing.LastUpdateUtc = sc.LastUpdateUtc;

                        if (sc.ServiceAccount != 0)
                            existing.ServiceAccount = sc.ServiceAccount;

                        break;
                }
            }
        }
    }
}
