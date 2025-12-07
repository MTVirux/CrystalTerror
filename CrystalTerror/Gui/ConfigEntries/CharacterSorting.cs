namespace CrystalTerror.Gui.ConfigEntries;

using CrystalTerror.Helpers;
using NightmareUI.PrimaryUI;
using ImGui = Dalamud.Bindings.ImGui.ImGui;
using Dalamud.Bindings.ImGui;

public class CharacterSorting : ConfigEntry
{
    public override string Path => "Character Sorting";

    public override NuiBuilder Builder { get; init; } = null!;

    public CharacterSorting()
    {
        Builder = new NuiBuilder()
            .Section("Sort Order")
            .TextWrapped("Change how characters are sorted in the main window.")
            .Widget(() =>
            {
                var sortOptions = new string[]
                {
                    "Alphabetical (A-Z)",
                    "Reverse Alphabetical (Z-A)",
                    "World (A-Z)",
                    "Reverse World (Z-A)",
                    "AutoRetainer Order",
                    "Custom Order"
                };
                
                var currentSort = (int)Plugin.Config.CharacterSortOption;
                ImGui.SetNextItemWidth(300);
                if (ImGui.Combo("##SortOrder", ref currentSort, sortOptions, sortOptions.Length))
                {
                    Plugin.Config.CharacterSortOption = (CharacterSortOptions)currentSort;
                    
                    // Exit edit mode if switching away from Custom
                    if (Plugin.Config.CharacterSortOption != CharacterSortOptions.Custom)
                    {
                        Plugin.Config.IsEditMode = false;
                    }
                    
                    // Invalidate cache since sort option changed
                    Plugin.InvalidateSortCache();
                    ConfigHelper.Save(Plugin.Config);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Change how characters are sorted in the main window.");
                }
            })

            .If(() => Plugin.Config.CharacterSortOption == CharacterSortOptions.Custom)
            .Widget(() =>
            {
                var editButtonText = Plugin.Config.IsEditMode ? "Exit Edit Mode" : "Edit Order";
                if (ImGui.Button(editButtonText))
                {
                    Plugin.Config.IsEditMode = !Plugin.Config.IsEditMode;
                    ConfigHelper.Save(Plugin.Config);

                    // Open main window when entering edit mode
                    if (Plugin.Config.IsEditMode)
                    {
                        Plugin.OpenMainUi();
                    }
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(Plugin.Config.IsEditMode
                        ? "Exit edit mode to return to normal view."
                        : "Enter edit mode to reorder characters with up/down arrows.");
                }
            })
            .EndIf()

            .Section("Display Options")
            .Widget(() =>
            {
                bool showCurrentAtTop = Plugin.Config.ShowCurrentCharacterAtTop || Plugin.Config.CharacterSortOption == CharacterSortOptions.AutoRetainer;
                bool isAutoRetainerSort = Plugin.Config.CharacterSortOption == CharacterSortOptions.AutoRetainer;

                if (isAutoRetainerSort)
                    ImGui.BeginDisabled();

                var label = isAutoRetainerSort 
                    ? "Show current character at top (Enforced by AutoRetainer sort)" 
                    : "Show current character at top";

                if (ImGui.Checkbox(label, ref showCurrentAtTop))
                {
                    Plugin.Config.ShowCurrentCharacterAtTop = showCurrentAtTop;
                    ConfigHelper.Save(Plugin.Config);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Show the currently logged in character at the top of the list");
                }

                if (isAutoRetainerSort)
                    ImGui.EndDisabled();
            });
    }
}
