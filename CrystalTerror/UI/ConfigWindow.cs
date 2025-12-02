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
