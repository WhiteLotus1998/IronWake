namespace Ironwake.Core;

/// <summary>
/// Where the captain comes from (issue 681, DESIGN section 14), chosen at a campaign's start: a
/// region whose <see cref="Stats"/> and <see cref="Growths"/> are added to the cast file's captain.
/// Each set sums to 0, so the four cards are one size and gate 1 compares like with like. No
/// support or rapport number depends on it (round 201); the origin's Commander's Word variant is
/// issue 648's.
/// </summary>
public sealed record CaptainOrigin(string Id, string Name, Stats Stats, Stats Growths)
{
    /// <summary>
    /// <paramref name="captain"/> as this origin makes them: the deltas on stats and growths, and
    /// the origin's id as the home region.
    /// </summary>
    public Unit Apply(Unit captain) =>
        captain with { Stats = captain.Stats + Stats, Growths = captain.Growths + Growths, Region = Id };

    /// <summary>The sum of a delta set, which the loader holds to 0.</summary>
    public static int Sum(Stats deltas) => Core.Stats.All.Sum(deltas.Get);
}
