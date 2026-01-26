using System.Collections;
using Dalamud.Plugin;

namespace CrystalTerror.Helpers;

/// <summary>
/// Helper utilities for integrating with AutoRetainer IPC.
/// Uses ECommons for reliable player data access.
/// </summary>
public static class AutoRetainerHelper
{
    /// <summary>
    /// Information about a retainer from AutoRetainer IPC.
    /// Includes OwnerCID for reliable character identification and venture tracking data.
    /// </summary>
    public record RetainerInfo(
        string Name, 
        ulong Atid, 
        int? Job, 
        int Level, 
        int Ilvl, 
        int Gathering, 
        int Perception, 
        string OwnerName, 
        string OwnerWorld,
        ulong OwnerCID,
        uint VentureID,
        long VentureEndsAt
    );

    private static bool isProcessingVenture = false;

    /// <summary>
    /// Get the ordered list of character ContentIds from AutoRetainer.
    /// This is the order shown in AutoRetainer's UI.
    /// </summary>
    public static List<ulong> GetCharacterOrder()
    {
        try
        {
            var getRegistered = Svc.PluginInterface.GetIpcSubscriber<List<ulong>>("AutoRetainer.GetRegisteredCIDs");
            return getRegistered?.InvokeFunc() ?? new List<ulong>();
        }
        catch
        {
            return new List<ulong>();
        }
    }

    /// <summary>
    /// Query AutoRetainer IPC to collect retainers for all registered characters.
    /// Returns an empty list if AutoRetainer IPC is unavailable.
    /// </summary>
    public static List<RetainerInfo> GetAllRetainersViaAutoRetainer()
    {
        var outList = new List<RetainerInfo>();

        try
        {
            Svc.Log.Debug("[CrystalTerror] Starting AutoRetainer import via IPC");
        }
        catch { }

        var getRegistered = Svc.PluginInterface.GetIpcSubscriber<List<ulong>>("AutoRetainer.GetRegisteredCIDs");
        var cids = getRegistered?.InvokeFunc() ?? new List<ulong>();

        try
        {
            Svc.Log.Debug($"[CrystalTerror] AutoRetainer reported {cids.Count} registered content IDs");
        }
        catch { }

        var getOffline = Svc.PluginInterface.GetIpcSubscriber<ulong, object>("AutoRetainer.GetOfflineCharacterData");
        if (getOffline == null)
            return outList;

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
                            
                            int gathering = 0;
                            int perception = 0;
                            uint ventureID = 0;
                            long ventureEndsAt = 0;
                            
                            // Extract venture data from retainer
                            try { ventureID = (uint)(rr.VentureID ?? 0u); } catch { }
                            try { ventureEndsAt = (long)(rr.VentureEndsAt ?? 0L); } catch { }
                            
                            try
                            {
                                var getAdditional = Svc.PluginInterface.GetIpcSubscriber<ulong, string, object>("AutoRetainer.GetAdditionalRetainerData");
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
                            catch { }

                            outList.Add(new RetainerInfo(name, atid, job, level, ilvl, gathering, perception, ownerName, ownerWorld, cid, ventureID, ventureEndsAt));
                            try
                            {
                                Svc.Log.Debug($"[CrystalTerror] AutoRetainer found retainer: {name} (Atid={atid}) Owner={ownerName}@{ownerWorld} (CID={cid:X16}) Level={level} Ilvl={ilvl} VentureID={ventureID}");
                            }
                            catch { }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        return outList;
    }

    /// <summary>
    /// Handler for RetainerList addon opening. Imports current character and retainer data.
    /// </summary>
    public static void HandleRetainerListSetup(
        List<StoredCharacter> characters,
        Configuration config)
    {
        try
        {
            Svc.Log.Debug("[CrystalTerror] Retainer list opened. Triggering import.");

            var sc = CharacterHelper.ImportCurrentCharacter();
            if (sc != null)
            {
                CharacterHelper.MergeInto(characters, new[] { sc }, CharacterHelper.MergePolicy.Merge);
                ConfigHelper.SaveAndSync(config, characters);
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Debug($"[CrystalTerror] HandleRetainerListSetup error: {ex.Message}");
        }
    }

    /// <summary>
    /// Handler for AutoRetainer.OnSendRetainerToVenture IPC hook.
    /// Called just before AutoRetainer sends a retainer to a venture.
    /// Calculates the appropriate venture based on global crystal/shard capacity.
    /// </summary>
    public static void HandleRetainerSendToVenture(
        string retainerName,
        Configuration config,
        Dalamud.Plugin.Ipc.ICallGateSubscriber<uint, object>? autoRetainerSetVenture,
        List<StoredCharacter> characters,
        Dalamud.Plugin.Services.IPluginLog log)
    {
        try
        {
            if (isProcessingVenture)
            {
                log.Debug($"[CrystalTerror] Venture processing already in progress, ignoring duplicate trigger for {retainerName}");
                return;
            }

            if (!config.AutoVentureEnabled || autoRetainerSetVenture == null)
                return;

            isProcessingVenture = true;
            log.Debug($"[CrystalTerror] AutoRetainer venture hook triggered for retainer: {retainerName}");

            // Find the current character using ECommons Player helper
            if (!Player.Available || Player.CID == 0)
            {
                log.Debug("[CrystalTerror] Player not available, cannot apply venture decisions.");
                return;
            }

            // Find character by ContentId first (most reliable), then by name
            var currentChar = characters.FirstOrDefault(c => c.MatchesCID(Player.CID)) ??
                              characters.FirstOrDefault(c => string.Equals(c.Name, Player.Name, StringComparison.OrdinalIgnoreCase));

            if (currentChar == null)
            {
                log.Debug($"[CrystalTerror] No matching character found for {Player.Name} (CID={Player.CID:X16})");
                return;
            }

            // Find the retainer
            var retainer = currentChar.Retainers.FirstOrDefault(r => 
                string.Equals(r.Name, retainerName, StringComparison.OrdinalIgnoreCase));
                
            if (retainer == null)
            {
                log.Debug($"[CrystalTerror] Retainer '{retainerName}' not found for character '{currentChar.Name}'");
                return;
            }

            log.Debug($"[CrystalTerror] {retainer.Name}: Level={retainer.Level}, Gathering={retainer.Gathering}, Job={ClassJobExtensions.GetAbbreviation(retainer.Job)}");

            // Check per-character auto-venture toggle
            if (!currentChar.AutoVentureEnabled)
            {
                log.Debug($"[CrystalTerror] ✗ Skipping {retainer.Name} - Auto venture disabled for character {currentChar.Name}");
                return;
            }

            if (!retainer.EnableAutoVenture)
            {
                log.Debug($"[CrystalTerror] ✗ Skipping {retainer.Name} - Auto venture disabled for this retainer");
                return;
            }

            // Use the new global capacity-based venture determination
            var ventureId = VentureHelper.DetermineLowestCrystalVenture(currentChar, retainer, config, log);
            
            if (ventureId.HasValue)
            {
                log.Information($"[CrystalTerror] ✓ Overriding venture for {retainer.Name} with {VentureHelper.GetVentureName(ventureId.Value)} (ID: {(uint)ventureId.Value})");
                autoRetainerSetVenture.InvokeAction((uint)ventureId.Value);

                // Update local retainer state for immediate re-calculation accuracy
                retainer.CurrentVentureId = (uint)ventureId.Value;
                retainer.VentureEndsAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600; // 1-hour venture
            }
            else
            {
                log.Debug($"[CrystalTerror] ✗ No suitable venture for {retainer.Name}");
                if (!VentureHelper.IsGatheringRetainer(retainer))
                {
                    log.Debug($"  (Not a gathering retainer: {ClassJobExtensions.GetAbbreviation(retainer.Job)})");
                }
                else if (VentureHelper.IsFisher(retainer) && !config.AutoVentureFSHEnabled)
                {
                    log.Debug($"  (FSH retainer and FSH is disabled)");
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

    /// <summary>
    /// Update retainer venture tracking data from AutoRetainer IPC.
    /// Call this when importing retainer data to populate CurrentVentureId and VentureEndsAt.
    /// </summary>
    public static void UpdateRetainerVentureData(Retainer retainer, uint ventureId, long ventureEndsAt)
    {
        if (retainer == null)
            return;

        retainer.CurrentVentureId = ventureId > 0 ? ventureId : null;
        retainer.VentureEndsAt = ventureEndsAt > 0 ? ventureEndsAt : null;
    }
}
