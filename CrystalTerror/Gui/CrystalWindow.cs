using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface.Windowing;
using System.Text;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;

namespace CrystalTerror
{
    /// <summary>
    /// Main UI window rendering the crystal/retainer table and controls.
    /// Contains drawing logic and helpers used by the plugin UI.
    /// </summary>
    public unsafe class CrystalWindow : Window, IDisposable
    {
        private static string ExtractDisplayString(object? obj)
        {
            if (obj == null) return string.Empty;
            try
            {
                if (obj is string s) return s;
                var t = obj.GetType();

                // Special-case Lumina Excel row types: Try to find a Name-like property and extract recursively
                var full = t.FullName ?? string.Empty;
                if (full.StartsWith("Lumina.Excel.Sheets."))
                {
                    // Try common property names first
                    var candidates = new[] { "Name", "Name English", "Name_en", "Name_English", "NameRaw", "PlaceName", "TownName", "Value" };
                    foreach (var cn in candidates)
                    {
                        var p = t.GetProperty(cn) ?? t.GetProperty(cn.Replace(" ", "")) ?? t.GetProperty(cn.Replace(" ", "_"));
                        if (p != null)
                        {
                            var v = p.GetValue(obj);
                            var extracted = ExtractDisplayString(v);
                            if (!string.IsNullOrWhiteSpace(extracted) && !extracted.Contains("Lumina.")) return extracted;
                        }
                    }

                    // Fallback: inspect all properties for a Name-like suffix
                    foreach (var p in t.GetProperties())
                    {
                        if (p.Name.EndsWith("Name", StringComparison.OrdinalIgnoreCase) || p.Name.IndexOf("name", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            try
                            {
                                var v = p.GetValue(obj);
                                var extracted = ExtractDisplayString(v);
                                if (!string.IsNullOrWhiteSpace(extracted) && !extracted.Contains("Lumina.")) return extracted;
                            }
                            catch { }
                        }
                    }
                }

                // Common patterns: property "Name" or "Value"
                var prop = t.GetProperty("Name") ?? t.GetProperty("Value");
                if (prop != null)
                {
                    var v = prop.GetValue(obj);
                    var rec = ExtractDisplayString(v);
                    if (!string.IsNullOrWhiteSpace(rec) && !rec.Contains("Lumina.")) return rec;
                }

                // Fallback to ToString()
                var to = obj.ToString() ?? string.Empty;
                return to;
            }
            catch
            {
                return string.Empty;
            }
        }

        private string? GetLocalPlayerHomeWorld()
        {
            try
            {
                // Prefer PlayerState.LocalPlayer for home-world info
                var ps = this.plugin?.PlayerState;
                if (ps != null)
                {
                    try
                    {
                        var t = ps.GetType();
                        var prop = t.GetProperty("HomeWorld") ?? t.GetProperty("HomeWorldId") ?? t.GetProperty("HomeWorldRaw") ?? t.GetProperty("HomeWorldName");
                        if (prop != null)
                        {
                            var val = prop.GetValue(ps);
                            var s = ExtractDisplayString(val);
                            if (!string.IsNullOrWhiteSpace(s))
                            {
                                return s;
                            }
                        }
                    }
                    catch { }
                }

                // Fallback: Try ObjectTable.LocalPlayer (may not have world info)
                try
                {
                    var ot = this.plugin?.ObjectTable;
                    var lp2 = ot?.LocalPlayer;
                    if (lp2 != null)
                    {
                        var t2 = lp2.GetType();
                        var prop2 = t2.GetProperty("HomeWorld") ?? t2.GetProperty("HomeWorldId") ?? t2.GetProperty("HomeWorldRaw") ?? t2.GetProperty("HomeWorldName");
                        if (prop2 != null)
                        {
                            var val2 = prop2.GetValue(lp2);
                            var s2 = ExtractDisplayString(val2);
                            if (!string.IsNullOrWhiteSpace(s2))
                            {
                                return s2;
                            }
                            else
                            {
                                try { this.plugin!.Log?.Info($"ObjectTable.LocalPlayer.HomeWorld value present but extraction returned empty. Type={val2?.GetType()?.FullName}, ToString={(val2?.ToString() ?? "<null>")}"); } catch { }
                            }
                        }
                    }
                }
                catch { }
            }
            catch { }
            return null;
        }

        private string? GetLocalPlayerName()
        {
            try
            {
                // Try several sources for the local player name in order of reliability
                object? playerNameSource = null;
                try { playerNameSource = this.plugin?.PlayerState?.GetType().GetProperty("Name")?.GetValue(this.plugin?.PlayerState); } catch { }
                if (playerNameSource == null)
                {
                    try { playerNameSource = this.plugin?.ClientState?.LocalPlayer?.Name; } catch { }
                }
                if (playerNameSource == null)
                {
                    try { playerNameSource = this.plugin?.ObjectTable?.LocalPlayer?.Name; } catch { }
                }

                var pn = ExtractDisplayString(playerNameSource);
                if (!string.IsNullOrWhiteSpace(pn)) return pn;
            }
            catch { }
            return null;
        }

        private static string NormalizeStoredDisplay(string? storedName, string key)
        {
            try
            {
                var name = storedName ?? string.Empty;
                // Hide generic literal "Player" in UI; prefer empty so callers can decide display
                if (string.Equals(name, "Player", StringComparison.OrdinalIgnoreCase))
                    name = string.Empty;
                // If the stored name looks like a Lumina type string, prefer the key parts
                if (name.Contains("Lumina.") || name.Contains("Row`)" ) || name.Contains("Excel.Row"))
                    name = string.Empty;

                var k = key ?? string.Empty;
                var atIdx = k.IndexOf('@');
                var world = atIdx >= 0 && atIdx < k.Length - 1 ? k.Substring(atIdx + 1) : string.Empty;
                var keyName = atIdx > 0 ? k.Substring(0, atIdx) : k;

                if (string.IsNullOrWhiteSpace(name))
                {
                    name = keyName ?? string.Empty;
                }

                if (!string.IsNullOrWhiteSpace(world))
                {
                    // If name already contains world, don't duplicate
                    if (!name.Contains(world))
                        return name + " (" + world + ")";
                }

                // If there is no name and no key provided, show a friendly placeholder for the local player
                if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(key))
                    return "(you)";

                return name;
            }
            catch { return storedName ?? key ?? string.Empty; }
        }
        private readonly CrystalTerror plugin;
        private readonly System.Collections.Generic.Dictionary<string, bool> expanded = new(System.StringComparer.OrdinalIgnoreCase);
        private int selectedTab = 0; // 0=Overview,1=Filters,2=Settings
        // UI: Selected retainers filter (entries formatted as "Key|RetainerName"; use "Key|*" for all retainers of a character)
        private System.Collections.Generic.HashSet<string> selectedRetainers = new(System.StringComparer.OrdinalIgnoreCase);
        private string retainerSearch = string.Empty;
        // Cache Lumina id map for shards/crystals/clusters to avoid rebuilding every draw
        private System.Collections.Generic.Dictionary<uint, (string type, string element)>? idMapAll = null;
        // Cache last scan results and throttle scans to reduce draw-time work
        private System.Collections.Generic.List<CristalRow>? cachedCounts = null;
        private System.DateTime lastScanTime = System.DateTime.MinValue;
        private readonly UiSettings uiSettings = new UiSettings();
        private bool didInitialScanAttempt = false;
        // Removed unused field didExtendedPlayerScan
        // Background IPC worker fields
        private System.Threading.CancellationTokenSource? ipcCancellation;
        private System.Threading.Tasks.Task? ipcTask;
        private readonly object ipcLock = new();
        // Debounced config save fields
        private System.Threading.CancellationTokenSource? saveCts;
        private readonly object saveLock = new();
        
        // Background scan guard to avoid concurrent heavy scans on the main thread
        private bool scanInProgress = false;
        private readonly object scanGuard = new();
        // Per-retainer cached aggregates: retainerId -> (lastUpdatedUtc, counts by "Type:Element")
        private System.Collections.Generic.Dictionary<ulong, (System.DateTime lastUpdatedUtc, System.Collections.Generic.Dictionary<string, long> counts)> perRetainerAggregates = new();
        // Snapshot of known retainer ids (set by main-thread scan) for the background worker to use
        private System.Collections.Generic.List<ulong> retainerIdSnapshot = new();
        

        private static readonly string[] Elements = new[] { "Fire", "Ice", "Wind", "Earth", "Lightning", "Water" };
        private static readonly string[] Types = new[] { "Shard", "Crystal", "Cluster" };
        private static string ElementShort(string el)
        {
            try
            {
                if (string.IsNullOrEmpty(el)) return el ?? string.Empty;
                return el.Length >= 2 ? el.Substring(0, 2) : el;
            }
            catch { return el ?? string.Empty; }
        }
        // Relevant item RowIds for elemental shards/crystals/clusters (inclusive range provided by user)
        private static readonly System.Collections.Generic.HashSet<uint> RelevantItemIds =
            new(System.Linq.Enumerable.Range(2, 18).Select(i => (uint)i));
        private static readonly InventoryType[] PlayerInventoriesToScan = new[]
        {
            InventoryType.Inventory1,
            InventoryType.Inventory2,
            InventoryType.Inventory3,
            InventoryType.Inventory4,
        };

        public CrystalWindow(CrystalTerror plugin) : base("Crystal Terror")
        {
            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new System.Numerics.Vector2(400, 150),
                MaximumSize = new System.Numerics.Vector2(2000, 2000),
            };

            this.plugin = plugin;
                try { this.plugin!.Framework.Update += this.OnFrameworkUpdate; } catch { }
            try { this.MigrateStoredCharacters(); } catch { }
            // Subscribe to Dalamud inventory change events if enabled
            try
            {
                if (this.plugin!.Config.UseInventoryEvents && this.plugin.GameInventory != null)
                {
                    this.plugin.GameInventory.InventoryChanged += this.OnGameInventoryChangelog;
                    try { this.plugin.GameInventory.ItemAddedExplicit += this.OnItemAddedExplicit; } catch { }
                    try { this.plugin.GameInventory.ItemRemovedExplicit += this.OnItemRemovedExplicit; } catch { }
                    try { this.plugin.GameInventory.ItemChangedExplicit += this.OnItemChangedExplicit; } catch { }
                }
            }
            catch { }
            try
            {
                try { this.plugin!.ClientState.TerritoryChanged += this.OnTerritoryChanged; } catch { }
                try { this.plugin!.ClientState.MapIdChanged += this.OnMapIdChanged; } catch { }
            }
            catch { }
            // Prebuild the Lumina id map once at construction to avoid heavy work during Draw
            try
            {
                this.EnsureIdMapBuilt();
            }
            catch
            {
                // Ignore failures here; ScanInventories will attempt again if needed
            }
            // Start background IPC worker
            try
            {
                this.ipcCancellation = new System.Threading.CancellationTokenSource();
                var token = this.ipcCancellation.Token;
                this.ipcTask = System.Threading.Tasks.Task.Run(() =>
                {
                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                                // Snapshot retainer ids and idMap for thread-safety
                            List<ulong> snapIds;
                            lock (this.ipcLock) { snapIds = new List<ulong>(this.retainerIdSnapshot); }
                            var idMapLocal = this.idMapAll;

                            var itemCountIpc = this.plugin!.PluginInterface.GetIpcSubscriber<uint, ulong, uint, uint>("AllaganTools.ItemCount");
                            if (itemCountIpc != null && idMapLocal != null && snapIds.Count > 0)
                            {
                                var newAgg = new System.Collections.Generic.Dictionary<ulong, (System.DateTime, System.Collections.Generic.Dictionary<string, long>)>();
                                foreach (var rid in snapIds)
                                {
                                    var counts = new System.Collections.Generic.Dictionary<string, long>(System.StringComparer.OrdinalIgnoreCase);
                                    foreach (var kv in idMapLocal)
                                    {
                                        try
                                        {
                                            var qty = (long)itemCountIpc.InvokeFunc(kv.Key, rid, (uint)InventoryType.RetainerCrystals);
                                            if (qty > 0)
                                            {
                                                var key = kv.Value.type + ":" + kv.Value.element;
                                                counts.TryGetValue(key, out var cur); counts[key] = cur + qty;
                                            }
                                        }
                                        catch { }
                                    }
                                    newAgg[rid] = (System.DateTime.UtcNow, counts);
                                }

                                lock (this.ipcLock)
                                {
                                    this.perRetainerAggregates = newAgg;
                                    try { this.RequestSaveDebounced(); } catch { }
                                }
                            }
                        }
                        catch { }
                        // Wait with cancellation support
                        try { if (token.WaitHandle.WaitOne(this.uiSettings.Timing.IpcIntervalMs)) break; } catch { break; }
                    }
                }, token);
            }
            catch { }
        }

        private void MigrateStoredCharacters()
        {
            try
            {
                var cfg = this.plugin?.Config;
                if (cfg == null || cfg.StoredCharacters == null || cfg.StoredCharacters.ByCharacter == null) return;

                var old = cfg.StoredCharacters.ByCharacter;
                var updated = new System.Collections.Generic.Dictionary<string, StoredCharacter>(System.StringComparer.OrdinalIgnoreCase);

                var currentWorld = this.GetLocalPlayerHomeWorld();
                var currentPlayerName = ExtractDisplayString(this.plugin?.ObjectTable?.LocalPlayer?.Name) ?? string.Empty;
                foreach (var kv in old)
                {
                    try
                    {
                        var key = kv.Key ?? string.Empty;
                        var sc = kv.Value ?? new StoredCharacter();

                        // Normalize stored character display name if it looks like a Lumina row/type string
                        var name = sc.Name ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(name) || name.Contains("Lumina.") || name.Contains("Excel.Row") || name.Contains("Row`") || name.Contains("Row\u0060"))
                        {
                            // Prefer the name part of the key (before '@') when possible
                            var at = key.IndexOf('@');
                            var keyName = at > 0 ? key.Substring(0, at) : key;
                            if (!string.IsNullOrWhiteSpace(keyName)) name = keyName;
                        }


                        // Determine world: prefer current player's detected world when this entry matches current player
                        var world = string.Empty;
                        var atIdx = key.IndexOf('@');
                        if (atIdx >= 0 && atIdx < key.Length - 1) world = key.Substring(atIdx + 1);
                        // If this stored entry matches the current player name, and we have a detected current world, prefer it
                        if (!string.IsNullOrWhiteSpace(currentPlayerName) && (string.Equals(name, currentPlayerName, StringComparison.OrdinalIgnoreCase) || string.Equals(key, currentPlayerName, StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrWhiteSpace(currentWorld))
                            world = currentWorld;

                        // Build canonical key: "Name@World" if world exists, else just name
                        var canonical = string.IsNullOrWhiteSpace(world) ? name : (name + "@" + world);

                        // Ensure sc.Name is readable
                        sc.Name = string.IsNullOrWhiteSpace(name) ? (string.IsNullOrWhiteSpace(key) ? "" : key) : name;

                        // Insert or merge (prefer newer if duplicate canonical exists)
                        updated[canonical] = sc;
                    }
                    catch { }
                }

                cfg.StoredCharacters.ByCharacter = updated;
                try { this.plugin!.PluginInterface.SavePluginConfig(cfg); } catch { }
            }
            catch { }
        }

        /// <summary>
        /// Copy a raw inventory dump (player containers, retainer pages, retainer crystals) to the clipboard.
        /// Useful for debugging or exporting full item lists.
        /// </summary>
        public void CopyRawInventoryToClipboard()
        {
            var inventory = InventoryManager.Instance();
            var sheet = this.plugin!.DataManager.Excel.GetSheet<Item>();
            var sb = new System.Text.StringBuilder();
            if (inventory == null)
            {
                sb.AppendLine("(no inventory)");
                ImGui.SetClipboardText(sb.ToString());
                return;
            }

            void AppendContainer(InventoryContainer* cont, InventoryType t)
            {
                if (cont == null) return;
                sb.AppendLine($"Container: {t} (Size={cont->Size})");
                for (var i = 0; i < cont->Size; ++i)
                {
                    var s = cont->GetInventorySlot(i);
                    if (s == null || s->ItemId == 0) continue;
                    var id = (uint)s->ItemId;
                    var item = sheet.GetRowOrDefault(id);
                    var name = item.HasValue ? item.Value.Name.ToString() : "(unknown)";
                    long qty = 1;
                    try { qty = (long)inventory->GetItemCountInContainer(id, t); } catch { }
                    sb.AppendLine($"#{i}: {id} - {name} x{qty}");
                }
            }

            // Player inventories
            foreach (var t in PlayerInventoriesToScan)
                AppendContainer(inventory->GetInventoryContainer(t), t);

            // Retainer pages
            for (var t = InventoryType.RetainerPage1; t <= InventoryType.RetainerPage7; ++t)
                AppendContainer(inventory->GetInventoryContainer(t), t);

            // RetainerCrystals
            AppendContainer(inventory->GetInventoryContainer(InventoryType.RetainerCrystals), InventoryType.RetainerCrystals);

            ImGui.SetClipboardText(sb.ToString());
        }

        private void EnsureIdMapBuilt()
        {
            if (this.idMapAll != null) return;
            var sheetAll = this.plugin!.DataManager.Excel.GetSheet<Item>();
            var map = new System.Collections.Generic.Dictionary<uint, (string type, string element)>();
            if (sheetAll != null)
            {
                foreach (var row in sheetAll)
                {
                    try
                    {
                        var nm = row.Name.ToString();
                        if (nm.IndexOf("Shard", StringComparison.OrdinalIgnoreCase) < 0
                            && nm.IndexOf("Cluster", StringComparison.OrdinalIgnoreCase) < 0
                            && nm.IndexOf("Crystal", StringComparison.OrdinalIgnoreCase) < 0)
                            continue;

                        string type = "Unknown";
                        if (nm.IndexOf("Shard", StringComparison.OrdinalIgnoreCase) >= 0) type = "Shard";
                        else if (nm.IndexOf("Cluster", StringComparison.OrdinalIgnoreCase) >= 0) type = "Cluster";
                        else if (nm.IndexOf("Crystal", StringComparison.OrdinalIgnoreCase) >= 0) type = "Crystal";

                        var elMatch = Elements.FirstOrDefault(el => nm.IndexOf(el, StringComparison.OrdinalIgnoreCase) >= 0) ?? "Unknown";
                        map[row.RowId] = (type, elMatch);
                    }
                    catch { }
                }
            }

            // Ensure deterministic mapping for the known relevant ID range (2..19).
            // This avoids relying on name-based fallback when Lumina parsing fails.
            foreach (var id in RelevantItemIds)
            {
                if (map.ContainsKey(id))
                    continue;

                var idx = (int)(id - 2); // 0-based index into the 18 expected items
                var typeIndex = idx / Elements.Length;
                var elIndex = idx % Elements.Length;
                var type = (typeIndex >= 0 && typeIndex < Types.Length) ? Types[typeIndex] : "Unknown";
                var element = (elIndex >= 0 && elIndex < Elements.Length) ? Elements[elIndex] : "Unknown";
                map[id] = (type, element);
            }

            this.idMapAll = map;
        }

        /// <summary>Dispose of the window, unregister events and save the configuration.</summary>
        public void Dispose()
        {
            // Ensure final state saved
            try { this.SaveConfigNow(); } catch { }

            try
            {
                if (this.plugin != null && this.plugin.Framework != null)
                    this.plugin!.Framework.Update -= this.OnFrameworkUpdate;
            }
            catch { }

            try
            {
                if (this.plugin?.GameInventory != null)
                {
                    this.plugin.GameInventory.InventoryChanged -= this.OnGameInventoryChangelog;
                    try { this.plugin.GameInventory.ItemAddedExplicit -= this.OnItemAddedExplicit; } catch { }
                    try { this.plugin.GameInventory.ItemRemovedExplicit -= this.OnItemRemovedExplicit; } catch { }
                    try { this.plugin.GameInventory.ItemChangedExplicit -= this.OnItemChangedExplicit; } catch { }
                }
            }
            catch { }

            try
            {
                if (this.plugin?.ClientState != null)
                {
                    try { this.plugin.ClientState.TerritoryChanged -= this.OnTerritoryChanged; } catch { }
                    try { this.plugin.ClientState.MapIdChanged -= this.OnMapIdChanged; } catch { }
                }
            }
            catch { }

            try
            {
                if (this.ipcCancellation != null)
                {
                    this.ipcCancellation.Cancel();
                    try { this.ipcTask?.Wait(1000); } catch { }
                    this.ipcCancellation.Dispose();
                    this.ipcCancellation = null;
                }
            }
            catch { }
        }

        /// <summary>Render the main plugin window contents. Called by the Dalamud UI loop.</summary>
        public override void Draw()
        {
                // Draw centered checkbox groups: Types (Shard/Crystal/Cluster) above Elements
            var changedAny = false;

            void DrawCenteredGroup(string id, string[] labels, System.Collections.Generic.Dictionary<string, bool> map)
            {
                ImGui.PushID(id);
                // Compute approximate total width
                var avail = ImGui.GetContentRegionAvail().X;
                var total = 0.0f;
                var spacing = this.uiSettings.Spacing.CenteredGroupSpacing; // approximate spacing between checkboxes
                for (var i = 0; i < labels.Length; ++i)
                {
                    var ts = ImGui.CalcTextSize(labels[i]);
                    total += ts.X;
                }
                total += spacing * (labels.Length - 1);

                var cur = ImGui.GetCursorPosX();
                var offset = Math.Max(0, (avail - total) / 2);
                ImGui.SetCursorPosX(cur + offset);

                for (var i = 0; i < labels.Length; ++i)
                {
                    var label = labels[i];
                    if (!map.ContainsKey(label))
                        map[label] = true;

                    var val = map[label];
                    if (ImGui.Checkbox(label, ref val))
                    {
                        map[label] = val;
                        changedAny = true;
                    }

                    if (i < labels.Length - 1)
                        ImGui.SameLine();
                }

                ImGui.PopID();
            }

            // Filters tab contains the type/element toggles
            if (this.selectedTab == 1)
            {
                // Types first
                DrawCenteredGroup("type_filters", Types, this.plugin.Config.TypesEnabled);
                ImGui.Spacing();
                // Elements below
                DrawCenteredGroup("element_filters", Elements, this.plugin.Config.ElementsEnabled);

                if (changedAny)
                    this.plugin.PluginInterface.SavePluginConfig(this.plugin.Config);
            }

            // Settings button in top-right of window content
            try
            {
                var pad = ImGui.GetStyle().FramePadding.X;
                var btnSize = this.uiSettings.Spacing.SettingsButtonSize;
                var xpos = ImGui.GetWindowContentRegionMax().X - btnSize - pad;
                ImGui.SetCursorPosX(xpos);
                if (ImGui.Button("⚙##ct_settings", new System.Numerics.Vector2(btnSize, btnSize)))
                {
                    try { this.plugin?.OpenConfigUi(); } catch { this.selectedTab = 2; }
                }
            }
            catch
            {
                // Ignore positioning failures
            }


            // Use cached scan results filled on framework updates to avoid doing heavy work in Draw
            // Instead of only showing the current character, build the table data from persisted
            // `StoredCharacters` so all characters and retainers are displayed at all times.
            // Merge the live `cachedCounts` for the currently logged-in character when available.
            var counts = new System.Collections.Generic.List<CristalRow>();

            // Ensure we have done at least one synchronous scan if needed
            if ((this.cachedCounts == null || this.cachedCounts.Count == 0) && !this.didInitialScanAttempt)
            {
                this.didInitialScanAttempt = true;
                try
                {
                    var now = System.DateTime.Now;
                    var resInit = this.ScanInventories();
                    this.cachedCounts = resInit ?? new System.Collections.Generic.List<CristalRow>();
                    try { this.EnsureSaveIfCachedValid(); } catch { }
                    this.lastScanTime = now;
                }
                catch { }
            }

            // Compute the current player's stored-key (name@world when available) to prefer live data
            var currentKey = "";
            try
            {
                var playerNameLocal = this.GetLocalPlayerName();
                currentKey = playerNameLocal ?? string.Empty;
                try
                {
                    var world = this.GetLocalPlayerHomeWorld();
                    if (!string.IsNullOrEmpty(world)) currentKey = currentKey + "@" + world;
                }
                catch { }
            }
            catch { }

            var stored = this.plugin?.Config?.StoredCharacters?.ByCharacter;
            // Layout: main area only (menu moved to config window)
            try
            {
                ImGui.BeginChild("main", new System.Numerics.Vector2(0, 0), false);
            }
            catch { }

            // Main content: Overview (selection + table)
            if (this.selectedTab == 0)
            {
                try
                {
                    ImGui.Separator();
                    ImGui.Text("Select retainers:");
                    ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                    if (ImGui.BeginCombo("##ct_sel", $"Selected {this.selectedRetainers.Count}", ImGuiComboFlags.HeightLarge))
                    {
                        ImGui.InputTextWithHint("##ct_search", "Character or retainer search", ref this.retainerSearch, 100);
                        if (stored != null)
                        {
                            foreach (var kv in stored.OrderBy(k => k.Key, System.StringComparer.OrdinalIgnoreCase))
                            {
                                var key = kv.Key ?? string.Empty;
                                var sc = kv.Value;
                                var display = NormalizeStoredDisplay(sc?.Name ?? string.Empty, key);

                                // Simple search filter (matches display or any retainer name)
                                if (!string.IsNullOrWhiteSpace(this.retainerSearch))
                                {
                                    var s = this.retainerSearch;
                                    var matchesDisplay = (!string.IsNullOrWhiteSpace(display) && display.Contains(s, StringComparison.OrdinalIgnoreCase));
                                    var matchesRetainer = false;
                                    if (sc?.RetainerCounts != null)
                                    {
                                        foreach (var r in sc.RetainerCounts.Keys)
                                        {
                                            if (!string.IsNullOrWhiteSpace(r) && r.Contains(s, StringComparison.OrdinalIgnoreCase)) { matchesRetainer = true; break; }
                                        }
                                    }
                                    if (!matchesDisplay && !matchesRetainer) continue;
                                }

                                ImGui.PushID(key);
                                var allKey = key + "|*";
                                var allSel = this.selectedRetainers.Contains(allKey);
                                if (ImGui.Checkbox(display + "##all", ref allSel))
                                {
                                    if (allSel)
                                    {
                                        this.selectedRetainers.Add(allKey);
                                        if (sc?.RetainerCounts != null)
                                        {
                                            foreach (var r in sc.RetainerCounts.Keys)
                                                this.selectedRetainers.Add(key + "|" + r);
                                        }
                                    }
                                    else
                                    {
                                        this.selectedRetainers.Remove(allKey);
                                        if (sc?.RetainerCounts != null)
                                        {
                                            foreach (var r in sc.RetainerCounts.Keys)
                                                this.selectedRetainers.Remove(key + "|" + r);
                                        }
                                    }
                                }

                                ImGui.Indent();
                                if (sc?.RetainerCounts != null)
                                {
                                    foreach (var r in sc.RetainerCounts.Keys)
                                    {
                                        var entry = key + "|" + r;
                                        var sel = this.selectedRetainers.Contains(entry);
                                        var label = r + "##" + entry;
                                        if (ImGui.Checkbox(label, ref sel))
                                        {
                                            if (sel) this.selectedRetainers.Add(entry);
                                            else this.selectedRetainers.Remove(entry);
                                        }
                                    }
                                }
                                ImGui.Unindent();
                                ImGui.PopID();
                            }
                        }
                        ImGui.EndCombo();

                        ImGui.Separator();
                        if (ImGui.Button("Deselect All"))
                            this.selectedRetainers.Clear();
                        ImGui.SameLine();
                        if (ImGui.Button("Select All"))
                        {
                            this.selectedRetainers.Clear();
                            if (stored != null)
                            {
                                foreach (var kv in stored)
                                {
                                    var key = kv.Key ?? string.Empty;
                                    var sc = kv.Value;
                                    var allKey = key + "|*";
                                    this.selectedRetainers.Add(allKey);
                                    if (sc?.RetainerCounts != null)
                                    {
                                        foreach (var r in sc.RetainerCounts.Keys)
                                            this.selectedRetainers.Add(key + "|" + r);
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            // Build columns: Character | (for each Type) (for each enabled Element) -> e.g. Shard Fire, Shard Ice, ... Crystal Fire ... Cluster Water
            var enabledElements = Elements.Where(e => { bool b = false; return (this.plugin?.Config?.ElementsEnabled?.TryGetValue(e, out b) ?? false) && b; }).ToArray();
            var enabledTypes = Types.Where(t => { bool b = false; return (this.plugin?.Config?.TypesEnabled?.TryGetValue(t, out b) ?? false) && b; }).ToArray();
            var colCount = 1 + enabledTypes.Length * enabledElements.Length; // Character + combinations

            if (this.selectedTab == 0)
            {
                // Render each character as its own collapsible header + table. No outer table wrapper so headers can span full width.
                {

                // Build groups: each group represents one character (player) and its retainers
                var groups = new System.Collections.Generic.List<(string UniqueKey, string DisplayName, System.Collections.Generic.Dictionary<string,long> PlayerCounts, System.Collections.Generic.List<CristalRow> Retainers)>();

                if (stored != null && stored.Count > 0)
                {
                    foreach (var kv in stored.OrderBy(k => k.Key, System.StringComparer.OrdinalIgnoreCase))
                    {
                        var key = kv.Key;
                        var sc = kv.Value;

                        if (!string.IsNullOrEmpty(currentKey) && string.Equals(key, currentKey, StringComparison.OrdinalIgnoreCase)
                            && this.cachedCounts != null && this.cachedCounts.Count > 0)
                        {
                                // Build group from live cachedCounts
                                var playerRow = this.cachedCounts[0];
                                var retainerRows = this.cachedCounts.Skip(1).ToList();
                                groups.Add((key, NormalizeStoredDisplay(playerRow.Character, key), new System.Collections.Generic.Dictionary<string,long>(playerRow.ElementCounts, System.StringComparer.OrdinalIgnoreCase), retainerRows));
                            continue;
                        }

                        var display = NormalizeStoredDisplay(sc.Name, key);

                        var playerCountsDict = new System.Collections.Generic.Dictionary<string,long>(sc.PlayerCounts ?? new System.Collections.Generic.Dictionary<string,long>(), System.StringComparer.OrdinalIgnoreCase);
                        var retList = new System.Collections.Generic.List<CristalRow>();
                        if (sc.RetainerCounts != null)
                        {
                            foreach (var r in sc.RetainerCounts)
                                retList.Add(new CristalRow(r.Key, new System.Collections.Generic.Dictionary<string,long>(r.Value ?? new System.Collections.Generic.Dictionary<string,long>(), System.StringComparer.OrdinalIgnoreCase)));
                        }
                        groups.Add((key, display, playerCountsDict, retList));
                    }
                }

                // If there was no stored entry for current player but we have a live scan, ensure it's shown
                if (!string.IsNullOrEmpty(currentKey) && (stored == null || !stored.ContainsKey(currentKey)) && this.cachedCounts != null && this.cachedCounts.Count > 0)
                {
                    var playerRow = this.cachedCounts[0];
                    var retainerRows = this.cachedCounts.Skip(1).ToList();
                    groups.Insert(0, (currentKey, NormalizeStoredDisplay(playerRow.Character, currentKey), new System.Collections.Generic.Dictionary<string,long>(playerRow.ElementCounts, System.StringComparer.OrdinalIgnoreCase), retainerRows));
                }

                // If selection filter is active, limit groups/retainers to the selected set
                var groupsToRender = groups;
                try
                {
                    if (this.selectedRetainers != null && this.selectedRetainers.Count > 0)
                    {
                        var filtered = new System.Collections.Generic.List<(string UniqueKey, string DisplayName, System.Collections.Generic.Dictionary<string,long> PlayerCounts, System.Collections.Generic.List<CristalRow> Retainers)>();
                        foreach (var g in groups)
                        {
                            var any = false;
                            var allKey = g.UniqueKey + "|*";
                            if (this.selectedRetainers.Contains(allKey)) any = true;
                            if (!any)
                            {
                                foreach (var r in g.Retainers)
                                {
                                    var entry = g.UniqueKey + "|" + r.Character;
                                    if (this.selectedRetainers.Contains(entry)) { any = true; break; }
                                }
                            }
                            if (any) filtered.Add(g);
                        }
                        groupsToRender = filtered;
                    }
                }
                catch { }

                // Render each character group as a collapsible section
                foreach (var g in groupsToRender)
                {
                    ImGui.PushID(g.UniqueKey);
                    var headerLabel = (string.IsNullOrWhiteSpace(g.DisplayName) ? g.UniqueKey : g.DisplayName) + "##" + g.UniqueKey;

                    // Compute totals for the header: player + retainers for each column
                    var summedRetainers = new System.Collections.Generic.Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                    if (g.Retainers != null)
                    {
                        foreach (var rr in g.Retainers)
                        {
                            // Respect selection filter when computing totals
                            if (this.selectedRetainers != null && this.selectedRetainers.Count > 0)
                            {
                                var allKey = g.UniqueKey + "|*";
                                var entryKey = g.UniqueKey + "|" + rr.Character;
                                if (!this.selectedRetainers.Contains(allKey) && !this.selectedRetainers.Contains(entryKey))
                                    continue;
                            }
                            foreach (var kv in rr.ElementCounts)
                            {
                                summedRetainers.TryGetValue(kv.Key, out var cur); summedRetainers[kv.Key] = cur + kv.Value;
                            }
                        }
                    }

                    var displayCountsForHeader = new System.Collections.Generic.Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                    if (g.PlayerCounts != null)
                    {
                        foreach (var kv in g.PlayerCounts) displayCountsForHeader[kv.Key] = kv.Value;
                    }
                    foreach (var kv in summedRetainers)
                    {
                        displayCountsForHeader.TryGetValue(kv.Key, out var cur); displayCountsForHeader[kv.Key] = cur + kv.Value;
                    }

                    // Prepare column keys in order (skip character column)
                    var colKeys = new System.Collections.Generic.List<string>();
                    foreach (var ty in enabledTypes)
                        foreach (var el in enabledElements)
                            colKeys.Add(ty + ":" + el);

                    // Constrain header label drawing to the character column width so name length doesn't affect column positions.
                    var contentMin = ImGui.GetWindowContentRegionMin().X;
                    var contentMax = ImGui.GetWindowContentRegionMax().X;
                    var totalW = contentMax - contentMin;
                    var nCols = colCount;
                    var dataCols = Math.Max(1, nCols - 1);

                    // Increase the base width reserved for the character name to reduce truncation.
                    // Reserve at least 120px or 20% of the available width, capped to half of total.
                    // Prefer to shrink the crystal/data columns first down to a minimum before reducing the character column.
                    var minCharWidth = this.uiSettings.Sizing.MinCharWidth;
                    var desiredCharWidth = Math.Max(minCharWidth, totalW * 0.20f);
                    if (desiredCharWidth > totalW * 0.5f) desiredCharWidth = totalW * 0.5f;
                    var minDataColWidth = this.uiSettings.Sizing.MinDataColWidth; // smallest acceptable width per crystal column before shrinking char column
                    var charWidth = desiredCharWidth;
                    var requiredDataTotalMin = minDataColWidth * dataCols;
                    var availableForDataWithDesiredChar = totalW - desiredCharWidth;
                    float dataColWidth;
                    if (availableForDataWithDesiredChar < requiredDataTotalMin)
                    {
                        // Need to free up space for data columns. Reduce charWidth down to keep data columns at min width,
                        // But never below minCharWidth.
                        var maxCharWidthAllowed = Math.Max(minCharWidth, totalW - requiredDataTotalMin);
                        if (maxCharWidthAllowed < charWidth)
                            charWidth = maxCharWidthAllowed;
                        dataColWidth = (totalW - charWidth) / (float)dataCols;
                        if (dataColWidth < 1.0f) dataColWidth = 1.0f;
                    }
                    else
                    {
                        dataColWidth = (totalW - charWidth) / (float)dataCols;
                    }

                    // Truncate the visible header name so it cannot push the column starts.
                    var fullName = string.IsNullOrWhiteSpace(g.DisplayName) ? g.UniqueKey : g.DisplayName;
                    var visibleName = fullName;
                    try
                    {
                        var arrowReserve = this.uiSettings.Spacing.ArrowReserve; // space for arrow and padding
                        var maxLabelWidth = Math.Max(this.uiSettings.Sizing.MinLabelWidth, charWidth - arrowReserve - this.uiSettings.Spacing.LabelInnerPadding);
                        var txtSize = ImGui.CalcTextSize(visibleName);
                        if (txtSize.X > maxLabelWidth)
                        {
                            // Trim and append ellipsis until it fits
                            var baseName = visibleName;
                            var keep = baseName.Length;
                            while (keep > 0 && ImGui.CalcTextSize(baseName.Substring(0, keep) + "...").X > maxLabelWidth)
                                keep--;
                            if (keep > 0)
                                visibleName = baseName.Substring(0, keep) + "...";
                            else
                                visibleName = "...";
                        }
                    }
                    catch { }

                    var visibleLabel = visibleName + "##" + g.UniqueKey;
                    var open = ImGui.CollapsingHeader(visibleLabel, ImGuiTreeNodeFlags.SpanAvailWidth);
                    if (visibleName != fullName)
                    {
                        try { if (ImGui.IsItemHovered()) ImGui.SetTooltip(fullName); } catch { }
                    }
                    // If collapsed, display totals aligned to the table columns on the same header line
                    if (!open)
                    {
                        try
                        {
                            if (totalW <= 0) { ImGui.SameLine(); ImGui.TextUnformatted(string.Empty); }
                            else
                            {
                                // Render each column value at its column start X (character column is index 0)
                                for (var ci = 0; ci < colKeys.Count; ++ci)
                                {
                                    var k = colKeys[ci];
                                    var v = displayCountsForHeader.TryGetValue(k, out var vv) ? vv : 0L;
                                    var vStr = v.ToString();

                                    // Derive type and element from key (format "Type:Element")
                                    var parts = k.Split(':');
                                    var ty = parts.Length > 0 ? parts[0] : string.Empty;
                                    var el = parts.Length > 1 ? parts[1] : string.Empty;

                                    // Show full element name in header labels unless the reserved character column
                                    // width is below a threshold, in which case use a two-letter abbreviation.
                                    var headerFullMinWidth = this.uiSettings.Sizing.HeaderFullMinWidth; // if charWidth is below this, abbreviate
                                    var useFullElement = charWidth >= headerFullMinWidth;
                                    var elemLabel = useFullElement ? el : ElementShort(el);
                                    var label = enabledTypes.Length == 1 ? (elemLabel + ":") : (ty + " " + elemLabel + ":");

                                    try
                                    {
                                        // Column left and right in absolute coords
                                        var columnLeft = ImGui.GetWindowPos().X + contentMin + charWidth + dataColWidth * ci;
                                        var columnRight = ImGui.GetWindowPos().X + contentMin + charWidth + dataColWidth * (ci + 1);

                                        // Draw label left-aligned inside column, then value immediately after the label
                                        ImGui.SameLine();
                                        var colRelLeft = columnLeft - ImGui.GetWindowPos().X + 4;
                                        var labelSz = ImGui.CalcTextSize(label);
                                        ImGui.SetCursorPosX(colRelLeft + this.uiSettings.Spacing.ColumnLeftPadding);
                                        ImGui.TextUnformatted(label);
                                        ImGui.SameLine();
                                        try
                                        {
                                            ImGui.SetCursorPosX(colRelLeft + labelSz.X + this.uiSettings.Spacing.LabelValueGap);
                                        }
                                        catch { }
                                    }
                                    catch { }

                                    ImGui.TextUnformatted(vStr);
                                }
                            }
                        }
                        catch { }
                    }
                    ImGui.PopID();

                    if (!open) continue;

                    var tableId = "cristalTable_" + g.UniqueKey;
                    if (ImGui.BeginTable(tableId, colCount, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders))
                    {
                        // First column fixed to charWidth so its width doesn't vary by name length
                        ImGui.TableSetupColumn("Character", ImGuiTableColumnFlags.WidthFixed, charWidth);
                        // Set each crystal/data column to a fixed equal width so all have the same space
                        foreach (var ty in enabledTypes)
                        {
                            foreach (var el in enabledElements)
                            {
                                // Use full element name for the table column header
                                ImGui.TableSetupColumn(ty + " " + el, ImGuiTableColumnFlags.WidthFixed, dataColWidth);
                            }
                        }
                        ImGui.TableHeadersRow();

                        // Player row
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGui.Text(g.DisplayName);
                        var colIdx = 0;
                        foreach (var ty in enabledTypes)
                        {
                            foreach (var el in enabledElements)
                            {
                                ImGui.TableNextColumn();
                                var key = ty + ":" + el;
                                var val = g.PlayerCounts != null && g.PlayerCounts.TryGetValue(key, out var cv) ? cv : 0L;
                                var valStr = val.ToString();
                                ImGui.TextUnformatted(valStr);
                                colIdx++;
                            }
                        }

                        // Retainer rows
                        if (g.Retainers != null && g.Retainers.Count > 0)
                        {
                            foreach (var r in g.Retainers)
                            {
                                // If selection filter active and this retainer is not selected, skip rendering
                                if (this.selectedRetainers != null && this.selectedRetainers.Count > 0)
                                {
                                    var allKey = g.UniqueKey + "|*";
                                    var entryKey = g.UniqueKey + "|" + r.Character;
                                    if (!this.selectedRetainers.Contains(allKey) && !this.selectedRetainers.Contains(entryKey))
                                        continue;
                                }

                                ImGui.TableNextRow();
                                ImGui.TableNextColumn();
                                ImGui.Indent((int)this.uiSettings.Spacing.RetainerIndent);
                                ImGui.Text(r.Character);
                                ImGui.Unindent();

                                var colIdx2 = 0;
                                foreach (var ty in enabledTypes)
                                {
                                    foreach (var el in enabledElements)
                                    {
                                        ImGui.TableNextColumn();
                                        var key = ty + ":" + el;
                                        var val = (r.ElementCounts != null && r.ElementCounts.TryGetValue(key, out var cv)) ? cv : 0;
                                        var valStr = val.ToString();
                                        ImGui.TextUnformatted(valStr);
                                        colIdx2++;
                                    }
                                }
                            }
                        }

                        ImGui.EndTable();
                    }
                }
                }
            }

            try { ImGui.EndChild(); } catch { }
        }

                /// <summary>Set the currently selected tab in the main window (0-based).</summary>
                public void SetSelectedTab(int tab)
        {
            if (tab < 0) tab = 0;
            this.selectedTab = tab;
        }

        private unsafe List<CristalRow> ScanInventories()
        {
            var results = new List<CristalRow>();

            var inventory = InventoryManager.Instance();

            var previousCachedName = this.cachedCounts != null && this.cachedCounts.Count > 0 ? this.cachedCounts[0].Character : null;

            var playerName = this.GetLocalPlayerName();
            // If name detection fails, prefer an existing readable cached name; otherwise keep empty so we don't display/save the literal "Player"
            if (string.IsNullOrWhiteSpace(playerName)
                && !string.IsNullOrWhiteSpace(previousCachedName)
                && !string.Equals(previousCachedName, "Player", System.StringComparison.OrdinalIgnoreCase))
            {
                playerName = previousCachedName;
            }
            // If still missing, leave playerName null/empty (we'll display a friendly placeholder via NormalizeStoredDisplay)
            if (inventory == null)
            {
                var displayName = string.IsNullOrWhiteSpace(playerName) ? "(you)" : playerName;
                results.Add(new CristalRow(displayName + " (no inventory)", new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)));
                return results;
            }

            // Helper to count in a container. Counts: "shard", "cluster", and per-element keys like "Fire", "Ice"
            static void CountContainer(InventoryContainer* container, InventoryType invType, Dictionary<string, long> counts, string[] elements, IDataManager dataManager, System.Collections.Generic.Dictionary<uint, (string type, string element)>? idMap = null)
            {
                if (container == null)
                    return;

                var sheet = dataManager.Excel.GetSheet<Item>();
                var inv = InventoryManager.Instance();
                var seen = new System.Collections.Generic.HashSet<uint>();
                for (var i = 0; i < container->Size; ++i)
                {
                    var slot = container->GetInventorySlot(i);
                    if (slot == null || slot->ItemId == 0)
                    {
                        continue;
                    }

                    var id = (uint)slot->ItemId;
                    if (!seen.Add(id))
                    {
                        continue; // already counted this item id in this container
                    }

                    // Determine match solely by ID map or by the fixed RelevantItemIds set.
                    var matched = false;
                    if (idMap != null)
                    {
                        matched = idMap.ContainsKey(id);
                    }
                    else
                    {
                        matched = RelevantItemIds.Contains(id);
                    }

                    if (!matched)
                        continue;

                    // Read qty
                    long qty = 1;
                    try
                    {
                        if (inv != null)
                        {
                            qty = (long)inv->GetItemCountInContainer(id, invType);
                            if (qty <= 0) qty = 1;
                        }
                    }
                    catch
                    {
                        qty = 1;
                    }

                    // Classify by idMap when present; otherwise use deterministic mapping for IDs 2..19
                    string type;
                    string element;
                    if (idMap != null && idMap.TryGetValue(id, out var mapEntry))
                    {
                        type = mapEntry.type;
                        element = mapEntry.element;
                    }
                    else
                    {
                        var idx = (int)(id - 2);
                        var typeIndex = idx / elements.Length;
                        var elIndex = idx % elements.Length;
                        type = (typeIndex >= 0 && typeIndex < Types.Length) ? Types[typeIndex] : "Unknown";
                        element = (elIndex >= 0 && elIndex < elements.Length) ? elements[elIndex] : "Unknown";
                    }

                    var key = type + ":" + element;
                    counts.TryGetValue(key, out var c); counts[key] = c + qty;
                }
            }

            var playerCounts = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

            // Ensure id map is built (built once at construction via EnsureIdMapBuilt)
            try { this.EnsureIdMapBuilt(); } catch { }

            // Scan common player inventory containers (includes saddlebags, armory and mail)
            var scannedTypes = new System.Collections.Generic.HashSet<InventoryType>();
            foreach (var t in PlayerInventoriesToScan)
            {
                scannedTypes.Add(t);
                var cont = inventory->GetInventoryContainer(t);
                CountContainer(cont, t, playerCounts, Elements, this.plugin.DataManager, this.idMapAll != null && this.idMapAll.Count > 0 ? this.idMapAll : null);
            }

            results.Add(new CristalRow(playerName ?? string.Empty, playerCounts));

            // Also scan other non-retainer containers not covered in PlayerInventoriesToScan
            // (handles uncommon storage locations). This avoids relying on a one-time fallback
            // And ensure crystals stored in other containers are counted.
            try
            {
                foreach (InventoryType t in Enum.GetValues(typeof(InventoryType)))
                {
                    // Skip retainer pages and retainer crystals (we only want character containers here)
                    if (t >= InventoryType.RetainerPage1 && t <= InventoryType.RetainerPage7) continue;
                    if (t == InventoryType.RetainerCrystals) continue;
                    if (scannedTypes.Contains(t)) continue; // already scanned above

                    var cont = inventory->GetInventoryContainer(t);
                    if (cont == null || cont->Size == 0) continue;

                    // Measure counts before/after to detect if this container contributed
                    long beforeTotal = playerCounts.Values.Sum();
                    CountContainer(cont, t, playerCounts, Elements, this.plugin.DataManager, this.idMapAll != null && this.idMapAll.Count > 0 ? this.idMapAll : null);
                    long afterTotal = playerCounts.Values.Sum();
                }

                // Update the cached first row to reflect merged player counts
                if (results.Count > 0)
                    results[0] = new CristalRow(playerName ?? string.Empty, playerCounts);
            }
            catch { }

            Dictionary<int, string>? retainerNames = null;
            if (this.plugin.Config.ShowRetainers)
                retainerNames = TryGetRetainerNames();

            if (this.plugin.Config.ShowRetainers)
            {
                // Try to get AllaganTools IPC for per-retainer counts in RetainerCrystals
                var itemCountIpc = this.plugin.PluginInterface.GetIpcSubscriber<uint, ulong, uint, uint>("AllaganTools.ItemCount");

                // Reuse the Lumina-derived id map built earlier for player scanning
                var idMap = idMapAll;
                // Try retainer pages (RetainerPage1..RetainerPage7) but only up to the actual number of retainers the account has
                var handledRetainerIds = new System.Collections.Generic.HashSet<ulong>();
                int maxDirectPages = 7;
                int actualPages = 0;
                try
                {
                    for (uint i = 0; i < (uint)maxDirectPages; ++i)
                    {
                        var ret = FFXIVClientStructs.FFXIV.Client.Game.RetainerManager.Instance()->GetRetainerBySortedIndex(i);
                        if (ret == null || ret->RetainerId == 0) break;
                        actualPages++;
                    }
                }
                catch
                {
                    // Ignore failures and fall back to showing up to maxDirectPages
                    actualPages = maxDirectPages;
                }

                var collectedRetainerIds = new System.Collections.Generic.List<ulong>();
                for (int pageIndex = 0; pageIndex < actualPages; ++pageIndex)
                {
                    var t = (InventoryType)((int)InventoryType.RetainerPage1 + pageIndex);
                    var pageNumber = (int)(t - InventoryType.RetainerPage1) + 1;
                    var cont = inventory->GetInventoryContainer(t);
                    // If container is null, still add an empty row so UI rows remain stable
                    var rCounts = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                    if (cont != null)
                    {
                        CountContainer(cont, t, rCounts, Elements, this.plugin.DataManager, idMap);
                    }

                    // If there's a retainer struct for this page and it's marked unavailable, skip or mark it depending on settings
                    try
                    {
                        var pageIdxLocal = (int)(t - InventoryType.RetainerPage1);
                        var retTemp = FFXIVClientStructs.FFXIV.Client.Game.RetainerManager.Instance()->GetRetainerBySortedIndex((uint)pageIdxLocal);
                        if (retTemp != null && !retTemp->Available && this.plugin.Config.SkipDisabledRetainers)
                            continue;
                    }
                    catch
                    {
                        // Ignore failures
                    }

                    // If we have AllaganTools IPC and can resolve a retainer id for this page, merge RetainerCrystals counts into this retainer
                    if (itemCountIpc != null)
                    {
                        try
                        {
                            var pageIdx = (int)(t - InventoryType.RetainerPage1);
                            var ret = FFXIVClientStructs.FFXIV.Client.Game.RetainerManager.Instance()->GetRetainerBySortedIndex((uint)pageIdx);
                            if (ret != null && ret->RetainerId != 0)
                            {
                                if (!ret->Available)
                                {
                                    if (this.plugin.Config.SkipDisabledRetainers)
                                        continue;
                                    // If not skipping, still mark this retainer handled so it's not double-added later
                                    handledRetainerIds.Add(ret->RetainerId);
                                }
                                else
                                {
                                    handledRetainerIds.Add(ret->RetainerId);
                                }

                                var rid = ret->RetainerId;
                                // Merge cached IPC aggregates if available, else fall back to direct per-item calls
                                (System.DateTime last, System.Collections.Generic.Dictionary<string, long> counts) cached;
                                var haveCached = false;
                                lock (this.ipcLock)
                                {
                                    if (this.perRetainerAggregates.TryGetValue(rid, out var v))
                                    {
                                        cached = v;
                                        haveCached = true;
                                    }
                                    else
                                        cached = (System.DateTime.MinValue, new System.Collections.Generic.Dictionary<string, long>());
                                }

                                if (haveCached)
                                {
                                    foreach (var kv in cached.counts)
                                    {
                                        rCounts.TryGetValue(kv.Key, out var cur); rCounts[kv.Key] = cur + kv.Value;
                                    }
                                }
                                else if (idMap != null)
                                {
                                    foreach (var kv in idMap)
                                    {
                                        try
                                        {
                                            var qty = (long)itemCountIpc.InvokeFunc(kv.Key, rid, (uint)InventoryType.RetainerCrystals);
                                            if (qty > 0)
                                            {
                                                var key = kv.Value.type + ":" + kv.Value.element;
                                                rCounts.TryGetValue(key, out var curr); rCounts[key] = curr + qty;
                                            }
                                        }
                                        catch { }
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // Ignore ipc/retainer lookup errors
                        }
                    }

                    var pageName = retainerNames != null && retainerNames.TryGetValue(pageNumber, out var rn) && !string.IsNullOrWhiteSpace(rn) ? rn : t.ToString();
                    try
                    {
                        var pageIdxLocal2 = (int)(t - InventoryType.RetainerPage1);
                        var retTemp2 = FFXIVClientStructs.FFXIV.Client.Game.RetainerManager.Instance()->GetRetainerBySortedIndex((uint)pageIdxLocal2);
                        if (retTemp2 != null && !retTemp2->Available && !this.plugin.Config.SkipDisabledRetainers)
                            pageName += " (unavailable)";
                    }
                    catch { }

                    results.Add(new CristalRow(pageName, rCounts));
                }
                // If IPC not available, fall back to adding the special RetainerCrystals container as its own row
                if (itemCountIpc == null)
                {
                    var rc = inventory->GetInventoryContainer(InventoryType.RetainerCrystals);
                    if (rc != null)
                    {
                        var rcEmpty = true;
                        for (var i = 0; i < rc->Size; ++i)
                        {
                            var s = rc->GetInventorySlot(i);
                            if (s != null && s->ItemId != 0) { rcEmpty = false; break; }
                        }

                        if (!rcEmpty)
                        {
                            var rcCounts = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                            CountContainer(rc, InventoryType.RetainerCrystals, rcCounts, Elements, this.plugin.DataManager, idMap);
                            results.Add(new CristalRow("RetainerCrystals", rcCounts));
                        }
                    }
                }

                // If we have AllaganTools IPC, also enumerate up to 10 sorted retainers and add rows for any retainers
                // That don't map to the standard RetainerPage1..RetainerPage7 containers (handles retainer slots 8..10)
                if (itemCountIpc != null)
                {
                    try
                    {
                        for (uint i = 0; i < 10; ++i)
                        {
                            var ret = FFXIVClientStructs.FFXIV.Client.Game.RetainerManager.Instance()->GetRetainerBySortedIndex(i);
                            if (ret == null || ret->RetainerId == 0) continue;
                            if (!ret->Available && this.plugin.Config.SkipDisabledRetainers) continue;
                            if (handledRetainerIds.Contains(ret->RetainerId)) continue;

                            var rid = ret->RetainerId;
                            // Collect retainer id for background worker
                            collectedRetainerIds.Add(rid);
                            var rCounts = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                            if (idMap != null)
                            {
                            foreach (var kv in idMap)
                            {
                                try
                                {
                                    var qty = (long)itemCountIpc.InvokeFunc(kv.Key, rid, (uint)InventoryType.RetainerCrystals);
                                    if (qty > 0)
                                    {
                                        var key = kv.Value.type + ":" + kv.Value.element;
                                        rCounts.TryGetValue(key, out var curr); rCounts[key] = curr + qty;
                                    }
                                }
                                catch { }
                            }
                            }

                            var idx = (int)i + 1;
                            var name = retainerNames != null && retainerNames.TryGetValue(idx, out var rn) && !string.IsNullOrWhiteSpace(rn) ? rn : ret->NameString;
                            if (!ret->Available && !this.plugin.Config.SkipDisabledRetainers)
                                name += " (unavailable)";
                            results.Add(new CristalRow(name, rCounts));
                        }
                    }
                    catch
                    {
                        // Ignore failures
                    }
                }
                // Publish collected retainer ids snapshot for background IPC worker
                try { lock (this.ipcLock) { this.retainerIdSnapshot = collectedRetainerIds; } } catch { }
            }

            // Persist scanned results into plugin config so data is kept across characters
            try
            {
                // Build a canonical key: require player name + home world (Name@World).
                var key = playerName ?? "";
                string? worldForKey = null;
                try
                {
                    worldForKey = this.GetLocalPlayerHomeWorld();
                }
                catch { worldForKey = null; }

                // If we don't have a detected world, do not persist stored character data
                if (string.IsNullOrWhiteSpace(worldForKey) || string.IsNullOrWhiteSpace(playerName) || string.Equals(playerName, "Player", System.StringComparison.OrdinalIgnoreCase))
                {
                    // Skip saving to config when we can't form a canonical Name@World key
                    return results;
                }

                key = playerName + "@" + worldForKey;
                var stored = new StoredCharacter();
                stored.Name = playerName ?? string.Empty;
                stored.LastUpdatedUtc = System.DateTime.UtcNow;
                stored.PlayerCounts = new System.Collections.Generic.Dictionary<string, long>(playerCounts ?? new System.Collections.Generic.Dictionary<string, long>(), System.StringComparer.OrdinalIgnoreCase);
                // Retainers: results entries after the first
                var retDict = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, long>>(System.StringComparer.OrdinalIgnoreCase);
                for (var i = 1; i < results.Count; ++i)
                {
                    var r = results[i];
                    // Clone counts into serializable dictionary
                    var cd = new System.Collections.Generic.Dictionary<string, long>(r.ElementCounts ?? new System.Collections.Generic.Dictionary<string, long>(), System.StringComparer.OrdinalIgnoreCase);
                    retDict[r.Character ?? string.Empty] = cd;
                }
                stored.RetainerCounts = retDict;

                var cfg = this.plugin.Config ?? (this.plugin.PluginInterface.GetPluginConfig() as CrystalConfig ?? new CrystalConfig());
                if (cfg.StoredCharacters == null) cfg.StoredCharacters = new StoredCharactersContainer();
                if (cfg.StoredCharacters.ByCharacter == null) cfg.StoredCharacters.ByCharacter = new System.Collections.Generic.Dictionary<string, StoredCharacter>(System.StringComparer.OrdinalIgnoreCase);
                // Validate canonical key again before writing
                var atIdx = key.IndexOf('@');
                if (atIdx > 0 && atIdx < key.Length - 1)
                {
                    // If any existing stored entry already has the same stored.Name, prefer updating that key
                    string writeKey = key;
                    try
                    {
                        if (cfg.StoredCharacters.ByCharacter != null)
                        {
                            var match = cfg.StoredCharacters.ByCharacter.FirstOrDefault(kv => string.Equals(kv.Value?.Name ?? string.Empty, stored.Name ?? string.Empty, StringComparison.OrdinalIgnoreCase));
                            if (!string.IsNullOrWhiteSpace(match.Key))
                            {
                                writeKey = match.Key;
                            }
                            else
                            {
                                writeKey = this.EnsureNonConflictingStoredKey(cfg, key, stored.Name);
                            }
                        }
                    }
                    catch { writeKey = key; }
                    cfg.StoredCharacters.ByCharacter[writeKey] = stored;
                }
                try { this.plugin.PluginInterface.SavePluginConfig(cfg); } catch { }
            }
            catch { }

            return results;
        }

        private void OnFrameworkUpdate(Dalamud.Plugin.Services.IFramework framework)
        {
            try
            {
                var now = System.DateTime.Now;
                if ((now - this.lastScanTime).TotalMilliseconds > this.uiSettings.Timing.ScanIntervalMs)
                {
                    // If a scan is already running in background, skip starting another
                    lock (this.scanGuard)
                    {
                        if (this.scanInProgress) return;
                        this.scanInProgress = true;
                    }

                    // Run the potentially expensive scan off the main thread to avoid freezes
                    var taskCt = System.Threading.CancellationToken.None;
                    _ = System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                                    var res = this.ScanInventories();
                                            var safeRes = res ?? new System.Collections.Generic.List<CristalRow>();
                                                lock (this.scanGuard)
                                                {
                                                    this.cachedCounts = safeRes;
                                                    try { this.EnsureSaveIfCachedValid(); } catch { }
                                                    this.lastScanTime = System.DateTime.Now;
                                                }
                        }
                        catch { }
                        finally
                        {
                            lock (this.scanGuard) { this.scanInProgress = false; }
                        }
                    }, taskCt);
                }
            }
            catch { }
        }

        private void OnGameInventoryChangelog(System.Collections.Generic.IReadOnlyCollection<Dalamud.Game.Inventory.InventoryEventArgTypes.InventoryEventArgs> events)
        {
            try
            {
                // Trigger an immediate scan when inventory changes are reported
                var res = this.ScanInventories();
                this.cachedCounts = res ?? new System.Collections.Generic.List<CristalRow>();
                try { this.EnsureSaveIfCachedValid(); } catch { }
                this.lastScanTime = System.DateTime.Now;
            }
            catch { }
        }

        private void OnItemAddedExplicit(Dalamud.Game.Inventory.InventoryEventArgTypes.InventoryItemAddedArgs args)
        {
            try
            {
                this.HandlePlayerInventoryDelta(args.Inventory, args.Item.ItemId, args.Item.Quantity);
            }
            catch { }
        }

        private void OnItemRemovedExplicit(Dalamud.Game.Inventory.InventoryEventArgTypes.InventoryItemRemovedArgs args)
        {
            try
            {
                // removed -> subtract quantity
                this.HandlePlayerInventoryDelta(args.Inventory, args.Item.ItemId, -args.Item.Quantity);
            }
            catch { }
        }

        private void OnItemChangedExplicit(Dalamud.Game.Inventory.InventoryEventArgTypes.InventoryItemChangedArgs args)
        {
            try
            {
                var oldQty = args.OldItemState.Quantity;
                var newQty = args.Item.Quantity;
                var delta = newQty - oldQty;
                if (delta != 0)
                    this.HandlePlayerInventoryDelta(args.Inventory, args.Item.ItemId, delta);
            }
            catch { }
        }

        private void HandlePlayerInventoryDelta(Dalamud.Game.Inventory.GameInventoryType inventoryType, uint itemId, int delta)
        {
            try
            {
                // Only handle main player inventories (Inventory1..Inventory4) and related player containers
                if ((int)inventoryType > 1000) // crude cutoff: player containers are low numbers, retainer/crystals are large
                    return;

                if (delta == 0) return;

                // Ensure cachedCounts seeded
                if (this.cachedCounts == null || this.cachedCounts.Count == 0)
                {
                    var res = this.ScanInventories();
                    this.cachedCounts = res ?? new System.Collections.Generic.List<CristalRow>();
                }

                // Ensure id map is ready
                try { this.EnsureIdMapBuilt(); } catch { }

                // Determine key (Type:Element)
                string key;
                if (this.idMapAll != null && this.idMapAll.TryGetValue(itemId, out var mapEntry))
                {
                    key = mapEntry.type + ":" + mapEntry.element;
                }
                else
                {
                    if (!RelevantItemIds.Contains(itemId)) return; // not a crystal/shard type we track
                    var idx = (int)(itemId - 2);
                    var typeIndex = idx / Elements.Length;
                    var elIndex = idx % Elements.Length;
                    var type = (typeIndex >= 0 && typeIndex < Types.Length) ? Types[typeIndex] : "Unknown";
                    var element = (elIndex >= 0 && elIndex < Elements.Length) ? Elements[elIndex] : "Unknown";
                    key = type + ":" + element;
                }

                // Update player's cached row (index 0)
                if (this.cachedCounts.Count == 0) return;
                var row = this.cachedCounts[0];
                var dict = row.ElementCounts;
                dict.TryGetValue(key, out var existing);
                var updated = existing + delta;
                if (updated <= 0)
                {
                    if (dict.ContainsKey(key)) dict.Remove(key);
                }
                else
                {
                    dict[key] = updated;
                }

                // refresh last scan timestamp so draw shows updated values
                this.lastScanTime = System.DateTime.Now;

                // Persist (debounced) the updated cachedCounts into plugin config
                try { this.RequestSaveDebounced(); } catch { }
            }
            catch { }
        }

        private void OnTerritoryChanged(ushort territoryId)
        {
            try { this.SaveConfigNow(); } catch { }
        }

        private void OnMapIdChanged(uint mapId)
        {
            try { this.SaveConfigNow(); } catch { }
        }

        private void RequestSaveDebounced()
        {
            lock (this.saveLock)
            {
                try { this.saveCts?.Cancel(); } catch { }
                this.saveCts = new System.Threading.CancellationTokenSource();
                var ct = this.saveCts.Token;
                _ = System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        // WaitOne returns true if the handle was signaled (canceled); false if timeout elapsed
                        var signaled = ct.WaitHandle.WaitOne(this.uiSettings.Timing.SaveDelayMs);
                        if (!signaled)
                        {
                            this.SaveConfigNow();
                        }
                    }
                    catch { }
                }, ct);
            }
        }

        // If we have a cached player row that appears valid (readable name and detected home world),
        // request a debounced save so the live player row is persisted to stored characters.
        private void EnsureSaveIfCachedValid()
        {
            try
            {
                if (this.cachedCounts == null || this.cachedCounts.Count == 0) return;
                var playerRow = this.cachedCounts[0];
                var playerName = playerRow.Character ?? string.Empty;
                if (string.IsNullOrWhiteSpace(playerName) || string.Equals(playerName, "Player", StringComparison.OrdinalIgnoreCase)) return;
                string? world = null;
                try { world = this.GetLocalPlayerHomeWorld(); } catch { world = null; }
                if (string.IsNullOrWhiteSpace(world)) return;

                // Looks valid: ensure a stored-character entry exists for this canonical key.
                try { this.AddStoredCharacterIfMissing(); } catch { }
            }
            catch { }
        }

        // Add a stored-character entry for the cached player row if no canonical key exists yet.
        // This will not overwrite existing entries — it only creates a new entry when missing.
        private void AddStoredCharacterIfMissing()
        {
            try
            {
                if (this.cachedCounts == null || this.cachedCounts.Count == 0) return;
                var playerRow = this.cachedCounts[0];
                var playerName = playerRow.Character ?? string.Empty;
                if (string.IsNullOrWhiteSpace(playerName) || string.Equals(playerName, "Player", StringComparison.OrdinalIgnoreCase)) return;

                string? world = null;
                try { world = this.GetLocalPlayerHomeWorld(); } catch { world = null; }
                if (string.IsNullOrWhiteSpace(world)) return;

                var key = playerName + "@" + world;

                var cfg = this.plugin.Config ?? (this.plugin.PluginInterface.GetPluginConfig() as CrystalConfig ?? new CrystalConfig());
                if (cfg.StoredCharacters == null) cfg.StoredCharacters = new StoredCharactersContainer();
                if (cfg.StoredCharacters.ByCharacter == null) cfg.StoredCharacters.ByCharacter = new System.Collections.Generic.Dictionary<string, StoredCharacter>(System.StringComparer.OrdinalIgnoreCase);

                var changed = false;
                // Prefer existing canonical entry if present; capture the actual write key used for logging.
                var writeKey = key;
                if (!cfg.StoredCharacters.ByCharacter.TryGetValue(key, out var target))
                {
                    // If there's an existing entry with the same stored name, prefer to merge retainers into it
                    try
                    {
                        var match = cfg.StoredCharacters.ByCharacter.FirstOrDefault(kv => string.Equals(kv.Value?.Name ?? string.Empty, playerName ?? string.Empty, StringComparison.OrdinalIgnoreCase));
                        if (!string.IsNullOrWhiteSpace(match.Key))
                        {
                            target = match.Value;
                            writeKey = match.Key;
                        }
                    }
                    catch { }

                    // If still no target, create a new canonical entry
                    if (target == null)
                    {
                        var stored = new StoredCharacter();
                        stored.Name = playerName ?? string.Empty;
                        stored.LastUpdatedUtc = System.DateTime.UtcNow;
                        stored.PlayerCounts = new System.Collections.Generic.Dictionary<string, long>(playerRow.ElementCounts ?? new System.Collections.Generic.Dictionary<string, long>(), System.StringComparer.OrdinalIgnoreCase);
                        stored.RetainerCounts = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, long>>(System.StringComparer.OrdinalIgnoreCase);
                        cfg.StoredCharacters.ByCharacter[key] = stored;
                        target = stored;
                        writeKey = key;
                        changed = true;
                        try { this.plugin.Log?.Info($"Stored character added: {writeKey} (Name={stored.Name})"); } catch { }
                        try { this.plugin.Chat?.Print($"Crystal Terror: added stored character {stored.Name} ({writeKey})"); } catch { }
                    }
                }

                // Ensure target has a RetainerCounts dictionary
                if (target.RetainerCounts == null)
                    target.RetainerCounts = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, long>>(System.StringComparer.OrdinalIgnoreCase);

                // Merge retainer rows from cachedCounts (skip index 0 which is player)
                try
                {
                    for (var i = 1; i < this.cachedCounts.Count; ++i)
                    {
                        var r = this.cachedCounts[i];
                        var rn = r.Character ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(rn)) continue;
                        // Prepare serializable dictionary for counts
                        var cd = new System.Collections.Generic.Dictionary<string, long>(r.ElementCounts ?? new System.Collections.Generic.Dictionary<string, long>(), System.StringComparer.OrdinalIgnoreCase);

                        // If we don't already have this retainer stored, add it.
                        if (!target.RetainerCounts.ContainsKey(rn))
                        {
                            target.RetainerCounts[rn] = cd;
                            changed = true;
                            try { this.plugin.Log?.Info($"Added retainer counts for {rn} to {writeKey}"); } catch { }
                            try { this.plugin.Chat?.Print($"Crystal Terror: added retainer {rn} for {playerName} ({writeKey})"); } catch { }
                        }
                        else
                        {
                            // Existing retainer: compare counts and update any differences (log changes)
                            try
                            {
                                var prev = target.RetainerCounts[rn];
                                foreach (var ckv in cd)
                                {
                                    try
                                    {
                                        var ckey = ckv.Key ?? string.Empty;
                                        var newV = ckv.Value;
                                        prev.TryGetValue(ckey, out var prevV);
                                        if (prevV != newV)
                                        {
                                            try { this.plugin.Log?.Info($"Retainer counts changed for {writeKey} -> {rn}: {ckey} {prevV} -> {newV}"); } catch { }
                                            try { this.plugin.Chat?.Print($"Crystal Terror: {rn} counts updated for {playerName}: {ckey} {prevV} -> {newV}"); } catch { }
                                            prev[ckey] = newV;
                                            changed = true;
                                        }
                                    }
                                    catch { }
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }

                if (changed)
                {
                    try { this.plugin.PluginInterface.SavePluginConfig(cfg); } catch { }
                }
            }
            catch { }
        }

        private void SaveConfigNow()
        {
            lock (this.saveLock)
            {
                try { this.saveCts?.Cancel(); } catch { }
                try
                {
                    // ensure cached counts are present
                    if (this.cachedCounts == null || this.cachedCounts.Count == 0)
                    {
                        try { var res = this.ScanInventories(); this.cachedCounts = res ?? new System.Collections.Generic.List<CristalRow>(); } catch { }
                    }

                    // copy cachedCounts[0] into config stored characters under current key
                    CrystalConfig cfg = null!;
                    try { cfg = this.UpdateConfigFromCached(); } catch { }

                    try { if (cfg != null) this.plugin.PluginInterface.SavePluginConfig(cfg); } catch { }
                }
                catch { }
            }
        }

        private CrystalConfig UpdateConfigFromCached()
        {
            // Build and return a modified config instance (do not attempt to set Plugin.Config directly)
            var cfg = this.plugin.Config ?? (this.plugin.PluginInterface.GetPluginConfig() as CrystalConfig ?? new CrystalConfig());
            try
            {
                if (this.cachedCounts == null || this.cachedCounts.Count == 0) return cfg;
                var playerRow = this.cachedCounts[0];
                var playerName = playerRow.Character;
                // Require a valid home world when persisting cached data. If missing, skip saving.
                string? worldForKey = null;
                try { worldForKey = this.GetLocalPlayerHomeWorld(); } catch { worldForKey = null; }
                if (string.IsNullOrWhiteSpace(worldForKey) || string.IsNullOrWhiteSpace(playerName) || string.Equals(playerName, "Player", System.StringComparison.OrdinalIgnoreCase))
                {
                    return cfg;
                }

                var key = playerName + "@" + worldForKey;

                if (cfg.StoredCharacters == null) cfg.StoredCharacters = new StoredCharactersContainer();
                if (cfg.StoredCharacters.ByCharacter == null) cfg.StoredCharacters.ByCharacter = new System.Collections.Generic.Dictionary<string, StoredCharacter>(System.StringComparer.OrdinalIgnoreCase);
                // If the detected player name is the generic "Player" or empty, do not persist it into stored characters
                if (string.IsNullOrWhiteSpace(playerName) || string.Equals(playerName, "Player", System.StringComparison.OrdinalIgnoreCase))
                {
                    return cfg;
                }

                var stored = new StoredCharacter();
                stored.Name = playerName ?? string.Empty;
                stored.LastUpdatedUtc = System.DateTime.UtcNow;
                stored.PlayerCounts = new System.Collections.Generic.Dictionary<string, long>(playerRow.ElementCounts, System.StringComparer.OrdinalIgnoreCase);

                // preserve existing retainer data if present
                if (cfg.StoredCharacters.ByCharacter.TryGetValue(key, out var existing))
                {
                    stored.RetainerCounts = existing.RetainerCounts ?? new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, long>>(System.StringComparer.OrdinalIgnoreCase);
                }
                else
                {
                    stored.RetainerCounts = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, long>>(System.StringComparer.OrdinalIgnoreCase);
                }

                // Merge any background IPC per-retainer aggregates into the stored retainer counts
                try
                {
                    System.Collections.Generic.Dictionary<ulong, (System.DateTime lastUpdatedUtc, System.Collections.Generic.Dictionary<string, long> counts)> perAggCopy = null!;
                    lock (this.ipcLock)
                    {
                        if (this.perRetainerAggregates != null && this.perRetainerAggregates.Count > 0)
                            perAggCopy = new System.Collections.Generic.Dictionary<ulong, (System.DateTime, System.Collections.Generic.Dictionary<string, long>)>(this.perRetainerAggregates);
                    }

                    if (perAggCopy != null && perAggCopy.Count > 0)
                    {
                        foreach (var kv in perAggCopy)
                        {
                            try
                            {
                                var rid = kv.Key;
                                var counts = kv.Value.counts;
                                string rName;
                                if (cfg.RetainerNames != null && cfg.RetainerNames.TryGetValue(rid, out var rn) && !string.IsNullOrWhiteSpace(rn))
                                {
                                    rName = rn;
                                }
                                else
                                {
                                    rName = "Retainer " + rid.ToString();
                                }

                                // clone counts into serializable dictionary
                                var cd = new System.Collections.Generic.Dictionary<string, long>(counts, System.StringComparer.OrdinalIgnoreCase);
                                stored.RetainerCounts[rName] = cd;
                            }
                            catch { }
                        }
                    }
                }
                catch { }

                // Prefer an existing key with the same stored name to avoid duplicates from transient key differences
                string writeKey2 = key;
                try
                {
                    if (cfg.StoredCharacters.ByCharacter != null)
                    {
                        var match2 = cfg.StoredCharacters.ByCharacter.FirstOrDefault(kv => string.Equals(kv.Value?.Name ?? string.Empty, stored.Name ?? string.Empty, StringComparison.OrdinalIgnoreCase));
                        if (!string.IsNullOrWhiteSpace(match2.Key))
                            writeKey2 = match2.Key;
                        else
                            writeKey2 = this.EnsureNonConflictingStoredKey(cfg, key, stored.Name);
                    }
                }
                catch { writeKey2 = key; }
                cfg.StoredCharacters.ByCharacter[writeKey2] = stored;
            }
            catch { }

            return cfg;
        }

        // Ensure we don't overwrite an existing stored-character key that belongs to a different character.
        // Returns either the original key or a modified unique key.
        private string EnsureNonConflictingStoredKey(CrystalConfig cfg, string baseKey, string storedName)
        {
            try
            {
                if (cfg == null || string.IsNullOrWhiteSpace(baseKey)) return baseKey ?? string.Empty;
                if (cfg.StoredCharacters == null || cfg.StoredCharacters.ByCharacter == null) return baseKey;

                if (!cfg.StoredCharacters.ByCharacter.TryGetValue(baseKey, out var existing)) return baseKey;
                // If existing entry matches the same stored name, it's safe to reuse
                if (string.Equals(existing?.Name ?? string.Empty, storedName ?? string.Empty, StringComparison.OrdinalIgnoreCase)) return baseKey;

                // Otherwise generate a non-conflicting key by appending a numeric suffix
                var idx = 1;
                string candidate;
                do
                {
                    candidate = baseKey + "!" + idx.ToString();
                    idx++;
                } while (cfg.StoredCharacters.ByCharacter.ContainsKey(candidate));

                return candidate;
            }
            catch { return baseKey ?? string.Empty; }
        }


            private unsafe Dictionary<int, string>? TryGetRetainerNames()
            {
                try
                {
                    var dict = new Dictionary<int, string>();
                    // Use RetainerManager from FFXIVClientStructs (Artisan approach). Read up to 10 sorted retainers.
                    var added = false;
                    for (uint i = 0; i < 10; ++i)
                    {
                        var ret = FFXIVClientStructs.FFXIV.Client.Game.RetainerManager.Instance()->GetRetainerBySortedIndex(i);
                        if (ret == null) continue;
                        if (ret->RetainerId == 0) continue;

                        try
                        {
                            // many client structs expose a NameString or similar; attempt to read it
                            string name = ret->NameString;
                            if (!string.IsNullOrWhiteSpace(name))
                            {
                                dict[(int)i + 1] = name;
                                // persist mapping by retainer id for stable naming
                                if (!this.plugin.Config.RetainerNames.ContainsKey(ret->RetainerId) || this.plugin.Config.RetainerNames[ret->RetainerId] != name)
                                {
                                    this.plugin.Config.RetainerNames[ret->RetainerId] = name;
                                    added = true;
                                }
                            }
                        }
                        catch
                        {
                            // ignore individual failures
                        }
                    }

                    if (added)
                        this.plugin.PluginInterface.SavePluginConfig(this.plugin.Config);

                    // If we didn't find by sorted index, try to map from persisted retainer ids in config
                    if (dict.Count == 0 && this.plugin.Config.RetainerNames.Count > 0)
                    {
                        var i = 1;
                        foreach (var kv in this.plugin.Config.RetainerNames)
                        {
                            if (i > 7) break;
                            dict[i++] = kv.Value;
                        }
                    }

                    return dict.Count > 0 ? dict : null;
                }
                catch
                {
                    return null;
                }
            }

        private record CristalRow(string Character, System.Collections.Generic.Dictionary<string, long> ElementCounts);
    }
}