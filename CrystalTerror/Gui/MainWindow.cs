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

	private string FormatCrystalCounts(long shard, long crystal, long cluster)
	{
		var cfg = this.plugin.Config;
		var parts = new List<string>();
		
		if (cfg.ShowShards)
			parts.Add(shard.ToString());
		if (cfg.ShowCrystals)
			parts.Add(crystal.ToString());
		if (cfg.ShowClusters)
			parts.Add(cluster.ToString());
		
		return parts.Count > 0 ? string.Join("/", parts) : "-";
	}

	private List<StoredCharacter> GetSortedCharacters()
	{
		var characters = this.plugin.Characters.ToList();
		
		return this.plugin.Config.CharacterSortOption switch
		{
			CharacterSortOptions.Alphabetical => characters.OrderBy(c => c.Name).ToList(),
			CharacterSortOptions.ReverseAlphabetical => characters.OrderByDescending(c => c.Name).ToList(),
			CharacterSortOptions.World => characters.OrderBy(c => c.World).ThenBy(c => c.Name).ToList(),
			CharacterSortOptions.ReverseWorld => characters.OrderByDescending(c => c.World).ThenByDescending(c => c.Name).ToList(),
			CharacterSortOptions.AutoRetainer => characters, // Keep original order (assumed to be AutoRetainer order)
			CharacterSortOptions.Custom => characters, // Keep original order (user-defined)
			_ => characters
		};
	}

	public override void Draw()
	{
		if (this.plugin.Characters.Count == 0)
		{
			ImGui.TextWrapped("No characters imported yet. Open settings to import characters.");
			return;
		}

		var sortedCharacters = this.GetSortedCharacters();
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
			
			var header = $"{c.Name} @ {c.World} — Retainers: {c.Retainers?.Count ?? 0}";
			if (ImGui.CollapsingHeader(header))
			{
				ImGui.TextUnformatted($"LastUpdateUtc: {c.LastUpdateUtc:u}");
				ImGui.Separator();

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
						ImGui.TextUnformatted(c.World);
						for (int col = 0; col < elements.Length; ++col)
						{
							ImGui.TableSetColumnIndex(2 + col);
							var el = elements[col];
							var displayText = this.FormatCrystalCounts(
								c.Inventory.GetCount(el, CrystalType.Shard),
								c.Inventory.GetCount(el, CrystalType.Crystal),
								c.Inventory.GetCount(el, CrystalType.Cluster));
							
							// Center the text in the cell
							var textSize = ImGui.CalcTextSize(displayText);
							var cellWidth = ImGui.GetContentRegionAvail().X;
							var offset = (cellWidth - textSize.X) * 0.5f;
							if (offset > 0)
								ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
							ImGui.TextUnformatted(displayText);
						}							ImGui.EndTable();
						}
					}
					ImGui.Separator();
				}

				if (c.Retainers == null || c.Retainers.Count == 0)
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
									var displayText = this.FormatCrystalCounts(shard, crystal, cluster);
									
									// Center the text in the cell
									var textSize = ImGui.CalcTextSize(displayText);
									var cellWidth = ImGui.GetContentRegionAvail().X;
									var offset = (cellWidth - textSize.X) * 0.5f;
									if (offset > 0)
										ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
									ImGui.TextUnformatted(displayText);
								}
							}

							ImGui.EndTable();
						}
						
						// Dummy table for sizing reference
						ImGui.Spacing();
						var dummyTableId = $"dummy_table_{i}";
						if (ImGui.BeginTable(dummyTableId, colCount, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
						{
							ImGui.TableSetupColumn("Retainer", ImGuiTableColumnFlags.WidthFixed, ConfigStatic.RetainerNameColumnWidth);
							ImGui.TableSetupColumn("Job", ImGuiTableColumnFlags.WidthFixed, ConfigStatic.JobLevelColumnWidth);
							foreach (var el in elements)
							{
								ImGui.TableSetupColumn(el.ToString(), ImGuiTableColumnFlags.WidthStretch);
							}
							
							ImGui.TableHeadersRow();
							
							ImGui.TableNextRow();
							ImGui.TableSetColumnIndex(0);
							
							// Display checkbox before retainer name if auto venture is enabled
							if (this.plugin.Config.AutoVentureEnabled)
							{
								bool dummyCheckbox = false;
								ImGui.Checkbox("##dummy_venture", ref dummyCheckbox);
								ImGui.SameLine();
							}
							
							ImGui.TextUnformatted("aaaaaaaaaaaaaaa");
							ImGui.TableSetColumnIndex(1);
							ImGui.TextUnformatted("Dummy");
							
							ImGui.EndTable();
						}
					}
				}
			}
		}
	}
}
