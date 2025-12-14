using Dalamud.Plugin;

namespace CrystalTerror.Services;

/// <summary>
/// Provides global access to the Dalamud plugin interface for IPC and configuration.
/// </summary>
public static class PluginInterfaceService
{
    /// <summary>
    /// Gets the Dalamud plugin interface.
    /// </summary>
    public static IDalamudPluginInterface Interface { get; internal set; } = null!;
}
