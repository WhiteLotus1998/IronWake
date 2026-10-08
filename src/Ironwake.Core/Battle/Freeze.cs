namespace Ironwake.Core;

/// <summary>
/// The freeze (issue 1330, Lotus's round-3 spell rulings: Still Water's second use, "on or beside water"; DECISIONS/0328).
/// A hit from a tome naming <see cref="RiderKind.Freeze"/> on a unit that survives the combat and stands on a Water tile
/// or orthogonally beside one (<see cref="ByWater"/>) freezes it:
/// <list type="bullet">
/// <item>Mov 0 through its side's next phase, on the chill's clock (<see cref="BattleUnit.Frozen"/>); a boss keeps
/// <see cref="BossMov"/>. It still strikes, counters and uses items from its tile.</item>
/// <item>Off water the hit is only a hit: the forecast says so (<see cref="ForecastText"/>).</item>
/// <item>It never stacks: a second freeze refreshes the clock. A miss does nothing and a kill leaves no one to freeze.</item>
/// <item>Light's cleanse clears it (<see cref="Cleanse"/>).</item>
/// </list>
/// The word is freeze in every line, never root (Lotus). A freeze names the ice school's chill rider as an ember names
/// fire's burn (<see cref="SchoolRider.Borrows"/>), so a learned caster's gate binds it (<see cref="LearnedGate"/>).
/// Everything is board state, so Recall restores it with the board, and <c>threat</c>, the planners and the Sim read it
/// through <see cref="BattleState.ReachOf"/>. No shipped tome names it until Lotus signs its numbers (#1247).
/// </summary>
public static class Freeze
{
    /// <summary>The terrain a freeze needs on or beside the struck unit's tile.</summary>
    public const string WaterTerrainId = "water";

    /// <summary>The Mov a frozen boss keeps.</summary>
    public const int BossMov = 1;

    /// <summary>Whether <paramref name="weapon"/> names a freeze.</summary>
    public static bool Freezes(GameContent content, Weapon? weapon) =>
        content.RiderOf(weapon) is { Kind: RiderKind.Freeze };

    /// <summary>Whether <paramref name="at"/> is a Water tile or orthogonally beside one in <paramref name="state"/>'s map as it stands now; a diagonal never counts.</summary>
    public static bool ByWater(BattleState state, Coord at) =>
        new[] { at, new Coord(at.X + 1, at.Y), new Coord(at.X - 1, at.Y), new Coord(at.X, at.Y + 1), new Coord(at.X, at.Y - 1) }
            .Any(c => state.Map.Contains(c) && state.Map.TerrainIdAt(c) == WaterTerrainId);

    /// <summary><paramref name="mov"/> as a freeze leaves it for <paramref name="unit"/>: 0 while frozen, at most <see cref="BossMov"/> for a boss.</summary>
    public static int Mov(int mov, BattleUnit unit) => unit.Frozen > 0 ? Math.Min(mov, unit.IsBoss ? BossMov : 0) : mov;

    /// <summary>
    /// After a combat or a strike: each unit in <paramref name="strikes"/> still standing by water that a hit from a freezing
    /// tome landed on is frozen (<see cref="UnitFrozen"/>), its clock set to 1. <paramref name="a"/> and <paramref name="b"/>
    /// are the two as they entered the combat, whose gate is read; else as <paramref name="state"/> finds them.
    /// </summary>
    public static BattleState AfterCombat(BattleState state, GameContent content, string aId, Weapon? aWeapon, string bId, Weapon? bWeapon, ValueList<StrikeEvent> strikes, List<GameEvent> events, BattleUnit? a = null, BattleUnit? b = null)
    {
        a ??= state.Find(aId);
        b ??= state.Find(bId);
        foreach (var (strikerId, weapon, targetId, striker, struck) in new[] { (aId, aWeapon, bId, a, b), (bId, bWeapon, aId, b, a) })
        {
            if (!Freezes(content, weapon) || !LearnedGate.Fires(content, striker, weapon, struck)
                || !strikes.Any(s => s.AttackerId == strikerId && s.TargetId == targetId && s.Hit)
                || state.Find(targetId) is not { } target || !ByWater(state, target.At))
            {
                continue;
            }

            events.Add(new UnitFrozen(target.Id, strikerId, target.Side, target.IsBoss, target.Side == state.Phase));
            state = state.WithUnit(target with { Frozen = 1 });
        }

        return state;
    }

    /// <summary>
    /// The forecast's words for a side whose weapon freezes, after its strike columns: <c> freezes</c> on a target by water,
    /// <c> freezes: boss Mov 1</c> on a boss there, <c> no water: no freeze</c> off it; empty when it carries none.
    /// </summary>
    public static string ForecastText(BattleState state, GameContent content, Weapon? weapon, BattleUnit target) =>
        !Freezes(content, weapon) ? ""
        : !ByWater(state, target.At) ? " no water: no freeze"
        : target.IsBoss ? $" freezes: boss Mov {BossMov}"
        : " freezes";

    /// <summary>
    /// The unit card's line for a frozen unit: <c>frozen: cannot move next phase</c>, or <c>this phase</c> while its
    /// clock has begun on its own side's phase; a boss reads <c>Mov 1</c> for <c>cannot move</c>. Null when not frozen.
    /// </summary>
    public static string? CardLine(BattleUnit unit) =>
        unit.Frozen > 0 ? $"frozen: {What(unit.IsBoss)} {(unit.Frozen == 2 ? "this" : "next")} phase" : null;

    /// <summary>What a freeze leaves a unit as a line says it: <c>cannot move</c>, or <c>Mov 1</c> for a boss.</summary>
    public static string What(bool boss) => boss ? $"Mov {BossMov}" : "cannot move";
}
