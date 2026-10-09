using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Issue 1461, forms slice a1 (DECISIONS/0386, 0398): Grit on a <c>forms: on</c> map. Every unit
/// starts at 0, gains 1 as its side's phase begins and 1 for each hit landed on it, at most 3;
/// a declared form costs its Grit, hit or miss, and its weapon pays only for the strikes.
/// </summary>
public class GritTests
{
    private static readonly Ability Payoff = Art("payoff", new CombatArtEffect(WeaponType.Sword, WeaponRank.E, 2, 4, 10, 0, 0, 0) { Grit = 2 });
    private static readonly Ability Wild = Art("wild", new CombatArtEffect(WeaponType.Sword, WeaponRank.E, 2, 1, -300, 0, 0, 0) { Grit = 2 });

    private static readonly GameContent Forms = Starter with
    {
        Abilities = Starter.Abilities.Add(Payoff.Id, Payoff).Add(Wild.Id, Wild),
    };

    private static readonly Unit Knower = Hale with { Abilities = ValueList<string>.Of("payoff", "wild") };

    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    private static Ability Art(string id, CombatArtEffect effect) => new(id, id, "A test art.", effect);

    private static MapDefinition FormsMap => YardMap with { FormsEnabled = true };

    private static BattleState Begin(bool forms = true) =>
        BattleState.From(forms ? FormsMap : YardMap, Forms, ValueList<Unit>.Of(Knower, Wren), 7);

    private static BattleState Beside(bool forms = true) =>
        Resolver.Apply(Begin(forms), Forms, new Move("hale", new Coord(2, 1))).Next;

    private static BattleState WithGrit(BattleState state, string id, int grit) => state.WithUnit(state.Find(id)! with { Grit = grit });

    private static int Uses(BattleState state) => state.Find("hale")!.Unit.Inventory.Items[0].Uses;

    [Fact]
    public void OnAFormsMapThePlayerSideBeginsWithOneGritForItsFirstPhaseAndTheEnemyWithNone()
    {
        var state = Begin();

        Assert.All(state.UnitsOf(Side.Player), u => Assert.Equal(1, u.Grit));
        Assert.All(state.UnitsOf(Side.Enemy), u => Assert.Equal(0, u.Grit));
    }

    [Fact]
    public void OffTheHeaderNoUnitCarriesGrit()
    {
        var state = Begin(forms: false);
        for (var i = 0; i < 4; i++)
        {
            state = Resolver.Apply(state, Forms, new EndPhase()).Next;
        }

        Assert.All(state.Units, u => Assert.Equal(0, u.Grit));
    }

    [Fact]
    public void GritGainsOneAsItsSidesPhaseBeginsAndNeverPassesThree()
    {
        var state = Begin();

        state = Resolver.Apply(state, Forms, new EndPhase()).Next;
        Assert.Equal(1, state.Find("brigand-1")!.Grit);
        Assert.Equal(1, state.Find("hale")!.Grit);

        for (var i = 0; i < 9; i++)
        {
            state = Resolver.Apply(state, Forms, new EndPhase()).Next;
        }

        Assert.All(state.Units, u => Assert.Equal(Grit.Cap, u.Grit));
    }

    [Fact]
    public void AFormIsRefusedBelowItsGrit()
    {
        var state = Beside();

        var result = Resolver.Apply(state, Forms, new Attack("hale", "brigand-1", null, "payoff"));

        Assert.False(result.Accepted);
        Assert.Equal(RejectionReason.ArtRefused, result.Rejection!.Reason);
        Assert.Equal("hale cannot use payoff: payoff costs 2 Grit and hale has 1", result.Rejection.Message);
        Assert.Null(Queries.Forecast(state, Forms, state.Find("hale")!, state.Find("brigand-1")!, art: "payoff"));
    }

    [Fact]
    public void AFormPaysItsGritOnAMissAndTheWeaponPaysOnlyForItsStrikes()
    {
        var state = WithGrit(Beside(), "hale", 3);
        var before = Uses(state);

        var result = Resolver.Apply(state, Forms, new Attack("hale", "brigand-1", null, "wild"));

        Assert.True(result.Accepted, result.Rejection?.Message);
        var fought = result.Events.OfType<CombatFought>().Single().Strikes;
        var mine = fought.Where(s => s.AttackerId == "hale").ToList();
        Assert.All(mine, s => Assert.False(s.Hit));
        var taken = fought.Count(s => s.TargetId == "hale" && s.Hit);
        Assert.Equal(before - mine.Count, Uses(result.Next));
        Assert.Equal(Math.Min(Grit.Cap, 3 - 2 + taken), result.Next.Find("hale")!.Grit);
        Assert.Equal(new ArtDeclared("hale", "wild", "iron_sword", 0) { Grit = 2 }, result.Events.OfType<ArtDeclared>().Single());
    }

    [Fact]
    public void EveryHitLandedOnAUnitGainsItOneGritCounterHitsIncluded()
    {
        var state = WithGrit(Beside(), "hale", 0);

        var result = Resolver.Apply(state, Forms, new Attack("hale", "brigand-1"));

        Assert.True(result.Accepted, result.Rejection?.Message);
        var strikes = result.Events.OfType<CombatFought>().Single().Strikes;
        Assert.Equal(Math.Min(Grit.Cap, strikes.Count(s => s.TargetId == "hale" && s.Hit)), result.Next.Find("hale")!.Grit);
        if (result.Next.Find("brigand-1") is { } brigand)
        {
            Assert.Equal(Math.Min(Grit.Cap, strikes.Count(s => s.TargetId == "brigand-1" && s.Hit)), brigand.Grit);
        }
    }

    [Fact]
    public void OffTheHeaderAnArtStillCostsUses()
    {
        var state = Beside(forms: false);
        var before = Uses(state);

        var result = Resolver.Apply(state, Forms, new Attack("hale", "brigand-1", null, "wild"));

        Assert.True(result.Accepted, result.Rejection?.Message);
        var mine = result.Events.OfType<CombatFought>().Single().Strikes.Count(s => s.AttackerId == "hale");
        Assert.Equal(before - mine - 2, Uses(result.Next));
        Assert.Equal(0, result.Next.Find("hale")!.Grit);
    }

    [Fact]
    public void TheBoardPrintsGritOnEveryUnitsRowOnAFormsMap()
    {
        var board = MapRenderer.Render(Begin(), Forms);

        Assert.Contains(Grit.Legend, board);
        Assert.Contains("Grit 1/3", board);
        Assert.Contains("Grit 0/3", board);
        Assert.DoesNotContain("Grit", MapRenderer.Render(Begin(forms: false), Forms));
    }

    [Fact]
    public void TheShippedArtsAreRecostedPayoffsAtTwoAndFullMeasureAtZero()
    {
        foreach (var id in new[] { "feint", "heavy_cut", "long_thrust", "cleave", "aimed_shot", "overcast", "read_ahead", "turn_the_key", "paid_in_full" })
        {
            Assert.Equal(2, ((CombatArtEffect)Shipped.Ability(id).Effect).Grit);
        }

        Assert.Equal(0, ((CombatArtEffect)Shipped.Ability("full_measure").Effect).Grit);
    }

    [Fact]
    public void TheFormsHeaderRoundTrips()
    {
        var text = MapFormat.Write(MapFixture.Parse(MapFixture.OldMillRoad) with { FormsEnabled = true }, MapFixture.Content);

        Assert.Contains("forms: on\n", text);
        var map = MapFixture.Parse(text);
        Assert.True(map.FormsEnabled);
        Assert.Equal(text, MapFormat.Write(map, MapFixture.Content));
        Assert.False(MapFixture.Parse(MapFixture.OldMillRoad).FormsEnabled);
    }

    [Fact]
    public void GritSurvivesASuspend()
    {
        var state = WithGrit(Begin(), "hale", 3);

        var read = Ironwake.Content.Protocol.ProtocolJson.ReadState(Ironwake.Content.Protocol.ProtocolJson.State(state, Forms), Forms);

        Assert.Equal(3, read.Find("hale")!.Grit);
    }
}
