using System;

namespace CrystalTerror
{
    /// <summary>
    /// Elements used for crystals.
    /// </summary>
    public enum Element
    {
        Fire,
        Ice,
        Wind,
        Lightning,
        Earth,
        Water,
    }

    /// <summary>
    /// Crystal sizes/types.
    /// </summary>
    public enum CrystalType
    {
        Shard,
        Crystal,
        Cluster,
    }

    /// <summary>
    /// How saved characters should be presented in the UI.
    /// </summary>
    public enum CharacterSort
    {
        Alphabetical,
        ReverseAlphabetical,
        World,
        ReverseWorld,
        AutoRetainer,
        Custom,
    }
}
