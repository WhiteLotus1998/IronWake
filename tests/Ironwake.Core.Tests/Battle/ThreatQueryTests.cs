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
}
