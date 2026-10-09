namespace CrystalTerror;

[Serializable]
public class LevelingTier
{
    public int MaxLevel { get; set; } = 1;
    public LevelingAction Action { get; set; } = LevelingAction.QuickExploration;

    public uint VentureIdMiner { get; set; } = (uint)VentureId.QuickExploration;
    public uint VentureIdBotanist { get; set; } = (uint)VentureId.QuickExploration;
    public uint VentureIdFisher { get; set; } = (uint)VentureId.QuickExploration;
    public uint VentureIdCombat { get; set; } = (uint)VentureId.QuickExploration;

    public uint GetVentureId(int? job) => job switch
    {
        16 => VentureIdMiner,
        17 => VentureIdBotanist,
        18 => VentureIdFisher,
        _ => VentureIdCombat,
    };

    public void SetVentureId(int? job, uint id)
    {
        switch (job)
        {
            case 16: VentureIdMiner = id; break;
            case 17: VentureIdBotanist = id; break;
            case 18: VentureIdFisher = id; break;
            default: VentureIdCombat = id; break;
        }
    }
}
