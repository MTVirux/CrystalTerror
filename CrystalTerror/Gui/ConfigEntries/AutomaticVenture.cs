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
    private static readonly string[] FallbackModeOptions = { "Assign Specific Venture", "Skip (Let AutoRetainer Decide)" };
    
    // Cached ventures organized by category
    private Dictionary<VentureListHelper.VentureCategory, List<VentureListHelper.VentureInfo>>? _venturesByCategory;
    private string _ventureSearchFilter = "";

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

            // === Venture Credit Check ===
            .TextWrapped("Venture Credit Management:")
            .Widget("Enable Venture Credit Check", (x) =>
            {
                var val = Plugin.Config.AutoVentureCreditCheckEnabled;
                if (ImGui.Checkbox(x, ref val))
                {
                    Plugin.Config.AutoVentureCreditCheckEnabled = val;
                    ConfigHelper.Save(Plugin.Config);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("When enabled, checks venture credit count before assigning crystal ventures.\n" +
                        "If credits are below threshold, Quick Exploration is assigned instead.\n" +
                        "Quick Exploration rewards venture credits, helping to replenish your supply.");
                }
            })
            .If(() => Plugin.Config.AutoVentureCreditCheckEnabled)
            .Widget(() =>
            {
                var val = Plugin.Config.AutoVentureCreditThreshold;
                ImGui.SetNextItemWidth(200);
                if (ImGui.InputInt("Minimum Venture Credits", ref val))
                {
                    if (val < 1) val = 1;
                    Plugin.Config.AutoVentureCreditThreshold = val;
                    ConfigHelper.Save(Plugin.Config);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Minimum venture credits required to assign crystal/shard ventures.\n" +
                        "If your credit count falls below this, Quick Exploration is assigned instead.\n\n" +
                        "Recommended: Set to at least 2× your retainer count to ensure you always have\n" +
                        "enough credits for a full round of ventures.");
                }

                // Show current venture credit count
                var currentCredits = VentureCreditHelper.GetVentureCreditCount();
                var color = currentCredits >= Plugin.Config.AutoVentureCreditThreshold 
                    ? new Vector4(0.0f, 1.0f, 0.0f, 1.0f)  // Green
                    : new Vector4(1.0f, 0.5f, 0.0f, 1.0f); // Orange
                ImGui.TextColored(color, $"Current Venture Credits: {currentCredits}");
            })
            .EndIf()

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
                        "Enter the total count at which each type is considered \"full\".\n" +
                        "When all enabled types reach this count, the fallback venture is assigned.\n" +
                        "Set to 0 to disable global threshold (fill to capacity).\n\n" +
                        "Fill to capacity = 9,999 × (1 character + N retainers).\n" +
                        "Example: 1 character + 10 retainers = 109,989 max per type.\n\n" +
                        "Per-type thresholds override this value when set.");
                }
            })
            .Widget(() =>
            {
                var currentModeIndex = (int)Plugin.Config.AutoVentureFallbackMode;
                ImGui.SetNextItemWidth(200);
                if (ImGui.Combo("When Crystals Full", ref currentModeIndex, FallbackModeOptions, FallbackModeOptions.Length))
                {
                    Plugin.Config.AutoVentureFallbackMode = (FallbackVentureMode)currentModeIndex;
                    ConfigHelper.Save(Plugin.Config);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("What to do when all enabled crystal/shard types are above threshold.\n" +
                        "• Assign Specific Venture: Assign the selected venture below\n" +
                        "• Skip: Do not override, let AutoRetainer handle the venture assignment");
                }

                // Show venture selection if mode is SpecificVenture
                if (Plugin.Config.AutoVentureFallbackMode == FallbackVentureMode.SpecificVenture)
                {
                    DrawVentureSelector();
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
                        ImGui.SetTooltip($"Threshold for {typeName}.\n" +
                            "0 = use global threshold, or fill to capacity if global is also 0.\n\n" +
                            "Fill to capacity = 9,999 × (1 character + N retainers).");
                    }
                }
            }

            ImGui.EndTable();
        }
    }

    /// <summary>
    /// Draw the venture selector with categories similar to AutoRetainer's Venture Planner.
    /// </summary>
    private void DrawVentureSelector()
    {
        // Lazy load ventures by category
        if (_venturesByCategory == null)
        {
            _venturesByCategory = VentureListHelper.GetVenturesByCategory();
        }

        // Show current selection
        var currentVentureId = Plugin.Config.AutoVentureFallbackVentureId;
        var currentVentureName = VentureListHelper.GetVentureName(currentVentureId);
        
        if (!ImGui.CollapsingHeader($"Fallback Venture: {currentVentureName}##FallbackVentureHeader"))
        {
            return;
        }

        ImGui.Indent();

        // Search filter
        ImGui.SetNextItemWidth(200);
        ImGui.InputTextWithHint("##VentureSearch", "Filter ventures...", ref _ventureSearchFilter, 100);

        // Venture selection in a scrollable child region
        if (ImGui.BeginChild("##VentureList", new Vector2(0, 200), true))
        {
            // Define category order - Quick Exploration first, then Field, then gathering types
            var categoryOrder = new[]
            {
                VentureListHelper.VentureCategory.QuickExploration,
                VentureListHelper.VentureCategory.FieldExploration,
                VentureListHelper.VentureCategory.Mining,
                VentureListHelper.VentureCategory.Botany,
                VentureListHelper.VentureCategory.Fishing,
                VentureListHelper.VentureCategory.Hunting,
            };

            foreach (var category in categoryOrder)
            {
                if (!_venturesByCategory.TryGetValue(category, out var ventures) || ventures.Count == 0)
                    continue;

                // Filter ventures by search
                var filteredVentures = string.IsNullOrEmpty(_ventureSearchFilter)
                    ? ventures
                    : ventures.Where(v => v.Name.Contains(_ventureSearchFilter, StringComparison.OrdinalIgnoreCase)).ToList();

                if (filteredVentures.Count == 0)
                    continue;

                var categoryName = VentureListHelper.GetCategoryDisplayName(category);
                
                if (ImGui.CollapsingHeader($"{categoryName} ({filteredVentures.Count})##Category_{category}"))
                {
                    ImGui.Indent();
                    foreach (var venture in filteredVentures)
                    {
                        var isSelected = venture.Id == currentVentureId;
                        var label = venture.Level > 0 
                            ? $"[Lv{venture.Level}] {venture.Name}##Venture_{venture.Id}"
                            : $"{venture.Name}##Venture_{venture.Id}";
                        
                        if (ImGui.Selectable(label, isSelected))
                        {
                            Plugin.Config.AutoVentureFallbackVentureId = venture.Id;
                            ConfigHelper.Save(Plugin.Config);
                        }
                        
                        if (ImGui.IsItemHovered())
                        {
                            var duration = venture.MaxTimeMinutes >= 60 
                                ? $"{venture.MaxTimeMinutes / 60}h" 
                                : $"{venture.MaxTimeMinutes}m";
                            ImGui.SetTooltip($"{venture.Name}\nID: {venture.Id}\nLevel: {venture.Level}\nDuration: {duration}");
                        }
                    }
                    ImGui.Unindent();
                }
            }
        }
        ImGui.EndChild();
        ImGui.Unindent();
    }
}
