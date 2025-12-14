namespace CrystalTerror.Gui.MainWindow;

using System;
using System.Collections.Generic;
using System.Linq;
using CrystalTerror.Gui.Common;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Bindings.ImGui;
using ImGui = Dalamud.Bindings.ImGui.ImGui;
using ECommons.ImGuiMethods;

/// <summary>
/// Component for rendering a single retainer entry with stats, job info, and inventory.
/// Uses ECommons ImGuiEx for consistent styling.
/// </summary>
public class RetainerEntryRow : IUIComponent
{
    private readonly CrystalTerrorPlugin plugin;
    private readonly CrystalCountsUtility countsUtility;
    private readonly Retainer retainer;
    private readonly Dalamud.Plugin.Services.ITextureProvider textureProvider;

    public RetainerEntryRow(
        CrystalTerrorPlugin plugin, 
        CrystalCountsUtility countsUtility, 
        Retainer retainer,
        Dalamud.Plugin.Services.ITextureProvider textureProvider)
    {
        this.plugin = plugin;
        this.countsUtility = countsUtility;
        this.retainer = retainer;
        this.textureProvider = textureProvider;
    }

    public void Render()
    {
        var headerText = BuildRetainerHeaderText();
        var headerId = $"{headerText}##{retainer.Atid}_{retainer.Name}";

        // Style based on ignored state
        int colorsPushed = 0;
        if (retainer.IsIgnored)
        {
            ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.25f, 0.25f, 0.25f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0.3f, 0.3f, 0.3f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(0.35f, 0.35f, 0.35f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.5f, 1.0f));
            colorsPushed = 4;
        }

        // Force collapsed for ignored retainers
        if (retainer.IsIgnored)
        {
            ImGui.SetNextItemOpen(false, ImGuiCond.Always);
        }

        ImGuiEx.TreeNodeCollapsingHeader(headerId, () =>
        {
            RenderRetainerContent();
        });

        // Context menu (needs to be outside the TreeNodeCollapsingHeader for proper popup handling)
        if (ImGui.BeginPopupContextItem($"ret_ctx_{retainer.Atid}_{retainer.Name}"))
        {
            RenderRetainerContextMenu();
            ImGui.EndPopup();
        }

        if (colorsPushed > 0)
        {
            ImGui.PopStyleColor(colorsPushed);
        }
    }

    private string BuildRetainerHeaderText()
    {
        var parts = new List<string>();
        
        // Name
        parts.Add(retainer.Name);

        // Job abbreviation
        if (retainer.Job.HasValue)
        {
            parts.Add($"[{ClassJobExtensions.GetAbbreviation(retainer.Job)}]");
        }

        // Level
        if (retainer.Level > 0)
        {
            parts.Add($"Lv{retainer.Level}");
        }

        // iLvl for combat, Gathering for gatherers
        if (IsGatheringRetainer())
        {
            if (retainer.Gathering > 0)
            {
                parts.Add($"G:{retainer.Gathering}");
            }
        }
        else if (retainer.Ilvl > 0)
        {
            parts.Add($"iL{retainer.Ilvl}");
        }

        // Auto venture indicator
        if (retainer.EnableAutoVenture)
        {
            parts.Add("[AV]");
        }

        // Crystal totals if configured
        if (plugin.Config.ShowTotalsInHeaders)
        {
            var totalParts = BuildCrystalTotalParts();
            if (totalParts.Count > 0)
            {
                parts.Add($"({string.Join(" ", totalParts)})");
            }
        }

        return string.Join(" ", parts);
    }

    private List<string> BuildCrystalTotalParts()
    {
        var result = new List<string>();
        var elements = Enum.GetValues<Element>().Where(e => countsUtility.IsElementVisible(e));

        foreach (var element in elements)
        {
            var shard = retainer.Inventory?.GetCount(element, CrystalType.Shard) ?? 0;
            var crystal = retainer.Inventory?.GetCount(element, CrystalType.Crystal) ?? 0;
            var cluster = retainer.Inventory?.GetCount(element, CrystalType.Cluster) ?? 0;

            var counts = new List<long>();
            if (plugin.Config.ShowShards) counts.Add(shard);
            if (plugin.Config.ShowCrystals) counts.Add(crystal);
            if (plugin.Config.ShowClusters) counts.Add(cluster);

            if (counts.Count > 0)
            {
                var formatted = string.Join("/", counts.Select(c => countsUtility.FormatNumber(c, plugin.Config.UseReducedNotationInHeaders)));
                var elName = plugin.Config.UseAbbreviatedElementNames 
                    ? element.ToString().Substring(0, 2) 
                    : element.ToString();
                
                if (plugin.Config.ShowElementNamesInTotals)
                    result.Add($"{elName}:{formatted}");
                else
                    result.Add(formatted);
            }
        }

        return result;
    }

    private void RenderRetainerContent()
    {
        // Stats row
        RenderStatsRow();
        
        ImGui.Spacing();

        // Inventory table
        RenderInventoryTable();
    }

    private void RenderStatsRow()
    {
        ImGui.BeginGroup();
        
        // Job icon and name
        if (retainer.Job.HasValue)
        {
            var classJob = ClassJobExtensions.GetClassJob(retainer.Job);
            var jobDisplay = classJob != null ? $"{classJob.Abbreviation} ({classJob.Name})" : "Unknown";
            ImGui.Text($"Job: {jobDisplay}");
        }
        else
        {
            ImGui.TextDisabled("Job: Unknown");
        }

        ImGui.SameLine(150f * ImGuiHelpers.GlobalScale);
        ImGui.Text($"Level: {retainer.Level}");

        ImGui.SameLine(250f * ImGuiHelpers.GlobalScale);
        
        if (IsGatheringRetainer())
        {
            ImGui.Text($"Gathering: {retainer.Gathering}");
            ImGui.SameLine(380f * ImGuiHelpers.GlobalScale);
            ImGui.Text($"Perception: {retainer.Perception}");
        }
        else
        {
            ImGui.Text($"iLevel: {retainer.Ilvl}");
        }

        ImGui.EndGroup();

        // Auto Venture toggle
        ImGui.Spacing();
        var enableAutoVenture = retainer.EnableAutoVenture;
        if (ImGui.Checkbox($"Enable Auto Venture##{retainer.Atid}", ref enableAutoVenture))
        {
            retainer.EnableAutoVenture = enableAutoVenture;
            ConfigHelper.SaveAndSync(plugin.Config, plugin.Characters);
        }
        
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("When enabled, CrystalTerror will automatically assign crystal ventures for this retainer.");
        }
    }

    private void RenderInventoryTable()
    {
        if (retainer.Inventory == null)
        {
            ImGui.TextDisabled("No inventory data");
            return;
        }

        var visibleElements = Enum.GetValues<Element>().Where(e => countsUtility.IsElementVisible(e)).ToArray();
        if (visibleElements.Length == 0)
        {
            ImGui.TextDisabled("No elements visible");
            return;
        }

        var columnCount = 1 + visibleElements.Length;
        if (!ImGui.BeginTable($"ret_inv_{retainer.Atid}", columnCount, 
            ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))
            return;

        // Setup columns
        ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 60f);
        foreach (var element in visibleElements)
        {
            ImGui.TableSetupColumn(element.ToString(), ImGuiTableColumnFlags.WidthFixed, 70f);
        }
        ImGui.TableHeadersRow();

        // Render rows
        var crystalTypes = new[] { CrystalType.Shard, CrystalType.Crystal, CrystalType.Cluster };
        var showTypes = new[] { plugin.Config.ShowShards, plugin.Config.ShowCrystals, plugin.Config.ShowClusters };

        for (int i = 0; i < crystalTypes.Length; i++)
        {
            if (!showTypes[i]) continue;

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(crystalTypes[i].ToString());

            foreach (var element in visibleElements)
            {
                ImGui.TableNextColumn();
                var count = retainer.Inventory.GetCount(element, crystalTypes[i]);
                var color = countsUtility.GetCrystalWarningColor(count);
                var text = countsUtility.FormatNumber(count, plugin.Config.UseReducedNotationInTables);
                
                if (color.HasValue)
                    ImGui.TextColored(color.Value, text);
                else
                    ImGui.TextUnformatted(text);

                // Tooltip with full number if using reduced notation
                if (plugin.Config.UseReducedNotationInTables && plugin.Config.ShowFullNumbersOnHoverInTables && ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(count.ToString("N0"));
                }
            }
        }

        ImGui.EndTable();
    }

    private void RenderRetainerContextMenu()
    {
        if (ImGui.MenuItem(retainer.IsIgnored ? "Unignore Retainer" : "Ignore Retainer"))
        {
            retainer.IsIgnored = !retainer.IsIgnored;
            ConfigHelper.SaveAndSync(plugin.Config, plugin.Characters);
        }

        ImGui.Separator();

        var enableAutoVenture = retainer.EnableAutoVenture;
        if (ImGui.MenuItem("Enable Auto Venture", "", ref enableAutoVenture))
        {
            retainer.EnableAutoVenture = enableAutoVenture;
            ConfigHelper.SaveAndSync(plugin.Config, plugin.Characters);
        }

        ImGui.Separator();

        if (ImGui.MenuItem("Delete Retainer"))
        {
            retainer.OwnerCharacter?.Retainers?.Remove(retainer);
            ConfigHelper.SaveAndSync(plugin.Config, plugin.Characters);
        }
    }

    private bool IsGatheringRetainer()
    {
        // MIN=16, BTN=17, FSH=18
        return retainer.Job == 16 || retainer.Job == 17 || retainer.Job == 18;
    }
}
