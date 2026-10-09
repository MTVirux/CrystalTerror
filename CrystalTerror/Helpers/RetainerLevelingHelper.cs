using FFXIVClientStructs.FFXIV.Client.Game;

namespace CrystalTerror.Helpers;

public static class RetainerLevelingHelper
{
    public static LevelingTier? FindTier(IEnumerable<LevelingTier> tiers, int level)
        => tiers.OrderBy(t => t.MaxLevel).FirstOrDefault(t => level <= t.MaxLevel);

    // The stored level can lag one level behind right after a venture is collected.
    public static unsafe int? GetLiveLevel(string retainerName)
    {
        var mgr = RetainerManager.Instance();
        if (mgr == null)
            return null;

        var active = mgr->GetActiveRetainer();
        if (active == null || active->RetainerId == 0)
            return null;

        if (!string.Equals(active->NameString, retainerName, StringComparison.OrdinalIgnoreCase))
            return null;

        return active->Level;
    }
}
