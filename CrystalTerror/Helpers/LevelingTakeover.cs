using Dalamud.Plugin.Ipc;

namespace CrystalTerror.Helpers;

/// <summary>
/// For retainers inside a leveling tier, makes AutoRetainer only collect the finished venture and
/// assigns the next one during AutoRetainer's retainer postprocess step, so the post-collection level is used.
/// </summary>
public sealed class LevelingTakeover : IDisposable
{
    private sealed record ArmedRetainer(StoredCharacter Character, ulong Cid, string RetainerName, uint PreviousVentureId, int? Job);

    private readonly Configuration config;
    private readonly ICallGateSubscriber<string, object>? onAdditionalTask;
    private readonly ICallGateSubscriber<string, string, object>? onReadyForPostprocess;
    private readonly ICallGateSubscriber<string, object>? requestPostprocess;
    private readonly ICallGateSubscriber<object>? finishPostprocess;
    private readonly bool subscribed;

    private ArmedRetainer? armed;
    // AutoRetainer waits forever until FinishPostprocessRequest is called while this is set.
    private bool postprocessActive;
    private int postprocessId;

    public LevelingTakeover(Configuration config)
    {
        this.config = config;

        try
        {
            this.requestPostprocess = Svc.PluginInterface.GetIpcSubscriber<string, object>("AutoRetainer.RequestPostprocess");
            this.finishPostprocess = Svc.PluginInterface.GetIpcSubscriber<object>("AutoRetainer.FinishPostprocessRequest");
            this.onAdditionalTask = Svc.PluginInterface.GetIpcSubscriber<string, object>("AutoRetainer.OnRetainerAdditionalTask");
            this.onReadyForPostprocess = Svc.PluginInterface.GetIpcSubscriber<string, string, object>("AutoRetainer.OnRetainerReadyForPostprocess");

            this.onAdditionalTask.Subscribe(this.OnRetainerAdditionalTask);
            this.onReadyForPostprocess.Subscribe(this.OnRetainerReadyForPostprocess);
            this.subscribed = true;
        }
        catch (Exception ex)
        {
            Svc.Log.Debug($"[LevelingTakeover] AutoRetainer postprocess IPC not available: {ex.Message}");
        }
    }

    private bool CanTakeOver =>
        this.subscribed &&
        this.requestPostprocess?.HasAction == true &&
        this.finishPostprocess?.HasAction == true;

    /// <summary>
    /// Makes AutoRetainer only collect this retainer's venture so the next one can be picked after the level-up.
    /// Returns false when the normal venture override should be used instead.
    /// </summary>
    public bool TryArm(StoredCharacter character, Retainer retainer)
    {
        // AutoRetainer only fires the send-to-venture hook while idle, so anything still armed is stale.
        this.ReleaseStale();

        try
        {
            if (!this.CanTakeOver || !this.config.AutoVentureLevelingEnabled)
                return false;

            if (RetainerLevelingHelper.FindTier(this.config.AutoVentureLevelingTiers, retainer.Level) == null)
                return false;

            if (VentureHelper.IsFisher(retainer) && !this.config.AutoVentureFSHEnabled)
                return false;

            var ventureId = RetainerLevelingHelper.GetListVentureId(retainer.Name) ?? retainer.CurrentVentureId ?? 0;
            if (ventureId == 0)
                return false;

            var cid = Player.CID;
            if (!ForceCollectOnly(cid, retainer.Name, ventureId))
            {
                Svc.Log.Warning($"[LevelingTakeover] Could not take over {retainer.Name}, using the normal venture override");
                return false;
            }

            this.armed = new ArmedRetainer(character, cid, retainer.Name, ventureId, retainer.Job);
            return true;
        }
        catch (Exception ex)
        {
            Svc.Log.Warning($"[LevelingTakeover] TryArm failed for {retainer.Name}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Restores any outstanding planner backups and finishes any in-flight postprocess.
    /// </summary>
    public void ReleaseStale()
    {
        if (this.armed == null && !this.postprocessActive)
            return;

        Svc.Log.Debug($"[LevelingTakeover] Releasing stale takeover for {this.armed?.RetainerName ?? "in-flight postprocess"}");
        this.ReleaseAll();
    }

    public void Dispose()
    {
        try { this.onAdditionalTask?.Unsubscribe(this.OnRetainerAdditionalTask); } catch { }
        try { this.onReadyForPostprocess?.Unsubscribe(this.OnRetainerReadyForPostprocess); } catch { }

        this.ReleaseAll();
    }

    private void ReleaseAll()
    {
        this.armed = null;

        try
        {
            RetainerVentureAssigner.Abort();
        }
        catch (Exception ex)
        {
            Svc.Log.Warning($"[LevelingTakeover] Aborting venture assignment failed: {ex.Message}");
        }

        try
        {
            AutoRetainerPlannerBridge.RestoreAll();
        }
        catch (Exception ex)
        {
            Svc.Log.Warning($"[LevelingTakeover] Restoring AutoRetainer planners failed: {ex.Message}");
        }

        this.FinishPostprocess(this.postprocessId);
    }

    private void OnRetainerAdditionalTask(string retainerName)
    {
        if (this.armed == null || !NameEquals(this.armed.RetainerName, retainerName))
            return;

        try
        {
            this.requestPostprocess!.InvokeAction(Svc.PluginInterface.InternalName);
            Svc.Log.Debug($"[LevelingTakeover] Requested postprocess for {retainerName}");
        }
        catch (Exception ex)
        {
            Svc.Log.Warning($"[LevelingTakeover] Could not request postprocess for {retainerName}: {ex.Message}");
            this.ReleaseAll();
        }
    }

    private void OnRetainerReadyForPostprocess(string pluginName, string retainerName)
    {
        if (pluginName != Svc.PluginInterface.InternalName)
            return;

        var id = ++this.postprocessId;
        this.postprocessActive = true;
        var arm = this.armed;
        this.armed = null;

        try
        {
            if (arm == null || !NameEquals(arm.RetainerName, retainerName))
            {
                Svc.Log.Debug($"[LevelingTakeover] Nothing armed for {retainerName}, finishing postprocess");
                this.FinishPostprocess(id);
                return;
            }

            RestorePlanner(arm);

            var retainer = FindRetainer(arm);
            var ventureId = this.PickVenture(arm, retainer);

            if (RetainerVentureAssigner.IsBusy)
            {
                Svc.Log.Warning("[LevelingTakeover] Venture assigner still busy, aborting the previous assignment");
                RetainerVentureAssigner.Abort();
            }

            Svc.Log.Information($"[LevelingTakeover] Assigning {VentureListHelper.GetVentureName(ventureId)} (ID: {ventureId}) to {arm.RetainerName}");
            RetainerVentureAssigner.Enqueue(ventureId, retainer?.Job ?? arm.Job, ok => this.OnAssignComplete(id, arm, ventureId, ok));
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[LevelingTakeover] Postprocess for {retainerName} failed: {ex.Message}");
            try { RetainerVentureAssigner.Abort(); } catch { }
            this.FinishPostprocess(id);
        }
    }

    private uint PickVenture(ArmedRetainer arm, Retainer? retainer)
    {
        var fallback = arm.PreviousVentureId != 0 ? arm.PreviousVentureId : (uint)VentureId.QuickExploration;
        if (retainer == null)
            return fallback;

        var liveLevel = RetainerLevelingHelper.GetLiveLevel(arm.RetainerName);
        if (liveLevel.HasValue && liveLevel.Value != retainer.Level)
            Svc.Log.Information($"[LevelingTakeover] {retainer.Name} is now Lv{liveLevel.Value} (was Lv{retainer.Level})");
        retainer.Level = liveLevel ?? retainer.Level;

        var picked = VentureHelper.DetermineVenture(arm.Character, retainer, this.config, Svc.Log);
        return picked.HasValue ? (uint)picked.Value : fallback;
    }

    private void OnAssignComplete(int id, ArmedRetainer arm, uint ventureId, bool ok)
    {
        try
        {
            if (!ok)
            {
                Svc.Log.Warning($"[LevelingTakeover] Could not assign a venture to {arm.RetainerName}");
                return;
            }

            var venture = VentureListHelper.GetVenture(ventureId);
            var retainer = FindRetainer(arm);
            if (retainer != null)
            {
                retainer.CurrentVentureId = ventureId;
                retainer.VentureEndsAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (venture?.MaxTimeMinutes ?? 60) * 60;
            }

            Svc.Log.Information($"[LevelingTakeover] ✓ Assigned {venture?.Name ?? ventureId.ToString()} to {arm.RetainerName}");
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[LevelingTakeover] Updating {arm.RetainerName} after assignment failed: {ex.Message}");
        }
        finally
        {
            this.FinishPostprocess(id);
        }
    }

    // Guarded by id so a late callback from an older request can't release a newer one.
    private void FinishPostprocess(int id)
    {
        if (!this.postprocessActive || id != this.postprocessId)
            return;

        this.postprocessActive = false;
        try
        {
            this.finishPostprocess?.InvokeAction();
            Svc.Log.Debug("[LevelingTakeover] Finished postprocess");
        }
        catch (Exception ex)
        {
            Svc.Log.Warning($"[LevelingTakeover] FinishPostprocessRequest failed: {ex.Message}");
        }
    }

    private static bool ForceCollectOnly(ulong cid, string retainerName, uint ventureId)
    {
        try
        {
            return AutoRetainerPlannerBridge.ForceCollectOnly(cid, retainerName, ventureId);
        }
        catch (Exception ex)
        {
            Svc.Log.Warning($"[LevelingTakeover] Forcing collect-only for {retainerName} failed: {ex.Message}");
            try { AutoRetainerPlannerBridge.Restore(cid, retainerName); } catch { }
            return false;
        }
    }

    private static void RestorePlanner(ArmedRetainer arm)
    {
        try
        {
            AutoRetainerPlannerBridge.Restore(arm.Cid, arm.RetainerName);
        }
        catch (Exception ex)
        {
            Svc.Log.Warning($"[LevelingTakeover] Restoring the AutoRetainer planner for {arm.RetainerName} failed: {ex.Message}");
        }
    }

    private static Retainer? FindRetainer(ArmedRetainer arm)
        => arm.Character.Retainers.FirstOrDefault(r => NameEquals(r.Name, arm.RetainerName));

    private static bool NameEquals(string a, string b)
        => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
