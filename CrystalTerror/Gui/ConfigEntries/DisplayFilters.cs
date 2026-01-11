namespace CrystalTerror.Gui.ConfigEntries;

using NightmareUI.PrimaryUI;
using ImGui = Dalamud.Bindings.ImGui.ImGui;
using Dalamud.Bindings.ImGui;

public class DisplayFilters : ConfigEntry
{
    public override string Path => "Display Filters";

    public override NuiBuilder? Builder { get; init; }

    private static readonly string[] NameDisplayOptions = new[] { "Full Name", "First Name", "Last Name", "Initials" };

    public DisplayFilters()
    {
        Builder = new NuiBuilder()
            .Section("Character Display")
            .Widget("Name Display Format", (x) =>
            {
                var current = (int)Plugin.Config.NameDisplayFormat;
                ImGui.SetNextItemWidth(150f);
                if (ImGui.Combo("##nameformat", ref current, NameDisplayOptions, NameDisplayOptions.Length))
                {
                    Plugin.Config.NameDisplayFormat = (NameDisplayFormat)current;
                    ConfigHelper.Save(Plugin.Config);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Choose how character names are displayed in the main window.");
                }
            })
            .Widget("Show World Name", (x) =>
            {
                var val = Plugin.Config.ShowWorldInHeader;
                if (ImGui.Checkbox(x, ref val))
                {
                    Plugin.Config.ShowWorldInHeader = val;
                    ConfigHelper.Save(Plugin.Config);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Show the world/server name after the character name.");
                }
            })

            .Section("Character Filters")
            .Widget("Hide characters without gathering retainers", (x) =>
            {
                var val = Plugin.Config.HideNonGatheringCharacters;
                if (ImGui.Checkbox(x, ref val))
                {
                    Plugin.Config.HideNonGatheringCharacters = val;
                    ConfigHelper.Save(Plugin.Config);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Hide characters that have no gathering retainers (MIN/BTN/FSH)");
                }
            })

            .Section("Elements")
            .Widget(() =>
            {
                var val = Plugin.Config.ShowFireElement;
                ImGui.Checkbox("Fire", ref val);
                Plugin.Config.ShowFireElement = val;
                ImGui.SameLine();
                
                val = Plugin.Config.ShowIceElement;
                ImGui.Checkbox("Ice", ref val);
                Plugin.Config.ShowIceElement = val;
                ImGui.SameLine();
                
                val = Plugin.Config.ShowWindElement;
                ImGui.Checkbox("Wind", ref val);
                Plugin.Config.ShowWindElement = val;
                
                val = Plugin.Config.ShowLightningElement;
                ImGui.Checkbox("Lightning", ref val);
                Plugin.Config.ShowLightningElement = val;
                ImGui.SameLine();
                
                val = Plugin.Config.ShowEarthElement;
                ImGui.Checkbox("Earth", ref val);
                Plugin.Config.ShowEarthElement = val;
                ImGui.SameLine();
                
                val = Plugin.Config.ShowWaterElement;
                ImGui.Checkbox("Water", ref val);
                Plugin.Config.ShowWaterElement = val;
            })

            .Section("Crystal Types")
            .Widget(() =>
            {
                var val = Plugin.Config.ShowShards;
                ImGui.Checkbox("Shards", ref val);
                Plugin.Config.ShowShards = val;
                ImGui.SameLine();
                
                val = Plugin.Config.ShowCrystals;
                ImGui.Checkbox("Crystals", ref val);
                Plugin.Config.ShowCrystals = val;
                ImGui.SameLine();
                
                val = Plugin.Config.ShowClusters;
                ImGui.Checkbox("Clusters", ref val);
                Plugin.Config.ShowClusters = val;
            });
    }
}
