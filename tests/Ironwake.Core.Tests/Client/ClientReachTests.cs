using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Client;

/// <summary>
/// Issue 533: the enemy reach overlay reads <see cref="Threat.StruckByUnit"/>, the strike set the
/// planner's threat reads, for one inspected enemy or every seen one; a sleeping group draws the
/// reach it would have awake with its wake ring; the unit card carries a player unit's EXP; and the
/// forecast names the EXP a strike earns when it would cross a level.
/// </summary>
public class ClientReachTests
{
    private static ClientSession Open(string map, ulong seed)
    {
        var content = ContentLoader.Load(Fixture.RealContentDirectory());
        var file = MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "maps", map + ".map"), content);
        return new ClientSession(content, BattleState.From(file, content, content.Cast, seed));
    }

    private static ClientSession TurnThree()
    {
        var client = Open("the_tollgate", 113);
        var script = File.ReadAllLines(Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "screenshots", "the_tollgate-113-turn3.script"));
        Ironwake.Client.Script.Apply(client, string.Join("\n", script));
        return client;
    }

    private static BattleUnit AwakeEnemy(ClientSession client) =>
        client.State.UnitsOf(Side.Enemy).First(e => e is not { Behavior: Behavior.Guard, Group: { } g } || client.State.IsAwake(g));

    [Fact]
    public void AClickOnAnEnemyWithNothingSelectedInspectsItsReach()
    {
        var client = Open("the_tollgate", 113);
        var enemy = AwakeEnemy(client);

        Assert.False(client.Select(enemy.At));

        Assert.Equal(enemy.Id, client.Inspected);
        var reach = client.InspectedReach!;
        Assert.False(reach.Asleep);
        Assert.NotEmpty(reach.Tiles);
        Assert.True(Threat.StruckByUnit(client.State, client.Content, enemy).SetEquals(reach.Tiles));
    }

    [Fact]
    public void SelectingOneOfOursOrClearingDropsTheInspectedEnemy()
    {
        var client = Open("the_tollgate", 113);
        client.Select(AwakeEnemy(client).At);

        Assert.True(client.Select(new Coord(6, 11)));
        Assert.Null(client.Inspected);

        client.ClearSelection();
        client.Select(AwakeEnemy(client).At);
        client.ClearSelection();
        Assert.Null(client.Inspected);
        Assert.Null(client.InspectedReach);
    }

    [Fact]
    public void EveryReachTogetherIsTheThreatHatchWhereNoGroupSleeps()
    {
        var client = TurnThree();
        var awake = client.EnemyReaches.Where(r => !r.Asleep).SelectMany(r => r.Tiles).ToHashSet();

        Assert.True(awake.SetEquals(client.EnemyThreat));
    }

    [Fact]
    public void EachReachIsTheStrikeSetTheThreatReads()
    {
        var client = TurnThree();

        Assert.NotEmpty(client.EnemyReaches);
        Assert.All(client.EnemyReaches.Where(r => !r.Asleep), reach =>
            Assert.True(Threat.StruckByUnit(client.State, client.Content, client.State.Find(reach.Id)!).SetEquals(reach.Tiles), reach.Id));
    }

    [Fact]
    public void ASleepingGuardDrawsTheReachItWouldHaveAwakeAndItsWakeRing()
    {
        var client = Open("saltmarsh_ford", 7);
        var sleeper = client.State.UnitsOf(Side.Enemy).First(e => e is { Behavior: Behavior.Guard, Group: { } g } && !client.State.IsAwake(g));
        var woken = client.State.Wake(sleeper.Group!);

        var reach = client.EnemyReaches.Single(r => r.Id == sleeper.Id);

        Assert.True(reach.Asleep);
        Assert.Empty(Threat.StruckByUnit(client.State, client.Content, sleeper));
        Assert.NotEmpty(reach.Tiles);
        Assert.True(Threat.StruckByUnit(woken, client.Content, woken.Find(sleeper.Id)!).SetEquals(reach.Tiles));
        var radius = client.Content.WakeRadius;
        Assert.Contains(sleeper.At, reach.WakeRing);
        Assert.All(reach.WakeRing, at => Assert.Contains(client.State.UnitsOf(Side.Enemy).Where(u => u.Group == sleeper.Group), u => u.At.DistanceTo(at) <= radius));
        var outside = new Coord(sleeper.At.X, sleeper.At.Y + radius + 1);
        if (client.State.UnitsOf(Side.Enemy).Where(u => u.Group == sleeper.Group).All(u => u.At.DistanceTo(outside) > radius))
        {
            Assert.DoesNotContain(outside, reach.WakeRing);
        }
    }

    [Fact]
    public void AnAwakeEnemyHasNoWakeRing()
    {
        var client = Open("the_tollgate", 113);

        Assert.All(client.EnemyReaches.Where(r => !r.Asleep), reach => Assert.Empty(reach.WakeRing));
    }

    [Fact]
    public void AnEnemyTheDarkHidesIsNeitherInspectedNorDrawn()
    {
        var client = Open("brackwater_cut", 7);
        var hidden = client.State.UnitsOf(Side.Enemy).First(enemy => !Dusk.Seen(client.State, enemy));

        client.Select(hidden.At);

        Assert.Null(client.Inspected);
        Assert.Null(client.EnemyReachAt(hidden.At));
        Assert.DoesNotContain(client.EnemyReaches, r => r.Id == hidden.Id);
    }

    [Fact]
    public void TheUnitCardCarriesAPlayerUnitsExpAndNoneForAnEnemy()
    {
        var client = TurnThree();
        var teodor = client.State.Find("teodor")!;

        Assert.Equal(teodor.Unit.Exp, client.Card(teodor.At)!.Exp);
        Assert.Null(client.Card(AwakeEnemy(client).At)!.Exp);
    }

    /// <summary>Turn three with Teodor on 8,7 carrying <paramref name="exp"/> EXP, selected, the Toll Brigand on 7,5 in reach.</summary>
    private static ClientSession TeodorAt(int exp, int? level = null)
    {
        var turn = TurnThree();
        var teodor = turn.State.Find("teodor")!;
        var client = new ClientSession(turn.Content, turn.State.WithUnit(teodor with { Unit = teodor.Unit with { Exp = exp, Level = level ?? teodor.Unit.Level } }));
        Assert.True(client.Select(new Coord(8, 7)));
        return client;
    }

    [Fact]
    public void AStrikeThatWouldCrossALevelNamesItsExp()
    {
        var client = TeodorAt(Unit.MaxExp);
        var hover = client.Hover(new Coord(7, 5)).First();
        var teodor = client.State.Find("teodor")!;
        var target = client.State.Find(hover.TargetId)!;
        var expected = Experience.ForCombat(teodor.Unit.Level, target.Unit.Level, true, hover.Card.Defender.After == 0, target.IsBoss);

        Assert.Equal(expected, hover.Card.LevelUpExp);
        Assert.Equal($"+{expected} EXP, level up", hover.Card.LevelUpLine);
    }

    [Fact]
    public void AStrikeShortOfALevelNamesNoExp()
    {
        var client = TeodorAt(0);

        var card = client.Hover(new Coord(7, 5)).First().Card;

        Assert.Null(card.LevelUpExp);
        Assert.Null(card.LevelUpLine);
    }

    [Fact]
    public void AUnitAtTheLevelCapIsNeverPromisedALevel()
    {
        var client = TeodorAt(Unit.MaxExp, Unit.MaxLevel);

        Assert.Null(client.Hover(new Coord(7, 5)).First().Card.LevelUpExp);
    }
}
