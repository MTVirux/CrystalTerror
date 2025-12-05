using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Data;
using Lumina.Excel.Sheets;
using FFXIVClientStructs.FFXIV.Client.Game;
using System.Text;
using System.IO;
using Newtonsoft.Json;

namespace CrystalTerror
{
    /// <summary>
    /// Helper utilities for character-related import functions.
    /// Provides import from the currently logged-in character (via Dalamud's IClientState)
    /// and mass import from AutoRetainer IPC.
    /// </summary>
    public static class CharacterHelper
    {
        /// <summary>
        /// Create a <see cref="StoredCharacter"/> representing the currently logged-in player.
        /// Returns null if the client is not logged in or LocalPlayer is unavailable.
        /// </summary>
        public static StoredCharacter? ImportCurrentCharacter(Dalamud.Plugin.Services.IPlayerState playerState, Dalamud.Plugin.Services.IObjectTable objects, IDataManager? dataManager = null)
        {
            if (playerState == null) throw new ArgumentNullException(nameof(playerState));
            if (objects == null) throw new ArgumentNullException(nameof(objects));

            // Use ContentId to determine login state and IObjectTable to access the local player.
            if (playerState.ContentId == 0) return null;

            var local = objects.LocalPlayer;
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

            // If a data manager is supplied, try to resolve the human-readable world name
            if (dataManager != null)
            {
                try
                {
                        var sheet = dataManager.GetExcelSheet<Lumina.Excel.Sheets.World>();
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

                            var ret = RetainerHelper.Create(sc, string.IsNullOrEmpty(rname) ? string.Empty : rname, rptr->RetainerId, (int)rptr->ClassJob, rptr->Level, 0);
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
        public static List<StoredCharacter> ImportFromAutoRetainer(IDalamudPluginInterface pluginInterface)
        {
            if (pluginInterface == null) throw new ArgumentNullException(nameof(pluginInterface));

            var outChars = new List<StoredCharacter>();

            var retInfos = AutoRetainerHelper.GetAllRetainersViaAutoRetainer(pluginInterface);
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
                    var r = RetainerHelper.CreateFromAutoRetainer(sc, ri.Name, ri.Atid, ri.Job, ri.Level, ri.Ilvl);
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

        // Persistence is now handled via Dalamud plugin config (Configuration.Characters).
        // Legacy file-based helpers were removed; callers should update the plugin config
        // and call `PluginInterface.SavePluginConfig(Configuration)` after modifying characters.
    }
}
