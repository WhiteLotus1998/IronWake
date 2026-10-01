using Ironwake.Content;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Issue 217: <see cref="Queries.Threats"/> answers what the coming enemy phase could do
/// to a player unit on a tile, one line per enemy, each the strike the planner itself
/// would make on that unit, so the printed weapon and numbers are the ones that come.
/// </summary>
public sealed class ThreatQueryTests
{
    private const string Yard = """
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
        P captain 0,1
        P recruit:wren 0,3
        {0}

        """;

    private static BattleState BulwarkTrial()
    {
        var map = MapFiles.Load(Path.Combine(Ironwake.Core.Tests.Content.Fixture.RealContentDirectory(), "trials", "bulwark_trial.map"), Starter);
        return BattleState.From(map, Starter, Starter.Cast, 12);
    }

    private static BattleState Tollgate(ulong seed = 151)
    {
        var map = MapFiles.Load(Path.Combine(MapFixture.MapsDirectory, "the_tollgate.map"), Starter);
        return BattleState.From(map, Starter, Starter.Cast, seed);
    }

    /// <summary>
    /// The enemy phase's first attack on the board as the query sees it: each named
    /// enemy's strike in the plan, with the forecast the enemy phase reads before it.
    /// </summary>
    private static (Coord From, int Slot, CombatForecast Forecast) Executed(BattleState playerPhase, string enemyId, string targetId)
    {
        var working = playerPhase.Do(new EndPhase());
        foreach (var command in EnemyAi.Plan(working, Starter))
        {
            if (command is Attack attack && attack.UnitId == enemyId)
            {
                Assert.Equal(targetId, attack.TargetId);
                var enemy = working.Find(enemyId)!;
                var slot = attack.Slot ?? enemy.EquippedSlot(Starter);
                var forecast = Queries.Forecast(working, Starter, enemy, working.Find(targetId)!, enemy.At, attack.Slot)!;
                return (enemy.At, slot, forecast);
            }

            working = working.Do(command);
        }

        throw new Xunit.Sdk.XunitException($"{enemyId} did not attack {targetId}");
    }

    /// <summary>Issue 402: a sleeping group's members are an immutable list, so two answers with the same members are equal.</summary>
    [Fact]
    public void SleepingThreatsCompareByTheirMembers()
    {
        var start = Tollgate();
        var members = start.UnitsOf(Side.Enemy).Take(2);

        Assert.Equal(new SleepingThreat("keep", ValueList<BattleUnit>.From(members)), new SleepingThreat("keep", ValueList<BattleUnit>.From(members.ToList())));
    }

    /// <summary>
    /// Issue 402: <see cref="Queries.StrikeForecast"/> prices the planner's strike, and a strike
    /// the forecast cannot price is a broken
    /// invariant that throws naming both units and the tile. Here the strike names a slot the enemy does not have.
    /// </summary>
    [Fact]
    public void AStrikeWithNoForecastThrowsNamingTheStrike()
    {
        var start = Tollgate().WithoutUnit("toll_warden-1").WithoutUnit("archer-1").Wake("keep");
        var captain = start.Find("captain")!;
        var door = new Coord(6, 2);
        var standing = start.WithUnit(captain with { At = door });
        var line = Assert.Single(Queries.Threats(standing, Starter, standing.Find("captain")!, door)!);
        var boss = standing.Find("bandit_leader-1")!;
        var target = standing.Find("captain")!;

        Assert.Equal(line.Forecast, Queries.StrikeForecast(standing, Starter, boss, target, new EnemyStrike(line.From, line.Slot)));
        var error = Assert.Throws<InvalidOperationException>(() => Queries.StrikeForecast(standing, Starter, boss, target, new EnemyStrike(boss.At, 99)));
        Assert.Contains($"bandit_leader-1 on captain from {boss.At}", error.Message);
    }

    /// <summary>
    /// Chat's seed-151 board on the Tollgate, turn 8: the warden and archer-1 dead, the
    /// captain at the door at 6,2 beside the boss. The query names the Steel Axe, and the
    /// enemy phase swings it from the same tile at the same numbers.
    /// </summary>
    [Fact]
    public void TheThreatLineIsTheStrikeTheEnemyPhaseMakes()
    {
        var start = Tollgate().WithoutUnit("toll_warden-1").WithoutUnit("archer-1").Wake("keep");
        var captain = start.Find("captain")!;
        var door = new Coord(6, 2);
        var standing = start.WithUnit(captain with { At = door });

        var lines = Queries.Threats(standing, Starter, standing.Find("captain")!, door)!;

        var line = Assert.Single(lines);
        Assert.Equal("bandit_leader-1", line.Enemy.Id);
        Assert.Equal("steel_axe", line.Weapon.Id);
        var executed = Executed(standing, "bandit_leader-1", "captain");
        Assert.Equal(executed.From, line.From);
        Assert.Equal(executed.Slot, line.Slot);
        Assert.Equal(executed.Forecast, line.Forecast);
    }

    /// <summary>
    /// Chat's seed-151 board on turn 6: the woods cleared, Teodor at 6,3 below the door, the keep awake. The
    /// archer at range 2, the boss's thrown Toll Axe, and the warden all strike, and the
    /// query asked from 6,3 before the move equals the query on the tile after it.
    /// </summary>
    [Fact]
    public void AThreatFromATileListsEveryEnemyThatStrikesThereWithTheWeaponItWouldSwing()
    {
        var start = Tollgate().WithoutUnit("toll_brigand-1").WithoutUnit("archer-2").Wake("keep");
        var teodor = start.Find("teodor")!;
        var below = new Coord(6, 3);
        var near = start.WithUnit(teodor with { At = new Coord(6, 4) });

        var asked = Queries.Threats(near, Starter, near.Find("teodor")!, below)!;
        var standing = near.WithUnit(near.Find("teodor")! with { At = below });
        var there = Queries.Threats(standing, Starter, standing.Find("teodor")!, below)!;

        Assert.Equal(new[] { "archer-1", "bandit_leader-1", "toll_warden-1" }, asked.Select(l => l.Enemy.Id));
        Assert.Equal("toll_axe", asked.Single(l => l.Enemy.Id == "bandit_leader-1").Weapon.Id);
        Assert.Equal(there.Select(l => (l.Enemy.Id, l.From, l.Slot, l.Forecast)), asked.Select(l => (l.Enemy.Id, l.From, l.Slot, l.Forecast)));
        Assert.Equal(asked.Sum(l => l.Forecast.Attacker.Damage), asked.Sum(l => l.IfAllLand));
    }

    [Fact]
    public void AHoldUnitIsListedOnlyWhenItStrikesFromItsOwnTile()
    {
        var state = Start(map: string.Format(Yard, "E soldier 5,1 group:y behavior:hold"));
        var hale = state.Find("hale")!;

        var beside = Queries.Threats(state, Starter, hale, new Coord(4, 1))!;
        var twoAway = Queries.Threats(state, Starter, hale, new Coord(3, 1))!;

        var line = Assert.Single(beside);
        Assert.Equal(new Coord(5, 1), line.From);
        Assert.Empty(twoAway);
    }

    /// <summary>
    /// A Guard group asleep on the board the unit would leave is not listed; the same
    /// group is listed from a tile whose proximity certainly wakes it, and walks to strike.
    /// </summary>
    [Fact]
    public void ASleepingGroupIsNotListedAndAGroupTheTileWakesIs()
    {
        var map = string.Format(Yard, "E soldier 7,1 group:y behavior:guard");
        var state = Start(map: map);
        var hale = state.Find("hale")!;

        var asleep = Queries.Threats(state, Starter, hale, new Coord(2, 1))!;
        var woken = Queries.Threats(state, Starter, hale, new Coord(3, 1))!;

        Assert.Empty(asleep);
        var line = Assert.Single(woken);
        Assert.Equal("soldier-1", line.Enemy.Id);
        Assert.NotEqual(new Coord(7, 1), line.From);
    }

    [Fact]
    public void AnEnemyWithNoWeaponIsNotListedAndATileOutOfReachAnEnemyOrTheEnemyPhaseIsRefused()
    {
        var state = Start(map: string.Format(Yard, "E soldier 5,1 group:y behavior:hold"));
        var hale = state.Find("hale")!;
        var disarmed = state.WithUnit(state.Find("soldier-1")! with { Unit = state.Find("soldier-1")!.Unit with { Inventory = Inventory.Empty } });

        Assert.Empty(Queries.Threats(disarmed, Starter, disarmed.Find("hale")!, new Coord(4, 1))!);
        Assert.Null(Queries.Threats(state, Starter, hale, new Coord(7, 3)));
        Assert.Null(Queries.Threats(state, Starter, state.Find("soldier-1")!, new Coord(5, 1)));
        var enemyPhase = state.Do(new EndPhase());
        Assert.Null(Queries.Threats(enemyPhase, Starter, enemyPhase.Find("hale")!, enemyPhase.Find("hale")!.At));
    }

    private const string Lane = """
        name: Lane
        size: 16x4
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1
        {0}
        ................
        ................
        ................
        ................

        units:
        P captain 0,1
        P recruit:wren 0,3
        {1}

        events:
        arrival turn 1 enemy spawn soldier 7,0 group:n behavior:aggressive

        """;

    /// <summary>
    /// DECISIONS/0045: an enemy an unannounced event brings is invisible to the query, so
    /// the board it reads does not carry it, even though the resolver spawns it at the
    /// start of the enemy phase the query prices.
    /// </summary>
    [Fact]
    public void AnUnannouncedSpawnIsNotPriced()
    {
        var state = Start(map: string.Format(Lane, "", "E soldier 15,3 group:y behavior:hold"));
        var hale = state.Find("hale")!;

        Assert.Empty(Queries.Threats(state, Starter, hale, new Coord(4, 1))!);
        Assert.Contains(state.Do(new EndPhase()).Units, u => u.At == new Coord(7, 0));
    }

    /// <summary>
    /// Issue 248: on an <c>announce: on</c> map the spawn is a promise, so the query prices
    /// it with numbers like any awake enemy and names the tile it arrives on.
    /// </summary>
    [Fact]
    public void AnAnnouncedSpawnIsPricedAndMarkedWithWhereItArrives()
    {
        var state = Start(map: string.Format(Lane, "announce: on\n", "E soldier 15,3 group:y behavior:hold"));
        var hale = state.Find("hale")!;

        var line = Assert.Single(Queries.Threats(state, Starter, hale, new Coord(4, 1))!);

        Assert.Equal(new Coord(7, 0), line.Arrives);
        Assert.StartsWith("soldier-", line.Enemy.Id);
        Assert.True(line.IfAllLand > 0);
    }

    /// <summary>A held spawn tile spends the event (DECISIONS/0036), so the unit standing on it is not threatened by the arrival.</summary>
    [Fact]
    public void AnAnnouncedSpawnWhoseTileTheUnitHoldsIsNotListed()
    {
        var state = Start(map: string.Format(Lane, "announce: on\n", "E soldier 15,3 group:y behavior:hold"));
        var standing = state.WithUnit(state.Find("hale")! with { At = new Coord(6, 0) });

        Assert.Empty(Queries.Threats(standing, Starter, standing.Find("hale")!, new Coord(7, 0))!);
    }

    /// <summary>
    /// Issue 248, shape one: a Guard group asleep on the board the query reads is named,
    /// with the members that could strike the tile were it awake (the archer on 7,3 cannot
    /// reach 2,1, issue 454); a group too
    /// far to reach it is not; a group the tile certainly wakes is priced instead.
    /// </summary>
    [Fact]
    public void ASleepingGroupThatCouldReachTheTileIsNamedWithItsMembers()
    {
        var state = Start(map: string.Format(Yard, "E soldier 7,1 group:y behavior:guard\nE archer 7,3 group:y behavior:guard"));
        var hale = state.Find("hale")!;

        var asleep = Assert.Single(Queries.SleepingThreats(state, Starter, hale, new Coord(2, 1))!);
        Assert.Equal("y", asleep.Group);
        Assert.Equal(new[] { "soldier-1" }, asleep.Members.Select(m => m.Id));
        Assert.Empty(Queries.Threats(state, Starter, hale, new Coord(2, 1))!);
        Assert.Empty(Queries.SleepingThreats(state, Starter, hale, new Coord(3, 1))!);
        Assert.NotEmpty(Queries.Threats(state, Starter, hale, new Coord(3, 1))!);
    }

    /// <summary>
    /// Issue 454: the group is named for the members that could strike the tile, and only
    /// those are listed; a member that could never reach it (the Critic's archer on 14,4 of
    /// Harrow Weir) is left off the row.
    /// </summary>
    [Fact]
    public void ASleepingGroupListsOnlyTheMembersThatCouldStrikeTheTile()
    {
        var state = Start(map: string.Format(Lane, "", "E soldier 8,1 group:y behavior:guard\nE archer 15,3 group:y behavior:guard"));
        var hale = state.Find("hale")!;

        var asleep = Assert.Single(Queries.SleepingThreats(state, Starter, hale, new Coord(3, 1))!);
        Assert.Equal("y", asleep.Group);
        Assert.Equal(new[] { "soldier-1" }, asleep.Members.Select(m => m.Id));
    }

    [Fact]
    public void ASleepingGroupTooFarToStrikeTheTileIsNotNamed()
    {
        var state = Start(map: string.Format(Lane, "", "E soldier 15,3 group:far behavior:guard"));
        var hale = state.Find("hale")!;

        Assert.Empty(Queries.SleepingThreats(state, Starter, hale, new Coord(1, 1))!);
        Assert.Null(Queries.SleepingThreats(state, Starter, hale, new Coord(9, 3)));
    }

    /// <summary>
    /// Issue 253, Chat's board on the Bulwark trial: from 5,3 in the corridor the only open
    /// tile beside the captain is 4,3, and both western brigands can reach it. Both lines
    /// stay, each striking from 4,3, and the total counts one of them, since one tile holds
    /// one brigand.
    /// </summary>
    [Fact]
    public void TheTotalCountsOneAttackerPerStrikeTile()
    {
        var state = BulwarkTrial();
        var corridor = new Coord(5, 3);

        var lines = Queries.Threats(state, Starter, state.Find("captain")!, corridor)!;

        Assert.Equal(new[] { "brigand-1", "brigand-2" }, lines.Select(l => l.Enemy.Id));
        Assert.All(lines, l => Assert.Equal(new Coord(4, 3), l.From));
        Assert.All(lines, l => Assert.Equal(new[] { new Coord(4, 3) }, l.Tiles!.Value));
        Assert.Equal(lines.Max(l => l.IfAllLand), Queries.IfAllLand(lines));
        Assert.True(Queries.IfAllLand(lines) < lines.Sum(l => l.IfAllLand));
    }

    /// <summary>On an open field every attacker finds a tile of its own, so the total is the sum of the lines.</summary>
    [Fact]
    public void AttackersWithTilesOfTheirOwnAreAllCounted()
    {
        var state = Start(map: string.Format(Yard, "E soldier 5,1 group:y behavior:aggressive\nE soldier 5,2 group:y behavior:aggressive"));
        var hale = state.Find("hale")!;

        var lines = Queries.Threats(state, Starter, hale, new Coord(3, 1))!;

        Assert.Equal(2, lines.Count);
        Assert.Equal(lines.Sum(l => l.IfAllLand), Queries.IfAllLand(lines));
    }

    /// <summary>
    /// The assignment is a matching, not first come first served: an attacker seated on a
    /// tile moves to its other tile when a later one can use only that tile, and when two
    /// share a single tile the harder hitter is the one counted.
    /// </summary>
    [Fact]
    public void TheTotalIsTheWorstCaseOverAssignmentsOfAttackersToTiles()
    {
        var state = BulwarkTrial();
        var line = Queries.Threats(state, Starter, state.Find("captain")!, new Coord(5, 3))![0];
        var a = new Coord(4, 3);
        var b = new Coord(6, 3);
        var hard = line with { Forecast = line.Forecast with { Attacker = line.Forecast.Attacker with { Damage = 10 } } };
        var soft = line with { Forecast = line.Forecast with { Attacker = line.Forecast.Attacker with { Damage = 3 } } };
        var hits = hard.IfAllLand;

        Assert.Equal(hits + soft.IfAllLand, Queries.IfAllLand(new[] { hard with { Tiles = ValueList<Coord>.Of(a, b) }, soft with { Tiles = ValueList<Coord>.Of(a) } }));
        Assert.Equal(hits, Queries.IfAllLand(new[] { soft with { Tiles = ValueList<Coord>.Of(a) }, hard with { Tiles = ValueList<Coord>.Of(a) } }));
        Assert.Equal(0, Queries.IfAllLand(Array.Empty<ThreatLine>()));
    }
    /// <summary>
    /// The Tollgate with the door open (the warden and archer-1 dead), the captain on 6,3 and
    /// Pell on 5,2, both unmoved, each with a path onto the throne at 7,1 beside the boss.
    /// </summary>
    private static BattleState TollgateDoorOpen()
    {
        var start = Tollgate(97).WithoutUnit("toll_warden-1").WithoutUnit("archer-1");
        start = start.WithUnit(start.Find("captain")! with { At = new Coord(6, 3) });
        return start.WithUnit(start.Find("pell")! with { At = new Coord(5, 2) });
    }

    /// <summary>
    /// Issue 356: the captain's move onto a Seize throne wins the map, so no enemy phase
    /// follows it, and <c>threat</c> says so instead of <c>no enemy can strike it</c>, which
    /// read as an all-clear with the boss adjacent (Chat's seed 97).
    /// </summary>
    [Fact]
    public void AThreatOnATileWhoseMoveWinsTheMapSaysSo()
    {
        var state = TollgateDoorOpen();
        var captain = state.Find("captain")!;
        var throne = new Coord(7, 1);

        Assert.True(Queries.MoveWins(state, Starter, captain, throne));
        var text = Ironwake.Cli.PlaySession.ThreatText(state, Starter, captain, throne, Queries.Threats(state, Starter, captain, throne)!, Queries.SleepingThreats(state, Starter, captain, throne)!, Queries.Unseeing(state, Starter, captain, throne), Queries.MoveWins(state, Starter, captain, throne));
        Assert.Equal("Threat on Alder Fenn at 7,1 (Gate): this move wins the map", text);
    }

    /// <summary>
    /// Issue 356's falsifier: a recruit on the throne wins nothing, so the query prices the
    /// boss beside it as before, and the captain's own tile off the throne is no win either.
    /// </summary>
    [Fact]
    public void ANonCaptainOnTheThroneStillGetsTheEnemyList()
    {
        var state = TollgateDoorOpen();
        var pell = state.Find("pell")!;
        var throne = new Coord(7, 1);

        Assert.False(Queries.MoveWins(state, Starter, pell, throne));
        Assert.False(Queries.MoveWins(state, Starter, state.Find("captain")!, new Coord(6, 2)));
        var lines = Queries.Threats(state, Starter, pell, throne)!;
        Assert.Contains(lines, line => line.Enemy.Id == "bandit_leader-1");
        var text = Ironwake.Cli.PlaySession.ThreatText(state, Starter, pell, throne, lines, Queries.SleepingThreats(state, Starter, pell, throne)!, Queries.Unseeing(state, Starter, pell, throne), Queries.MoveWins(state, Starter, pell, throne));
        Assert.StartsWith("Threat on Pell at 7,1 (Gate):\n  Bandit Leader from ", text);
    }
}
