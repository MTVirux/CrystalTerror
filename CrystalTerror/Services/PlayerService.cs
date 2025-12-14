using Dalamud.Plugin.Services;

namespace CrystalTerror.Services;

/// <summary>
/// Provides global access to player state and game objects.
/// </summary>
public static class PlayerService
{
    /// <summary>
    /// Gets the player state service (login status, content ID, etc.).
    /// </summary>
    public static IPlayerState State { get; internal set; } = null!;

    /// <summary>
    /// Gets the object table service (local player, game objects, etc.).
    /// </summary>
    public static IObjectTable Objects { get; internal set; } = null!;
}
