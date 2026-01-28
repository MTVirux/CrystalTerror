using Dalamud.Configuration;

namespace CrystalTerror;

/// <summary>
/// Plugin configuration persisted by Dalamud's config system.
/// </summary>
public class Configuration : IPluginConfiguration
{
    /// <inheritdoc />
    public int Version { get; set; } = 1;

    // ===== Data Storage =====
    
    /// <summary>
    /// Stored characters persisted by Dalamud's plugin config system.
    /// </summary>
    public List<StoredCharacter> Characters { get; set; } = new List<StoredCharacter>();

    // ===== General Settings =====

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
    /// If true, show crystal totals in character headers.
    /// </summary>
    public bool ShowTotalsInHeaders { get; set; } = true;

    /// <summary>
    /// If true, show element names in header totals (e.g., "Fire: 100/200/300" vs "100/200/300").
    /// </summary>
    public bool ShowElementNamesInTotals { get; set; } = true;

    /// <summary>
    /// If true, use abbreviated element names (first 2 characters) in character headers.
    /// </summary>
    public bool UseAbbreviatedElementNames { get; set; } = false;

    /// <summary>
    /// If true, use reduced notation (22k, 1.5M) for numbers in character headers.
    /// </summary>
    public bool UseReducedNotationInHeaders { get; set; } = false;

    /// <summary>
    /// If true, use reduced notation (22k, 1.5M) for numbers in table cells.
    /// </summary>
    public bool UseReducedNotationInTables { get; set; } = false;

    /// <summary>
    /// If true, show the full non-abbreviated numbers in a tooltip when hovering abbreviated table cells.
    /// </summary>
    public bool ShowFullNumbersOnHoverInTables { get; set; } = true;

    /// <summary>
    /// If true, color the currently logged in character's header.
    /// </summary>
    public bool ColorCurrentCharacter { get; set; } = false;

    /// <summary>
    /// Color to use for the currently logged in character's header.
    /// </summary>
    // Default: #265B2AFF (R:0x26, G:0x5B, B:0x2A, A:0xFF)
    public Vector4 CurrentCharacterColor { get; set; } = new Vector4(0.14901961f, 0.35686275f, 0.16470588f, 1.0f);

    // ===== Progress Bar Settings (AutoRetainer-style) =====

    /// <summary>
    /// If true, show progress bars behind character headers (AutoRetainer-style).
    /// </summary>
    public bool ShowProgressBars { get; set; } = false;

    /// <summary>
    /// Maximum value for progress bar calculation. A character with this total crystal count shows a full bar.
    /// </summary>
    public long ProgressBarMaxValue { get; set; } = 100000;

    // ===== Display Filters =====

    // Elements
    public bool ShowFireElement { get; set; } = true;
    public bool ShowIceElement { get; set; } = true;
    public bool ShowWindElement { get; set; } = true;
    public bool ShowLightningElement { get; set; } = true;
    public bool ShowEarthElement { get; set; } = true;
    public bool ShowWaterElement { get; set; } = true;

    // Crystal Types
    public bool ShowShards { get; set; } = true;
    public bool ShowCrystals { get; set; } = true;
    public bool ShowClusters { get; set; } = true;

    // Character Name Display
    /// <summary>
    /// How character names are displayed in the UI (full name, first name, last name, or initials).
    /// </summary>
    public NameDisplayFormat NameDisplayFormat { get; set; } = NameDisplayFormat.FullName;

    /// <summary>
    /// Whether to show the world/server name after the character name.
    /// </summary>
    public bool ShowWorldInHeader { get; set; } = true;

    // ===== Character Sorting =====

    /// <summary>
    /// How characters should be sorted in the main window.
    /// </summary>
    public CharacterSortOptions CharacterSortOption { get; set; } = CharacterSortOptions.Alphabetical;

    /// <summary>
    /// Whether the main window is in edit mode for custom character ordering.
    /// </summary>
    public bool IsEditMode { get; set; } = false;

    // ===== Warning Thresholds =====

    // Retainer Crystal Warning Thresholds
    public bool RetainerCrystalThreshold1Enabled { get; set; } = false;
    public int RetainerCrystalThreshold1Value { get; set; } = 8000;
    public Vector4 RetainerCrystalThreshold1Color { get; set; } = new Vector4(0.0f, 1.0f, 0.0f, 1.0f); // Green

    public bool RetainerCrystalThreshold2Enabled { get; set; } = false;
    public int RetainerCrystalThreshold2Value { get; set; } = 9879;
    public Vector4 RetainerCrystalThreshold2Color { get; set; } = new Vector4(1.0f, 1.0f, 0.0f, 1.0f); // Yellow

    public bool RetainerCrystalThreshold3Enabled { get; set; } = false;
    public int RetainerCrystalThreshold3Value { get; set; } = 9999;
    public Vector4 RetainerCrystalThreshold3Color { get; set; } = new Vector4(1.0f, 0.0f, 0.0f, 1.0f); // Red

    // Character Total Warning Thresholds
    public bool CharacterTotalThreshold1Enabled { get; set; } = false;
    public int CharacterTotalThreshold1Value { get; set; } = 432000;
    public Vector4 CharacterTotalThreshold1Color { get; set; } = new Vector4(0.0f, 1.0f, 0.0f, 1.0f); // Green

    public bool CharacterTotalThreshold2Enabled { get; set; } = false;
    public int CharacterTotalThreshold2Value { get; set; } = 533466;
    public Vector4 CharacterTotalThreshold2Color { get; set; } = new Vector4(1.0f, 1.0f, 0.0f, 1.0f); // Yellow

    public bool CharacterTotalThreshold3Enabled { get; set; } = false;
    public int CharacterTotalThreshold3Value { get; set; } = 539946;
    public Vector4 CharacterTotalThreshold3Color { get; set; } = new Vector4(1.0f, 0.0f, 0.0f, 1.0f); // Red

    // ===== Automatic Venture Assignment =====

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
    public long AutoVentureThreshold { get; set; } = 0;

    /// <summary>
    /// Priority preference when crystal/shard counts are tied.
    /// </summary>
    public VenturePriority AutoVenturePriority { get; set; } = VenturePriority.Balanced;

    /// <summary>
    /// Expected reward amount per venture. Used to estimate pending rewards from active ventures.
    /// Default is 120 (standard 1-hour gathering venture yield).
    /// </summary>
    public int AutoVentureRewardAmount { get; set; } = 120;

    /// <summary>
    /// Whether to include Fisher (FSH) retainers in automatic venture assignment.
    /// If false, FSH retainers are skipped entirely.
    /// </summary>
    public bool AutoVentureFSHEnabled { get; set; } = false;

    /// <summary>
    /// Whether to check venture credit count before assigning crystal ventures.
    /// If true and credits are below threshold, Quick Exploration is assigned instead.
    /// </summary>
    public bool AutoVentureCreditCheckEnabled { get; set; } = false;

    /// <summary>
    /// Minimum venture credits required to assign crystal/shard ventures.
    /// If current credit count is below this, Quick Exploration is assigned instead.
    /// Default is 0 (disabled). Typical values: 2-10 depending on retainer count.
    /// </summary>
    public int AutoVentureCreditThreshold { get; set; } = 2;

    /// <summary>
    /// What to do when all enabled crystal/shard types are above threshold (full).
    /// Default is SpecificVenture (assigns the venture specified by AutoVentureFallbackVentureId).
    /// Set to Skip to let AutoRetainer handle the venture.
    /// </summary>
    public FallbackVentureMode AutoVentureFallbackMode { get; set; } = FallbackVentureMode.SpecificVenture;

    /// <summary>
    /// The venture ID to assign when crystals are full and AutoVentureFallbackMode is SpecificVenture.
    /// Default is 395 (Quick Exploration). Can be set to any valid RetainerTask row ID.
    /// </summary>
    public uint AutoVentureFallbackVentureId { get; set; } = 395; // Quick Exploration

    /// <summary>
    /// Per element×type venture settings. Key format: "Element_CrystalType" (e.g., "Fire_Crystal").
    /// Each entry controls whether the type is enabled and its individual threshold.
    /// </summary>
    public Dictionary<string, PerTypeVentureSetting> AutoVenturePerTypeSettings { get; set; } = new();

    /// <summary>
    /// Get or create the per-type setting for a specific element and crystal type.
    /// </summary>
    public PerTypeVentureSetting GetPerTypeSetting(Element element, CrystalType type)
    {
        var key = $"{element}_{type}";
        if (!AutoVenturePerTypeSettings.TryGetValue(key, out var setting))
        {
            setting = new PerTypeVentureSetting();
            AutoVenturePerTypeSettings[key] = setting;
        }
        return setting;
    }

    // ===== Window Settings =====

    /// <summary>
    /// If true, the main window position and size are locked.
    /// </summary>
    public bool PinMainWindow { get; set; } = false;

    /// <summary>
    /// Saved position for the main window when pinned.
    /// </summary>
    public Vector2 MainWindowPos { get; set; } = new Vector2(100, 100);

    /// <summary>
    /// Saved size for the main window when pinned.
    /// </summary>
    public Vector2 MainWindowSize { get; set; } = new Vector2(600, 400);

    /// <summary>
    /// If true, the config window position and size are locked.
    /// </summary>
    public bool PinConfigWindow { get; set; } = false;

    /// <summary>
    /// Saved position for the config window when pinned.
    /// </summary>
    public Vector2 ConfigWindowPos { get; set; } = new Vector2(100, 100);

    /// <summary>
    /// Saved size for the config window when pinned.
    /// </summary>
    public Vector2 ConfigWindowSize { get; set; } = new Vector2(600, 400);
}
