using Dalamud.Game.Inventory;
using Dalamud.Game.Inventory.InventoryEventArgTypes;

namespace CrystalTerror.Helpers;

/// <summary>
/// Handles Dalamud GameInventory subscriptions and routes inventory events
/// into the plugin's existing import/merge flow.
/// Uses ECommons for reliable player state checking.
/// </summary>
public static class InventoryHelper
{
    private static CrystalTerrorPlugin? plugin;
    private static DateTime lastUpdate = DateTime.MinValue;
    private const double UpdateThrottleMs = 500; // Throttle updates to prevent excessive saves

    public static void Initialize(CrystalTerrorPlugin owner)
    {
        plugin = owner ?? throw new ArgumentNullException(nameof(owner));
        try
        {
            if (Svc.GameInventory != null)
            {
                Svc.GameInventory.ItemAddedExplicit += OnInventoryItemAdded;
                Svc.GameInventory.ItemRemovedExplicit += OnInventoryItemRemoved;
                Svc.GameInventory.ItemChangedExplicit += OnInventoryItemChanged;
                Svc.Log.Debug("[CrystalTerror] InventoryHelper initialized");
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Debug($"[CrystalTerror] InventoryHelper initialization failed: {ex.Message}");
        }
    }

    public static void Dispose()
    {
        try
        {
            if (Svc.GameInventory != null)
            {
                Svc.GameInventory.ItemAddedExplicit -= OnInventoryItemAdded;
                Svc.GameInventory.ItemRemovedExplicit -= OnInventoryItemRemoved;
                Svc.GameInventory.ItemChangedExplicit -= OnInventoryItemChanged;
            }
        }
        catch { }
        finally
        {
            plugin = null;
        }
    }

    private static void OnInventoryItemAdded(InventoryItemAddedArgs args)
    {
        HandleCrystalChange(args.Inventory, args.Item.ContainerType);
    }

    private static void OnInventoryItemRemoved(InventoryItemRemovedArgs args)
    {
        HandleCrystalChange(args.Inventory, args.Item.ContainerType);
    }

    private static void OnInventoryItemChanged(InventoryItemChangedArgs args)
    {
        HandleCrystalChange(args.OldItemState.ContainerType, args.Item.ContainerType);
    }

    private static void HandleCrystalChange(GameInventoryType inventory1, GameInventoryType inventory2)
    {
        try
        {
            // Only process crystal inventory changes
            if (inventory1 != GameInventoryType.Crystals && inventory2 != GameInventoryType.Crystals)
                return;

            // Throttle updates to prevent excessive saves
            var now = DateTime.UtcNow;
            if ((now - lastUpdate).TotalMilliseconds < UpdateThrottleMs)
                return;

            // Ensure player is available
            if (!Player.Available || Player.CID == 0 || plugin == null)
                return;

            lastUpdate = now;

            var sc = CharacterHelper.ImportCurrentCharacter();
            if (sc != null)
            {
                CharacterHelper.MergeInto(plugin.Characters, new[] { sc }, CharacterHelper.MergePolicy.Overwrite);
                ConfigHelper.SaveAndSync(plugin.Config, plugin.Characters);
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Debug($"[CrystalTerror] HandleCrystalChange error: {ex.Message}");
        }
    }
}
