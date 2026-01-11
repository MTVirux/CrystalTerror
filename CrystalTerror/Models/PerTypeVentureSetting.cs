namespace CrystalTerror;

/// <summary>
/// Per-type venture setting for automatic venture assignment.
/// Each element×type combination can have its own enabled state and threshold.
/// </summary>
[Serializable]
public class PerTypeVentureSetting
{
    /// <summary>
    /// If true, this element×type is considered for automatic venture assignment.
    /// If false, this type is excluded from candidates.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Threshold for this specific element×type.
    /// 0 = no threshold (fill to capacity).
    /// > 0 = stop assigning when count reaches this value.
    /// </summary>
    public long Threshold { get; set; } = 0;

    public PerTypeVentureSetting() { }

    public PerTypeVentureSetting(bool enabled, long threshold = 0)
    {
        Enabled = enabled;
        Threshold = threshold;
    }
}
