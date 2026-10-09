namespace CrystalTerror.Gui.ConfigEntries;

using CrystalTerror.Gui.Common;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using ECommons.ImGuiMethods;
using ImGui = Dalamud.Bindings.ImGui.ImGui;

public static class RetainerLevelingSection
{
    private const int MaxRetainerLevel = 100;
    private const int CombatJob = 0;
    private static readonly Vector4 WarningColor = new(1.0f, 0.5f, 0.0f, 1.0f);
    private static readonly string[] ActionOptions = { "Specific Venture", "Quick Exploration", "Crystal Logic", "Skip (Let AutoRetainer Decide)" };

    public static void Draw(Configuration config)
    {
        var enabled = config.AutoVentureLevelingEnabled;
        if (ImGui.Checkbox("Enable Retainer Leveling", ref enabled))
        {
            config.AutoVentureLevelingEnabled = enabled;
            ConfigHelper.Save(config);
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Pick ventures based on retainer level.\n" +
                "Tiers are checked from the lowest level up - the first tier at or above the retainer's level is used.\n" +
                "Retainers above every tier use the normal crystal logic.\n" +
                "For retainers inside a tier, the next venture is picked after the finished one is collected, using the new level.");
        }

        if (!config.AutoVentureLevelingEnabled)
            return;

        ImGui.TextWrapped("The first tier at or above a retainer's level is used. Retainers above every tier use the normal crystal logic. " +
            "Inside a tier, the next venture is picked after the finished one is collected, so level-ups count right away.");

        var tiers = config.AutoVentureLevelingTiers;
        int? removeIndex = null;
        var resort = false;

        if (tiers.Count > 0 && ImGui.BeginTable("LevelingTiers", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))
        {
            ImGui.TableSetupColumn("Level <=", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthFixed, 200);
            ImGui.TableSetupColumn("Ventures", ImGuiTableColumnFlags.WidthFixed, 380);
            ImGui.TableSetupColumn("##Remove", ImGuiTableColumnFlags.WidthFixed, 30);
            ImGui.TableHeadersRow();

            for (var i = 0; i < tiers.Count; i++)
            {
                var tier = tiers[i];
                var minLevel = i == 0 ? 1 : tiers[i - 1].MaxLevel + 1;
                ImGui.PushID(i);
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                var maxLevel = tier.MaxLevel;
                ImGui.SetNextItemWidth(70);
                if (ImGui.InputInt("##MaxLevel", ref maxLevel))
                {
                    tier.MaxLevel = Math.Clamp(maxLevel, 1, MaxRetainerLevel);
                    ConfigHelper.Save(config);
                }
                if (ImGui.IsItemDeactivatedAfterEdit())
                    resort = true;

                ImGui.TableNextColumn();
                var actionIndex = (int)tier.Action;
                ImGui.SetNextItemWidth(190);
                if (ImGui.Combo("##Action", ref actionIndex, ActionOptions, ActionOptions.Length))
                {
                    tier.Action = (LevelingAction)actionIndex;
                    ConfigHelper.Save(config);
                }

                ImGui.TableNextColumn();
                if (tier.Action == LevelingAction.SpecificVenture)
                {
                    DrawTierVenture(config, tier, "MIN", 16, minLevel);
                    DrawTierVenture(config, tier, "BTN", 17, minLevel);
                    if (config.AutoVentureFSHEnabled)
                        DrawTierVenture(config, tier, "FSH", 18, minLevel);
                    DrawTierVenture(config, tier, "Combat", CombatJob, minLevel);
                }
                else
                {
                    ImGui.TextDisabled("-");
                }

                ImGui.TableNextColumn();
                if (ImGuiEx.IconButton(FontAwesomeIcon.Trash, "##remove"))
                    removeIndex = i;

                ImGui.PopID();
            }

            ImGui.EndTable();
        }

        if (removeIndex.HasValue)
        {
            tiers.RemoveAt(removeIndex.Value);
            ConfigHelper.Save(config);
        }

        if (resort)
        {
            config.AutoVentureLevelingTiers = tiers.OrderBy(t => t.MaxLevel).ToList();
            ConfigHelper.Save(config);
        }

        if (ImGui.Button("Add Tier"))
        {
            var lastLevel = config.AutoVentureLevelingTiers.Count > 0 ? config.AutoVentureLevelingTiers.Max(t => t.MaxLevel) : 0;
            config.AutoVentureLevelingTiers.Add(new LevelingTier { MaxLevel = Math.Min(MaxRetainerLevel, lastLevel + 1) });
            ConfigHelper.Save(config);
        }

        if (config.AutoVentureLevelingTiers.GroupBy(t => t.MaxLevel).Any(g => g.Count() > 1))
            ImGui.TextColored(WarningColor, "Some tiers share the same level - only the first of them is used.");
    }

    private static void DrawTierVenture(Configuration config, LevelingTier tier, string label, int job, int minLevel)
    {
        var ventureId = tier.GetVentureId(job);
        var venture = VentureListHelper.GetVenture(ventureId);

        ImGui.SetNextItemWidth(220);
        // HeightLargest stops the popup from capping its height below the picker's search box + list.
        if (ImGui.BeginCombo($"{label}##Venture_{job}", venture?.Name ?? $"Unknown ({ventureId})", ImGuiComboFlags.HeightLargest))
        {
            if (VenturePicker.Draw($"Tier_{job}", job, ref ventureId, 350))
            {
                tier.SetVentureId(job, ventureId);
                ConfigHelper.Save(config);
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndCombo();
        }

        if (venture != null && venture.Level > minLevel)
        {
            ImGui.SameLine();
            ImGui.TextColored(WarningColor, $"Needs Lv{venture.Level}");
        }
    }
}
