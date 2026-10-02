using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The captain's strike (issue 636): Full Measure is a sword art the captain knows, declared at
/// most once a map, and the attack costs his side's next phase, which he begins moved and acted,
/// so he neither moves, acts nor braces until the phase after. Played on the Tollgate with the
/// captain moved beside the brigand at 6,5.
/// </summary>
public class CaptainsStrikeTests
{
    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    private static readonly Coord Brigand = new(6, 5);

    private static BattleState Placed(ulong seed = 636)
    {
        var map = MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "maps", "the_tollgate.map"), Shipped);
        var state = BattleState.From(map, Shipped, Shipped.Cast, seed);
        return state.WithUnit(state.Find("captain")! with { At = new Coord(6, 6) });
    }

    private static string BrigandId(BattleState state) => state.Units.Single(u => u.At == Brigand).Id;

    private static ApplyResult Apply(BattleState state, Command command) => Resolver.Apply(state, Shipped, command);

    /// <summary>The captain strikes the brigand with Full Measure, then is given HP no enemy phase can take, so the cost is read on a living captain.</summary>
    private static BattleState Struck(BattleState state) => Hardy(Apply(state, new Attack("captain", BrigandId(state), null, "full_measure")).Next);

    private static BattleState Hardy(BattleState state) => state.WithUnit(state.Find("captain")! with { Hp = 999 });

    /// <summary>Ends the player phase and every enemy phase after it until the next player phase begins.</summary>
    private static ApplyResult ToNextPlayerPhase(BattleState state)
    {
        var result = Apply(state, new EndPhase());
        var events = result.Events.ToList();
        while (result.Next.Phase == Side.Enemy && result.Next.Outcome.Result == BattleResult.Ongoing)
        {
            result = Apply(result.Next, new EndPhase());
            events.AddRange(result.Events);
        }

        return result with { Events = ValueList<GameEvent>.From(events) };
    }

    [Fact]
    public void FullMeasureShipsWithTheIssuesNumbersAndTheCaptainKnowsIt()
    {
        var art = (CombatArtEffect)Shipped.Ability("full_measure").Effect;

        Assert.Equal(new CombatArtEffect(WeaponType.Sword, WeaponRank.E, 2, 8, 30, 20, 0, 0) { PerMap = 1, CostsNextPhase = true, Single = true }, art);
        Assert.Contains("full_measure", Shipped.ArtsOf(Shipped.Cast.Single(u => u.Id == "captain")).Select(a => a.Ability.Id));
    }

    [Fact]
    public void AnArtThatStrikesOnceNeverDoublesWhereThePlainAttackDoubles()
    {
        var state = Placed();
        var captain = state.Find("captain")!;
        var brigand = state.Units.Single(u => u.At == Brigand);

        var plain = Queries.Forecast(state, Shipped, captain, brigand)!;
        var struck = Queries.Forecast(state, Shipped, captain, brigand, null, "full_measure")!;

        Assert.True(plain.Attacker.Doubles);
        Assert.Equal(2, plain.Attacker.StrikeCount);
        Assert.False(struck.Attacker.Doubles);
        Assert.Equal(1, struck.Attacker.StrikeCount);
        Assert.Equal(3, struck.AttackerSpendsAtMost);
    }

    [Fact]
    public void AnArtThatStrikesOnceLeavesTheCounterUnchanged()
    {
        var state = Placed();
        var captain = state.Find("captain")!;
        var brigand = state.Units.Single(u => u.At == Brigand);

        var plain = Queries.Forecast(state, Shipped, captain, brigand)!;
        var struck = Queries.Forecast(state, Shipped, captain, brigand, null, "full_measure")!;

        Assert.Equal(plain.Defender, struck.Defender);
    }

    [Fact]
    public void TheResolverStrikesOnceWithTheArtAndTwiceWithThePlainAttack()
    {
        var doubled = false;
        foreach (var seed in Enumerable.Range(1, 20).Select(s => (ulong)s))
        {
            var state = Placed(seed);
            var art = Apply(state, new Attack("captain", BrigandId(state), null, "full_measure"));
            var plain = Apply(state, new Attack("captain", BrigandId(state), null, null));

            Assert.Equal(1, art.Events.OfType<CombatFought>().First().Strikes.Count(s => s.AttackerId == "captain"));
            doubled |= plain.Events.OfType<CombatFought>().First().Strikes.Count(s => s.AttackerId == "captain") == 2;
        }

        Assert.True(doubled);
    }

    [Fact]
    public void TheMenuRowOfAnArtThatStrikesOnceForecastsOneStrike()
    {
        var state = Placed();
        var rows = Queries.AttackOptions(state, Shipped, state.Find("captain")!, state.Units.Single(u => u.At == Brigand));

        Assert.Equal(1, rows.Single(r => r.Art?.Id == "full_measure").Forecast!.Attacker.StrikeCount);
        Assert.Equal(2, rows.First(r => r.Art is null).Forecast!.Attacker.StrikeCount);
    }

    [Fact]
    public void AnArtWithAPerMapCapIsRefusedOnceSpentWithItsReason()
    {
        var state = Placed();
        state = state.WithUnit(state.Find("captain")! with { ArtsDeclared = ValueList<string>.Of("full_measure") });

        var result = Apply(state, new Attack("captain", BrigandId(state), null, "full_measure"));

        Assert.False(result.Accepted);
        Assert.Equal(RejectionReason.ArtRefused, result.Rejection!.Reason);
        Assert.Contains("Full Measure is once a map and is spent", result.Rejection.Message);
    }

    [Fact]
    public void DeclaringTheStrikeRecordsItOnTheUnit()
    {
        var struck = Struck(Placed());

        Assert.Equal(new ArtDeclared("captain", "full_measure", "iron_sword", 2), Apply(Placed(), new Attack("captain", BrigandId(Placed()), null, "full_measure")).Events.OfType<ArtDeclared>().Single());
        Assert.Equal(1, struck.Find("captain")!.TimesDeclared("full_measure"));
    }

    [Fact]
    public void TheMenuGreysASpentStrikeWithTheSameRefusal()
    {
        var state = Placed();
        var captain = state.Find("captain")! with { ArtsDeclared = ValueList<string>.Of("full_measure") };
        state = state.WithUnit(captain);

        var row = Queries.AttackOptions(state, Shipped, captain, state.Units.Single(u => u.At == Brigand)).Single(r => r.Art?.Id == "full_measure");

        Assert.False(row.Legal);
        Assert.Contains("Full Measure is once a map and is spent", row.Refusal!.Message);
        Assert.True(Queries.AttackOptions(state, Shipped, captain, state.Units.Single(u => u.At == Brigand)).Single(r => r.Art?.Id == "feint").Legal);
    }

    [Fact]
    public void AnArtWithoutACapIsNeverCountedOrRefusedForUse()
    {
        var state = Apply(Placed(), new Attack("captain", BrigandId(Placed()), null, "feint")).Next;

        Assert.Null(state.Find("captain")!.ArtsDeclared);
        Assert.Equal(0, state.Find("captain")!.Spent);
    }

    [Fact]
    public void TheStrikeCostsTheCaptainHisSidesNextPhase()
    {
        var struck = Struck(Placed());
        Assert.Equal(1, struck.Find("captain")!.Spent);

        var result = ToNextPlayerPhase(struck);
        var captain = result.Next.Find("captain")!;

        Assert.Contains(new UnitRested("captain"), result.Events);
        Assert.True(captain.Moved);
        Assert.True(captain.Acted);
        Assert.True(captain.Resting);
        var move = Apply(result.Next, new Move("captain", new Coord(captain.At.X - 1, captain.At.Y)));
        Assert.False(move.Accepted);
        Assert.Contains("spent this phase on last phase's strike", move.Rejection!.Message);
        Assert.False(Apply(result.Next, new Wait("captain")).Accepted);
    }

    [Fact]
    public void TheCaptainIsHimselfAgainThePhaseAfter()
    {
        var resting = Hardy(ToNextPlayerPhase(Struck(Placed())).Next);

        var after = ToNextPlayerPhase(resting);
        var captain = after.Next.Find("captain")!;
        Assert.Equal(BattleResult.Ongoing, after.Next.Outcome.Result);

        Assert.DoesNotContain(after.Events, e => e is UnitRested);
        Assert.Equal(0, captain.Spent);
        Assert.False(captain.Moved);
        Assert.False(captain.Acted);
        Assert.True(Apply(after.Next, new Wait("captain")).Accepted);
    }

    [Fact]
    public void RecallRestoresTheChargeWithTheBoard()
    {
        var before = Placed();
        var struck = Struck(before);

        Assert.Equal(1, struck.Find("captain")!.TimesDeclared("full_measure"));
        Assert.Equal(0, before.Find("captain")!.TimesDeclared("full_measure"));
        Assert.True(Queries.AttackOptions(before, Shipped, before.Find("captain")!, before.Units.Single(u => u.At == Brigand)).Single(r => r.Art?.Id == "full_measure").Legal);
    }

    [Fact]
    public void TheSpentMarkAndTheDeclaredArtsRoundTripThroughTheProtocol()
    {
        var struck = Struck(Placed());

        var json = ProtocolJson.State(struck, Shipped);
        var back = ProtocolJson.ReadState(json, Shipped).Find("captain")!;

        Assert.Contains("\"spent\":1", json);
        Assert.Contains("\"artsDeclared\":[\"full_measure\"]", json);
        Assert.Equal(1, back.Spent);
        Assert.Equal(1, back.TimesDeclared("full_measure"));
    }
}
