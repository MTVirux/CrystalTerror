namespace CrystalTerror
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using Dalamud.Interface.Windowing;
    using Dalamud.Plugin;
    using Dalamud.Game.Command;
    using Dalamud.Plugin.Services;
    using Dalamud.Data;
    using Dalamud.Game.Addon.Lifecycle;
    using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;

    public class CrystalTerrorPlugin : IDalamudPlugin, IDisposable
    {
        public IDalamudPluginInterface PluginInterface { get; init; }

        public Configuration Config { get; private set; } = new Configuration();

        private readonly WindowSystem windowSystem = new(typeof(CrystalTerrorPlugin).AssemblyQualifiedName);
        private readonly Gui.MainWindow mainWindow;
        private readonly Gui.ConfigWindow configWindow;
        private readonly ICommandManager commandManager;
        private readonly Dalamud.Plugin.Services.IPlayerState playerState;
        private readonly Dalamud.Plugin.Services.IObjectTable objects;
        private readonly IFramework framework;
        private readonly IGameGui gameGui;
        private readonly IAddonLifecycle addonLifecycle;
        private ulong lastLocalContentId;
        private bool lastRetainerOpen;
        private string lastPlayerKey = string.Empty;
        private readonly IDataManager dataManager;

        private bool disposed;
        private readonly IPluginLog pluginLog;

        // In-memory list of imported/stored characters for the UI.
        public List<StoredCharacter> Characters { get; } = new();

        // Expose client state and data manager for helper usage (e.g., importing current character)
        public Dalamud.Plugin.Services.IPlayerState PlayerState => this.playerState;
        public Dalamud.Plugin.Services.IObjectTable Objects => this.objects;
        public IDataManager DataManager => this.dataManager;

        public CrystalTerrorPlugin(IDalamudPluginInterface pluginInterface, ICommandManager commandManager, Dalamud.Plugin.Services.IPlayerState playerState, Dalamud.Plugin.Services.IObjectTable objects, IDataManager dataManager, IFramework framework, IGameGui gameGui, IAddonLifecycle addonLifecycle, IPluginLog pluginLog)
        {
            this.PluginInterface = pluginInterface;

            this.Config = this.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

            this.commandManager = commandManager;
            this.playerState = playerState;
            this.objects = objects;
            this.dataManager = dataManager;
            this.framework = framework;
            this.gameGui = gameGui;
            this.addonLifecycle = addonLifecycle;
            this.pluginLog = pluginLog;

            // Initialize local content id tracking and subscribe to framework updates to detect character changes
            this.lastLocalContentId = 0;
            this.lastRetainerOpen = false;
            this.framework.Update += this.OnFrameworkUpdate;

            this.mainWindow = new Gui.MainWindow(this);
            this.configWindow = new Gui.ConfigWindow(this);

            this.windowSystem.AddWindow(this.mainWindow);
            this.windowSystem.AddWindow(this.configWindow);

            this.PluginInterface.UiBuilder.Draw += this.DrawUi;
            this.PluginInterface.UiBuilder.OpenConfigUi += this.OpenConfigUi;
            this.PluginInterface.UiBuilder.OpenMainUi += this.OpenMainUi;

            // Register command to open/toggle main window
            this.commandManager.AddHandler("/ct", new CommandInfo(this.OnOpenCommand)
            {
                HelpMessage = "Open the CrystalTerror main window.",
            });

            this.mainWindow.IsOpen = true;
            if (this.Config.ShowOnStart)
            {
                // Ensure the main window is opened via the plugin UI hook in case the windowing system
                // needs the OpenMainUi flow to run properly on startup.
                try
                {
                    this.OpenMainUi();
                }
                catch
                {
                    // ignore
                }
            }

            // Load persisted characters from Dalamud plugin config into memory
            try
            {
                if (this.Config?.Characters != null && this.Config.Characters.Count > 0)
                {
                    // Copy into in-memory list and repair OwnerCharacter references which are not persisted
                    RetainerHelper.RepairOwnerReferences(this.Config.Characters);
                    foreach (var sc in this.Config.Characters)
                        this.Characters.Add(sc);
                }
                
                try
                {
                    var current = this.playerState.ContentId;
                    if (current != 0 && current != this.lastLocalContentId)
                    {
                        // character changed (or first login) — import current character automatically
                        var sc = CharacterHelper.ImportCurrentCharacter(this.playerState, this.objects, this.dataManager);
                        if (sc != null)
                        {
                            CharacterHelper.MergeInto(this.Characters, new[] { sc }, CharacterHelper.MergePolicy.Skip);
                            try
                            {
                                this.Config.Characters = this.Characters;
                                this.PluginInterface.SavePluginConfig(this.Config);
                            }
                            catch
                            {
                            }
                        }

                        this.lastLocalContentId = current;
                    }
                    else if (current == 0)
                    {
                        // logged out, reset tracking id
                        this.lastLocalContentId = 0;
                    }
                }
                catch
                {
                }

                // Register addon lifecycle listener for RetainerList opens
                try
                {
                    this.addonLifecycle.RegisterListener(AddonEvent.PreSetup, "RetainerList", this.OnRetainerListSetup);
                }
                catch
                {
                    // ignore registration errors
                }
            }
            catch
            {
                // ignore startup import errors
            }
        }

        public static string Name => "Crystal Terror";

        public void OpenMainUi()
            => this.mainWindow.IsOpen = true;

        public void OpenConfigUi()
            => this.configWindow.IsOpen = true;

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
                return;

            if (disposing)
            {
                this.PluginInterface.SavePluginConfig(this.Config);

                this.windowSystem.RemoveAllWindows();
                this.mainWindow.Dispose();
                this.configWindow.Dispose();

                this.PluginInterface.UiBuilder.Draw -= this.DrawUi;
                this.PluginInterface.UiBuilder.OpenConfigUi -= this.OpenConfigUi;
                this.PluginInterface.UiBuilder.OpenMainUi -= this.OpenMainUi;

                // Unsubscribe framework update handler
                try
                {
                    this.framework.Update -= this.OnFrameworkUpdate;
                }
                catch
                {
                }

                // Unregister addon lifecycle listener
                try
                {
                    this.addonLifecycle.UnregisterListener(AddonEvent.PreSetup, "RetainerList", this.OnRetainerListSetup);
                }
                catch
                {
                }

                this.commandManager.RemoveHandler("/ct");
            }

            this.disposed = true;
        }

        private void DrawUi()
            => this.windowSystem.Draw();

        private void OnFrameworkUpdate(IFramework _)
        {
            try
            {
                var contentId = this.playerState.ContentId;
                var localObjId = this.objects.LocalPlayer?.GameObjectId ?? 0u;
                var currentKey = $"{contentId}:{localObjId}";

                if (contentId != 0 && currentKey != this.lastPlayerKey)
                {
                    // character changed (or first login) — import current character automatically
                    var sc = CharacterHelper.ImportCurrentCharacter(this.playerState, this.objects, this.dataManager);
                    if (sc != null)
                    {
                        CharacterHelper.MergeInto(this.Characters, new[] { sc }, CharacterHelper.MergePolicy.Skip);
                        try
                        {
                            this.Config.Characters = this.Characters;
                            this.PluginInterface.SavePluginConfig(this.Config);
                        }
                        catch
                        {
                        }
                    }

                    this.lastLocalContentId = contentId;
                    this.lastPlayerKey = currentKey;
                }
                else if (contentId == 0)
                {
                    // logged out, reset tracking id
                    this.lastLocalContentId = 0;
                    this.lastPlayerKey = string.Empty;
                }
            }
            catch
            {
                // swallow to avoid throwing in framework update
            }
        }

        private void OnRetainerListSetup(AddonEvent type, AddonArgs args)
        {
            try
            {
                try
                {
                    this.pluginLog.Information($"Retainer addon opened (ContentId={this.playerState.ContentId}). Triggering import.");
                }
                catch { }

                var sc = CharacterHelper.ImportCurrentCharacter(this.playerState, this.objects, this.dataManager);
                if (sc != null)
                {
                    CharacterHelper.MergeInto(this.Characters, new[] { sc }, CharacterHelper.MergePolicy.Overwrite);
                    try
                    {
                        this.Config.Characters = this.Characters;
                        this.PluginInterface.SavePluginConfig(this.Config);
                    }
                    catch { }
                }
            }
            catch
            {
                // swallow handler errors
            }
        }

        private void OnOpenCommand(string command, string arguments)
        {
            // Toggle the main window when the command is executed. If arguments are provided, still open the window.
            if (string.IsNullOrWhiteSpace(arguments))
                this.mainWindow.IsOpen = !this.mainWindow.IsOpen;
            else
                this.mainWindow.IsOpen = true;
        }
    }
}