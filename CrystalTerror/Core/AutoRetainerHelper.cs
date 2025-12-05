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
        public record RetainerInfo(string Name, ulong Atid, int? Job, int Level, int Ilvl, int Gathering, int Perception, string OwnerName, string OwnerWorld);

        private static bool isProcessingVenture = false;

        /// <summary>
        /// Query AutoRetainer IPC to collect retainers for all registered characters.
        /// Returns an empty list if AutoRetainer IPC is unavailable.
        /// </summary>
        public static List<RetainerInfo> GetAllRetainersViaAutoRetainer()
        {
            var outList = new List<RetainerInfo>();

            var getRegistered = Services.PluginInterface.GetIpcSubscriber<List<ulong>>("AutoRetainer.GetRegisteredCIDs");
            var cids = getRegistered?.InvokeFunc() ?? new List<ulong>();

            var getOffline = Services.PluginInterface.GetIpcSubscriber<ulong, object>("AutoRetainer.GetOfflineCharacterData");
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
                                
                                // Try to get gathering and perception from AdditionalRetainerData
                                int gathering = 0;
                                int perception = 0;
                                try
                                {
                                    var getAdditional = Services.PluginInterface.GetIpcSubscriber<ulong, string, object>("AutoRetainer.GetAdditionalRetainerData");
                                    if (getAdditional != null)
                                    {
                                        var additionalData = getAdditional.InvokeFunc(cid, name);
                                        if (additionalData != null)
                                        {
                                            dynamic adata = additionalData;
                                            try { gathering = (int?)(adata.Gathering ?? 0) ?? 0; } catch { }
                                            try { perception = (int?)(adata.Perception ?? 0) ?? 0; } catch { }
                                        }
                                    }
                                }
                                catch { /* AdditionalRetainerData not available */ }

                                outList.Add(new RetainerInfo(name, atid, job, level, ilvl, gathering, perception, ownerName, ownerWorld));
                            }
                            catch { /* tolerate malformed entries */ }
                        }
                    }
                }
                catch { /* tolerate malformed shapes */ }
            }

            return outList;
        }

        /// <summary>
        /// Handler for RetainerList addon opening. Imports current character and retainer data.
        /// </summary>
        public static void HandleRetainerListSetup(
            Dalamud.Plugin.Services.IPlayerState playerState,
            Dalamud.Plugin.Services.IObjectTable objects,
            Dalamud.Plugin.Services.IDataManager dataManager,
            List<StoredCharacter> characters,
            Configuration config,
            Dalamud.Plugin.Services.IPluginLog log)
        {
            try
            {
                try
                {
                    log.Information($"Retainer addon opened (ContentId={playerState.ContentId}). Triggering import.");
                }
                catch { }

                var sc = CharacterHelper.ImportCurrentCharacter(playerState, objects, dataManager);
                if (sc != null)
                {
                    CharacterHelper.MergeInto(characters, new[] { sc }, CharacterHelper.MergePolicy.Overwrite);
                    try
                    {
                        config.Characters = characters;
                        Services.PluginInterface.SavePluginConfig(config);
                    }
                    catch { }
                }
            }
            catch
            {
                // swallow handler errors
            }
        }

        /// <summary>
        /// Handler for AutoRetainer.OnSendRetainerToVenture IPC hook.
        /// Called just before AutoRetainer sends a retainer to a venture.
        /// Calculates the appropriate venture based on crystal/shard inventory.
        /// </summary>
        public static void HandleRetainerSendToVenture(
            string retainerName,
            Configuration config,
            Dalamud.Plugin.Ipc.ICallGateSubscriber<uint, object>? autoRetainerSetVenture,
            Dalamud.Plugin.Services.IPlayerState playerState,
            Dalamud.Plugin.Services.IObjectTable objects,
            List<StoredCharacter> characters,
            Dalamud.Plugin.Services.IPluginLog log)
        {
            try
            {
                // Check if we're already processing a venture to prevent multiple triggers
                if (isProcessingVenture)
                {
                    log.Information($"[CrystalTerror] Venture processing already in progress, ignoring duplicate trigger for {retainerName}");
                    return;
                }

                // Only process if auto-venture is enabled
                if (!config.AutoVentureEnabled || autoRetainerSetVenture == null)
                    return;

                isProcessingVenture = true;
                log.Information($"[CrystalTerror] AutoRetainer venture hook triggered for retainer: {retainerName}");

                // Find the current character
                var contentId = playerState.ContentId;
                var playerName = objects.LocalPlayer?.Name.TextValue;
                var currentChar = characters.FirstOrDefault(c => c.Name == playerName && contentId != 0);

                if (currentChar == null)
                {
                    return;
                }

                // Find the retainer
                var retainer = currentChar.Retainers.FirstOrDefault(r => r.Name == retainerName);
                if (retainer == null)
                {
                    return;
                }

                // Log retainer stats for Informationging
                log.Information($"[CrystalTerror] {retainer.Name}: Level={retainer.Level}, Gathering={retainer.Gathering}, Job={ClassJobExtensions.GetAbreviation(retainer.Job)}");

                // Check if retainer is eligible
                if (retainer.Job == null || !VentureHelper.IsRetainerEligibleForVenture(retainer, CrystalType.Shard))
                {
                    var jobName = retainer.Job.HasValue ? ClassJobExtensions.GetAbreviation(retainer.Job) : "Unknown";
                    log.Information($"[CrystalTerror] ✗ Skipping {retainer.Name} - Job: {jobName} (not MIN/BTN/FSH)");
                    return;
                }

                // Determine the best venture
                var ventureId = VentureHelper.DetermineLowestCrystalVenture(retainer, config, log);
                if (ventureId.HasValue)
                {
                    log.Information($"[CrystalTerror] ✓ Overriding venture for {retainer.Name} with {VentureHelper.GetVentureName(ventureId.Value)} (ID: {(uint)ventureId.Value})");
                    autoRetainerSetVenture.InvokeAction((uint)ventureId.Value);
                }
                else
                {
                    // No suitable venture found
                    {
                        log.Information($"[CrystalTerror] ✗ No suitable venture for {retainer.Name} - Level: {retainer.Level}, Gathering: {retainer.Gathering}");
                        log.Information($"  (Crystals require Level > 26 AND Gathering > 90)");
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error($"[CrystalTerror] Error in venture override handler: {ex.Message}");
                log.Error($"  Stack trace: {ex.StackTrace}");
            }
            finally
            {
                // Always release the lock when we're done
                isProcessingVenture = false;
            }
        }
    }
}
