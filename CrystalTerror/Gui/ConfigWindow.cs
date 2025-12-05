namespace CrystalTerror.Gui;

using System;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
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
        // Import / Purge controls moved to config window
        if (ImGui.Button("Import Current Character"))
        {
            var sc = CharacterHelper.ImportCurrentCharacter(this.plugin.PlayerState, this.plugin.Objects, this.plugin.DataManager);
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
            var list = CharacterHelper.ImportFromAutoRetainer(this.plugin.PluginInterface);
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
                    var sc = CharacterHelper.ImportCurrentCharacter(this.plugin.PlayerState, this.plugin.Objects, this.plugin.DataManager);
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

        ImGui.Spacing();
        // Configuration is saved immediately on change; no explicit Save button required.
        if (ImGui.Button("Close"))
            this.IsOpen = false;
    }
}
