namespace CrystalTerror.Gui.Common;

/// <summary>
/// Base interface for all UI components. Each component is responsible for rendering
/// its own visual elements within the ImGui context.
/// </summary>
public interface IUIComponent
{
	/// <summary>
	/// Renders the component. Should be called once per frame during the parent window's Draw() method.
	/// </summary>
	void Render();
}
