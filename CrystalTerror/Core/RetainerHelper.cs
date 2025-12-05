using System;
using System.Collections.Generic;

namespace CrystalTerror
{
    /// <summary>
    /// Utilities for creating and repairing Retainer objects and owner relationships.
    /// </summary>
    public static class RetainerHelper
    {
        public static Retainer Create(StoredCharacter owner, string name, ulong atid, int? job, int level, int ilvl)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));

            var r = new Retainer(owner)
            {
                Name = name ?? string.Empty,
                atid = atid,
                Job = job,
                Level = level,
                Ilvl = ilvl,
                Inventory = new Inventory()
            };

            return r;
        }

        public static Retainer CreateFromAutoRetainer(StoredCharacter owner, string? name, ulong atid, int? job, int level, int ilvl)
        {
            return Create(owner, name ?? string.Empty, atid, job, level, ilvl);
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
}
