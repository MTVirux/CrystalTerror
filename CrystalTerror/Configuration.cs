using Dalamud.Configuration;

public class Configuration : IPluginConfiguration
{
    /// <inheritdoc />
    public int Version { get; set; }

    /// <summary>
    /// If true, the main window is opened on plugin start.
    /// </summary>
    public bool ShowOnStart { get; set; } = false;
}
