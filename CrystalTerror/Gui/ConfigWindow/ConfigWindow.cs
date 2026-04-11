namespace CrystalTerror.Gui.ConfigWindow;

using System;
using CrystalTerror.Helpers;
using CrystalTerror.Gui.Common;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using NightmareUI.OtterGuiWrapper.FileSystems.Configuration;
using CrystalTerror.Gui.ConfigEntries;
using ImGui = Dalamud.Bindings.ImGui.ImGui;

public class ConfigWindow : Window, IDisposable
{
    private readonly CrystalTerrorPlugin plugin;
    internal ConfigFileSystem FileSystem;
    private ConfigWindowContainerComponent? containerComponent;
    
    // Lock button reference for dynamic icon updates
    private TitleBarButton? lockButton;

    private readonly ConfigFileSystemEntry[] ConfigTabs =
    [
        new GeneralSettings(),
        new DisplayFilters(),
        new CharacterSorting(),
        new WarningThresholds(),
        new AutomaticVenture(),
        new DataManagement(),
    ];

    public ConfigWindow(CrystalTerrorPlugin plugin)
        : base("CrystalTerror Configuration")
    {
        this.plugin = plugin;
        this.SizeConstraints = new WindowSizeConstraints()
        {
            MinimumSize = new System.Numerics.Vector2(600, 400),
            MaximumSize = new System.Numerics.Vector2(float.MaxValue, float.MaxValue),
        };

        // Create and add lock button to title bar
        lockButton = new TitleBarButton
        {
            Icon = plugin.Config.PinConfigWindow ? FontAwesomeIcon.Lock : FontAwesomeIcon.LockOpen,
            IconOffset = new System.Numerics.Vector2(2, 2),
            ShowTooltip = () => ImGui.SetTooltip("Lock window position and size"),
        };
        lockButton.Click = (m) => 
        {
            if (m == ImGuiMouseButton.Left)
            {
                // Toggle pinned state. When enabling pin, capture the current window
                // position and size so the config window remains where the user placed it.
                var newPinned = !plugin.Config.PinConfigWindow;
                plugin.Config.PinConfigWindow = newPinned;
                if (newPinned)
                {
                    plugin.Config.ConfigWindowPos = ImGui.GetWindowPos();
                    plugin.Config.ConfigWindowSize = ImGui.GetWindowSize();
                }
                ConfigHelper.SaveAndSync(plugin.Config, plugin.Characters);
                lockButton!.Icon = plugin.Config.PinConfigWindow ? FontAwesomeIcon.Lock : FontAwesomeIcon.LockOpen;
            }
        };
        TitleBarButtons.Add(lockButton);

        // Initialize config file system
        FileSystem = new(() => ConfigTabs);

        // Initialize container component
        this.containerComponent = new ConfigWindowContainerComponent(FileSystem);
    }

    public void Dispose()
    {
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
        
        // Update lock button icon to reflect current state
        if (lockButton != null)
        {
            lockButton.Icon = plugin.Config.PinConfigWindow ? FontAwesomeIcon.Lock : FontAwesomeIcon.LockOpen;
        }
    }

    public override void PostDraw()
    {
        // When locked, the PreDraw will reset size next frame
        // allowing temporary stretching during the current frame only
    }

    public override void Draw()
    {
        if (this.containerComponent != null)
        {
            this.containerComponent.Render();
        }
    }

    public override void OnClose()
    {
        ConfigHelper.Save(this.plugin.Config);
    }
}
