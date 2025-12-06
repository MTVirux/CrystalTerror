namespace CrystalTerror.Gui.ConfigEntries;

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
                if (ImGui.Button("Import Current Character"))
                {
                    var sc = CharacterHelper.ImportCurrentCharacter();
                    if (sc != null)
                    {
                        CharacterHelper.MergeInto(Plugin.Characters, new[] { sc }, CharacterHelper.MergePolicy.Skip);
                        Plugin.Config.Characters = Plugin.Characters;
                        Plugin.PluginInterface.SavePluginConfig(Plugin.Config);
                        Plugin.InvalidateSortCache();
                    }
                }
                ImGui.SameLine();
                if (ImGui.Button("Import From AutoRetainer"))
                {
                    var list = CharacterHelper.ImportFromAutoRetainer();
                    CharacterHelper.MergeInto(Plugin.Characters, list, CharacterHelper.MergePolicy.Overwrite);
                    Plugin.Config.Characters = Plugin.Characters;
                    Plugin.PluginInterface.SavePluginConfig(Plugin.Config);
                    Plugin.InvalidateSortCache();
                }
                ImGui.SameLine();
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
                        Plugin.PluginInterface.SavePluginConfig(Plugin.Config);
                        Plugin.InvalidateSortCache();
                        // Try to import the currently-logged-in character immediately after purging
                        try
                        {
                            var sc = CharacterHelper.ImportCurrentCharacter();
                            if (sc != null)
                            {
                                CharacterHelper.MergeInto(Plugin.Characters, new[] { sc }, CharacterHelper.MergePolicy.Skip);
                                Plugin.Config.Characters = Plugin.Characters;
                                Plugin.PluginInterface.SavePluginConfig(Plugin.Config);
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
            });
    }
}
