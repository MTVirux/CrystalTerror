using System.Collections;
using System.Reflection;
using System.Runtime.Loader;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using ECommons.Reflection;

namespace CrystalTerror.Helpers;

public static class AutoRetainerPlannerBridge
{
    private const string AdditionalRetainerDataTypeName = "AutoRetainerAPI.Configuration.AdditionalRetainerData";

    private sealed class PlannerFields
    {
        public readonly Type DataType;
        public readonly FieldInfo VenturePlan;
        public readonly FieldInfo VenturePlanIndex;
        public readonly FieldInfo EnablePlanner;
        public readonly FieldInfo PlanList;
        public readonly FieldInfo PlanCompleteBehavior;
        public readonly ConstructorInfo PlannedVentureCtor;
        private readonly object subscriber;
        private readonly MethodInfo invokeFunc;

        public PlannerFields(Type dataType)
        {
            DataType = dataType;
            VenturePlan = GetField(dataType, "VenturePlan");
            VenturePlanIndex = GetField(dataType, "VenturePlanIndex");
            EnablePlanner = GetField(dataType, "EnablePlanner");
            PlanList = GetField(VenturePlan.FieldType, "List");
            PlanCompleteBehavior = GetField(VenturePlan.FieldType, "PlanCompleteBehavior");
            var plannedVentureType = PlanList.FieldType.GetGenericArguments()[0];
            PlannedVentureCtor = plannedVentureType.GetConstructor([typeof(uint), typeof(int)])
                ?? throw new MissingMethodException(plannedVentureType.FullName, ".ctor(uint, int)");

            // Dalamud JSON-copies IPC results unless the subscriber's return type is the provider's exact type,
            // so the subscriber has to be built against AutoRetainer's own AdditionalRetainerData type.
            var getSubscriber = typeof(IDalamudPluginInterface).GetMethods()
                .Single(m => m.Name == nameof(IDalamudPluginInterface.GetIpcSubscriber) && m.GetGenericArguments().Length == 3)
                .MakeGenericMethod(typeof(ulong), typeof(string), dataType);
            subscriber = getSubscriber.Invoke(Svc.PluginInterface, ["AutoRetainer.GetAdditionalRetainerData"])!;
            invokeFunc = typeof(ICallGateSubscriber<,,>).MakeGenericType(typeof(ulong), typeof(string), dataType)
                .GetMethod("InvokeFunc")!;
        }

        public object? Fetch(ulong cid, string retainerName) => invokeFunc.Invoke(subscriber, [cid, retainerName]);

        private static FieldInfo GetField(Type type, string name) =>
            type.GetField(name, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new MissingFieldException(type.FullName, name);
    }

    private sealed record Backup(object Data, PlannerFields Fields, object? VenturePlan, uint VenturePlanIndex, bool EnablePlanner);

    private static readonly Dictionary<(ulong Cid, string Name), Backup> Backups = [];
    private static PlannerFields? cachedFields;

    // Makes AutoRetainer only collect (not reassign) this retainer's venture on its current visit.
    // False, with nothing changed, if the retainer has its own active AR venture plan or AR's planner data couldn't be changed.
    public static bool ForceCollectOnly(ulong cid, string retainerName, uint currentVentureId)
    {
        lock (Backups)
        {
            try
            {
                var fields = GetPlannerFields()
                    ?? throw new InvalidOperationException("AutoRetainer's AdditionalRetainerData type not found");

                var data = fields.Fetch(cid, retainerName);
                if (data == null || !ReferenceEquals(data, fields.Fetch(cid, retainerName)))
                    throw new InvalidOperationException("AutoRetainer did not return its live retainer data");

                if (IsPlannerActive(fields, data))
                {
                    Svc.Log.Debug($"[CrystalTerror] {retainerName} follows its AutoRetainer venture plan, not forcing collect-only");
                    return false;
                }

                Backups.TryAdd((cid, retainerName), new Backup(
                    data,
                    fields,
                    fields.VenturePlan.GetValue(data),
                    (uint)fields.VenturePlanIndex.GetValue(data)!,
                    (bool)fields.EnablePlanner.GetValue(data)!));

                // Plan already at its end with Do_nothing makes AutoRetainer collect the venture without reassigning it.
                var plan = Activator.CreateInstance(fields.VenturePlan.FieldType)!;
                var list = (IList)fields.PlanList.GetValue(plan)!;
                list.Add(fields.PlannedVentureCtor.Invoke([currentVentureId, 1]));
                fields.PlanCompleteBehavior.SetValue(plan, Enum.Parse(fields.PlanCompleteBehavior.FieldType, "Do_nothing"));

                fields.VenturePlan.SetValue(data, plan);
                fields.VenturePlanIndex.SetValue(data, 1u);
                fields.EnablePlanner.SetValue(data, true);
                return true;
            }
            catch (Exception ex)
            {
                Svc.Log.Warning($"[CrystalTerror] Could not force collect-only planner for {retainerName}: {ex.GetBaseException().Message}");
                Restore(cid, retainerName);
                return false;
            }
        }
    }

    // Restores planner fields saved by ForceCollectOnly; no-op if nothing saved.
    public static void Restore(ulong cid, string retainerName)
    {
        lock (Backups)
        {
            if (Backups.Remove((cid, retainerName), out var backup))
                Apply(backup, retainerName);
        }
    }

    public static void RestoreAll()
    {
        lock (Backups)
        {
            foreach (var (key, backup) in Backups)
                Apply(backup, key.Name);
            Backups.Clear();
        }
    }

    private static bool IsPlannerActive(PlannerFields fields, object data)
        => (bool)fields.EnablePlanner.GetValue(data)!
           && fields.VenturePlan.GetValue(data) is { } plan
           && fields.PlanList.GetValue(plan) is IList { Count: > 0 };

    private static void Apply(Backup backup, string retainerName)
    {
        try
        {
            backup.Fields.VenturePlan.SetValue(backup.Data, backup.VenturePlan);
            backup.Fields.VenturePlanIndex.SetValue(backup.Data, backup.VenturePlanIndex);
            backup.Fields.EnablePlanner.SetValue(backup.Data, backup.EnablePlanner);
        }
        catch (Exception ex)
        {
            Svc.Log.Warning($"[CrystalTerror] Could not restore AutoRetainer planner for {retainerName}: {ex.Message}");
        }
    }

    private static PlannerFields? GetPlannerFields()
    {
        if (!DalamudReflector.TryGetDalamudPlugin("AutoRetainer", out var autoRetainer, suppressErrors: true, ignoreCache: true))
            return null;

        var dataType = AssemblyLoadContext.GetLoadContext(autoRetainer.GetType().Assembly)?.Assemblies
            .Select(a => a.GetType(AdditionalRetainerDataTypeName))
            .FirstOrDefault(t => t != null);
        if (dataType == null)
            return null;

        if (cachedFields?.DataType != dataType)
            cachedFields = new PlannerFields(dataType);
        return cachedFields;
    }
}
