namespace CrystalTerror.Gui;

using System;
using CrystalTerror.Helpers;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using NightmareUI.OtterGuiWrapper.FileSystems.Configuration;
using CrystalTerror.Gui.ConfigEntries;
using ImGui = Dalamud.Bindings.ImGui.ImGui;

public class ConfigWindow : Window, IDisposable
{
    private readonly CrystalTerrorPlugin plugin;
    private TitleBarButton lockButton;
    internal ConfigFileSystem FileSystem;

    private readonly ConfigFileSystemEntry[] ConfigTabs =
    [
        new GeneralSettings(),
        new DisplayFilters(),
        new CharacterSorting(),
        new WarningThresholds(),
        new AutomaticVenture(),
#if DEBUG
        new DataManagement(),
#endif
    ];

    public ConfigWindow(CrystalTerrorPlugin plugin)
        : base("CrystalTerror Configuration")
    {
        this.plugin = plugin;
        this.SizeConstraints = new WindowSizeConstraints()
        {
            MinimumSize = new System.Numerics.Vector2(600, 400),
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

        // Initialize config file system
        FileSystem = new(() => ConfigTabs);
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

            ConfigHelper.Save(this.plugin.Config);
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
        FileSystem.Draw(null);
    }

    public override void OnClose()
    {
        ConfigHelper.Save(this.plugin.Config);
    }
}
