namespace Ironwake.Core;

/// <summary>
/// A school's burn rider (issue 1243, DECISIONS/0297; fire's, from <c>rules.json</c>), built to stack
/// (issue 1279, DECISIONS/0307, 0308; Lotus's Smolder). A hit from a tome that names its school's
/// <see cref="RiderKind.Burn"/> rider (<see cref="GameContent.RiderOf"/>) on a unit that survives the
/// combat lays a stack of burn on it: at the start of each of its own side's next
/// <see cref="SchoolRider.Phases"/> phases it loses its <see cref="Tick"/>,
/// <c>max(1, stacks * amount - Res / 2)</c>, never below 1 HP.
/// <list type="bullet">
/// <item>A miss does nothing, and a kill leaves no one to burn.</item>
/// <item>Each burning hit adds a stack, up to the rider's <see cref="SchoolRider.Cap"/>, and refreshes
/// the one phase count the stacks share: one count, so the card prints the burn as one line and the
/// cash-out is the tick times the phases left.</item>
/// <item>A hit from a tome that names <see cref="RiderKind.Ember"/> (Last Ember's) on a burning unit that
/// survives deals what the burn still owes now (<see cref="Owed"/>), never below 1 HP, and clears it
/// (<see cref="BurnCashed"/>).</item>
/// <item>Tile fire (DESIGN.md 13.15) is untouched and does not stack with it: a burning unit on a
/// burning tile loses the larger of the two, as one <see cref="UnitBurned"/>.</item>
/// </list>
/// Everything is board state (<see cref="BattleUnit.Burn"/>, <see cref="BattleUnit.BurnStacks"/>,
/// <see cref="BattleUnit.BurnPhases"/>), so Recall restores it with the board.
/// </summary>
public static class Burning
{
    /// <summary>
    /// After a combat or a strike: each unit in <paramref name="strikes"/> still standing that a hit
    /// from a burning school's tome landed on takes a stack (<see cref="UnitIgnited"/>), its count
    /// refreshed, and each burning one an ember tome's hit landed on is cashed out (<see cref="BurnCashed"/>).
    /// <paramref name="aWeapon"/> is what <paramref name="aId"/> struck with and
    /// <paramref name="bWeapon"/> what <paramref name="bId"/> did; a null weapon never burns.
    /// A learned school's rider fires only past its gate (issue 1246, <see cref="LearnedGate"/>), read
    /// on <paramref name="a"/> and <paramref name="b"/>, the two as they entered the combat, else as
    /// <paramref name="state"/> finds them.
    /// </summary>
    public static BattleState AfterCombat(BattleState state, GameContent content, string aId, Weapon? aWeapon, string bId, Weapon? bWeapon, ValueList<StrikeEvent> strikes, List<GameEvent> events, BattleUnit? a = null, BattleUnit? b = null)
    {
        a ??= state.Find(aId);
        b ??= state.Find(bId);
        foreach (var (strikerId, weapon, targetId, striker, struck) in new[] { (aId, aWeapon, bId, a, b), (bId, bWeapon, aId, b, a) })
        {
            if (!LearnedGate.Fires(content, striker, weapon, struck) || !strikes.Any(s => s.AttackerId == strikerId && s.TargetId == targetId && s.Hit))
            {
                continue;
            }

            if (state.Find(targetId) is not { } target)
            {
                continue;
            }

            if (Rider(content, weapon) is { } rider)
            {
                var laid = Laid(target, rider);
                events.Add(new UnitIgnited(target.Id, strikerId, Tick(content, laid), laid.BurnPhases, laid.BurnStacks));
                state = state.WithUnit(laid);
            }
            else if (Embers(content, weapon) && target.BurnPhases > 0)
            {
                var hpAfter = Math.Max(Math.Min(1, target.Hp), target.Hp - Owed(content, target));
                events.Add(new BurnCashed(target.Id, strikerId, target.Hp - hpAfter, hpAfter));
                state = state.WithUnit(target with { Hp = hpAfter, Burn = 0, BurnStacks = 0, BurnPhases = 0 });
            }
        }

        return state;
    }

    /// <summary>The burn rider <paramref name="weapon"/> carries, or null when it carries none.</summary>
    public static SchoolRider? Rider(GameContent content, Weapon? weapon) =>
        content.RiderOf(weapon) is { Kind: RiderKind.Burn } rider ? rider : null;

    /// <summary>Whether <paramref name="weapon"/> names its school's ember rider, the cash-out (issue 1279).</summary>
    public static bool Embers(GameContent content, Weapon? weapon) =>
        content.RiderOf(weapon) is { Kind: RiderKind.Ember };

    /// <summary><paramref name="unit"/> with one more stack of <paramref name="rider"/>'s burn on it, up to the cap, its count refreshed.</summary>
    public static BattleUnit Laid(BattleUnit unit, SchoolRider rider) =>
        unit.BurnPhases > 0
            ? unit with { Burn = Math.Max(unit.Burn, rider.Amount), BurnStacks = Math.Min(rider.Cap, unit.BurnStacks + 1), BurnPhases = rider.Phases }
            : unit with { Burn = rider.Amount, BurnStacks = Math.Min(rider.Cap, 1), BurnPhases = rider.Phases };

    /// <summary>
    /// What <paramref name="unit"/>'s burn takes at its side's next phase start (issue 1279):
    /// <c>max(1, stacks * amount - Res / 2)</c> while the count runs, else 0. The HP floor of 1 is the
    /// phase start's, not this.
    /// </summary>
    public static int Tick(GameContent content, BattleUnit unit) =>
        unit.BurnPhases > 0 && unit.BurnStacks > 0 ? Math.Max(1, unit.BurnStacks * unit.Burn - content.StatsOf(unit.Unit).Res / 2) : 0;

    /// <summary>The ticks <paramref name="unit"/>'s burn still owes, the tick times the phases left: what an ember's hit cashes (issue 1279).</summary>
    public static int Owed(GameContent content, BattleUnit unit) => Tick(content, unit) * unit.BurnPhases;

    /// <summary><paramref name="unit"/> with its burn's count turned once, as its side's phase begins: cleared, stacks and all, when the count runs out.</summary>
    public static BattleUnit Ticked(BattleUnit unit) =>
        unit.BurnPhases > 1 ? unit with { BurnPhases = unit.BurnPhases - 1 } : unit with { Burn = 0, BurnStacks = 0, BurnPhases = 0 };

    /// <summary>
    /// The forecast's words for a side whose weapon burns or embers <paramref name="struck"/>, after its
    /// strike columns: the burn a hit leaves, <c> burn 3 (2 stacks, 2 phases)</c>, or what a hit cashes,
    /// <c> cashes burn 6</c> or <c> cashes no burn</c>; empty when it does neither.
    /// </summary>
    public static string ForecastText(GameContent content, Weapon? weapon, BattleUnit struck) =>
        Rider(content, weapon) is { } rider ? " " + Text(content, Laid(struck, rider))
        : Embers(content, weapon) ? (struck.BurnPhases > 0 ? $" cashes burn {Owed(content, struck)}" : " cashes no burn")
        : "";

    /// <summary>The unit card's line for a burning unit: <c>burn 3 (2 stacks, 2 phases)</c>; null when it is not burning.</summary>
    public static string? CardLine(GameContent content, BattleUnit unit) =>
        unit.BurnPhases > 0 ? Text(content, unit) : null;

    /// <summary>
    /// What a burn or ember rider is worth to a planner striking <paramref name="target"/> (issues 1243,
    /// 1279): the HP a stack adds to what the burn already owes, or the HP an ember cashes, never past
    /// leaving 1 of the HP <paramref name="hpAfter"/> the strikes are expected to leave, weighted by
    /// <paramref name="landed"/>, the chance some strike hits. 0 when the weapon carries neither.
    /// </summary>
    public static double Expected(GameContent content, Weapon? weapon, BattleUnit target, double hpAfter, double landed)
    {
        double worth = Rider(content, weapon) is { } rider ? Owed(content, Laid(target, rider)) - Owed(content, target)
            : Embers(content, weapon) ? Owed(content, target)
            : 0;
        return Math.Max(0, Math.Min(worth, hpAfter - 1)) * landed;
    }

    private static string Text(GameContent content, BattleUnit unit) =>
        $"burn {Tick(content, unit)} ({Count(unit.BurnStacks, "stack")}, {Count(unit.BurnPhases, "phase")})";

    private static string Count(int n, string noun) => $"{n} {noun}" + (n == 1 ? "" : "s");
}
