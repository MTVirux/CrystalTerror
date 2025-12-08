namespace CrystalTerror.Gui.MainWindow;

using System;
using System.Collections.Generic;
using System.Linq;
using CrystalTerror.Helpers;
using CrystalTerror.Gui.Common;
using Dalamud.Bindings.ImGui;
using ImGui = Dalamud.Bindings.ImGui.ImGui;

/// <summary>
/// Component for rendering the character collapsing header with totals, context menu, and styling.
/// </summary>
public class CharacterHeaderComponent : IUIComponent
{
	private readonly CrystalTerrorPlugin plugin;
	private readonly CrystalCountsUtility countsUtility;
	private readonly StoredCharacter character;
	private readonly int characterIndex;
	private bool headerOpen;

	public bool HeaderOpen => headerOpen;

	public CharacterHeaderComponent(CrystalTerrorPlugin plugin, CrystalCountsUtility countsUtility, StoredCharacter character, int characterIndex)
	{
		this.plugin = plugin;
		this.countsUtility = countsUtility;
		this.character = character;
		this.characterIndex = characterIndex;
		this.headerOpen = false;
	}

	public void Render()
	{
		// Calculate totals for visible elements
		var headerElements = new[] { Element.Fire, Element.Ice, Element.Wind, Element.Lightning, Element.Earth, Element.Water };
		var headerVisibleElements = headerElements.Where(el => this.countsUtility.IsElementVisible(el)).ToArray();
		var totalParts = new List<(string text, System.Numerics.Vector4? color)>();
		long grandTotal = 0;
		
		foreach (var el in headerVisibleElements)
		{
			long totalShard = character.Inventory?.GetCount(el, CrystalType.Shard) ?? 0;
			long totalCrystal = character.Inventory?.GetCount(el, CrystalType.Crystal) ?? 0;
			long totalCluster = character.Inventory?.GetCount(el, CrystalType.Cluster) ?? 0;
			
			// Add retainer totals
			if (character.Retainers != null)
			{
				bool showIgnored = ImGui.GetIO().KeyCtrl;
				var headerRetainers = showIgnored ? character.Retainers : character.Retainers.Where(r => !r.IsIgnored);
				foreach (var r in headerRetainers)
				{
					totalShard += r.Inventory?.GetCount(el, CrystalType.Shard) ?? 0;
					totalCrystal += r.Inventory?.GetCount(el, CrystalType.Crystal) ?? 0;
					totalCluster += r.Inventory?.GetCount(el, CrystalType.Cluster) ?? 0;
				}
			}
			
			// Add to grand total based on which types are visible
			if (this.plugin.Config.ShowShards) grandTotal += totalShard;
			if (this.plugin.Config.ShowCrystals) grandTotal += totalCrystal;
			if (this.plugin.Config.ShowClusters) grandTotal += totalCluster;
			
			// Calculate element total and determine warning color based on crystal threshold * 6
			long elementTotal = 0;
			if (this.plugin.Config.ShowShards) elementTotal += totalShard;
			if (this.plugin.Config.ShowCrystals) elementTotal += totalCrystal;
			if (this.plugin.Config.ShowClusters) elementTotal += totalCluster;
			
			var warningColor = this.countsUtility.GetWarningColor(elementTotal / 6);
			
			// Format crystal counts for header - use reduced notation if enabled
			var headerParts = new List<string>();
			if (this.plugin.Config.ShowShards)
				headerParts.Add(countsUtility.FormatNumber(totalShard, this.plugin.Config.UseReducedNotationInHeaders));
			if (this.plugin.Config.ShowCrystals)
				headerParts.Add(countsUtility.FormatNumber(totalCrystal, this.plugin.Config.UseReducedNotationInHeaders));
			if (this.plugin.Config.ShowClusters)
				headerParts.Add(countsUtility.FormatNumber(totalCluster, this.plugin.Config.UseReducedNotationInHeaders));
			var elementTotalStr = headerParts.Count > 0 ? string.Join("/", headerParts) : "-";
			
			if (this.plugin.Config.ShowElementNamesInTotals)
			{
				var elementName = this.plugin.Config.UseAbbreviatedElementNames ? el.ToString().Substring(0, 2) : el.ToString();
				totalParts.Add(($"{elementName}: {elementTotalStr}", warningColor));
			}
			else
			{
				totalParts.Add((elementTotalStr, warningColor));
			}
		}
		
		// Check if this is the current character
		bool isCurrentChar = false;
		if (this.plugin.Config.ColorCurrentCharacter)
		{
			var contentId = Services.PlayerService.State.ContentId;
			if (contentId != 0)
			{
				var local = Services.PlayerService.Objects.LocalPlayer;
				if (local != null)
				{
					var currentName = local.Name.TextValue ?? string.Empty;
					var currentWorldId = local.HomeWorld.RowId.ToString();
					
					try
					{
						var sheet = Services.DataService.Manager.GetExcelSheet<Lumina.Excel.Sheets.World>();
						if (sheet != null)
						{
							var row = sheet.GetRowOrDefault((uint)local.HomeWorld.RowId);
							if (row.HasValue)
							{
								currentWorldId = row.Value.Name.ExtractText();
							}
						}
					}
					catch { }
					
					isCurrentChar = string.Equals(character.Name, currentName, StringComparison.OrdinalIgnoreCase) &&
						string.Equals(character.World, currentWorldId, StringComparison.OrdinalIgnoreCase);
				}
			}
		}
		
		// Build header text (without totals - we'll render those separately with colors)
		var header = $"{character.Name} @ {character.World}";
		
		// Determine header background color for character total thresholds
		System.Numerics.Vector4? headerBgColor = null;
		
		// Check character total thresholds
		var charThresholds = new List<(int value, System.Numerics.Vector4 color)>();
		if (this.plugin.Config.CharacterTotalThreshold1Enabled)
			charThresholds.Add((this.plugin.Config.CharacterTotalThreshold1Value, this.plugin.Config.CharacterTotalThreshold1Color));
		if (this.plugin.Config.CharacterTotalThreshold2Enabled)
			charThresholds.Add((this.plugin.Config.CharacterTotalThreshold2Value, this.plugin.Config.CharacterTotalThreshold2Color));
		if (this.plugin.Config.CharacterTotalThreshold3Enabled)
			charThresholds.Add((this.plugin.Config.CharacterTotalThreshold3Value, this.plugin.Config.CharacterTotalThreshold3Color));
		
		// Sort ascending and check thresholds - highest threshold wins
		charThresholds.Sort((a, b) => a.value.CompareTo(b.value));
		foreach (var threshold in charThresholds)
		{
			if (grandTotal >= threshold.value)
				headerBgColor = threshold.color;
		}
		
		// Render header with colors
		int colorsPushed = 0;

		// Dim header and text for ignored characters
		if (character.IsIgnored)
		{
			// Pop any previously pushed style colors (if any)
			if (colorsPushed > 0)
			{
				ImGui.PopStyleColor(colorsPushed);
				colorsPushed = 0;
			}
			// Dim the header background for ignored characters
			ImGui.PushStyleColor(ImGuiCol.Header, new System.Numerics.Vector4(0.3f, 0.3f, 0.3f, 1.0f));
			ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new System.Numerics.Vector4(0.35f, 0.35f, 0.35f, 1.0f));
			ImGui.PushStyleColor(ImGuiCol.HeaderActive, new System.Numerics.Vector4(0.4f, 0.4f, 0.4f, 1.0f));
			ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.5f, 0.5f, 0.5f, 1.0f));
			colorsPushed += 4;
		}
		else
		{
			// Push header background color if character total threshold matched
			if (headerBgColor.HasValue)
			{
				ImGui.PushStyleColor(ImGuiCol.Header, headerBgColor.Value);
				ImGui.PushStyleColor(ImGuiCol.HeaderHovered, headerBgColor.Value * new System.Numerics.Vector4(1.2f, 1.2f, 1.2f, 1.0f));
				ImGui.PushStyleColor(ImGuiCol.HeaderActive, headerBgColor.Value * new System.Numerics.Vector4(1.4f, 1.4f, 1.4f, 1.0f));
				colorsPushed += 3;
			}

			// Push text color if current character
			if (isCurrentChar)
			{
				ImGui.PushStyleColor(ImGuiCol.Text, this.plugin.Config.CurrentCharacterColor);
				colorsPushed++;
			}
		}
		
		// Render header with totals
		if (this.plugin.Config.ShowTotalsInHeaders && totalParts.Count > 0)
		{
			// Render main header text
			var headerId = header + " —" + $"##{characterIndex}";
			headerOpen = ImGui.CollapsingHeader(headerId);
			
			// Attach context menu to the header item with forced white text
			if (ImGui.BeginPopupContextItem($"char_ctx_{characterIndex}"))
			{
				ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(1f, 1f, 1f, 1f));
				if (ImGui.MenuItem("Reset character inventory"))
				{
					character.Inventory?.Reset();
					if (character.Retainers != null)
					{
						foreach (var r in character.Retainers)
							r.Inventory?.Reset();
					}
					ConfigHelper.SaveAndSync(this.plugin.Config, this.plugin.Characters);
				}
				if (!character.IsIgnored)
				{
					if (ImGui.MenuItem("Ignore character"))
					{
						character.IsIgnored = true;
						ConfigHelper.SaveAndSync(this.plugin.Config, this.plugin.Characters);
						this.plugin.InvalidateSortCache();
					}
				}
				else
				{
					if (ImGui.MenuItem("Unignore character"))
					{
						character.IsIgnored = false;
						ConfigHelper.SaveAndSync(this.plugin.Config, this.plugin.Characters);
						this.plugin.InvalidateSortCache();
					}
				}
				ImGui.PopStyleColor();
				ImGui.EndPopup();
			}
			
			// Render colored totals on the same line
			ImGui.SameLine(0, 5);
			for (int idx = 0; idx < totalParts.Count; idx++)
			{
				if (idx > 0)
				{
					ImGui.SameLine(0, 0);
					ImGui.TextUnformatted(" | ");
					ImGui.SameLine(0, 0);
				}
				
				var (text, color) = totalParts[idx];
				if (color.HasValue)
					ImGui.TextColored(color.Value, text);
				else
					ImGui.TextUnformatted(text);
				
				if (idx < totalParts.Count - 1)
					ImGui.SameLine(0, 0);
			}
		}
		else
		{
			var headerId = header + $"##{characterIndex}";
			headerOpen = ImGui.CollapsingHeader(headerId);
			
			// Attach context menu to the header item with forced white text
			if (ImGui.BeginPopupContextItem($"char_ctx_{characterIndex}"))
			{
				ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(1f, 1f, 1f, 1f));
				if (ImGui.MenuItem("Reset character inventory"))
				{
					character.Inventory?.Reset();
					if (character.Retainers != null)
					{
						foreach (var r in character.Retainers)
							r.Inventory?.Reset();
					}
					ConfigHelper.SaveAndSync(this.plugin.Config, this.plugin.Characters);
				}
				if (!character.IsIgnored)
				{
					if (ImGui.MenuItem("Ignore character"))
					{
						character.IsIgnored = true;
						ConfigHelper.SaveAndSync(this.plugin.Config, this.plugin.Characters);
						this.plugin.InvalidateSortCache();
					}
				}
				else
				{
					if (ImGui.MenuItem("Unignore character"))
					{
						character.IsIgnored = false;
						ConfigHelper.SaveAndSync(this.plugin.Config, this.plugin.Characters);
						this.plugin.InvalidateSortCache();
					}
				}
				ImGui.PopStyleColor();
				ImGui.EndPopup();
			}
		}
		
		if (colorsPushed > 0)
			ImGui.PopStyleColor(colorsPushed);
	}
}
