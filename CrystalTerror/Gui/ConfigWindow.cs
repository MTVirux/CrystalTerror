namespace CrystalTerror.Gui;

using System;
using Dalamud.Interface.Windowing;
using ImGui = Dalamud.Bindings.ImGui.ImGui;

public class ConfigWindow : Window, IDisposable
{
    private readonly CrystalTerrorPlugin plugin;

    public ConfigWindow(CrystalTerrorPlugin plugin)
        : base("CrystalTerrorConfigWindow")
    {
        this.plugin = plugin;
        this.SizeConstraints = new WindowSizeConstraints()
        {
            MinimumSize = new System.Numerics.Vector2(300, 100),
            MaximumSize = ImGui.GetIO().DisplaySize,
        };
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        ImGui.Text("Crystal Terror - Configuration");
        ImGui.Separator();

        var cfg = this.plugin.Config;

        var show = cfg.ShowOnStart;
        if (ImGui.Checkbox("Show main window on start", ref show))
        {
            cfg.ShowOnStart = show;
            this.plugin.PluginInterface.SavePluginConfig(cfg);
        }

        ImGui.Spacing();
        if (ImGui.Button("Save"))
        {
            this.plugin.PluginInterface.SavePluginConfig(cfg);
        }

        ImGui.SameLine();
        if (ImGui.Button("Close"))
            this.IsOpen = false;
    }
}
