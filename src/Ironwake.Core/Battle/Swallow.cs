namespace Ironwake.Core;

/// <summary>
/// A boss's second stage (issue 1385, Lotus's Hask rework, Table round 487; numbers provisional on #1247): the
/// fresh bar <paramref name="Hp"/> the swallow sets his max HP to, the <paramref name="Def"/> and
/// <paramref name="Res"/> it adds, the HP the Kin heals him at each of his side's phase starts
/// (<paramref name="Heal"/>), and the line his card prints under his name from then on
/// (<paramref name="Description"/>, the empty pommel), or null to keep his own. <paramref name="Rooted"/> roots him on
/// the tile he swallowed on (Table round 505, issue 1395): from the swallow he holds (<see cref="Behavior.Hold"/>), striking
/// only what his lance and line reach from there. <paramref name="Late"/> starts the clock a phase late (Table rounds 507,
/// 508, issue 1423): Frozen Iron first lands at his side's second phase start after the swallow, not its first. <paramref name="Race"/>
/// makes stage 2 a race with no turn limit (Table rounds 514, 515, issue 1395): while he stands swallowed, the map's limit ends
/// nothing, and stage 2 ends when he falls or the company does (<see cref="BattleState.Racing"/>). <paramref name="Dose"/> is
/// what Frozen Iron lands for the first time and <paramref name="Step"/> what each later landing adds (Table round 518, issue
/// 1395: the start against the step); a first dose of 0 still lands, the dose naming itself. Read from a template's
/// <c>swallow</c> block in <c>units/</c>.
/// </summary>
public sealed record KinStage(int Hp, int Def, int Res, int Heal, string? Description = null, bool Rooted = false, bool Late = false, bool Race = false, int Dose = Swallow.FirstDose, int Step = Swallow.DoseStep);

/// <summary>Stage 1's bar reached 0 and <paramref name="UnitId"/> swallowed the shard (issue 1385): stage 2 begins on <paramref name="Hp"/>, a fresh bar, on <paramref name="At"/>.</summary>
public sealed record ShardSwallowed(string UnitId, Coord At, int Hp) : GameEvent;

/// <summary>
/// Frozen Iron landed at his side's phase start (issues 1385, 1395): <paramref name="Amount"/> on every unit on the board, either
/// side, but the swallowed boss who casts it (Lotus, 2026-10-09), flat past Def and Res; <paramref name="Struck"/> names them in unit order and <paramref name="HpAfter"/> gives
/// each one's HP after it. A <see cref="UnitDied"/> or <see cref="ShardSwallowed"/> follows for each it takes to 0.
/// <paramref name="Turned"/> says the frost has turned (issue 1386 slice 3e, <see cref="Swallow.Turned"/>), so the Kin
/// is among the struck while he stands above his floor.
/// </summary>
public sealed record FrozenIronFell(int Amount, ValueList<string> Struck, ValueList<int> HpAfter, bool Turned = false) : GameEvent;

/// <summary>The Kin healed <paramref name="UnitId"/> <paramref name="Amount"/> at its side's phase start (issue 1385), to <paramref name="HpAfter"/>.</summary>
public sealed record KinHealed(string UnitId, int Amount, int HpAfter) : GameEvent;

/// <summary>The shard under the hill broke on a <c>shard_breaks: stills</c> map while a swallowed boss stood (issue 1386 slice 3e): Frozen Iron holds at <paramref name="Dose"/>, the dose it had reached or the stage's step if that is more, and climbs no more.</summary>
public sealed record FrozenIronStilled(int Dose) : GameEvent;

/// <summary>The shard under the hill broke on a <c>shard_breaks: turns</c> map while the swallowed <paramref name="UnitId"/> stood (issue 1386 slice 3e): Frozen Iron lands on him too from now, climbing as before, never taking him below <paramref name="Floor"/>.</summary>
public sealed record FrozenIronTurned(string UnitId, int Floor) : GameEvent;

/// <summary>The swallowed boss <paramref name="UnitId"/> fell (issue 1385): the cold drains out of him and the shard lies on <paramref name="At"/>, his tile.</summary>
public sealed record ColdDrained(string UnitId, Coord At) : GameEvent;

/// <summary>
/// Hask's second stage (issue 1385; DESIGN.md section 8). A unit placed from a template with a <c>swallow</c> block
/// carries it (<see cref="BattleUnit.Kin"/>). The hit, strike, cast, blow or Frozen Iron that takes it to 0 HP
/// while it has not swallowed is not its death: it swallows the shard (<see cref="ShardSwallowed"/>), stays on its
/// tile, and takes the stage's numbers on a fresh bar (<see cref="Take"/>). That hit pays no kill EXP, frees no bond,
/// swears no grudge and drops nothing; the hit's riders land on him as they would on a survivor. From his side's next
/// phase start, at each of his side's phase starts (<see cref="Casts"/>, issue 1395), Frozen Iron lands on every unit
/// on the board but him (Lotus, 2026-10-09: he is exempt, his own side is not), flat past Def and Res: the stage's
/// <see cref="KinStage.Dose"/> at the first, its <see cref="KinStage.Step"/> more each after, with no cap (Lotus, 2026-10-09: it climbs until he dies) (<see cref="Fall"/>). It can kill; a unit it kills dies as any other. Then the Kin heals
/// him <see cref="KinStage.Heal"/> at his side's phase start, to max. When he falls in stage 2, the cold drains out
/// of him and the shard lies on his tile (<see cref="ColdDrained"/>). Everything is board state, so Recall restores it.
/// </summary>
public static class Swallow
{
    /// <summary>Frozen Iron at its first landing when the stage names none (issue 1385, <see cref="KinStage.Dose"/>).</summary>
    public const int FirstDose = 2;

    /// <summary>What each later landing adds when the stage names none (issue 1385, <see cref="KinStage.Step"/>).</summary>
    public const int DoseStep = 2;

    /// <summary>
    /// Whether the frost is stilled on <paramref name="state"/> (issue 1386 slice 3e, Code's lean, round 550, on a sample):
    /// the map carries <c>shard_breaks: stills</c> and the Kin's shard is broken, so each landing holds the dose set at the
    /// break (<see cref="KinShard.Take"/>: the dose reached, never below the stage's <see cref="KinStage.Step"/>) instead of climbing.
    /// </summary>
    public static bool Stilled(BattleState state) => state.Map.ShardBreaks == ShardBreak.Stills && KinShard.Of(state) is { Broken: true };

    /// <summary>
    /// Whether the frost has turned on the Kin on <paramref name="state"/> (issue 1386 slice 3e, Chat's lean, rounds 551 and
    /// 555, on a sample; it ends Lotus's exemption on the hill, so it goes on his sign-off): the map carries
    /// <c>shard_breaks: turns</c> and the Kin's shard is broken, so Frozen Iron lands on the swallowed boss too, down to
    /// his <see cref="Floor"/> and no lower.
    /// </summary>
    public static bool Turned(BattleState state) => state.Map.ShardBreaks == ShardBreak.Turns && KinShard.Of(state) is { Broken: true };

    /// <summary>
    /// The HP the turned frost never takes <paramref name="unit"/> below (Table round 555): half his current stage's max HP,
    /// rounded up, the rule anything that stands back up stands by. The frost alone never wins the hill.
    /// </summary>
    public static int Floor(BattleUnit unit, GameContent content) => Hollow.RisenHp(unit.MaxHp(content));

    /// <summary>
    /// Whether Frozen Iron passes <paramref name="unit"/> by on <paramref name="state"/>: the swallowed boss who casts it
    /// (Lotus, 2026-10-09), unless the frost has turned (<see cref="Turned"/>) and he stands above his <see cref="Floor"/>.
    /// </summary>
    public static bool Spared(BattleState state, BattleUnit unit, GameContent content) =>
        unit.Swallowed && (!Turned(state) || unit.Hp <= Floor(unit, content));

    /// <summary>Whether a hit taking <paramref name="unit"/> to 0 HP makes it swallow rather than die: it carries a stage and has not swallowed.</summary>
    public static bool Takes(BattleUnit unit) => unit is { Kin: not null, Swallowed: false };

    /// <summary>
    /// The unit <paramref name="unitId"/>, at 0 HP on <paramref name="state"/>, swallows: its Def and Res rise by the
    /// stage's, its max HP becomes the stage's bar, it stands on that bar full, a <see cref="KinStage.Rooted"/> stage
    /// holds its tile from then on, and Frozen Iron is set to land for the stage's <see cref="KinStage.Dose"/> from the next
    /// phase start, or the one after for a <see cref="KinStage.Late"/> stage (<see cref="BattleState.FrozenIronHeld"/>),
    /// unless a swallow on this board already set it. On a shard race (issue 1386, <see cref="ShardRun"/>) the first fall
    /// starts the race instead, and a hit while he runs leaves him on 1 HP; he swallows here when the race runs out.
    /// </summary>
    public static BattleState Take(BattleState state, GameContent content, string unitId, List<GameEvent> events)
    {
        var unit = state.Find(unitId)!;
        if (ShardRun.Running(unit))
        {
            return state.WithUnit(unit with { Hp = 1 });
        }

        if (ShardRun.Starts(state, unit))
        {
            return ShardRun.Begin(state, content, unitId, events);
        }

        var stage = unit.Kin!;
        var stats = unit.Unit.Stats;
        var raised = stats with { Hp = stats.Hp + stage.Hp - unit.MaxHp(content), Def = stats.Def + stage.Def, Res = stats.Res + stage.Res };
        var swallowed = unit with
        {
            Unit = unit.Unit with { Stats = raised, Description = stage.Description ?? unit.Unit.Description },
            Swallowed = true,
            Behavior = stage.Rooted ? Behavior.Hold : unit.Behavior,
        };
        swallowed = swallowed with { Hp = swallowed.MaxHp(content) };
        events.Add(new ShardSwallowed(unitId, unit.At, swallowed.Hp));
        var set = state.FrozenIron > 0;
        return state.WithUnit(swallowed) with
        {
            FrozenIron = set ? state.FrozenIron : stage.Dose,
            FrozenIronHeld = set ? state.FrozenIronHeld : stage.Late,
        };
    }

    /// <summary>
    /// The opening <paramref name="state"/> with the map's <c>swallowed:</c> boss (issue 1386 slice 3b',
    /// <see cref="MapDefinition.Swallowed"/>) already in his second stage, as <see cref="Take"/> leaves a boss who
    /// swallowed: on the stage's bar, full, with its Def and Res, rooted if the stage is, and Frozen Iron set to the
    /// stage's dose, held a phase for a late stage. No event: he begins the map so, and the board's row says so (<see cref="Line"/>). Unchanged on a
    /// map without the header.
    /// </summary>
    public static BattleState Opening(BattleState state, GameContent content)
    {
        if (state.Map.Swallowed is not { } at || state.UnitAt(at) is not { Kin: not null, Swallowed: false } boss)
        {
            return state;
        }

        return Take(state, content, boss.Id, new List<GameEvent>());
    }

    /// <summary>
    /// The board's row for a boss who began the map swallowed (issue 1386 slice 3b', <see cref="MapDefinition.Swallowed"/>),
    /// where no swallow announced the clock: what Frozen Iron lands for next, when, its step, and the Kin's heal. Null on
    /// a map without the header, or once he has fallen.
    /// </summary>
    public static string? Line(BattleState state, GameContent content, UnitNames names)
    {
        if (state.Map.Swallowed is null || state.Units.FirstOrDefault(u => u is { Swallowed: true, Kin: not null, Retreated: false } && u.Side == Side.Enemy) is not { } kin)
        {
            return null;
        }

        var when = state.FrozenIronHeld ? "from the enemy phase after next" : "at each enemy phase start";
        var climb = Stilled(state) ? "and climbs no more (the shard is broken)" : $"{kin.Kin!.Step} more each time";
        var whom = Turned(state) ? $"on every unit, him included but never below {Floor(kin, content)} (the shard is broken)" : "on every unit but him";
        return $"{names[kin.Id]} stands swallowed: Frozen Iron lands for {state.FrozenIron} {whom} {when}, {climb}, then the Kin heals him {kin.Kin!.Heal}";
    }

    /// <summary>
    /// The phase start's Frozen Iron and the Kin's heal (issue 1385), after the terrain's heal and burn: while
    /// <paramref name="side"/> is his (<see cref="Casts"/>), every unit
    /// on the board but a swallowed unit (<see cref="Spared"/>) takes it, never below 0, unless the landing is held (<see cref="BattleState.FrozenIronHeld"/>), when this
    /// phase start only lifts the hold; a unit it takes to 0 swallows if it may (<see cref="Takes"/>), else dies
    /// through <paramref name="died"/>; the next landing
    /// climbs by the caster's <see cref="KinStage.Step"/>, uncapped. Then each swallowed unit of
    /// <paramref name="side"/> still standing heals its stage's <see cref="KinStage.Heal"/>, to max.
    /// </summary>
    public static BattleState Fall(BattleState state, GameContent content, Side side, List<GameEvent> events, Func<BattleState, BattleUnit, BattleState> died)
    {
        if (Casts(state, side) && state.FrozenIronHeld)
        {
            state = state with { FrozenIronHeld = false };
        }
        else if (Casts(state, side))
        {
            var dose = state.FrozenIron;
            var step = Step(state, side);
            var struck = state.Units.Where(u => !u.Retreated && !Spared(state, u, content)).ToList();
            int After(BattleUnit u) => u.Swallowed ? Math.Max(Floor(u, content), u.Hp - dose) : Math.Max(0, u.Hp - dose);
            events.Add(new FrozenIronFell(dose, ValueList<string>.From(struck.Select(u => u.Id)), ValueList<int>.From(struck.Select(After)), Turned(state)));
            foreach (var aimed in struck)
            {
                var unit = state.Find(aimed.Id)!;
                var hp = After(unit);
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

            state = state with { FrozenIron = Stilled(state) ? dose : dose + step };
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
    /// The Frozen Iron that lands on a unit of <paramref name="side"/> at the other side's next phase start (round 502):
    /// the set dose while a swallowed unit of the other side stands (<see cref="Casts"/>) and the landing is not held
    /// (<see cref="BattleState.FrozenIronHeld"/>), else 0. It lands before that
    /// phase strikes, so <see cref="Exposure.Of"/> counts it.
    /// </summary>
    public static int NextLanding(BattleState state, Side side) =>
        !state.FrozenIronHeld && Casts(state, side == Side.Player ? Side.Enemy : Side.Player) ? state.FrozenIron : 0;

    /// <summary>What each landing adds while <paramref name="side"/> casts (Table round 518): the first standing swallowed unit's <see cref="KinStage.Step"/>.</summary>
    public static int Step(BattleState state, Side side) =>
        state.Units.FirstOrDefault(u => u is { Swallowed: true, Retreated: false } && u.Side == side)?.Kin?.Step ?? DoseStep;

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
