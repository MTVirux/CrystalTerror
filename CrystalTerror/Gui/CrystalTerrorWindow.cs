using System;
using OtterGui;
using OtterGui.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using OtterGui.Text;
using Dalamud.Plugin;
// Use dynamic for client state to avoid a hard dependency on the Dalamud client state interface

namespace CrystalTerror.Gui
{
    public class CrystalTerrorWindow : Window, IDisposable
    {
        private bool disposed;
        private readonly PluginConfig config;
        private readonly dynamic? clientState;
        private readonly Func<(string? Name, string? World)>? getPlayerFunc;
        private readonly IDalamudPluginInterface pluginInterface;
        public Action? RequestOpenConfig;
        public CrystalTerrorWindow(PluginConfig config, dynamic? clientState, Func<(string? Name, string? World)>? getPlayerFunc = null, IDalamudPluginInterface? pluginInterface = null)
            : base("CrystalTerror###CrystalTerrorMainWindow")
        {
            this.config = config;
            this.clientState = clientState;
            this.getPlayerFunc = getPlayerFunc;
            this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
            SizeConstraints = new WindowSizeConstraints()
            {
                MinimumSize = new System.Numerics.Vector2(300, 120),
                MaximumSize = new System.Numerics.Vector2(9999, 9999),
            };
            
            // Add a cog to the title bar that opens the config when clicked
            try
            {
                TitleBarButtons.Add(new()
                {
                    Click = (m) => { if (m == ImGuiMouseButton.Left) this.RequestOpenConfig?.Invoke(); },
                    Icon = (Dalamud.Interface.FontAwesomeIcon)FontAwesomeIcon.Cog,
                    IconOffset = new(2, 2),
                    ShowTooltip = () => ImGui.SetTooltip("Open settings window"),
                });
            }
            catch
            {
                // If TitleBarButtons or related types are not available, silently ignore.
            }

        }

        public override void PreDraw()
        {
            // customize window title or flags here if needed
        }

        public override void Draw()
        {

            // Logged-in character. Prefer plugin-provided values (kept up-to-date by OnFrameworkUpdate),
            // otherwise fall back to reflective reading of clientState.
            var playerName = "(none)";
            var playerWorld = "(unknown)";

            if (this.getPlayerFunc != null)
            {
                try
                {
                    var tup = this.getPlayerFunc();
                    if (!string.IsNullOrEmpty(tup.Name))
                        playerName = tup.Name;
                    if (!string.IsNullOrEmpty(tup.World))
                        playerWorld = tup.World;
                }
                catch
                {
                    // swallow; will attempt reflection below
                }
            }

            if ((playerName == "(none)" || playerWorld == "(unknown)") && this.clientState != null)
            {
                try
                {
                    var clientType = this.clientState.GetType();
                    var localPlayerProp = clientType.GetProperty("LocalPlayer");
                    var localPlayer = localPlayerProp?.GetValue(this.clientState);
                    if (localPlayer != null)
                    {
                        var nameProp = localPlayer.GetType().GetProperty("Name");
                        var nameVal = nameProp?.GetValue(localPlayer);
                        if (!string.IsNullOrEmpty(nameVal?.ToString()))
                            playerName = nameVal?.ToString() ?? playerName;

                        var homeWorldProp = localPlayer.GetType().GetProperty("HomeWorld");
                        if (homeWorldProp != null)
                        {
                            var hw = homeWorldProp.GetValue(localPlayer);
                            if (hw != null)
                            {
                                string? resolved = null;
                                var hwNameProp = hw.GetType().GetProperty("Name");
                                if (hwNameProp != null)
                                {
                                    var hwVal = hwNameProp.GetValue(hw);
                                    resolved = hwVal?.ToString();
                                }

                                if (string.IsNullOrEmpty(resolved))
                                {
                                    var valueProp = hw.GetType().GetProperty("Value");
                                    if (valueProp != null)
                                    {
                                        var inner = valueProp.GetValue(hw);
                                        if (inner != null)
                                        {
                                            var innerNameProp = inner.GetType().GetProperty("Name");
                                            if (innerNameProp != null)
                                            {
                                                var innerName = innerNameProp.GetValue(inner);
                                                resolved = innerName?.ToString();
                                            }
                                            else
                                            {
                                                resolved = inner.ToString();
                                            }
                                        }
                                    }
                                }

                                if (!string.IsNullOrEmpty(resolved))
                                    playerWorld = resolved;
                                else if (!string.IsNullOrEmpty(hw.ToString()))
                                    playerWorld = hw.ToString();
                            }
                        }
                        else
                        {
                            var worldProp = localPlayer.GetType().GetProperty("World");
                            var worldVal = worldProp?.GetValue(localPlayer);
                            if (worldVal != null && !string.IsNullOrEmpty(worldVal.ToString()))
                                playerWorld = worldVal.ToString() ?? playerWorld;
                        }
                    }
                }
                catch
                {
                    // ignore reflection errors
                }
            }

            ImGui.Spacing();
            ImGui.Bullet(); ImGui.TextUnformatted($"Logged in character: {playerName}@{playerWorld}");

            ImGui.Spacing();
            ImGui.TextUnformatted("Saved characters:");
            ImGui.Spacing();
            if (this.config?.Characters != null)
            {
                for (var i = 0; i < this.config.Characters.Count; ++i)
                {
                    var c = this.config.Characters[i];
                    var header = $"{c.Name}@{c.World}";

                    ImGui.PushID(i);
                    if (this.config.EditMode)
                    {
                        var isFirst = i == 0;
                        var isLast = i == this.config.Characters.Count - 1;

                        ImGui.BeginDisabled(isFirst);
                        if (ImGui.Button("↑"))
                        {
                            if (!isFirst)
                            {
                                var tmp = this.config.Characters[i - 1];
                                this.config.Characters[i - 1] = this.config.Characters[i];
                                this.config.Characters[i] = tmp;
                                try
                                {
                                    this.pluginInterface.SavePluginConfig(this.config);
                                }
                                catch
                                {
                                    // ignore save errors
                                }
                            }
                        }
                        ImGui.EndDisabled();
                        ImGui.SameLine();

                        ImGui.BeginDisabled(isLast);
                        if (ImGui.Button("↓"))
                        {
                            if (!isLast)
                            {
                                var tmp = this.config.Characters[i + 1];
                                this.config.Characters[i + 1] = this.config.Characters[i];
                                this.config.Characters[i] = tmp;
                                try
                                {
                                    this.pluginInterface.SavePluginConfig(this.config);
                                }
                                catch
                                {
                                    // ignore save errors
                                }
                            }
                        }
                        ImGui.EndDisabled();
                        ImGui.SameLine();
                    }

                    if (ImGui.CollapsingHeader(header))
                    {
                        ImGui.Indent();
                        ImGui.TextUnformatted($"Last update (UTC): {c.LastUpdateUtc:u}");
                        ImGui.TextUnformatted($"Retainers: {c.Retainers?.Count ?? 0}");
                        ImGui.Unindent();
                    }

                    ImGui.PopID();
                }
            }
        }

        public override void OnClose()
        {
            // save ephemeral state if necessary
        }

        public void Dispose()
        {
            if (this.disposed)
                return;

            this.disposed = true;
        }
    }
}
