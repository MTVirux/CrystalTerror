using Dalamud.Plugin.Services;

namespace CrystalTerror.Services
{
    /// <summary>
    /// Provides global access to plugin logging.
    /// </summary>
    public static class LogService
    {
        /// <summary>
        /// Gets the plugin logger.
        /// </summary>
        public static IPluginLog Log { get; internal set; } = null!;
    }
}
