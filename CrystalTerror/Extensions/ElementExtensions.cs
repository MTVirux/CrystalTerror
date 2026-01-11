namespace CrystalTerror;

/// <summary>
/// Extension methods for the Element enum.
/// </summary>
public static class ElementExtensions
{
    /// <summary>
    /// Gets a two-character abbreviation for the element (e.g., "Fi" for Fire, "Li" for Lightning).
    /// </summary>
    public static string GetAbbreviation(this Element element)
    {
        return element.ToString()[..2];
    }

    /// <summary>
    /// Gets the full display name for the element.
    /// </summary>
    public static string GetDisplayName(this Element element)
    {
        return element.ToString();
    }
}
