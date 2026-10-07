namespace Ironwake.Core;

/// <summary>
/// The gate on a learned school's rider (issue 1246, DECISIONS/0301): a rider from a school the
/// caster reaches only by a primer (<see cref="Unit.Learned"/>) fires only when the caster's Mag is
/// above the target's Res plus the rider's <see cref="SchoolRider.Gate"/>. Both are the card's
/// numbers (<see cref="GameContent.StatsOf(Unit)"/>: unit, class and passives); a tile's cover is not
/// counted. A caster whose class names the school is never gated, and a tome with no rider has
/// nothing to gate. The forecast prints the reading either way.
/// </summary>
public static class LearnedGate
{
    /// <summary>One reading of the gate: the caster's Mag, the target's Res and the rider's margin.</summary>
    public sealed record Reading(int Mag, int Res, int Margin)
    {
        /// <summary>Whether the rider fires: Mag above Res plus the margin.</summary>
        public bool Passes => Mag > Res + Margin;

        /// <summary>The numbers as a line prints them: <c>Mag 6 over Res 4</c> when it passes, else <c>Mag 4, Res 4</c>; a margin shows as <c>Res 4+2</c>.</summary>
        public string Text => Passes ? $"Mag {Mag} over Res {ResText}" : $"Mag {Mag}, Res {ResText}";

        private string ResText => Margin == 0 ? Res.ToString(System.Globalization.CultureInfo.InvariantCulture) : $"{Res}+{Margin}";
    }

    /// <summary>
    /// The gate <paramref name="caster"/> striking <paramref name="target"/> with <paramref name="weapon"/>
    /// is under, or null when it is under none: no rider on the weapon, the rider's gate is null, or
    /// the caster's class reaches the school.
    /// </summary>
    public static Reading? Read(GameContent content, BattleUnit? caster, Weapon? weapon, BattleUnit? target)
    {
        if (caster is null || target is null || content.RiderOf(weapon) is not { } rider || weapon!.School is not { } school)
        {
            return null;
        }

        if (rider.Gate is not { } margin || content.Class(caster.Unit.ClassId).Reaches(school) || !caster.Unit.Learned.Contains(school))
        {
            return null;
        }

        return new Reading(content.StatsOf(caster.Unit).Mag, content.StatsOf(target.Unit).Res, margin);
    }

    /// <summary>Whether a rider <paramref name="caster"/>'s <paramref name="weapon"/> carries fires on <paramref name="target"/>: ungated, or the gate passes.</summary>
    public static bool Fires(GameContent content, BattleUnit? caster, Weapon? weapon, BattleUnit? target) =>
        Read(content, caster, weapon, target) is not { Passes: false };

    /// <summary>
    /// The forecast's words after a gated rider's own when it fires, <c>: Mag 6 over Res 4</c>; empty
    /// when no gate applies or it does not pass (<see cref="Refused"/> prints that).
    /// </summary>
    public static string Suffix(Reading? reading) => reading is { Passes: true } ? ": " + reading.Text : "";

    /// <summary>The words in place of a rider named <paramref name="noun"/> the gate holds back: <c> no burn: Mag 4, Res 4</c>.</summary>
    public static string Refused(string noun, Reading reading) => $" no {noun}: {reading.Text}";
}
