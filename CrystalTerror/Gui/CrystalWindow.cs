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
                    return name + "@" + world;

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
        
        // Debounced config save fields (kept as placeholders)
        private readonly object saveLock = new();
        
        

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
            try { this.MigrateStoredCharacters(); } catch { }
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
                if (this.plugin?.ClientState != null)
                {
                    try { this.plugin.ClientState.TerritoryChanged -= this.OnTerritoryChanged; } catch { }
                    try { this.plugin.ClientState.MapIdChanged -= this.OnMapIdChanged; } catch { }
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

            // Debug: always show the detected current character above the table
            try
            {
                var dbg = string.IsNullOrWhiteSpace(currentKey) ? "(no current character)" : currentKey;
                ImGui.TextDisabled("Current character: " + dbg);
            }
            catch { }

            // Build columns: Character | (for each Type) (for each enabled Element) -> e.g. Shard Fire, Shard Ice, ... Crystal Fire ... Cluster Water
            var enabledElements = Elements.Where(e => { bool b = false; return (this.plugin?.Config?.ElementsEnabled?.TryGetValue(e, out b) ?? false) && b; }).ToArray();
            var enabledTypes = Types.Where(t => { bool b = false; return (this.plugin?.Config?.TypesEnabled?.TryGetValue(t, out b) ?? false) && b; }).ToArray();
            var colCount = 1 + enabledTypes.Length * enabledElements.Length; // Character + combinations

            if (this.selectedTab == 0)
            {
                // Render each character as its own collapsible header + table. No outer table wrapper so headers can span full width.
                {

                // Build groups: each group represents one character (player) and its retainers
                var groups = new System.Collections.Generic.List<(string UniqueKey, string DisplayName, System.Collections.Generic.Dictionary<string,long> PlayerCounts, System.Collections.Generic.List<CristalRow> Retainers, System.DateTime LastUpdatedUtc, long TotalCount)>();

                if (stored != null && stored.Count > 0)
                {
                    foreach (var kv in stored.OrderBy(k => k.Key, System.StringComparer.OrdinalIgnoreCase))
                    {
                        var key = kv.Key;
                        var sc = kv.Value;

                        if (!string.IsNullOrEmpty(currentKey) && string.Equals(key, currentKey, StringComparison.OrdinalIgnoreCase)
                            && this.cachedCounts != null && this.cachedCounts.Count > 0)
                        {
                                // Build group from live cachedCounts. Prefer the freshly-detected player name
                                var playerRow = this.cachedCounts[0];
                                var retainerRows = this.cachedCounts.Skip(1).ToList();
                                var playerCountsLive = new System.Collections.Generic.Dictionary<string,long>(playerRow.ElementCounts, System.StringComparer.OrdinalIgnoreCase);
                                long totalLive = 0;
                                try { totalLive = playerCountsLive.Values.Sum(); } catch { totalLive = 0; }
                                try { foreach (var rr in retainerRows) totalLive += rr.ElementCounts.Values.Sum(); } catch { }
                                var detectedName = this.GetLocalPlayerName() ?? playerRow.Character ?? string.Empty;
                                groups.Add((key, NormalizeStoredDisplay(detectedName, key), playerCountsLive, retainerRows, System.DateTime.UtcNow, totalLive));
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
                        long totalStored = 0;
                        try { totalStored = playerCountsDict.Values.Sum(); } catch { totalStored = 0; }
                        try { foreach (var rr in retList) totalStored += rr.ElementCounts.Values.Sum(); } catch { }
                        var lastUpdated = sc?.LastUpdatedUtc ?? System.DateTime.MinValue;
                        groups.Add((key, display, playerCountsDict, retList, lastUpdated, totalStored));
                    }
                }

                // If there was no stored entry for current player but we have a live scan, ensure it's shown
                if (!string.IsNullOrEmpty(currentKey) && (stored == null || !stored.ContainsKey(currentKey)) && this.cachedCounts != null && this.cachedCounts.Count > 0)
                {
                    var playerRow = this.cachedCounts[0];
                    var retainerRows = this.cachedCounts.Skip(1).ToList();
                    var playerCountsLive2 = new System.Collections.Generic.Dictionary<string,long>(playerRow.ElementCounts, System.StringComparer.OrdinalIgnoreCase);
                    long totalLive2 = 0;
                    try { totalLive2 = playerCountsLive2.Values.Sum(); } catch { totalLive2 = 0; }
                    try { foreach (var rr in retainerRows) totalLive2 += rr.ElementCounts.Values.Sum(); } catch { }
                    var detectedName2 = this.GetLocalPlayerName() ?? playerRow.Character ?? string.Empty;
                    groups.Add((currentKey, NormalizeStoredDisplay(detectedName2, currentKey), playerCountsLive2, retainerRows, System.DateTime.UtcNow, totalLive2));
                }

                // Apply ordering selected in settings. We support a separate world ordering plus a character ordering
                try
                {
                    var cfg = this.plugin?.Config;

                    // Migration: if old configs used CharacterOrder to store world ordering (legacy values), move them to WorldOrder
                    try
                    {
                        if (cfg != null && (cfg.WorldOrder == WorldSortMode.None) && (cfg.CharacterOrder == CharacterSortMode.WorldAsc || cfg.CharacterOrder == CharacterSortMode.WorldDesc))
                        {
                            cfg.WorldOrder = cfg.CharacterOrder == CharacterSortMode.WorldAsc ? WorldSortMode.WorldAsc : WorldSortMode.WorldDesc;
                            cfg.CharacterOrder = CharacterSortMode.AlphabeticalAsc;
                            try { this.plugin.PluginInterface.SavePluginConfig(cfg); } catch { }
                        }
                    }
                    catch { }

                    var worldOrder = cfg?.WorldOrder ?? WorldSortMode.None;
                    var charOrder = cfg?.CharacterOrder ?? CharacterSortMode.AlphabeticalAsc;

                    // Helper: get world string from UniqueKey (after '@')
                    static string KeyWorld((string UniqueKey, string DisplayName, System.Collections.Generic.Dictionary<string,long> PlayerCounts, System.Collections.Generic.List<CristalRow> Retainers, System.DateTime LastUpdatedUtc, long TotalCount) g)
                    {
                        var k = g.UniqueKey ?? string.Empty;
                        var at = k.IndexOf('@');
                        return (at >= 0 && at < k.Length - 1) ? k.Substring(at + 1) : string.Empty;
                    }

                    // Secondary character sort function based on character order setting
                    System.Func<(string UniqueKey, string DisplayName, System.Collections.Generic.Dictionary<string,long> PlayerCounts, System.Collections.Generic.List<CristalRow> Retainers, System.DateTime LastUpdatedUtc, long TotalCount), object> secondaryKey = g => string.IsNullOrWhiteSpace(g.DisplayName) ? g.UniqueKey : g.DisplayName;
                    var secondaryDesc = false;
                    switch (charOrder)
                    {
                        case CharacterSortMode.AlphabeticalAsc:
                            secondaryKey = g => string.IsNullOrWhiteSpace(g.DisplayName) ? g.UniqueKey : g.DisplayName;
                            secondaryDesc = false;
                            break;
                        case CharacterSortMode.AlphabeticalDesc:
                            secondaryKey = g => string.IsNullOrWhiteSpace(g.DisplayName) ? g.UniqueKey : g.DisplayName;
                            secondaryDesc = true;
                            break;
                        case CharacterSortMode.LastUpdatedDesc:
                            secondaryKey = g => g.LastUpdatedUtc;
                            secondaryDesc = true;
                            break;
                        case CharacterSortMode.LastUpdatedAsc:
                            secondaryKey = g => g.LastUpdatedUtc;
                            secondaryDesc = false;
                            break;
                        case CharacterSortMode.TotalCrystalsDesc:
                            secondaryKey = g => g.TotalCount;
                            secondaryDesc = true;
                            break;
                        case CharacterSortMode.TotalCrystalsAsc:
                            secondaryKey = g => g.TotalCount;
                            secondaryDesc = false;
                            break;
                        case CharacterSortMode.AutoRetainer:
                            // Handled below by trying to query AutoRetainer ordering via IPC; set placeholder to DisplayName
                            secondaryKey = g => string.IsNullOrWhiteSpace(g.DisplayName) ? g.UniqueKey : g.DisplayName;
                            secondaryDesc = false;
                            break;
                        default:
                            secondaryKey = g => string.IsNullOrWhiteSpace(g.DisplayName) ? g.UniqueKey : g.DisplayName;
                            secondaryDesc = false;
                            break;
                    }

                    if (worldOrder == WorldSortMode.WorldAsc)
                    {
                            if (charOrder == CharacterSortMode.Custom)
                            {
                                // Build char index map
                                var charMap = new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
                                try
                                {
                                    if (cfg?.CustomCharacterOrder != null)
                                    {
                                        for (var i = 0; i < cfg.CustomCharacterOrder.Count; ++i)
                                        {
                                            var k = cfg.CustomCharacterOrder[i];
                                            if (string.IsNullOrWhiteSpace(k)) continue;
                                            if (!charMap.ContainsKey(k)) charMap[k] = i;
                                        }
                                    }
                                }
                                catch { }

                                groups = groups.OrderBy(g => KeyWorld(g), System.StringComparer.OrdinalIgnoreCase)
                                    .ThenBy(g => {
                                        var idx = int.MaxValue;
                                        try
                                        {
                                            var key = g.UniqueKey ?? string.Empty;
                                            if (charMap.TryGetValue(key, out var xi)) idx = xi;
                                            else if (!string.IsNullOrWhiteSpace(g.DisplayName) && charMap.TryGetValue(g.DisplayName, out xi)) idx = xi;
                                        }
                                        catch { }
                                        return idx;
                                    })
                                    .ThenBy(g => secondaryKey(g))
                                    .ToList();
                            }
                            else
                            {
                                if (secondaryDesc)
                                    groups = groups.OrderBy(g => KeyWorld(g), System.StringComparer.OrdinalIgnoreCase).ThenByDescending(g => secondaryKey(g)).ToList();
                                else
                                    groups = groups.OrderBy(g => KeyWorld(g), System.StringComparer.OrdinalIgnoreCase).ThenBy(g => secondaryKey(g)).ToList();
                            }
                    }
                    else if (worldOrder == WorldSortMode.WorldDesc)
                    {
                            if (charOrder == CharacterSortMode.Custom)
                            {
                                var charMap = new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
                                try
                                {
                                    if (cfg?.CustomCharacterOrder != null)
                                    {
                                        for (var i = 0; i < cfg.CustomCharacterOrder.Count; ++i)
                                        {
                                            var k = cfg.CustomCharacterOrder[i];
                                            if (string.IsNullOrWhiteSpace(k)) continue;
                                            if (!charMap.ContainsKey(k)) charMap[k] = i;
                                        }
                                    }
                                }
                                catch { }

                                groups = groups.OrderByDescending(g => KeyWorld(g), System.StringComparer.OrdinalIgnoreCase)
                                    .ThenBy(g => {
                                        var idx = int.MaxValue;
                                        try
                                        {
                                            var key = g.UniqueKey ?? string.Empty;
                                            if (charMap.TryGetValue(key, out var xi)) idx = xi;
                                            else if (!string.IsNullOrWhiteSpace(g.DisplayName) && charMap.TryGetValue(g.DisplayName, out xi)) idx = xi;
                                        }
                                        catch { }
                                        return idx;
                                    })
                                    .ThenBy(g => secondaryKey(g))
                                    .ToList();
                            }
                            else
                            {
                                if (secondaryDesc)
                                    groups = groups.OrderByDescending(g => KeyWorld(g), System.StringComparer.OrdinalIgnoreCase).ThenByDescending(g => secondaryKey(g)).ToList();
                                else
                                    groups = groups.OrderByDescending(g => KeyWorld(g), System.StringComparer.OrdinalIgnoreCase).ThenBy(g => secondaryKey(g)).ToList();
                            }
                    }
                        // If the selected character ordering is AutoRetainer, try to fetch ordering via IPC and apply it
                        else if (charOrder == CharacterSortMode.AutoRetainer)
                        {
                            try
                            {
                                var orderList = this.TryGetAutoRetainerOrder();
                                if (orderList != null && orderList.Count > 0)
                                {
                                    // Build index map
                                    var idxMap = new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
                                    for (var i = 0; i < orderList.Count; ++i)
                                    {
                                        var k = orderList[i]; if (k == null) continue;
                                        if (!idxMap.ContainsKey(k)) idxMap[k] = i;
                                    }

                                    // Order by world (if enabled) then by autoretainer index (matching UniqueKey or DisplayName), else fallback
                                    if (worldOrder == WorldSortMode.WorldAsc || worldOrder == WorldSortMode.WorldDesc || worldOrder == WorldSortMode.Custom)
                                    {
                                        System.Func<(string UniqueKey, string DisplayName, System.Collections.Generic.Dictionary<string,long> PlayerCounts, System.Collections.Generic.List<CristalRow> Retainers, System.DateTime LastUpdatedUtc, long TotalCount), int> getIdx = g =>
                                        {
                                            try
                                            {
                                                var key = g.UniqueKey ?? string.Empty;
                                                if (idxMap.TryGetValue(key, out var v)) return v;
                                                if (!string.IsNullOrWhiteSpace(g.DisplayName) && idxMap.TryGetValue(g.DisplayName, out v)) return v;
                                            }
                                            catch { }
                                            return int.MaxValue;
                                        };

                                        if (worldOrder == WorldSortMode.WorldAsc)
                                            groups = groups.OrderBy(g => KeyWorld(g), System.StringComparer.OrdinalIgnoreCase).ThenBy(g => getIdx(g)).ToList();
                                        else if (worldOrder == WorldSortMode.WorldDesc)
                                            groups = groups.OrderByDescending(g => KeyWorld(g), System.StringComparer.OrdinalIgnoreCase).ThenBy(g => getIdx(g)).ToList();
                                        else // custom world
                                        {
                                            var worldMap = new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
                                            try { if (cfg?.CustomWorldOrder != null) { for (var i = 0; i < cfg.CustomWorldOrder.Count; ++i) worldMap[cfg.CustomWorldOrder[i]] = i; } } catch { }
                                            groups = groups.OrderBy(g => { var w = KeyWorld(g); return worldMap.TryGetValue(w, out var wi) ? wi : int.MaxValue; }).ThenBy(g => getIdx(g)).ToList();
                                        }
                                    }
                                    else
                                    {
                                        groups = groups.OrderBy(g => { try { var key = g.UniqueKey ?? string.Empty; return idxMap.TryGetValue(key, out var v) ? v : int.MaxValue; } catch { return int.MaxValue; } }).ToList();
                                    }
                                    // Done
                                }
                                else
                                {
                                    // If we couldn't fetch list, fallback to alphabetical
                                    groups = groups.OrderBy(g => string.IsNullOrWhiteSpace(g.DisplayName) ? g.UniqueKey : g.DisplayName, System.StringComparer.OrdinalIgnoreCase).ToList();
                                }
                            }
                            catch { }
                        }
                        else if (worldOrder == WorldSortMode.Custom)
                        {
                            var worldMap = new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
                            try
                            {
                                if (cfg?.CustomWorldOrder != null)
                                {
                                    for (var i = 0; i < cfg.CustomWorldOrder.Count; ++i)
                                    {
                                        var w = cfg.CustomWorldOrder[i];
                                        if (string.IsNullOrWhiteSpace(w)) continue;
                                        if (!worldMap.ContainsKey(w)) worldMap[w] = i;
                                    }
                                }
                            }
                            catch { }

                            if (charOrder == CharacterSortMode.Custom)
                            {
                                var charMap = new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
                                try
                                {
                                    if (cfg?.CustomCharacterOrder != null)
                                    {
                                        for (var i = 0; i < cfg.CustomCharacterOrder.Count; ++i)
                                        {
                                            var k = cfg.CustomCharacterOrder[i];
                                            if (string.IsNullOrWhiteSpace(k)) continue;
                                            if (!charMap.ContainsKey(k)) charMap[k] = i;
                                        }
                                    }
                                }
                                catch { }

                                groups = groups.OrderBy(g => {
                                    var w = KeyWorld(g);
                                    return worldMap.TryGetValue(w, out var wi) ? wi : int.MaxValue;
                                }).ThenBy(g => {
                                    var idx = int.MaxValue;
                                    try
                                    {
                                        var key = g.UniqueKey ?? string.Empty;
                                        if (charMap.TryGetValue(key, out var ci)) idx = ci;
                                        else if (!string.IsNullOrWhiteSpace(g.DisplayName) && charMap.TryGetValue(g.DisplayName, out ci)) idx = ci;
                                    }
                                    catch { }
                                    return idx;
                                }).ThenBy(g => secondaryKey(g)).ToList();
                            }
                            else
                            {
                                if (secondaryDesc)
                                    groups = groups.OrderBy(g => { var w = KeyWorld(g); return worldMap.TryGetValue(w, out var wi) ? wi : int.MaxValue; }).ThenByDescending(g => secondaryKey(g)).ToList();
                                else
                                    groups = groups.OrderBy(g => { var w = KeyWorld(g); return worldMap.TryGetValue(w, out var wi) ? wi : int.MaxValue; }).ThenBy(g => secondaryKey(g)).ToList();
                            }
                        }
                    else
                    {
                        // No world grouping: apply character-only ordering across entire list
                            if (charOrder == CharacterSortMode.Custom)
                            {
                                var charMap = new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
                                try
                                {
                                    if (cfg?.CustomCharacterOrder != null)
                                    {
                                        for (var i = 0; i < cfg.CustomCharacterOrder.Count; ++i)
                                        {
                                            var k = cfg.CustomCharacterOrder[i];
                                            if (string.IsNullOrWhiteSpace(k)) continue;
                                            if (!charMap.ContainsKey(k)) charMap[k] = i;
                                        }
                                    }
                                }
                                catch { }

                                groups = groups.OrderBy(g => {
                                    var idx = int.MaxValue;
                                    try
                                    {
                                        var key = g.UniqueKey ?? string.Empty;
                                        if (charMap.TryGetValue(key, out var ci)) idx = ci;
                                        else if (!string.IsNullOrWhiteSpace(g.DisplayName) && charMap.TryGetValue(g.DisplayName, out ci)) idx = ci;
                                    }
                                    catch { }
                                    return idx;
                                }).ThenBy(g => secondaryKey(g)).ToList();
                            }
                            else
                            {
                                if (secondaryDesc)
                                    groups = groups.OrderByDescending(g => secondaryKey(g)).ToList();
                                else
                                    groups = groups.OrderBy(g => secondaryKey(g)).ToList();
                            }
                    }
                }
                catch { }

            

                // If selection filter is active, limit groups/retainers to the selected set
                var groupsToRender = groups;
                try
                {
                    if (this.selectedRetainers != null && this.selectedRetainers.Count > 0)
                    {
                        var filtered = new System.Collections.Generic.List<(string UniqueKey, string DisplayName, System.Collections.Generic.Dictionary<string,long> PlayerCounts, System.Collections.Generic.List<CristalRow> Retainers, System.DateTime LastUpdatedUtc, long TotalCount)>();
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

                // Try to query AutoRetainer ordering via any known IPC names. Returns list of canonical keys or display names.
                private System.Collections.Generic.List<string>? TryGetAutoRetainerOrder()
                {
                    try
                    {
                        var pi = this.plugin?.PluginInterface;
                        if (pi == null) return null;
                        var candidateNames = new[] { "AutoRetainer.RetainerOrder", "AutoRetainer.GetCharacterOrder", "AutoRetainer.Order", "AutoRetainer.List", "AutoRetainer.GetOrder" };
                        foreach (var name in candidateNames)
                        {
                            try
                            {
                                var sub = pi.GetIpcSubscriber<object, object>(name);
                                if (sub == null) continue;
                                var t = sub.GetType();
                                var mi = t.GetMethod("InvokeFunc");
                                object? res = null;
                                if (mi != null)
                                {
                                    try { res = mi.Invoke(sub, Array.Empty<object>()); } catch { }
                                }
                                else
                                {
                                    var mi2 = t.GetMethod("Invoke");
                                    if (mi2 != null)
                                    {
                                        try { res = mi2.Invoke(sub, Array.Empty<object>()); } catch { }
                                    }
                                }

                                if (res == null) continue;
                                // Convert result to list of strings
                                if (res is System.Collections.Generic.IEnumerable<string> se)
                                {
                                    return se.ToList();
                                }
                                if (res is string[] sa)
                                {
                                    return new System.Collections.Generic.List<string>(sa);
                                }
                                if (res is System.Collections.IEnumerable oe)
                                {
                                    var outl = new System.Collections.Generic.List<string>();
                                    foreach (var o in oe)
                                    {
                                        try { if (o != null) outl.Add(o.ToString() ?? string.Empty); } catch { }
                                    }
                                    if (outl.Count > 0) return outl;
                                }
                                if (res is string s)
                                {
                                    // comma or newline separated
                                    var parts = s.Split(new[] { ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                                    if (parts.Count > 0) return parts;
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                    return null;
                }

        private unsafe List<CristalRow> ScanInventories()
        {
            
            var results = new List<CristalRow>();
            var playerName = this.GetLocalPlayerName();
            results.Add(new CristalRow(string.IsNullOrWhiteSpace(playerName) ? "(you)" : playerName!, new Dictionary<string, long>(System.StringComparer.OrdinalIgnoreCase)));
            return results;
        }

        private void OnFrameworkUpdate(Dalamud.Plugin.Services.IFramework framework)
        {
            // no-op
        }

        private void OnGameInventoryChangelog(System.Collections.Generic.IReadOnlyCollection<Dalamud.Game.Inventory.InventoryEventArgTypes.InventoryEventArgs> events)
        {
            // no-op
        }

        private void OnItemAddedExplicit(Dalamud.Game.Inventory.InventoryEventArgTypes.InventoryItemAddedArgs args)
        {
            // no-op
        }

        private void OnItemRemovedExplicit(Dalamud.Game.Inventory.InventoryEventArgTypes.InventoryItemRemovedArgs args)
        {
            // no-op
        }

        private void OnItemChangedExplicit(Dalamud.Game.Inventory.InventoryEventArgTypes.InventoryItemChangedArgs args)
        {
            // no-op
        }

        private void HandlePlayerInventoryDelta(Dalamud.Game.Inventory.GameInventoryType inventoryType, uint itemId, int delta)
        {
            // no-op
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
            try { this.EnsureSaveIfCachedValid(); } catch { }
        }

        // If we have a cached player row that appears valid (readable name and detected home world),
        // request a debounced save so the live player row is persisted to stored characters.
        private void EnsureSaveIfCachedValid()
        {
            try
            {
                if (this.plugin == null) return;
                var cfg = this.plugin.Config;
                if (cfg == null) return;

                if (this.cachedCounts == null || this.cachedCounts.Count == 0) return;

                // Determine canonical current key using detected player name + home world when available
                var playerNameLocal = this.GetLocalPlayerName() ?? string.Empty;
                var currentKey = playerNameLocal;
                try
                {
                    var world = this.GetLocalPlayerHomeWorld();
                    if (!string.IsNullOrEmpty(world)) currentKey = currentKey + "@" + world;
                }
                catch { }

                if (string.IsNullOrWhiteSpace(currentKey)) return;

                // Build StoredCharacter from cachedCounts: index 0 is player, rest are retainers
                var sc = new StoredCharacter();
                sc.LastUpdatedUtc = System.DateTime.UtcNow;
                // Use the detected display name (without world) if available
                sc.Name = string.IsNullOrWhiteSpace(playerNameLocal) ? currentKey : playerNameLocal;

                // Player counts
                try
                {
                    var playerRow = this.cachedCounts[0];
                    sc.PlayerCounts = new System.Collections.Generic.Dictionary<string, long>(playerRow.ElementCounts, System.StringComparer.OrdinalIgnoreCase);
                }
                catch { sc.PlayerCounts = new System.Collections.Generic.Dictionary<string, long>(System.StringComparer.OrdinalIgnoreCase); }

                // Retainer counts
                var retMap = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, long>>(System.StringComparer.OrdinalIgnoreCase);
                try
                {
                    foreach (var r in this.cachedCounts.Skip(1))
                    {
                        var name = r.Character ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(name)) name = "(retainer)";
                        retMap[name] = new System.Collections.Generic.Dictionary<string, long>(r.ElementCounts, System.StringComparer.OrdinalIgnoreCase);
                    }
                }
                catch { }
                sc.RetainerCounts = retMap;

                // Merge/update only the current character entry into StoredCharacters (preserve other characters)
                try
                {
                    cfg.StoredCharacters = cfg.StoredCharacters ?? new StoredCharactersContainer();
                    cfg.StoredCharacters.ByCharacter = cfg.StoredCharacters.ByCharacter ?? new System.Collections.Generic.Dictionary<string, StoredCharacter>(System.StringComparer.OrdinalIgnoreCase);

                    // Ensure we don't accidentally overwrite a different character's entry with same key
                    var baseKey = currentKey ?? string.Empty;
                    var keyToUse = this.EnsureNonConflictingStoredKey(cfg, baseKey, sc.Name);

                    cfg.StoredCharacters.ByCharacter[keyToUse] = sc;
                    try { this.plugin.PluginInterface.SavePluginConfig(cfg); } catch { }
                }
                catch { }
            }
            catch { }
        }

        // Add a stored-character entry for the cached player row if no canonical key exists yet.
        // This will not overwrite existing entries — it only creates a new entry when missing.
        private void AddStoredCharacterIfMissing()
        {
            try
            {
                if (this.plugin == null) return;
                var cfg = this.plugin.Config;
                if (cfg == null) return;
                if (this.cachedCounts == null || this.cachedCounts.Count == 0) return;

                var playerNameLocal = this.GetLocalPlayerName() ?? string.Empty;
                var currentKey = playerNameLocal;
                try
                {
                    var world = this.GetLocalPlayerHomeWorld();
                    if (!string.IsNullOrEmpty(world)) currentKey = currentKey + "@" + world;
                }
                catch { }

                if (string.IsNullOrWhiteSpace(currentKey)) return;

                cfg.StoredCharacters = cfg.StoredCharacters ?? new StoredCharactersContainer();
                cfg.StoredCharacters.ByCharacter = cfg.StoredCharacters.ByCharacter ?? new System.Collections.Generic.Dictionary<string, StoredCharacter>(System.StringComparer.OrdinalIgnoreCase);

                // If an entry already exists for this canonical key, do nothing
                if (cfg.StoredCharacters.ByCharacter.ContainsKey(currentKey)) return;

                // Build stored character like EnsureSaveIfCachedValid but only add when missing
                var sc = new StoredCharacter();
                sc.LastUpdatedUtc = System.DateTime.UtcNow;
                sc.Name = string.IsNullOrWhiteSpace(playerNameLocal) ? currentKey : playerNameLocal;
                try { sc.PlayerCounts = new System.Collections.Generic.Dictionary<string, long>(this.cachedCounts[0].ElementCounts, System.StringComparer.OrdinalIgnoreCase); } catch { sc.PlayerCounts = new System.Collections.Generic.Dictionary<string, long>(System.StringComparer.OrdinalIgnoreCase); }
                var retMap = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, long>>(System.StringComparer.OrdinalIgnoreCase);
                try { foreach (var r in this.cachedCounts.Skip(1)) { var name = r.Character ?? string.Empty; if (string.IsNullOrWhiteSpace(name)) name = "(retainer)"; retMap[name] = new System.Collections.Generic.Dictionary<string, long>(r.ElementCounts, System.StringComparer.OrdinalIgnoreCase); } } catch { }
                sc.RetainerCounts = retMap;

                var baseKey = currentKey ?? string.Empty;
                var keyToUse = this.EnsureNonConflictingStoredKey(cfg, baseKey, sc.Name);
                cfg.StoredCharacters.ByCharacter[keyToUse] = sc;
                try { this.plugin.PluginInterface.SavePluginConfig(cfg); } catch { }
            }
            catch { }
        }

        private void SaveConfigNow()
        {
            try { this.plugin?.PluginInterface?.SavePluginConfig(this.plugin.Config); } catch { }
        }

        private CrystalConfig UpdateConfigFromCached()
        {
            try
            {
                this.EnsureSaveIfCachedValid();
            }
            catch { }
            return this.plugin.Config ?? (this.plugin.PluginInterface.GetPluginConfig() as CrystalConfig ?? new CrystalConfig());
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