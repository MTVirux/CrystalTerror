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
    private WindowLockButtonComponent? lockButtonComponent;
    internal ConfigFileSystem FileSystem;
    private ConfigWindowContainerComponent? containerComponent;

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

        // Initialize lock button component
        this.lockButtonComponent = new WindowLockButtonComponent(plugin, isConfigWindow: true);

        // Create and add lock button to title bar
        TitleBarButtons.Add(new TitleBarButton
        {
            Click = (m) => 
            {
                if (m == ImGuiMouseButton.Left)
                {
                    // Toggle pin state
                    plugin.Config.PinConfigWindow = !plugin.Config.PinConfigWindow;
                    if (plugin.Config.PinConfigWindow)
                    {
                        plugin.Config.ConfigWindowPos = ImGui.GetWindowPos();
                        plugin.Config.ConfigWindowSize = ImGui.GetWindowSize();
                    }
                    ConfigHelper.SaveAndSync(plugin.Config, plugin.Characters);
                }
            },
            Icon = FontAwesomeIcon.Thumbtack,
            IconOffset = new System.Numerics.Vector2(2, 2),
            ShowTooltip = () => ImGui.SetTooltip(plugin.Config.PinConfigWindow ? "Unlock window" : "Lock window position and size"),
        });

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
