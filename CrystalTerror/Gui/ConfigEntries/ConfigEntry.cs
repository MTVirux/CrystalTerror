namespace CrystalTerror.Gui.ConfigEntries;

using NightmareUI.OtterGuiWrapper.FileSystems.Configuration;
using NightmareUI.PrimaryUI;

public abstract class ConfigEntry : ConfigFileSystemEntry
{
    protected CrystalTerrorPlugin Plugin => CrystalTerrorPlugin.Instance;
    protected Configuration Config => Plugin.Config;
}
