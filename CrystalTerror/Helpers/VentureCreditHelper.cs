using FFXIVClientStructs.FFXIV.Client.Game;

namespace CrystalTerror.Helpers;

/// <summary>
/// Helper utilities for checking venture credits (Venture) currency.
/// </summary>
public static class VentureCreditHelper
{
    /// <summary>
    /// Item ID for Venture credits (the currency used for retainer ventures).
    /// </summary>
    public const uint VentureCreditItemId = 21072;

    /// <summary>
    /// Get the current venture credit count for the logged-in character.
    /// </summary>
    /// <returns>The number of venture credits, or 0 if unavailable.</returns>
    public static int GetVentureCreditCount()
    {
        try
        {
            unsafe
            {
                var invMgr = InventoryManager.Instance();
                if (invMgr == null)
                    return 0;

                // Venture credits are stored in the Currency container
                // Use GetInventoryItemCount to search across all relevant containers
                return invMgr->GetInventoryItemCount(VentureCreditItemId);
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Debug($"[VentureCreditHelper] Failed to read venture credit count: {ex.Message}");
            return 0;
        }
    }

    /// <summary>
    /// Check if the current character has enough venture credits to meet the threshold.
    /// </summary>
    /// <param name="threshold">Minimum number of venture credits required.</param>
    /// <returns>True if credits >= threshold, false otherwise.</returns>
    public static bool HasEnoughVentureCredits(int threshold)
    {
        if (threshold <= 0)
            return true;

        return GetVentureCreditCount() >= threshold;
    }
}
