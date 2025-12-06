using Dalamud.Plugin.Services;

namespace CrystalTerror.Services
{
    /// <summary>
    /// Provides global access to game state services.
    /// </summary>
    public static class GameStateService
    {
        /// <summary>
        /// Gets the condition service for checking game state (at summoning bell, in combat, etc.).
        /// </summary>
        public static ICondition Condition { get; internal set; } = null!;
    }
}
