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
                }
            }
            ImGui.SameLine();
            if (ImGui.Button("Import From AutoRetainer"))
            {
                var list = CharacterHelper.ImportFromAutoRetainer();
                CharacterHelper.MergeInto(this.plugin.Characters, list, CharacterHelper.MergePolicy.Overwrite);
                this.plugin.Config.Characters = this.plugin.Characters;
                this.plugin.PluginInterface.SavePluginConfig(this.plugin.Config);
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
                    // Try to import the currently-logged-in character immediately after purging
                    try
                    {
                        var sc = CharacterHelper.ImportCurrentCharacter();
                        if (sc != null)
                        {
                            CharacterHelper.MergeInto(this.plugin.Characters, new[] { sc }, CharacterHelper.MergePolicy.Skip);
                            this.plugin.Config.Characters = this.plugin.Characters;
                            this.plugin.PluginInterface.SavePluginConfig(this.plugin.Config);
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
