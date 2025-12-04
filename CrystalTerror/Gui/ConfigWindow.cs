using System;
using System.Collections.Generic;
using System.Linq;
using OtterGui;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin;

namespace CrystalTerror.Gui
{
    public class ConfigWindow : Window, IDisposable
    {
        private bool disposed;
        private readonly PluginConfig config;
        private readonly IDalamudPluginInterface pluginInterface;
        private string importResultMessage = string.Empty;
        private bool wasOpen = false;

        

        public ConfigWindow(PluginConfig config, IDalamudPluginInterface pluginInterface)
            : base("CrystalTerror Config###CrystalTerrorConfigWindow")
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
            SizeConstraints = new WindowSizeConstraints()
            {
                MinimumSize = new System.Numerics.Vector2(300, 100),
                MaximumSize = new System.Numerics.Vector2(9999, 9999),
            };
        }

        public override void Draw()
        {
            ImGui.TextUnformatted("CrystalTerror Configuration");
            ImGui.Separator();

            ImGui.Spacing();

            // --- General section ---
            if (this.IsOpen && !this.wasOpen) ImGui.SetNextItemOpen(false, ImGuiCond.Once);
            if (ImGui.CollapsingHeader("General"))
            {
                // Option: suppress external plugin warnings
                var ignoreWarnings = this.config.IgnoreMissingPluginWarnings;
                if (ImGui.Checkbox("Ignore missing plugin warnings (hide top-of-window warnings)", ref ignoreWarnings))
                {
                    this.config.IgnoreMissingPluginWarnings = ignoreWarnings;
                    try { this.pluginInterface.SavePluginConfig(this.config); } catch { }
                }
            }

            // --- Filters ---
            if (this.IsOpen && !this.wasOpen) ImGui.SetNextItemOpen(false, ImGuiCond.Once);
            if (ImGui.CollapsingHeader("Filters"))
            {
                ImGui.TextUnformatted("Elements to include:");
                ImGui.Indent();
                foreach (var el in Enum.GetValues(typeof(CrystalTerror.Element)).Cast<CrystalTerror.Element>())
                {
                    var present = this.config.EnabledElements.Contains(el);
                    var label = el.ToString();
                    if (ImGui.Checkbox(label + "##el_" + label, ref present))
                    {
                        try
                        {
                            if (present)
                            {
                                if (!this.config.EnabledElements.Contains(el)) this.config.EnabledElements.Add(el);
                            }
                            else
                            {
                                if (this.config.EnabledElements.Contains(el)) this.config.EnabledElements.RemoveAll(x => x == el);
                            }
                            this.pluginInterface.SavePluginConfig(this.config);
                        }
                        catch { }
                    }
                }
                ImGui.Unindent();

                ImGui.Spacing();
                ImGui.TextUnformatted("Crystal types to include:");
                ImGui.Indent();
                foreach (var t in Enum.GetValues(typeof(CrystalTerror.CrystalType)).Cast<CrystalTerror.CrystalType>())
                {
                    var present = this.config.EnabledTypes.Contains(t);
                    var label = t.ToString();
                    if (ImGui.Checkbox(label + "##type_" + label, ref present))
                    {
                        try
                        {
                            if (present)
                            {
                                if (!this.config.EnabledTypes.Contains(t)) this.config.EnabledTypes.Add(t);
                            }
                            else
                            {
                                if (this.config.EnabledTypes.Contains(t)) this.config.EnabledTypes.RemoveAll(x => x == t);
                            }
                            this.pluginInterface.SavePluginConfig(this.config);
                        }
                        catch { }
                    }
                }
                ImGui.Unindent();
            }

            // --- Storage / Import section ---
            // --- Character Order ---
            if (this.IsOpen && !this.wasOpen) ImGui.SetNextItemOpen(false, ImGuiCond.Once);
            if (ImGui.CollapsingHeader("Character Order"))
            {
                // Edit mode toggle
                if (ImGui.Button(this.config.EditMode ? "Exit edit mode" : "Enter edit mode"))
                {
                    var entering = !this.config.EditMode;
                    this.config.EditMode = entering;

                    // When entering edit mode, switch view to persisted ordering (Custom)
                    if (entering)
                    {
                        this.config.CharacterSort = CrystalTerror.CharacterSort.Custom;
                    }

                    try { this.pluginInterface.SavePluginConfig(this.config); } catch { }
                }
                ImGui.SameLine();
                ImGui.TextUnformatted(this.config.EditMode ? "Edit mode: ON" : "Edit mode: OFF");

                ImGui.Spacing();

                // Character sort selection (dropdown)
                ImGui.TextUnformatted("Character sort:");
                var comboLabel = this.config.CharacterSort.ToString();
                if (ImGui.BeginCombo("##CharacterSortCombo", comboLabel))
                {
                    if (ImGui.Selectable("Alphabetical", this.config.CharacterSort == CrystalTerror.CharacterSort.Alphabetical))
                    {
                        this.config.CharacterSort = CrystalTerror.CharacterSort.Alphabetical;
                        try { this.pluginInterface.SavePluginConfig(this.config); } catch { }
                    }
                    if (ImGui.Selectable("Reverse alphabetical", this.config.CharacterSort == CrystalTerror.CharacterSort.ReverseAlphabetical))
                    {
                        this.config.CharacterSort = CrystalTerror.CharacterSort.ReverseAlphabetical;
                        try { this.pluginInterface.SavePluginConfig(this.config); } catch { }
                    }
                    if (ImGui.Selectable("World", this.config.CharacterSort == CrystalTerror.CharacterSort.World))
                    {
                        this.config.CharacterSort = CrystalTerror.CharacterSort.World;
                        try { this.pluginInterface.SavePluginConfig(this.config); } catch { }
                    }
                    if (ImGui.Selectable("Reverse by world", this.config.CharacterSort == CrystalTerror.CharacterSort.ReverseWorld))
                    {
                        this.config.CharacterSort = CrystalTerror.CharacterSort.ReverseWorld;
                        try { this.pluginInterface.SavePluginConfig(this.config); } catch { }
                    }
                    if (ImGui.Selectable("Custom (persisted order)", this.config.CharacterSort == CrystalTerror.CharacterSort.Custom))
                    {
                        this.config.CharacterSort = CrystalTerror.CharacterSort.Custom;
                        try { this.pluginInterface.SavePluginConfig(this.config); } catch { }
                    }
                    if (ImGui.Selectable("AutoRetainer order", this.config.CharacterSort == CrystalTerror.CharacterSort.AutoRetainer))
                    {
                        this.config.CharacterSort = CrystalTerror.CharacterSort.AutoRetainer;
                        try { this.pluginInterface.SavePluginConfig(this.config); } catch { }
                    }

                    ImGui.EndCombo();
                }
            }

            // --- Storage / Import section ---
            if (this.IsOpen && !this.wasOpen) ImGui.SetNextItemOpen(false, ImGuiCond.Once);
            if (ImGui.CollapsingHeader("Storage"))
            {
                ImGui.TextUnformatted("Import or purge stored character data");
                ImGui.Spacing();
                if (ImGui.Button("Import data from AutoRetainer"))
                {
                    try
                    {
                        var getCids = this.pluginInterface.GetIpcSubscriber<List<ulong>>("AutoRetainer.GetRegisteredCIDs");
                        var cids = getCids?.InvokeFunc();
                        if (cids == null || cids.Count == 0)
                        {
                            this.importResultMessage = "No AutoRetainer characters found or AutoRetainer not loaded.";
                            ImGui.OpenPopup("ImportResult");
                        }
                        else
                        {
                            var getOcd = this.pluginInterface.GetIpcSubscriber<ulong, object>("AutoRetainer.GetOfflineCharacterData");
                            var added = 0;
                            foreach (var cid in cids)
                            {
                                try
                                {
                                    var ocd = getOcd?.InvokeFunc(cid);
                                    if (ocd == null) continue;
                                    dynamic d = ocd;
                                    string name = "(unknown)";
                                    string world = "(unknown)";
                                    try { name = d.Name ?? name; } catch { }
                                    try { world = d.World ?? d.HomeWorld ?? world; } catch { }

                                    // Check for existing stored character
                                    var existing = this.config.Characters?.FirstOrDefault(x => x.Name == name && x.World == world);
                                    if (existing != null)
                                    {
                                        if (existing.Retainers == null)
                                            existing.Retainers = new List<Retainer>();

                                        try
                                        {
                                            if (d.RetainerData != null)
                                            {
                                                foreach (var rd in d.RetainerData)
                                                {
                                                    string rname = "(unknown)";
                                                    try { rname = rd.Name ?? rname; } catch { }
                                                    if (!existing.Retainers.Any(rr => string.Equals(rr.Name, rname, StringComparison.OrdinalIgnoreCase)))
                                                    {
                                                        var r = new Retainer(existing) { Name = rname };
                                                        existing.Retainers.Add(r);
                                                    }
                                                }
                                            }
                                        }
                                        catch { }

                                        // already stored, nothing more to add
                                        continue;
                                    }

                                    // New stored character
                                    var sc = new StoredCharacter()
                                    {
                                        Name = name,
                                        World = world,
                                        ServiceAccount = 1,
                                        LastUpdateUtc = DateTime.UtcNow,
                                        Retainers = new List<Retainer>(),
                                        Inventory = new Inventory(),
                                    };

                                    try
                                    {
                                        if (d.RetainerData != null)
                                        {
                                            foreach (var rd in d.RetainerData)
                                            {
                                                string rname = "(unknown)";
                                                try { rname = rd.Name ?? rname; } catch { }

                                                var r = new Retainer(sc) { Name = rname };
                                                sc.Retainers.Add(r);
                                            }
                                        }

                                        // Persist the new stored character
                                        this.config.Characters.Add(sc);
                                        added++;
                                    }
                                    catch { }
                                }
                                catch
                                {
                                    // ignore individual failures
                                }
                            }

                            try { this.pluginInterface.SavePluginConfig(this.config); } catch { }
                            this.importResultMessage = $"Imported {added} characters from AutoRetainer.";
                            ImGui.OpenPopup("ImportResult");
                        }
                    }
                    catch
                    {
                        this.importResultMessage = "Import failed (AutoRetainer missing or IPC error).";
                        ImGui.OpenPopup("ImportResult");
                    }
                }

                if (ImGui.Button("Purge saved data"))
                {
                    ImGui.OpenPopup("PurgeConfirm");
                }
            }

            

            if (ImGui.BeginPopupModal("PurgeConfirm", ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.TextUnformatted("This will delete all stored characters and their data. This action cannot be undone.");
                ImGui.Spacing();
                if (ImGui.Button("Confirm Purge"))
                {
                    try
                    {
                        this.config.Characters?.Clear();
                        this.pluginInterface.SavePluginConfig(this.config);
                    }
                    catch
                    {
                        // ignore save errors
                    }
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel"))
                {
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }

            if (ImGui.BeginPopupModal("ImportResult", ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.TextUnformatted(this.importResultMessage);
                ImGui.Spacing();
                if (ImGui.Button("OK"))
                {
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }

            // Update open-state tracker
            this.wasOpen = this.IsOpen;
        }

        /// <summary>
        /// Import a single AutoRetainer character (by CID) into the stored config.
        /// This performs the same logic as the bulk import button but for one CID.
        /// </summary>
        public void ImportCharacterFromCid(ulong cid)
        {
            try
            {
                var getOcd = this.pluginInterface.GetIpcSubscriber<ulong, object>("AutoRetainer.GetOfflineCharacterData");
                var ocd = getOcd?.InvokeFunc(cid);
                if (ocd == null)
                {
                    this.importResultMessage = "Character data not available from AutoRetainer.";
                    return;
                }

                dynamic d = ocd;
                string name = "(unknown)";
                string world = "(unknown)";
                try { name = d.Name ?? name; } catch { }
                try { world = d.World ?? d.HomeWorld ?? world; } catch { }

                var existing = this.config.Characters?.FirstOrDefault(x => x.Name == name && x.World == world);
                if (existing != null)
                {
                    if (existing.Retainers == null)
                        existing.Retainers = new List<Retainer>();

                    try
                    {
                        if (d.RetainerData != null)
                        {
                            foreach (var rd in d.RetainerData)
                            {
                                string rname = "(unknown)";
                                try { rname = rd.Name ?? rname; } catch { }
                                if (!existing.Retainers.Any(rr => string.Equals(rr.Name, rname, StringComparison.OrdinalIgnoreCase)))
                                {
                                    var r = new Retainer(existing) { Name = rname };
                                    existing.Retainers.Add(r);
                                }
                            }
                        }
                    }
                    catch { }

                    try { this.pluginInterface.SavePluginConfig(this.config); } catch { }
                    this.importResultMessage = $"Updated retainers for {name}@{world}.";
                    return;
                }

                var sc = new StoredCharacter()
                {
                    Name = name,
                    World = world,
                    ServiceAccount = 1,
                    LastUpdateUtc = DateTime.UtcNow,
                    Retainers = new List<Retainer>(),
                    Inventory = new Inventory(),
                };

                try
                {
                    if (d.RetainerData != null)
                    {
                        foreach (var rd in d.RetainerData)
                        {
                            string rname = "(unknown)";
                            try { rname = rd.Name ?? rname; } catch { }
                            var r = new Retainer(sc) { Name = rname };
                            sc.Retainers.Add(r);
                        }
                    }
                }
                catch { }

                this.config.Characters.Add(sc);
                try { this.pluginInterface.SavePluginConfig(this.config); } catch { }
                this.importResultMessage = $"Imported character {name}@{world} from AutoRetainer.";
            }
            catch
            {
                // ignore; leave message for user via UI if needed
            }
        }

        public void Dispose()
        {
            if (this.disposed)
                return;

            this.disposed = true;
        }
    }
}
