using CrystalTerror.Services;
using Lumina.Excel.Sheets;

namespace CrystalTerror.Helpers;

/// <summary>
/// Helper for loading venture data from game Excel sheets.
/// </summary>
public static class VentureListHelper
{
    private static List<VentureInfo>? _cachedVentures;
    private static readonly object _cacheLock = new();

    /// <summary>
    /// Venture categories matching AutoRetainer's structure.
    /// </summary>
    public enum VentureCategory
    {
        QuickExploration,
        FieldExploration,  // 18-hour explorations
        Hunting,           // 1-hour item gathering (combat)
        Mining,            // 1-hour item gathering (MIN)
        Botany,            // 1-hour item gathering (BTN)
        Fishing,           // 1-hour item gathering (FSH)
    }

    // ClassJobCategory IDs from AutoRetainer
    private const int CategoryMIN = 17;
    private const int CategoryBTN = 18;
    private const int CategoryFSH = 19;
    private const int CategoryDoW = 34;

    /// <summary>
    /// Represents information about a venture task.
    /// </summary>
    public record VentureInfo(
        uint Id, 
        string Name, 
        int Level, 
        int MaxTimeMinutes, 
        bool IsRandom,
        VentureCategory Category,
        int ClassJobCategoryId);

    /// <summary>
    /// Get all available ventures from the RetainerTask Excel sheet.
    /// Results are cached for performance.
    /// </summary>
    public static List<VentureInfo> GetAllVentures()
    {
        lock (_cacheLock)
        {
            if (_cachedVentures != null)
                return _cachedVentures;

            _cachedVentures = LoadVenturesFromSheet();
            return _cachedVentures;
        }
    }

    /// <summary>
    /// Clear the cached venture list (call if game data might have changed).
    /// </summary>
    public static void ClearCache()
    {
        lock (_cacheLock)
        {
            _cachedVentures = null;
        }
    }

    /// <summary>
    /// Get the name of a venture by its ID.
    /// </summary>
    public static string GetVentureName(uint ventureId)
    {
        var ventures = GetAllVentures();
        var venture = ventures.FirstOrDefault(v => v.Id == ventureId);
        return venture?.Name ?? $"Unknown ({ventureId})";
    }

    /// <summary>
    /// Get ventures grouped by category for display in UI.
    /// </summary>
    public static Dictionary<VentureCategory, List<VentureInfo>> GetVenturesByCategory()
    {
        var ventures = GetAllVentures();
        return ventures
            .Where(v => !string.IsNullOrEmpty(v.Name))
            .GroupBy(v => v.Category)
            .ToDictionary(g => g.Key, g => g.OrderBy(v => v.Level).ThenBy(v => v.Name).ToList());
    }

    /// <summary>
    /// Get the display name for a venture category.
    /// </summary>
    public static string GetCategoryDisplayName(VentureCategory category)
    {
        return category switch
        {
            VentureCategory.QuickExploration => "Quick Exploration",
            VentureCategory.FieldExploration => "Field Exploration (18h)",
            VentureCategory.Hunting => "Hunting (Combat)",
            VentureCategory.Mining => "Mining (MIN)",
            VentureCategory.Botany => "Botany (BTN)",
            VentureCategory.Fishing => "Fishing (FSH)",
            _ => category.ToString()
        };
    }

    private static VentureCategory DetermineCategory(RetainerTask task, bool isRandom)
    {
        // Quick Exploration is ID 395
        if (task.RowId == 395)
            return VentureCategory.QuickExploration;

        // Field explorations are 18-hour ventures (1080 minutes)
        if (task.MaxTimemin == 1080)
            return VentureCategory.FieldExploration;

        // 1-hour ventures (60 minutes) are categorized by ClassJobCategory
        if (task.MaxTimemin == 60)
        {
            var categoryId = (int)task.ClassJobCategory.RowId;
            return categoryId switch
            {
                CategoryMIN => VentureCategory.Mining,
                CategoryBTN => VentureCategory.Botany,
                CategoryFSH => VentureCategory.Fishing,
                CategoryDoW => VentureCategory.Hunting,
                _ => VentureCategory.Hunting // Default to hunting for unknown
            };
        }

        // Random ventures that aren't Quick Exploration go to Field Exploration
        if (isRandom)
            return VentureCategory.FieldExploration;

        // Default to Hunting for anything else
        return VentureCategory.Hunting;
    }

    private static List<VentureInfo> LoadVenturesFromSheet()
    {
        var ventures = new List<VentureInfo>();

        try
        {
            var retainerTaskSheet = DataService.Manager.GetExcelSheet<RetainerTask>();
            if (retainerTaskSheet == null)
                return ventures;

            var retainerTaskRandomSheet = DataService.Manager.GetExcelSheet<RetainerTaskRandom>();
            var retainerTaskNormalSheet = DataService.Manager.GetExcelSheet<RetainerTaskNormal>();

            foreach (var task in retainerTaskSheet)
            {
                if (task.RowId == 0)
                    continue;

                string name;
                bool isRandom = task.IsRandom;

                if (isRandom)
                {
                    // Random venture (Quick Exploration, Field Exploration, etc.)
                    var randomTask = retainerTaskRandomSheet?.GetRowOrDefault(task.Task.RowId);
                    name = randomTask?.Name.ExtractText() ?? string.Empty;
                }
                else
                {
                    // Normal venture (hunting/gathering specific items)
                    var normalTask = retainerTaskNormalSheet?.GetRowOrDefault(task.Task.RowId);
                    var item = normalTask?.Item.ValueNullable;
                    name = item?.Name.ExtractText() ?? string.Empty;
                }

                if (!string.IsNullOrEmpty(name))
                {
                    var category = DetermineCategory(task, isRandom);
                    ventures.Add(new VentureInfo(
                        task.RowId,
                        name,
                        task.RetainerLevel,
                        task.MaxTimemin,
                        isRandom,
                        category,
                        (int)task.ClassJobCategory.RowId
                    ));
                }
            }
        }
        catch (Exception ex)
        {
            LogService.Log?.Warning($"[VentureListHelper] Failed to load ventures: {ex.Message}");
        }

        return ventures;
    }
}

