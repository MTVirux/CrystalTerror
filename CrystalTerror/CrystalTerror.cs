using System;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using Dalamud.Game.ClientState;
using Dalamud.Game.Text;

namespace CrystalTerror
{
    public sealed class CrystalTerror : IDalamudPlugin
    {
        public string Name => "Crystal Terror";

        private readonly WindowSystem windowSystem = new(typeof(CrystalTerror).AssemblyQualifiedName);
        private readonly CrystalWindow window;
        private readonly ConfigWindow configWindow;

        public IDalamudPluginInterface PluginInterface { get; init; }
        public IDataManager DataManager { get; init; }
        public ICommandManager CommandManager { get; init; } = null!;
        public IClientState ClientState { get; init; }
        public IPlayerState PlayerState { get; init; }
        public Dalamud.Plugin.Services.IGameInventory GameInventory { get; init; }
        public IObjectTable ObjectTable { get; init; }
        public IFramework Framework { get; init; }
        public IChatGui Chat { get; init; }
        public IPluginLog Log { get; init; }

        public CrystalConfig Config { get; private set; }

        public CrystalTerror(IDalamudPluginInterface pluginInterface, IDataManager dataManager, ICommandManager commandManager, IFramework framework, IClientState clientState, IPlayerState playerState, Dalamud.Plugin.Services.IGameInventory gameInventory, IObjectTable objectTable, IChatGui chat, IPluginLog log)
        {
            this.PluginInterface = pluginInterface;
            this.DataManager = dataManager;
            this.CommandManager = commandManager;
            this.Framework = framework;
            this.ClientState = clientState;
            this.PlayerState = playerState;
            this.GameInventory = gameInventory;
            this.ObjectTable = objectTable;
            this.Chat = chat;
            this.Log = log;

            this.Config = this.PluginInterface.GetPluginConfig() as CrystalConfig ?? new CrystalConfig();

            this.window = new CrystalWindow(this);
            this.configWindow = new ConfigWindow(this);
            this.windowSystem.AddWindow(this.window);
            this.windowSystem.AddWindow(this.configWindow);

            this.PluginInterface.UiBuilder.Draw += this.DrawUi;
            this.PluginInterface.UiBuilder.OpenConfigUi += this.OpenConfigUi;
            this.PluginInterface.UiBuilder.OpenMainUi += this.OpenMainUi;

            // Register chat commands to open the window
            this.CommandManager?.AddHandler("/crystaline", new CommandInfo(this.OnOpenCommand) { HelpMessage = "Open the CrystalTerror window." });
            this.CommandManager?.AddHandler("/ct", new CommandInfo(this.OnOpenCommand) { HelpMessage = "Open the CrystalTerror window." });
            this.CommandManager?.AddHandler("/ctdump", new CommandInfo(this.OnDumpCommand) { HelpMessage = "Copy raw inventory to clipboard." });

#if DEBUG
            this.window.IsOpen = true;
#endif
        }

        public void Dispose()
        {
            this.PluginInterface.SavePluginConfig(this.Config);
            this.PluginInterface.UiBuilder.Draw -= this.DrawUi;
            this.PluginInterface.UiBuilder.OpenConfigUi -= this.OpenConfigUi;
            this.PluginInterface.UiBuilder.OpenMainUi -= this.OpenMainUi;
            if (this.CommandManager != null)
            {
                this.CommandManager.RemoveHandler("/crystaline");
                this.CommandManager.RemoveHandler("/ct");
                this.CommandManager.RemoveHandler("/ctdump");
            }
            this.windowSystem.RemoveAllWindows();
            this.window.Dispose();
            try { this.configWindow.Dispose(); } catch { }
        }

        public void OpenConfigUi() => this.configWindow.IsOpen = true;

        public void OpenMainUi() => this.window.IsOpen = true;

        private void OnOpenCommand(string command, string arguments)
        {
            this.window.IsOpen = true;
        }

        private void OnDumpCommand(string command, string arguments)
        {
            try
            {
                this.window.CopyRawInventoryToClipboard();
                try { this.Chat.Print("Crystal Terror: raw inventory copied to clipboard."); } catch { }
            }
            catch (Exception ex)
            {
                this.Log?.Error(ex, "Failed to copy raw inventory");
            }
        }

        private void DrawUi() => this.windowSystem.Draw();
    }
}
