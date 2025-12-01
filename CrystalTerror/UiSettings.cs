using System;

namespace CrystalTerror
{
    /// <summary>
    /// Holds UI layout and timing settings used by the plugin.
    /// Provides grouped nested settings for timing, sizing and spacing.
    /// </summary>
    public class UiSettings
    {
        /// <summary>Timing-related UI/settings values (scan intervals, delays).</summary>
        public TimingSettings Timing { get; set; } = new TimingSettings();
        /// <summary>Sizing constraints used when laying out the main window and columns.</summary>
        public SizingSettings Sizing { get; set; } = new SizingSettings();
        /// <summary>Spacing and padding values used in various UI elements.</summary>
        public SpacingSettings Spacing { get; set; } = new SpacingSettings();

        /// <summary>Group of timing-related settings.</summary>
        public class TimingSettings
        {
            /// <summary>Milliseconds between automatic inventory scans.</summary>
            public int ScanIntervalMs { get; set; } = 1000; // Milliseconds between scans (framework-driven)
            /// <summary>Debounce delay (ms) used when saving configuration.</summary>
            public int SaveDelayMs { get; set; } = 3000; // Debounce delay for saving config
            /// <summary>Interval (ms) for background IPC refresh tasks.</summary>
            public int IpcIntervalMs { get; set; } = 2000; // Background IPC refresh interval
        }

        /// <summary>Settings that control width/minimum sizes for UI columns and labels.</summary>
        public class SizingSettings
        {
            /// <summary>Minimum width reserved for the character name column.</summary>
            public float MinCharWidth { get; set; } = 250.0f; // Layout: minimum reserved width for character column before shrinking
            /// <summary>Minimum width for each data (crystal) column.</summary>
            public float MinDataColWidth { get; set; } = 30.0f; // Layout: minimum width for each data/crystal column
            /// <summary>Threshold width to decide if a header should render in full mode.</summary>
            public float HeaderFullMinWidth { get; set; } = 300.0f; // Layout: threshold of char column width to decide whether collapsed-header uses full element
            /// <summary>Minimum width to reserve for labels when truncating header text.</summary>
            public float MinLabelWidth { get; set; } = 40.0f; // Minimum visible label width used when truncating header text
        }

        /// <summary>Spacing and padding values for UI widgets and groups.</summary>
        public class SpacingSettings
        {
            /// <summary>Space reserved for arrow/padding in collapsing headers.</summary>
            public float ArrowReserve { get; set; } = 24.0f; // Space reserved for arrow/padding in collapsing header
            /// <summary>Inner padding used when computing label widths.</summary>
            public float LabelInnerPadding { get; set; } = 8.0f; // Inner padding subtracted from char column when computing max label width
            /// <summary>Left padding inside each data column.</summary>
            public float ColumnLeftPadding { get; set; } = 4.0f; // Left padding inside each data column when drawing labels
            /// <summary>Gap between label and value in collapsed header rows.</summary>
            public float LabelValueGap { get; set; } = 6.0f; // Gap between label and value in collapsed header
            /// <summary>Spacing used when centering checkbox groups.</summary>
            public float CenteredGroupSpacing { get; set; } = 20.0f; // Spacing used when centering checkbox groups
            /// <summary>Size of the small settings button.</summary>
            public float SettingsButtonSize { get; set; } = 20.0f; // Size for the small settings button
            /// <summary>Indent applied to retainer names inside the table.</summary>
            public float RetainerIndent { get; set; } = 20.0f; // Indent used for retainer names inside table
        }
    }
}
