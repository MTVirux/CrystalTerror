using System;
using OtterGui;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin;

namespace CrystalTerror.Gui
{
    public class ConfigWindow : Window, IDisposable
    {
        private bool disposed;
        private readonly PluginConfig config;
        private readonly IDalamudPluginInterface pluginInterface;

        // (placeholder fields removed)

        public ConfigWindow(PluginConfig config, IDalamudPluginInterface pluginInterface)
            : base("CrystalTerror Config###CrystalTerrorConfigWindow")
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
            SizeConstraints = new WindowSizeConstraints()
            {
                MinimumSize = new System.Numerics.Vector2(300, 100),
                MaximumSize = new System.Numerics.Vector2(9999, 9999),
            };
        }

        public override void Draw()
        {
            ImGui.TextUnformatted("CrystalTerror Configuration");
            ImGui.Separator();

            ImGui.Spacing();
            // Edit mode button toggles a UI mode used for reordering characters in the main window.
            if (ImGui.Button(this.config.EditMode ? "Exit edit mode" : "Enter edit mode"))
            {
                this.config.EditMode = !this.config.EditMode;
                try
                {
                    this.pluginInterface.SavePluginConfig(this.config);
                }
                catch
                {
                    // ignore save errors
                }
            }
            ImGui.SameLine();
            ImGui.TextUnformatted(this.config.EditMode ? "Edit mode: ON" : "Edit mode: OFF");

            ImGui.Spacing();
            if (ImGui.Button("Save"))
            {
                try
                {
                    this.pluginInterface.SavePluginConfig(this.config);
                }
                catch
                {
                    // ignore save errors
                }
                this.IsOpen = false;
            }

            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
            {
                this.IsOpen = false;
            }

            ImGui.SameLine();
            if (ImGui.Button("Purge saved data"))
            {
                ImGui.OpenPopup("PurgeConfirm");
            }

            if (ImGui.BeginPopupModal("PurgeConfirm", ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.TextUnformatted("This will delete all stored characters and their data. This action cannot be undone.");
                ImGui.Spacing();
                if (ImGui.Button("Confirm Purge"))
                {
                    try
                    {
                        this.config.Characters?.Clear();
                        this.pluginInterface.SavePluginConfig(this.config);
                    }
                    catch
                    {
                        // ignore save errors
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

        public void Dispose()
        {
            if (this.disposed)
                return;

            this.disposed = true;
        }
    }
}
