namespace CrystalTerror.Gui.ConfigEntries;

using CrystalTerror.Helpers;
using NightmareUI.PrimaryUI;
using System.Numerics;
using ImGui = Dalamud.Bindings.ImGui.ImGui;
using Dalamud.Bindings.ImGui;

public class AutomaticVenture : ConfigEntry
{
    public override string Path => "Automatic Venture Assignment";

    public override NuiBuilder? Builder { get; init; }

    public AutomaticVenture()
    {
        Builder = new NuiBuilder()
            .Section("Automatic Venture Assignment")
            .Widget("Enable Automatic Venture Assignment", (x) =>
            {
                var val = Plugin.Config.AutoVentureEnabled;
                if (ImGui.Checkbox(x, ref val))
                {
                    Plugin.Config.AutoVentureEnabled = val;
                    ConfigHelper.Save(Plugin.Config);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Automatically assign ventures to retainers based on their lowest crystal/shard counts.");
                }
            })

            .If(() => Plugin.Config.AutoVentureEnabled)
            .Indent()
            .Widget("Include Shards", (x) =>
            {
                var val = Plugin.Config.AutoVentureShardsEnabled;
                if (ImGui.Checkbox(x, ref val))
                {
                    Plugin.Config.AutoVentureShardsEnabled = val;
                    ConfigHelper.Save(Plugin.Config);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Consider shard counts when determining which venture to assign.");
                }
            })
            .Widget("Include Crystals", (x) =>
            {
                var val = Plugin.Config.AutoVentureCrystalsEnabled;
                if (ImGui.Checkbox(x, ref val))
                {
                    Plugin.Config.AutoVentureCrystalsEnabled = val;
                    ConfigHelper.Save(Plugin.Config);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Consider crystal counts when determining which venture to assign.");
                }
            })
            .Unindent()

            .Widget(() =>
            {
                if (!Plugin.Config.AutoVentureShardsEnabled && !Plugin.Config.AutoVentureCrystalsEnabled && Plugin.Config.AutoVentureEnabled)
                {
                    ImGui.TextColored(new Vector4(1.0f, 0.5f, 0.0f, 1.0f), "Warning: At least one crystal type must be enabled.");
                }
            })

            .TextWrapped("Threshold Settings:")
            .Widget(() =>
            {
                var val = (int)Plugin.Config.AutoVentureThreshold;
                ImGui.SetNextItemWidth(200);
                if (ImGui.InputInt("Maximum Amount", ref val))
                {
                    if (val < 0) val = 0;
                    Plugin.Config.AutoVentureThreshold = val;
                    ConfigHelper.Save(Plugin.Config);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("If all enabled crystal/shard types are above this value, assign Quick Exploration instead.\nSet to 0 to always assign crystal/shard ventures.");
                }
            })
            .EndIf();
    }
}
