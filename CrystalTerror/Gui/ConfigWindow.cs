using System;
using System.Collections.Generic;
using System.Linq;
using OtterGui;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace CrystalTerror.Gui
{
    public class ConfigWindow : Window, IDisposable
    {
        private bool disposed;
        private readonly PluginConfig config;
        private readonly IDalamudPluginInterface pluginInterface;
        private readonly IPluginLog log;
        private string importResultMessage = string.Empty;
        private bool wasOpen = false;

        

        public ConfigWindow(PluginConfig config, IDalamudPluginInterface pluginInterface, IPluginLog log)
            : base("CrystalTerror Config###CrystalTerrorConfigWindow")
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            SizeConstraints = new WindowSizeConstraints()
            {
                MinimumSize = new System.Numerics.Vector2(300, 100),
                MaximumSize = new System.Numerics.Vector2(9999, 9999),
            };
        }

        private void SaveConfig()
        {
            try
            {
                this.pluginInterface.SavePluginConfig(this.config);
                try { this.log.Information("CrystalTerror: Saved plugin config."); } catch { }
            }
            catch (Exception ex)
            {
                try { this.log.Error($"CrystalTerror: Failed to save plugin config: {ex}"); } catch { }
            }
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
                    SaveConfig();
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
                            SaveConfig();
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
                            SaveConfig();
                        }
                        catch { }
                    }
                }
                ImGui.Unindent();
            }

            // --- Warnings ---
            if (this.IsOpen && !this.wasOpen) ImGui.SetNextItemOpen(false, ImGuiCond.Once);
            if (ImGui.CollapsingHeader("Warnings"))
            {
                try
                {
                    if (this.config.Warnings == null) this.config.Warnings = new WarningSettings();
                    var ws = this.config.Warnings;

                    ImGui.TextUnformatted("Warning thresholds:");
                    ImGui.Indent();

                    var changed = false;

                    // Level 1
                    // Enable/disable
                    var en1 = ws.Level1.Enabled;
                    if (ImGui.Checkbox("Enable Level 1##warn_en1", ref en1))
                    {
                        ws.Level1.Enabled = en1;
                        changed = true;
                    }

                    var l1 = ws.Level1.Threshold;
                    if (ImGui.SliderInt("Level 1 threshold##warn_l1", ref l1, 1, 9999))
                    {
                        if (l1 < 1) l1 = 1;
                        if (l1 >= ws.Level2.Threshold)
                        {
                            ws.Level2.Threshold = Math.Min(9999, l1 + 1);
                            if (ws.Level3.Threshold <= ws.Level2.Threshold)
                                ws.Level3.Threshold = Math.Min(9999, ws.Level2.Threshold + 1);
                        }
                        ws.Level1.Threshold = l1;
                        changed = true;
                    }
                    var color1 = ws.Level1.Color;
                    if (color1 == null || color1.Length < 4) color1 = new float[] { 1f, 0.9f, 0f, 1f };
                    var vec1 = new System.Numerics.Vector4(color1[0], color1[1], color1[2], color1[3]);
                    if (ImGui.ColorEdit4("Level 1 color##warn_c1", ref vec1))
                    {
                        ws.Level1.Color = new float[] { vec1.X, vec1.Y, vec1.Z, vec1.W };
                        changed = true;
                    }

                    ImGui.Spacing();

                    // Level 2
                    // Enable/disable
                    var en2 = ws.Level2.Enabled;
                    if (ImGui.Checkbox("Enable Level 2##warn_en2", ref en2))
                    {
                        ws.Level2.Enabled = en2;
                        changed = true;
                    }

                    var l2 = ws.Level2.Threshold;
                    if (ImGui.SliderInt("Level 2 threshold##warn_l2", ref l2, 1, 9999))
                    {
                        if (l2 <= ws.Level1.Threshold)
                        {
                            ws.Level1.Threshold = Math.Max(1, l2 - 1);
                            changed = true;
                        }
                        if (l2 >= ws.Level3.Threshold)
                        {
                            ws.Level3.Threshold = Math.Min(9999, l2 + 1);
                            changed = true;
                        }
                        ws.Level2.Threshold = l2;
                        changed = true;
                    }
                    var color2 = ws.Level2.Color;
                    if (color2 == null || color2.Length < 4) color2 = new float[] { 1f, 0.6f, 0.15f, 1f };
                    var vec2 = new System.Numerics.Vector4(color2[0], color2[1], color2[2], color2[3]);
                    if (ImGui.ColorEdit4("Level 2 color##warn_c2", ref vec2))
                    {
                        ws.Level2.Color = new float[] { vec2.X, vec2.Y, vec2.Z, vec2.W };
                        changed = true;
                    }

                    ImGui.Spacing();

                    // Level 3
                    // Enable/disable
                    var en3 = ws.Level3.Enabled;
                    if (ImGui.Checkbox("Enable Level 3##warn_en3", ref en3))
                    {
                        ws.Level3.Enabled = en3;
                        changed = true;
                    }

                    var l3 = ws.Level3.Threshold;
                    if (ImGui.SliderInt("Level 3 threshold##warn_l3", ref l3, 1, 9999))
                    {
                        if (l3 <= ws.Level2.Threshold)
                        {
                            ws.Level2.Threshold = Math.Max(1, l3 - 1);
                            if (ws.Level1.Threshold >= ws.Level2.Threshold)
                                ws.Level1.Threshold = Math.Max(1, ws.Level2.Threshold - 1);
                            changed = true;
                        }
                        ws.Level3.Threshold = l3;
                        changed = true;
                    }
                    var color3 = ws.Level3.Color;
                    if (color3 == null || color3.Length < 4) color3 = new float[] { 1f, 0.15f, 0.15f, 1f };
                    var vec3 = new System.Numerics.Vector4(color3[0], color3[1], color3[2], color3[3]);
                    if (ImGui.ColorEdit4("Level 3 color##warn_c3", ref vec3))
                    {
                        ws.Level3.Color = new float[] { vec3.X, vec3.Y, vec3.Z, vec3.W };
                        changed = true;
                    }

                    if (changed)
                    {
                        this.config.Warnings = ws;
                        SaveConfig();
                    }

                    ImGui.Unindent();
                }
                catch
                {
                    // ignore UI errors
                }
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

                    SaveConfig();
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
                        SaveConfig();
                    }
                    if (ImGui.Selectable("Reverse alphabetical", this.config.CharacterSort == CrystalTerror.CharacterSort.ReverseAlphabetical))
                    {
                        this.config.CharacterSort = CrystalTerror.CharacterSort.ReverseAlphabetical;
                        SaveConfig();
                    }
                    if (ImGui.Selectable("World", this.config.CharacterSort == CrystalTerror.CharacterSort.World))
                    {
                        this.config.CharacterSort = CrystalTerror.CharacterSort.World;
                        SaveConfig();
                    }
                    if (ImGui.Selectable("Reverse by world", this.config.CharacterSort == CrystalTerror.CharacterSort.ReverseWorld))
                    {
                        this.config.CharacterSort = CrystalTerror.CharacterSort.ReverseWorld;
                        SaveConfig();
                    }
                    if (ImGui.Selectable("Custom (persisted order)", this.config.CharacterSort == CrystalTerror.CharacterSort.Custom))
                    {
                        this.config.CharacterSort = CrystalTerror.CharacterSort.Custom;
                        SaveConfig();
                    }
                    if (ImGui.Selectable("AutoRetainer order", this.config.CharacterSort == CrystalTerror.CharacterSort.AutoRetainer))
                    {
                        this.config.CharacterSort = CrystalTerror.CharacterSort.AutoRetainer;
                        SaveConfig();
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

                            SaveConfig();
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
                            SaveConfig();
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

                    SaveConfig();
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
                SaveConfig();
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
