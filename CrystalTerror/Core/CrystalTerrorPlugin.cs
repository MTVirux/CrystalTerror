// Auto-generated simple plugin entry to host the main window
using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
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
        private readonly Gui.AutoRetainerCharsWindow autoRetainerWindow;
        private readonly Gui.AutoRetainerRetainersWindow autoRetainerRetainersWindow;
        private readonly PluginConfig config;
        private readonly IFramework framework;
        private readonly IClientState clientState;
        private readonly IDataManager dataManager;
        private readonly Dictionary<uint, (Element, CrystalType)> trackedItems = new();
        private readonly Dictionary<uint, long> lastItemCounts = new();
        private DateTime lastPoll = DateTime.MinValue;
        private readonly TimeSpan pollInterval = TimeSpan.FromSeconds(5);
        private Dalamud.Plugin.Ipc.ICallGateSubscriber<(uint, FFXIVClientStructs.FFXIV.Client.Game.InventoryItem.ItemFlags, ulong, uint), bool>? allaganItemAdded;
        private Dalamud.Plugin.Ipc.ICallGateSubscriber<(uint, FFXIVClientStructs.FFXIV.Client.Game.InventoryItem.ItemFlags, ulong, uint), bool>? allaganItemRemoved;
        private Dalamud.Plugin.Ipc.ICallGateSubscriber<uint, ulong, uint, uint>? allaganItemCount;
        private Dalamud.Plugin.Ipc.ICallGateSubscriber<uint, ulong, uint, uint>? allaganItemCountHQ;
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
            this.dataManager = dataManager;

            // subscribe to login/logout events to trigger import on login
            try { this.clientState.Login += OnClientLogin; } catch { }
            try { this.clientState.Logout += OnClientLogout; } catch { }

            // build tracked items map from Lumina sheet
            try { BuildTrackedItemMap(); } catch { }

            // try to subscribe to AllaganTools item IPC if available
            try
            {
                allaganItemAdded = this.PluginInterface.GetIpcSubscriber<(uint, FFXIVClientStructs.FFXIV.Client.Game.InventoryItem.ItemFlags, ulong, uint), bool>("AllaganTools.ItemAdded");
                allaganItemRemoved = this.PluginInterface.GetIpcSubscriber<(uint, FFXIVClientStructs.FFXIV.Client.Game.InventoryItem.ItemFlags, ulong, uint), bool>("AllaganTools.ItemRemoved");
                allaganItemAdded?.Subscribe(OnAllaganItemChanged);
                allaganItemRemoved?.Subscribe(OnAllaganItemChanged);
                allaganItemCount = this.PluginInterface.GetIpcSubscriber<uint, ulong, uint, uint>("AllaganTools.ItemCount");
                allaganItemCountHQ = this.PluginInterface.GetIpcSubscriber<uint, ulong, uint, uint>("AllaganTools.ItemCountHQ");
            }
            catch
            {
                // ignore if AllaganTools not present
            }
            this.dataManager = dataManager;

            // subscribe to framework updates to detect login/logout and record characters
            this.framework.Update += OnFrameworkUpdate;

            this.mainWindow = new Gui.CrystalTerrorWindow(this.config, clientState, () => (this.lastSeenName, this.lastSeenWorld), this.PluginInterface);
            this.configWindow = new Gui.ConfigWindow(this.config, this.PluginInterface);
            this.autoRetainerWindow = new Gui.AutoRetainerCharsWindow(this.PluginInterface);
            this.autoRetainerRetainersWindow = new Gui.AutoRetainerRetainersWindow(this.PluginInterface);
            this.windowSystem.AddWindow(this.mainWindow);
            this.windowSystem.AddWindow(this.configWindow);
            this.windowSystem.AddWindow(this.autoRetainerWindow);
            this.windowSystem.AddWindow(this.autoRetainerRetainersWindow);

            // wire main window request to open config and autoretainer list
            this.mainWindow.RequestOpenConfig = () => this.configWindow.IsOpen = true;
            this.mainWindow.RequestOpenAutoRetainer = () => this.autoRetainerWindow.IsOpen = true;
            // wire chars window request to open retainers window for a CID
            this.autoRetainerWindow.RequestOpenRetainers = (cid, name) =>
            {
                try
                {
                    this.autoRetainerRetainersWindow.SetCharacter(cid, name);
                    this.autoRetainerRetainersWindow.IsOpen = true;
                }
                catch { }
            };

            // Also allow the chars window to request importing a character's data into config
            this.autoRetainerWindow.RequestImportCharacter = (cid) =>
            {
                try { this.configWindow.ImportCharacterFromCid(cid); } catch { }
            };


            // register a simple toggle command
            this.CommandManager.AddHandler("/ct", new CommandInfo(this.OnToggleCommand)
            {
                HelpMessage = "Toggle the CrystalTerror main window",
            });

            // support manual import via "/ct import"
            this.CommandManager.AddHandler("/ctimport", new CommandInfo((c, a) => { TryImportForCurrentPlayer(); })
            {
                HelpMessage = "Import current character inventory into CrystalTerror config",
            });

            this.PluginInterface.UiBuilder.Draw += this.DrawUi;
            this.PluginInterface.UiBuilder.OpenMainUi += this.OpenMainUi;
            this.PluginInterface.UiBuilder.OpenConfigUi += this.OpenConfigUi;

            // If both windows are closed at startup, ensure we are not stuck in edit mode.
            try
            {
                if (!this.mainWindow.IsOpen && !this.configWindow.IsOpen && this.config.EditMode)
                {
                    this.config.EditMode = false;
                    try { this.PluginInterface.SavePluginConfig(this.config); } catch { }
                }
            }
            catch
            {
                // ignore
            }

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
            if (!string.IsNullOrWhiteSpace(args) && args.Trim().Equals("import", StringComparison.OrdinalIgnoreCase))
            {
                TryImportForCurrentPlayer();
                return;
            }

            this.mainWindow.IsOpen = !this.mainWindow.IsOpen;
        }

        private void DrawUi()
        {
            try
            {
                // If user closed all windows, automatically exit edit mode and persist.
                if (!this.mainWindow.IsOpen && !this.configWindow.IsOpen && this.config.EditMode)
                {
                    this.config.EditMode = false;
                    try { this.PluginInterface.SavePluginConfig(this.config); } catch { }
                }
            }
            catch
            {
                // be conservative — don't let UI housekeeping crash drawing
            }

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
                this.autoRetainerWindow.Dispose();
                this.autoRetainerRetainersWindow.Dispose();
                try { this.clientState.Login -= OnClientLogin; } catch { }
                try { this.clientState.Logout -= OnClientLogout; } catch { }
                try { allaganItemAdded?.Unsubscribe(OnAllaganItemChanged); } catch { }
                try { allaganItemRemoved?.Unsubscribe(OnAllaganItemChanged); } catch { }
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
                {
                    // still same character — poll inventory occasionally
                    try
                    {
                        var scPoll = this.config.Characters.FirstOrDefault(c => c.Name == this.lastSeenName && c.World == this.lastSeenWorld);
                        if (scPoll != null) PollTrackedCountsAndMaybeImport(scPoll);
                    }
                    catch { }
                    return;
                }

                lastSeenName = name;
                lastSeenWorld = world;
                SaveOrUpdateCharacter(name, world);
                // trigger import on login/change
                try { TryImportForCurrentPlayer(); } catch { }
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
                try
                {
                    // attempt to populate inventory for newly-seen character
                    TryImportForCurrentPlayer();
                }
                catch
                {
                    // ignore import failures
                }
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

        private unsafe void TryImportForCurrentPlayer()
        {
            try
            {
                if (string.IsNullOrEmpty(this.lastSeenName) || string.IsNullOrEmpty(this.lastSeenWorld))
                {
                    // fallback: try to reflectively read clientState
                    var clientType = this.clientState?.GetType();
                    var localPlayerProp = clientType?.GetProperty("LocalPlayer");
                    var localPlayer = localPlayerProp?.GetValue(this.clientState);
                    if (localPlayer == null) return;

                    var nameProp = localPlayer.GetType().GetProperty("Name");
                    var nameVal = nameProp?.GetValue(localPlayer);
                    var name = nameVal?.ToString();
                    if (string.IsNullOrEmpty(name)) return;

                    string world = "(unknown)";
                    var worldProp = localPlayer.GetType().GetProperty("World") ?? localPlayer.GetType().GetProperty("HomeWorld");
                    var worldVal = worldProp?.GetValue(localPlayer);
                    if (worldVal != null) world = worldVal.ToString() ?? world;

                    this.lastSeenName = name;
                    this.lastSeenWorld = world;
                }

                var sc = this.config.Characters.FirstOrDefault(c => c.Name == this.lastSeenName && c.World == this.lastSeenWorld);
                if (sc == null) return;

                ImportInventoryForCharacter(sc);
                try { ImportRetainersForCharacter(sc); } catch { }
                // initialize lastItemCounts for tracked items
                try
                {
                    var inv = FFXIVClientStructs.FFXIV.Client.Game.InventoryManager.Instance();
                    if (inv != null)
                    {
                        foreach (var kv in trackedItems)
                        {
                            try { lastItemCounts[kv.Key] = inv->GetInventoryItemCount(kv.Key); } catch { }
                        }
                    }
                }
                catch { }
                try { this.PluginInterface.SavePluginConfig(this.config); } catch { }
            }
            catch
            {
                // ignore import errors
            }
        }

        private void OnClientLogin()
        {
            try { TryImportForCurrentPlayer(); } catch { }
        }

        private void OnClientLogout(int a, int b)
        {
            try { lastItemCounts.Clear(); } catch { }
        }

        private void OnAllaganItemChanged((uint itemId, FFXIVClientStructs.FFXIV.Client.Game.InventoryItem.ItemFlags flags, ulong a, uint b) tuple)
        {
            try
            {
                if (trackedItems.ContainsKey(tuple.itemId))
                {
                    var sc = this.config.Characters.FirstOrDefault(c => c.Name == this.lastSeenName && c.World == this.lastSeenWorld);
                    if (sc != null)
                    {
                        ImportInventoryForCharacter(sc);
                        try { ImportRetainersForCharacter(sc); } catch { }
                    }
                }
            }
            catch { }
        }

        private unsafe void ImportRetainersForCharacter(StoredCharacter sc)
        {
            try
            {
                if (allaganItemCount == null) return; // AllaganTools not present

                var rm = RetainerManager.Instance();
                if (rm == null) return;

                // ensure we have a retainers list
                if (sc.Retainers == null) sc.Retainers = new System.Collections.Generic.List<Retainer>();

                // For each retainer slot (0..9) try to lookup a retainer id and query counts
                for (uint slot = 0; slot < 10; slot++)
                {
                    try
                    {
                        var ret = rm->GetRetainerBySortedIndex(slot);
                        if (ret == null) continue;
                        if (!ret->Available) continue;
                        var retId = ret->RetainerId;
                        if (retId == 0) continue;

                        // read the in-game retainer name if available
                        string displayName = null;
                        try
                        {
                            displayName = ret->NameString;
                        }
                        catch { }

                        // find or create a Retainer entry by numeric atid or (legacy) numeric-name
                        var stored = sc.Retainers.FirstOrDefault(r => r.atid == retId || r.Name == retId.ToString());
                        if (stored == null)
                        {
                            stored = new Retainer(sc) { atid = retId, Name = string.IsNullOrEmpty(displayName) ? retId.ToString() : displayName };
                            sc.Retainers.Add(stored);
                        }
                        else
                        {
                            // ensure persisted atid is set and update display name if available
                            stored.atid = retId;
                            if (!string.IsNullOrEmpty(displayName)) stored.Name = displayName;
                        }

                        // zero out counts first
                        stored.Inventory = new Inventory();

                        // for each tracked item, query AllaganTools for quantities across retainer containers
                        foreach (var kv in trackedItems)
                        {
                            try
                            {
                                uint itemId = kv.Key;
                                uint qty = 0;
                                // sum across the same containers as RetainerInfo.GetRetainerInventoryItem
                                qty += allaganItemCount.InvokeFunc(itemId, retId, 10000);
                                qty += allaganItemCount.InvokeFunc(itemId, retId, 10001);
                                qty += allaganItemCount.InvokeFunc(itemId, retId, 10002);
                                qty += allaganItemCount.InvokeFunc(itemId, retId, 10003);
                                qty += allaganItemCount.InvokeFunc(itemId, retId, 10004);
                                qty += allaganItemCount.InvokeFunc(itemId, retId, 10005);
                                qty += allaganItemCount.InvokeFunc(itemId, retId, 10006);
                                qty += allaganItemCount.InvokeFunc(itemId, retId, (uint)InventoryType.RetainerCrystals);

                                var (el, type) = trackedItems[itemId];
                                stored.Inventory.SetCount(el, type, qty);
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
                try { this.PluginInterface.SavePluginConfig(this.config); } catch { }
            }
            catch { }
        }

        private unsafe void PollTrackedCountsAndMaybeImport(StoredCharacter sc)
        {
            try
            {
                var now = DateTime.UtcNow;
                if (now - lastPoll < pollInterval) return;
                lastPoll = now;

                var inv = InventoryManager.Instance();
                if (inv == null) return;

                var changed = false;
                foreach (var id in trackedItems.Keys)
                {
                    try
                    {
                        var cnt = inv->GetInventoryItemCount(id);
                        if (!lastItemCounts.TryGetValue(id, out var prev) || prev != cnt)
                        {
                            lastItemCounts[id] = cnt;
                            changed = true;
                        }
                    }
                    catch { }
                }

                if (changed) ImportInventoryForCharacter(sc);
            }
            catch { }
        }

        private unsafe void ImportInventoryForCharacter(StoredCharacter sc)
        {
            try
            {
                var invMgr = InventoryManager.Instance();
                if (invMgr == null) return;

                var sheet = this.dataManager.GetExcelSheet<Item>();
                if (sheet == null) return;

                var elements = Enum.GetValues(typeof(Element)).Cast<Element>().ToArray();
                var typeNames = new Dictionary<CrystalType, string>
                {
                    { CrystalType.Shard, "Shard" },
                    { CrystalType.Crystal, "Crystal" },
                    { CrystalType.Cluster, "Cluster" }
                };

                // iterate items and match by name pattern like "Fire Shard"
                foreach (var item in sheet)
                {
                    try
                    {
                        var itemName = item.Name.ToString();
                        if (string.IsNullOrEmpty(itemName)) continue;

                        foreach (var el in elements)
                        {
                            foreach (var kv in typeNames)
                            {
                                var expected = el.ToString() + " " + kv.Value;
                                if (!itemName.Equals(expected, StringComparison.OrdinalIgnoreCase) && !itemName.Contains(expected, StringComparison.OrdinalIgnoreCase))
                                    continue;

                                var count = (long)invMgr->GetInventoryItemCount((uint)item.RowId);
                                sc.Inventory.SetCount(el, kv.Key, count);
                                // once matched, skip other types for this item
                                break;
                            }
                        }
                    }
                    catch
                    {
                        // ignore per-item errors
                    }
                }
            }
            catch
            {
                // ignore import errors
            }
        }

        private void BuildTrackedItemMap()
        {
            // Use an explicit ID map for crystals/shards/clusters to avoid language issues
            try
            {
                var explicitMap = new Dictionary<uint, (Element, CrystalType)>
                {
                    { 2u, (Element.Fire, CrystalType.Shard) },
                    { 3u, (Element.Ice, CrystalType.Shard) },
                    { 4u, (Element.Wind, CrystalType.Shard) },
                    { 5u, (Element.Earth, CrystalType.Shard) },
                    { 6u, (Element.Lightning, CrystalType.Shard) },
                    { 7u, (Element.Water, CrystalType.Shard) },
                    { 8u, (Element.Fire, CrystalType.Crystal) },
                    { 9u, (Element.Ice, CrystalType.Crystal) },
                    { 10u, (Element.Wind, CrystalType.Crystal) },
                    { 11u, (Element.Earth, CrystalType.Crystal) },
                    { 12u, (Element.Lightning, CrystalType.Crystal) },
                    { 13u, (Element.Water, CrystalType.Crystal) },
                    { 14u, (Element.Fire, CrystalType.Cluster) },
                    { 15u, (Element.Ice, CrystalType.Cluster) },
                    { 16u, (Element.Wind, CrystalType.Cluster) },
                    { 17u, (Element.Earth, CrystalType.Cluster) },
                    { 18u, (Element.Lightning, CrystalType.Cluster) },
                    { 19u, (Element.Water, CrystalType.Cluster) },
                };

                trackedItems.Clear();
                foreach (var kv in explicitMap)
                {
                    trackedItems[kv.Key] = kv.Value;
                }
            }
            catch { }
        }
    }
                    // end foreach item
}
