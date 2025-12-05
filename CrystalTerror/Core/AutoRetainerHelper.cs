using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin;

namespace CrystalTerror
{
    /// <summary>
    /// Helper utilities for integrating with AutoRetainer IPC.
    /// </summary>
    public static class AutoRetainerHelper
    {
        public record RetainerInfo(string Name, ulong Atid, int? Job, int Level, int Ilvl, string OwnerName, string OwnerWorld);

        /// <summary>
        /// Query AutoRetainer IPC to collect retainers for all registered characters.
        /// Returns an empty list if AutoRetainer IPC is unavailable.
        /// </summary>
        public static List<RetainerInfo> GetAllRetainersViaAutoRetainer(IDalamudPluginInterface pluginInterface)
        {
            if (pluginInterface == null) throw new ArgumentNullException(nameof(pluginInterface));

            var outList = new List<RetainerInfo>();

            var getRegistered = pluginInterface.GetIpcSubscriber<List<ulong>>("AutoRetainer.GetRegisteredCIDs");
            var cids = getRegistered?.InvokeFunc() ?? new List<ulong>();

            var getOffline = pluginInterface.GetIpcSubscriber<ulong, object>("AutoRetainer.GetOfflineCharacterData");
            if (getOffline == null)
                return outList; // AutoRetainer not installed or no IPC available.

            foreach (var cid in cids)
            {
                var raw = getOffline.InvokeFunc(cid);
                if (raw == null) continue;

                dynamic dyn = raw;
                string ownerName = dyn.Name ?? string.Empty;
                string ownerWorld = dyn.World ?? string.Empty;

                try
                {
                    if (dyn.RetainerData is IEnumerable retDataEnumerable)
                    {
                        foreach (var r in retDataEnumerable)
                        {
                            try
                            {
                                dynamic rr = r;
                                var name = (string?)(rr.Name ?? string.Empty) ?? string.Empty;
                                ulong atid = 0UL;
                                try { atid = (ulong)(rr.atid ?? 0UL); } catch { }
                                int? job = null;
                                try { job = rr.Job == null ? null : (int?)rr.Job; } catch { }
                                int level = 0; try { level = (int?)(rr.Level ?? 0) ?? 0; } catch { }
                                int ilvl = 0; try { ilvl = (int?)(rr.Ilvl ?? 0) ?? 0; } catch { }

                                outList.Add(new RetainerInfo(name, atid, job, level, ilvl, ownerName, ownerWorld));
                            }
                            catch { /* tolerate malformed entries */ }
                        }
                    }
                }
                catch { /* tolerate malformed shapes */ }
            }

            return outList;
        }
    }
}
