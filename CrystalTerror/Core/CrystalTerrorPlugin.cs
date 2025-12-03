// Auto-generated simple plugin entry to host the main window
using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Game.Gui;
using Dalamud.Game.ClientState;
using Dalamud.Game.Text.SeStringHandling; 

namespace CrystalTerror
{
    public class CrystalTerrorPlugin : IDalamudPlugin, IDisposable
    {
        private readonly WindowSystem windowSystem = new(typeof(CrystalTerrorPlugin).AssemblyQualifiedName);
        private readonly Gui.CrystalTerrorWindow mainWindow;
        private readonly Gui.ConfigWindow configWindow;
        private readonly PluginConfig config;
        private readonly IFramework framework;
        private readonly IClientState clientState;
        private string? lastSeenName;
        private string? lastSeenWorld;
        private bool isDisposed;

        public IDalamudPluginInterface PluginInterface { get; init; }
        public ICommandManager CommandManager { get; init; }
        public IPluginLog Log { get; init; }

        public CrystalTerrorPlugin(IDalamudPluginInterface pluginInterface,
            ICommandManager commandManager,
            IFramework framework,
            IClientState clientState,
            IGameGui gameGui,
            IChatGui chatGui,
            IPluginLog log,
            IDataManager dataManager,
            ITextureProvider textureProvider)
        {
            this.PluginInterface = pluginInterface;
            this.CommandManager = commandManager;
            this.Log = log;

            // load or create config
            var cfgObj = this.PluginInterface.GetPluginConfig();
            this.config = cfgObj as PluginConfig ?? new PluginConfig();

            this.framework = framework;
            this.clientState = clientState;

            // subscribe to framework updates to detect login/logout and record characters
            this.framework.Update += OnFrameworkUpdate;

            this.mainWindow = new Gui.CrystalTerrorWindow(this.config, clientState, () => (this.lastSeenName, this.lastSeenWorld), this.PluginInterface);
            this.configWindow = new Gui.ConfigWindow(this.config, this.PluginInterface);
            this.windowSystem.AddWindow(this.mainWindow);
            this.windowSystem.AddWindow(this.configWindow);

            // wire main window request to open config
            this.mainWindow.RequestOpenConfig = () => this.configWindow.IsOpen = true;

            // register a simple toggle command
            this.CommandManager.AddHandler("/ct", new CommandInfo(this.OnToggleCommand)
            {
                HelpMessage = "Toggle the CrystalTerror main window",
            });

            this.PluginInterface.UiBuilder.Draw += this.DrawUi;
            this.PluginInterface.UiBuilder.OpenMainUi += this.OpenMainUi;
            this.PluginInterface.UiBuilder.OpenConfigUi += this.OpenConfigUi;

#if DEBUG
            this.mainWindow.IsOpen = true;
#endif
        }

        public string Name => "CrystalTerror";

        public void OpenMainUi()
        {
            this.mainWindow.IsOpen = true;
        }

        public void OpenConfigUi()
        {
            this.configWindow.IsOpen = true;
        }

        private void OnToggleCommand(string command, string args)
        {
            this.mainWindow.IsOpen = !this.mainWindow.IsOpen;
        }

        private void DrawUi()
        {
            this.windowSystem.Draw();
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.isDisposed)
                return;

            if (disposing)
            {
                try
                {
                    // persist any config changes
                    this.PluginInterface.SavePluginConfig(this.config);
                }
                catch
                {
                    // ignore
                }

                this.PluginInterface.UiBuilder.Draw -= this.DrawUi;
                this.PluginInterface.UiBuilder.OpenMainUi -= this.OpenMainUi;
                this.PluginInterface.UiBuilder.OpenConfigUi -= this.OpenConfigUi;
                this.CommandManager.RemoveHandler("/cterr");

                // unsubscribe framework updates
                try
                {
                    this.framework.Update -= OnFrameworkUpdate;
                }
                catch
                {
                    // ignore if already removed or null
                }

                this.windowSystem.RemoveAllWindows();
                this.mainWindow.Dispose();
                this.configWindow.Dispose();
            }

            this.isDisposed = true;
        }

        private void OnFrameworkUpdate(IFramework _)
        {
            try
            {
                // reflectively access LocalPlayer to avoid hard type coupling
                if (this.clientState == null)
                    return;

                var clientType = this.clientState.GetType();
                var localPlayerProp = clientType.GetProperty("LocalPlayer");
                var localPlayer = localPlayerProp?.GetValue(this.clientState);
                if (localPlayer == null)
                {
                    lastSeenName = null;
                    lastSeenWorld = null;
                    return;
                }

                var nameProp = localPlayer.GetType().GetProperty("Name");
                var nameVal = nameProp?.GetValue(localPlayer);
                var name = nameVal?.ToString();
                if (string.IsNullOrEmpty(name))
                    return;

                string world = "(unknown)";
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

                        world = !string.IsNullOrEmpty(resolved) ? resolved : hw.ToString() ?? "(unknown)";
                    }
                }
                else
                {
                    var worldProp = localPlayer.GetType().GetProperty("World");
                    var worldVal = worldProp?.GetValue(localPlayer);
                    if (worldVal != null)
                        world = worldVal.ToString() ?? "(unknown)";
                }

                if (name == lastSeenName && world == lastSeenWorld)
                    return;

                lastSeenName = name;
                lastSeenWorld = world;
                SaveOrUpdateCharacter(name, world);
            }
            catch
            {
                // be conservative — don't let the framework tick crash
            }
        }

        private void SaveOrUpdateCharacter(string name, string world)
        {
            var existing = this.config.Characters.FirstOrDefault(c => c.Name == name && c.World == world);
            if (existing != null)
            {
                existing.LastUpdateUtc = DateTime.UtcNow;
            }
            else
            {
                var sc = new StoredCharacter
                {
                    Name = name,
                    World = world,
                    ServiceAccount = 1,
                    LastUpdateUtc = DateTime.UtcNow,
                    Retainers = new System.Collections.Generic.List<Retainer>(),
                    Inventory = new Inventory()
                };

                this.config.Characters.Add(sc);
            }

            try
            {
                this.PluginInterface.SavePluginConfig(this.config);
            }
            catch
            {
                // ignore save errors
            }
        }
    }
}
