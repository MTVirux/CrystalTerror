using System;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;

namespace CrystalTerror.Gui
{
    public class AutoRetainerCharsWindow : Window, IDisposable
    {
        private bool disposed;
        private readonly IDalamudPluginInterface pluginInterface;
        private List<(ulong Cid, string Name, string World)> characters = new();

        public AutoRetainerCharsWindow(IDalamudPluginInterface pluginInterface)
            : base("AutoRetainer Characters###CrystalTerrorAutoRetainerWindow")
        {
            this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
            SizeConstraints = new WindowSizeConstraints()
            {
                MinimumSize = new System.Numerics.Vector2(300, 120),
                MaximumSize = new System.Numerics.Vector2(9999, 9999),
            };
        }

        public override void Draw()
        {
            if (ImGui.Button("Refresh"))
            {
                RefreshList();
            }

            ImGui.SameLine();
            if (ImGui.Button("Close"))
            {
                this.IsOpen = false;
            }

            ImGui.Separator();

            ImGui.TextUnformatted($"Known characters: {this.characters.Count}");
            ImGui.Spacing();

            for (var i = 0; i < this.characters.Count; ++i)
            {
                var c = this.characters[i];
                ImGui.PushID(i);
                if (ImGui.Button("Open"))
                {
                    this.RequestOpenRetainers?.Invoke(c.Cid, c.Name);
                    try { this.RequestImportCharacter?.Invoke(c.Cid); } catch { }
                }
                ImGui.SameLine();
                ImGui.TextUnformatted($"{c.Name}@{c.World}  ({c.Cid})");
                ImGui.PopID();
            }
        }

        public void RefreshList()
        {
            try
            {
                var getCids = this.pluginInterface.GetIpcSubscriber<List<ulong>>("AutoRetainer.GetRegisteredCIDs");
                var cids = getCids?.InvokeFunc();
                if (cids == null)
                {
                    this.characters = new List<(ulong, string, string)>();
                    return;
                }

                var getOcd = this.pluginInterface.GetIpcSubscriber<ulong, object>("AutoRetainer.GetOfflineCharacterData");
                var list = new List<(ulong, string, string)>();
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

                        list.Add((cid, name, world));
                    }
                    catch
                    {
                        // ignore per-character errors
                    }
                }

                this.characters = list;
            }
            catch
            {
                this.characters = new List<(ulong, string, string)>();
            }
        }

        public void Dispose()
        {
            if (this.disposed) return;
            this.disposed = true;
        }
        public Action<ulong, string>? RequestOpenRetainers;
        public Action<ulong>? RequestImportCharacter;
    }
}
