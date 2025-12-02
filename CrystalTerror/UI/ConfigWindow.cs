using System;
using System.Linq;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;

namespace CrystalTerror
{
    /// <summary>
    /// Configuration window used to display and edit plugin settings.
    /// Provides UI controls for display, filters and data management actions.
    /// </summary>
    public unsafe class ConfigWindow : Window, IDisposable
    {
    private readonly CrystalTerror plugin;

        private static readonly string[] Elements = new[] { "Fire", "Ice", "Wind", "Earth", "Lightning", "Water" };
        private static readonly string[] Types = new[] { "Shard", "Crystal", "Cluster" };
        private static readonly InventoryType[] PlayerInventoriesToScan = new[]
        {
            InventoryType.Inventory1,
            InventoryType.Inventory2,
            InventoryType.Inventory3,
            InventoryType.Inventory4,
        };

        /// <summary>Creates the configuration window bound to the given plugin instance.</summary>
        public ConfigWindow(CrystalTerror plugin) : base("Crystal Terror - Settings")
        {
            this.plugin = plugin;
            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new System.Numerics.Vector2(400, 150),
                MaximumSize = new System.Numerics.Vector2(900, 1200),
            };
        }

        /// <summary>Dispose the configuration window and release any resources.</summary>
        public void Dispose()
        {
        }

        /// <summary>Render the configuration UI; called by the Dalamud UI loop.</summary>
        public override void Draw()
        {
            try
            {
                var cfg_local = this.plugin.Config ?? (this.plugin.PluginInterface?.GetPluginConfig() as CrystalConfig ?? new CrystalConfig());

                // Replace menu with collapsible sections for a cleaner settings layout
                try
                {
                    if (ImGui.CollapsingHeader("Display"))
                    {
                        ImGui.Separator();

                        var showPlayer = cfg_local.ShowPlayer;
                        if (ImGui.Checkbox("Show player row", ref showPlayer)) { cfg_local.ShowPlayer = showPlayer; try { this.plugin.PluginInterface.SavePluginConfig(cfg_local); } catch { } }
                        var showRetainers = cfg_local.ShowRetainers;
                        if (ImGui.Checkbox("Show retainers", ref showRetainers)) { cfg_local.ShowRetainers = showRetainers; try { this.plugin.PluginInterface.SavePluginConfig(cfg_local); } catch { } }
                        var skip = cfg_local.SkipDisabledRetainers;
                        if (ImGui.Checkbox("Skip disabled/unavailable retainers", ref skip)) { cfg_local.SkipDisabledRetainers = skip; try { this.plugin.PluginInterface.SavePluginConfig(cfg_local); } catch { } }
                        var useInvEvents = cfg_local.UseInventoryEvents;
                        if (ImGui.Checkbox("Use inventory events (immediate refresh)", ref useInvEvents)) { cfg_local.UseInventoryEvents = useInvEvents; try { this.plugin.PluginInterface.SavePluginConfig(cfg_local); } catch { } }
                        
                    }

                    if (ImGui.CollapsingHeader("Ordering", ImGuiTreeNodeFlags.None))
                    {
                        ImGui.Spacing();
                        ImGui.TextUnformatted("Ordering");
                        ImGui.Separator();
                        try
                        {
                            // World ordering control (map combo index -> enum explicitly so 'Custom' is shown)
                            var worldLabels = new[] { "None", "World (A → Z)", "World (Z → A)", "Custom" };
                            var worldValues = new[] { WorldSortMode.None, WorldSortMode.WorldAsc, WorldSortMode.WorldDesc, WorldSortMode.Custom };
                            var worldIdx = 0;
                            try { worldIdx = Array.FindIndex(worldValues, w => w == cfg_local.WorldOrder); if (worldIdx < 0) worldIdx = 0; } catch { worldIdx = 0; }
                            if (ImGui.Combo("World order", ref worldIdx, worldLabels, worldLabels.Length))
                            {
                                try { cfg_local.WorldOrder = worldValues[Math.Max(0, Math.Min(worldIdx, worldValues.Length - 1))]; } catch { cfg_local.WorldOrder = WorldSortMode.None; }
                                try { this.plugin.PluginInterface.SavePluginConfig(cfg_local); } catch { }
                            }

                            // Prefill custom worlds when user selects Custom and no list exists
                            try
                            {
                                if (cfg_local.WorldOrder == WorldSortMode.Custom && (cfg_local.CustomWorldOrder == null || cfg_local.CustomWorldOrder.Count == 0))
                                {
                                    var stored = cfg_local.StoredCharacters?.ByCharacter;
                                    var worlds = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                                    if (stored != null)
                                    {
                                        foreach (var kv in stored)
                                        {
                                            try
                                            {
                                                var key = kv.Key ?? string.Empty;
                                                var at = key.IndexOf('@');
                                                var w = (at >= 0 && at < key.Length - 1) ? key.Substring(at + 1) : string.Empty;
                                                if (!string.IsNullOrWhiteSpace(w)) worlds.Add(w);
                                            }
                                            catch { }
                                        }
                                    }
                                    cfg_local.CustomWorldOrder = worlds.OrderBy(x => x, System.StringComparer.OrdinalIgnoreCase).ToList();
                                    try { this.plugin.PluginInterface.SavePluginConfig(cfg_local); } catch { }
                                }
                            }
                            catch { }

                            // If custom world ordering selected, show manual editor
                            if (cfg_local.WorldOrder == WorldSortMode.Custom)
                            {
                                try
                                {
                                    var worldsText = string.Join("\n", cfg_local.CustomWorldOrder ?? new System.Collections.Generic.List<string>());
                                    if (ImGui.InputTextMultiline("##custom_worlds", ref worldsText, 8192, new System.Numerics.Vector2(-1, 100)))
                                    {
                                        var lines = worldsText.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries)
                                            .Select(s => s.Trim())
                                            .Where(s => !string.IsNullOrWhiteSpace(s))
                                            .ToList();
                                        cfg_local.CustomWorldOrder = lines;
                                        try { this.plugin.PluginInterface.SavePluginConfig(cfg_local); } catch { }
                                    }
                                    ImGui.TextWrapped("Enter world names, one per line, top-to-bottom order. Unknown worlds will be listed after these.");
                                }
                                catch { }
                            }

                            ImGui.Spacing();

                            // Character ordering control (map combo index -> enum explicitly so 'Custom' and AutoRetainer are shown and saved)
                            var baseCharLabels = new System.Collections.Generic.List<string>() { "Alphabetical (A → Z)", "Alphabetical (Z → A)", "Last updated (newest)", "Last updated (oldest)", "Total crystals (desc)", "Total crystals (asc)", "Custom" };
                            var baseCharValues = new System.Collections.Generic.List<CharacterSortMode>() { CharacterSortMode.AlphabeticalAsc, CharacterSortMode.AlphabeticalDesc, CharacterSortMode.LastUpdatedDesc, CharacterSortMode.LastUpdatedAsc, CharacterSortMode.TotalCrystalsDesc, CharacterSortMode.TotalCrystalsAsc, CharacterSortMode.Custom };

                            // Detect AutoRetainer IPC availability so we can present the option
                            var autoRetainerAvailable = false;
                            try
                            {
                                var candidates = new[] { "AutoRetainer.RetainerOrder", "AutoRetainer.GetCharacterOrder", "AutoRetainer.Order", "AutoRetainer.List" };
                                foreach (var c in candidates)
                                {
                                    try
                                    {
                                        var sub = this.plugin.PluginInterface.GetIpcSubscriber<object, object>(c);
                                        if (sub != null) { autoRetainerAvailable = true; break; }
                                    }
                                    catch { }
                                }
                            }
                            catch { }

                            if (autoRetainerAvailable)
                            {
                                baseCharLabels.Add("Use AutoRetainer ordering");
                                baseCharValues.Add(CharacterSortMode.AutoRetainer);
                            }

                            var charOrderLabels = baseCharLabels.ToArray();
                            var charOrderValues = baseCharValues.ToArray();
                            var curIdx = 0;
                            try { curIdx = Array.FindIndex(charOrderValues, v => v == cfg_local.CharacterOrder); if (curIdx < 0) curIdx = 0; } catch { curIdx = 0; }
                            if (ImGui.Combo("Character order", ref curIdx, charOrderLabels, charOrderLabels.Length))
                            {
                                try { cfg_local.CharacterOrder = charOrderValues[Math.Max(0, Math.Min(curIdx, charOrderValues.Length - 1))]; } catch { cfg_local.CharacterOrder = CharacterSortMode.AlphabeticalAsc; }
                                try { this.plugin.PluginInterface.SavePluginConfig(cfg_local); } catch { }

                                // Prefill custom characters when user selects Custom and no list exists
                                try
                                {
                                    if (cfg_local.CharacterOrder == CharacterSortMode.Custom && (cfg_local.CustomCharacterOrder == null || cfg_local.CustomCharacterOrder.Count == 0))
                                    {
                                        var stored = cfg_local.StoredCharacters?.ByCharacter;
                                        var list = new System.Collections.Generic.List<string>();
                                        if (stored != null)
                                        {
                                            foreach (var kv in stored)
                                            {
                                                try { list.Add(kv.Key ?? string.Empty); } catch { }
                                            }
                                        }

                                        // Helper to extract world
                                        static string KeyWorld(string k)
                                        {
                                            if (string.IsNullOrEmpty(k)) return string.Empty;
                                            var at = k.IndexOf('@');
                                            return (at >= 0 && at < k.Length - 1) ? k.Substring(at + 1) : string.Empty;
                                        }

                                        var worldOrder = cfg_local.WorldOrder;
                                        if (worldOrder == WorldSortMode.Custom && (cfg_local.CustomWorldOrder != null && cfg_local.CustomWorldOrder.Count > 0))
                                        {
                                            var worldMap = cfg_local.CustomWorldOrder.Select((w, i) => new { w, i }).ToDictionary(x => x.w, x => x.i, System.StringComparer.OrdinalIgnoreCase);
                                            list = list.OrderBy(k => worldMap.TryGetValue(KeyWorld(k), out var wi) ? wi : int.MaxValue).ThenBy(k => k, System.StringComparer.OrdinalIgnoreCase).ToList();
                                        }
                                        else if (worldOrder == WorldSortMode.WorldAsc)
                                        {
                                            list = list.OrderBy(k => KeyWorld(k), System.StringComparer.OrdinalIgnoreCase).ThenBy(k => k, System.StringComparer.OrdinalIgnoreCase).ToList();
                                        }
                                        else if (worldOrder == WorldSortMode.WorldDesc)
                                        {
                                            list = list.OrderByDescending(k => KeyWorld(k), System.StringComparer.OrdinalIgnoreCase).ThenBy(k => k, System.StringComparer.OrdinalIgnoreCase).ToList();
                                        }
                                        else
                                        {
                                            list = list.OrderBy(k => k, System.StringComparer.OrdinalIgnoreCase).ToList();
                                        }

                                        cfg_local.CustomCharacterOrder = list.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                                        try { this.plugin.PluginInterface.SavePluginConfig(cfg_local); } catch { }
                                    }
                                }
                                catch { }
                            }

                            // If custom character ordering selected, show manual editor
                            if (cfg_local.CharacterOrder == CharacterSortMode.Custom)
                            {
                                try
                                {
                                    var charsText = string.Join("\n", cfg_local.CustomCharacterOrder ?? new System.Collections.Generic.List<string>());
                                    if (ImGui.InputTextMultiline("##custom_chars", ref charsText, 16384, new System.Numerics.Vector2(-1, 200)))
                                    {
                                        var lines = charsText.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries)
                                            .Select(s => s.Trim())
                                            .Where(s => !string.IsNullOrWhiteSpace(s))
                                            .ToList();
                                        cfg_local.CustomCharacterOrder = lines;
                                        try { this.plugin.PluginInterface.SavePluginConfig(cfg_local); } catch { }
                                    }
                                    ImGui.TextWrapped("Enter characters in the desired order. Use canonical form 'Name@World' where possible; plain names will also match.");
                                }
                                catch { }
                            }
                        }
                        catch { }
                    }

                    if (ImGui.CollapsingHeader("Filters", ImGuiTreeNodeFlags.None))
                    {
                        ImGui.Spacing();
                        ImGui.TextUnformatted("Filters");
                        ImGui.Separator();

                        ImGui.Text("Types:"); ImGui.SameLine();
                        foreach (var t in Types)
                        {
                            var cur = false; cfg_local.TypesEnabled.TryGetValue(t, out cur);
                            if (ImGui.Checkbox(t + "##type_" + t, ref cur)) { cfg_local.TypesEnabled[t] = cur; try { this.plugin.PluginInterface.SavePluginConfig(cfg_local); } catch { } }
                            ImGui.SameLine();
                        }
                        ImGui.NewLine();

                        ImGui.Text("Elements:"); ImGui.SameLine();
                        foreach (var e in Elements)
                        {
                            var cur = false; cfg_local.ElementsEnabled.TryGetValue(e, out cur);
                            if (ImGui.Checkbox(e + "##el_" + e, ref cur)) { cfg_local.ElementsEnabled[e] = cur; try { this.plugin.PluginInterface.SavePluginConfig(cfg_local); } catch { } }
                            ImGui.SameLine();
                        }
                    }

                    if (ImGui.CollapsingHeader("Data Management", ImGuiTreeNodeFlags.None))
                    {
                        ImGui.Spacing();
                        ImGui.TextUnformatted("Data Management");
                        ImGui.Separator();
                        ImGui.Spacing();
                        if (ImGui.Button("Purge stored data")) ImGui.OpenPopup("PurgeConfirm");
                        ImGui.SameLine();
                        if (ImGui.Button("Sanitize stored data")) ImGui.OpenPopup("SanitizeConfirm");
                        ImGui.SameLine();
                        if (ImGui.Button("Copy raw inventory to clipboard")) { try { CopyRawInventoryToClipboard(); } catch { } }
                    }
                }
                catch { }

                // Purge confirmation
                if (ImGui.BeginPopupModal("PurgeConfirm", ImGuiWindowFlags.AlwaysAutoResize))
                {
                    ImGui.Text("This will delete all stored character and retainer data. This cannot be undone.");
                    ImGui.Separator();
                    if (ImGui.Button("Cancel")) { ImGui.CloseCurrentPopup(); }
                    ImGui.SameLine();
                    if (ImGui.Button("Purge"))
                    {
                        try
                        {
                            var cfg_local2 = this.plugin.Config ?? (this.plugin.PluginInterface.GetPluginConfig() as CrystalConfig ?? new CrystalConfig());
                            cfg_local2.StoredCharacters = new StoredCharactersContainer();
                            cfg_local2.RetainerNames = new System.Collections.Generic.Dictionary<ulong, string>();
                            try { this.plugin.PluginInterface.SavePluginConfig(cfg_local2); } catch { }
                            try { this.plugin.Chat?.Print("Crystal Terror: stored data purged."); } catch { }
                        }
                        catch { }
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndPopup();
                }

                // Sanitize confirmation
                if (ImGui.BeginPopupModal("SanitizeConfirm", ImGuiWindowFlags.AlwaysAutoResize))
                {
                    ImGui.Text("This will remove stored character entries and retainer data that are malformed or do not follow the canonical 'Name@World' structure.\nIt will also remove placeholder entries like 'Player'.\nThis cannot be undone.");
                    ImGui.Separator();
                    if (ImGui.Button("Cancel")) { ImGui.CloseCurrentPopup(); }
                    ImGui.SameLine();
                    if (ImGui.Button("Sanitize"))
                    {
                        try
                        {
                            var cfg_local2 = this.plugin.Config ?? (this.plugin.PluginInterface.GetPluginConfig() as CrystalConfig ?? new CrystalConfig());

                            // Basic sanitization: remove entries without '@' or with invalid names
                            try
                            {
                                if (cfg_local2.StoredCharacters != null && cfg_local2.StoredCharacters.ByCharacter != null)
                                {
                                    var toRemove = new System.Collections.Generic.List<string>();
                                    foreach (var kv in cfg_local2.StoredCharacters.ByCharacter)
                                    {
                                        try
                                        {
                                            var key = kv.Key ?? string.Empty;
                                            var sc = kv.Value ?? new StoredCharacter();
                                            var atIdx = key.IndexOf('@');
                                            var validKey = atIdx > 0 && atIdx < key.Length - 1;
                                            var nameInvalid = string.IsNullOrWhiteSpace(sc.Name) || (sc.Name?.Contains("Lumina.") ?? false) || (sc.Name?.Contains("Excel.Row") ?? false) || (sc.Name?.Contains("Row`") ?? false);
                                            if (!validKey || nameInvalid) toRemove.Add(key);
                                        }
                                        catch { }
                                    }
                                    foreach (var k in toRemove) { try { cfg_local2.StoredCharacters.ByCharacter.Remove(k); } catch { } }
                                }
                            }
                            catch { }

                            try
                            {
                                if (cfg_local2.StoredCharacters != null && cfg_local2.StoredCharacters.ByCharacter != null)
                                {
                                    foreach (var kv in cfg_local2.StoredCharacters.ByCharacter)
                                    {
                                        try
                                        {
                                            var sc = kv.Value;
                                            if (sc?.RetainerCounts == null) continue;
                                            var rrem = new System.Collections.Generic.List<string>();
                                            foreach (var rkv in sc.RetainerCounts)
                                            {
                                                try
                                                {
                                                    var rname = rkv.Key ?? string.Empty;
                                                    var invalidR = string.IsNullOrWhiteSpace(rname) || (rname.Contains("Lumina.") || rname.Contains("Excel.Row") || rname.Contains("Row`")) || string.Equals(rname, "Player", StringComparison.OrdinalIgnoreCase);
                                                    if (invalidR) rrem.Add(rname);
                                                }
                                                catch { }
                                            }
                                            foreach (var rn in rrem) { try { sc.RetainerCounts.Remove(rn); } catch { } }
                                        }
                                        catch { }
                                    }
                                }
                            }
                            catch { }

                            try
                            {
                                if (cfg_local2.RetainerNames != null)
                                {
                                    var ridRem = new System.Collections.Generic.List<ulong>();
                                    foreach (var kv in cfg_local2.RetainerNames)
                                    {
                                        try
                                        {
                                            var v = kv.Value ?? string.Empty;
                                            if (string.IsNullOrWhiteSpace(v) || string.Equals(v, "Player", StringComparison.OrdinalIgnoreCase) || v.Contains("Lumina.") || v.Contains("Excel.Row") || v.Contains("Row`")) ridRem.Add(kv.Key);
                                        }
                                        catch { }
                                    }
                                    foreach (var id in ridRem) { try { cfg_local2.RetainerNames.Remove(id); } catch { } }
                                }
                            }
                            catch { }

                            try { this.plugin.PluginInterface.SavePluginConfig(cfg_local2); } catch { }
                            try { this.plugin.Chat?.Print("Crystal Terror: sanitized stored data."); } catch { }
                        }
                        catch { }
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndPopup();
                }
            }
            catch { }
        }

        private void CopyRawInventoryToClipboard()
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
    }
}
