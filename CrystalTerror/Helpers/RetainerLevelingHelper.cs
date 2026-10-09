using FFXIVClientStructs.FFXIV.Client.Game;

namespace CrystalTerror.Helpers;

public static class RetainerLevelingHelper
{
    public const int MaxRetainerLevel = 100;

    public readonly record struct ActiveRetainer(string Name, int Level, uint VentureId, uint VentureComplete)
    {
        public bool HasRunningVenture => VentureId != 0 && VentureComplete > DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    public static LevelingTier? FindTier(IEnumerable<LevelingTier> tiers, int level)
        => tiers.OrderBy(t => t.MaxLevel).FirstOrDefault(t => level <= t.MaxLevel);

    // The retainer whose menu is open. Its level is current, while the stored level can lag one level behind right after a venture is collected.
    public static unsafe ActiveRetainer? GetActiveRetainer()
    {
        var mgr = RetainerManager.Instance();
        if (mgr == null)
            return null;

        var active = mgr->GetActiveRetainer();
        if (active == null || active->RetainerId == 0)
            return null;

        return new ActiveRetainer(active->NameString, active->Level, active->VentureId, active->VentureComplete);
    }

    public static unsafe uint? GetListVentureId(string retainerName)
    {
        var mgr = RetainerManager.Instance();
        if (mgr == null)
            return null;

        for (uint i = 0; i < 10; i++)
        {
            var retainer = mgr->GetRetainerBySortedIndex(i);
            if (retainer == null || retainer->RetainerId == 0)
                continue;

            if (string.Equals(retainer->NameString, retainerName, StringComparison.OrdinalIgnoreCase))
                return retainer->VentureId;
        }

        return null;
    }
}
