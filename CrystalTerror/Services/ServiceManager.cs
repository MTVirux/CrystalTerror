using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace CrystalTerror.Services;

/// <summary>
/// Centralized manager for initializing all global services.
/// </summary>
public static class ServiceManager
{
    /// <summary>
    /// Initialize all services. Should only be called once during plugin construction.
    /// </summary>
    public static void Initialize(
        IDalamudPluginInterface pluginInterface,
        IPlayerState playerState,
        IObjectTable objectTable,
        IDataManager dataManager,
        IPluginLog pluginLog,
        ICondition condition)
    {
        PluginInterfaceService.Interface = pluginInterface;
        PlayerService.State = playerState;
        PlayerService.Objects = objectTable;
        DataService.Manager = dataManager;
        LogService.Log = pluginLog;
        GameStateService.Condition = condition;
    }
}
