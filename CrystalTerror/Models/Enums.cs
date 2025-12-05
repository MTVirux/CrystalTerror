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

    /// <summary>
    /// Known retainer venture IDs for shards and crystals (language-independent IDs).
    /// These map to the in-game RetainerTask rows for elemental crystal/shard ventures.
    /// </summary>
    public enum VentureId : uint
    {
        Fire_Shard = 70,
        Ice_Shard = 71,
        Wind_Shard = 72,
        Earth_Shard = 73,
        Lightning_Shard = 74,
        Water_Shard = 75,

        Fire_Crystal = 111,
        Ice_Crystal = 112,
        Wind_Crystal = 113,
        Earth_Crystal = 114,
        Lightning_Crystal = 115,
        Water_Crystal = 116,
    }
}
