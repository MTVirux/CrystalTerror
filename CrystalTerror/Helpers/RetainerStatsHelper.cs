using System;
using System.Linq;
using Lumina.Excel.Sheets;
using FFXIVClientStructs.FFXIV.Client.Game;
using Dalamud.Plugin.Services;

namespace CrystalTerror
{
    /// <summary>
    /// Helper utilities for calculating retainer stats from their equipped items.
    /// Based on AutoRetainer's ItemLevel.Calculate logic.
    /// </summary>
    public static class RetainerStatsHelper
    {
        // Item UI categories that can have an offhand
        private static readonly uint[] CanHaveOffhand = [2, 6, 8, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32];
        
        // Item UI categories to ignore (waist slot)
        private static readonly uint[] IgnoreCategory = [105];

        // BaseParam IDs for Gathering and Perception
        private const uint GatheringParamId = 72;
        private const uint PerceptionParamId = 73;

        /// <summary>
        /// Calculate retainer stats (item level, gathering, perception) from their currently equipped gear.
        /// Returns null if retainer inventory is not accessible.
        /// </summary>
        public static unsafe (int? ItemLevel, int Gathering, int Perception) CalculateRetainerStats()
        {
            int gathering = 0;
            int perception = 0;

            var container = InventoryManager.Instance()->GetInventoryContainer(InventoryType.RetainerEquippedItems);
            if (container == null) return (null, 0, 0);

            var sheet = Services.DataService.Manager.GetExcelSheet<Item>();
            if (sheet == null) return (null, 0, 0);

            uint sum = 0;
            int count = 12; // Default to 12 gear slots (13 slots minus waist)

            for (int i = 0; i < 13; i++)
            {
                if (i == 5) continue; // Skip waist slot

                var slot = container->GetInventorySlot(i);
                if (slot == null) continue;

                uint itemId = slot->ItemId;
                if (itemId == 0) continue;

                var itemRow = sheet.GetRowOrDefault(itemId);
                if (itemRow == null || !itemRow.HasValue) continue;

                var item = itemRow.Value;

                // Check if this item should be ignored (waist)
                bool shouldIgnore = false;
                foreach (var cat in IgnoreCategory)
                {
                    if (item.ItemUICategory.RowId == cat)
                    {
                        shouldIgnore = true;
                        break;
                    }
                }

                if (shouldIgnore)
                {
                    if (i == 0) count -= 1; // If main hand is ignored, also reduce count
                    count -= 1;
                    continue;
                }

                // If main hand doesn't support offhand, count both slots as one item
                if (i == 0)
                {
                    bool canHaveOH = false;
                    foreach (var cat in CanHaveOffhand)
                    {
                        if (item.ItemUICategory.RowId == cat)
                        {
                        canHaveOH = true;
                            break;
                        }
                    }

                    if (!canHaveOH)
                    {
                        sum += item.LevelItem.RowId;
                        i++; // Skip offhand slot
                    }
                }

                // Read gathering and perception stats from the item's BaseParam arrays
                gathering += GetItemStat(item, GatheringParamId);
                perception += GetItemStat(item, PerceptionParamId);

                sum += item.LevelItem.RowId;
            }

            int? avgItemLevel = count > 0 ? (int)(sum / count) : null;
            return (avgItemLevel, gathering, perception);
        }

        private static int GetItemStat(Item item, uint paramId)
        {
            int value = 0;
            for (int i = 0; i < item.BaseParam.Count; i++)
            {
                if (item.BaseParam[i].RowId == paramId)
                {
                    value += item.BaseParamValue[i];
                }
            }
            return value;
        }

        /// <summary>
        /// Update retainer stats if at summoning bell and throttle is met.
        /// </summary>
        public static unsafe void UpdateRetainerStatsIfNeeded(
            System.Collections.Generic.List<StoredCharacter> characters,
            ref DateTime lastStatsUpdate,
            double throttleSeconds)
        {
            try
            {
                // Only update if we're at a summoning bell
                if (!Services.GameStateService.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.OccupiedSummoningBell])
                    return;

                // Throttle updates
                if ((DateTime.UtcNow - lastStatsUpdate).TotalSeconds < throttleSeconds)
                    return;

                lastStatsUpdate = DateTime.UtcNow;

                // Calculate stats for current retainer
                var (itemLevel, gathering, perception) = CalculateRetainerStats();
                if (itemLevel == null)
                    return;

                // Find the currently selected retainer and update their stats
                var retainerMgr = FFXIVClientStructs.FFXIV.Client.Game.RetainerManager.Instance();
                if (retainerMgr == null || !retainerMgr->IsReady)
                    return;

                var activeRetainer = retainerMgr->GetActiveRetainer();
                if (activeRetainer == null || activeRetainer->RetainerId == 0)
                    return;

                // Find this retainer in our stored characters
                var contentId = Services.PlayerService.State.ContentId;
                var currentChar = characters.FirstOrDefault(c => c.Name == Services.PlayerService.Objects.LocalPlayer?.Name.TextValue && contentId != 0);
                if (currentChar == null)
                    return;

                var retainer = currentChar.Retainers.FirstOrDefault(r => r.atid == activeRetainer->RetainerId);
                if (retainer == null)
                    return;

                // Update stats
                retainer.Ilvl = itemLevel.Value;
                retainer.Gathering = gathering;
                retainer.Perception = perception;

                Services.LogService.Log.Debug($"Updated stats for retainer {retainer.Name}: Ilvl={itemLevel}, Gathering={gathering}, Perception={perception}");
            }
            catch (Exception ex)
            {
                Services.LogService.Log.Warning($"Error updating retainer stats: {ex.Message}");
            }
        }

    }
}
