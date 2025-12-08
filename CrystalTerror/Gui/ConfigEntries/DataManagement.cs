namespace CrystalTerror.Gui.ConfigEntries;

using CrystalTerror.Helpers;
using NightmareUI.PrimaryUI;
using ImGui = Dalamud.Bindings.ImGui.ImGui;
using Dalamud.Bindings.ImGui;

public class DataManagement : ConfigEntry
{
    public override string Path => "Data Management";

    public override NuiBuilder Builder { get; init; } = null!;

    public DataManagement()
    {
        Builder = new NuiBuilder()
            .Section("Data Management")
            .Widget(() =>
            {
                if (ImGui.Button("Force Import Current Character"))
                {
                    var sc = CharacterHelper.ImportCurrentCharacter();
                    if (sc != null)
                    {
                        CharacterHelper.MergeInto(Plugin.Characters, new[] { sc }, CharacterHelper.MergePolicy.Merge);
                        ConfigHelper.SaveAndSync(Plugin.Config, Plugin.Characters);
                        Plugin.InvalidateSortCache();
                    }
                }

                if (ImGui.Button("Import From AutoRetainer"))
                {
                    var list = CharacterHelper.ImportFromAutoRetainer();
                    CharacterHelper.MergeInto(Plugin.Characters, list, CharacterHelper.MergePolicy.Skip);
                    ConfigHelper.SaveAndSync(Plugin.Config, Plugin.Characters);
                    Plugin.InvalidateSortCache();
                    // After importing from AutoRetainer, also import the currently-logged-in character
                    try
                    {
                        var sc = CharacterHelper.ImportCurrentCharacter();
                        if (sc != null)
                        {
                            // Overwrite the current character entry with live data
                            CharacterHelper.MergeInto(Plugin.Characters, new[] { sc }, CharacterHelper.MergePolicy.Overwrite);
                            ConfigHelper.SaveAndSync(Plugin.Config, Plugin.Characters);
                            Plugin.InvalidateSortCache();
                        }
                    }
                    catch
                    {
                        // ignore any errors while attempting to import current character
                    }
                }


                if (ImGui.Button("Purge Characters"))
                {
                    ImGui.OpenPopup("PurgeConfirm");
                }

                if (ImGui.BeginPopupModal("PurgeConfirm", ImGuiWindowFlags.AlwaysAutoResize))
                {
                    ImGui.TextWrapped("Are you sure you want to permanently purge all imported character data? This cannot be undone.");
                    ImGui.Spacing();
                    if (ImGui.Button("Yes, Purge"))
                    {
                        Plugin.Characters.Clear();
                        Plugin.Config.Characters.Clear();
                        // Save cleared config
                        ConfigHelper.SaveAndSync(Plugin.Config, Plugin.Characters);
                        Plugin.InvalidateSortCache();
                        // Try to import the currently-logged-in character immediately after purging
                        try
                        {
                            var sc = CharacterHelper.ImportCurrentCharacter();
                            if (sc != null)
                            {
                                CharacterHelper.MergeInto(Plugin.Characters, new[] { sc }, CharacterHelper.MergePolicy.Merge);
                                ConfigHelper.SaveAndSync(Plugin.Config, Plugin.Characters);
                                Plugin.InvalidateSortCache();
                            }
                        }
                        catch
                        {
                            // ignore errors during import
                        }
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("Cancel"))
                    {
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndPopup();
                }

                if (ImGui.Button("Clear Inventories"))
                {
                    ImGui.OpenPopup("ClearInventoriesConfirm");
                }

                if (ImGui.BeginPopupModal("ClearInventoriesConfirm", ImGuiWindowFlags.AlwaysAutoResize))
                {
                    ImGui.TextWrapped("Are you sure you want to clear all crystal inventories? This will reset all crystal counts to zero for all characters and retainers. This cannot be undone.");
                    ImGui.Spacing();
                    if (ImGui.Button("Yes, Clear All Inventories"))
                    {
                        foreach (var character in Plugin.Characters)
                        {
                            character.Inventory.Reset();
                            foreach (var retainer in character.Retainers)
                            {
                                retainer.Inventory.Reset();
                            }
                        }
                        ConfigHelper.SaveAndSync(Plugin.Config, Plugin.Characters);
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("Cancel"))
                    {
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndPopup();
                }
            });
    }
}
