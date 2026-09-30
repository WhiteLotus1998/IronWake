using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Cover (DESIGN.md 13.19, experiment; issue 531's acceptance as rounds 141 and 142 set it): on a
/// <c>cover: on</c> map a player unit beside an ally covers it; the first Attack aimed at the ally
/// while the two stand side by side swaps them and strikes the coverer on the ally's tile; the
/// cover is then spent, and it ends at the covered side's next phase. One cover per ally, no
/// chains, a range-2 strike still swaps, a pin reads the swapped board. The planner prices the
/// swap; <c>threat</c> prices through it and the enemy phase says when it turned a strike away.
/// </summary>
public class CoverTests
{
    private static readonly Unit Tank = Recruit("ta", "pikeman", new Stats(30, 8, 0, 6, 5, 2, 9, 1, 3), "iron_lance");
    private static readonly Unit Mage = Recruit("ma", "bowman", new Stats(16, 5, 0, 7, 6, 2, 1, 1, 3), "iron_bow");
    private static readonly Unit Third = Recruit("th", new Stats(20, 7, 0, 6, 4, 5, 4, 2, 3), "iron_sword");

    private static string Field(string units, bool cover = true, string extra = "") =>
        $"""
        name: Field
        size: 9x5
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1
        {(cover ? "cover: on" : "")}
        {extra}

        .........
        .........
        .........
        .........
        .........

        units:
        {units}
        """.Replace("\n\n\n", "\n\n").Replace("\n\n\n", "\n\n");

    /// <summary>The tank at 3,1 beside the mage at 3,2; a brigand beside the mage at 4,2.</summary>
    private const string Pair = """
        P captain 0,0
        P recruit:ta 3,1
        P recruit:ma 3,2
        E brigand 4,2 group:field behavior:hold

        """;

    private static BattleState Start(string units = Pair, bool cover = true, string extra = "", ulong seed = 7) =>
        BattleFixture.Start(seed, ValueList<Unit>.Of(Hale, Tank, Mage, Third), Field(units, cover, extra));

    private static ApplyResult Step(BattleState state, Command command)
    {
        var result = state.Try(command);
        Assert.True(result.Accepted, result.Rejection?.Message);
        return result;
    }

    /// <summary>The tank covers the mage, everyone else waits, and the enemy phase begins.</summary>
    private static BattleState Covered(BattleState state) =>
        state.Do(new Cover("ta", "ma")).Do(new Wait("ma")).Do(new Wait("hale")).Do(new EndPhase());

    [Fact]
    public void TheFirstAttackAimedAtACoveredAllySwapsThemAndStrikesTheCoverer()
    {
        var covered = Covered(Start());
        Assert.Equal("ta", covered.Find("ma")!.CoveredBy);

        var struck = Step(covered, new Attack("brigand-1", "ma"));

        var fired = Assert.Single(struck.Events.OfType<CoverFired>());
        Assert.Equal(("ta", "ma", "brigand-1", new Coord(3, 2), new Coord(3, 1)), (fired.UnitId, fired.AllyId, fired.AttackerId, fired.At, fired.AllyTo));
        var fought = Assert.Single(struck.Events.OfType<CombatFought>());
        Assert.Equal("ta", fought.TargetId);
        Assert.Equal(new Coord(3, 2), struck.Next.Find("ta")!.At);
        Assert.Equal(new Coord(3, 1), struck.Next.Find("ma")!.At);
        Assert.Equal(covered.Find("ma")!.Hp, struck.Next.Find("ma")!.Hp);
    }

    [Fact]
    public void TheCoverIsSpentOnceItFires()
    {
        var struck = Step(Covered(Start()), new Attack("brigand-1", "ma")).Next;

        Assert.Null(struck.Find("ma")!.CoveredBy);
        Assert.Null(CoverRule.Swapped(struck, struck.Find("ma")!));
    }

    [Fact]
    public void TheCoverEndsWhenTheCoveredSidesNextPhaseBegins()
    {
        var covered = Covered(Start());
        var enemyEnds = Step(covered.Do(new Wait("brigand-1")), new EndPhase()).Next;

        Assert.Equal(Side.Player, enemyEnds.Phase);
        Assert.Null(enemyEnds.Find("ma")!.CoveredBy);
    }

    [Fact]
    public void ACoverFiresOnlyWhileTheTwoStandSideBySide()
    {
        var covered = Covered(Start());
        var apart = covered.WithUnit(covered.Find("ta")! with { At = new Coord(1, 1) });

        var struck = Step(apart, new Attack("brigand-1", "ma"));

        Assert.Empty(struck.Events.OfType<CoverFired>());
        Assert.Equal("ma", Assert.Single(struck.Events.OfType<CombatFought>()).TargetId);
    }

    [Fact]
    public void CoverIsAnActionWithNoCantoAndNoBrace()
    {
        var state = Start(extra: "brace: on");

        var result = Step(state, new Cover("ta", "ma"));

        var taken = Assert.Single(result.Events.OfType<CoverTaken>());
        Assert.Equal(new CoverTaken("ta", "ma", new Coord(3, 1)), taken);
        var tank = result.Next.Find("ta")!;
        Assert.True(tank.Acted);
        Assert.True(tank.Moved);
        Assert.False(tank.Braced);
        Assert.Null(tank.Canto);
        Assert.Empty(result.Events.OfType<UnitWaited>());
    }

    [Fact]
    public void TheCoverEventNamesTheStrikeTheCovererPassedUp()
    {
        var units = """
            P captain 0,0
            P recruit:ta 4,1
            P recruit:ma 3,1
            E brigand 4,2 group:field behavior:hold

            """;
        var taken = Assert.Single(Step(Start(units), new Cover("ta", "ma")).Events.OfType<CoverTaken>());

        Assert.Equal("brigand-1", taken.PassedUpTargetId);
        Assert.NotNull(taken.PassedUpHit);
    }

    [Fact]
    public void CoverIsRefusedWithoutTheHeader()
    {
        var result = Start(cover: false).Try(new Cover("ta", "ma"));

        Assert.False(result.Accepted);
        Assert.Equal(RejectionReason.CannotCover, result.Rejection!.Reason);
        Assert.Contains("cover: on", result.Rejection.Message);
    }

    [Fact]
    public void CoverIsRefusedForAnAllyNotBesideTheUnit()
    {
        var result = Start().Try(new Cover("hale", "ma"));

        Assert.False(result.Accepted);
        Assert.Equal(RejectionReason.CannotCover, result.Rejection!.Reason);
        Assert.Contains("not beside", result.Rejection.Message);
    }

    [Fact]
    public void CoverIsRefusedForAnEnemy()
    {
        var result = Start().Try(new Cover("ta", "brigand-1"));

        Assert.False(result.Accepted);
        Assert.Contains("not ta's ally", result.Rejection!.Message);
    }

    [Fact]
    public void AUnitCannotCoverItself()
    {
        var result = Start().Try(new Cover("ta", "ta"));

        Assert.False(result.Accepted);
        Assert.Equal(RejectionReason.CannotCover, result.Rejection!.Reason);
        Assert.Contains("ta cannot cover itself", result.Rejection.Message);
        Assert.DoesNotContain(Resolver.Legal(Start(), Starter), c => c == new Cover("ta", "ta"));
    }

    [Fact]
    public void OneCoverPerAllyASecondCoverIsRefused()
    {
        var units = """
            P captain 0,0
            P recruit:ta 3,1
            P recruit:ma 3,2
            P recruit:th 2,2
            E brigand 6,2 group:field behavior:hold

            """;
        var covered = Start(units).Do(new Cover("ta", "ma"));

        var second = covered.Try(new Cover("th", "ma"));

        Assert.False(second.Accepted);
        Assert.Contains("already covered by ta", second.Rejection!.Message);
    }

    [Fact]
    public void NoChainsACoveredUnitCannotCover()
    {
        var units = """
            P captain 0,0
            P recruit:ta 3,1
            P recruit:ma 3,2
            P recruit:th 2,2
            E brigand 6,2 group:field behavior:hold

            """;
        var covered = Start(units).Do(new Cover("ta", "ma"));

        var chained = covered.Try(new Cover("ma", "th"));

        Assert.False(chained.Accepted);
        Assert.Contains("ma is covered by ta and cannot cover", chained.Rejection!.Message);
    }

    [Fact]
    public void NoChainsACoveringUnitCannotBeCovered()
    {
        var units = """
            P captain 0,0
            P recruit:ta 3,1
            P recruit:ma 3,2
            P recruit:th 2,1
            E brigand 6,2 group:field behavior:hold

            """;
        var covered = Start(units).Do(new Cover("ta", "ma"));

        var chained = covered.Try(new Cover("th", "ta"));

        Assert.False(chained.Accepted);
        Assert.Contains("ta is covering ma and cannot be covered", chained.Rejection!.Message);
    }

    [Fact]
    public void ARangeTwoStrikeStillSwapsAndAMeleeCovererCannotCounterIt()
    {
        var units = """
            P captain 0,0
            P recruit:ta 3,1
            P recruit:ma 3,2
            E archer 5,2 group:field behavior:hold

            """;
        var struck = Step(Covered(Start(units)), new Attack("archer-1", "ma"));

        var fired = Assert.Single(struck.Events.OfType<CoverFired>());
        Assert.False(fired.Counters);
        var fought = Assert.Single(struck.Events.OfType<CombatFought>());
        Assert.Equal("ta", fought.TargetId);
        Assert.All(fought.Strikes, s => Assert.Equal("archer-1", s.AttackerId));
        Assert.Contains("ta cannot counter", PlaySession.Describe(fired, Starter));
    }

    [Fact]
    public void TheCoverEventSaysWhetherTheBlowWouldHaveKilledTheAlly()
    {
        var covered = Covered(Start());
        var frail = covered.WithUnit(covered.Find("ma")! with { Hp = 1 });

        var fired = Assert.Single(Step(frail, new Attack("brigand-1", "ma")).Events.OfType<CoverFired>());
        var healthy = Assert.Single(Step(covered, new Attack("brigand-1", "ma")).Events.OfType<CoverFired>());

        Assert.True(fired.WouldHaveKilled);
        Assert.False(healthy.WouldHaveKilled);
    }

    [Fact]
    public void APinResolvesOnTheSwappedBoard()
    {
        var units = """
            P captain 0,0
            P recruit:ta 3,1
            P recruit:ma 3,2
            E brigand 4,2 group:field behavior:hold
            E brigand 2,2 group:field behavior:hold

            """;
        var covered = Covered(Start(units, extra: "pincer: on"));
        var swap = CoverRule.Swapped(covered, covered.Find("ma")!)!.Value;

        Assert.NotNull(Pincer.PinnedBy(swap.Board, covered.Find("brigand-1")!, swap.Struck));
        Assert.Equal(
            EnemyAi.Score(swap.Board, Starter, covered.Find("brigand-1")!, new Coord(4, 2), swap.Struck),
            EnemyAi.Score(covered, Starter, covered.Find("brigand-1")!, new Coord(4, 2), covered.Find("ma")!));
    }

    [Fact]
    public void ThePlannerScoresAStrikeOnACoveredAllyAgainstTheCovererOnItsTile()
    {
        var covered = Covered(Start());
        var brigand = covered.Find("brigand-1")!;
        var mage = covered.Find("ma")!;
        var swap = CoverRule.Swapped(covered, mage)!.Value;

        var priced = EnemyAi.Score(covered, Starter, brigand, brigand.At, mage);

        Assert.Equal(EnemyAi.Score(swap.Board, Starter, brigand, brigand.At, swap.Struck), priced);
        var lifted = CoverRule.Lifted(covered);
        Assert.NotEqual(EnemyAi.Score(lifted, Starter, brigand, brigand.At, lifted.Find("ma")!), priced);
    }

    [Fact]
    public void ThreatAgainstACoveredAllyPricesTheStrikeAgainstTheCoverer()
    {
        var state = Start().Do(new Cover("ta", "ma"));
        var mage = state.Find("ma")!;

        var line = Assert.Single(Queries.Threats(state, Starter, mage, mage.At)!);
        var text = PlaySession.ThreatText(state, Starter, mage, mage.At, new[] { line }, Array.Empty<SleepingThreat>());

        Assert.Equal("ta", line.CoveredBy!.Id);
        Assert.Contains("brigand-1 from 4,2 with", text);
        Assert.Contains("covered by ta, strikes ta on 3,2", text);
        Assert.Contains("the first strike swaps them; ta takes up to", text);
        Assert.Contains("ma lands on 3,1", text);
    }

    [Fact]
    public void ThreatWithoutACoverIsUnchanged()
    {
        var state = Start();
        var mage = state.Find("ma")!;

        var line = Assert.Single(Queries.Threats(state, Starter, mage, mage.At)!);

        Assert.Null(line.CoveredBy);
        Assert.DoesNotContain("covered", PlaySession.ThreatText(state, Starter, mage, mage.At, new[] { line }, Array.Empty<SleepingThreat>()));
    }

    [Fact]
    public void TheEnemyPhaseSaysWhenACoverTurnedAStrikeAway()
    {
        var units = """
            P captain 0,0
            P recruit:ta 3,1
            P recruit:ma 3,2
            P recruit:th 4,3
            E brigand 4,2 group:field behavior:hold

            """;
        var covered = Start(units).Do(new Cover("ta", "ma")).Do(new Wait("ma")).Do(new Wait("th")).Do(new Wait("hale")).Do(new EndPhase());
        var bare = EnemyAi.PlanUnit(CoverRule.Lifted(covered), Starter, covered.Find("brigand-1")!);
        var plan = EnemyAi.PlanUnit(covered, Starter, covered.Find("brigand-1")!);
        Assert.Equal("ma", bare.OfType<Attack>().Single().TargetId);
        Assert.NotEqual("ma", plan.OfType<Attack>().Single().TargetId);

        Assert.Equal("brigand-1 passed ma (covered by ta)", CoverRule.PassedLine(covered, Starter, plan[0]));
    }

    [Fact]
    public void NoPassedLineWhenTheCoverChangesNothing()
    {
        var covered = Covered(Start());
        var plan = EnemyAi.PlanUnit(covered, Starter, covered.Find("brigand-1")!);

        Assert.Null(CoverRule.PassedLine(CoverRule.Lifted(covered), Starter, plan[0]));
    }

    [Fact]
    public void TheEnemyNeverCovers()
    {
        var units = """
            P captain 0,0
            P recruit:ta 3,1
            E brigand 6,2 group:field behavior:aggressive
            E brigand 6,3 group:field behavior:aggressive

            """;
        var state = Start(units).Do(new Wait("hale")).Do(new Wait("ta")).Do(new EndPhase());

        Assert.DoesNotContain(EnemyAi.Plan(state, Starter), c => c is Cover);
        Assert.False(state.Try(new Cover("brigand-1", "brigand-2")).Accepted);
    }

    [Fact]
    public void LegalCommandsOfferCoverOnlyOnACoverMap()
    {
        Assert.Contains(Resolver.Legal(Start(), Starter), c => c == new Cover("ta", "ma"));
        Assert.DoesNotContain(Resolver.Legal(Start(cover: false), Starter), c => c is Cover);
    }

    [Fact]
    public void RecallRestoresACover()
    {
        var covered = Start().Do(new Cover("ta", "ma"));
        var later = covered.Do(new Wait("ma"));

        Assert.Equal("ta", later.Find("ma")!.CoveredBy);
        Assert.Null(Step(later, new Recall(0)).Next.Find("ma")!.CoveredBy);
    }

    [Fact]
    public void TheHeaderRoundTrips()
    {
        var map = MapFixture.Parse(Field(Pair), "field.map");
        Assert.True(map.CoverEnabled);
        Assert.True(MapFixture.Parse(MapFormat.Write(map, Starter), "again.map").CoverEnabled);
        Assert.False(MapFixture.Parse(Field(Pair, cover: false), "plain.map").CoverEnabled);
    }

    /// <summary>
    /// Issue 531: 13.19's sample is the shipped Tollgate with only <c>cover: on</c> added, so a
    /// play of it reads against the plain map's entries.
    /// </summary>
    [Fact]
    public void TheTollgateCoverSampleIsTheShippedMapWithOnlyTheCoverHeaderAdded()
    {
        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var shippedPath = Path.Combine(repo, "content", "maps", "the_tollgate.map");
        var samplePath = Path.Combine(repo, "docs", "samples", "the_tollgate_cover.map");
        var shipped = File.ReadAllText(shippedPath).Replace("\r\n", "\n").Split('\n').ToList();
        var sampleText = File.ReadAllText(samplePath).Replace("\r\n", "\n");
        var sampleLines = sampleText.Split('\n').ToList();

        var at = sampleLines.IndexOf("cover: on");
        Assert.True(at >= 0);
        sampleLines.RemoveAt(at);
        Assert.Equal(shipped, sampleLines);

        var sample = MapFiles.Load(samplePath, MapFixture.Content);
        var original = MapFiles.Load(shippedPath, MapFixture.Content);
        Assert.Equal(original with { CoverEnabled = true }, sample);
        Assert.Equal(sampleText, MapFormat.Write(sample, Starter));
    }
}
