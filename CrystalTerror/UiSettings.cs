using System;

namespace CrystalTerror
{
    // Simple holder for UI/layout/configurable timing values.
    // Kept lightweight so you can later persist to plugin config if desired.
    public class UiSettings
    {
        // Grouped settings for clarity
        public TimingSettings Timing { get; set; } = new TimingSettings();
        public SizingSettings Sizing { get; set; } = new SizingSettings();
        public SpacingSettings Spacing { get; set; } = new SpacingSettings();

        public class TimingSettings
        {
            public int ScanIntervalMs { get; set; } = 1000; // Milliseconds between scans (framework-driven)
            public int SaveDelayMs { get; set; } = 3000; // Debounce delay for saving config
            public int IpcIntervalMs { get; set; } = 2000; // Background IPC refresh interval
        }

        public class SizingSettings
        {
            public float MinCharWidth { get; set; } = 250.0f; // Layout: minimum reserved width for character column before shrinking
            public float MinDataColWidth { get; set; } = 30.0f; // Layout: minimum width for each data/crystal column
            public float HeaderFullMinWidth { get; set; } = 300.0f; // Layout: threshold of char column width to decide whether collapsed-header uses full element
            public float MinLabelWidth { get; set; } = 40.0f; // Minimum visible label width used when truncating header text
        }

        public class SpacingSettings
        {
            public float ArrowReserve { get; set; } = 24.0f; // Space reserved for arrow/padding in collapsing header
            public float LabelInnerPadding { get; set; } = 8.0f; // Inner padding subtracted from char column when computing max label width
            public float ColumnLeftPadding { get; set; } = 4.0f; // Left padding inside each data column when drawing labels
            public float LabelValueGap { get; set; } = 6.0f; // Gap between label and value in collapsed header
            public float CenteredGroupSpacing { get; set; } = 20.0f; // Spacing used when centering checkbox groups
            public float SettingsButtonSize { get; set; } = 20.0f; // Size for the small settings button
            public float RetainerIndent { get; set; } = 20.0f; // Indent used for retainer names inside table
        }
    }
}
