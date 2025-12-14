namespace CrystalTerror.Gui.MainWindow;

using System;
using System.Collections.Generic;
using System.Linq;
using CrystalTerror.Helpers;
using CrystalTerror.Gui.Common;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using ImGui = Dalamud.Bindings.ImGui.ImGui;

/// <summary>
/// Component for rendering the retainer inventory table.
/// </summary>
public class RetainerTableComponent : IUIComponent
{
	private readonly CrystalTerrorPlugin plugin;
	private readonly CrystalCountsUtility countsUtility;
	private readonly StoredCharacter character;
	private readonly int characterIndex;
	private readonly ITextureProvider textureProvider;
	private readonly bool showIgnored;

	public RetainerTableComponent(CrystalTerrorPlugin plugin, CrystalCountsUtility countsUtility, StoredCharacter character, int characterIndex, ITextureProvider textureProvider, bool showIgnored)
	{
		this.plugin = plugin;
		this.countsUtility = countsUtility;
		this.character = character;
		this.characterIndex = characterIndex;
		this.textureProvider = textureProvider;
		this.showIgnored = showIgnored;
	}

	public void Render()
	{
		if (character?.Retainers == null || character.Retainers.Count == 0)
		{
			ImGui.TextWrapped("No retainers for this character.");
			return;
		}

		// Render inventory aggregated by Element. Each element column shows "shard/crystal/cluster".
		var allElements = new[] { Element.Fire, Element.Ice, Element.Wind, Element.Earth, Element.Lightning, Element.Water };
		var elements = allElements.Where(el => this.countsUtility.IsElementVisible(el)).ToArray();
		
		if (elements.Length == 0)
		{
			ImGui.TextWrapped("No elements selected in filters. Check config to enable elements.");
			return;
		}

		var colCount = 2 + elements.Length; // retainer name + job/level + element columns
		var tableId = $"retainers_table_{characterIndex}";
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

			var visibleRetainers = showIgnored ? character.Retainers.ToList() : character.Retainers.Where(r => !r.IsIgnored).ToList();
			for (int j = 0; j < visibleRetainers.Count; ++j)
			{
				var r = visibleRetainers[j];

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
						if (ImGui.Checkbox($"##auto_venture_{r.Atid}", ref enableAutoVenture))
						{
							r.EnableAutoVenture = enableAutoVenture;
							ConfigHelper.Save(this.plugin.Config);
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
				if (ImGui.BeginPopupContextItem($"ret_ctx_{characterIndex}_{j}"))
				{
					if (ImGui.MenuItem("Reset retainer inventory"))
					{
						r.Inventory?.Reset();
						ConfigHelper.SaveAndSync(this.plugin.Config, this.plugin.Characters);
					}
					if (!r.IsIgnored)
					{
						if (ImGui.MenuItem("Ignore retainer"))
						{
							r.IsIgnored = true;
							// IsIgnored setter ensures EnableAutoVenture is disabled
							ConfigHelper.SaveAndSync(this.plugin.Config, this.plugin.Characters);
						}
					}
					else
					{
						if (ImGui.MenuItem("Unignore retainer"))
						{
							r.IsIgnored = false;
							// Caller may re-enable AutoVenture manually from UI if desired
							ConfigHelper.SaveAndSync(this.plugin.Config, this.plugin.Characters);
						}
					}
					ImGui.EndPopup();
				}
				
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
					var displayText = this.countsUtility.FormatCrystalCounts(shard, crystal, cluster);
					var textSize = ImGui.CalcTextSize(displayText);
					var cellWidth = ImGui.GetContentRegionAvail().X;
					var offset = (cellWidth - textSize.X) * 0.5f;
					if (offset > 0)
						ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
					
					// Render with color
					this.countsUtility.RenderColoredCrystalCounts(shard, crystal, cluster);
				}
			}

			ImGui.EndTable();
		}
	}
}
