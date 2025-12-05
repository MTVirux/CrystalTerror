namespace CrystalTerror.Gui;

using System;
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

	public override void Draw()
	{
		ImGui.Text("Crystal Terror");
		ImGui.Separator();


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
			for (int i = 0; i < this.plugin.Characters.Count; ++i)
			{
				var c = this.plugin.Characters[i];
				var header = $"{c.Name} @ {c.World} — Retainers: {c.Retainers?.Count ?? 0}";
				if (ImGui.CollapsingHeader(header))
				{
					ImGui.TextUnformatted($"LastUpdateUtc: {c.LastUpdateUtc:u}");
					ImGui.Separator();
					if (c.Retainers == null || c.Retainers.Count == 0)
					{
						ImGui.TextWrapped("No retainers for this character.");
					}
					else
					{
						// Render inventory aggregated by Element. Each element column shows "shard/crystal/cluster".
						var elements = new[] { Element.Fire, Element.Ice, Element.Wind, Element.Lightning, Element.Earth, Element.Water };
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
								ImGui.TextUnformatted($"{r.Name} @ {r.OwnerCharacter?.World}");

								for (int col = 0; col < elements.Length; ++col)
								{
									ImGui.TableSetColumnIndex(1 + col);
									var el = elements[col];
									long shard = r.Inventory?.GetCount(el, CrystalType.Shard) ?? 0;
									long crystal = r.Inventory?.GetCount(el, CrystalType.Crystal) ?? 0;
									long cluster = r.Inventory?.GetCount(el, CrystalType.Cluster) ?? 0;
									ImGui.TextUnformatted($"{shard}/{crystal}/{cluster}");
								}
							}

							ImGui.EndTable();
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
