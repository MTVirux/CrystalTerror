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
/// Main character list panel component that displays all characters and their retainers
/// in a style similar to AutoRetainer's retainer tab.
/// Uses ECommons ImGuiEx for consistent styling.
/// </summary>
public class CharacterListPanel : IUIComponent
{
    private readonly CrystalTerrorPlugin plugin;
    private readonly CrystalCountsUtility countsUtility;
    private readonly Dalamud.Plugin.Services.ITextureProvider textureProvider;
    private string filterText = string.Empty;

    public CharacterListPanel(CrystalTerrorPlugin plugin, CrystalCountsUtility countsUtility, Dalamud.Plugin.Services.ITextureProvider textureProvider)
    {
        this.plugin = plugin;
        this.countsUtility = countsUtility;
        this.textureProvider = textureProvider;
    }

    public void Render()
    {
        if (plugin.Characters.Count == 0)
        {
            RenderEmptyState();
            return;
        }

        RenderFilterBar();
        ImGui.Separator();
        RenderCharacterList();
    }

    private void RenderEmptyState()
    {
        ImGui.Spacing();
        ImGuiEx.TextWrapped("No characters imported yet. Log in to a character to import their data, or use AutoRetainer integration.");
        ImGui.Spacing();
        
        if (ImGui.Button("Import from AutoRetainer"))
        {
            try
            {
                var imported = CharacterHelper.ImportFromAutoRetainer();
                if (imported.Count > 0)
                {
                    CharacterHelper.MergeInto(plugin.Characters, imported, CharacterHelper.MergePolicy.Merge);
                    ConfigHelper.SaveAndSync(plugin.Config, plugin.Characters);
                }
            }
            catch (Exception ex)
            {
                Svc.Log.Error($"Failed to import from AutoRetainer: {ex.Message}");
            }
        }
    }

    private void RenderFilterBar()
    {
        ImGui.SetNextItemWidth(200f * ImGuiHelpers.GlobalScale);
        ImGui.InputTextWithHint("##filter", "Filter characters...", ref filterText, 100);
        
        ImGui.SameLine();
        
        // Refresh button
        if (ImGuiEx.IconButton(FontAwesomeIcon.Sync, "Refresh current character"))
        {
            try
            {
                var sc = CharacterHelper.ImportCurrentCharacter();
                if (sc != null)
                {
                    CharacterHelper.MergeInto(plugin.Characters, new[] { sc }, CharacterHelper.MergePolicy.Overwrite);
                    ConfigHelper.SaveAndSync(plugin.Config, plugin.Characters);
                }
            }
            catch { }
        }
        
        ImGui.SameLine();
        
        // Import from AutoRetainer
        if (ImGuiEx.IconButton(FontAwesomeIcon.Download, "Import from AutoRetainer"))
        {
            try
            {
                var imported = CharacterHelper.ImportFromAutoRetainer();
                if (imported.Count > 0)
                {
                    CharacterHelper.MergeInto(plugin.Characters, imported, CharacterHelper.MergePolicy.Merge);
                    ConfigHelper.SaveAndSync(plugin.Config, plugin.Characters);
                }
            }
            catch { }
        }

        // Show CTRL hint for showing ignored characters
        ImGui.SameLine();
        ImGui.TextDisabled("(Hold CTRL to show ignored)");
    }

    private void RenderCharacterList()
    {
        var characters = GetFilteredCharacters();
        var showIgnored = ImGui.GetIO().KeyCtrl;

        if (!showIgnored)
        {
            characters = characters.Where(c => !c.IsIgnored).ToList();
        }

        if (characters.Count == 0)
        {
            ImGui.TextDisabled("No characters match the filter.");
            return;
        }

        // Render each character as a collapsing section
        foreach (var character in characters)
        {
            RenderCharacterEntry(character, showIgnored);
        }
    }

    private void RenderCharacterEntry(StoredCharacter character, bool showIgnored)
    {
        var isCurrentCharacter = character.MatchesCID(Player.CID);
        var headerFlags = ImGuiTreeNodeFlags.None;
        
        // Style the header
        var headerColor = GetHeaderColor(character, isCurrentCharacter);
        var textColor = character.IsIgnored ? new Vector4(0.5f, 0.5f, 0.5f, 1.0f) : (Vector4?)null;

        // Push colors if needed
        int colorsPushed = 0;
        if (headerColor.HasValue)
        {
            ImGui.PushStyleColor(ImGuiCol.Header, headerColor.Value);
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, headerColor.Value * new Vector4(1.2f, 1.2f, 1.2f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.HeaderActive, headerColor.Value * new Vector4(1.4f, 1.4f, 1.4f, 1.0f));
            colorsPushed += 3;
        }
        if (textColor.HasValue)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, textColor.Value);
            colorsPushed++;
        }

        // Build header text with crystal totals
        var headerText = BuildCharacterHeaderText(character, showIgnored);
        var headerId = $"{headerText}##{character.UniqueKey}";

        // Force collapsed for ignored characters
        if (character.IsIgnored)
        {
            ImGui.SetNextItemOpen(false, ImGuiCond.Always);
        }

        var isOpen = ImGui.CollapsingHeader(headerId, headerFlags);

        // Pop colors
        if (colorsPushed > 0)
        {
            ImGui.PopStyleColor(colorsPushed);
        }

        // Context menu
        if (ImGui.BeginPopupContextItem($"ctx_{character.UniqueKey}"))
        {
            RenderCharacterContextMenu(character);
            ImGui.EndPopup();
        }

        // Render content if open
        if (isOpen)
        {
            ImGui.Indent();
            RenderCharacterContent(character, showIgnored);
            ImGui.Unindent();
        }
    }

    private string BuildCharacterHeaderText(StoredCharacter character, bool showIgnored)
    {
        var namePart = $"{character.Name} @ {character.World}";
        
        if (!plugin.Config.ShowTotalsInHeaders)
            return namePart;

        // Calculate totals
        var totals = CalculateCharacterTotals(character, showIgnored);
        var totalParts = new List<string>();

        foreach (var element in Enum.GetValues<Element>())
        {
            if (!countsUtility.IsElementVisible(element))
                continue;

            var (shard, crystal, cluster) = totals[element];
            var elementTotal = new List<string>();
            
            if (plugin.Config.ShowShards) elementTotal.Add(countsUtility.FormatNumber(shard, plugin.Config.UseReducedNotationInHeaders));
            if (plugin.Config.ShowCrystals) elementTotal.Add(countsUtility.FormatNumber(crystal, plugin.Config.UseReducedNotationInHeaders));
            if (plugin.Config.ShowClusters) elementTotal.Add(countsUtility.FormatNumber(cluster, plugin.Config.UseReducedNotationInHeaders));

            if (elementTotal.Count > 0)
            {
                var elementStr = string.Join("/", elementTotal);
                if (plugin.Config.ShowElementNamesInTotals)
                {
                    var elName = plugin.Config.UseAbbreviatedElementNames 
                        ? element.ToString().Substring(0, 2) 
                        : element.ToString();
                    totalParts.Add($"{elName}: {elementStr}");
                }
                else
                {
                    totalParts.Add(elementStr);
                }
            }
        }

        return totalParts.Count > 0 
            ? $"{namePart}  [{string.Join("  ", totalParts)}]"
            : namePart;
    }

    private Dictionary<Element, (long shard, long crystal, long cluster)> CalculateCharacterTotals(StoredCharacter character, bool showIgnored)
    {
        var result = new Dictionary<Element, (long shard, long crystal, long cluster)>();

        foreach (var element in Enum.GetValues<Element>())
        {
            long shard = character.Inventory?.GetCount(element, CrystalType.Shard) ?? 0;
            long crystal = character.Inventory?.GetCount(element, CrystalType.Crystal) ?? 0;
            long cluster = character.Inventory?.GetCount(element, CrystalType.Cluster) ?? 0;

            if (character.Retainers != null)
            {
                var retainers = showIgnored ? character.Retainers : character.Retainers.Where(r => !r.IsIgnored);
                foreach (var r in retainers)
                {
                    shard += r.Inventory?.GetCount(element, CrystalType.Shard) ?? 0;
                    crystal += r.Inventory?.GetCount(element, CrystalType.Crystal) ?? 0;
                    cluster += r.Inventory?.GetCount(element, CrystalType.Cluster) ?? 0;
                }
            }

            result[element] = (shard, crystal, cluster);
        }

        return result;
    }

    private Vector4? GetHeaderColor(StoredCharacter character, bool isCurrentCharacter)
    {
        if (character.IsIgnored)
            return new Vector4(0.3f, 0.3f, 0.3f, 1.0f);

        if (isCurrentCharacter && plugin.Config.ColorCurrentCharacter)
            return plugin.Config.CurrentCharacterColor;

        // Check character total thresholds
        var totals = CalculateCharacterTotals(character, false);
        long grandTotal = 0;
        foreach (var element in Enum.GetValues<Element>())
        {
            var (shard, crystal, cluster) = totals[element];
            if (plugin.Config.ShowShards) grandTotal += shard;
            if (plugin.Config.ShowCrystals) grandTotal += crystal;
            if (plugin.Config.ShowClusters) grandTotal += cluster;
        }

        var thresholds = new List<(int value, Vector4 color)>();
        if (plugin.Config.CharacterTotalThreshold1Enabled)
            thresholds.Add((plugin.Config.CharacterTotalThreshold1Value, plugin.Config.CharacterTotalThreshold1Color));
        if (plugin.Config.CharacterTotalThreshold2Enabled)
            thresholds.Add((plugin.Config.CharacterTotalThreshold2Value, plugin.Config.CharacterTotalThreshold2Color));
        if (plugin.Config.CharacterTotalThreshold3Enabled)
            thresholds.Add((plugin.Config.CharacterTotalThreshold3Value, plugin.Config.CharacterTotalThreshold3Color));

        thresholds.Sort((a, b) => a.value.CompareTo(b.value));

        Vector4? result = null;
        foreach (var threshold in thresholds)
        {
            if (grandTotal >= threshold.value)
                result = threshold.color;
        }

        return result;
    }

    private void RenderCharacterContextMenu(StoredCharacter character)
    {
        if (ImGui.MenuItem(character.IsIgnored ? "Unignore Character" : "Ignore Character"))
        {
            character.IsIgnored = !character.IsIgnored;
            ConfigHelper.SaveAndSync(plugin.Config, plugin.Characters);
        }

        if (ImGui.MenuItem("Delete Character"))
        {
            plugin.Characters.Remove(character);
            ConfigHelper.SaveAndSync(plugin.Config, plugin.Characters);
        }

        ImGui.Separator();

        if (ImGui.MenuItem("Refresh Character Data") && character.MatchesCID(Player.CID))
        {
            var sc = CharacterHelper.ImportCurrentCharacter();
            if (sc != null)
            {
                CharacterHelper.MergeInto(plugin.Characters, new[] { sc }, CharacterHelper.MergePolicy.Overwrite);
                ConfigHelper.SaveAndSync(plugin.Config, plugin.Characters);
            }
        }
    }

    private void RenderCharacterContent(StoredCharacter character, bool showIgnored)
    {
        // Character's own inventory
        ImGuiEx.TreeNodeCollapsingHeader($"Character Inventory##{character.UniqueKey}", () =>
        {
            RenderInventoryTable(character.Inventory, $"char_inv_{character.UniqueKey}");
        });

        // Retainers
        if (character.Retainers != null && character.Retainers.Count > 0)
        {
            var retainers = showIgnored ? character.Retainers : character.Retainers.Where(r => !r.IsIgnored).ToList();
            
            foreach (var retainer in retainers)
            {
                var retainerRow = new RetainerEntryRow(plugin, countsUtility, retainer, textureProvider);
                retainerRow.Render();
            }
        }
        else
        {
            ImGui.TextDisabled("No retainers");
        }
    }

    private void RenderInventoryTable(Inventory? inventory, string tableId)
    {
        if (inventory == null)
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

        // Create table with columns for each visible element
        var columnCount = 1 + visibleElements.Length; // Type column + element columns
        if (!ImGui.BeginTable(tableId, columnCount, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))
            return;

        // Setup columns
        ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 60f);
        foreach (var element in visibleElements)
        {
            ImGui.TableSetupColumn(element.ToString(), ImGuiTableColumnFlags.WidthFixed, 70f);
        }
        ImGui.TableHeadersRow();

        // Render rows for each crystal type
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
                var count = inventory.GetCount(element, crystalTypes[i]);
                var color = countsUtility.GetCrystalWarningColor(count);
                var text = countsUtility.FormatNumber(count, plugin.Config.UseReducedNotationInTables);
                
                if (color.HasValue)
                    ImGui.TextColored(color.Value, text);
                else
                    ImGui.TextUnformatted(text);
            }
        }

        ImGui.EndTable();
    }

    private List<StoredCharacter> GetFilteredCharacters()
    {
        var characters = plugin.Characters.ToList();

        // Apply text filter
        if (!string.IsNullOrWhiteSpace(filterText))
        {
            var filter = filterText.Trim().ToLowerInvariant();
            characters = characters.Where(c =>
                c.Name?.ToLowerInvariant().Contains(filter) == true ||
                c.World?.ToLowerInvariant().Contains(filter) == true
            ).ToList();
        }

        // Apply sort
        characters = ApplySorting(characters);

        // Show current character at top if configured
        if (plugin.Config.ShowCurrentCharacterAtTop && Player.Available && Player.CID != 0)
        {
            var current = characters.FirstOrDefault(c => c.MatchesCID(Player.CID));
            if (current != null)
            {
                characters.Remove(current);
                characters.Insert(0, current);
            }
        }

        return characters;
    }

    private List<StoredCharacter> ApplySorting(List<StoredCharacter> characters)
    {
        return plugin.Config.CharacterSortOption switch
        {
            CharacterSortOptions.Alphabetical => characters.OrderBy(c => c.Name).ToList(),
            CharacterSortOptions.ReverseAlphabetical => characters.OrderByDescending(c => c.Name).ToList(),
            CharacterSortOptions.World => characters.OrderBy(c => c.World).ThenBy(c => c.Name).ToList(),
            CharacterSortOptions.ReverseWorld => characters.OrderByDescending(c => c.World).ThenByDescending(c => c.Name).ToList(),
            _ => characters
        };
    }
}
