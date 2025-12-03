using System;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;

namespace CrystalTerror.Gui
{
    public class AutoRetainerRetainersWindow : Window, IDisposable
    {
        private bool disposed;
        private readonly IDalamudPluginInterface pluginInterface;
        private ulong currentCid = 0;
        private string currentName = string.Empty;
        private List<(string Name, string World)> retainers = new();

        public AutoRetainerRetainersWindow(IDalamudPluginInterface pluginInterface)
            : base("AutoRetainer Retainers###CrystalTerrorAutoRetainersWindow")
        {
            this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
            SizeConstraints = new WindowSizeConstraints()
            {
                MinimumSize = new System.Numerics.Vector2(300, 120),
                MaximumSize = new System.Numerics.Vector2(9999, 9999),
            };
        }

        public void SetCharacter(ulong cid, string name)
        {
            this.currentCid = cid;
            this.currentName = name ?? string.Empty;
            RefreshList();
        }

        public override void Draw()
        {
            ImGui.TextUnformatted($"Character: {this.currentName} ({this.currentCid})");
            ImGui.SameLine();
            if (ImGui.Button("Refresh")) RefreshList();
            ImGui.SameLine();
            if (ImGui.Button("Close")) this.IsOpen = false;

            ImGui.Separator();
            ImGui.TextUnformatted($"Retainers: {this.retainers.Count}");
            ImGui.Spacing();

            for (var i = 0; i < this.retainers.Count; ++i)
            {
                var r = this.retainers[i];
                ImGui.TextUnformatted($"{r.Name}@{r.World}");
            }
        }

        public void RefreshList()
        {
            try
            {
                var getOcd = this.pluginInterface.GetIpcSubscriber<ulong, object>("AutoRetainer.GetOfflineCharacterData");
                var ocd = getOcd?.InvokeFunc(this.currentCid);
                var list = new List<(string, string)>();
                if (ocd != null)
                {
                    dynamic d = ocd;
                    try
                    {
                        var retData = d.RetainerData;
                        if (retData != null)
                        {
                            foreach (var r in retData)
                            {
                                try
                                {
                                    string rn = "(unknown)";
                                    string rw = "(unknown)";
                                    try { rn = r.Name ?? rn; } catch { }
                                    try { rw = r.World ?? rw; } catch { }
                                    list.Add((rn, rw));
                                }
                                catch { }
                            }
                        }
                    }
                    catch { }
                }

                this.retainers = list;
            }
            catch
            {
                this.retainers = new List<(string, string)>();
            }
        }

        public void Dispose()
        {
            if (this.disposed) return;
            this.disposed = true;
        }
    }
}
