using System;

namespace CrystalTerror
{
    /// <summary>
    /// Class/job identifiers (sourced from ClassJob.csv).
    /// Note: id 0 (Adventurer / empty) is intentionally omitted.
    /// Values correspond to the 'key' / row id in the CSV.
    /// </summary>
    public enum ClassJob : int
    {
        Gladiator = 1,      // GLA
        Pugilist = 2,       // PGL
        Marauder = 3,       // MRD
        Lancer = 4,         // LNC
        Archer = 5,         // ARC
        Conjurer = 6,       // CNJ
        Thaumaturge = 7,    // THM
        Carpenter = 8,      // CRP
        Blacksmith = 9,     // BSM
        Armorer = 10,       // ARM
        Goldsmith = 11,     // GSM
        Leatherworker = 12, // LTW
        Weaver = 13,        // WVR
        Alchemist = 14,     // ALC
        Culinarian = 15,    // CUL
        Miner = 16,         // MIN
        Botanist = 17,      // BTN
        Fisher = 18,        // FSH
        Paladin = 19,       // PLD
        Monk = 20,          // MNK
        Warrior = 21,       // WAR
        Dragoon = 22,       // DRG
        Bard = 23,          // BRD
        WhiteMage = 24,     // WHM
        BlackMage = 25,     // BLM
        Arcanist = 26,      // ACN
        Summoner = 27,      // SMN
        Scholar = 28,       // SCH
        Rogue = 29,         // ROG
        Ninja = 30,         // NIN
        Machinist = 31,     // MCH
        DarkKnight = 32,    // DRK
        Astrologian = 33,   // AST
        Samurai = 34,       // SAM
        RedMage = 35,       // RDM
        BlueMage = 36,      // BLU
        Gunbreaker = 37,    // GNB
        Dancer = 38,        // DNC
        Reaper = 39,        // RPR
        Sage = 40,          // SGE
        Viper = 41,         // VPR
        Pictomancer = 42    // PCT
    }
}
