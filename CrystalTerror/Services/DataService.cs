using Dalamud.Plugin.Services;

namespace CrystalTerror.Services;

/// <summary>
/// Provides global access to game data services.
/// </summary>
public static class DataService
{
    /// <summary>
    /// Gets the data manager for accessing game data (Excel sheets, items, etc.).
    /// </summary>
    public static IDataManager Manager { get; internal set; } = null!;
}
