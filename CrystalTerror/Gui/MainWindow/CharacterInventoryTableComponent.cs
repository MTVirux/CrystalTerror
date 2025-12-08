namespace CrystalTerror.Gui.MainWindow;

using System;
using System.Linq;
using Dalamud.Bindings.ImGui;
using CrystalTerror.Gui.Common;
using ImGui = Dalamud.Bindings.ImGui.ImGui;

/// <summary>
/// Component for rendering the character inventory table.
/// </summary>
public class CharacterInventoryTableComponent : IUIComponent
{
	private readonly CrystalTerrorPlugin plugin;
	private readonly CrystalCountsUtility countsUtility;
	private readonly StoredCharacter character;
	private readonly int characterIndex;

	public CharacterInventoryTableComponent(CrystalTerrorPlugin plugin, CrystalCountsUtility countsUtility, StoredCharacter character, int characterIndex)
	{
		this.plugin = plugin;
		this.countsUtility = countsUtility;
		this.character = character;
		this.characterIndex = characterIndex;
	}

	public void Render()
	{
		if (character?.Inventory == null)
			return;

		ImGui.Text("Character Inventory:");
		var allElements = new[] { Element.Fire, Element.Ice, Element.Wind, Element.Lightning, Element.Earth, Element.Water };
		var elements = allElements.Where(el => this.countsUtility.IsElementVisible(el)).ToArray();
		
		if (elements.Length == 0)
		{
			ImGui.TextWrapped("No elements selected in filters. Check config to enable elements.");
			return;
		}

		var colCount = 2 + elements.Length;
		var charTableId = $"char_inventory_table_{characterIndex}";
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
			ImGui.TextUnformatted(character.Name);
			ImGui.TableSetColumnIndex(1);
			{
				var worldText = character.World;
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
				long shard = character.Inventory.GetCount(el, CrystalType.Shard);
				long crystal = character.Inventory.GetCount(el, CrystalType.Crystal);
				long cluster = character.Inventory.GetCount(el, CrystalType.Cluster);
				
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
			ImGui.EndTable();
		}
	}
}
