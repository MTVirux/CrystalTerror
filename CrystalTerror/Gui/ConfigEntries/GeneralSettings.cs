namespace CrystalTerror.Gui.ConfigEntries;

using CrystalTerror.Helpers;
using NightmareUI.PrimaryUI;
using ImGui = Dalamud.Bindings.ImGui.ImGui;
using Dalamud.Bindings.ImGui;

public class GeneralSettings : ConfigEntry
{
    public override string Path => "General Settings";

    public override NuiBuilder Builder { get; init; } = null!;

    public GeneralSettings()
    {
        Builder = new NuiBuilder()
            .Section("Main Window")
            .Widget("Show main window on start", (x) =>
            {
                var val = Plugin.Config.ShowOnStart;
                if (ImGui.Checkbox(x, ref val))
                {
                    Plugin.Config.ShowOnStart = val;
                    ConfigHelper.Save(Plugin.Config);
                }
            })
            .Widget("Main window ignores ESC key", (x) =>
            {
                var val = Plugin.Config.IgnoreEscapeOnMainWindow;
                if (ImGui.Checkbox(x, ref val))
                {
                    Plugin.Config.IgnoreEscapeOnMainWindow = val;
                    ConfigHelper.Save(Plugin.Config);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("When enabled, pressing ESC will not close the main window");
                }
            })

            .Section("Character UI Toggles")
            .Widget("Show totals in character headers", (x) =>
            {
                var val = Plugin.Config.ShowTotalsInHeaders;
                if (ImGui.Checkbox(x, ref val))
                {
                    Plugin.Config.ShowTotalsInHeaders = val;
                    ConfigHelper.Save(Plugin.Config);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Display crystal/shard/cluster totals in character headers");
                }
            })

            .If(() => Plugin.Config.ShowTotalsInHeaders)
            .Indent()
            
            .Widget("Use reduced notation in headers", (x) =>
            {
                var val = Plugin.Config.UseReducedNotationInHeaders;
                if (ImGui.Checkbox(x, ref val))
                {
                    Plugin.Config.UseReducedNotationInHeaders = val;
                    ConfigHelper.Save(Plugin.Config);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Display numbers in headers as 22k, 1.5M, etc. instead of full numbers");
                }
            })

            .Widget("Show element names in totals", (x) =>
            {
                var val = Plugin.Config.ShowElementNamesInTotals;
                if (ImGui.Checkbox(x, ref val))
                {
                    Plugin.Config.ShowElementNamesInTotals = val;
                    ConfigHelper.Save(Plugin.Config);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Show element names before totals (e.g., 'Fire: 100/200/300' vs '100/200/300')");
                }
            })

            .If(() => Plugin.Config.ShowElementNamesInTotals)
            .Indent()
            .Widget("Use abbreviated element names", (x) =>
            {
                var val = Plugin.Config.UseAbbreviatedElementNames;
                if (ImGui.Checkbox(x, ref val))
                {
                    Plugin.Config.UseAbbreviatedElementNames = val;
                    ConfigHelper.Save(Plugin.Config);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Use first 2 characters only (e.g., 'Fi' instead of 'Fire')");
                }
            })
            .Unindent()
            .EndIf()

            
            .Unindent()
            .EndIf()

            .Widget("Color current character header", (x) =>
            {
                var val = Plugin.Config.ColorCurrentCharacter;
                if (ImGui.Checkbox(x, ref val))
                {
                    Plugin.Config.ColorCurrentCharacter = val;
                    ConfigHelper.Save(Plugin.Config);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Apply a custom color to the currently logged in character's header");
                }
            })

            .If(() => Plugin.Config.ColorCurrentCharacter)
            .Indent()
            .Widget("Current character color", (x) =>
            {
                var color = Plugin.Config.CurrentCharacterColor;
                if (ImGui.ColorEdit4(x, ref color, ImGuiColorEditFlags.NoInputs))
                {
                    Plugin.Config.CurrentCharacterColor = color;
                    ConfigHelper.Save(Plugin.Config);
                }
            })
            .Unindent()
            .EndIf()

            .Section("Table UI Toggles")
            .Widget("Use reduced notation in tables", (x) =>
            {
                var val = Plugin.Config.UseReducedNotationInTables;
                if (ImGui.Checkbox(x, ref val))
                {
                    Plugin.Config.UseReducedNotationInTables = val;
                    ConfigHelper.Save(Plugin.Config);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Display numbers in table cells as 22k, 1.5M, etc. instead of full numbers");
                }
            })

            .Section("Visibility")
            .Widget("Hide characters with no gathering retainers", (x) =>
            {
                var val = Plugin.Config.HideNonGatheringCharacters;
                if (ImGui.Checkbox(x, ref val))
                {
                    Plugin.Config.HideNonGatheringCharacters = val;
                    ConfigHelper.Save(Plugin.Config);
                }
            });
    }
}
