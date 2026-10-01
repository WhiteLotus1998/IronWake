using Ironwake.Core.Tests.Maps;
using Ironwake.Sim;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The pincer's planner arm (DESIGN.md 13.13, issue 419): on a <c>pincer: on</c> map an enemy
/// steps in beside a player unit so that a group-mate's strike on it is pinned, and acts ahead
/// of its group to do it. One follower is promised to one anvil; a boss or a throne-holder is
/// never an anvil; the far tile is checked with the anvil already standing; and without the
/// header the phase is planned exactly as before.
/// </summary>
public class AnvilTests
{
    private static string Field(bool pincer, string units, string win = "rout") =>
        $"""
        name: Field
        size: 9x5
        win: {win}
        turn_limit: 10
        recall: 3
        enemy_level: 1
        {(pincer ? "pincer: on" : "")}

        .........
        .........
        .........
        .........
        .........

        units:
        {units}
        """.Replace("\n\n\n", "\n\n");

    /// <summary>The archer, holding out of reach, is first by placement; the brigand is the anvil and the soldier its follower.</summary>
    private const string Trio = """
        P captain 4,2
        E archer 8,4 group:yard behavior:hold
        E brigand 1,2 group:field behavior:aggressive
        E soldier 7,2 group:field behavior:aggressive

        """;

    private static BattleState Enemy(string units, bool pincer = true, ValueList<Unit>? roster = null) =>
        BattleFixture.Start(map: Field(pincer, units), roster: roster) with { Phase = Side.Enemy };

    /// <summary>An unarmed captain, so no anvil dies to a counter before its follower strikes.</summary>
    private static readonly ValueList<Unit> Bare = ValueList<Unit>.Of(Unarmed);

    private static string UnitOf(Command command) => command switch
    {
        Move m => m.UnitId,
        Attack a => a.UnitId,
        Wait w => w.UnitId,
        _ => "",
    };

    private static readonly IReadOnlySet<string> None = new HashSet<string>();

    /// <summary>The plan the phase would get with every enemy acting in placement order, as before issue 419.</summary>
    private static List<Command> InOrder(BattleState state)
    {
        var plan = new List<Command>();
        foreach (var id in state.UnitsOf(Side.Enemy).Select(u => u.Id).ToList())
        {
            if (state.Outcome.IsOver || state.Find(id) is not { Acted: false } unit)
            {
                continue;
            }

            foreach (var command in EnemyAi.PlanUnit(state, Starter, unit))
            {
                plan.Add(command);
                state = Resolver.Apply(state, Starter, command).Next;
            }
        }

        if (!state.Outcome.IsOver)
        {
            plan.Add(new EndPhase());
        }

        return plan;
    }

    [Fact]
    public void AnEnemyStepsInBesideAPlayerUnitAheadOfItsGroupSoAGroupMatesStrikeIsPinned()
    {
        var state = Enemy(Trio, roster: Bare);
        var pell = state.Find("pell")!;

        var anvil = EnemyAi.Anvil(state, Starter, state.Find("brigand-1")!, None);
        var plan = EnemyAi.Plan(state, Starter);

        Assert.NotNull(anvil);
        Assert.Equal("pell", anvil!.PinnedId);
        Assert.Equal("soldier-1", anvil.FollowerId);
        Assert.True(anvil.Bonus > 0, $"bonus {anvil.Bonus}");
        Assert.Equal(1, anvil.Tile.DistanceTo(pell.At));
        Assert.Equal(anvil.Commands[0], plan[0]);
        Assert.Equal("brigand-1", ((Move)plan[0]).UnitId);

        var board = state;
        var pinned = false;
        foreach (var command in plan)
        {
            if (command is Attack { UnitId: "soldier-1", TargetId: "pell" })
            {
                pinned = Pincer.PinnedBy(board, board.Find("soldier-1")!, board.Find("pell")!)?.Id == "brigand-1";
            }

            board = Resolver.Apply(board, Starter, command).Next;
        }

        Assert.True(pinned, "the soldier strikes pell pinned by the brigand");
    }

    [Fact]
    public void ThePlanReportsTheAnvilsItTookAndNoneWithoutTheHeader()
    {
        var state = Enemy(Trio, roster: Bare);

        var (commands, anvils) = EnemyAi.PlanWithAnvils(state, Starter);

        Assert.Equal(EnemyAi.Plan(state, Starter), commands);
        Assert.Equal(EnemyAi.Anvil(state, Starter, state.Find("brigand-1")!, None), Assert.Single(anvils));
        Assert.Equal(anvils[0].Commands[0], commands[0]);
        Assert.Empty(EnemyAi.PlanWithAnvils(Enemy(Trio, pincer: false, roster: Bare), Starter).Anvils);
    }

    /// <summary>
    /// Issue 495: the Sim's runner counts an enemy anvil when the plan's first command for it is
    /// applied. On the Brackwater sample the chase group anvils (#429); on the Tollgate sample the
    /// rider, the only Aggressive unit, is alone in its group, so no enemy anvil acts there.
    /// </summary>
    [Fact]
    public void TheSimCountsAnvilsOnTheBrackwaterSampleAndNoneOnTheTollgateSample()
    {
        var repo = Directory.GetParent(Content.Fixture.RealContentDirectory())!.FullName;
        MapDefinition Sample(string file) => Ironwake.Content.MapFiles.Load(Path.Combine(repo, "docs", "samples", file), MapFixture.Content);

        var brackwater = Runner.Play(MapFixture.Content, Sample("brackwater_cut_pincer.map"), 1, new HeuristicPlayer());
        var tollgate = Runner.Play(MapFixture.Content, Sample("the_tollgate_pincer.map"), 1, new HeuristicPlayer());

        Assert.True(brackwater.Pins.Anvils > 0, $"anvils {brackwater.Pins.Anvils}");
        Assert.True(brackwater.Pins.EnemyStrikes > 0, $"enemy pinned strikes {brackwater.Pins.EnemyStrikes}");
        Assert.Equal(0, tollgate.Pins.Anvils);
    }

    [Fact]
    public void WithoutTheHeaderNoEnemyIsAnAnvilAndThePhaseIsPlannedInPlacementOrder()
    {
        var state = Enemy(Trio, pincer: false);

        Assert.Null(EnemyAi.Anvil(state, Starter, state.Find("brigand-1")!, None));
        Assert.Equal(InOrder(state), EnemyAi.Plan(state, Starter).ToList());
        Assert.Equal("archer-1", UnitOf(EnemyAi.Plan(state, Starter)[0]));
        Assert.Equal("brigand-1", UnitOf(EnemyAi.Plan(Enemy(Trio), Starter)[0]));
    }

    [Fact]
    public void WithTheHeaderButNoAnvilThePhaseIsPlannedInPlacementOrder()
    {
        var state = Enemy("""
            P captain 4,2
            E archer 8,4 group:yard behavior:hold
            E brigand 1,2 group:field behavior:aggressive
            E soldier 7,2 group:yard behavior:aggressive

            """);

        Assert.Null(EnemyAi.Anvil(state, Starter, state.Find("brigand-1")!, None));
        Assert.Null(EnemyAi.Anvil(state, Starter, state.Find("soldier-1")!, None));
        Assert.Equal(InOrder(state), EnemyAi.Plan(state, Starter).ToList());
    }

    [Fact]
    public void TheFarTileIsCheckedWithTheAnvilAlreadyStandingOnItsTile()
    {
        // Hale can be struck only from 3,2 and 5,2: the gate holds 4,1 and 4,3. The brigand
        // stands on 5,2, so the soldier's far tile is free only once the brigand has moved to 3,2.
        var state = Enemy("""
            P captain 4,2
            E brigand 5,2 group:field behavior:aggressive
            E soldier 7,2 group:field behavior:aggressive
            E soldier 4,1 group:gate behavior:hold
            E soldier 4,3 group:gate behavior:hold

            """);
        var soldier = state.Find("soldier-1")!;

        var anvil = EnemyAi.Anvil(state, Starter, state.Find("brigand-1")!, None);

        Assert.False(state.ReachOf(soldier, Starter).CanEnd(new Coord(5, 2)), "on the board as it stands the brigand holds the far tile");
        Assert.NotNull(anvil);
        Assert.Equal(new Coord(3, 2), anvil!.Tile);
        Assert.Equal("soldier-1", anvil.FollowerId);
    }

    [Fact]
    public void AFollowerPromisedToOneAnvilIsScoredByNoOther()
    {
        var state = Enemy("""
            P captain 4,2
            E brigand 1,2 group:field behavior:aggressive
            E brigand 4,4 group:field behavior:aggressive
            E soldier 7,2 group:field behavior:aggressive

            """);
        var second = state.Find("brigand-2")!;

        var free = EnemyAi.Anvil(state, Starter, second, None);

        Assert.NotNull(free);
        var promised = EnemyAi.Anvil(state, Starter, second, new HashSet<string> { free!.FollowerId });
        Assert.NotEqual(free.FollowerId, promised?.FollowerId);
        Assert.Null(EnemyAi.Anvil(state, Starter, second, new HashSet<string> { "brigand-1", "soldier-1" }));
        Assert.NotNull(EnemyAi.Anvil(state, Starter, state.Find("soldier-1")!, None));
        Assert.Null(EnemyAi.Anvil(state, Starter, state.Find("soldier-1")!, new HashSet<string> { "soldier-1" }));
    }

    [Fact]
    public void ABossIsNeverAnAnvil()
    {
        var state = Enemy("""
            P captain 4,2
            B brigand 1,2 group:field behavior:guard
            E soldier 7,2 group:field behavior:aggressive

            """).Wake("field");
        var boss = state.Units.Single(u => u.IsBoss);

        Assert.Equal(Behavior.Aggressive, state.EffectiveBehavior(boss, Starter));
        Assert.NotNull(EnemyAi.Anvil(state, Starter, boss with { IsBoss = false }, None));
        Assert.Null(EnemyAi.Anvil(state, Starter, boss, None));
    }

    [Fact]
    public void AThroneHolderIsNoAnvilAndStepsOffOnlyToStrike()
    {
        const string units = """
            P captain 4,2
            E brigand 3,0 group:field behavior:aggressive
            E soldier 7,2 group:field behavior:aggressive

            """;
        var seated = BattleState.From(Throne(units, "...T....."), Starter, Bare, 7) with { Phase = Side.Enemy };
        var standing = Enemy(units, roster: Bare);
        var holder = seated.Find("brigand-1")!;

        Assert.True(EnemyAi.HoldsTheThrone(seated, holder));
        Assert.NotNull(EnemyAi.Anvil(standing, Starter, standing.Find("brigand-1")!, None));
        Assert.Null(EnemyAi.Anvil(seated, Starter, holder, None));

        var plan = EnemyAi.Plan(seated, Starter).ToList();
        for (var i = 0; i < plan.Count; i++)
        {
            if (plan[i] is Move { UnitId: "brigand-1" })
            {
                Assert.IsType<Attack>(plan[i + 1]);
            }
        }
    }

    /// <summary>The trio on the player phase before the one <see cref="Enemy"/> plans.</summary>
    private static BattleState Player(bool pincer = true) => BattleFixture.Start(map: Field(pincer, Trio), roster: Bare);

    private static string ThreatText(BattleState state, string unitId)
    {
        var unit = state.Find(unitId)!;
        var lines = Queries.Threats(state, Starter, unit, unit.At)!;
        return Ironwake.Cli.PlaySession.ThreatText(state, Starter, unit, unit.At, lines, Queries.SleepingThreats(state, Starter, unit, unit.At)!, anvils: Queries.Anvils(state, Starter, unit, unit.At));
    }

    /// <summary>
    /// Issue 457: on a <c>pincer: on</c> map <c>threat</c> lists the planner's anvil plan against
    /// the unit, and the plan it lists is the one <see cref="EnemyAi.Anvil"/> returns on the board
    /// the enemy phase starts from: the anvil, its tile, the follower, and the tile across.
    /// </summary>
    [Fact]
    public void TheThreatQueryListsTheAnvilPlanThePlannerWouldTake()
    {
        var state = Player();
        var pell = state.Find("pell")!;
        var enemyPhase = Resolver.Apply(state, Starter, new EndPhase()).Next;

        var line = Assert.Single(Queries.Anvils(state, Starter, pell, pell.At)!);
        var plan = EnemyAi.Anvil(enemyPhase, Starter, enemyPhase.Find("brigand-1")!, None)!;

        Assert.Equal("brigand-1", line.Anvil.Id);
        Assert.Equal(plan.Tile, line.Tile);
        Assert.Equal(plan.FollowerId, line.Follower.Id);
        Assert.Equal(new Coord(2 * pell.At.X - plan.Tile.X, 2 * pell.At.Y - plan.Tile.Y), line.From);
        Assert.Contains($"  Anvil: Brigand could step to {plan.Tile} so Soldier strikes you pinned from {line.From}", ThreatText(state, "pell"));
    }

    /// <summary>Issue 457: an anvil already beside the unit on its tile is printed as holding it, not stepping to it.</summary>
    [Fact]
    public void AnAnvilOnItsOwnTileIsPrintedAsHoldingIt()
    {
        var edge = Trio.Replace("P captain 4,2", "P captain 4,0").Replace("E brigand 1,2", "E brigand 3,0").Replace("E soldier 7,2", "E soldier 7,0");
        var state = BattleFixture.Start(map: Field(true, edge), roster: Bare);
        var pell = state.Find("pell")!;

        var line = Assert.Single(Queries.Anvils(state, Starter, pell, pell.At)!);

        Assert.Equal(line.Anvil.At, line.Tile);
        Assert.Contains($"  Anvil: Brigand could hold {line.Tile} so Soldier strikes you pinned from {line.From}", ThreatText(state, "pell"));
    }

    /// <summary>Issue 457 at dusk: a plan whose follower the player does not see is not printed, as a strike row from it would not be.</summary>
    [Fact]
    public void AtDuskAPlanWithAnUnseenFollowerIsNotPrinted()
    {
        var dark = Field(true, Trio.Replace("E brigand 1,2", "E brigand 3,2")).Replace("pincer: on\n", "pincer: on\ndusk: 2\n");
        var state = BattleFixture.Start(map: dark, roster: Bare);
        var pell = state.Find("pell")!;

        var line = Assert.Single(Queries.Anvils(state, Starter, pell, pell.At)!);

        Assert.Equal("soldier-1", line.Follower.Id);
        Assert.False(Dusk.Seen(state, line.Follower));
        Assert.DoesNotContain("anvil:", ThreatText(state, "pell"));
        Assert.DoesNotContain("\"anvil\":", new Ironwake.Cli.ProtocolSession(Starter, state, TextWriter.Null).Answer("""{"query":"threat","unit":"pell"}"""));
    }

    /// <summary>Issue 457: the anvil line is unpriced, so the threat total reads the same with the header as without it.</summary>
    [Fact]
    public void TheAnvilLineAddsNothingToTheThreatTotal()
    {
        var pincer = ThreatText(Player(), "pell");
        var plain = ThreatText(Player(pincer: false), "pell");

        Assert.Contains("  Anvil: ", pincer);
        Assert.Equal(plain.Split('\n').Single(l => l.StartsWith("  If all land: ", StringComparison.Ordinal)), pincer.Split('\n').Single(l => l.StartsWith("  If all land: ", StringComparison.Ordinal)));
        Assert.Equal(plain, string.Join("\n", pincer.Split('\n').Where(l => !l.StartsWith("  Anvil: ", StringComparison.Ordinal))));
    }

    /// <summary>Issue 457: without the header the query lists no anvil, the text has no anvil line, and the protocol carries no <c>anvils</c>.</summary>
    [Fact]
    public void WithoutTheHeaderTheThreatQueryListsNoAnvil()
    {
        var state = Player(pincer: false);
        var pell = state.Find("pell")!;

        Assert.Empty(Queries.Anvils(state, Starter, pell, pell.At)!);
        Assert.DoesNotContain("anvil", ThreatText(state, "pell"));
        Assert.DoesNotContain("\"anvils\"", new Ironwake.Cli.ProtocolSession(Starter, state, TextWriter.Null).Answer("""{"query":"threat","unit":"pell"}"""));
    }

    /// <summary>Issue 457: the protocol's threat answer carries the anvil plan beside the strikes, and its text carries the line.</summary>
    [Fact]
    public void TheProtocolsThreatAnswerCarriesTheAnvils()
    {
        var state = Player();
        var pell = state.Find("pell")!;
        var line = Assert.Single(Queries.Anvils(state, Starter, pell, pell.At)!);

        var answer = new Ironwake.Cli.ProtocolSession(Starter, state, TextWriter.Null).Answer("""{"query":"threat","unit":"pell"}""");

        Assert.Contains($"\"anvils\":[{{\"anvil\":\"brigand-1\",\"tile\":{{\"x\":{line.Tile.X},\"y\":{line.Tile.Y}}},\"follower\":\"soldier-1\",\"from\":{{\"x\":{line.From.X},\"y\":{line.From.Y}}}}}]", answer);
        Assert.Contains("  Anvil: Brigand could step to ", answer);
    }

    private static MapDefinition Throne(string units, string row0) =>
        Maps.MapFixture.Parse(Field(true, units, "seize").Replace("\n\n.........\n", $"\n\n{row0}\n"), "throne.map");
}
