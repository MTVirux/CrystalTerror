namespace CrystalTerror.Gui.MainWindow;

using Dalamud.Bindings.ImGui;
using CrystalTerror.Gui.Common;
using System.Collections.Generic;
using CrystalTerror;
using OtterGui;
using ImGui = Dalamud.Bindings.ImGui.ImGui;

/// <summary>
/// Component for rendering the character filter/search box and related UI controls.
/// Internally uses `OtterGui.ItemSelector`'s filter input but only exposes the
/// filter text (no item list) so the existing container can perform filtering.
/// </summary>
public class CharacterFilterComponent : IUIComponent
{
	private readonly CrystalTerrorPlugin plugin;
	private readonly FilterOnlySelector selector;

	public string FilterText
	{
		get => selector.FilterText ?? string.Empty;
		set
		{
			selector.FilterText = value ?? string.Empty;
			selector.SetFilterDirty();
		}
	}

	public CharacterFilterComponent(CrystalTerrorPlugin plugin)
	{
		this.plugin = plugin;
		// Provide an empty backing list so the ItemSelector only renders the filter input.
		this.selector = new FilterOnlySelector(new List<StoredCharacter>()) { Label = "##ct_filter_selector" };
	}

	public void Render()
	{
		// Render a custom input so we can provide a descriptive hint.
		var hint = "Search for a character";
		ImGui.PushItemWidth(-1);
		var temp = this.selector.FilterText ?? string.Empty;
		if (ImGui.InputTextWithHint("##ct_filter", hint, ref temp, 256))
		{
			this.selector.FilterText = temp;
			this.selector.SetFilterDirty();
		}
		ImGui.PopItemWidth();
	}

	// Small subclass exposing the protected Filter from ItemSelector.
	private sealed class FilterOnlySelector : ItemSelector<StoredCharacter>
	{
		public FilterOnlySelector(IList<StoredCharacter> items) : base(items, Flags.Filter)
		{
		}

		// Expose the protected filter string for external use.
		public string? FilterText
		{
			get => base.Filter;
			set => base.Filter = value ?? string.Empty;
		}

		// Minimal overrides: we won't be drawing any items (empty list), but implement required methods.
		protected override bool OnDraw(int idx) => false;
		protected override bool Filtered(int idx) => true;
		protected override bool OnDelete(int idx) => false;
		protected override bool OnAdd(string name) => false;
		protected override bool OnMove(int idx1, int idx2) => false;
		protected override bool OnClipboardImport(string name, string data) => false;
		protected override bool OnDuplicate(string name, int idx) => false;
		protected override void OnDrop(object? data, int idx) { }
	}
}
