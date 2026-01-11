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
using Dalamud.Interface.Colors;

/// <summary>
/// Main character list panel component that displays all characters and their retainers
/// in a style similar to AutoRetainer's retainer tab with collapsing headers and tables.
/// </summary>
public class CharacterListPanel : IUIComponent
{
    private readonly CrystalTerrorPlugin plugin;
    private readonly CrystalCountsUtility countsUtility;
    private readonly Dalamud.Plugin.Services.ITextureProvider textureProvider;
    private string filterText = string.Empty;
    private float statusTextWidth = 0f;

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
        // Search filter
        ImGui.SetNextItemWidth(200f * ImGuiHelpers.GlobalScale);
        ImGui.InputTextWithHint("##filter", "Filter characters...", ref filterText, 100);
        
        ImGui.SameLine();
        
        // Refresh button
        if (ImGuiEx.IconButton(FontAwesomeIcon.Sync, "##refresh"))
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
            catch (Exception ex)
            {
                Svc.Log.Warning($"[CrystalTerror] Failed to refresh current character: {ex.Message}");
            }
        }
        ImGuiEx.Tooltip("Refresh current character");
        
        ImGui.SameLine();
        
        // Import from AutoRetainer
        if (ImGuiEx.IconButton(FontAwesomeIcon.Download, "##importar"))
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
                Svc.Log.Warning($"[CrystalTerror] Failed to import from AutoRetainer: {ex.Message}");
            }
        }
        ImGuiEx.Tooltip("Import from AutoRetainer");

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

        // Collect overlay texts for status display at the end
        var overlayTexts = new List<(Vector2 pos, List<(Vector4? color, string text)> texts)>();

        // Render each character as a collapsing section with retainer table
        for (var index = 0; index < characters.Count; index++)
        {
            var character = characters[index];
            RenderCharacterEntry(character, index, showIgnored, overlayTexts);
        }

        // Draw overlay texts (totals displayed on the right side)
        statusTextWidth = 0f;
        DrawOverlayTexts(overlayTexts);
    }

    private void RenderCharacterEntry(StoredCharacter character, int index, bool showIgnored, List<(Vector2 pos, List<(Vector4?, string)> texts)> overlayTexts)
    {
        var isCurrentCharacter = character.MatchesCID(Player.CID);
        
        ImGui.PushID(character.UniqueKey);
        var rCurPos = ImGui.GetCursorPos();

        // Auto-venture enable toggle button (like AutoRetainer's multi-mode button)
        if (plugin.Config.AutoVentureEnabled)
        {
            var colen = false;
            if (character.AutoVentureEnabled)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, 0xFF097000u); // Green when enabled
                colen = true;
            }
            if (ImGuiEx.IconButton(FontAwesomeIcon.Bolt, $"##av_{character.UniqueKey}"))
            {
                character.AutoVentureEnabled = !character.AutoVentureEnabled;
                ConfigHelper.SaveAndSync(plugin.Config, plugin.Characters);
            }
            if (colen) ImGui.PopStyleColor();
            ImGuiEx.Tooltip(character.AutoVentureEnabled 
                ? "Auto Venture enabled for this character (click to disable)" 
                : "Auto Venture disabled for this character (click to enable)");
            ImGui.SameLine(0, 3);
        }

        // Settings button
        if (ImGuiEx.IconButton(FontAwesomeIcon.UserCog, $"##cfg_{character.UniqueKey}"))
        {
            ImGui.OpenPopup($"popup_{character.UniqueKey}");
        }
        ImGuiEx.Tooltip("Configure Character");
        ImGui.SameLine(0, 3);

        // Character config popup
        if (ImGui.BeginPopup($"popup_{character.UniqueKey}"))
        {
            DrawCharacterConfig(character);
            ImGui.EndPopup();
        }

        // Progress bar background (visual indicator)
        var initCurpos = ImGui.GetCursorPos();
        
        // Calculate how "full" the character is based on crystal totals
        if (plugin.Config.ShowProgressBars)
        {
            var totals = CalculateCharacterTotals(character, showIgnored);
            var totalCrystals = totals.Values.Sum(t => t.shard + t.crystal + t.cluster);
            var maxExpected = plugin.Config.ProgressBarMaxValue > 0 ? plugin.Config.ProgressBarMaxValue : 100000L;
            var prog = Math.Min(1.0f, (float)totalCrystals / maxExpected);
            
            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, prog >= 1.0f ? 0xFF005000u : 0xFF500000u);
            ImGui.ProgressBar(prog, new(ImGui.GetContentRegionAvail().X, ImGui.CalcTextSize("A").Y + ImGui.GetStyle().FramePadding.Y * 2), "");
            ImGui.PopStyleColor();
            ImGui.SetCursorPos(initCurpos);
        }

        // Push style for current/preferred character
        var colpref = PushColorIfCurrentCharacter(character, isCurrentCharacter);

        // Ignored styling
        if (character.IsIgnored)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.5f, 1.0f));
        }

        // Collapsing header
        var headerText = GetCutCharacterString(character, statusTextWidth);
        var isOpen = ImGui.CollapsingHeader($"{headerText}###chara_{character.UniqueKey}");

        if (character.IsIgnored)
        {
            ImGui.PopStyleColor();
        }

        if (colpref)
        {
            ImGui.PopStyleColor();
        }

        // Build overlay text (crystal totals on right side)
        // When expanded, show only character's inventory; when collapsed, show totals including retainers
        var totalsInfo = BuildTotalsOverlayText(character, showIgnored, isOpen);
        overlayTexts.Add((new Vector2(ImGui.GetContentRegionMax().X - ImGui.GetStyle().FramePadding.X, rCurPos.Y + ImGui.GetStyle().FramePadding.Y), totalsInfo));

        // Context menu on header
        if (ImGui.BeginPopupContextItem($"ctx_{character.UniqueKey}"))
        {
            RenderCharacterContextMenu(character);
            ImGui.EndPopup();
        }

        // Render content if open
        if (isOpen)
        {
            RenderCharacterContent(character, showIgnored);
            ImGui.Dummy(new Vector2(2, 2));
        }

        ImGui.PopID();
    }

    private void DrawCharacterConfig(StoredCharacter character)
    {
        ImGui.CollapsingHeader($"{character.Name} Configuration##conf", ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.Bullet | ImGuiTreeNodeFlags.OpenOnArrow);
        ImGui.Dummy(new Vector2(400, 1));

        // Auto venture toggle
        var autoVenture = character.AutoVentureEnabled;
        if (ImGui.Checkbox("Enable Auto Venture", ref autoVenture))
        {
            character.AutoVentureEnabled = autoVenture;
            ConfigHelper.SaveAndSync(plugin.Config, plugin.Characters);
        }
        ImGuiEx.Tooltip("When enabled, CrystalTerror will automatically assign crystal ventures for this character's retainers.");

        // Ignore toggle
        var isIgnored = character.IsIgnored;
        if (ImGui.Checkbox("Ignore Character", ref isIgnored))
        {
            character.IsIgnored = isIgnored;
            ConfigHelper.SaveAndSync(plugin.Config, plugin.Characters);
        }
        ImGuiEx.Tooltip("Ignored characters are hidden by default. Hold CTRL to show them.");

        ImGui.Separator();

        // Delete character
        if (ImGui.Button("Delete Character Data") && ImGui.GetIO().KeyCtrl)
        {
            plugin.Characters.Remove(character);
            ConfigHelper.SaveAndSync(plugin.Config, plugin.Characters);
            ImGui.CloseCurrentPopup();
        }
        ImGuiEx.Tooltip("Hold CTRL and click to delete this character's stored data.");
    }

    private void RenderCharacterContent(StoredCharacter character, bool showIgnored)
    {
        ImGui.PushID($"content_{character.UniqueKey}");

        // Retainer table
        var retainers = character.Retainers ?? new List<Retainer>();
        if (!showIgnored)
        {
            retainers = retainers.Where(r => !r.IsIgnored).ToList();
        }

        if (retainers.Count == 0)
        {
            ImGui.TextDisabled("No retainers");
            ImGui.PopID();
            return;
        }

        // Draw retainer table similar to AutoRetainer's style
        DrawRetainerTable(character, retainers);

        ImGui.PopID();
    }

    private void DrawRetainerTable(StoredCharacter character, List<Retainer> retainers)
    {
        // Get visible elements for crystal columns
        var visibleElements = Enum.GetValues<Element>().Where(e => countsUtility.IsElementVisible(e)).ToArray();
        
        // Column count: Enable, Name, and then crystal counts
        var baseColumns = 2; // Enable checkbox, Name
        var crystalColumns = visibleElements.Length;
        var columnCount = baseColumns + crystalColumns;

        if (!ImGui.BeginTable($"##retainertable_{character.UniqueKey}", columnCount, 
            ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
            return;

        // Setup columns
        ImGui.TableSetupColumn("##enable", ImGuiTableColumnFlags.WidthFixed, 25f);
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoResize);
        
        foreach (var element in visibleElements)
        {
            var elName = plugin.Config.UseAbbreviatedElementNames 
                ? element.ToString().Substring(0, 2) 
                : element.ToString();
            ImGui.TableSetupColumn(elName, ImGuiTableColumnFlags.WidthStretch);
        }
        
        // Custom header row with centered crystal column headers
        ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
        ImGui.TableNextColumn(); // Enable column (empty header)
        ImGui.TableNextColumn();
        ImGui.TextUnformatted("Name");
        foreach (var element in visibleElements)
        {
            ImGui.TableNextColumn();
            var elName = plugin.Config.UseAbbreviatedElementNames 
                ? element.ToString().Substring(0, 2) 
                : element.ToString();
            var textWidth = ImGui.CalcTextSize(elName).X;
            var columnWidth = ImGui.GetColumnWidth();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (columnWidth - textWidth) * 0.5f);
            ImGui.TextUnformatted(elName);
        }

        foreach (var retainer in retainers)
        {
            DrawRetainerRow(character, retainer, visibleElements);
        }

        ImGui.EndTable();

        // Character's own inventory section (collapsible)
        if (character.Inventory != null)
        {
            ImGuiEx.TreeNodeCollapsingHeader($"Character Inventory##charinv_{character.UniqueKey}", () =>
            {
                DrawInventoryMiniTable(character.Inventory, $"charinv_table_{character.UniqueKey}", visibleElements);
            });
        }
    }

    private void DrawRetainerRow(StoredCharacter character, Retainer retainer, Element[] visibleElements)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();

        // Enable checkbox for auto venture
        if (plugin.Config.AutoVentureEnabled && character.AutoVentureEnabled)
        {
            var enableAutoVenture = retainer.EnableAutoVenture;
            if (ImGui.Checkbox($"##av_{retainer.Atid}", ref enableAutoVenture))
            {
                retainer.EnableAutoVenture = enableAutoVenture;
                ConfigHelper.SaveAndSync(plugin.Config, plugin.Characters);
            }
            ImGuiEx.Tooltip(enableAutoVenture 
                ? "Auto Venture enabled (click to disable)" 
                : "Auto Venture disabled (click to enable)");
        }
        else
        {
            ImGui.TextDisabled("-");
        }

        // Name column
        ImGui.TableNextColumn();
        
        // Determine warning level for coloring
        var warningLevel = GetRetainerWarningLevel(retainer);
        var warningColor = GetWarningColor(warningLevel);
        var hasWarning = warningLevel > 0;
        
        // Apply style for ignored retainers or warning level
        if (retainer.IsIgnored)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.5f, 1.0f));
        }
        else if (hasWarning)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, warningColor);
        }
        
        ImGui.TextUnformatted(retainer.Name);
        
        // Pop name color immediately after rendering the name
        if (retainer.IsIgnored || hasWarning)
        {
            ImGui.PopStyleColor();
        }
        
        // Tooltip showing job, level and stats
        if (ImGui.IsItemHovered())
        {
            RenderRetainerTooltip(retainer);
        }
        
        // Right-click context menu for retainer
        if (ImGui.BeginPopupContextItem($"retctx_{retainer.Atid}"))
        {
            DrawRetainerContextMenu(retainer);
            ImGui.EndPopup();
        }

        // Right-click config popup (separate from context menu)
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right) && ImGui.GetIO().KeyShift)
        {
            ImGui.OpenPopup($"retpopup_{retainer.Atid}");
        }
        if (ImGui.BeginPopup($"retpopup_{retainer.Atid}"))
        {
            DrawRetainerConfig(retainer);
            ImGui.EndPopup();
        }

        // Crystal columns (centered)
        foreach (var element in visibleElements)
        {
            ImGui.TableNextColumn();
            var crystalText = BuildCrystalCellText(retainer, element);
            var cellColor = GetCrystalCellThresholdColor(retainer, element);
            
            // Center the text
            var textWidth = ImGui.CalcTextSize(crystalText).X;
            var columnWidth = ImGui.GetColumnWidth();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (columnWidth - textWidth) * 0.5f);
            
            if (cellColor.HasValue)
                ImGui.TextColored(cellColor.Value, crystalText);
            else
                ImGui.TextUnformatted(crystalText);
            
            // Tooltip showing exact crystal counts
            if (ImGui.IsItemHovered())
            {
                RenderCrystalCellTooltip(retainer, element);
            }
        }
    }

    /// <summary>
    /// Renders a tooltip for a crystal cell showing exact counts for each crystal type.
    /// </summary>
    private void RenderCrystalCellTooltip(Retainer retainer, Element element)
    {
        if (retainer.Inventory == null) return;
        
        ImGui.BeginTooltip();
        ImGui.TextUnformatted($"{element}");
        ImGui.Separator();
        
        if (plugin.Config.ShowShards)
        {
            var shard = retainer.Inventory.GetCount(element, CrystalType.Shard);
            ImGui.TextUnformatted($"Shards: {shard:N0}");
        }
        if (plugin.Config.ShowCrystals)
        {
            var crystal = retainer.Inventory.GetCount(element, CrystalType.Crystal);
            ImGui.TextUnformatted($"Crystals: {crystal:N0}");
        }
        if (plugin.Config.ShowClusters)
        {
            var cluster = retainer.Inventory.GetCount(element, CrystalType.Cluster);
            ImGui.TextUnformatted($"Clusters: {cluster:N0}");
        }
        
        ImGui.EndTooltip();
    }

    /// <summary>
    /// Renders a tooltip for a retainer showing job, level, and stats with color-coded warnings.
    /// </summary>
    private void RenderRetainerTooltip(Retainer retainer)
    {
        ImGui.BeginTooltip();
        
        // Job
        var jobText = ClassJobExtensions.GetAbbreviation(retainer.Job) ?? "Unknown";
        ImGui.TextUnformatted($"Job: {jobText}");
        
        // Level with color coding
        // Red if under 26, Yellow if under 81, otherwise white
        ImGui.TextUnformatted("Level: ");
        ImGui.SameLine(0, 0);
        var levelColor = GetLevelWarningColor(retainer.Level);
        ImGui.PushStyleColor(ImGuiCol.Text, levelColor);
        ImGui.TextUnformatted(retainer.Level.ToString());
        ImGui.PopStyleColor();
        
        // Stats for gathering retainers
        if (IsGatheringRetainer(retainer))
        {
            // Gathering - Red if under 90
            ImGui.TextUnformatted("Gathering: ");
            ImGui.SameLine(0, 0);
            var gatheringColor = retainer.Gathering < 90 
                ? new Vector4(1.0f, 0.4f, 0.4f, 1.0f) // Red
                : new Vector4(1.0f, 1.0f, 1.0f, 1.0f); // White
            ImGui.PushStyleColor(ImGuiCol.Text, gatheringColor);
            ImGui.TextUnformatted(retainer.Gathering.ToString());
            ImGui.PopStyleColor();
            
            // Perception - Yellow if under 1620
            ImGui.TextUnformatted("Perception: ");
            ImGui.SameLine(0, 0);
            var perceptionColor = retainer.Perception < 1620 
                ? new Vector4(1.0f, 1.0f, 0.4f, 1.0f) // Yellow
                : new Vector4(1.0f, 1.0f, 1.0f, 1.0f); // White
            ImGui.PushStyleColor(ImGuiCol.Text, perceptionColor);
            ImGui.TextUnformatted(retainer.Perception.ToString());
            ImGui.PopStyleColor();
        }
        else
        {
            // Item level for combat retainers
            ImGui.TextUnformatted($"Item Level: {retainer.Ilvl}");
        }
        
        ImGui.EndTooltip();
    }

    /// <summary>
    /// Gets the warning level for a retainer (0=none, 1=yellow, 2=red).
    /// Based on: Level under 26 = red, Level under 81 = yellow,
    /// Gathering under 90 = red, Perception under 1620 = yellow.
    /// Returns the highest warning level.
    /// </summary>
    private int GetRetainerWarningLevel(Retainer retainer)
    {
        var level = 0;
        
        // Level warnings
        if (retainer.Level < 26)
        {
            level = Math.Max(level, 2); // Red
        }
        else if (retainer.Level < 81)
        {
            level = Math.Max(level, 1); // Yellow
        }
        
        // Gathering warning (only for gatherers)
        if (IsGatheringRetainer(retainer) && retainer.Gathering < 90)
        {
            level = Math.Max(level, 2); // Red
        }
        
        // Perception warning (only for gatherers)
        if (IsGatheringRetainer(retainer) && retainer.Perception < 1620)
        {
            level = Math.Max(level, 1); // Yellow
        }
        
        return level;
    }

    /// <summary>
    /// Gets the color for a warning level (0=white, 1=yellow, 2=red).
    /// </summary>
    private Vector4 GetWarningColor(int warningLevel)
    {
        return warningLevel switch
        {
            2 => new Vector4(1.0f, 0.4f, 0.4f, 1.0f), // Red
            1 => new Vector4(1.0f, 1.0f, 0.4f, 1.0f), // Yellow
            _ => new Vector4(1.0f, 1.0f, 1.0f, 1.0f), // White
        };
    }

    /// <summary>
    /// Gets the color for a retainer's level (red if under 26, yellow if under 81, white otherwise).
    /// </summary>
    private Vector4 GetLevelWarningColor(int level)
    {
        if (level < 26)
        {
            return new Vector4(1.0f, 0.4f, 0.4f, 1.0f); // Red
        }
        else if (level < 81)
        {
            return new Vector4(1.0f, 1.0f, 0.4f, 1.0f); // Yellow
        }
        return new Vector4(1.0f, 1.0f, 1.0f, 1.0f); // White
    }

    private string BuildStatsText(Retainer retainer)
    {
        var parts = new List<string>();
        
        if (retainer.Level > 0)
        {
            parts.Add($"L{retainer.Level}");
        }

        // Show iLvl for combat, Gathering for gatherers
        if (IsGatheringRetainer(retainer))
        {
            if (retainer.Gathering > 0)
            {
                parts.Add($"G{retainer.Gathering}");
            }
        }
        else if (retainer.Ilvl > 0)
        {
            parts.Add($"i{retainer.Ilvl}");
        }

        return parts.Count > 0 ? string.Join(" ", parts) : "-";
    }

    private string BuildCrystalCellText(Retainer retainer, Element element)
    {
        if (retainer.Inventory == null) return "-";

        var parts = new List<string>();
        
        if (plugin.Config.ShowShards)
        {
            var shard = retainer.Inventory.GetCount(element, CrystalType.Shard);
            parts.Add(countsUtility.FormatNumber(shard, plugin.Config.UseReducedNotationInTables));
        }
        if (plugin.Config.ShowCrystals)
        {
            var crystal = retainer.Inventory.GetCount(element, CrystalType.Crystal);
            parts.Add(countsUtility.FormatNumber(crystal, plugin.Config.UseReducedNotationInTables));
        }
        if (plugin.Config.ShowClusters)
        {
            var cluster = retainer.Inventory.GetCount(element, CrystalType.Cluster);
            parts.Add(countsUtility.FormatNumber(cluster, plugin.Config.UseReducedNotationInTables));
        }

        return parts.Count > 0 ? string.Join("/", parts) : "-";
    }

    /// <summary>
    /// Determines the threshold color for a retainer's crystal cell based on configured thresholds.
    /// </summary>
    private Vector4? GetCrystalCellThresholdColor(Retainer retainer, Element element)
    {
        if (retainer.Inventory == null) return null;

        // Calculate total based on visible crystal types
        var total = 0L;
        if (plugin.Config.ShowShards)
            total += retainer.Inventory.GetCount(element, CrystalType.Shard);
        if (plugin.Config.ShowCrystals)
            total += retainer.Inventory.GetCount(element, CrystalType.Crystal);
        if (plugin.Config.ShowClusters)
            total += retainer.Inventory.GetCount(element, CrystalType.Cluster);

        // Determine color based on threshold settings (check from highest to lowest, first match wins)
        if (plugin.Config.RetainerCrystalThreshold3Enabled && total >= plugin.Config.RetainerCrystalThreshold3Value)
            return plugin.Config.RetainerCrystalThreshold3Color;
        if (plugin.Config.RetainerCrystalThreshold2Enabled && total >= plugin.Config.RetainerCrystalThreshold2Value)
            return plugin.Config.RetainerCrystalThreshold2Color;
        if (plugin.Config.RetainerCrystalThreshold1Enabled && total >= plugin.Config.RetainerCrystalThreshold1Value)
            return plugin.Config.RetainerCrystalThreshold1Color;

        return null;
    }

    private void DrawRetainerConfig(Retainer retainer)
    {
        ImGui.CollapsingHeader($"{retainer.Name} Configuration##retconf", ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.Bullet);
        ImGui.Dummy(new Vector2(300, 1));

        // Enable auto venture
        var enableAutoVenture = retainer.EnableAutoVenture;
        if (ImGui.Checkbox("Enable Auto Venture", ref enableAutoVenture))
        {
            retainer.EnableAutoVenture = enableAutoVenture;
            ConfigHelper.SaveAndSync(plugin.Config, plugin.Characters);
        }

        // Ignore toggle
        var isIgnored = retainer.IsIgnored;
        if (ImGui.Checkbox("Ignore Retainer", ref isIgnored))
        {
            retainer.IsIgnored = isIgnored;
            ConfigHelper.SaveAndSync(plugin.Config, plugin.Characters);
        }

        ImGui.Separator();

        // Stats display
        ImGui.Text($"Job: {ClassJobExtensions.GetAbbreviation(retainer.Job)} ({ClassJobExtensions.GetClassJob(retainer.Job)?.Name ?? "Unknown"})");
        ImGui.Text($"Level: {retainer.Level}");
        
        if (IsGatheringRetainer(retainer))
        {
            ImGui.Text($"Gathering: {retainer.Gathering}");
            ImGui.Text($"Perception: {retainer.Perception}");
        }
        else
        {
            ImGui.Text($"Item Level: {retainer.Ilvl}");
        }

        ImGui.Separator();

        // Delete button
        if (ImGui.Button("Delete Retainer Data") && ImGui.GetIO().KeyCtrl)
        {
            retainer.OwnerCharacter?.Retainers?.Remove(retainer);
            ConfigHelper.SaveAndSync(plugin.Config, plugin.Characters);
            ImGui.CloseCurrentPopup();
        }
        ImGuiEx.Tooltip("Hold CTRL and click to delete.");
    }

    private void DrawRetainerContextMenu(Retainer retainer)
    {
        if (ImGui.MenuItem(retainer.IsIgnored ? "Unignore Retainer" : "Ignore Retainer"))
        {
            retainer.IsIgnored = !retainer.IsIgnored;
            ConfigHelper.SaveAndSync(plugin.Config, plugin.Characters);
        }

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

    private void DrawInventoryMiniTable(Inventory inventory, string tableId, Element[] visibleElements)
    {
        var columnCount = 1 + visibleElements.Length;
        if (!ImGui.BeginTable(tableId, columnCount, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))
            return;

        // Setup columns
        ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 60f);
        foreach (var element in visibleElements)
        {
            var elName = plugin.Config.UseAbbreviatedElementNames 
                ? element.ToString().Substring(0, 2) 
                : element.ToString();
            ImGui.TableSetupColumn(elName, ImGuiTableColumnFlags.WidthFixed, 90f);
        }
        
        // Custom header row with centered crystal column headers
        ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
        ImGui.TableNextColumn();
        ImGui.TextUnformatted("Type");
        foreach (var element in visibleElements)
        {
            ImGui.TableNextColumn();
            var elName = plugin.Config.UseAbbreviatedElementNames 
                ? element.ToString().Substring(0, 2) 
                : element.ToString();
            var textWidth = ImGui.CalcTextSize(elName).X;
            var columnWidth = ImGui.GetColumnWidth();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (columnWidth - textWidth) * 0.5f);
            ImGui.TextUnformatted(elName);
        }

        // Rows for each crystal type
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
                
                // Center the text
                var textWidth = ImGui.CalcTextSize(text).X;
                var columnWidth = ImGui.GetColumnWidth();
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (columnWidth - textWidth) * 0.5f);
                
                if (color.HasValue)
                    ImGui.TextColored(color.Value, text);
                else
                    ImGui.TextUnformatted(text);
            }
        }

        ImGui.EndTable();
    }

    private List<(Vector4? color, string text)> BuildTotalsOverlayText(StoredCharacter character, bool showIgnored, bool isExpanded = false)
    {
        var result = new List<(Vector4?, string)>();
        
        // When expanded, show only character's inventory; when collapsed, show totals including retainers
        var totals = isExpanded 
            ? CalculateCharacterOnlyTotals(character) 
            : CalculateCharacterTotals(character, showIgnored);
        
        // Calculate threshold multiplier: when expanded (character only) use 1, otherwise non-ignored retainer count + 1
        var retainers = character.Retainers ?? new List<Retainer>();
        var nonIgnoredRetainerCount = retainers.Count(r => !r.IsIgnored);
        var thresholdMultiplier = isExpanded ? 1 : nonIgnoredRetainerCount + 1;

        // Build a summary string for the header overlay
        foreach (var element in Enum.GetValues<Element>())
        {
            if (!countsUtility.IsElementVisible(element))
                continue;

            var (shard, crystal, cluster) = totals[element];
            var total = (plugin.Config.ShowShards ? shard : 0) 
                      + (plugin.Config.ShowCrystals ? crystal : 0) 
                      + (plugin.Config.ShowClusters ? cluster : 0);
            
            var elName = plugin.Config.UseAbbreviatedElementNames 
                ? element.ToString().Substring(0, 2) 
                : element.ToString().Substring(0, 1);
            
            // Determine color based on threshold settings (check from highest to lowest, first match wins)
            // Threshold values are multiplied by (retainer count + 1)
            Vector4? color = null;
            if (plugin.Config.RetainerCrystalThreshold3Enabled && total >= plugin.Config.RetainerCrystalThreshold3Value * thresholdMultiplier)
                color = plugin.Config.RetainerCrystalThreshold3Color;
            else if (plugin.Config.RetainerCrystalThreshold2Enabled && total >= plugin.Config.RetainerCrystalThreshold2Value * thresholdMultiplier)
                color = plugin.Config.RetainerCrystalThreshold2Color;
            else if (plugin.Config.RetainerCrystalThreshold1Enabled && total >= plugin.Config.RetainerCrystalThreshold1Value * thresholdMultiplier)
                color = plugin.Config.RetainerCrystalThreshold1Color;
            
            result.Add((color, $"{elName}:{countsUtility.FormatNumber(total, true)}"));
        }

        return result;
    }

    private void DrawOverlayTexts(List<(Vector2 pos, List<(Vector4? color, string text)> texts)> overlayTexts)
    {
        if (overlayTexts.Count == 0) return;

        // First pass: Calculate maximum width for each column position across all rows
        var maxColumnCount = overlayTexts.Max(o => o.texts.Count);
        var columnWidths = new float[maxColumnCount];
        
        foreach (var (_, texts) in overlayTexts)
        {
            for (var i = 0; i < texts.Count; i++)
            {
                var textWidth = ImGui.CalcTextSize(texts[i].text).X;
                columnWidths[i] = Math.Max(columnWidths[i], textWidth);
            }
        }

        // Calculate total width using fixed column widths
        var itemSpacing = ImGui.GetStyle().ItemSpacing.X;
        var totalWidth = columnWidths.Sum() + (maxColumnCount - 1) * itemSpacing;
        statusTextWidth = Math.Max(statusTextWidth, totalWidth);

        // Second pass: Draw each row using calculated column widths for alignment
        foreach (var (pos, texts) in overlayTexts)
        {
            if (texts.Count == 0) continue;

            var savedPos = ImGui.GetCursorPos();
            var xPos = pos.X - totalWidth;
            
            for (var i = 0; i < texts.Count; i++)
            {
                var (color, text) = texts[i];
                var textWidth = ImGui.CalcTextSize(text).X;
                
                // Right-align text within column by adding padding
                var columnPadding = columnWidths[i] - textWidth;
                ImGui.SetCursorPos(new Vector2(xPos + columnPadding, pos.Y));
                
                if (color.HasValue)
                    ImGui.TextColored(color.Value, text);
                else
                    ImGui.TextDisabled(text);
                
                xPos += columnWidths[i] + itemSpacing;
            }

            ImGui.SetCursorPos(savedPos);
        }
    }

    private bool PushColorIfCurrentCharacter(StoredCharacter character, bool isCurrentCharacter)
    {
        if (!isCurrentCharacter || !plugin.Config.ColorCurrentCharacter)
            return false;

        ImGui.PushStyleColor(ImGuiCol.Text, plugin.Config.CurrentCharacterColor);
        return true;
    }

    private string GetCutCharacterString(StoredCharacter character, float reservedWidth)
    {
        var displayName = FormatCharacterName(character.Name);
        var chstr = plugin.Config.ShowWorldInHeader 
            ? $"{displayName} @ {character.World}" 
            : displayName;
        var maxWidth = ImGui.GetContentRegionAvail().X - reservedWidth - CollapsingHeaderSpacingsWidth;
        
        var mod = false;
        while (ImGui.CalcTextSize(chstr).X > maxWidth && chstr.Length > 5)
        {
            mod = true;
            chstr = chstr[..^1];
        }
        
        if (mod) chstr += "...";
        return chstr;
    }

    private string FormatCharacterName(string fullName)
    {
        if (string.IsNullOrEmpty(fullName))
            return fullName;

        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        return plugin.Config.NameDisplayFormat switch
        {
            NameDisplayFormat.FirstName => parts.Length > 0 ? parts[0] : fullName,
            NameDisplayFormat.LastName => parts.Length > 1 ? parts[1] : (parts.Length > 0 ? parts[0] : fullName),
            NameDisplayFormat.Initials => parts.Length > 1 
                ? $"{parts[0][0]}.{parts[1][0]}." 
                : (parts.Length > 0 ? $"{parts[0][0]}." : fullName),
            _ => fullName // FullName is default
        };
    }

    private static float CollapsingHeaderSpacingsWidth => 
        ImGui.GetStyle().FramePadding.X * 2f + ImGui.GetStyle().ItemSpacing.X * 2 + ImGui.CalcTextSize("▲...").X;

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

    private Dictionary<Element, (long shard, long crystal, long cluster)> CalculateCharacterOnlyTotals(StoredCharacter character)
    {
        var result = new Dictionary<Element, (long shard, long crystal, long cluster)>();

        foreach (var element in Enum.GetValues<Element>())
        {
            long shard = character.Inventory?.GetCount(element, CrystalType.Shard) ?? 0;
            long crystal = character.Inventory?.GetCount(element, CrystalType.Crystal) ?? 0;
            long cluster = character.Inventory?.GetCount(element, CrystalType.Cluster) ?? 0;

            result[element] = (shard, crystal, cluster);
        }

        return result;
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

        // Hide characters without gathering retainers if option is enabled
        if (plugin.Config.HideNonGatheringCharacters)
        {
            characters = characters.Where(c => c.Retainers.Any(r => IsGatheringRetainer(r))).ToList();
        }

        // Apply sort
        characters = ApplySorting(characters);

        // Always show current character at top
        if (Player.Available && Player.CID != 0)
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
            CharacterSortOptions.AutoRetainer => ApplyAutoRetainerSorting(characters),
            _ => characters
        };
    }

    private List<StoredCharacter> ApplyAutoRetainerSorting(List<StoredCharacter> characters)
    {
        var cidOrder = AutoRetainerHelper.GetCharacterOrder();
        if (cidOrder.Count == 0)
            return characters;

        var cidPositions = new Dictionary<ulong, int>();
        for (int i = 0; i < cidOrder.Count; i++)
        {
            cidPositions[cidOrder[i]] = i;
        }

        return characters
            .OrderBy(c => c.ContentId != 0 && cidPositions.TryGetValue(c.ContentId, out var pos) ? pos : int.MaxValue)
            .ThenBy(c => c.Name)
            .ToList();
    }

    private bool IsGatheringRetainer(Retainer retainer)
    {
        // MIN=16, BTN=17, FSH=18
        return retainer.Job == 16 || retainer.Job == 17 || retainer.Job == 18;
    }
}
