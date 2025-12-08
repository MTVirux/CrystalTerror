namespace CrystalTerror.Gui.Common;

using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using ImGui = Dalamud.Bindings.ImGui.ImGui;

/// <summary>
/// Utility component for crystal count formatting, color calculations, and rendering.
/// Handles all logic related to displaying crystal/shard/cluster counts with color coding.
/// </summary>
public class CrystalCountsUtility
{
	private readonly CrystalTerrorPlugin plugin;

	public CrystalCountsUtility(CrystalTerrorPlugin plugin)
	{
		this.plugin = plugin;
	}

	/// <summary>
	/// Formats a numeric value with optional reduced notation (k, M).
	/// </summary>
	public string FormatNumber(long value, bool useReducedNotation)
	{
		if (!useReducedNotation)
			return value.ToString();
		
		if (value >= 1000000)
			return $"{value / 1000000.0:0.#}M";
		if (value >= 1000)
			return $"{value / 1000.0:0.#}k";
		
		return value.ToString();
	}

	/// <summary>
	/// Formats crystal counts (shard/crystal/cluster) as a combined string.
	/// </summary>
	public string FormatCrystalCounts(long shard, long crystal, long cluster)
	{
		var cfg = this.plugin.Config;
		var parts = new List<string>();
		
		if (cfg.ShowShards)
			parts.Add(FormatNumber(shard, cfg.UseReducedNotationInTables));
		if (cfg.ShowCrystals)
			parts.Add(FormatNumber(crystal, cfg.UseReducedNotationInTables));
		if (cfg.ShowClusters)
			parts.Add(FormatNumber(cluster, cfg.UseReducedNotationInTables));
		
		return parts.Count > 0 ? string.Join("/", parts) : "-";
	}

	/// <summary>
	/// Gets the warning color for a given value based on configured thresholds.
	/// Returns null if no threshold is met.
	/// </summary>
	public System.Numerics.Vector4? GetWarningColor(long value)
	{
		var cfg = this.plugin.Config;
		
		// Build list of enabled thresholds and sort by value (ascending)
		var thresholds = new List<(int value, System.Numerics.Vector4 color)>();
		
		if (cfg.RetainerCrystalThreshold1Enabled)
			thresholds.Add((cfg.RetainerCrystalThreshold1Value, cfg.RetainerCrystalThreshold1Color));
		
		if (cfg.RetainerCrystalThreshold2Enabled)
			thresholds.Add((cfg.RetainerCrystalThreshold2Value, cfg.RetainerCrystalThreshold2Color));
		
		if (cfg.RetainerCrystalThreshold3Enabled)
			thresholds.Add((cfg.RetainerCrystalThreshold3Value, cfg.RetainerCrystalThreshold3Color));
		
		// Sort ascending so we check the lowest threshold first
		thresholds.Sort((a, b) => a.value.CompareTo(b.value));
		
		// Return color of the last (highest) threshold that the value is at or above
		System.Numerics.Vector4? result = null;
		foreach (var threshold in thresholds)
		{
			if (value >= threshold.value)
				result = threshold.color;
		}
		
		return result;
	}

	/// <summary>
	/// Renders crystal counts with appropriate coloring based on thresholds.
	/// </summary>
	public void RenderColoredCrystalCounts(long shard, long crystal, long cluster)
	{
		var cfg = this.plugin.Config;
		var parts = new List<(long value, System.Numerics.Vector4? color)>();
		
		if (cfg.ShowShards)
			parts.Add((shard, GetWarningColor(shard)));
		if (cfg.ShowCrystals)
			parts.Add((crystal, GetWarningColor(crystal)));
		if (cfg.ShowClusters)
			parts.Add((cluster, GetWarningColor(cluster)));
		
		if (parts.Count == 0)
		{
			ImGui.TextUnformatted("-");
			return;
		}
		
		// Check if all parts have the same color
		var firstColor = parts[0].color;
		bool allSameColor = parts.All(p => p.color == firstColor);
		
		if (allSameColor && firstColor.HasValue)
		{
			// Render entire text in one color
			var displayText = string.Join("/", parts.Select(p => FormatNumber(p.value, cfg.UseReducedNotationInTables)));
			ImGui.TextColored(firstColor.Value, displayText);
		}
		else if (allSameColor)
		{
			// No color, render as plain text
			var displayText = string.Join("/", parts.Select(p => FormatNumber(p.value, cfg.UseReducedNotationInTables)));
			ImGui.TextUnformatted(displayText);
		}
		else
		{
			// Mixed colors - render each part with SameLine
			for (int i = 0; i < parts.Count; i++)
			{
				if (i > 0)
				{
					ImGui.SameLine(0, 0);
					ImGui.TextUnformatted("/");
					ImGui.SameLine(0, 0);
				}
				
				var colorValue = parts[i].color;
				if (colorValue.HasValue)
					ImGui.TextColored(colorValue.Value, FormatNumber(parts[i].value, cfg.UseReducedNotationInTables));
				else
					ImGui.TextUnformatted(FormatNumber(parts[i].value, cfg.UseReducedNotationInTables));
			}
		}
	}

	/// <summary>
	/// Checks if an element is visible based on configuration.
	/// </summary>
	public bool IsElementVisible(Element element)
	{
		return element switch
		{
			Element.Fire => this.plugin.Config.ShowFireElement,
			Element.Ice => this.plugin.Config.ShowIceElement,
			Element.Wind => this.plugin.Config.ShowWindElement,
			Element.Lightning => this.plugin.Config.ShowLightningElement,
			Element.Earth => this.plugin.Config.ShowEarthElement,
			Element.Water => this.plugin.Config.ShowWaterElement,
			_ => true
		};
	}
}
