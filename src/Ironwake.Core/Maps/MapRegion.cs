namespace Ironwake.Core;

/// <summary>
/// Where a map lies (issue 916, the <c>region:</c> header): the seam the keep holds, one of the
/// three regions, or the outlands past them. It picks the colour plain is drawn in and nothing
/// else; no rule reads it.
/// </summary>
public enum MapRegion
{
    Seam,
    Sallow,
    Aldmere,
    Kestrow,
    Outland,
}

/// <summary>The <c>region:</c> header's words (issue 916).</summary>
public static class MapRegions
{
    /// <summary>The region's word as the header prints it.</summary>
    public static string Word(MapRegion region) => region switch
    {
        MapRegion.Sallow => "sallow",
        MapRegion.Aldmere => "aldmere",
        MapRegion.Kestrow => "kestrow",
        MapRegion.Outland => "outland",
        _ => "seam",
    };

    /// <summary>The region a header word names, or null for none.</summary>
    public static MapRegion? Parse(string word) =>
        Enum.GetValues<MapRegion>().Cast<MapRegion?>().FirstOrDefault(r => Word(r!.Value) == word);
}
