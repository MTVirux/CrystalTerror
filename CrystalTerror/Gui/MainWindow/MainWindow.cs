namespace CrystalTerror.Gui.MainWindow;

using System;
using System.Collections.Generic;
using System.Linq;
using CrystalTerror.Helpers;
using CrystalTerror.Gui.Common;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using ImGui = Dalamud.Bindings.ImGui.ImGui;

public class MainWindow : Window, IDisposable
{
	private static string GetTitleWithVersion()
	{
		try
		{
			var ver = typeof(CrystalTerrorPlugin).Assembly.GetName().Version?.ToString() ?? string.Empty;

			string title;

			#if DEBUG
				title = string.IsNullOrEmpty(ver) ? "Crystal Terror - v??????? [TESTING]" : $"Crystal Terror  -  v{ver} [TESTING]";
			#else
				title = string.IsNullOrEmpty(ver) ? "Crystal Terror - v???????" : $"Crystal Terror  -  v{ver}";
			#endif

			return title;
		}
		catch
		{
			return "Crystal Terror";
		}
	}
	private readonly CrystalTerrorPlugin plugin;
	private WindowLockButtonComponent? lockButtonComponent;
	private TitleBarButton? lockButton;
	private readonly ITextureProvider textureProvider;
	
	// UI Components
	private CharacterFilterComponent? filterComponent;
	private CrystalCountsUtility? countsUtility;
	private MainWindowContainerComponent? containerComponent;

	public MainWindow(CrystalTerrorPlugin plugin, ITextureProvider textureProvider)
		: base(GetTitleWithVersion())
	{
		this.plugin = plugin;
		this.textureProvider = textureProvider;
		this.SizeConstraints = new WindowSizeConstraints()
		{
			MinimumSize = new System.Numerics.Vector2(300, 100),
			MaximumSize = ImGui.GetIO().DisplaySize,
		};

		// Initialize UI components
		this.filterComponent = new CharacterFilterComponent(plugin);
		this.countsUtility = new CrystalCountsUtility(plugin);
		this.containerComponent = new MainWindowContainerComponent(plugin, this.countsUtility, this.filterComponent, textureProvider);

		// Initialize lock button component
		this.lockButtonComponent = new WindowLockButtonComponent(plugin, isConfigWindow: false);

		// Create and add title bar buttons
		TitleBarButtons.Add(new TitleBarButton
		{
			Click = (m) => { if (m == ImGuiMouseButton.Left) plugin.OpenConfigUi(); },
			Icon = FontAwesomeIcon.Cog,
			IconOffset = new System.Numerics.Vector2(2, 2),
			ShowTooltip = () => ImGui.SetTooltip("Open settings"),
		});

		// Create lock button with component's click handler.
		// Use a local TitleBarButton so we can update its Icon immediately after clicks.
		var lockTb = new TitleBarButton
		{
			Icon = this.lockButtonComponent.CurrentIcon,
			IconOffset = new System.Numerics.Vector2(3, 2),
			ShowTooltip = () => ImGui.SetTooltip("Lock window position and size"),
		};

		lockTb.Click = (m) =>
		{
			this.lockButtonComponent.OnLockButtonClick(m);
			// Immediately refresh the icon to reflect the new state
			lockTb.Icon = this.lockButtonComponent.CurrentIcon;
		};

		lockButton = lockTb;
		TitleBarButtons.Add(lockButton);
	}

	public void Dispose()
	{
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

		// Ensure titlebar button icon reflects current configuration every frame
		if (this.lockButtonComponent != null && this.lockButton != null)
		{
			this.lockButton.Icon = this.lockButtonComponent.CurrentIcon;
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
		return this.countsUtility?.IsElementVisible(element) ?? true;
	}

	/// <summary>
	/// Invalidate the sorted characters cache. Call this when character data changes.
	/// </summary>
	public void InvalidateSortCache()
	{
		Services.LogService.Log.Debug("[MainWindow] Character sort cache invalidated");
		// Cache invalidation is now handled by the container component
	}

	public override void Draw()
	{
		if (this.containerComponent != null)
		{
			this.containerComponent.Render();
		}
	}
}
