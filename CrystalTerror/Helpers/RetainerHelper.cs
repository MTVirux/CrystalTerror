namespace CrystalTerror.Helpers;

/// <summary>
/// Utilities for creating and repairing Retainer objects and owner relationships.
/// </summary>
public static class RetainerHelper
{
    public static Retainer Create(StoredCharacter owner, string name, ulong retainerId, int? job, int level, int ilvl, int gathering = 0, int perception = 0)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));

        var r = new Retainer(owner)
        {
            Name = name ?? string.Empty,
            Atid = retainerId,
            Job = job,
            Level = level,
            Ilvl = ilvl,
            Gathering = gathering,
            Perception = perception,
            Inventory = new Inventory()
        };

        return r;
    }

        public static Retainer CreateFromAutoRetainer(StoredCharacter owner, string? name, ulong retainerId, int? job, int level, int ilvl, int gathering = 0, int perception = 0)
        {
            return Create(owner, name ?? string.Empty, retainerId, job, level, ilvl, gathering, perception);
        }

        public static void SetOwnerForRetainers(StoredCharacter owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (owner.Retainers == null) return;

            foreach (var r in owner.Retainers)
            {
                try { r.OwnerCharacter = owner; } catch { }
            }
        }

        public static void RepairOwnerReferences(IEnumerable<StoredCharacter> characters)
        {
            if (characters == null) return;
            foreach (var sc in characters)
            {
                if (sc?.Retainers == null) continue;
                foreach (var r in sc.Retainers)
                {
                    try { r.OwnerCharacter = sc; } catch { }
                }
            }
        }
    }
