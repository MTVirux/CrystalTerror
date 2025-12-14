namespace CrystalTerror;

/// <summary>
/// Explicit inventory structure: one property for each Element x CrystalType combination.
/// Properties are named `{Element}_{CrystalType}`, e.g. `Fire_Shard`.
/// Includes helpers to get/set by enum and to export as dictionary.
/// </summary>
[Serializable]
public class Inventory
{
        // Fire
        public long Fire_Shard { get; set; }
        public long Fire_Crystal { get; set; }
        public long Fire_Cluster { get; set; }

        // Ice
        public long Ice_Shard { get; set; }
        public long Ice_Crystal { get; set; }
        public long Ice_Cluster { get; set; }

        // Wind
        public long Wind_Shard { get; set; }
        public long Wind_Crystal { get; set; }
        public long Wind_Cluster { get; set; }

        // Earth
        public long Earth_Shard { get; set; }
        public long Earth_Crystal { get; set; }
        public long Earth_Cluster { get; set; }

        // Lightning
        public long Lightning_Shard { get; set; }
        public long Lightning_Crystal { get; set; }
        public long Lightning_Cluster { get; set; }

        // Water
        public long Water_Shard { get; set; }
        public long Water_Crystal { get; set; }
        public long Water_Cluster { get; set; }

        public Inventory()
        {
            // defaults are zero; explicit constructor kept for clarity
        }

        public void Reset()
        {
            Fire_Shard = Fire_Crystal = Fire_Cluster = 0;
            Ice_Shard = Ice_Crystal = Ice_Cluster = 0;
            Wind_Shard = Wind_Crystal = Wind_Cluster = 0;
            Earth_Shard = Earth_Crystal = Earth_Cluster = 0;
            Lightning_Shard = Lightning_Crystal = Lightning_Cluster = 0;
            Water_Shard = Water_Crystal = Water_Cluster = 0;
        }

        public long GetCount(Element element, CrystalType type)
        {
            return type switch
            {
                CrystalType.Shard => element switch
                {
                    Element.Fire => Fire_Shard,
                    Element.Ice => Ice_Shard,
                    Element.Wind => Wind_Shard,
                    Element.Earth => Earth_Shard,
                    Element.Lightning => Lightning_Shard,
                    Element.Water => Water_Shard,
                    _ => 0
                },
                CrystalType.Crystal => element switch
                {
                    Element.Fire => Fire_Crystal,
                    Element.Ice => Ice_Crystal,
                    Element.Wind => Wind_Crystal,
                    Element.Earth => Earth_Crystal,
                    Element.Lightning => Lightning_Crystal,
                    Element.Water => Water_Crystal,
                    _ => 0
                },
                CrystalType.Cluster => element switch
                {
                    Element.Fire => Fire_Cluster,
                    Element.Ice => Ice_Cluster,
                    Element.Wind => Wind_Cluster,
                    Element.Earth => Earth_Cluster,
                    Element.Lightning => Lightning_Cluster,
                    Element.Water => Water_Cluster,
                    _ => 0
                },
                _ => 0
            };
        }

        public void SetCount(Element element, CrystalType type, long value)
        {
            switch (type)
            {
                case CrystalType.Shard:
                    switch (element)
                    {
                        case Element.Fire: Fire_Shard = value; break;
                        case Element.Ice: Ice_Shard = value; break;
                        case Element.Wind: Wind_Shard = value; break;
                        case Element.Earth: Earth_Shard = value; break;
                        case Element.Lightning: Lightning_Shard = value; break;
                        case Element.Water: Water_Shard = value; break;
                    }
                    break;
                case CrystalType.Crystal:
                    switch (element)
                    {
                        case Element.Fire: Fire_Crystal = value; break;
                        case Element.Ice: Ice_Crystal = value; break;
                        case Element.Wind: Wind_Crystal = value; break;
                        case Element.Earth: Earth_Crystal = value; break;
                        case Element.Lightning: Lightning_Crystal = value; break;
                        case Element.Water: Water_Crystal = value; break;
                    }
                    break;
                case CrystalType.Cluster:
                    switch (element)
                    {
                        case Element.Fire: Fire_Cluster = value; break;
                        case Element.Ice: Ice_Cluster = value; break;
                        case Element.Wind: Wind_Cluster = value; break;
                        case Element.Earth: Earth_Cluster = value; break;
                        case Element.Lightning: Lightning_Cluster = value; break;
                        case Element.Water: Water_Cluster = value; break;
                    }
                    break;
            }
        }

        public IDictionary<string, long> ToDictionary()
        {
            return new Dictionary<string, long>
            {
                { nameof(Fire_Shard), Fire_Shard },
                { nameof(Fire_Crystal), Fire_Crystal },
                { nameof(Fire_Cluster), Fire_Cluster },

                { nameof(Ice_Shard), Ice_Shard },
                { nameof(Ice_Crystal), Ice_Crystal },
                { nameof(Ice_Cluster), Ice_Cluster },

                { nameof(Wind_Shard), Wind_Shard },
                { nameof(Wind_Crystal), Wind_Crystal },
                { nameof(Wind_Cluster), Wind_Cluster },

                { nameof(Earth_Shard), Earth_Shard },
                { nameof(Earth_Crystal), Earth_Crystal },
                { nameof(Earth_Cluster), Earth_Cluster },

                { nameof(Lightning_Shard), Lightning_Shard },
                { nameof(Lightning_Crystal), Lightning_Crystal },
                { nameof(Lightning_Cluster), Lightning_Cluster },

                { nameof(Water_Shard), Water_Shard },
                { nameof(Water_Crystal), Water_Crystal },
                { nameof(Water_Cluster), Water_Cluster }
            };
        }

        /// <summary>
        /// Calculates the total count of all crystals in this inventory.
        /// </summary>
        public long Total()
        {
            return Fire_Shard + Fire_Crystal + Fire_Cluster +
                   Ice_Shard + Ice_Crystal + Ice_Cluster +
                   Wind_Shard + Wind_Crystal + Wind_Cluster +
                   Earth_Shard + Earth_Crystal + Earth_Cluster +
                   Lightning_Shard + Lightning_Crystal + Lightning_Cluster +
                   Water_Shard + Water_Crystal + Water_Cluster;
        }
}
