using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// DESIGN.md section 8: the four behaviors, the target score with the crit expectation,
/// the tile rule, the approach rule, and the phase order (issue 10). The Old Mill Road
/// case is the named test for the approach rule: the brigand at 6,5 goes to 3,6.
/// </summary>
public class EnemyAiTests
{
    private static string Yard(string players, string enemies) =>
        $"""
        name: Yard
        size: 8x4
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1

        ........
        ........
        ........
        ........

        units:
        {players}
        {enemies}

        """;

    private static BattleState EnemyPhase(string map, ValueList<Unit>? roster = null) =>
        Start(map: map, roster: roster).Do(new EndPhase());

    private static Unit Target(string id, int hp, int def, int lck, params string[] items) =>
        Recruit(id, new Stats(hp, 7, 0, 6, 8, lck, def, 2, 3), items);

    [Fact]
    public void TheApproachRuleSendsTheBrigandOnOldMillRoadToThreeSix()
    {
        var state = BattleState.From(MapFixture.Parse(MapFixture.OldMillRoad), Starter, ValueList<Unit>.Of(Hale, Wren), 7).Do(new EndPhase());

        var plan = EnemyAi.Plan(state, Starter);

        Assert.Equal(
            new Command[]
            {
                new Wait("archer-1"),
                new Wait("bandit_leader-1"),
                new Move("brigand-1", new Coord(3, 6)),
                new Wait("brigand-1"),
                new Wait("soldier-1"),
                new EndPhase(),
            },
            plan);
    }

    [Fact]
    public void TheApproachTargetsTheNearestPlayerByPathCostAndThreeTilesTie()
    {
        var state = BattleState.From(MapFixture.Parse(MapFixture.OldMillRoad), Starter, ValueList<Unit>.Of(Hale, Wren), 7).Do(new EndPhase());
        var brigand = state.Find("brigand-1")!;
        var weapon = brigand.EquippedWeapon(Starter)!;
        var occupant = (Coord at) => at == brigand.At ? Occupant.None : state.OccupantAt(at, Side.Enemy);

        var toWren = Movement.DistancesTo(state.Map, Starter, EnemyAi.AttackTiles(state, Starter, brigand, weapon, state.Find("wren")!, MovementType.Infantry), MovementType.Infantry, occupant);
        var toHale = Movement.DistancesTo(state.Map, Starter, EnemyAi.AttackTiles(state, Starter, brigand, weapon, state.Find("hale")!, MovementType.Infantry), MovementType.Infantry, occupant);

        Assert.Equal(6, toWren.From(brigand.At));
        Assert.Equal(7, toHale.From(brigand.At));
        Assert.Equal(2, toWren.From(new Coord(3, 6)));
        Assert.Equal(2, toWren.From(new Coord(4, 7)));
        Assert.Equal(2, toWren.From(new Coord(5, 8)));
        Assert.Equal(0, toWren.From(new Coord(3, 8)));
    }

    [Fact]
    public void AnAggressiveUnitWithNoPathToAnyAttackTileWaits()
    {
        var map = """
            name: Walled
            size: 5x3
            win: rout
            turn_limit: 10
            recall: 3
            enemy_level: 1

            ..#..
            ..#..
            ..#..

            units:
            P captain 0,1
            P recruit:wren 0,2
            E brigand 4,1 group:road behavior:aggressive

            """;
        var state = EnemyPhase(map);

        Assert.Equal(new Command[] { new Wait("brigand-1"), new EndPhase() }, EnemyAi.Plan(state, Starter));
    }

    [Fact]
    public void ABossNeverLeavesItsTile()
    {
        var far = EnemyPhase(Yard("P captain 0,1\nP recruit:wren 0,2", "B bandit_leader 7,0 group:g behavior:boss"));
        Assert.Equal(new Command[] { new Wait("bandit_leader-1"), new EndPhase() }, EnemyAi.Plan(far, Starter));

        var adjacent = far.WithUnit(far.Find("hale")! with { At = new Coord(6, 0) });
        Assert.Equal(new Command[] { new Attack("bandit_leader-1", "hale") }, EnemyAi.PlanUnit(adjacent, Starter, adjacent.Find("bandit_leader-1")!));
    }

    private static BattleState Armed(BattleState state, string unitId, params string[] items)
    {
        var unit = state.Find(unitId)!;
        var stacks = items.Select(item => new ItemStack(item, Starter.Weapon(item).Durability));
        return state.WithUnit(unit with { Unit = unit.Unit with { Inventory = new Inventory(ValueList<ItemStack>.From(stacks.ToList())) } });
    }

    [Fact]
    public void ATwoWeaponEnemyAttacksWithTheSlotThatScoresHigherAndCountersWithIt()
    {
        var far = EnemyPhase(Yard("P captain 0,1\nP recruit:wren 0,2", "B bandit_leader 7,0 group:g behavior:boss"));
        var adjacent = far.WithUnit(far.Find("hale")! with { At = new Coord(6, 0) });
        var state = Armed(adjacent, "bandit_leader-1", "steel_axe", "hatchet");
        var leader = state.Find("bandit_leader-1")!;
        var hale = state.Find("hale")!;

        var steel = EnemyAi.Score(state, Starter, leader, leader.At, hale);
        var hatchet = EnemyAi.Score(state, Starter, leader.WithSlotInFront(1), leader.At, hale);
        Assert.True(hatchet > steel, $"hatchet {hatchet} against steel axe {steel}");
        var plan = EnemyAi.PlanUnit(state, Starter, leader);
        Assert.Equal(new Command[] { new Attack("bandit_leader-1", "hale", 1) }, plan);

        var after = state.Do(plan[0]);
        Assert.Equal("hatchet", after.Find("bandit_leader-1")!.EquippedWeapon(Starter)!.Id);

        var playerPhase = after.Do(new EndPhase());
        SideForecast Counter(BattleState board) => Ironwake.Core.Combat.Forecast(
            board.Find("hale")!.ToCombatant(board.Map, Starter),
            board.Find("bandit_leader-1")!.ToCombatant(board.Map, Starter),
            1, board.Scheme).Defender;
        Assert.Equal(Counter(Armed(playerPhase, "bandit_leader-1", "hatchet")), Counter(playerPhase));
        Assert.NotEqual(Counter(Armed(playerPhase, "bandit_leader-1", "steel_axe")), Counter(playerPhase));
    }

    [Fact]
    public void OfTwoWeaponsScoringTheSameTheEquippedSlotStrikesAndNoSlotIsNamed()
    {
        var far = EnemyPhase(Yard("P captain 0,1\nP recruit:wren 0,2", "B bandit_leader 7,0 group:g behavior:boss"));
        var adjacent = far.WithUnit(far.Find("hale")! with { At = new Coord(6, 0) });
        var state = Armed(adjacent, "bandit_leader-1", "hatchet", "hatchet");

        Assert.Equal(new Command[] { new Attack("bandit_leader-1", "hale") }, EnemyAi.PlanUnit(state, Starter, state.Find("bandit_leader-1")!));
    }

    [Fact]
    public void AHoldUnitAttacksInRangeAndNeverMoves()
    {
        var state = EnemyPhase(Yard("P captain 0,1\nP recruit:wren 0,2", "E archer 3,1 group:g behavior:hold"));

        Assert.Equal(new Command[] { new Wait("archer-1"), new EndPhase() }, EnemyAi.Plan(state, Starter));

        var inRange = state.WithUnit(state.Find("hale")! with { At = new Coord(1, 1) });
        Assert.Equal(new Command[] { new Attack("archer-1", "hale") }, EnemyAi.PlanUnit(inRange, Starter, inRange.Find("archer-1")!));
    }

    [Fact]
    public void TheKillOptionBeatsAHigherDamageNonKill()
    {
        var tough = Target("tough", 30, 0, 3, "iron_sword");
        var weak = Target("weak", 30, 6, 3, "iron_sword");
        var state = EnemyPhase(Yard("P captain 0,1\nP recruit:weak 3,0\nP recruit:tough 2,1", "E brigand 3,1 group:g behavior:aggressive"), ValueList<Unit>.Of(Hale, weak, tough));
        state = state.WithUnit(state.Find("weak")! with { Hp = 2 });
        var brigand = state.Find("brigand-1")!;

        var toughSide = Core.Combat.Forecast(brigand.ToCombatant(state.Map, Starter), state.Find("tough")!.ToCombatant(state.Map, Starter), 1, state.Scheme).Attacker;
        var weakSide = Core.Combat.Forecast(brigand.ToCombatant(state.Map, Starter), state.Find("weak")!.ToCombatant(state.Map, Starter), 1, state.Scheme).Attacker;
        Assert.True(toughSide.Damage > weakSide.Damage, "the non-kill must deal more");
        Assert.True(weakSide.Damage >= 2 && toughSide.Damage < 30, "only the weak target dies");

        var nonKill = EnemyAi.Score(state, Starter, brigand, brigand.At, state.Find("tough")!);
        var kill = EnemyAi.Score(state, Starter, brigand, brigand.At, state.Find("weak")!);
        Assert.True(nonKill < EnemyAi.KillBonus - 20, $"non-kill scored {nonKill}");
        Assert.True(kill > nonKill + 50, $"kill {kill} against non-kill {nonKill}");
        Assert.Equal(new Attack("brigand-1", "weak"), EnemyAi.PlanUnit(state, Starter, brigand).Last());
    }

    [Fact]
    public void TiesResolveByLowestUnitId()
    {
        var a = Target("twin-b", 30, 4, 3, "iron_sword");
        var b = Target("twin-a", 30, 4, 3, "iron_sword");
        var state = EnemyPhase(Yard("P captain 0,3\nP recruit:twin-b 3,0\nP recruit:twin-a 3,2", "E brigand 3,1 group:g behavior:aggressive"), ValueList<Unit>.Of(Hale, a, b));
        var brigand = state.Find("brigand-1")!;

        Assert.Equal(
            EnemyAi.Score(state, Starter, brigand, brigand.At, state.Find("twin-a")!),
            EnemyAi.Score(state, Starter, brigand, brigand.At, state.Find("twin-b")!));
        Assert.Equal(new Attack("brigand-1", "twin-a"), EnemyAi.PlanUnit(state, Starter, brigand).Last());
    }

    [Fact]
    public void ATargetAtFifteenPercentCritScoresStrictlyAboveAnOtherwiseIdenticalTargetAtZero()
    {
        var sharp = Recruit("sharp", new Stats(30, 8, 0, 40, 4, 0, 5, 2, 9), "iron_sword");
        var lucky = Recruit("lucky", new Stats(40, 7, 0, 6, 2, 20, 4, 2, 3));
        var plain = Recruit("plain", new Stats(40, 7, 0, 6, 2, 5, 4, 2, 3));
        var state = Start(map: Yard("P captain 0,1\nP recruit:lucky 3,0\nP recruit:plain 3,2", "E brigand 3,1 group:g behavior:aggressive"), roster: ValueList<Unit>.Of(sharp, lucky, plain));
        var attacker = state.Find("sharp")!;
        var against = (string id) => Core.Combat.Forecast(attacker.ToCombatant(state.Map, Starter), state.Find(id)!.ToCombatant(state.Map, Starter), 1, state.Scheme).Attacker;

        Assert.Equal(0, against("lucky").CritChance);
        Assert.Equal(15, against("plain").CritChance);
        Assert.Equal(against("lucky").HitChance, against("plain").HitChance);
        Assert.Equal(against("lucky").Damage, against("plain").Damage);

        var atZero = EnemyAi.Score(state, Starter, attacker, new Coord(3, 1), state.Find("lucky")!);
        var atFifteen = EnemyAi.Score(state, Starter, attacker, new Coord(3, 1), state.Find("plain")!);
        Assert.True(atFifteen > atZero, $"{atFifteen} should exceed {atZero}");
        Assert.Equal(atZero * 1.3 - 3, atFifteen, 6);
    }

    [Fact]
    public void AnAttackLethalOnlyOnACritIsNotAKillAndLosesToAGuaranteedLargerChunk()
    {
        var sharp = Recruit("sharp", new Stats(30, 8, 0, 40, 8, 0, 5, 2, 9), "iron_sword");
        var brittle = Recruit("brittle", new Stats(40, 7, 0, 6, 8, 5, 8, 2, 3));
        var soft = Recruit("soft", new Stats(40, 7, 0, 6, 8, 20, 0, 2, 3));
        var state = Start(map: Yard("P captain 0,1\nP recruit:brittle 3,0\nP recruit:soft 3,2", "E brigand 3,1 group:g behavior:aggressive"), roster: ValueList<Unit>.Of(sharp, brittle, soft));
        var attacker = state.Find("sharp")!;
        state = state.WithUnit(state.Find("brittle")! with { Hp = 12 });
        var brittleSide = Core.Combat.Forecast(attacker.ToCombatant(state.Map, Starter), state.Find("brittle")!.ToCombatant(state.Map, Starter), 1, state.Scheme).Attacker;

        Assert.Equal(5, brittleSide.Damage);
        Assert.Equal(15, brittleSide.CritChance);
        Assert.True(brittleSide.Damage * Core.Combat.CritMultiplier >= 12, "a crit is lethal");
        Assert.False(brittleSide.Doubles);

        var critOnly = EnemyAi.Score(state, Starter, attacker, new Coord(3, 1), state.Find("brittle")!);
        var chunk = EnemyAi.Score(state, Starter, attacker, new Coord(3, 1), state.Find("soft")!);
        Assert.True(critOnly < EnemyAi.KillBonus, $"crit-only lethality priced as a kill: {critOnly}");
        Assert.True(chunk > critOnly, $"{chunk} should exceed {critOnly}");
    }

    [Fact]
    public void ExpectedDamageIsCappedAtTheTargetsRemainingHp()
    {
        var state = EnemyPhase(Yard("P captain 3,0\nP recruit:wren 0,3", "E brigand 3,1 group:g behavior:aggressive"));
        var brigand = state.Find("brigand-1")!;
        var hale = state.Find("hale")!;
        var side = Core.Combat.Forecast(brigand.ToCombatant(state.Map, Starter), hale.ToCombatant(state.Map, Starter), 1, state.Scheme).Attacker;
        var hit = Core.Combat.HitProbability(side.HitChance, state.Scheme);
        var fullDealt = Math.Min(hale.Hp, side.Damage * (1 + 2 * side.CritChance / 100.0) * (side.Doubles ? 2 : 1));
        Assert.True(fullDealt > 1);

        var full = EnemyAi.Score(state, Starter, brigand, brigand.At, hale);
        var wounded = state.WithUnit(hale with { Hp = 1 });
        var score = EnemyAi.Score(wounded, Starter, brigand, brigand.At, wounded.Find("hale")!);

        Assert.True(full < EnemyAi.KillBonus);
        Assert.Equal(EnemyAi.KillBonus + (1 - fullDealt) * hit, score - full, 6);
    }

    [Fact]
    public void TheTileRulePrefersTerrainAvoidThenFewestPlayerUnitsThatCanReachIt()
    {
        var map = """
            name: Copse
            size: 8x4
            win: rout
            turn_limit: 10
            recall: 3
            enemy_level: 1

            ........
            ...^....
            ........
            ........

            units:
            P captain 0,0
            P recruit:wren 3,2
            E brigand 6,1 group:g behavior:aggressive

            """;
        var state = EnemyPhase(map);

        var plan = EnemyAi.PlanUnit(state, Starter, state.Find("brigand-1")!);

        Assert.Equal(new Command[] { new Move("brigand-1", new Coord(3, 1)), new Attack("brigand-1", "wren") }, plan);
    }

    [Fact]
    public void AHealerIsWorthFiveMore()
    {
        var stats = new Stats(20, 7, 0, 6, 8, 5, 4, 2, 3);
        var mender = new Unit("mender", "mender", "chaplain", 1, 0, stats, Stats.Zero, Inventory.Empty.Add(new ItemStack("salve", 8)), ValueList<string>.Empty);
        var bystander = Recruit("bystander", stats);
        var state = EnemyPhase(Yard("P captain 0,3\nP recruit:mender 3,0\nP recruit:bystander 3,2", "E brigand 3,1 group:g behavior:aggressive"), ValueList<Unit>.Of(Hale, mender, bystander));
        var brigand = state.Find("brigand-1")!;

        Assert.True(EnemyAi.IsHealer(state.Find("mender")!, Starter));
        Assert.False(EnemyAi.IsHealer(state.Find("bystander")!, Starter));
        Assert.False(EnemyAi.IsHealer(state.Find("hale")!, Starter));
        var healer = EnemyAi.Score(state, Starter, brigand, brigand.At, state.Find("mender")!);
        var other = EnemyAi.Score(state, Starter, brigand, brigand.At, state.Find("bystander")!);
        Assert.Equal(EnemyAi.HealerBonus, healer - other, 6);
        Assert.Equal(new Attack("brigand-1", "mender"), EnemyAi.PlanUnit(state, Starter, brigand).Last());
    }

    [Fact]
    public void EnemiesActInAscendingIdOrderEachOnTheBoardTheLastOneLeft()
    {
        var state = EnemyPhase(Yard("P captain 0,1\nP recruit:wren 7,3", "E soldier 7,0 group:g behavior:aggressive\nE brigand 6,0 group:g behavior:aggressive"));

        var plan = EnemyAi.Plan(state, Starter).ToList();

        var ids = plan.OfType<Move>().Select(m => m.UnitId).ToList();
        Assert.Equal(new[] { "brigand-1", "soldier-1" }, ids);
        var working = state;
        foreach (var command in plan)
        {
            working = working.Do(command);
        }

        Assert.Equal(Side.Player, working.Phase);
    }

    [Fact]
    public void AFullEnemyPhaseOnTheSampleMapReplaysIdenticallyForTheSameSeed()
    {
        var map = MapFixture.Parse(MapFixture.OldMillRoad);
        for (var seed = 1; seed <= 5; seed++)
        {
            var (a, planA) = PlayOneTurn(map, (ulong)seed);
            var (b, planB) = PlayOneTurn(map, (ulong)seed);
            Assert.Equal(planA, planB);
            Assert.Equal(a, b);
        }
    }

    private static (string Canonical, ValueList<Command> Plan) PlayOneTurn(MapDefinition map, ulong seed)
    {
        var state = BattleState.From(map, Starter, ValueList<Unit>.Of(Hale, Wren), seed);
        var random = new Random((int)seed);
        while (state.Phase == Side.Player)
        {
            var legal = Resolver.Legal(state, Starter).ToList();
            state = state.Do(legal[random.Next(legal.Count)]);
        }

        var plan = EnemyAi.Plan(state, Starter);
        foreach (var command in plan)
        {
            state = state.Do(command);
        }

        return (state.Canonical(), plan);
    }
}
