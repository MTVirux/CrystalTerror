using System;
using System.Collections.Generic;

namespace CrystalTerror
{
    [Serializable]
    /// <summary>
    /// Explicit inventory structure: one property for each Element x CrystalType combination.
    /// Properties are named `{Element}_{CrystalType}`, e.g. `Fire_Shard`.
    /// Includes helpers to get/set by enum and to export as dictionary.
    /// </summary>
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

        // Lightning
        public long Lightning_Shard { get; set; }
        public long Lightning_Crystal { get; set; }
        public long Lightning_Cluster { get; set; }

        // Earth
        public long Earth_Shard { get; set; }
        public long Earth_Crystal { get; set; }
        public long Earth_Cluster { get; set; }

        // Water
        public long Water_Shard { get; set; }
        public long Water_Crystal { get; set; }
        public long Water_Cluster { get; set; }

        // Light
        public long Light_Shard { get; set; }
        public long Light_Crystal { get; set; }
        public long Light_Cluster { get; set; }

        // Dark
        public long Dark_Shard { get; set; }
        public long Dark_Crystal { get; set; }
        public long Dark_Cluster { get; set; }

        public Inventory()
        {
            // defaults are zero; explicit constructor kept for clarity
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
                    Element.Lightning => Lightning_Shard,
                    Element.Earth => Earth_Shard,
                    Element.Water => Water_Shard,
                    Element.Light => Light_Shard,
                    Element.Dark => Dark_Shard,
                    _ => 0
                },
                CrystalType.Crystal => element switch
                {
                    Element.Fire => Fire_Crystal,
                    Element.Ice => Ice_Crystal,
                    Element.Wind => Wind_Crystal,
                    Element.Lightning => Lightning_Crystal,
                    Element.Earth => Earth_Crystal,
                    Element.Water => Water_Crystal,
                    Element.Light => Light_Crystal,
                    Element.Dark => Dark_Crystal,
                    _ => 0
                },
                CrystalType.Cluster => element switch
                {
                    Element.Fire => Fire_Cluster,
                    Element.Ice => Ice_Cluster,
                    Element.Wind => Wind_Cluster,
                    Element.Lightning => Lightning_Cluster,
                    Element.Earth => Earth_Cluster,
                    Element.Water => Water_Cluster,
                    Element.Light => Light_Cluster,
                    Element.Dark => Dark_Cluster,
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
                        case Element.Lightning: Lightning_Shard = value; break;
                        case Element.Earth: Earth_Shard = value; break;
                        case Element.Water: Water_Shard = value; break;
                        case Element.Light: Light_Shard = value; break;
                        case Element.Dark: Dark_Shard = value; break;
                    }
                    break;
                case CrystalType.Crystal:
                    switch (element)
                    {
                        case Element.Fire: Fire_Crystal = value; break;
                        case Element.Ice: Ice_Crystal = value; break;
                        case Element.Wind: Wind_Crystal = value; break;
                        case Element.Lightning: Lightning_Crystal = value; break;
                        case Element.Earth: Earth_Crystal = value; break;
                        case Element.Water: Water_Crystal = value; break;
                        case Element.Light: Light_Crystal = value; break;
                        case Element.Dark: Dark_Crystal = value; break;
                    }
                    break;
                case CrystalType.Cluster:
                    switch (element)
                    {
                        case Element.Fire: Fire_Cluster = value; break;
                        case Element.Ice: Ice_Cluster = value; break;
                        case Element.Wind: Wind_Cluster = value; break;
                        case Element.Lightning: Lightning_Cluster = value; break;
                        case Element.Earth: Earth_Cluster = value; break;
                        case Element.Water: Water_Cluster = value; break;
                        case Element.Light: Light_Cluster = value; break;
                        case Element.Dark: Dark_Cluster = value; break;
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

                { nameof(Lightning_Shard), Lightning_Shard },
                { nameof(Lightning_Crystal), Lightning_Crystal },
                { nameof(Lightning_Cluster), Lightning_Cluster },

                { nameof(Earth_Shard), Earth_Shard },
                { nameof(Earth_Crystal), Earth_Crystal },
                { nameof(Earth_Cluster), Earth_Cluster },

                { nameof(Water_Shard), Water_Shard },
                { nameof(Water_Crystal), Water_Crystal },
                { nameof(Water_Cluster), Water_Cluster },

                { nameof(Light_Shard), Light_Shard },
                { nameof(Light_Crystal), Light_Crystal },
                { nameof(Light_Cluster), Light_Cluster },

                { nameof(Dark_Shard), Dark_Shard },
                { nameof(Dark_Crystal), Dark_Crystal },
                { nameof(Dark_Cluster), Dark_Cluster }
            };
        }

        public long Total()
        {
            long sum = 0;
            foreach (var v in ToDictionary().Values) sum += v;
            return sum;
        }
    }
}
