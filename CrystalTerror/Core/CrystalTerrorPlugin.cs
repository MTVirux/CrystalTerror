using CrystalTerror.Helpers;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.IoC;
using Dalamud.Game.Inventory;
using Dalamud.Game.Inventory.InventoryEventArgTypes;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using ECommons;

namespace CrystalTerror.Core;

public class CrystalTerrorPlugin : IDalamudPlugin, IDisposable
{
    public static CrystalTerrorPlugin Instance { get; private set; } = null!;

    public IDalamudPluginInterface PluginInterface { get; init; }

    public Configuration Config { get; private set; } = new Configuration();

    private readonly WindowSystem windowSystem = new(typeof(CrystalTerrorPlugin).AssemblyQualifiedName);
    private readonly Gui.MainWindow.MainWindow mainWindow;
    private readonly Gui.ConfigWindow.ConfigWindow configWindow;
    private readonly ICommandManager commandManager;
    private readonly Dalamud.Plugin.Services.IPlayerState playerState;
    private readonly Dalamud.Plugin.Services.IObjectTable objects;
    private readonly IFramework framework;
    private readonly IAddonLifecycle addonLifecycle;
    private readonly ICondition condition;
    private ulong lastLocalContentId;
    private bool loggedOut;
    // Lock for atomically updating character-change tracking to prevent duplicate imports
    private readonly object _playerKeyLock = new();
    // Timestamp of last framework import for additional throttling
    private DateTime lastFrameworkImport = DateTime.MinValue;
    private const double FrameworkImportThrottleSeconds = 2.0;
    // Ensure framework events don't run before plugin finished loading config/characters
    private bool isInitialized = false;
    private readonly IDataManager dataManager;

    private bool disposed;
    private readonly IPluginLog pluginLog;

    [PluginService]
    public static IGameInventory GameInventory { get; private set; } = null!;

    // AutoRetainer IPC for setting ventures
    private Dalamud.Plugin.Ipc.ICallGateSubscriber<uint, object>? autoRetainerSetVenture;
    private Dalamud.Plugin.Ipc.ICallGateSubscriber<string, object>? autoRetainerOnSendToVenture;

    // In-memory list of imported/stored characters for the UI.
    public List<StoredCharacter> Characters { get; } = new();

    public CrystalTerrorPlugin(IDalamudPluginInterface pluginInterface, ICommandManager commandManager, Dalamud.Plugin.Services.IPlayerState playerState, Dalamud.Plugin.Services.IObjectTable objects, IDataManager dataManager, IFramework framework, IAddonLifecycle addonLifecycle, ICondition condition, IPluginLog pluginLog, Dalamud.Plugin.Services.ITextureProvider textureProvider)
    {
        Instance = this;
        this.PluginInterface = pluginInterface;
        
        // Initialize ECommons first (required for logging and other services)
        ECommons.ECommonsMain.Init(pluginInterface, this);
        
        // Initialize global services
        Services.ServiceManager.Initialize(pluginInterface, playerState, objects, dataManager, pluginLog, condition);

        this.Config = ConfigHelper.Load();

        this.commandManager = commandManager;
        this.playerState = playerState;
        this.objects = objects;
        this.dataManager = dataManager;
        this.framework = framework;
        this.addonLifecycle = addonLifecycle;
        this.condition = condition;
        this.pluginLog = pluginLog;

        // Initialize local content id tracking. Subscription to framework updates
        // is deferred until after config/characters are loaded to avoid a race
        // where the framework fires before `Characters` is populated.
            this.lastLocalContentId = 0;

            this.mainWindow = new Gui.MainWindow.MainWindow(this, textureProvider);
            this.configWindow = new Gui.ConfigWindow.ConfigWindow(this);

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

            this.mainWindow.IsOpen = this.Config.ShowOnStart;

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
                    if (current != 0 && current != this.lastLocalContentId && this.Config != null)
                    {
                        // character changed (or first login) — import current character automatically
                        CharacterHelper.ImportMergeSaveTimed("Startup", this.Characters, this.Config, CharacterHelper.MergePolicy.Merge);
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
                    // Listen for retainer inventory/bank close events to capture crystal changes
                    this.addonLifecycle.RegisterListener(AddonEvent.PreFinalize, "Bank", this.OnRetainerInventoryClose);
                    this.addonLifecycle.RegisterListener(AddonEvent.PreFinalize, "InventoryRetainer", this.OnRetainerInventoryClose);
                    this.addonLifecycle.RegisterListener(AddonEvent.PreFinalize, "InventoryRetainerLarge", this.OnRetainerInventoryClose);
                    // Listen for SelectString close (retainer menu) to trigger final update
                    this.addonLifecycle.RegisterListener(AddonEvent.PreFinalize, "SelectString", this.OnSelectStringClose);
                }
                catch
                {
                    // ignore registration errors
                }

                // Initialize AutoRetainer IPC for setting ventures
                try
                {
                    this.autoRetainerSetVenture = this.PluginInterface.GetIpcSubscriber<uint, object>("AutoRetainer.SetVenture");
                    this.autoRetainerOnSendToVenture = this.PluginInterface.GetIpcSubscriber<string, object>("AutoRetainer.OnSendRetainerToVenture");
                    
                    // Subscribe to the venture override hook
                    this.autoRetainerOnSendToVenture.Subscribe(this.OnRetainerSendToVenture);
                    this.pluginLog.Information("[CrystalTerror] Subscribed to AutoRetainer.OnSendRetainerToVenture hook");
                }
                catch (Exception ex)
                {
                    // AutoRetainer not available
                    this.pluginLog.Debug($"AutoRetainer IPC not available: {ex.Message}");
                }
                // Initialize InventoryHelper to handle GameInventory events
                try
                {
                    InventoryHelper.Initialize(this);
                }
                catch (Exception ex)
                {
                    this.pluginLog.Debug($"InventoryHelper initialization failed: {ex.Message}");
                }
            }
            catch
            {
                // ignore startup import errors
            }

            // Now that config and characters have been loaded and handlers registered,
            // subscribe to the framework update event. This prevents running the import
            // logic against an empty `Characters` list if the framework fired earlier
            // during construction.
            try
            {
                this.framework.Update += this.OnFrameworkUpdate;
            }
            catch
            {
                // ignore subscription errors
            }

            this.isInitialized = true;
        }

        public static string Name => "Crystal Terror";

        public void OpenMainUi()
            => this.mainWindow.IsOpen = true;

        public void OpenConfigUi()
            => this.configWindow.IsOpen = true;
        
        public void InvalidateSortCache()
            => this.mainWindow.InvalidateSortCache();

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
                // Force save on dispose to ensure no data is lost (bypasses throttling)
                this.pluginLog.Information("[CrystalTerror] Plugin disposing, forcing final save...");
                ConfigHelper.ForceSave(this.Config, this.Characters);

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

                // Unregister addon lifecycle listeners
                try
                {
                    this.addonLifecycle.UnregisterListener(AddonEvent.PreSetup, "RetainerList", this.OnRetainerListSetup);
                    this.addonLifecycle.UnregisterListener(AddonEvent.PreFinalize, "Bank", this.OnRetainerInventoryClose);
                    this.addonLifecycle.UnregisterListener(AddonEvent.PreFinalize, "InventoryRetainer", this.OnRetainerInventoryClose);
                    this.addonLifecycle.UnregisterListener(AddonEvent.PreFinalize, "InventoryRetainerLarge", this.OnRetainerInventoryClose);
                    this.addonLifecycle.UnregisterListener(AddonEvent.PreFinalize, "SelectString", this.OnSelectStringClose);
                }
                catch
                {
                }

                // Dispose inventory helper (unsubscribe handlers)
                try
                {
                    InventoryHelper.Dispose();
                }
                catch
                {
                }

                this.commandManager.RemoveHandler("/ct");
                
                // Dispose ECommons
                ECommons.ECommonsMain.Dispose();
            }

            this.disposed = true;
        }

        private void DrawUi()
            => this.windowSystem.Draw();

        private void OnFrameworkUpdate(IFramework _)
        {
            if (!this.isInitialized)
                return;
            try
            {
                // Detect character change by ContentId only. The local GameObjectId changes on every
                // territory transition, so keying on it forced a redundant import + config write on
                // each zone change; the CID is stable per character and only changes on an actual switch.
                var contentId = Player.Available ? Player.CID : 0;

                bool shouldImport = false;
                lock (_playerKeyLock)
                {
                    if (contentId != 0)
                    {
                        if (contentId != this.lastLocalContentId)
                        {
                            // Additional time-based throttle to prevent rapid imports during character cycling
                            var now = DateTime.UtcNow;
                            if ((now - this.lastFrameworkImport).TotalSeconds >= FrameworkImportThrottleSeconds)
                            {
                                shouldImport = true;
                                this.lastLocalContentId = contentId;
                                this.lastFrameworkImport = now;
                                this.pluginLog.Information($"[CrystalTerror] Character change detected: CID={contentId:X16}");
                            }
                            else
                            {
                                this.pluginLog.Debug($"[CrystalTerror] Framework import throttled (last import {(now - this.lastFrameworkImport).TotalSeconds:F1}s ago)");
                            }
                        }

                        this.loggedOut = false;
                    }
                    else if (this.lastLocalContentId != 0 && !this.loggedOut)
                    {
                        // Player unavailable (logout, or a brief zone-transition blip). Refresh the UI
                        // once, but keep lastLocalContentId so re-appearing as the SAME character does
                        // not trigger a redundant re-import.
                        this.loggedOut = true;
                        this.mainWindow.InvalidateSortCache();
                        this.pluginLog.Debug("[CrystalTerror] Player unavailable (logout or zone transition)");
                    }
                }

                if (shouldImport)
                {
                    // Character changed (or first login) — import current character automatically
                    var sc = CharacterHelper.ImportMergeSaveTimed("Framework", this.Characters, this.Config, CharacterHelper.MergePolicy.Merge);
                    if (sc != null)
                    {
                        this.mainWindow.InvalidateSortCache();
                        this.pluginLog.Information($"[CrystalTerror] Character imported successfully: {sc.Name}@{sc.World}");
                    }
                    else
                    {
                        this.pluginLog.Warning("[CrystalTerror] ImportCurrentCharacter returned null despite player being available");
                    }
                }

                // Update retainer stats when at summoning bell
                RetainerStatsHelper.UpdateRetainerStatsIfNeeded(
                    this.Characters,
                    ref this.lastStatsUpdate,
                    StatsUpdateThrottleSeconds);
            }
            catch (Exception ex)
            {
                // Log the error instead of swallowing it completely
                this.pluginLog.Error($"[CrystalTerror] OnFrameworkUpdate error: {ex.Message}");
            }
        }

        private DateTime lastStatsUpdate = DateTime.MinValue;
        private const double StatsUpdateThrottleSeconds = 2.0;

        private void OnRetainerListSetup(AddonEvent type, AddonArgs args)
            => AutoRetainerHelper.HandleRetainerListSetup(
                this.Characters,
                this.Config);

        private void OnRetainerSendToVenture(string retainerName)
            => AutoRetainerHelper.HandleRetainerSendToVenture(
                retainerName,
                this.Config,
                this.autoRetainerSetVenture,
                this.Characters,
                this.pluginLog);

        private DateTime lastRetainerInventoryUpdate = DateTime.MinValue;
        
        /// <summary>
        /// Handler for retainer inventory/bank addon close events.
        /// Triggers an import of current character data to capture any crystal changes.
        /// </summary>
        private void OnRetainerInventoryClose(AddonEvent type, AddonArgs args)
        {
            try
            {
                // Throttle to prevent excessive updates
                if ((DateTime.UtcNow - lastRetainerInventoryUpdate).TotalMilliseconds < 500)
                    return;
                
                lastRetainerInventoryUpdate = DateTime.UtcNow;
                
                if (!Player.Available || Player.CID == 0)
                    return;
                
                this.pluginLog.Debug($"[CrystalTerror] OnRetainerInventoryClose triggered for {Player.Name}");

                var sc = CharacterHelper.ImportMergeSaveTimed("RetainerClose", this.Characters, this.Config, CharacterHelper.MergePolicy.Overwrite);
                if (sc != null)
                    this.pluginLog.Debug($"[CrystalTerror] Retainer inventory update saved for {sc.Name}@{sc.World}");
            }
            catch (Exception ex)
            {
                this.pluginLog.Warning($"[CrystalTerror] OnRetainerInventoryClose error: {ex.Message}");
            }
        }

        /// <summary>
        /// Handler for SelectString close (retainer menu close).
        /// This fires when exiting a retainer, ensuring we capture the final state.
        /// </summary>
        private void OnSelectStringClose(AddonEvent type, AddonArgs args)
        {
            try
            {
                // Only process if we're at a summoning bell (retainer context)
                if (!Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.OccupiedSummoningBell])
                    return;
                
                // Throttle to prevent excessive updates
                if ((DateTime.UtcNow - lastRetainerInventoryUpdate).TotalMilliseconds < 500)
                    return;
                
                lastRetainerInventoryUpdate = DateTime.UtcNow;
                
                if (!Player.Available || Player.CID == 0)
                    return;
                
                this.pluginLog.Debug($"[CrystalTerror] OnSelectStringClose triggered (summoning bell) for {Player.Name}");

                var sc = CharacterHelper.ImportMergeSaveTimed("SelectStringClose", this.Characters, this.Config, CharacterHelper.MergePolicy.Overwrite);
                if (sc != null)
                    this.pluginLog.Debug($"[CrystalTerror] Retainer menu close update saved for {sc.Name}@{sc.World}");
            }
            catch (Exception ex)
            {
                this.pluginLog.Warning($"[CrystalTerror] OnSelectStringClose error: {ex.Message}");
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
