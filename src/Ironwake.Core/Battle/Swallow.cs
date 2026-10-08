namespace Ironwake.Core;

/// <summary>
/// A boss's second stage (issue 1385, Lotus's Hask rework, Table round 487; numbers provisional on #1247): the
/// fresh bar <paramref name="Hp"/> the swallow sets his max HP to, the <paramref name="Def"/> and
/// <paramref name="Res"/> it adds, the HP the Kin heals him at each of his side's phase starts
/// (<paramref name="Heal"/>), and the line his card prints under his name from then on
/// (<paramref name="Description"/>, the empty pommel), or null to keep his own. Read from a template's
/// <c>swallow</c> block in <c>units/</c>.
/// </summary>
public sealed record KinStage(int Hp, int Def, int Res, int Heal, string? Description = null);

/// <summary>Stage 1's bar reached 0 and <paramref name="UnitId"/> swallowed the shard (issue 1385): stage 2 begins on <paramref name="Hp"/>, a fresh bar, on <paramref name="At"/>.</summary>
public sealed record ShardSwallowed(string UnitId, Coord At, int Hp) : GameEvent;

/// <summary>
/// Frozen Iron landed at his side's phase start (issues 1385, 1395): <paramref name="Amount"/> on every unit on the board, either
/// side, flat past Def and Res; <paramref name="Struck"/> names them in unit order and <paramref name="HpAfter"/> gives
/// each one's HP after it. A <see cref="UnitDied"/> or <see cref="ShardSwallowed"/> follows for each it takes to 0.
/// </summary>
public sealed record FrozenIronFell(int Amount, ValueList<string> Struck, ValueList<int> HpAfter) : GameEvent;

/// <summary>The Kin healed <paramref name="UnitId"/> <paramref name="Amount"/> at its side's phase start (issue 1385), to <paramref name="HpAfter"/>.</summary>
public sealed record KinHealed(string UnitId, int Amount, int HpAfter) : GameEvent;

/// <summary>The swallowed boss <paramref name="UnitId"/> fell (issue 1385): the cold drains out of him and the shard lies on <paramref name="At"/>, his tile.</summary>
public sealed record ColdDrained(string UnitId, Coord At) : GameEvent;

/// <summary>
/// Hask's second stage (issue 1385; DESIGN.md section 8). A unit placed from a template with a <c>swallow</c> block
/// carries it (<see cref="BattleUnit.Kin"/>). The hit, strike, cast, blow or Frozen Iron that takes it to 0 HP
/// while it has not swallowed is not its death: it swallows the shard (<see cref="ShardSwallowed"/>), stays on its
/// tile, and takes the stage's numbers on a fresh bar (<see cref="Take"/>). That hit pays no kill EXP, frees no bond,
/// swears no grudge and drops nothing; the hit's riders land on him as they would on a survivor. From his side's next
/// phase start, at each of his side's phase starts (<see cref="Casts"/>, issue 1395), Frozen Iron lands on every unit
/// on the board, him too, flat past Def and Res: <see cref="FirstDose"/> at the first, <see cref="DoseStep"/> more each after, at most
/// <see cref="MostDose"/> (<see cref="Fall"/>). It can kill; a unit it kills dies as any other. Then the Kin heals
/// him <see cref="KinStage.Heal"/> at his side's phase start, to max. When he falls in stage 2, the cold drains out
/// of him and the shard lies on his tile (<see cref="ColdDrained"/>). Everything is board state, so Recall restores it.
/// </summary>
public static class Swallow
{
    /// <summary>Frozen Iron at its first landing (issue 1385).</summary>
    public const int FirstDose = 2;

    /// <summary>What each later landing adds (issue 1385).</summary>
    public const int DoseStep = 2;

    /// <summary>The most Frozen Iron lands for (issue 1385's 2, 4, 6, 8, 10; a lean: it climbs no further).</summary>
    public const int MostDose = 10;

    /// <summary>Whether a hit taking <paramref name="unit"/> to 0 HP makes it swallow rather than die: it carries a stage and has not swallowed.</summary>
    public static bool Takes(BattleUnit unit) => unit is { Kin: not null, Swallowed: false };

    /// <summary>
    /// The unit <paramref name="unitId"/>, at 0 HP on <paramref name="state"/>, swallows: its Def and Res rise by the
    /// stage's, its max HP becomes the stage's bar, it stands on that bar full, and Frozen Iron is set to land from the
    /// next phase start.
    /// </summary>
    public static BattleState Take(BattleState state, GameContent content, string unitId, List<GameEvent> events)
    {
        var unit = state.Find(unitId)!;
        var stage = unit.Kin!;
        var stats = unit.Unit.Stats;
        var raised = stats with { Hp = stats.Hp + stage.Hp - unit.MaxHp(content), Def = stats.Def + stage.Def, Res = stats.Res + stage.Res };
        var swallowed = unit with
        {
            Unit = unit.Unit with { Stats = raised, Description = stage.Description ?? unit.Unit.Description },
            Swallowed = true,
        };
        swallowed = swallowed with { Hp = swallowed.MaxHp(content) };
        events.Add(new ShardSwallowed(unitId, unit.At, swallowed.Hp));
        return state.WithUnit(swallowed) with { FrozenIron = state.FrozenIron > 0 ? state.FrozenIron : FirstDose };
    }

    /// <summary>
    /// The phase start's Frozen Iron and the Kin's heal (issue 1385), after the terrain's heal and burn: while
    /// <see cref="BattleState.FrozenIron"/> is set and <paramref name="side"/> is his (<see cref="Casts"/>), every unit
    /// on the board takes it, never below 0; a unit it takes to 0 swallows if it may (<see cref="Takes"/>), else dies
    /// through <paramref name="died"/>; the next landing
    /// climbs by <see cref="DoseStep"/> to <see cref="MostDose"/>. Then each swallowed unit of
    /// <paramref name="side"/> still standing heals its stage's <see cref="KinStage.Heal"/>, to max.
    /// </summary>
    public static BattleState Fall(BattleState state, GameContent content, Side side, List<GameEvent> events, Func<BattleState, BattleUnit, BattleState> died)
    {
        if (state.FrozenIron > 0 && Casts(state, side))
        {
            var dose = state.FrozenIron;
            var struck = state.Units.Where(u => !u.Retreated).ToList();
            events.Add(new FrozenIronFell(dose, ValueList<string>.From(struck.Select(u => u.Id)), ValueList<int>.From(struck.Select(u => Math.Max(0, u.Hp - dose)))));
            foreach (var aimed in struck)
            {
                var unit = state.Find(aimed.Id)!;
                var hp = Math.Max(0, unit.Hp - dose);
                state = state.WithUnit(unit with { Hp = hp });
                if (hp > 0)
                {
                    continue;
                }

                if (Takes(unit))
                {
                    state = Take(state, content, unit.Id, events);
                    continue;
                }

                events.Add(new UnitDied(unit.Id, unit.Side, unit.At));
                state = died(state, unit with { Hp = 0 });
            }

            state = state with { FrozenIron = Math.Min(MostDose, dose + DoseStep) };
        }

        foreach (var healed in state.Units.Where(u => u is { Swallowed: true, Kin: not null } && u.Side == side).ToList())
        {
            var max = healed.MaxHp(content);
            var hp = Math.Min(max, healed.Hp + healed.Kin!.Heal);
            if (hp > healed.Hp)
            {
                events.Add(new KinHealed(healed.Id, hp - healed.Hp, hp));
                state = state.WithUnit(healed with { Hp = hp });
            }
        }

        return state;
    }

    /// <summary>
    /// Whether Frozen Iron lands at <paramref name="side"/>'s phase start (issue 1395): he casts it, so it lands at his
    /// own side's phase start alone, once a round, the landing the Kin's heal answers.
    /// </summary>
    public static bool Casts(BattleState state, Side side) => state.Units.Any(u => u is { Swallowed: true, Retreated: false } && u.Side == side);

    /// <summary>
    /// After a command (issue 1385): each <see cref="UnitDied"/> in <paramref name="events"/> for a unit that had
    /// swallowed on <paramref name="before"/> is followed by its <see cref="ColdDrained"/>.
    /// </summary>
    public static void AfterFall(BattleState before, List<GameEvent> events)
    {
        for (var i = 0; i < events.Count; i++)
        {
            if (events[i] is UnitDied died && before.Find(died.UnitId) is { Swallowed: true })
            {
                events.Insert(i + 1, new ColdDrained(died.UnitId, died.At));
                i++;
            }
        }
    }
}
