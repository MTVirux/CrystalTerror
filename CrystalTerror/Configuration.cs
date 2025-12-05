using System.Collections.Generic;
using Dalamud.Configuration;

namespace CrystalTerror
{
    public class Configuration : IPluginConfiguration
    {
        /// <inheritdoc />
        public int Version { get; set; } = 1;

        /// <summary>
        /// If true, the main window is opened on plugin start.
        /// </summary>
        public bool ShowOnStart { get; set; } = true;

        /// <summary>
        /// Stored characters persisted by Dalamud's plugin config system.
        /// </summary>
        public List<StoredCharacter> Characters { get; set; } = new List<StoredCharacter>();
    }
}
