namespace CrystalTerror.Gui;

using System;
using Dalamud.Interface.Windowing;
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

		ImGui.TextWrapped("A small window for the CrystalTerror plugin. Add UI here.");

		if (ImGui.Button("Open Config"))
		{
			this.plugin.OpenConfigUi();
		}

		ImGui.Spacing();
		if (ImGui.Button("Close"))
		{
			this.IsOpen = false;
		}
	}
}
