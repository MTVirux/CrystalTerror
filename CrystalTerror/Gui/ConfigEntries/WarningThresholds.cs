namespace CrystalTerror.Gui.ConfigEntries;

using NightmareUI.PrimaryUI;
using System.Numerics;
using ImGui = Dalamud.Bindings.ImGui.ImGui;
using Dalamud.Bindings.ImGui;

public class WarningThresholds : ConfigEntry
{
    public override string Path => "Warning Thresholds";

    public override NuiBuilder Builder { get; init; } = null!;

    public WarningThresholds()
    {
        Builder = new NuiBuilder()
            .Section("Retainer Crystal Warning Thresholds")
            .TextWrapped("Configure up to 3 color-coded warning thresholds for crystal counts in the main window.")

            // Threshold 1
            .Widget("Enable Warning Threshold 1", (x) =>
            {
                var val = Plugin.Config.RetainerCrystalThreshold1Enabled;
                if (ImGui.Checkbox(x, ref val))
                {
                    Plugin.Config.RetainerCrystalThreshold1Enabled = val;
                    Plugin.PluginInterface.SavePluginConfig(Plugin.Config);
                }
            })
            .If(() => Plugin.Config.RetainerCrystalThreshold1Enabled)
            .Indent()
            .Widget(() =>
            {
                ImGui.Text("Threshold Value:");
                ImGui.SameLine();
                var val = Plugin.Config.RetainerCrystalThreshold1Value;
                ImGui.SetNextItemWidth(250);
                if (ImGui.SliderInt("##threshold1value", ref val, 1, 9999))
                {
                    Plugin.Config.RetainerCrystalThreshold1Value = val;
                    Plugin.PluginInterface.SavePluginConfig(Plugin.Config);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Values at or above this threshold will be colored (1-9999)");
                }
            })
            .Widget(() =>
            {
                ImGui.Text("Warning Color:");
                ImGui.SameLine();
                var color = Plugin.Config.RetainerCrystalThreshold1Color;
                if (ImGui.ColorEdit4("##retainerThreshold1Color", ref color, ImGuiColorEditFlags.NoInputs))
                {
                    Plugin.Config.RetainerCrystalThreshold1Color = color;
                    Plugin.PluginInterface.SavePluginConfig(Plugin.Config);
                }
            })
            .Unindent()
            .EndIf()

            // Threshold 2
            .Widget("Enable Warning Threshold 2", (x) =>
            {
                var val = Plugin.Config.RetainerCrystalThreshold2Enabled;
                if (ImGui.Checkbox(x, ref val))
                {
                    Plugin.Config.RetainerCrystalThreshold2Enabled = val;
                    Plugin.PluginInterface.SavePluginConfig(Plugin.Config);
                }
            })
            .If(() => Plugin.Config.RetainerCrystalThreshold2Enabled)
            .Indent()
            .Widget(() =>
            {
                ImGui.Text("Threshold Value:");
                ImGui.SameLine();
                var val = Plugin.Config.RetainerCrystalThreshold2Value;
                ImGui.SetNextItemWidth(250);
                if (ImGui.SliderInt("##threshold2value", ref val, 1, 9999))
                {
                    Plugin.Config.RetainerCrystalThreshold2Value = val;
                    Plugin.PluginInterface.SavePluginConfig(Plugin.Config);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Values at or above this threshold will be colored (1-9999)");
                }
            })
            .Widget(() =>
            {
                ImGui.Text("Warning Color:");
                ImGui.SameLine();
                var color = Plugin.Config.RetainerCrystalThreshold2Color;
                if (ImGui.ColorEdit4("##retainerThreshold2Color", ref color, ImGuiColorEditFlags.NoInputs))
                {
                    Plugin.Config.RetainerCrystalThreshold2Color = color;
                    Plugin.PluginInterface.SavePluginConfig(Plugin.Config);
                }
            })
            .Unindent()
            .EndIf()

            // Threshold 3
            .Widget("Enable Warning Threshold 3", (x) =>
            {
                var val = Plugin.Config.RetainerCrystalThreshold3Enabled;
                if (ImGui.Checkbox(x, ref val))
                {
                    Plugin.Config.RetainerCrystalThreshold3Enabled = val;
                    Plugin.PluginInterface.SavePluginConfig(Plugin.Config);
                }
            })
            .If(() => Plugin.Config.RetainerCrystalThreshold3Enabled)
            .Indent()
            .Widget(() =>
            {
                ImGui.Text("Threshold Value:");
                ImGui.SameLine();
                var val = Plugin.Config.RetainerCrystalThreshold3Value;
                ImGui.SetNextItemWidth(250);
                if (ImGui.SliderInt("##threshold3value", ref val, 1, 9999))
                {
                    Plugin.Config.RetainerCrystalThreshold3Value = val;
                    Plugin.PluginInterface.SavePluginConfig(Plugin.Config);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Values at or above this threshold will be colored (1-9999)");
                }
            })
            .Widget(() =>
            {
                ImGui.Text("Warning Color:");
                ImGui.SameLine();
                var color = Plugin.Config.RetainerCrystalThreshold3Color;
                if (ImGui.ColorEdit4("##retainerThreshold3Color", ref color, ImGuiColorEditFlags.NoInputs))
                {
                    Plugin.Config.RetainerCrystalThreshold3Color = color;
                    Plugin.PluginInterface.SavePluginConfig(Plugin.Config);
                }
            })
            .Unindent()
            .EndIf()

            .Section("Character Total Warning Thresholds")
            .TextWrapped("Configure up to 3 color-coded warning thresholds for character total crystal counts (character + all retainers).")

            // Character Threshold 1
            .Widget("Enable Character Total Threshold 1", (x) =>
            {
                var val = Plugin.Config.CharacterTotalThreshold1Enabled;
                if (ImGui.Checkbox(x, ref val))
                {
                    Plugin.Config.CharacterTotalThreshold1Enabled = val;
                    Plugin.PluginInterface.SavePluginConfig(Plugin.Config);
                }
            })
            .If(() => Plugin.Config.CharacterTotalThreshold1Enabled)
            .Indent()
            .Widget(() =>
            {
                ImGui.Text("Threshold Value:");
                ImGui.SameLine();
                var val = Plugin.Config.CharacterTotalThreshold1Value;
                ImGui.SetNextItemWidth(250);
                if (ImGui.SliderInt("##charThreshold1value", ref val, 1, 599940))
                {
                    Plugin.Config.CharacterTotalThreshold1Value = val;
                    Plugin.PluginInterface.SavePluginConfig(Plugin.Config);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Character totals at or above this threshold will be colored (1-599940)");
                }
            })
            .Widget(() =>
            {
                ImGui.Text("Warning Color:");
                ImGui.SameLine();
                var color = Plugin.Config.CharacterTotalThreshold1Color;
                if (ImGui.ColorEdit4("##charTotalThreshold1Color", ref color, ImGuiColorEditFlags.NoInputs))
                {
                    Plugin.Config.CharacterTotalThreshold1Color = color;
                    Plugin.PluginInterface.SavePluginConfig(Plugin.Config);
                }
            })
            .Unindent()
            .EndIf()

            // Character Threshold 2
            .Widget("Enable Character Total Threshold 2", (x) =>
            {
                var val = Plugin.Config.CharacterTotalThreshold2Enabled;
                if (ImGui.Checkbox(x, ref val))
                {
                    Plugin.Config.CharacterTotalThreshold2Enabled = val;
                    Plugin.PluginInterface.SavePluginConfig(Plugin.Config);
                }
            })
            .If(() => Plugin.Config.CharacterTotalThreshold2Enabled)
            .Indent()
            .Widget(() =>
            {
                ImGui.Text("Threshold Value:");
                ImGui.SameLine();
                var val = Plugin.Config.CharacterTotalThreshold2Value;
                ImGui.SetNextItemWidth(250);
                if (ImGui.SliderInt("##charThreshold2value", ref val, 1, 599940))
                {
                    Plugin.Config.CharacterTotalThreshold2Value = val;
                    Plugin.PluginInterface.SavePluginConfig(Plugin.Config);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Character totals at or above this threshold will be colored (1-599940)");
                }
            })
            .Widget(() =>
            {
                ImGui.Text("Warning Color:");
                ImGui.SameLine();
                var color = Plugin.Config.CharacterTotalThreshold2Color;
                if (ImGui.ColorEdit4("##charTotalThreshold2Color", ref color, ImGuiColorEditFlags.NoInputs))
                {
                    Plugin.Config.CharacterTotalThreshold2Color = color;
                    Plugin.PluginInterface.SavePluginConfig(Plugin.Config);
                }
            })
            .Unindent()
            .EndIf()

            // Character Threshold 3
            .Widget("Enable Character Total Threshold 3", (x) =>
            {
                var val = Plugin.Config.CharacterTotalThreshold3Enabled;
                if (ImGui.Checkbox(x, ref val))
                {
                    Plugin.Config.CharacterTotalThreshold3Enabled = val;
                    Plugin.PluginInterface.SavePluginConfig(Plugin.Config);
                }
            })
            .If(() => Plugin.Config.CharacterTotalThreshold3Enabled)
            .Indent()
            .Widget(() =>
            {
                ImGui.Text("Threshold Value:");
                ImGui.SameLine();
                var val = Plugin.Config.CharacterTotalThreshold3Value;
                ImGui.SetNextItemWidth(250);
                if (ImGui.SliderInt("##charThreshold3value", ref val, 1, 599940))
                {
                    Plugin.Config.CharacterTotalThreshold3Value = val;
                    Plugin.PluginInterface.SavePluginConfig(Plugin.Config);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Character totals at or above this threshold will be colored (1-599940)");
                }
            })
            .Widget(() =>
            {
                ImGui.Text("Warning Color:");
                ImGui.SameLine();
                var color = Plugin.Config.CharacterTotalThreshold3Color;
                if (ImGui.ColorEdit4("##charTotalThreshold3Color", ref color, ImGuiColorEditFlags.NoInputs))
                {
                    Plugin.Config.CharacterTotalThreshold3Color = color;
                    Plugin.PluginInterface.SavePluginConfig(Plugin.Config);
                }
            })
            .Unindent()
            .EndIf();
    }
}
