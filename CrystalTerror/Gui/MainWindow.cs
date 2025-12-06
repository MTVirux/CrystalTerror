namespace CrystalTerror.Gui;

using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using ImGui = Dalamud.Bindings.ImGui.ImGui;

public class MainWindow : Window, IDisposable
{
	private readonly CrystalTerrorPlugin plugin;
	private TitleBarButton lockButton;
	private readonly ITextureProvider textureProvider;
	
	// Cache for sorted characters to avoid re-sorting every frame
	private List<StoredCharacter>? cachedSortedCharacters = null;
	private CharacterSortOptions? cachedSortOption = null;
	private int cachedCharacterCount = 0;

	public MainWindow(CrystalTerrorPlugin plugin, ITextureProvider textureProvider)
		: base("CrystalTerrorMainWindow")
	{
		this.plugin = plugin;
		this.textureProvider = textureProvider;
		this.SizeConstraints = new WindowSizeConstraints()
		{
			MinimumSize = new System.Numerics.Vector2(300, 100),
			MaximumSize = ImGui.GetIO().DisplaySize,
		};

		// Initialize lock button
		lockButton = new TitleBarButton
		{
			Click = OnLockButtonClick,
			Icon = plugin.Config.PinMainWindow ? FontAwesomeIcon.Lock : FontAwesomeIcon.LockOpen,
			IconOffset = new System.Numerics.Vector2(3, 2),
			ShowTooltip = () => ImGui.SetTooltip("Lock window position and size"),
		};

		// Add title bar buttons
		TitleBarButtons.Add(new TitleBarButton
		{
			Click = (m) => { if (m == ImGuiMouseButton.Left) plugin.OpenConfigUi(); },
			Icon = FontAwesomeIcon.Cog,
			IconOffset = new System.Numerics.Vector2(2, 2),
			ShowTooltip = () => ImGui.SetTooltip("Open settings"),
		});
		TitleBarButtons.Add(lockButton);
	}

	public void Dispose()
	{
	}

	private void OnLockButtonClick(ImGuiMouseButton button)
	{
		if (button == ImGuiMouseButton.Left)
		{
			this.plugin.Config.PinMainWindow = !this.plugin.Config.PinMainWindow;
			lockButton.Icon = this.plugin.Config.PinMainWindow ? FontAwesomeIcon.Lock : FontAwesomeIcon.LockOpen;

			// Save current position and size when locking
			if (this.plugin.Config.PinMainWindow)
			{
				this.plugin.Config.MainWindowPos = ImGui.GetWindowPos();
				this.plugin.Config.MainWindowSize = ImGui.GetWindowSize();
			}

			this.plugin.PluginInterface.SavePluginConfig(this.plugin.Config);
		}
	}

	public override void PreDraw()
	{
		if (this.plugin.Config.PinMainWindow)
		{
			Flags |= ImGuiWindowFlags.NoMove;
			Flags &= ~ImGuiWindowFlags.NoResize;
			ImGui.SetNextWindowPos(this.plugin.Config.MainWindowPos);
			ImGui.SetNextWindowSize(this.plugin.Config.MainWindowSize);
		}
		else
		{
			Flags &= ~ImGuiWindowFlags.NoMove;
		}

		// Handle ESC key ignore setting
		RespectCloseHotkey = !this.plugin.Config.IgnoreEscapeOnMainWindow;
	}

	public override void PostDraw()
	{
		// When locked, save the current size back to config so it resets next frame
		if (this.plugin.Config.PinMainWindow)
		{
			// This makes the window "snap back" to the locked size every frame
			// allowing temporary stretching during the current frame only
		}
	}

	private bool IsElementVisible(Element element)
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

	private string FormatNumber(long value, bool useReducedNotation)
	{
		if (!useReducedNotation)
			return value.ToString();
		
		if (value >= 1000000)
			return $"{value / 1000000.0:0.#}M";
		if (value >= 1000)
			return $"{value / 1000.0:0.#}k";
		
		return value.ToString();
	}

	private string FormatCrystalCounts(long shard, long crystal, long cluster)
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

	private System.Numerics.Vector4? GetWarningColor(long value)
	{
		var cfg = this.plugin.Config;
		
		// Build list of enabled thresholds and sort by value (descending) to prioritize lower thresholds
		var thresholds = new List<(int value, System.Numerics.Vector4 color)>();
		
		if (cfg.RetainerCrystalThreshold1Enabled)
			thresholds.Add((cfg.RetainerCrystalThreshold1Value, cfg.RetainerCrystalThreshold1Color));
		
		if (cfg.RetainerCrystalThreshold2Enabled)
			thresholds.Add((cfg.RetainerCrystalThreshold2Value, cfg.RetainerCrystalThreshold2Color));
		
		if (cfg.RetainerCrystalThreshold3Enabled)
			thresholds.Add((cfg.RetainerCrystalThreshold3Value, cfg.RetainerCrystalThreshold3Color));
		
		// Sort ascending so we check the lowest threshold first
		// This ensures higher threshold values take priority when a value is at or above multiple thresholds
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

	private void RenderColoredCrystalCounts(long shard, long crystal, long cluster)
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

	private List<StoredCharacter> GetSortedCharacters()
	{
		var characters = this.plugin.Characters.ToList();
		var currentSortOption = this.plugin.Config.CharacterSortOption;
		
		// Check if cache is valid
		if (cachedSortedCharacters != null && 
		    cachedSortOption == currentSortOption && 
		    cachedCharacterCount == characters.Count)
		{
			return cachedSortedCharacters;
		}
		
		// Cache is invalid, re-sort
		var sorted = currentSortOption switch
		{
			CharacterSortOptions.Alphabetical => characters.OrderBy(c => c.Name).ToList(),
			CharacterSortOptions.ReverseAlphabetical => characters.OrderByDescending(c => c.Name).ToList(),
			CharacterSortOptions.World => characters.OrderBy(c => c.World).ThenBy(c => c.Name).ToList(),
			CharacterSortOptions.ReverseWorld => characters.OrderByDescending(c => c.World).ThenByDescending(c => c.Name).ToList(),
			CharacterSortOptions.AutoRetainer => GetAutoRetainerOrderedCharacters(characters),
			CharacterSortOptions.Custom => characters, // Keep original order (user-defined)
			_ => characters
		};
		
		// Force current character to top if AutoRetainer sort is selected, or if ShowCurrentCharacterAtTop is enabled
		if (this.plugin.Config.ShowCurrentCharacterAtTop || this.plugin.Config.CharacterSortOption == CharacterSortOptions.AutoRetainer)
		{
			var contentId = Services.PlayerService.State.ContentId;
			if (contentId != 0)
			{
				var local = Services.PlayerService.Objects.LocalPlayer;
				if (local != null)
				{
					var currentName = local.Name.TextValue ?? string.Empty;
					var currentWorldId = local.HomeWorld.RowId.ToString();
					
					// Try to get world name
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
					
					// Find current character and move to top
					var currentChar = sorted.FirstOrDefault(c => 
						string.Equals(c.Name, currentName, StringComparison.OrdinalIgnoreCase) &&
						string.Equals(c.World, currentWorldId, StringComparison.OrdinalIgnoreCase));
					
					if (currentChar != null)
					{
						sorted.Remove(currentChar);
						sorted.Insert(0, currentChar);
					}
				}
			}
		}
		
		// Update cache
		Services.LogService.Log.Debug($"[MainWindow] Updating character sort cache (SortOption={currentSortOption}, Count={characters.Count})");
		cachedSortedCharacters = sorted;
		cachedSortOption = currentSortOption;
		cachedCharacterCount = characters.Count;
		
		return sorted;
	}
	
	/// <summary>
	/// Invalidate the sorted characters cache. Call this when character data changes.
	/// </summary>
	public void InvalidateSortCache()
	{
		Services.LogService.Log.Debug("[MainWindow] Character sort cache invalidated");
		cachedSortedCharacters = null;
	}

	private List<StoredCharacter> GetAutoRetainerOrderedCharacters(List<StoredCharacter> characters)
	{
		try
		{
			// Query AutoRetainer IPC for the ordered list of character CIDs
			var getRegisteredCIDs = Services.PluginInterfaceService.Interface.GetIpcSubscriber<List<ulong>>("AutoRetainer.GetRegisteredCIDs");
			var orderedCIDs = getRegisteredCIDs?.InvokeFunc();
			
			if (orderedCIDs == null || orderedCIDs.Count == 0)
			{
				// AutoRetainer not available or no characters registered, fall back to original order
				return characters;
			}
			
			// Get offline character data for each CID to retrieve Name and World
			var getOfflineData = Services.PluginInterfaceService.Interface.GetIpcSubscriber<ulong, object>("AutoRetainer.GetOfflineCharacterData");
			if (getOfflineData == null)
			{
				return characters;
			}
			
			// Build a list of (Name, World) tuples in the order from AutoRetainer
			var autoRetainerOrder = new List<(string Name, string World)>();
			foreach (var cid in orderedCIDs)
			{
				try
				{
					var charData = getOfflineData.InvokeFunc(cid);
					if (charData != null)
					{
						dynamic dyn = charData;
						string name = dyn.Name ?? string.Empty;
						string world = dyn.World ?? string.Empty;
						if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(world))
						{
							autoRetainerOrder.Add((name, world));
						}
					}
				}
				catch { /* Skip characters that fail to load */ }
			}
			
			// Create a dictionary for quick lookup of characters by (Name, World)
			var charactersByNameWorld = characters
				.ToDictionary(c => (c.Name, c.World), c => c);
			
			// Build ordered list based on AutoRetainer's order
			var ordered = new List<StoredCharacter>();
			foreach (var (name, world) in autoRetainerOrder)
			{
				if (charactersByNameWorld.TryGetValue((name, world), out var character))
				{
					ordered.Add(character);
					charactersByNameWorld.Remove((name, world)); // Remove so we don't duplicate
				}
			}
			
			// Append any characters that weren't in AutoRetainer's list at the end
			ordered.AddRange(charactersByNameWorld.Values);
			
			return ordered;
		}
		catch
		{
			// If AutoRetainer IPC fails, fall back to original order
			return characters;
		}
	}

	public override void Draw()
	{
		if (this.plugin.Characters.Count == 0)
		{
			ImGui.TextWrapped("No characters imported yet. Open settings to import characters.");
			return;
		}

		var sortedCharacters = this.GetSortedCharacters();
		
		// Filter out characters without gathering retainers if enabled
		if (this.plugin.Config.HideNonGatheringCharacters)
		{
			sortedCharacters = sortedCharacters
				.Where(c => c.Retainers.Any(r => r.Job == 16 || r.Job == 17 || r.Job == 18))
				.ToList();
		}
		
		for (int i = 0; i < sortedCharacters.Count; ++i)
		{
			var c = sortedCharacters[i];
			
			// Show up/down buttons in edit mode for Custom sort
			if (this.plugin.Config.IsEditMode && this.plugin.Config.CharacterSortOption == CharacterSortOptions.Custom)
			{
				var canMoveUp = i > 0;
				var canMoveDown = i < sortedCharacters.Count - 1;
				
				if (!canMoveUp)
				{
					ImGui.BeginDisabled();
				}
				if (ImGui.ArrowButton($"up_{i}", ImGuiDir.Up))
				{
					// Move character up
					var idx = this.plugin.Characters.IndexOf(c);
					if (idx > 0)
					{
						this.plugin.Characters.RemoveAt(idx);
						this.plugin.Characters.Insert(idx - 1, c);
						
						// Save updated order
						this.plugin.Config.Characters = this.plugin.Characters;
						this.plugin.PluginInterface.SavePluginConfig(this.plugin.Config);
					}
				}
				if (!canMoveUp)
				{
					ImGui.EndDisabled();
				}
				
				ImGui.SameLine();
				
				if (!canMoveDown)
				{
					ImGui.BeginDisabled();
				}
				if (ImGui.ArrowButton($"down_{i}", ImGuiDir.Down))
				{
					// Move character down
					var idx = this.plugin.Characters.IndexOf(c);
					if (idx >= 0 && idx < this.plugin.Characters.Count - 1)
					{
						this.plugin.Characters.RemoveAt(idx);
						this.plugin.Characters.Insert(idx + 1, c);
						
						// Save updated order
						this.plugin.Config.Characters = this.plugin.Characters;
						this.plugin.PluginInterface.SavePluginConfig(this.plugin.Config);
					}
				}
				if (!canMoveDown)
				{
					ImGui.EndDisabled();
				}
				
				ImGui.SameLine();
			}

		// Calculate totals for visible elements
		var headerElements = new[] { Element.Fire, Element.Ice, Element.Wind, Element.Lightning, Element.Earth, Element.Water };
		var headerVisibleElements = headerElements.Where(el => this.IsElementVisible(el)).ToArray();
		var totalParts = new List<(string text, System.Numerics.Vector4? color)>();
		long grandTotal = 0;
		
		foreach (var el in headerVisibleElements)
		{
			long totalShard = c.Inventory?.GetCount(el, CrystalType.Shard) ?? 0;
			long totalCrystal = c.Inventory?.GetCount(el, CrystalType.Crystal) ?? 0;
			long totalCluster = c.Inventory?.GetCount(el, CrystalType.Cluster) ?? 0;
			
			// Add retainer totals
			if (c.Retainers != null)
			{
				foreach (var r in c.Retainers)
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
			
			var warningColor = this.GetWarningColor(elementTotal / 6);
			
			// Format crystal counts for header - use reduced notation if enabled
			var headerParts = new List<string>();
			if (this.plugin.Config.ShowShards)
				headerParts.Add(FormatNumber(totalShard, this.plugin.Config.UseReducedNotationInHeaders));
			if (this.plugin.Config.ShowCrystals)
				headerParts.Add(FormatNumber(totalCrystal, this.plugin.Config.UseReducedNotationInHeaders));
			if (this.plugin.Config.ShowClusters)
				headerParts.Add(FormatNumber(totalCluster, this.plugin.Config.UseReducedNotationInHeaders));
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
					
					isCurrentChar = string.Equals(c.Name, currentName, StringComparison.OrdinalIgnoreCase) &&
						string.Equals(c.World, currentWorldId, StringComparison.OrdinalIgnoreCase);
				}
			}
		}
		
		// Build header text (without totals - we'll render those separately with colors)
		var header = $"{c.Name} @ {c.World}";
		
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
		bool headerOpen;
		int colorsPushed = 0;
		
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
		
		// Render header with totals
		if (this.plugin.Config.ShowTotalsInHeaders && totalParts.Count > 0)
		{
			// Render main header text
			headerOpen = ImGui.CollapsingHeader(header + " —" + $"##{i}");
			
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
			headerOpen = ImGui.CollapsingHeader(header + $"##{i}");
		}
		
		if (colorsPushed > 0)
			ImGui.PopStyleColor(colorsPushed);
		
		if (headerOpen)
		{
			#if DEBUG
			ImGui.TextUnformatted($"LastUpdateUtc: {c.LastUpdateUtc:u}");
			ImGui.Separator();
			#endif
			// Character inventory table
			if (c.Inventory != null)
			{
				ImGui.Text("Character Inventory:");
				var allElements = new[] { Element.Fire, Element.Ice, Element.Wind, Element.Lightning, Element.Earth, Element.Water };
				var elements = allElements.Where(el => this.IsElementVisible(el)).ToArray();
				
				if (elements.Length == 0)
				{
					ImGui.TextWrapped("No elements selected in filters. Check config to enable elements.");
				}
				else
				{
					var colCount = 2 + elements.Length;
					var charTableId = $"char_inventory_table_{i}";
					if (ImGui.BeginTable(charTableId, colCount, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
					{
						ImGui.TableSetupColumn("Character", ImGuiTableColumnFlags.WidthFixed, ConfigStatic.RetainerNameColumnWidth);
						ImGui.TableSetupColumn("World", ImGuiTableColumnFlags.WidthFixed, ConfigStatic.JobLevelColumnWidth);
						foreach (var el in elements)
						{
							ImGui.TableSetupColumn(el.ToString(), ImGuiTableColumnFlags.WidthStretch);
						}

					ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
					ImGui.TableSetColumnIndex(0);
					ImGui.TableHeader("Character");
					ImGui.TableSetColumnIndex(1);
					ImGui.TableHeader("World");
						for (int col = 0; col < elements.Length; ++col)
						{
							ImGui.TableSetColumnIndex(2 + col);
							var headerText = elements[col].ToString();
							var textSize = ImGui.CalcTextSize(headerText);
							var cellWidth = ImGui.GetContentRegionAvail().X;
							var offset = (cellWidth - textSize.X) * 0.5f;
							if (offset > 0)
								ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
						ImGui.TableHeader(headerText);
					}

					// Character inventory row (format: shard/crystal/cluster per element)
					ImGui.TableNextRow();
						ImGui.TableSetColumnIndex(0);
						ImGui.TextUnformatted(c.Name);
						ImGui.TableSetColumnIndex(1);
						{
							var worldText = c.World;
							var textSize = ImGui.CalcTextSize(worldText);
							var cellWidth = ImGui.GetContentRegionAvail().X;
							var offset = (cellWidth - textSize.X) * 0.5f;
							if (offset > 0)
								ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
							ImGui.TextUnformatted(worldText);
						}
						for (int col = 0; col < elements.Length; ++col)
						{
							ImGui.TableSetColumnIndex(2 + col);
							var el = elements[col];
							long shard = c.Inventory.GetCount(el, CrystalType.Shard);
							long crystal = c.Inventory.GetCount(el, CrystalType.Crystal);
							long cluster = c.Inventory.GetCount(el, CrystalType.Cluster);
							
							// Calculate width for centering
							var displayText = this.FormatCrystalCounts(shard, crystal, cluster);
							var textSize = ImGui.CalcTextSize(displayText);
							var cellWidth = ImGui.GetContentRegionAvail().X;
							var offset = (cellWidth - textSize.X) * 0.5f;
							if (offset > 0)
								ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
							
							// Render with color
						this.RenderColoredCrystalCounts(shard, crystal, cluster);
					}
					ImGui.EndTable();
				}
			}
				ImGui.Separator();
			}				if (c.Retainers == null || c.Retainers.Count == 0)
				{
					ImGui.TextWrapped("No retainers for this character.");
				}
				else
				{
					// Render inventory aggregated by Element. Each element column shows "shard/crystal/cluster".
					var allElements = new[] { Element.Fire, Element.Ice, Element.Wind, Element.Lightning, Element.Earth, Element.Water };
					var elements = allElements.Where(el => this.IsElementVisible(el)).ToArray();
					
					if (elements.Length == 0)
					{
						ImGui.TextWrapped("No elements selected in filters. Check config to enable elements.");
					}
				else
				{
					var colCount = 2 + elements.Length; // retainer name + job/level + element columns
					var tableId = $"retainers_table_{i}";
					if (ImGui.BeginTable(tableId, colCount, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
					{
						ImGui.TableSetupColumn("Retainer", ImGuiTableColumnFlags.WidthFixed, ConfigStatic.RetainerNameColumnWidth);
						ImGui.TableSetupColumn("Job", ImGuiTableColumnFlags.WidthFixed, ConfigStatic.JobLevelColumnWidth);
						foreach (var el in elements)
						{
							ImGui.TableSetupColumn(el.ToString(), ImGuiTableColumnFlags.WidthStretch);
						}

						ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
						ImGui.TableSetColumnIndex(0);
						ImGui.TableHeader("Retainer");
						ImGui.TableSetColumnIndex(1);
					ImGui.TableHeader("Job");
						for (int col = 0; col < elements.Length; ++col)
						{
							ImGui.TableSetColumnIndex(2 + col);
							var headerText = elements[col].ToString();
							var textSize = ImGui.CalcTextSize(headerText);
							var cellWidth = ImGui.GetContentRegionAvail().X;
							var offset = (cellWidth - textSize.X) * 0.5f;
							if (offset > 0)
								ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
							ImGui.TableHeader(headerText);
						}

						for (int j = 0; j < c.Retainers.Count; ++j)
						{
						var r = c.Retainers[j];

						ImGui.TableNextRow();
						ImGui.TableSetColumnIndex(0);
						
						// Display checkbox before retainer name if auto venture is enabled
						if (this.plugin.Config.AutoVentureEnabled)
						{
							// Only show checkbox for gathering retainers (MIN=16, BTN=17, FSH=18)
							bool isGatheringRetainer = r.Job.HasValue && (r.Job.Value == 16 || r.Job.Value == 17 || r.Job.Value == 18);
							
							if (isGatheringRetainer)
							{
								bool enableAutoVenture = r.EnableAutoVenture;
								if (ImGui.Checkbox($"##auto_venture_{r.atid}", ref enableAutoVenture))
								{
									r.EnableAutoVenture = enableAutoVenture;
									this.plugin.PluginInterface.SavePluginConfig(this.plugin.Config);
								}
								if (ImGui.IsItemHovered())
								{
									ImGui.SetTooltip(enableAutoVenture ? "Auto crystal venture enabled for this retainer" : "Auto crystal venture disabled for this retainer");
								}
							}
							else
							{
								// Add invisible checkbox to maintain spacing
								ImGui.Dummy(new System.Numerics.Vector2(ImGui.GetFrameHeight(), ImGui.GetFrameHeight()));
							}
							ImGui.SameLine();
						}
						
						// Display retainer name
						ImGui.TextUnformatted($"{r.Name}");
						
						ImGui.TableSetColumnIndex(1);
						
						// Display job icon and level
						uint iconId = r.Job.HasValue && r.Job.Value > 0 ? 062100u + (uint)r.Job.Value : 62143u;
						var iconTexture = this.textureProvider.GetFromGameIcon(new Dalamud.Interface.Textures.GameIconLookup(iconId)).GetWrapOrDefault();
						if (iconTexture != null)
						{
							ImGui.Image(iconTexture.Handle, new System.Numerics.Vector2(24, 24));
						}
						else
						{
							ImGui.Dummy(new System.Numerics.Vector2(24, 24));
						}
						
						ImGui.SameLine(0, 2);
						ImGui.TextUnformatted($"Lvl {r.Level}");
						
						for (int col = 0; col < elements.Length; ++col)
						{
							ImGui.TableSetColumnIndex(2 + col);
							var el = elements[col];
							long shard = r.Inventory?.GetCount(el, CrystalType.Shard) ?? 0;
							long crystal = r.Inventory?.GetCount(el, CrystalType.Crystal) ?? 0;
							long cluster = r.Inventory?.GetCount(el, CrystalType.Cluster) ?? 0;
							
							// Calculate width for centering
							var displayText = this.FormatCrystalCounts(shard, crystal, cluster);
							var textSize = ImGui.CalcTextSize(displayText);
							var cellWidth = ImGui.GetContentRegionAvail().X;
							var offset = (cellWidth - textSize.X) * 0.5f;
							if (offset > 0)
								ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
							
							// Render with color
							this.RenderColoredCrystalCounts(shard, crystal, cluster);
						}
					}

					ImGui.EndTable();
						}
					}
				}
			}
		}
	}
}
