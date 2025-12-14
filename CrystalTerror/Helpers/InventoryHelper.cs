using Dalamud.Game.Inventory;
using Dalamud.Game.Inventory.InventoryEventArgTypes;

namespace CrystalTerror.Helpers;

/// <summary>
/// Handles Dalamud GameInventory subscriptions and routes inventory events
/// into the plugin's existing import/merge flow.
/// </summary>
public static class InventoryHelper
{
    private static CrystalTerrorPlugin? plugin;

    public static void Initialize(CrystalTerrorPlugin owner)
    {
        plugin = owner ?? throw new ArgumentNullException(nameof(owner));
        try
        {
            if (CrystalTerrorPlugin.GameInventory != null)
            {
                CrystalTerrorPlugin.GameInventory.ItemAddedExplicit += OnInventoryItemAdded;
                CrystalTerrorPlugin.GameInventory.ItemRemovedExplicit += OnInventoryItemRemoved;
                CrystalTerrorPlugin.GameInventory.ItemChangedExplicit += OnInventoryItemChanged;
            }
        }
        catch
        {
            // swallow; plugin should remain functional without these subscriptions
        }
    }

    public static void Dispose()
    {
        try
        {
            if (CrystalTerrorPlugin.GameInventory != null)
            {
                CrystalTerrorPlugin.GameInventory.ItemAddedExplicit -= OnInventoryItemAdded;
                CrystalTerrorPlugin.GameInventory.ItemRemovedExplicit -= OnInventoryItemRemoved;
                CrystalTerrorPlugin.GameInventory.ItemChangedExplicit -= OnInventoryItemChanged;
            }
        }
        catch
        {
            // ignore
        }
        finally
        {
            plugin = null;
        }
    }

    private static void OnInventoryItemAdded(InventoryItemAddedArgs args)
    {
        try
        {
            if (args.Inventory == GameInventoryType.Crystals || args.Item.ContainerType == GameInventoryType.Crystals)
            {
                var sc = CharacterHelper.ImportCurrentCharacter();
                if (sc != null && plugin != null)
                {
                    CharacterHelper.MergeInto(plugin.Characters, new[] { sc }, CharacterHelper.MergePolicy.Overwrite);
                    ConfigHelper.SaveAndSync(plugin.Config, plugin.Characters);
                }
            }
        }
        catch
        {
            // swallow
        }
    }

    private static void OnInventoryItemRemoved(InventoryItemRemovedArgs args)
    {
        try
        {
            if (args.Inventory == GameInventoryType.Crystals || args.Item.ContainerType == GameInventoryType.Crystals)
            {
                var sc = CharacterHelper.ImportCurrentCharacter();
                if (sc != null && plugin != null)
                {
                    CharacterHelper.MergeInto(plugin.Characters, new[] { sc }, CharacterHelper.MergePolicy.Overwrite);
                    ConfigHelper.SaveAndSync(plugin.Config, plugin.Characters);
                }
            }
        }
        catch
        {
            // ignore
        }
    }

    private static void OnInventoryItemChanged(InventoryItemChangedArgs args)
    {
        try
        {
            if (args.OldItemState.ContainerType == GameInventoryType.Crystals || args.Item.ContainerType == GameInventoryType.Crystals)
            {
                var sc = CharacterHelper.ImportCurrentCharacter();
                if (sc != null && plugin != null)
                {
                    CharacterHelper.MergeInto(plugin.Characters, new[] { sc }, CharacterHelper.MergePolicy.Overwrite);
                    ConfigHelper.SaveAndSync(plugin.Config, plugin.Characters);
                }
            }
        }
        catch
        {
            // ignore
        }
    }
}
