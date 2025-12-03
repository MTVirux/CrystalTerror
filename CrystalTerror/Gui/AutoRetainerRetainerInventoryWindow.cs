using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;

namespace CrystalTerror.Gui
{
    public class AutoRetainerRetainerInventoryWindow : Window, IDisposable
    {
        private bool disposed;
        private readonly IDalamudPluginInterface pluginInterface;
        private ulong currentCid = 0;
        private string currentRetainer = string.Empty;
        private List<(string Key, long Count)> entries = new();

        public AutoRetainerRetainerInventoryWindow(IDalamudPluginInterface pluginInterface)
            : base("Retainer Inventory###CrystalTerrorRetainerInventoryWindow")
        {
            this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
            SizeConstraints = new WindowSizeConstraints()
            {
                MinimumSize = new System.Numerics.Vector2(300, 120),
                MaximumSize = new System.Numerics.Vector2(9999, 9999),
            };
        }

        public void SetRetainer(ulong cid, string retName)
        {
            this.currentCid = cid;
            this.currentRetainer = retName ?? string.Empty;
            Refresh();
        }

        public override void Draw()
        {
            ImGui.TextUnformatted($"Retainer: {this.currentRetainer} ({this.currentCid})");
            ImGui.SameLine();
            if (ImGui.Button("Close")) this.IsOpen = false;

            ImGui.Separator();
            ImGui.TextUnformatted("AutoRetainer inventory processing is disabled.");
            ImGui.TextUnformatted("Per-retainer inventories from AutoRetainer are not used by CrystalTerror.");
        }

        public void Refresh()
        {
            // Inventory data from AutoRetainer is not processed; leave entries empty
            this.entries = new List<(string, long)>();
        }

        public void Dispose()
        {
            if (this.disposed) return;
            this.disposed = true;
        }
    }
}
