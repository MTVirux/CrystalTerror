namespace CrystalTerror.Gui.MainWindow;

using System;
using System.Collections.Generic;
using System.Linq;
using CrystalTerror.Helpers;
using CrystalTerror.Gui.Common;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using ImGui = Dalamud.Bindings.ImGui.ImGui;

/// <summary>
/// Container component for the main window content.
/// Handles character display, filtering, sorting, and rendering of character/retainer data.
/// </summary>
public class MainWindowContainerComponent : IUIComponent
{
	private readonly CrystalTerrorPlugin plugin;
	private readonly CrystalCountsUtility countsUtility;
	private readonly CharacterFilterComponent filterComponent;
	private readonly ITextureProvider textureProvider;

	public MainWindowContainerComponent(CrystalTerrorPlugin plugin, CrystalCountsUtility countsUtility, CharacterFilterComponent filterComponent, ITextureProvider textureProvider)
	{
		this.plugin = plugin;
		this.countsUtility = countsUtility;
		this.filterComponent = filterComponent;
		this.textureProvider = textureProvider;
	}

	public void Render()
	{
		if (this.plugin.Characters.Count == 0)
		{
			ImGui.TextWrapped("No characters imported yet. Please Log In to a character to import them.");
			return;
		}

		// Render filter component
		this.filterComponent.Render();

		var trimmedFilter = this.filterComponent.FilterText?.Trim() ?? string.Empty;
		bool hasFilter = !string.IsNullOrEmpty(trimmedFilter);
		var filterLower = hasFilter ? trimmedFilter.ToLowerInvariant() : string.Empty;

		var sortedCharacters = this.GetSortedCharacters();

		// If CTRL is held, show ignored characters/retainers as well
		bool showIgnored = ImGui.GetIO().KeyCtrl;

		// Filter out characters that are explicitly ignored, unless CTRL is held
		if (!showIgnored)
		{
			sortedCharacters = sortedCharacters
				.Where(c => !c.IsIgnored)
				.ToList();
		}
		
		// Apply text filter on character name or world if provided
		if (hasFilter)
		{
			sortedCharacters = sortedCharacters
				.Where(c =>
					(!string.IsNullOrEmpty(c.Name) && c.Name.Contains(filterLower, StringComparison.OrdinalIgnoreCase)) ||
					(!string.IsNullOrEmpty(c.World) && c.World.Contains(filterLower, StringComparison.OrdinalIgnoreCase)))
				.ToList();
		}
		
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
				RenderCharacterMoveButtons(i, sortedCharacters.Count, c);
			}

			// Render character header component
			var headerComponent = new CharacterHeaderComponent(this.plugin, this.countsUtility, c, i);
			headerComponent.Render();

			if (headerComponent.HeaderOpen)
			{
				#if DEBUG
				ImGui.TextUnformatted($"LastUpdateUtc: {c.LastUpdateUtc:u}");
				ImGui.Separator();
				#endif
				
				// Render character inventory table component
				var charInventoryComponent = new CharacterInventoryTableComponent(this.plugin, this.countsUtility, c, i);
				charInventoryComponent.Render();
				ImGui.Separator();
				
				// Render retainer table component
				var retainerTableComponent = new RetainerTableComponent(this.plugin, this.countsUtility, c, i, this.textureProvider, showIgnored);
				retainerTableComponent.Render();
			}
		}
	}

	private void RenderCharacterMoveButtons(int i, int totalCount, StoredCharacter c)
	{
		var canMoveUp = i > 0;
		var canMoveDown = i < totalCount - 1;
		
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
				ConfigHelper.SaveAndSync(this.plugin.Config, this.plugin.Characters);
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
				ConfigHelper.SaveAndSync(this.plugin.Config, this.plugin.Characters);
			}
		}
		if (!canMoveDown)
		{
			ImGui.EndDisabled();
		}
		ImGui.SameLine();
	}

	private List<StoredCharacter> GetSortedCharacters()
	{
		var characters = this.plugin.Characters.ToList();
		var currentSortOption = this.plugin.Config.CharacterSortOption;
		
		// Cache is handled by the main window, but we still need to sort here
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
		
		return sorted;
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
}
