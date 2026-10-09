using ECommons.Automation;
using ECommons.Automation.NeoTaskManager;
using ECommons.ExcelServices.Sheets;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using static ECommons.GenericHelpers;
using AddonSheet = Lumina.Excel.Sheets.Addon;
using VentureCategory = CrystalTerror.Helpers.VentureListHelper.VentureCategory;

namespace CrystalTerror.Helpers;

/// <summary>
/// Assigns a venture by clicking through the retainer's menus. Must be started while the retainer's menu is open.
/// </summary>
public static unsafe class RetainerVentureAssigner
{
    private const string LogPrefix = "[RetainerVentureAssigner]";
    private const string RetainerBellSheet = "custom/000/CmnDefRetainerCall_00010";

    private const int StepTimeoutMs = 10_000;
    private const int CleanupTimeoutMs = 3_000;
    private const int CloseIntervalMs = 300;
    private const int RetryIntervalMs = 1_000;
    private const int MenuSettleMs = 200;
    // The venture lists are filled by a server response that can land after the window is ready.
    private const int ListSettleMs = 600;
    private const int MinSettleFrames = 5;

    // RetainerTaskSupply AtkValues: [40] number of level brackets, [42 + i] pointer to the id of listed venture i, [107] number of listed ventures.
    private const int SupplyBracketCountIndex = 40;
    private const int SupplyFirstVentureIndex = 42;
    private const int SupplyVentureCountIndex = 107;

    // Addon sheet rows: "Assign venture" (both states) and "Quit".
    private static readonly uint[] AssignVentureRows = [2386, 2387];
    private static readonly uint[] QuitRows = [2383];

    // Bell dialogue rows, combat/MIN/BTN/FSH where there are four.
    private static readonly uint[] QuickExplorationRows = [402];
    private static readonly uint[] FieldExplorationRows = [196, 198, 200, 202];
    private static readonly uint[] HourVentureRows = [195, 197, 199, 201];
    private static readonly uint[] AllCategoryRows = [.. QuickExplorationRows, .. FieldExplorationRows, .. HourVentureRows];

    private static readonly string[] VentureWindows = ["RetainerTaskAsk", "RetainerTaskSupply", "RetainerTaskList"];

    private static TaskManager? taskManager;
    private static Action<bool>? pendingCallback;

    // Per-step state, reset whenever a step starts.
    private static long settledSince;
    private static int settledFrames;
    private static long lastActionAt;

    public static bool IsBusy => pendingCallback != null;

    /// <summary>
    /// Calls onComplete exactly once: true after Assign went through and the retainer menu is back,
    /// false on failure, timeout or <see cref="Abort"/>. Job ids 16/17/18 are MIN/BTN/FSH, anything else is combat.
    /// </summary>
    public static void Enqueue(uint ventureId, int? job, Action<bool> onComplete)
    {
        if (IsBusy)
        {
            Svc.Log.Warning($"{LogPrefix} Already assigning a venture, rejecting {ventureId}");
            Notify(onComplete, false);
            return;
        }

        pendingCallback = onComplete;
        try
        {
            var error = EnqueueSteps(ventureId, job);
            if (error != null)
            {
                Svc.Log.Warning($"{LogPrefix} {error}");
                Finish(false);
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, $"{LogPrefix} Could not start assigning venture {ventureId}");
            taskManager?.Tasks.Clear();
            Finish(false);
        }
    }

    // Returns why the venture can't be assigned, or null once the steps are queued.
    private static string? EnqueueSteps(uint ventureId, int? job)
    {
        var venture = VentureListHelper.GetVenture(ventureId);
        if (venture == null)
            return $"Unknown venture {ventureId}";
        if (job != null && !VentureListHelper.IsVentureAssignableToJob(venture, job.Value))
            return $"{venture.Name} ({ventureId}) can't be assigned to job {job}";

        var assignTexts = GetTexts(AssignVentureRows, AddonText);
        var categoryTexts = GetTexts(GetCategoryRows(venture.Category), BellText);
        var anyCategoryTexts = GetTexts(AllCategoryRows, BellText);
        var quitTexts = GetTexts(QuitRows, AddonText);
        if (assignTexts.Length == 0 || categoryTexts.Length == 0)
            return "Could not read the retainer menu texts from the game data";

        Svc.Log.Debug($"{LogPrefix} Assigning {venture.Name} ({ventureId})");
        taskManager ??= new TaskManager();

        var categoryWindow = GetWindowAfterCategory(venture.Category);
        EnqueueStep("SelectAssignVenture", () => SelectMenuEntry(assignTexts, () => FindMenuEntry(anyCategoryTexts) != null));
        EnqueueStep("SelectCategory", () => SelectMenuEntry(categoryTexts, () => IsOpen(categoryWindow)));
        if (venture.Category == VentureCategory.FieldExploration)
            EnqueueStep("PickFromTaskList", () => PickFromTaskList(ventureId));
        else if (venture.Category != VentureCategory.QuickExploration)
            EnqueueStep("PickFromTaskSupply", () => PickFromTaskSupply(ventureId, venture.Level));
        EnqueueStep("ClickAssign", ClickAssign);
        EnqueueStep("WaitForRetainerMenu", () => WaitForRetainerMenu(quitTexts));
        taskManager.Enqueue(() => Finish(true), "Finish");
        return null;
    }

    public static void Abort()
    {
        taskManager?.Abort();
        if (pendingCallback == null)
            return;

        Svc.Log.Debug($"{LogPrefix} Aborted");
        if (Svc.Framework.IsInFrameworkUpdateThread)
            CloseVentureWindows();
        Finish(false);
    }

    private static void EnqueueStep(string name, Func<bool> step)
    {
        long deadline = 0;
        taskManager!.Enqueue(() =>
        {
            if (deadline == 0)
            {
                deadline = Environment.TickCount64 + StepTimeoutMs;
                settledSince = 0;
                settledFrames = 0;
                lastActionAt = 0;
                Svc.Log.Debug($"{LogPrefix} {name}");
            }

            try
            {
                if (step())
                    return true;
            }
            catch (Exception ex)
            {
                Svc.Log.Error(ex, $"{LogPrefix} {name} threw");
                return Fail($"{name} threw");
            }

            if (Environment.TickCount64 < deadline)
                return false;
            return Fail($"{name} timed out");
        }, name);
    }

    // Replaces the remaining steps with closing whatever venture windows are open, then reports failure.
    private static bool Fail(string reason)
    {
        Svc.Log.Warning($"{LogPrefix} {reason}, giving up");
        taskManager!.Tasks.Clear();

        long giveUpAt = 0;
        long nextCloseAt = 0;
        taskManager.Enqueue(() =>
        {
            var now = Environment.TickCount64;
            if (giveUpAt == 0)
                giveUpAt = now + CleanupTimeoutMs;
            if (now < nextCloseAt)
                return false;

            nextCloseAt = now + CloseIntervalMs;
            return !CloseVentureWindows() || now > giveUpAt;
        }, "CloseVentureWindows");
        taskManager.Enqueue(() => Finish(false), "Finish");
        return true;
    }

    private static bool Finish(bool success)
    {
        var callback = pendingCallback;
        pendingCallback = null;
        if (callback != null)
        {
            Svc.Log.Debug($"{LogPrefix} Finished, success: {success}");
            Notify(callback, success);
        }
        return true;
    }

    private static void Notify(Action<bool> callback, bool success)
    {
        try
        {
            callback(success);
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, $"{LogPrefix} Completion callback threw");
        }
    }

    // Done once the menu has moved on. Clicks get dropped now and then, so the entry is clicked again while it is still showing.
    private static bool SelectMenuEntry(string[] texts, Func<bool> movedOn)
    {
        if (movedOn())
            return true;

        var entry = FindMenuEntry(texts);
        if (!Settled(entry != null, MenuSettleMs) || !RetryDue() || entry is not { } picked)
            return false;

        Svc.Log.Debug($"{LogPrefix} {(lastActionAt == 0 ? "Selecting" : "Selecting again")} \"{picked.Text}\"");
        lastActionAt = Environment.TickCount64;
        picked.Select();
        return false;
    }

    private static bool PickFromTaskSupply(uint ventureId, int ventureLevel)
    {
        if (IsOpen("RetainerTaskAsk"))
            return true;

        var ready = TryGetAddonByName<AtkUnitBase>("RetainerTaskSupply", out var addon)
            && IsAddonReady(addon)
            && addon->AtkValuesCount > SupplyVentureCountIndex
            && addon->AtkValues[SupplyBracketCountIndex].Int > 0;
        if (!Settled(ready, ListSettleMs) || !RetryDue())
            return false;

        lastActionAt = Environment.TickCount64;
        var count = Math.Min((int)addon->AtkValues[SupplyVentureCountIndex].UInt, SupplyVentureCountIndex - SupplyFirstVentureIndex);
        for (var i = 0; i < count; i++)
        {
            var value = addon->AtkValues[SupplyFirstVentureIndex + i];
            if ((value.Type & AtkValueType.TypeMask) != AtkValueType.Pointer)
                continue;

            var id = (uint*)value.Pointer;
            if (id != null && *id == ventureId)
            {
                Svc.Log.Debug($"{LogPrefix} Picking list entry {i}");
                Callback.Fire(addon, true, 5, i, Callback.ZeroAtkValue);
                return false;
            }
        }

        // Not listed yet: open its level bracket. Brackets are 5 levels wide and listed highest first.
        var bracketCount = addon->AtkValues[SupplyBracketCountIndex].Int;
        var bracket = bracketCount - (ventureLevel - 1) / 5 - 1;
        if (bracket < 0)
        {
            Svc.Log.Debug($"{LogPrefix} No level bracket for a level {ventureLevel} venture ({bracketCount} brackets listed)");
            return false;
        }

        Svc.Log.Debug($"{LogPrefix} Opening level bracket {bracket}");
        Callback.Fire(addon, true, 4, bracket, Callback.ZeroAtkValue);
        return false;
    }

    private static bool PickFromTaskList(uint ventureId)
    {
        if (IsOpen("RetainerTaskAsk"))
            return true;

        var ready = TryGetAddonByName<AtkUnitBase>("RetainerTaskList", out var addon)
            && IsAddonReady(addon)
            && addon->AtkValuesCount > 0;
        if (!Settled(ready, ListSettleMs) || !RetryDue())
            return false;

        lastActionAt = Environment.TickCount64;
        Svc.Log.Debug($"{LogPrefix} Picking venture {ventureId} from the exploration list");
        Callback.Fire(addon, false, 11, (int)ventureId);
        return false;
    }

    private static bool ClickAssign()
    {
        if (!TryGetAddonByName<AddonRetainerTaskAsk>("RetainerTaskAsk", out var addon) || !addon->AtkUnitBase.IsVisible)
            return lastActionAt != 0;

        var ready = IsAddonReady(&addon->AtkUnitBase) && addon->AssignButton != null && addon->AssignButton->IsEnabled;
        if (!Settled(ready, MenuSettleMs) || !RetryDue())
            return false;

        lastActionAt = Environment.TickCount64;
        Svc.Log.Debug($"{LogPrefix} Clicking Assign");
        new AddonMaster.RetainerTaskAsk(addon).Assign();
        return false;
    }

    private static bool WaitForRetainerMenu(string[] quitTexts)
    {
        if (VentureWindows.Any(IsOpen))
            return false;
        if (!TryGetAddonByName<AddonSelectString>("SelectString", out var addon) || !IsAddonReady(&addon->AtkUnitBase))
            return false;
        if (quitTexts.Length > 0 && FindEntry(addon, quitTexts) == null)
            return false;

        var manager = RetainerManager.Instance();
        var active = manager == null ? null : manager->GetActiveRetainer();
        var activeVenture = active == null ? "unknown" : active->VentureId.ToString();
        Svc.Log.Debug($"{LogPrefix} Retainer menu is back, active retainer venture: {activeVenture}");
        return true;
    }

    // Returns true if there was something to close. Never closes the retainer's own menu, only the venture category menu.
    private static bool CloseVentureWindows()
    {
        try
        {
            var closed = false;
            foreach (var name in VentureWindows)
            {
                if (TryGetAddonByName<AtkUnitBase>(name, out var addon) && addon->IsVisible)
                {
                    Svc.Log.Debug($"{LogPrefix} Closing {name}");
                    addon->Close(true);
                    closed = true;
                }
            }
            if (closed)
                return true;

            var categoryTexts = GetTexts(AllCategoryRows, BellText);
            if (TryGetAddonByName<AddonSelectString>("SelectString", out var menu)
                && IsAddonReady(&menu->AtkUnitBase)
                && FindEntry(menu, categoryTexts) != null)
            {
                Svc.Log.Debug($"{LogPrefix} Closing the venture category menu");
                menu->AtkUnitBase.Close(true);
                return true;
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, $"{LogPrefix} Closing the venture windows threw");
        }
        return false;
    }

    // True once the condition has held for a few frames and minMs, so clicks don't land on a window that is still setting up.
    private static bool Settled(bool ready, int minMs)
    {
        if (!ready)
        {
            settledSince = 0;
            settledFrames = 0;
            return false;
        }

        if (settledSince == 0)
            settledSince = Environment.TickCount64;
        settledFrames++;
        return settledFrames >= MinSettleFrames && Environment.TickCount64 - settledSince >= minMs;
    }

    private static bool RetryDue()
        => lastActionAt == 0 || Environment.TickCount64 - lastActionAt >= RetryIntervalMs;

    private static bool IsOpen(string addonName)
        => TryGetAddonByName<AtkUnitBase>(addonName, out var addon) && addon->IsVisible;

    private static AddonMaster.SelectString.Entry? FindMenuEntry(string[] texts)
    {
        if (TryGetAddonByName<AddonSelectString>("SelectString", out var addon) && IsAddonReady(&addon->AtkUnitBase))
            return FindEntry(addon, texts);
        return null;
    }

    private static AddonMaster.SelectString.Entry? FindEntry(AddonSelectString* addon, string[] texts)
    {
        foreach (var entry in new AddonMaster.SelectString(addon).Entries)
        {
            var text = entry.Text;
            if (texts.Any(t => text.StartsWith(t, StringComparison.Ordinal)))
                return entry;
        }
        return null;
    }

    private static uint[] GetCategoryRows(VentureCategory category) => category switch
    {
        VentureCategory.QuickExploration => QuickExplorationRows,
        VentureCategory.FieldExploration => FieldExplorationRows,
        _ => HourVentureRows,
    };

    private static string GetWindowAfterCategory(VentureCategory category) => category switch
    {
        VentureCategory.QuickExploration => "RetainerTaskAsk",
        VentureCategory.FieldExploration => "RetainerTaskList",
        _ => "RetainerTaskSupply",
    };

    private static string[] GetTexts(uint[] rows, Func<uint, string> read)
        => rows.Select(read).Where(t => t.Length > 0).ToArray();

    private static string AddonText(uint row)
        => Svc.Data.GetExcelSheet<AddonSheet>().GetRowOrDefault(row)?.Text.GetText() ?? "";

    private static string BellText(uint row)
        => Svc.Data.GetExcelSheet<QuestDialogueText>(name: RetainerBellSheet).GetRowOrDefault(row)?.Value.GetText() ?? "";
}
