namespace CrystalTerror.Gui;

using System;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using ImGui = Dalamud.Bindings.ImGui.ImGui;

public class ConfigWindow : Window, IDisposable
{
    private readonly CrystalTerrorPlugin plugin;
    private TitleBarButton lockButton;

    public ConfigWindow(CrystalTerrorPlugin plugin)
        : base("CrystalTerrorConfigWindow")
    {
        this.plugin = plugin;
        this.SizeConstraints = new WindowSizeConstraints()
        {
            MinimumSize = new System.Numerics.Vector2(300, 100),
            MaximumSize = ImGui.GetIO().DisplaySize,
        };

        // Initialize lock button
        lockButton = new TitleBarButton
        {
            Click = OnLockButtonClick,
            Icon = plugin.Config.PinConfigWindow ? FontAwesomeIcon.Lock : FontAwesomeIcon.LockOpen,
            IconOffset = new System.Numerics.Vector2(3, 2),
            ShowTooltip = () => ImGui.SetTooltip("Lock window position and size"),
        };

        // Add lock button to title bar
        TitleBarButtons.Add(lockButton);
    }

    public void Dispose()
    {
    }

    private void OnLockButtonClick(ImGuiMouseButton button)
    {
        if (button == ImGuiMouseButton.Left)
        {
            this.plugin.Config.PinConfigWindow = !this.plugin.Config.PinConfigWindow;
            lockButton.Icon = this.plugin.Config.PinConfigWindow ? FontAwesomeIcon.Lock : FontAwesomeIcon.LockOpen;

            // Save current position and size when locking
            if (this.plugin.Config.PinConfigWindow)
            {
                this.plugin.Config.ConfigWindowPos = ImGui.GetWindowPos();
                this.plugin.Config.ConfigWindowSize = ImGui.GetWindowSize();
            }

            this.plugin.PluginInterface.SavePluginConfig(this.plugin.Config);
        }
    }

    public override void PreDraw()
    {
        if (this.plugin.Config.PinConfigWindow)
        {
            Flags |= ImGuiWindowFlags.NoMove;
            Flags &= ~ImGuiWindowFlags.NoResize;
            ImGui.SetNextWindowPos(this.plugin.Config.ConfigWindowPos);
            ImGui.SetNextWindowSize(this.plugin.Config.ConfigWindowSize);
        }
        else
        {
            Flags &= ~ImGuiWindowFlags.NoMove;
        }
    }

    public override void PostDraw()
    {
        // When locked, the PreDraw will reset size next frame
        // allowing temporary stretching during the current frame only
    }

    public override void Draw()
    {
        ImGui.Text("Crystal Terror - Configuration");
        ImGui.Separator();

        var cfg = this.plugin.Config;

        // General Settings
        if (ImGui.CollapsingHeader("General Settings", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var show = cfg.ShowOnStart;
            if (ImGui.Checkbox("Show main window on start", ref show))
            {
                cfg.ShowOnStart = show;
                this.plugin.PluginInterface.SavePluginConfig(cfg);
            }

            var ignoreEsc = cfg.IgnoreEscapeOnMainWindow;
            if (ImGui.Checkbox("Main window ignores ESC key", ref ignoreEsc))
            {
                cfg.IgnoreEscapeOnMainWindow = ignoreEsc;
                this.plugin.PluginInterface.SavePluginConfig(cfg);
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("When enabled, pressing ESC will not close the main window");
            }

            var hideNonGathering = cfg.HideNonGatheringCharacters;
            if (ImGui.Checkbox("Hide characters without gathering retainers", ref hideNonGathering))
            {
                cfg.HideNonGatheringCharacters = hideNonGathering;
                this.plugin.PluginInterface.SavePluginConfig(cfg);
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Hide characters that have no gathering retainers (MIN/BTN/FSH)");
            }

            // Show current character at top option - force enabled for AutoRetainer sort
            bool showCurrentAtTop = cfg.ShowCurrentCharacterAtTop || cfg.CharacterSortOption == CharacterSortOptions.AutoRetainer;
            bool isAutoRetainerSort = cfg.CharacterSortOption == CharacterSortOptions.AutoRetainer;
            
            if (isAutoRetainerSort)
                ImGui.BeginDisabled();
            
            if (ImGui.Checkbox($"Show current character at top { (isAutoRetainerSort ? "(Enforced by AutoRetainer sort)" : "") }", ref showCurrentAtTop))
            {
                cfg.ShowCurrentCharacterAtTop = showCurrentAtTop;
                this.plugin.PluginInterface.SavePluginConfig(cfg);
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Show the currently logged in character at the top of the list");
            }
            
            if (isAutoRetainerSort)
                ImGui.EndDisabled();

            var showTotals = cfg.ShowTotalsInHeaders;
            if (ImGui.Checkbox("Show totals in character headers", ref showTotals))
            {
                cfg.ShowTotalsInHeaders = showTotals;
                this.plugin.PluginInterface.SavePluginConfig(cfg);
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Display crystal/shard/cluster totals in character headers");
            }

            // Sub-option: show element names in totals (indented)
            if (cfg.ShowTotalsInHeaders)
            {
                ImGui.Indent();
                var showElementNames = cfg.ShowElementNamesInTotals;
                if (ImGui.Checkbox("Show element names in totals", ref showElementNames))
                {
                    cfg.ShowElementNamesInTotals = showElementNames;
                    this.plugin.PluginInterface.SavePluginConfig(cfg);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Show element names before totals (e.g., 'Fire: 100/200/300' vs '100/200/300')");
                }

                // Sub-sub-option: abbreviated element names (indented further)
                if (cfg.ShowElementNamesInTotals)
                {
                    ImGui.Indent();
                    var useAbbrev = cfg.UseAbbreviatedElementNames;
                    if (ImGui.Checkbox("Use abbreviated element names", ref useAbbrev))
                    {
                        cfg.UseAbbreviatedElementNames = useAbbrev;
                        this.plugin.PluginInterface.SavePluginConfig(cfg);
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("Use first 2 characters only (e.g., 'Fi' instead of 'Fire')");
                    }
                    ImGui.Unindent();
                }

                ImGui.Unindent();
            }

            var colorCurrent = cfg.ColorCurrentCharacter;
            if (ImGui.Checkbox("Color current character header", ref colorCurrent))
            {
                cfg.ColorCurrentCharacter = colorCurrent;
                this.plugin.PluginInterface.SavePluginConfig(cfg);
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Apply a custom color to the currently logged in character's header");
            }

            // Sub-option: color picker (indented)
            if (cfg.ColorCurrentCharacter)
            {
                ImGui.Indent();
                var currentCharColor = cfg.CurrentCharacterColor;
                if (ImGui.ColorEdit4("Current character color", ref currentCharColor, ImGuiColorEditFlags.NoInputs))
                {
                    cfg.CurrentCharacterColor = currentCharColor;
                    this.plugin.PluginInterface.SavePluginConfig(cfg);
                }
                ImGui.Unindent();
            }
        }

        // Display Filters
        if (ImGui.CollapsingHeader("Display Filters"))
        {
            ImGui.Text("Elements:");
            ImGui.Indent();
            
            var showFire = cfg.ShowFireElement;
            if (ImGui.Checkbox("Fire", ref showFire))
            {
                cfg.ShowFireElement = showFire;
                this.plugin.PluginInterface.SavePluginConfig(cfg);
            }
            ImGui.SameLine();
            
            var showIce = cfg.ShowIceElement;
            if (ImGui.Checkbox("Ice", ref showIce))
            {
                cfg.ShowIceElement = showIce;
                this.plugin.PluginInterface.SavePluginConfig(cfg);
            }
            ImGui.SameLine();
            
            var showWind = cfg.ShowWindElement;
            if (ImGui.Checkbox("Wind", ref showWind))
            {
                cfg.ShowWindElement = showWind;
                this.plugin.PluginInterface.SavePluginConfig(cfg);
            }
            
            var showLightning = cfg.ShowLightningElement;
            if (ImGui.Checkbox("Lightning", ref showLightning))
            {
                cfg.ShowLightningElement = showLightning;
                this.plugin.PluginInterface.SavePluginConfig(cfg);
            }
            ImGui.SameLine();
            
            var showEarth = cfg.ShowEarthElement;
            if (ImGui.Checkbox("Earth", ref showEarth))
            {
                cfg.ShowEarthElement = showEarth;
                this.plugin.PluginInterface.SavePluginConfig(cfg);
            }
            ImGui.SameLine();
            
            var showWater = cfg.ShowWaterElement;
            if (ImGui.Checkbox("Water", ref showWater))
            {
                cfg.ShowWaterElement = showWater;
                this.plugin.PluginInterface.SavePluginConfig(cfg);
            }
            
            ImGui.Unindent();
            ImGui.Spacing();
            
            ImGui.Text("Crystal Types:");
            ImGui.Indent();
            
            var showShards = cfg.ShowShards;
            if (ImGui.Checkbox("Shards", ref showShards))
            {
                cfg.ShowShards = showShards;
                this.plugin.PluginInterface.SavePluginConfig(cfg);
            }
            ImGui.SameLine();
            
            var showCrystals = cfg.ShowCrystals;
            if (ImGui.Checkbox("Crystals", ref showCrystals))
            {
                cfg.ShowCrystals = showCrystals;
                this.plugin.PluginInterface.SavePluginConfig(cfg);
            }
            ImGui.SameLine();
            
            var showClusters = cfg.ShowClusters;
            if (ImGui.Checkbox("Clusters", ref showClusters))
            {
                cfg.ShowClusters = showClusters;
                this.plugin.PluginInterface.SavePluginConfig(cfg);
            }
            
            ImGui.Unindent();
        }

        // Character Sorting
        if (ImGui.CollapsingHeader("Character Sorting"))
        {
            ImGui.Text("Sort characters by:");
            ImGui.Indent();

            var currentSort = (int)cfg.CharacterSortOption;
            var sortOptions = new string[]
            {
                "Alphabetical (A-Z)",
                "Reverse Alphabetical (Z-A)",
                "World (A-Z)",
                "Reverse World (Z-A)",
                "AutoRetainer Order",
                "Custom Order"
            };

            if (ImGui.Combo("Sort Order", ref currentSort, sortOptions, sortOptions.Length))
            {
                cfg.CharacterSortOption = (CharacterSortOptions)currentSort;
                
                // Exit edit mode if switching away from Custom
                if (cfg.CharacterSortOption != CharacterSortOptions.Custom)
                {
                    cfg.IsEditMode = false;
                }
                
                // Invalidate cache since sort option changed
                this.plugin.InvalidateSortCache();
                
                this.plugin.PluginInterface.SavePluginConfig(cfg);
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Change how characters are sorted in the main window.");
            }

            // Show Edit Order button only when Custom is selected
            if (cfg.CharacterSortOption == CharacterSortOptions.Custom)
            {
                ImGui.Spacing();
                var editButtonText = cfg.IsEditMode ? "Exit Edit Mode" : "Edit Order";
                if (ImGui.Button(editButtonText))
                {
                    cfg.IsEditMode = !cfg.IsEditMode;
                    this.plugin.PluginInterface.SavePluginConfig(cfg);
                    
                    // Open main window when entering edit mode
                    if (cfg.IsEditMode)
                    {
                        this.plugin.OpenMainUi();
                    }
                }
                
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(cfg.IsEditMode 
                        ? "Exit edit mode to return to normal view." 
                        : "Enter edit mode to reorder characters with up/down arrows.");
                }
            }

            ImGui.Unindent();
        }

        // Warning Thresholds
        if (ImGui.CollapsingHeader("Warning Thresholds"))
        {
            ImGui.Indent();
            
            // Retainer Crystal Warning Thresholds
            if (ImGui.CollapsingHeader("Retainer Crystal Warning Thresholds"))
            {
                ImGui.TextWrapped("Configure up to 3 color-coded warning thresholds for crystal counts in the main window.");
                ImGui.Spacing();

                // Warning Threshold 1
            var threshold1Enabled = cfg.RetainerCrystalThreshold1Enabled;
            if (ImGui.Checkbox("Enable Warning Threshold 1", ref threshold1Enabled))
            {
                cfg.RetainerCrystalThreshold1Enabled = threshold1Enabled;
                this.plugin.PluginInterface.SavePluginConfig(cfg);
            }

            if (cfg.RetainerCrystalThreshold1Enabled)
            {
                ImGui.Indent();

                ImGui.Text("Threshold Value:");
                ImGui.SameLine();
                var threshold1Value = cfg.RetainerCrystalThreshold1Value;
                ImGui.SetNextItemWidth(250);
                if (ImGui.SliderInt("##threshold1value", ref threshold1Value, 1, 9999))
                {
                    cfg.RetainerCrystalThreshold1Value = threshold1Value;
                    this.plugin.PluginInterface.SavePluginConfig(cfg);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Values at or above this threshold will be colored (1-9999)");
                }

                ImGui.Text("Warning Color:");
                ImGui.SameLine();
                var threshold1Color = cfg.RetainerCrystalThreshold1Color;
                if (ImGui.ColorEdit4("##threshold1color", ref threshold1Color, ImGuiColorEditFlags.NoInputs))
                {
                    cfg.RetainerCrystalThreshold1Color = threshold1Color;
                    this.plugin.PluginInterface.SavePluginConfig(cfg);
                }

                ImGui.Unindent();
            }

            ImGui.Spacing();

            // Warning Threshold 2
            var threshold2Enabled = cfg.RetainerCrystalThreshold2Enabled;
            if (ImGui.Checkbox("Enable Warning Threshold 2", ref threshold2Enabled))
            {
                cfg.RetainerCrystalThreshold2Enabled = threshold2Enabled;
                this.plugin.PluginInterface.SavePluginConfig(cfg);
            }

            if (cfg.RetainerCrystalThreshold2Enabled)
            {
                ImGui.Indent();

                ImGui.Text("Threshold Value:");
                ImGui.SameLine();
                var threshold2Value = cfg.RetainerCrystalThreshold2Value;
                ImGui.SetNextItemWidth(250);
                if (ImGui.SliderInt("##threshold2value", ref threshold2Value, 1, 9999))
                {
                    cfg.RetainerCrystalThreshold2Value = threshold2Value;
                    this.plugin.PluginInterface.SavePluginConfig(cfg);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Values at or above this threshold will be colored (1-9999)");
                }

                ImGui.Text("Warning Color:");
                ImGui.SameLine();
                var threshold2Color = cfg.RetainerCrystalThreshold2Color;
                if (ImGui.ColorEdit4("##threshold2color", ref threshold2Color, ImGuiColorEditFlags.NoInputs))
                {
                    cfg.RetainerCrystalThreshold2Color = threshold2Color;
                    this.plugin.PluginInterface.SavePluginConfig(cfg);
                }

                ImGui.Unindent();
            }

            ImGui.Spacing();

            // Warning Threshold 3
            var threshold3Enabled = cfg.RetainerCrystalThreshold3Enabled;
            if (ImGui.Checkbox("Enable Warning Threshold 3", ref threshold3Enabled))
            {
                cfg.RetainerCrystalThreshold3Enabled = threshold3Enabled;
                this.plugin.PluginInterface.SavePluginConfig(cfg);
            }

            if (cfg.RetainerCrystalThreshold3Enabled)
            {
                ImGui.Indent();

                ImGui.Text("Threshold Value:");
                ImGui.SameLine();
                var threshold3Value = cfg.RetainerCrystalThreshold3Value;
                ImGui.SetNextItemWidth(250);
                if (ImGui.SliderInt("##threshold3value", ref threshold3Value, 1, 9999))
                {
                    cfg.RetainerCrystalThreshold3Value = threshold3Value;
                    this.plugin.PluginInterface.SavePluginConfig(cfg);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Values at or above this threshold will be colored (1-9999)");
                }

                ImGui.Text("Warning Color:");
                ImGui.SameLine();
                var threshold3Color = cfg.RetainerCrystalThreshold3Color;
                if (ImGui.ColorEdit4("##threshold3color", ref threshold3Color, ImGuiColorEditFlags.NoInputs))
                {
                    cfg.RetainerCrystalThreshold3Color = threshold3Color;
                    this.plugin.PluginInterface.SavePluginConfig(cfg);
                }

                ImGui.Unindent();
            }
            }

            ImGui.Spacing();

            // Character Total Warning Thresholds
            if (ImGui.CollapsingHeader("Character Total Warning Thresholds"))
            {
                ImGui.TextWrapped("Configure up to 3 color-coded warning thresholds for character total crystal counts (character + all retainers).");
                ImGui.Spacing();

                // Character Total Threshold 1
                var charThreshold1Enabled = cfg.CharacterTotalThreshold1Enabled;
                if (ImGui.Checkbox("Enable Character Total Threshold 1", ref charThreshold1Enabled))
                {
                    cfg.CharacterTotalThreshold1Enabled = charThreshold1Enabled;
                    this.plugin.PluginInterface.SavePluginConfig(cfg);
                }

                if (cfg.CharacterTotalThreshold1Enabled)
                {
                    ImGui.Indent();

                    ImGui.Text("Threshold Value:");
                    ImGui.SameLine();
                    var charThreshold1Value = cfg.CharacterTotalThreshold1Value;
                    ImGui.SetNextItemWidth(250);
                    if (ImGui.SliderInt("##charThreshold1value", ref charThreshold1Value, 1, 599940))
                    {
                        cfg.CharacterTotalThreshold1Value = charThreshold1Value;
                        this.plugin.PluginInterface.SavePluginConfig(cfg);
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("Character totals at or above this threshold will be colored (1-599940)");
                    }

                    ImGui.Text("Warning Color:");
                    ImGui.SameLine();
                    var charThreshold1Color = cfg.CharacterTotalThreshold1Color;
                    if (ImGui.ColorEdit4("##charThreshold1color", ref charThreshold1Color, ImGuiColorEditFlags.NoInputs))
                    {
                        cfg.CharacterTotalThreshold1Color = charThreshold1Color;
                        this.plugin.PluginInterface.SavePluginConfig(cfg);
                    }

                    ImGui.Unindent();
                }

                ImGui.Spacing();

                // Character Total Threshold 2
                var charThreshold2Enabled = cfg.CharacterTotalThreshold2Enabled;
                if (ImGui.Checkbox("Enable Character Total Threshold 2", ref charThreshold2Enabled))
                {
                    cfg.CharacterTotalThreshold2Enabled = charThreshold2Enabled;
                    this.plugin.PluginInterface.SavePluginConfig(cfg);
                }

                if (cfg.CharacterTotalThreshold2Enabled)
                {
                    ImGui.Indent();

                    ImGui.Text("Threshold Value:");
                    ImGui.SameLine();
                    var charThreshold2Value = cfg.CharacterTotalThreshold2Value;
                    ImGui.SetNextItemWidth(250);
                    if (ImGui.SliderInt("##charThreshold2value", ref charThreshold2Value, 1, 599940))
                    {
                        cfg.CharacterTotalThreshold2Value = charThreshold2Value;
                        this.plugin.PluginInterface.SavePluginConfig(cfg);
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("Character totals at or above this threshold will be colored (1-599940)");
                    }

                    ImGui.Text("Warning Color:");
                    ImGui.SameLine();
                    var charThreshold2Color = cfg.CharacterTotalThreshold2Color;
                    if (ImGui.ColorEdit4("##charThreshold2color", ref charThreshold2Color, ImGuiColorEditFlags.NoInputs))
                    {
                        cfg.CharacterTotalThreshold2Color = charThreshold2Color;
                        this.plugin.PluginInterface.SavePluginConfig(cfg);
                    }

                    ImGui.Unindent();
                }

                ImGui.Spacing();

                // Character Total Threshold 3
                var charThreshold3Enabled = cfg.CharacterTotalThreshold3Enabled;
                if (ImGui.Checkbox("Enable Character Total Threshold 3", ref charThreshold3Enabled))
                {
                    cfg.CharacterTotalThreshold3Enabled = charThreshold3Enabled;
                    this.plugin.PluginInterface.SavePluginConfig(cfg);
                }

                if (cfg.CharacterTotalThreshold3Enabled)
                {
                    ImGui.Indent();

                    ImGui.Text("Threshold Value:");
                    ImGui.SameLine();
                    var charThreshold3Value = cfg.CharacterTotalThreshold3Value;
                    ImGui.SetNextItemWidth(250);
                    if (ImGui.SliderInt("##charThreshold3value", ref charThreshold3Value, 1, 599940))
                    {
                        cfg.CharacterTotalThreshold3Value = charThreshold3Value;
                        this.plugin.PluginInterface.SavePluginConfig(cfg);
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("Character totals at or above this threshold will be colored (1-599940)");
                    }

                    ImGui.Text("Warning Color:");
                    ImGui.SameLine();
                    var charThreshold3Color = cfg.CharacterTotalThreshold3Color;
                    if (ImGui.ColorEdit4("##charThreshold3color", ref charThreshold3Color, ImGuiColorEditFlags.NoInputs))
                    {
                        cfg.CharacterTotalThreshold3Color = charThreshold3Color;
                        this.plugin.PluginInterface.SavePluginConfig(cfg);
                    }

                    ImGui.Unindent();
                }
            }
            
            ImGui.Unindent();
        }

        // Automatic Venture Assignment
        if (ImGui.CollapsingHeader("Automatic Venture Assignment"))
        {
            var autoVentureEnabled = cfg.AutoVentureEnabled;
            if (ImGui.Checkbox("Enable Automatic Venture Assignment", ref autoVentureEnabled))
            {
                cfg.AutoVentureEnabled = autoVentureEnabled;
                this.plugin.PluginInterface.SavePluginConfig(cfg);
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Automatically assign ventures to retainers based on their lowest crystal/shard counts when opening the retainer list.");
            }

            // Only show detailed options if auto-venture is enabled
            if (cfg.AutoVentureEnabled)
            {
                ImGui.Indent();

                var shardsEnabled = cfg.AutoVentureShardsEnabled;
                if (ImGui.Checkbox("Include Shards", ref shardsEnabled))
                {
                    cfg.AutoVentureShardsEnabled = shardsEnabled;
                    this.plugin.PluginInterface.SavePluginConfig(cfg);
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Consider shard counts when determining which venture to assign.");
                }

                var crystalsEnabled = cfg.AutoVentureCrystalsEnabled;
                if (ImGui.Checkbox("Include Crystals", ref crystalsEnabled))
                {
                    cfg.AutoVentureCrystalsEnabled = crystalsEnabled;
                    this.plugin.PluginInterface.SavePluginConfig(cfg);
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Consider crystal counts when determining which venture to assign.");
                }

                ImGui.Unindent();

                if (!cfg.AutoVentureShardsEnabled && !cfg.AutoVentureCrystalsEnabled)
                {
                    ImGui.TextColored(new System.Numerics.Vector4(1.0f, 0.5f, 0.0f, 1.0f), "Warning: At least one crystal type must be enabled.");
                }

                ImGui.Spacing();
                ImGui.Text("Threshold Settings:");
                
                var threshold = (int)cfg.AutoVentureThreshold;
                ImGui.SetNextItemWidth(200);
                if (ImGui.InputInt("Minimum Threshold", ref threshold))
                {
                    if (threshold < 0) threshold = 0;
                    cfg.AutoVentureThreshold = threshold;
                    this.plugin.PluginInterface.SavePluginConfig(cfg);
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("If all enabled crystal/shard types are above this value, assign Quick Exploration instead.\nSet to 0 to always assign crystal/shard ventures.");
                }
            }
        }

        // Data Management
        if (ImGui.CollapsingHeader("Data Management"))
        {
            if (ImGui.Button("Import Current Character"))
            {
                var sc = CharacterHelper.ImportCurrentCharacter();
                if (sc != null)
                {
                    CharacterHelper.MergeInto(this.plugin.Characters, new[] { sc }, CharacterHelper.MergePolicy.Skip);
                    this.plugin.Config.Characters = this.plugin.Characters;
                    this.plugin.PluginInterface.SavePluginConfig(this.plugin.Config);
                    this.plugin.InvalidateSortCache();
                }
            }
            ImGui.SameLine();
            if (ImGui.Button("Import From AutoRetainer"))
            {
                var list = CharacterHelper.ImportFromAutoRetainer();
                CharacterHelper.MergeInto(this.plugin.Characters, list, CharacterHelper.MergePolicy.Overwrite);
                this.plugin.Config.Characters = this.plugin.Characters;
                this.plugin.PluginInterface.SavePluginConfig(this.plugin.Config);
                this.plugin.InvalidateSortCache();
            }
            ImGui.SameLine();
            if (ImGui.Button("Purge Characters"))
            {
                ImGui.OpenPopup("PurgeConfirm");
            }

            if (ImGui.BeginPopupModal("PurgeConfirm", ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.TextWrapped("Are you sure you want to permanently purge all imported character data? This cannot be undone.");
                ImGui.Spacing();
                if (ImGui.Button("Yes, Purge"))
                {
                    this.plugin.Characters.Clear();
                    this.plugin.Config.Characters.Clear();
                    // Save cleared config
                    this.plugin.PluginInterface.SavePluginConfig(this.plugin.Config);
                    this.plugin.InvalidateSortCache();
                    // Try to import the currently-logged-in character immediately after purging
                    try
                    {
                        var sc = CharacterHelper.ImportCurrentCharacter();
                        if (sc != null)
                        {
                            CharacterHelper.MergeInto(this.plugin.Characters, new[] { sc }, CharacterHelper.MergePolicy.Skip);
                            this.plugin.Config.Characters = this.plugin.Characters;
                            this.plugin.PluginInterface.SavePluginConfig(this.plugin.Config);
                            this.plugin.InvalidateSortCache();
                        }
                    }
                    catch
                    {
                        // ignore errors during import
                    }
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel"))
                {
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }
        }

        ImGui.Spacing();
        // Configuration is saved immediately on change; no explicit Save button required.
        if (ImGui.Button("Close"))
            this.IsOpen = false;
    }
}
