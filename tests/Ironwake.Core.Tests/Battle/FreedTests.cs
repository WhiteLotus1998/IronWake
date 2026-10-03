using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;
using Ironwake.Sim;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The bond (issue 750): on a map with a <c>freed: x,y by group</c> header, the enemy placed on
/// the tile leaves the board when a boss of the group falls, one <see cref="UnitFreed"/>, not a
/// kill; killed first, she is an ordinary kill and the board records it.
/// </summary>
public class FreedTests
{
    private const string Bound = "brigand-1";
    private const string Boss = "bandit_leader-1";

    private static string Field(string header = "freed: 1,1 by keep", bool archer = true) =>
        $"""
        name: Field
        size: 7x5
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1
        {header}

        .......
        .......
        .......
        .......
        .......

        units:
        P captain 1,2
        P recruit:wren 0,4
        B bandit_leader 2,2 group:keep behavior:boss
        E brigand 1,1 group:hunt behavior:hold
        {(archer ? "E archer 6,4 group:keep behavior:hold" : "")}

        """.Replace("\n\n\n", "\n\n").Replace("\n\n\n", "\n\n");

    /// <summary>The field with the boss and the bound brigand at 1 HP.</summary>
    private static BattleState Wounded(ulong seed, string header = "freed: 1,1 by keep", bool archer = true)
    {
        var state = BattleFixture.Start(seed, map: Field(header, archer));
        state = state.WithUnit(state.Find(Boss)! with { Hp = 1 });
        return state.WithUnit(state.Find(Bound)! with { Hp = 1 });
    }

    /// <summary>The captain's strike on <paramref name="target"/>, on the first seed from 1 on which it kills.</summary>
    private static ApplyResult Kill(string target, string header = "freed: 1,1 by keep", bool archer = true)
    {
        for (ulong seed = 1; seed < 200; seed++)
        {
            var result = Wounded(seed, header, archer).Try(new Attack("hale", target));
            Assert.True(result.Accepted, result.Rejection?.Message);
            if (result.Next.Find(target) is null)
            {
                return result;
            }
        }

        throw new InvalidOperationException($"no seed under 200 kills {target}");
    }

    [Fact]
    public void WhenTheBossFallsTheBoundEnemyIsFreedAfterHisDeath()
    {
        var result = Kill(Boss);

        Assert.Null(result.Next.Find(Bound));
        var died = result.Events.ToList().FindIndex(e => e is UnitDied { UnitId: Boss });
        var freed = result.Events.ToList().FindIndex(e => e is UnitFreed { UnitId: Bound });
        Assert.True(died >= 0 && freed > died);
        Assert.Equal(BondFate.Freed, result.Next.Bond);
    }

    [Fact]
    public void AFreeingIsNotAKillAndGivesNoExp()
    {
        var result = Kill(Boss);
        var without = Kill(Boss, header: "");

        Assert.DoesNotContain(result.Events, e => e is UnitDied { UnitId: Bound });
        Assert.Single(result.Events.OfType<UnitDied>());
        Assert.Equal(
            without.Events.OfType<ExpGained>().Sum(x => x.Amount),
            result.Events.OfType<ExpGained>().Sum(x => x.Amount));
    }

    [Fact]
    public void TheBoundEnemyIsNeverFreedOtherwise()
    {
        var without = Kill(Boss, header: "");
        Assert.NotNull(without.Next.Find(Bound));
        Assert.Null(without.Next.Bond);

        var state = Wounded(1).Do(new EndPhase());
        Assert.NotNull(state.Find(Bound));
        Assert.Null(state.Bond);
    }

    [Fact]
    public void AFreedEnemyIsGoneForRoutSoTheBossesDeathWinsWhenSheIsTheLast()
    {
        var result = Kill(Boss, archer: false);

        Assert.Empty(result.Next.UnitsOf(Side.Enemy));
        Assert.Equal(BattleResult.Won, result.Next.Outcome.Result);
    }

    [Fact]
    public void KillingTheBoundEnemyFirstIsAKillAndTheBoardRecordsIt()
    {
        var result = Kill(Bound);

        Assert.Contains(result.Events, e => e is UnitDied { UnitId: Bound });
        Assert.DoesNotContain(result.Events, e => e is UnitFreed);
        Assert.True(result.Events.OfType<ExpGained>().Sum(x => x.Amount) > 0);
        Assert.Equal(BondFate.Fell, result.Next.Bond);
        Assert.Contains("bond fell\n", result.Next.Canonical());
    }

    [Fact]
    public void ARecallBringsTheFreedBackUnbound()
    {
        var result = Kill(Boss);

        var back = result.Next.Try(new Recall(0));

        Assert.True(back.Accepted, back.Rejection?.Message);
        Assert.NotNull(back.Next.Find(Bound));
        Assert.Null(back.Next.Bond);
    }

    [Fact]
    public void TheBondPrintsOnTheBoardThreatAndTheForecastsOfTheBossAndTheBound()
    {
        var state = Wounded(1);
        var names = UnitNames.Of(state, Starter);
        var bond = $"{names[Bound]} is bound to {names[Boss]}: freed when {names[Boss]} falls";
        var hale = state.Find("hale")!;

        Assert.Equal(bond, Freed.Line(state, names));
        Assert.Contains(bond + "\n", MapRenderer.Render(state, Starter));
        Assert.Contains("  " + bond, PlaySession.BreakLines(state, Starter, hale, state.Find(Boss)!, names));
        Assert.Contains("  " + bond, PlaySession.BreakLines(state, Starter, hale, state.Find(Bound)!, names));
        Assert.DoesNotContain("  " + bond, PlaySession.BreakLines(state, Starter, hale, state.Find("archer-1")!, names));
        var threat = PlaySession.ThreatText(state, Starter, hale, hale.At, Queries.Threats(state, Starter, hale, hale.At)!, Queries.SleepingThreats(state, Starter, hale, hale.At)!);
        Assert.Contains("  " + bond, threat);

        var plain = BattleFixture.Start(1, map: Field(""));
        Assert.Null(Freed.Line(plain, UnitNames.Of(plain, Starter)));
    }

    [Fact]
    public void TheBondPrintsNoMoreOnceSheHasLeft()
    {
        var result = Kill(Boss);

        Assert.Null(Freed.Line(result.Next, UnitNames.Of(result.Next, Starter)));
    }

    [Theory]
    [InlineData("freed: 1,1", "freed: needs the bound enemy's tile and the boss's group")]
    [InlineData("freed: 1,1 to keep", "freed: needs the bound enemy's tile and the boss's group")]
    [InlineData("freed: 3,3 by keep", "freed names 3,3 but no E line places an enemy there")]
    [InlineData("freed: 1,2 by keep", "freed names 1,2 but no E line places an enemy there")]
    [InlineData("freed: 2,2 by keep", "is a boss; a boss is never bound")]
    [InlineData("freed: 1,1 by hunt", "freed: group 'hunt' has no boss, placed or spawned")]
    [InlineData("freed: 1,1 by nobody", "freed: group 'nobody' has no boss, placed or spawned")]
    public void AFreedHeaderMustNameANonBossEnemyAndAGroupWithABoss(string header, string message)
    {
        var error = Assert.Throws<MapException>(() => MapFixture.Parse(Field(header)));

        Assert.Contains(message, error.Message);
    }

    [Fact]
    public void TheFreedHeaderRoundTripsAndTheFinaleSampleBindsTheHunterToTheSpawnedLord()
    {
        var map = MapFixture.Parse(Field());
        Assert.Equal(new FreedBond(new Coord(1, 1), "keep"), map.Bond);
        Assert.Contains("freed: 1,1 by keep\n", MapFormat.Write(map, Starter));

        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var path = Path.Combine(repo, "docs", "samples", "ironwake_keep_finale.map");
        var real = Starter;
        var sample = MapFiles.Load(path, real);
        Assert.Equal(new FreedBond(new Coord(7, 6), "lord"), sample.Bond);
        Assert.Equal(File.ReadAllText(path).Replace("\r\n", "\n"), MapFormat.Write(sample, real));

        var opening = BattleState.From(sample, real, FinaleRun.Roster(real, FinaleRun.Company.Full, FinaleRun.DefaultLevel), 1);
        var names = UnitNames.Of(opening, real);
        Assert.Equal("Sworn Hunter is bound to Hask: freed when Hask falls", Freed.Line(opening, names));
    }

    [Fact]
    public void TheFreedEventHasAProtocolShapeAndTheConsoleSaysSo()
    {
        Assert.Equal(
            """{"type":"unitFreed","unit":"brigand-1","at":{"x":1,"y":1},"hp":4}""",
            ProtocolJson.Event(new UnitFreed("brigand-1", new Coord(1, 1), 4)));
        Assert.Equal("Brigand-1 lays down the weapon: freed (4 hp)", PlaySession.Describe(new UnitFreed("brigand-1", new Coord(1, 1), 4), Starter, UnitNames.None));
    }
}
