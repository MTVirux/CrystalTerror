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

    private static readonly string[] PriorityOptions = { "Balanced", "Prefer Crystals", "Prefer Shards" };

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
                    ImGui.SetTooltip("Automatically assign ventures to retainers based on global crystal/shard counts.\nCalculates totals across character + all retainers to maximize storage utilization.");
                }
            })

            .If(() => Plugin.Config.AutoVentureEnabled)
            .Indent()
            
            // === Crystal Type Toggles ===
            .TextWrapped("Crystal Types:")
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
                    ImGui.SetTooltip("Consider crystal counts when determining which venture to assign.\nRequires retainer Level > 26 and Gathering > 90.");
                }
            })
            .Widget("Include Fisher Retainers", (x) =>
            {
                var val = Plugin.Config.AutoVentureFSHEnabled;
                if (ImGui.Checkbox(x, ref val))
                {
                    Plugin.Config.AutoVentureFSHEnabled = val;
                    ConfigHelper.Save(Plugin.Config);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Include Fisher (FSH) retainers in automatic venture assignment.\nIf disabled, FSH retainers are skipped entirely.");
                }
            })

            .Widget(() =>
            {
                if (!Plugin.Config.AutoVentureShardsEnabled && !Plugin.Config.AutoVentureCrystalsEnabled && Plugin.Config.AutoVentureEnabled)
                {
                    ImGui.TextColored(new Vector4(1.0f, 0.5f, 0.0f, 1.0f), "Warning: At least one crystal type must be enabled.");
                }
            })

            // === Priority Settings ===
            .TextWrapped("Priority Settings:")
            .Widget(() =>
            {
                var currentIndex = (int)Plugin.Config.AutoVenturePriority;
                ImGui.SetNextItemWidth(200);
                if (ImGui.Combo("Tiebreaker Priority", ref currentIndex, PriorityOptions, PriorityOptions.Length))
                {
                    Plugin.Config.AutoVenturePriority = (VenturePriority)currentIndex;
                    ConfigHelper.Save(Plugin.Config);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("When multiple crystal/shard types have the same count, which to prioritize.\n" +
                        "• Balanced: No preference (uses element order)\n" +
                        "• Prefer Crystals: Assign crystal ventures first\n" +
                        "• Prefer Shards: Assign shard ventures first");
                }
            })

            // === Venture Reward Settings ===
            .TextWrapped("Capacity Calculation:")
            .Widget(() =>
            {
                var val = Plugin.Config.AutoVentureRewardAmount;
                ImGui.SetNextItemWidth(200);
                if (ImGui.InputInt("Expected Reward per Venture", ref val))
                {
                    if (val < 1) val = 1;
                    Plugin.Config.AutoVentureRewardAmount = val;
                    ConfigHelper.Save(Plugin.Config);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Expected number of crystals/shards per venture.\nUsed to estimate pending rewards from active ventures.\nDefault: 120 (standard 1-hour gathering venture yield).");
                }
            })

            // === Global Threshold ===
            .TextWrapped("Threshold Settings:")
            .Widget(() =>
            {
                var val = (int)Plugin.Config.AutoVentureThreshold;
                ImGui.SetNextItemWidth(200);
                if (ImGui.InputInt("Global Threshold", ref val))
                {
                    if (val < 0) val = 0;
                    Plugin.Config.AutoVentureThreshold = val;
                    ConfigHelper.Save(Plugin.Config);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Global threshold for all crystal/shard types.\n" +
                        "When all enabled types reach this count, assign Quick Exploration instead.\n" +
                        "Set to 0 to disable global threshold (fill to capacity).\n" +
                        "Per-type thresholds override this value when set.");
                }
            })

            // === Per-Type Settings (Collapsible) ===
            .Widget(() =>
            {
                if (ImGui.CollapsingHeader("Per-Type Settings"))
                {
                    ImGui.Indent();
                    DrawPerTypeSettingsImpl();
                    ImGui.Unindent();
                }
            })

            .Unindent()
            .EndIf();
    }

    private void DrawPerTypeSettingsImpl()
    {
        // Only show types that are globally enabled
        var showShards = Plugin.Config.AutoVentureShardsEnabled;
        var showCrystals = Plugin.Config.AutoVentureCrystalsEnabled;

        if (!showShards && !showCrystals)
        {
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1.0f), "Enable shards or crystals above to configure per-type settings.");
            return;
        }

        ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.3f, 1.0f), "These thresholds apply to all characters.");
        ImGui.Spacing();

        // Draw table header
        if (ImGui.BeginTable("PerTypeSettings", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))
        {
            ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 120);
            ImGui.TableSetupColumn("Enabled", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("Threshold", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableHeadersRow();

            foreach (var element in VentureCapacityCalculator.AllElements)
            {
                foreach (var type in VentureCapacityCalculator.VentureTypes)
                {
                    // Skip types not enabled globally
                    if (type == CrystalType.Shard && !showShards) continue;
                    if (type == CrystalType.Crystal && !showCrystals) continue;

                    var setting = Plugin.Config.GetPerTypeSetting(element, type);
                    var typeName = $"{element} {type}";

                    ImGui.TableNextRow();

                    // Type name
                    ImGui.TableNextColumn();
                    ImGui.Text(typeName);

                    // Enabled checkbox
                    ImGui.TableNextColumn();
                    var enabled = setting.Enabled;
                    if (ImGui.Checkbox($"##Enabled_{element}_{type}", ref enabled))
                    {
                        setting.Enabled = enabled;
                        ConfigHelper.Save(Plugin.Config);
                    }

                    // Threshold input
                    ImGui.TableNextColumn();
                    var threshold = (int)setting.Threshold;
                    ImGui.SetNextItemWidth(80);
                    if (ImGui.InputInt($"##Threshold_{element}_{type}", ref threshold))
                    {
                        if (threshold < 0) threshold = 0;
                        setting.Threshold = threshold;
                        ConfigHelper.Save(Plugin.Config);
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip($"Threshold for {typeName}.\n0 = use global threshold or fill to capacity.");
                    }
                }
            }

            ImGui.EndTable();
        }
    }
}
