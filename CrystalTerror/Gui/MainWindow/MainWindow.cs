namespace CrystalTerror.Gui.MainWindow;

using System;
using System.Collections.Generic;
using System.Linq;
using CrystalTerror.Helpers;
using CrystalTerror.Gui.Common;
using Dalamud.Interface.Windowing;
using Dalamud.Interface;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;
using ImGui = Dalamud.Bindings.ImGui.ImGui;
using ECommons.ImGuiMethods;

public class MainWindow : Window, IDisposable
{
	private static string GetTitleWithVersion()
	{
		try
		{
			var ver = typeof(CrystalTerrorPlugin).Assembly.GetName().Version?.ToString() ?? string.Empty;

			string title;

			#if DEBUG
				if (string.IsNullOrEmpty(ver))
				{
					title = "Crystal Terror - v??????? [TESTING]";
				}
				else
				{
					var parts = ver.Split('.');
					if (parts.Length > 0)
					{
						var last = parts[parts.Length - 1];
						if (int.TryParse(last, out var n))
						{
							parts[parts.Length - 1] = (n + 1).ToString();
						}
					}

					var verDbg = string.Join('.', parts);
					title = $"Crystal Terror  -  v{verDbg} [TESTING]";
				}
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
	private readonly ITextureProvider textureProvider;
	
	// UI Components - now using the new modular panel
	private CrystalCountsUtility? countsUtility;
	private CharacterListPanel? characterListPanel;
	
	// Lock button reference for dynamic icon updates
	private TitleBarButton? lockButton;

	public MainWindow(CrystalTerrorPlugin plugin, ITextureProvider textureProvider)
		: base(GetTitleWithVersion())
	{
		this.plugin = plugin;
		this.textureProvider = textureProvider;
		this.SizeConstraints = new WindowSizeConstraints()
		{
			MinimumSize = new System.Numerics.Vector2(400, 200),
			MaximumSize = new System.Numerics.Vector2(2000, 2000),
		};

		// Initialize UI components
		this.countsUtility = new CrystalCountsUtility(plugin);
		this.characterListPanel = new CharacterListPanel(plugin, this.countsUtility, textureProvider);

		// Create and add title bar buttons
		TitleBarButtons.Add(new TitleBarButton
		{
			Click = (m) => { if (m == ImGuiMouseButton.Left) plugin.OpenConfigUi(); },
			Icon = FontAwesomeIcon.Cog,
			IconOffset = new System.Numerics.Vector2(2, 2),
			ShowTooltip = () => ImGui.SetTooltip("Open settings"),
		});

		// Lock/pin button
		lockButton = new TitleBarButton
		{
			Icon = plugin.Config.PinMainWindow ? FontAwesomeIcon.Lock : FontAwesomeIcon.LockOpen,
			IconOffset = new System.Numerics.Vector2(3, 2),
			ShowTooltip = () => ImGui.SetTooltip("Lock window position and size"),
		};
		lockButton.Click = (m) => 
		{
			if (m == ImGuiMouseButton.Left)
			{
				if (!plugin.Config.PinMainWindow)
				{
					// About to lock - save current position/size
					plugin.Config.MainWindowPos = ImGui.GetWindowPos();
					plugin.Config.MainWindowSize = ImGui.GetWindowSize();
				}
				// Toggle pin state
				plugin.Config.PinMainWindow = !plugin.Config.PinMainWindow;
				ConfigHelper.SaveAndSync(plugin.Config, plugin.Characters);
				lockButton!.Icon = plugin.Config.PinMainWindow ? FontAwesomeIcon.Lock : FontAwesomeIcon.LockOpen;
			}
		};
		TitleBarButtons.Add(lockButton);
	}

	public void Dispose()
	{
	}

	public override void PreDraw()
	{
		static bool IsValidVec(System.Numerics.Vector2 v)
		{
			return !(float.IsNaN(v.X) || float.IsNaN(v.Y)) && v.X > 1f && v.Y > 1f;
		}

		if (this.plugin.Config.PinMainWindow)
		{
			// Prevent user from moving/resizing when pinned
			Flags |= ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize;

			// Only apply saved pos/size if they look valid (avoid 0,0 sentinel)
			if (IsValidVec(this.plugin.Config.MainWindowPos) && IsValidVec(this.plugin.Config.MainWindowSize))
			{
				ImGui.SetNextWindowPos(this.plugin.Config.MainWindowPos);
				ImGui.SetNextWindowSize(this.plugin.Config.MainWindowSize);
			}
		}
		else
		{
			Flags &= ~ImGuiWindowFlags.NoMove;
			Flags &= ~ImGuiWindowFlags.NoResize;
		}

		// Update lock button icon to reflect current state
		if (lockButton != null)
		{
			lockButton.Icon = plugin.Config.PinMainWindow ? FontAwesomeIcon.Lock : FontAwesomeIcon.LockOpen;
		}

		RespectCloseHotkey = !this.plugin.Config.IgnoreEscapeOnMainWindow;
	}

	public override void PostDraw()
	{
		// Position/size is saved only when the user locks the window (in the lock button click handler)
		// Not continuously tracking position when locked - this matches Kaleidoscope's implementation
	}

	/// <summary>
	/// Invalidate the sorted characters cache. Call this when character data changes.
	/// </summary>
	public void InvalidateSortCache()
	{
		Svc.Log.Debug("[MainWindow] Character sort cache invalidated");
	}

	public override void Draw()
	{
		this.characterListPanel?.Render();
	}
}
