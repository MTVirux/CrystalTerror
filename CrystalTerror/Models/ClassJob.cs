namespace CrystalTerror;

/// <summary>
/// Represents a class/job with an id, display name and abbreviation.
/// </summary>
public class ClassJob
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Abbreviation { get; set; } = string.Empty;

    public ClassJob() { }

    public ClassJob(int id, string name, string abbreviation)
    {
        Id = id;
        Name = name;
        Abbreviation = abbreviation;
    }
}

/// <summary>
/// Extension methods for ClassJob lookups.
/// </summary>
public static class ClassJobExtensions
{
    private static readonly Dictionary<int, string> AbbrevMap = new()
    {
        { 1, "GLA" }, { 2, "PGL" }, { 3, "MRD" }, { 4, "LNC" }, { 5, "ARC" }, { 6, "CNJ" }, { 7, "THM" },
        { 8, "CRP" }, { 9, "BSM" }, { 10, "ARM" }, { 11, "GSM" }, { 12, "LTW" }, { 13, "WVR" }, { 14, "ALC" }, { 15, "CUL" },
        { 16, "MIN" }, { 17, "BTN" }, { 18, "FSH" }, { 19, "PLD" }, { 20, "MNK" }, { 21, "WAR" }, { 22, "DRG" }, { 23, "BRD" },
        { 24, "WHM" }, { 25, "BLM" }, { 26, "ACN" }, { 27, "SMN" }, { 28, "SCH" }, { 29, "ROG" }, { 30, "NIN" }, { 31, "MCH" },
        { 32, "DRK" }, { 33, "AST" }, { 34, "SAM" }, { 35, "RDM" }, { 36, "BLU" }, { 37, "GNB" }, { 38, "DNC" }, { 39, "RPR" },
        { 40, "SGE" }, { 41, "VPR" }, { 42, "PCT" }
    };

    private static readonly Dictionary<int, string> NameMap = new()
    {
        { 1, "Gladiator" }, { 2, "Pugilist" }, { 3, "Marauder" }, { 4, "Lancer" }, { 5, "Archer" }, { 6, "Conjurer" }, { 7, "Thaumaturge" },
        { 8, "Carpenter" }, { 9, "Blacksmith" }, { 10, "Armorer" }, { 11, "Goldsmith" }, { 12, "Leatherworker" }, { 13, "Weaver" }, { 14, "Alchemist" }, { 15, "Culinarian" },
        { 16, "Miner" }, { 17, "Botanist" }, { 18, "Fisher" }, { 19, "Paladin" }, { 20, "Monk" }, { 21, "Warrior" }, { 22, "Dragoon" }, { 23, "Bard" },
        { 24, "White Mage" }, { 25, "Black Mage" }, { 26, "Arcanist" }, { 27, "Summoner" }, { 28, "Scholar" }, { 29, "Rogue" }, { 30, "Ninja" }, { 31, "Machinist" },
        { 32, "Dark Knight" }, { 33, "Astrologian" }, { 34, "Samurai" }, { 35, "Red Mage" }, { 36, "Blue Mage" }, { 37, "Gunbreaker" }, { 38, "Dancer" }, { 39, "Reaper" },
        { 40, "Sage" }, { 41, "Viper" }, { 42, "Pictomancer" }
    };

    /// <summary>
    /// Returns a ClassJob object for the provided id (or null if id is null).
    /// </summary>
    public static ClassJob? GetClassJob(int? id)
    {
        if (id == null) return null;
        var iid = id.Value;
        var abbr = AbbrevMap.TryGetValue(iid, out var a) ? a : iid.ToString();
        var name = NameMap.TryGetValue(iid, out var n) ? n : iid.ToString();
        return new ClassJob(iid, name, abbr);
    }

    /// <summary>
    /// Helper to get abbreviation string for a job ID.
    /// </summary>
    public static string? GetAbbreviation(int? id)
    {
        var cj = GetClassJob(id);
        return cj?.Abbreviation;
    }
}
