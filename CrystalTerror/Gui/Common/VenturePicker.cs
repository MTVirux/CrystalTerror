namespace CrystalTerror.Gui.Common;

using ImGui = Dalamud.Bindings.ImGui.ImGui;

/// <summary>
/// Searchable venture list grouped by category, similar to AutoRetainer's Venture Planner.
/// </summary>
public static class VenturePicker
{
    private static readonly Dictionary<string, string> SearchFilters = new();

    private static readonly VentureListHelper.VentureCategory[] CategoryOrder =
    {
        VentureListHelper.VentureCategory.QuickExploration,
        VentureListHelper.VentureCategory.FieldExploration,
        VentureListHelper.VentureCategory.Mining,
        VentureListHelper.VentureCategory.Botany,
        VentureListHelper.VentureCategory.Fishing,
        VentureListHelper.VentureCategory.Hunting,
    };

    public static bool Draw(string id, int retainerJob, ref uint ventureId, float width = 0)
    {
        var venturesByCategory = VentureListHelper.GetVenturesForJob(retainerJob);
        var picked = false;

        var search = SearchFilters.TryGetValue(id, out var existingSearch) ? existingSearch : string.Empty;
        ImGui.SetNextItemWidth(200);
        ImGui.InputTextWithHint($"##VentureSearch_{id}", "Filter ventures...", ref search, 100);
        SearchFilters[id] = search;

        if (ImGui.BeginChild($"##VentureList_{id}", new Vector2(width, 200), true))
        {
            foreach (var category in CategoryOrder)
            {
                if (!venturesByCategory.TryGetValue(category, out var ventures) || ventures.Count == 0)
                    continue;

                var filteredVentures = string.IsNullOrEmpty(search)
                    ? ventures
                    : ventures.Where(v => v.Name.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

                if (filteredVentures.Count == 0)
                    continue;

                var categoryName = VentureListHelper.GetCategoryDisplayName(category);

                if (ImGui.CollapsingHeader($"{categoryName} ({filteredVentures.Count})##Category_{category}_{id}"))
                {
                    ImGui.Indent();
                    foreach (var venture in filteredVentures)
                    {
                        var isSelected = venture.Id == ventureId;
                        var ventureLabel = venture.Level > 0
                            ? $"[Lv{venture.Level}] {venture.Name}##Venture_{venture.Id}_{id}"
                            : $"{venture.Name}##Venture_{venture.Id}_{id}";

                        if (ImGui.Selectable(ventureLabel, isSelected))
                        {
                            ventureId = venture.Id;
                            picked = true;
                        }

                        if (ImGui.IsItemHovered())
                        {
                            var duration = venture.MaxTimeMinutes >= 60
                                ? $"{venture.MaxTimeMinutes / 60}h"
                                : $"{venture.MaxTimeMinutes}m";
                            ImGui.SetTooltip($"{venture.Name}\nID: {venture.Id}\nLevel: {venture.Level}\nDuration: {duration}");
                        }
                    }
                    ImGui.Unindent();
                }
            }
        }
        ImGui.EndChild();

        return picked;
    }
}
