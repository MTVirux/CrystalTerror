namespace CrystalTerror
{
    using System;
    using Dalamud.Interface.Windowing;
    using Dalamud.Plugin;
    using Dalamud.Game.Command;
    using Dalamud.Plugin.Services;

    public class CrystalTerrorPlugin : IDalamudPlugin, IDisposable
    {
        public IDalamudPluginInterface PluginInterface { get; init; }

        public Configuration Config { get; private set; }

        private readonly WindowSystem windowSystem = new(typeof(CrystalTerrorPlugin).AssemblyQualifiedName);
        private readonly Gui.MainWindow mainWindow;
        private readonly Gui.ConfigWindow configWindow;
        private readonly ICommandManager commandManager;

        private bool disposed;

        public CrystalTerrorPlugin(IDalamudPluginInterface pluginInterface, ICommandManager commandManager)
        {
            this.PluginInterface = pluginInterface;

            this.Config = this.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

            this.commandManager = commandManager;

            this.mainWindow = new Gui.MainWindow(this);
            this.configWindow = new Gui.ConfigWindow(this);

            this.windowSystem.AddWindow(this.mainWindow);
            this.windowSystem.AddWindow(this.configWindow);

            this.PluginInterface.UiBuilder.Draw += this.DrawUi;
            this.PluginInterface.UiBuilder.OpenConfigUi += this.OpenConfigUi;
            this.PluginInterface.UiBuilder.OpenMainUi += this.OpenMainUi;

            // Register command to open/toggle main window
            this.commandManager.AddHandler("/ct", new CommandInfo(this.OnOpenCommand)
            {
                HelpMessage = "Open the CrystalTerror main window.",
            });

            this.mainWindow.IsOpen = this.Config.ShowOnStart;
        }

        public static string Name => "Crystal Terror";

        public void OpenMainUi()
            => this.mainWindow.IsOpen = true;

        public void OpenConfigUi()
            => this.configWindow.IsOpen = true;

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
                return;

            if (disposing)
            {
                this.PluginInterface.SavePluginConfig(this.Config);

                this.windowSystem.RemoveAllWindows();
                this.mainWindow.Dispose();
                this.configWindow.Dispose();

                this.PluginInterface.UiBuilder.Draw -= this.DrawUi;
                this.PluginInterface.UiBuilder.OpenConfigUi -= this.OpenConfigUi;
                this.PluginInterface.UiBuilder.OpenMainUi -= this.OpenMainUi;

                this.commandManager.RemoveHandler("/ct");
            }

            this.disposed = true;
        }

        private void DrawUi()
            => this.windowSystem.Draw();

        private void OnOpenCommand(string command, string arguments)
        {
            // Toggle the main window when the command is executed. If arguments are provided, still open the window.
            if (string.IsNullOrWhiteSpace(arguments))
                this.mainWindow.IsOpen = !this.mainWindow.IsOpen;
            else
                this.mainWindow.IsOpen = true;
        }
    }
}