namespace CrystalTerror.Gui;

using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using ImGui = Dalamud.Bindings.ImGui.ImGui;

public class MainWindow : Window, IDisposable
{
	private readonly CrystalTerrorPlugin plugin;

	public MainWindow(CrystalTerrorPlugin plugin)
		: base("CrystalTerrorMainWindow")
	{
		this.plugin = plugin;
		this.SizeConstraints = new WindowSizeConstraints()
		{
			MinimumSize = new System.Numerics.Vector2(300, 100),
			MaximumSize = ImGui.GetIO().DisplaySize,
		};
	}

	public void Dispose()
	{
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

		if (ImGui.Button("Open Config"))
		{
			this.plugin.OpenConfigUi();
		}

		ImGui.SameLine();
		ImGui.TextWrapped("(Import & purge moved to Config)");

		ImGui.Spacing();
		ImGui.Text("Characters:");
		ImGui.Separator();

		if (this.plugin.Characters.Count == 0)
		{
			ImGui.TextWrapped("No characters imported yet. Use the import buttons above.");
		}
		else
		{
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
							var colCount = 1 + elements.Length;
							var charTableId = $"char_inventory_table_{i}";
							if (ImGui.BeginTable(charTableId, colCount, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
							{
								ImGui.TableSetupColumn("Character (World)");
								foreach (var el in elements)
								{
									ImGui.TableSetupColumn(el.ToString());
								}

								ImGui.TableHeadersRow();

								// Character inventory row (format: shard/crystal/cluster per element)
								ImGui.TableNextRow();
								ImGui.TableSetColumnIndex(0);
								ImGui.TextUnformatted($"{c.Name} @ {c.World}");
								for (int col = 0; col < elements.Length; ++col)
								{
									ImGui.TableSetColumnIndex(1 + col);
									var el = elements[col];
									var displayText = this.FormatCrystalCounts(
										c.Inventory.GetCount(el, CrystalType.Shard),
										c.Inventory.GetCount(el, CrystalType.Crystal),
										c.Inventory.GetCount(el, CrystalType.Cluster));
									ImGui.TextUnformatted(displayText);
								}

								ImGui.EndTable();
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
							var colCount = 1 + elements.Length; // first column is retainer name+world
							var tableId = $"retainers_table_{i}";
							if (ImGui.BeginTable(tableId, colCount, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
							{
								ImGui.TableSetupColumn("Retainer (World)");
								foreach (var el in elements)
								{
									ImGui.TableSetupColumn(el.ToString());
								}

								ImGui.TableHeadersRow();

								for (int j = 0; j < c.Retainers.Count; ++j)
								{
									var r = c.Retainers[j];

									ImGui.TableNextRow();
									ImGui.TableSetColumnIndex(0);
									var jobAbbr = ClassJobExtensions.GetAbreviation(r.Job) ?? "?";
									ImGui.TextUnformatted($"{r.Name} @ {r.OwnerCharacter?.World}");
									ImGui.TextUnformatted($"  {jobAbbr} Lv.{r.Level} | Gathering: {r.Gathering}");

									for (int col = 0; col < elements.Length; ++col)
									{
										ImGui.TableSetColumnIndex(1 + col);
										var el = elements[col];
										long shard = r.Inventory?.GetCount(el, CrystalType.Shard) ?? 0;
										long crystal = r.Inventory?.GetCount(el, CrystalType.Crystal) ?? 0;
										long cluster = r.Inventory?.GetCount(el, CrystalType.Cluster) ?? 0;
										var displayText = this.FormatCrystalCounts(shard, crystal, cluster);
										ImGui.TextUnformatted(displayText);
									}
								}

								ImGui.EndTable();
							}
						}
					}
				}
			}
		}

		ImGui.Spacing();
		if (ImGui.Button("Close"))
		{
			this.IsOpen = false;
		}
	}
}
