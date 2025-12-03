using System;
using System.Collections.Generic;
using System.Linq;
using OtterGui;
using OtterGui.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using OtterGui.Text;
using Dalamud.Plugin;
// Use dynamic for client state to avoid a hard dependency on the Dalamud client state interface

namespace CrystalTerror.Gui
{
    public class CrystalTerrorWindow : Window, IDisposable
    {
        public Action? RequestOpenAutoRetainer;
        private bool disposed;
        private readonly PluginConfig config;
        private readonly dynamic? clientState;
        private readonly Func<(string? Name, string? World)>? getPlayerFunc;
        private readonly IDalamudPluginInterface pluginInterface;
        public Action? RequestOpenConfig;
        public CrystalTerrorWindow(PluginConfig config, dynamic? clientState, Func<(string? Name, string? World)>? getPlayerFunc = null, IDalamudPluginInterface? pluginInterface = null)
            : base("CrystalTerror###CrystalTerrorMainWindow")
        {
            this.config = config;
            this.clientState = clientState;
            this.getPlayerFunc = getPlayerFunc;
            this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
            SizeConstraints = new WindowSizeConstraints()
            {
                MinimumSize = new System.Numerics.Vector2(300, 120),
                MaximumSize = new System.Numerics.Vector2(9999, 9999),
            };
            
            // Add a cog to the title bar that opens the config when clicked
            try
            {
                TitleBarButtons.Add(new()
                {
                    Click = (m) => { if (m == ImGuiMouseButton.Left) this.RequestOpenConfig?.Invoke(); },
                    Icon = (Dalamud.Interface.FontAwesomeIcon)FontAwesomeIcon.Cog,
                    IconOffset = new(2, 2),
                    ShowTooltip = () => ImGui.SetTooltip("Open settings window"),
                });
            }
            catch
            {
                // If TitleBarButtons or related types are not available, silently ignore.
            }

        }

        public override void PreDraw()
        {
            // customize window title or flags here if needed
        }

        public override void Draw()
        {

            // Logged-in character. Prefer plugin-provided values (kept up-to-date by OnFrameworkUpdate),
            // otherwise fall back to reflective reading of clientState.
            var playerName = "(none)";
            var playerWorld = "(unknown)";

            if (this.getPlayerFunc != null)
            {
                try
                {
                    var tup = this.getPlayerFunc();
                    if (!string.IsNullOrEmpty(tup.Name))
                        playerName = tup.Name;
                    if (!string.IsNullOrEmpty(tup.World))
                        playerWorld = tup.World;
                }
                catch
                {
                    // swallow; will attempt reflection below
                }
            }

            if ((playerName == "(none)" || playerWorld == "(unknown)") && this.clientState != null)
            {
                try
                {
                    var clientType = this.clientState.GetType();
                    var localPlayerProp = clientType.GetProperty("LocalPlayer");
                    var localPlayer = localPlayerProp?.GetValue(this.clientState);
                    if (localPlayer != null)
                    {
                        var nameProp = localPlayer.GetType().GetProperty("Name");
                        var nameVal = nameProp?.GetValue(localPlayer);
                        if (!string.IsNullOrEmpty(nameVal?.ToString()))
                            playerName = nameVal?.ToString() ?? playerName;

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

                                if (!string.IsNullOrEmpty(resolved))
                                    playerWorld = resolved;
                                else if (!string.IsNullOrEmpty(hw.ToString()))
                                    playerWorld = hw.ToString();
                            }
                        }
                        else
                        {
                            var worldProp = localPlayer.GetType().GetProperty("World");
                            var worldVal = worldProp?.GetValue(localPlayer);
                            if (worldVal != null && !string.IsNullOrEmpty(worldVal.ToString()))
                                playerWorld = worldVal.ToString() ?? playerWorld;
                        }
                    }
                }
                catch
                {
                    // ignore reflection errors
                }
            }

            ImGui.Spacing();
            ImGui.Bullet(); ImGui.TextUnformatted($"Logged in character: {playerName}@{playerWorld}");

            ImGui.SameLine();
            if (ImGui.Button("AutoRetainer chars"))
            {
                this.RequestOpenAutoRetainer?.Invoke();
            }

            ImGui.Spacing();
            ImGui.TextUnformatted("Saved characters:");
            ImGui.Spacing();
            if (this.config?.Characters != null)
            {
                // Determine the list to display. In EditMode we must operate on the persisted list
                // so reordering buttons map to the underlying indices. Otherwise, present a sorted
                // copy based on the selected CharacterSort.
                List<StoredCharacter> displayList;
                if (this.config.EditMode)
                {
                    displayList = this.config.Characters;
                }
                else
                {
                    switch (this.config.CharacterSort)
                    {
                        case CharacterSort.Alphabetical:
                            displayList = this.config.Characters.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                                .ThenBy(c => c.World, StringComparer.OrdinalIgnoreCase).ToList();
                            break;
                        case CharacterSort.ReverseAlphabetical:
                            displayList = this.config.Characters.OrderByDescending(c => c.Name, StringComparer.OrdinalIgnoreCase)
                                .ThenByDescending(c => c.World, StringComparer.OrdinalIgnoreCase).ToList();
                            break;
                        case CharacterSort.World:
                            displayList = this.config.Characters.OrderBy(c => c.World, StringComparer.OrdinalIgnoreCase)
                                .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
                            break;
                        case CharacterSort.ReverseWorld:
                            displayList = this.config.Characters.OrderByDescending(c => c.World, StringComparer.OrdinalIgnoreCase)
                                .ThenByDescending(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
                            break;
                        case CharacterSort.AutoRetainer:
                            {
                                try
                                {
                                    var getCids = this.pluginInterface.GetIpcSubscriber<List<ulong>>("AutoRetainer.GetRegisteredCIDs");
                                    var cids = getCids?.InvokeFunc();
                                    if (cids != null && cids.Count > 0)
                                    {
                                        var getOcd = this.pluginInterface.GetIpcSubscriber<ulong, object>("AutoRetainer.GetOfflineCharacterData");
                                        var orderKeys = new List<string>();
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
                                                orderKeys.Add((name + "\u0001" + world).ToLowerInvariant());
                                            }
                                            catch
                                            {
                                                // ignore per-cid errors
                                            }
                                        }

                                        var rank = new Dictionary<string, int>();
                                        for (var idx = 0; idx < orderKeys.Count; ++idx)
                                        {
                                            var k = orderKeys[idx];
                                            if (!rank.ContainsKey(k)) rank[k] = idx;
                                        }

                                        displayList = this.config.Characters.OrderBy(c =>
                                        {
                                            var key = (c.Name + "\u0001" + c.World).ToLowerInvariant();
                                            return rank.TryGetValue(key, out var r) ? r : int.MaxValue;
                                        }).ToList();
                                        break;
                                    }
                                }
                                catch
                                {
                                    // fall back to persisted order
                                }

                                displayList = this.config.Characters;
                                break;
                            }
                        default:
                            displayList = this.config.Characters;
                            break;
                    }
                }

                for (var i = 0; i < displayList.Count; ++i)
                {
                    var c = displayList[i];
                    var header = $"{c.Name}@{c.World}";

                    ImGui.PushID(i);
                    if (this.config.EditMode)
                    {
                        var isFirst = i == 0;
                        var isLast = i == this.config.Characters.Count - 1;

                        ImGui.BeginDisabled(isFirst);
                        if (ImGui.Button("↑"))
                        {
                            if (!isFirst)
                            {
                                var tmp = this.config.Characters[i - 1];
                                this.config.Characters[i - 1] = this.config.Characters[i];
                                this.config.Characters[i] = tmp;
                                try
                                {
                                    this.pluginInterface.SavePluginConfig(this.config);
                                }
                                catch
                                {
                                    // ignore save errors
                                }
                            }
                        }
                        ImGui.EndDisabled();
                        ImGui.SameLine();

                        ImGui.BeginDisabled(isLast);
                        if (ImGui.Button("↓"))
                        {
                            if (!isLast)
                            {
                                var tmp = this.config.Characters[i + 1];
                                this.config.Characters[i + 1] = this.config.Characters[i];
                                this.config.Characters[i] = tmp;
                                try
                                {
                                    this.pluginInterface.SavePluginConfig(this.config);
                                }
                                catch
                                {
                                    // ignore save errors
                                }
                            }
                        }
                        ImGui.EndDisabled();
                        ImGui.SameLine();
                    }

                    if (ImGui.CollapsingHeader(header))
                    {
                        ImGui.Indent();
                        ImGui.TextUnformatted($"Last update (UTC): {c.LastUpdateUtc:u}");

                        // If we have stored retainers, show them in a table (rows = retainers, columns = elemental inventory)
                        if (c.Retainers != null && c.Retainers.Count > 0)
                        {
                            ImGui.TextUnformatted("Retainers:");
                            ImGui.Indent();
                            try
                            {
                                var elements = Enum.GetValues(typeof(CrystalTerror.Element)).Cast<CrystalTerror.Element>().ToArray();
                                var colCount = 1 + elements.Length; // Name + each element
                                if (ImGui.BeginTable($"retainers_table_{i}", colCount, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
                                {
                                    ImGui.TableSetupColumn("Name");
                                    foreach (var el in elements)
                                        ImGui.TableSetupColumn(el.ToString());

                                    ImGui.TableHeadersRow();

                                    for (var ri = 0; ri < c.Retainers.Count; ++ri)
                                    {
                                        var r = c.Retainers[ri];
                                        ImGui.TableNextRow();
                                        ImGui.TableSetColumnIndex(0);
                                        ImGui.TextUnformatted(r?.Name ?? "(unknown)");

                                        for (var ei = 0; ei < elements.Length; ++ei)
                                        {
                                            ImGui.TableSetColumnIndex(1 + ei);
                                            try
                                            {
                                                var el = elements[ei];
                                                var inv = r?.Inventory ?? new CrystalTerror.Inventory();
                                                var s = $"{inv.GetCount(el, CrystalTerror.CrystalType.Shard)}/{inv.GetCount(el, CrystalTerror.CrystalType.Crystal)}/{inv.GetCount(el, CrystalTerror.CrystalType.Cluster)}";
                                                ImGui.TextUnformatted(s);
                                            }
                                            catch
                                            {
                                                ImGui.TextUnformatted("-");
                                            }
                                        }
                                    }

                                    ImGui.EndTable();
                                }
                            }
                            catch
                            {
                                // fallback: simple list if table rendering fails
                                ImGui.Indent();
                                for (var ri = 0; ri < c.Retainers.Count; ++ri)
                                    ImGui.BulletText(c.Retainers[ri]?.Name ?? "(unknown)");
                                ImGui.Unindent();
                            }

                            ImGui.Unindent();
                        }
                        else
                        {
                            // No stored retainers — try to query AutoRetainer for this character and list them
                            var listed = false;
                            try
                            {
                                var getCids = this.pluginInterface.GetIpcSubscriber<System.Collections.Generic.List<ulong>>("AutoRetainer.GetRegisteredCIDs");
                                var cids = getCids?.InvokeFunc();
                                if (cids != null && cids.Count > 0)
                                {
                                    var getOcd = this.pluginInterface.GetIpcSubscriber<ulong, object>("AutoRetainer.GetOfflineCharacterData");
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

                                            if (!string.Equals(name, c.Name, StringComparison.OrdinalIgnoreCase) || !string.Equals(world, c.World, StringComparison.OrdinalIgnoreCase))
                                                continue;

                                            // matched character — enumerate retainer entries if present
                                            try
                                            {
                                                if (d.RetainerData != null)
                                                {
                                                    ImGui.TextUnformatted("Retainers (AutoRetainer):");
                                                    ImGui.Indent();
                                                    try
                                                    {
                                                        foreach (var rd in d.RetainerData)
                                                        {
                                                            string rname = "(unknown)";
                                                            try { rname = rd.Name ?? rname; } catch { }
                                                            ImGui.BulletText(rname);
                                                        }
                                                    }
                                                    catch
                                                    {
                                                        // if enumeration fails, ignore
                                                    }
                                                    ImGui.Unindent();
                                                    listed = true;
                                                    break;
                                                }
                                            }
                                            catch
                                            {
                                                // ignore per-cid failures
                                            }
                                        }
                                        catch
                                        {
                                            // ignore per-cid failures
                                        }
                                    }
                                }
                            }
                            catch
                            {
                                // ignore IPC errors
                            }

                            if (!listed)
                            {
                                ImGui.TextUnformatted("Retainers: none");
                            }
                        }

                        ImGui.Unindent();
                    }

                    ImGui.PopID();
                }
            }
        }

        public override void OnClose()
        {
            // save ephemeral state if necessary
        }

        public void Dispose()
        {
            if (this.disposed)
                return;

            this.disposed = true;
        }
    }
}
