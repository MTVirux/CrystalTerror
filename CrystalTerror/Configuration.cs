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
        /// If true, the main window will not close when pressing ESC.
        /// </summary>
        public bool IgnoreEscapeOnMainWindow { get; set; } = false;

        /// <summary>
        /// If true, hide characters that have no gathering retainers.
        /// </summary>
        public bool HideNonGatheringCharacters { get; set; } = false;

        /// <summary>
        /// If true, show the currently logged in character at the top of the list.
        /// </summary>
        public bool ShowCurrentCharacterAtTop { get; set; } = false;

        /// <summary>
        /// If true, use abbreviated element names (first 2 characters) in character headers.
        /// </summary>
        public bool UseAbbreviatedElementNames { get; set; } = false;

        /// <summary>
        /// If true, show crystal totals in character headers.
        /// </summary>
        public bool ShowTotalsInHeaders { get; set; } = true;

        /// <summary>
        /// If true, show element names in header totals (e.g., "Fire: 100/200/300" vs "100/200/300").
        /// </summary>
        public bool ShowElementNamesInTotals { get; set; } = true;

        /// <summary>
        /// If true, color the currently logged in character's header.
        /// </summary>
        public bool ColorCurrentCharacter { get; set; } = false;

        /// <summary>
        /// Color to use for the currently logged in character's header.
        /// </summary>
        public System.Numerics.Vector4 CurrentCharacterColor { get; set; } = new System.Numerics.Vector4(0.2f, 0.8f, 0.2f, 1.0f);

        // Character Total Warning Thresholds
        public bool CharacterTotalThreshold1Enabled { get; set; } = false;
        public int CharacterTotalThreshold1Value { get; set; } = 5000;
        public System.Numerics.Vector4 CharacterTotalThreshold1Color { get; set; } = new System.Numerics.Vector4(1.0f, 0.0f, 0.0f, 1.0f);

        public bool CharacterTotalThreshold2Enabled { get; set; } = false;
        public int CharacterTotalThreshold2Value { get; set; } = 8000;
        public System.Numerics.Vector4 CharacterTotalThreshold2Color { get; set; } = new System.Numerics.Vector4(1.0f, 0.65f, 0.0f, 1.0f);

        public bool CharacterTotalThreshold3Enabled { get; set; } = false;
        public int CharacterTotalThreshold3Value { get; set; } = 9879;
        public System.Numerics.Vector4 CharacterTotalThreshold3Color { get; set; } = new System.Numerics.Vector4(1.0f, 1.0f, 0.0f, 1.0f);

        /// <summary>
        /// Stored characters persisted by Dalamud's plugin config system.
        /// </summary>
        public List<StoredCharacter> Characters { get; set; } = new List<StoredCharacter>();

        /// <summary>
        /// If true, automatically assign ventures to retainers based on lowest crystal/shard counts.
        /// </summary>
        public bool AutoVentureEnabled { get; set; } = false;

        /// <summary>
        /// If true, automatic venture assignment will consider shards when determining lowest crystal type.
        /// </summary>
        public bool AutoVentureShardsEnabled { get; set; } = true;

        /// <summary>
        /// If true, automatic venture assignment will consider crystals when determining lowest crystal type.
        /// </summary>
        public bool AutoVentureCrystalsEnabled { get; set; } = true;

        /// <summary>
        /// If all enabled crystal/shard types for a retainer are above this threshold, skip venture assignment for that retainer.
        /// Set to 0 to disable threshold checking (always assign ventures).
        /// </summary>
        public long AutoVentureThreshold { get; set; } = 9879;

        // Display Filters - Elements
        public bool ShowFireElement { get; set; } = true;
        public bool ShowIceElement { get; set; } = true;
        public bool ShowWindElement { get; set; } = true;
        public bool ShowLightningElement { get; set; } = true;
        public bool ShowEarthElement { get; set; } = true;
        public bool ShowWaterElement { get; set; } = true;

        // Display Filters - Crystal Types
        public bool ShowShards { get; set; } = true;
        public bool ShowCrystals { get; set; } = true;
        public bool ShowClusters { get; set; } = true;

        /// <summary>
        /// How characters should be sorted in the main window.
        /// </summary>
        public CharacterSortOptions CharacterSortOption { get; set; } = CharacterSortOptions.Alphabetical;

        /// <summary>
        /// Whether the main window is in edit mode for custom character ordering.
        /// </summary>
        public bool IsEditMode { get; set; } = false;

        /// <summary>
        /// If true, the main window position and size are locked.
        /// </summary>
        public bool PinMainWindow { get; set; } = false;

        /// <summary>
        /// Saved position for the main window when pinned.
        /// </summary>
        public System.Numerics.Vector2 MainWindowPos { get; set; } = new System.Numerics.Vector2(100, 100);

        /// <summary>
        /// Saved size for the main window when pinned.
        /// </summary>
        public System.Numerics.Vector2 MainWindowSize { get; set; } = new System.Numerics.Vector2(600, 400);

        /// <summary>
        /// If true, the config window position and size are locked.
        /// </summary>
        public bool PinConfigWindow { get; set; } = false;

        /// <summary>
        /// Saved position for the config window when pinned.
        /// </summary>
        public System.Numerics.Vector2 ConfigWindowPos { get; set; } = new System.Numerics.Vector2(100, 100);

        /// <summary>
        /// Saved size for the config window when pinned.
        /// </summary>
        public System.Numerics.Vector2 ConfigWindowSize { get; set; } = new System.Numerics.Vector2(600, 400);

        // Warning Thresholds
        /// <summary>
        /// If true, warning threshold 1 is enabled.
        /// </summary>
        public bool WarningThreshold1Enabled { get; set; } = false;

        /// <summary>
        /// Value for warning threshold 1 (1-9999).
        /// </summary>
        public int WarningThreshold1Value { get; set; } = 8000;

        /// <summary>
        /// Color for warning threshold 1 (RGBA format).
        /// </summary>
        public System.Numerics.Vector4 WarningThreshold1Color { get; set; } = new System.Numerics.Vector4(1.0f, 0.0f, 0.0f, 1.0f); // Red

        /// <summary>
        /// If true, warning threshold 2 is enabled.
        /// </summary>
        public bool WarningThreshold2Enabled { get; set; } = false;

        /// <summary>
        /// Value for warning threshold 2 (1-9999).
        /// </summary>
        public int WarningThreshold2Value { get; set; } = 9879;

        /// <summary>
        /// Color for warning threshold 2 (RGBA format).
        /// </summary>
        public System.Numerics.Vector4 WarningThreshold2Color { get; set; } = new System.Numerics.Vector4(1.0f, 0.65f, 0.0f, 1.0f); // Orange

        /// <summary>
        /// If true, warning threshold 3 is enabled.
        /// </summary>
        public bool WarningThreshold3Enabled { get; set; } = false;

        /// <summary>
        /// Value for warning threshold 3 (1-9999).
        /// </summary>
        public int WarningThreshold3Value { get; set; } = 9999;

        /// <summary>
        /// Color for warning threshold 3 (RGBA format).
        /// </summary>
        public System.Numerics.Vector4 WarningThreshold3Color { get; set; } = new System.Numerics.Vector4(1.0f, 1.0f, 0.0f, 1.0f); // Yellow
    }
}
