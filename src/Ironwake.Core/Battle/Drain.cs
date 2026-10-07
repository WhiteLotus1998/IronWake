namespace Ironwake.Core;

/// <summary>
/// A school's drain rider (issue 1283, DECISIONS/0307; dark's, Lotus's omnivamp). After a combat or a
/// strike, a unit still standing whose hits from a tome naming its school's <see cref="RiderKind.Drain"/>
/// rider landed heals by the HP those hits took off the target, capped at its max HP, as one
/// <see cref="UnitDrained"/>.
/// <list type="bullet">
/// <item>The HP the hits removed, not their damage: a strike past the target's last HP adds only what it took.</item>
/// <item>A kill drains, a miss drains nothing, and a counter by a drain tome drains as an attack does.</item>
/// <item>The heal lands once, after the strikes, so a drainer the combat kills heals nothing and every
/// forecast and lethal read stays the combat's own.</item>
/// <item>A learned school's rider fires only past its gate (issue 1246, <see cref="LearnedGate"/>).</item>
/// </list>
/// The heal is HP on the board, so Recall restores it with the board.
/// </summary>
public static class Drain
{
    /// <summary>Whether <paramref name="weapon"/> names its school's drain rider.</summary>
    public static bool Drains(GameContent content, Weapon? weapon) =>
        content.RiderOf(weapon) is { Kind: RiderKind.Drain };

    /// <summary>
    /// After a combat or a strike: each of <paramref name="aId"/> and <paramref name="bId"/> still standing
    /// whose weapon drains heals by the HP its hits in <paramref name="strikes"/> took off the other
    /// (<see cref="Taken"/>), never past its max HP, and a <see cref="UnitDrained"/> says so when the heal
    /// is more than 0. <paramref name="a"/> and <paramref name="b"/> are the two as they entered the combat,
    /// whose HP the strikes count down from and whose gate is read; else as <paramref name="state"/> finds them.
    /// </summary>
    public static BattleState AfterCombat(BattleState state, GameContent content, string aId, Weapon? aWeapon, string bId, Weapon? bWeapon, ValueList<StrikeEvent> strikes, List<GameEvent> events, BattleUnit? a = null, BattleUnit? b = null)
    {
        a ??= state.Find(aId);
        b ??= state.Find(bId);
        foreach (var (strikerId, weapon, striker, struck) in new[] { (aId, aWeapon, a, b), (bId, bWeapon, b, a) })
        {
            if (striker is null || struck is null || !Drains(content, weapon) || !LearnedGate.Fires(content, striker, weapon, struck))
            {
                continue;
            }

            if (state.Find(strikerId) is not { } drainer)
            {
                continue;
            }

            var max = content.StatsOf(drainer.Unit).Hp;
            var healed = Math.Min(Taken(strikes, strikerId, struck.Id, struck.Hp), Math.Max(0, max - drainer.Hp));
            if (healed > 0)
            {
                events.Add(new UnitDrained(drainer.Id, struck.Id, healed, drainer.Hp + healed));
                state = state.WithUnit(drainer with { Hp = drainer.Hp + healed });
            }
        }

        return state;
    }

    /// <summary>
    /// The HP <paramref name="strikerId"/>'s hits in <paramref name="strikes"/> took off <paramref name="targetId"/>,
    /// which entered the combat at <paramref name="hpBefore"/>: each hit's fall in the target's HP, so
    /// nothing past its last HP counts.
    /// </summary>
    public static int Taken(ValueList<StrikeEvent> strikes, string strikerId, string targetId, int hpBefore)
    {
        var hp = hpBefore;
        var taken = 0;
        foreach (var strike in strikes.Where(s => s.TargetId == targetId))
        {
            if (strike.AttackerId == strikerId && strike.Hit)
            {
                taken += Math.Max(0, hp - strike.TargetHpAfter);
            }

            hp = strike.TargetHpAfter;
        }

        return taken;
    }

    /// <summary>
    /// What a side's strikes drain from <paramref name="struck"/> if every one lands, at the forecast's
    /// damage, never past the target's HP; 0 when the side does not strike. A crit drains more, as it
    /// deals more, and the line leaves it out as the forecast's damage column does.
    /// </summary>
    public static int MostDrained(SideForecast side, BattleUnit struck) =>
        side.Strikes ? Math.Min(struck.Hp, side.StrikeCount * side.Damage) : 0;

    /// <summary>
    /// The forecast's words for a side whose weapon drains, after its strike columns: <c> drains up to 12</c>, crits aside
    /// (<see cref="MostDrained"/>); empty when it does not drain or does not strike.
    /// </summary>
    public static string ForecastText(GameContent content, Weapon? weapon, SideForecast side, BattleUnit struck) =>
        Drains(content, weapon) && side.Strikes ? $" drains up to {MostDrained(side, struck)}" : "";

    /// <summary>
    /// What a drain is worth to a planner (issue 1283): the HP the expected damage <paramref name="dealt"/>
    /// heals the drainer, never past the HP it is expected to be missing,
    /// <paramref name="missing"/>, and 0 when <paramref name="weapon"/> does not drain.
    /// </summary>
    public static double Expected(GameContent content, Weapon? weapon, double dealt, double missing) =>
        Drains(content, weapon) ? Math.Max(0, Math.Min(dealt, missing)) : 0;
}
