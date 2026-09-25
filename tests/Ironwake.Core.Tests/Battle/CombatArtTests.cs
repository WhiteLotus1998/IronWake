using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Issue 68: combat arts, declared with the attack before the roll, paid in durability.
/// The arts here are test content: <c>content/abilities.json</c> ships none until the
/// Table sets which arts exist and their numbers.
/// </summary>
public class CombatArtTests
{
    private static readonly Ability Sunder = Art("sunder", new CombatArtEffect(WeaponType.Sword, WeaponRank.E, 2, 4, 10, 0, 0, 0));
    private static readonly Ability Heft = Art("heft", new CombatArtEffect(WeaponType.Sword, WeaponRank.E, 1, 5, 0, 0, 10, 0));
    private static readonly Ability Wild = Art("wild", new CombatArtEffect(WeaponType.Sword, WeaponRank.E, 2, 1, -300, 0, 0, 0));
    private static readonly Ability Reach = Art("reach", new CombatArtEffect(WeaponType.Sword, WeaponRank.E, 1, 0, 0, 0, 0, 1));
    private static readonly Ability Pike = Art("pike", new CombatArtEffect(WeaponType.Lance, WeaponRank.E, 1, 2, 0, 0, 0, 0));
    private static readonly Ability Veteran = Art("veteran", new CombatArtEffect(WeaponType.Sword, WeaponRank.D, 1, 2, 0, 0, 0, 0));

    private static readonly GameContent Arts = Starter with
    {
        Abilities = Starter.Abilities.Add(Sunder.Id, Sunder).Add(Heft.Id, Heft).Add(Wild.Id, Wild)
            .Add(Reach.Id, Reach).Add(Pike.Id, Pike).Add(Veteran.Id, Veteran),
    };

    private static readonly Unit Knower = Hale with { Abilities = ValueList<string>.Of("sunder", "heft", "wild", "reach", "pike", "veteran") };

    private static Ability Art(string id, CombatArtEffect effect) => new(id, id, "A test art.", effect);

    private static BattleState Begin(Unit? captain = null, ulong seed = 7) =>
        BattleState.From(YardMap, Arts, ValueList<Unit>.Of(captain ?? Knower, Wren), seed);

    private static BattleState Beside(Unit? captain = null, ulong seed = 7) =>
        Resolver.Apply(Begin(captain, seed), Arts, new Move("hale", new Coord(2, 1))).Next;

    private static ApplyResult Try(BattleState state, Command command) => Resolver.Apply(state, Arts, command);

    private static Rejection Refused(BattleState state, Command command)
    {
        var result = Try(state, command);
        Assert.False(result.Accepted, "the command was accepted");
        Assert.Same(state, result.Next);
        Assert.Empty(result.Events);
        return result.Rejection!;
    }

    private static int Uses(BattleState state) => state.Find("hale")!.Unit.Inventory.Items[0].Uses;

    [Fact]
    public void AnArtIsTheWeaponWithItsDeltasAddedAndMtAndWtFlooredAtZero()
    {
        var sword = Starter.Weapon("iron_sword");
        var art = new CombatArtEffect(WeaponType.Sword, WeaponRank.E, 1, -20, 10, 5, -20, 1);

        var struck = art.Apply(sword);

        Assert.Equal(0, struck.Mt);
        Assert.Equal(sword.Hit + 10, struck.Hit);
        Assert.Equal(sword.Crit + 5, struck.Crit);
        Assert.Equal(0, struck.Wt);
        Assert.Equal(sword.MinRange, struck.MinRange);
        Assert.Equal(sword.MaxRange + 1, struck.MaxRange);
        Assert.Equal(sword.Id, struck.Id);
        Assert.Equal(2, art.UsesNeeded);
    }

    [Fact]
    public void TheForecastOfAnArtShowsTheArtsNumbersAndItsCost()
    {
        var state = Beside();
        var hale = state.Find("hale")!;
        var brigand = state.Find("brigand-1")!;

        var plain = Queries.Forecast(state, Arts, hale, brigand)!;
        var art = Queries.Forecast(state, Arts, hale, brigand, art: "sunder")!;

        Assert.Equal(plain.Attacker.Damage + 4, art.Attacker.Damage);
        Assert.Equal(Math.Min(100, plain.Attacker.HitChance + 10), art.Attacker.HitChance);
        Assert.Equal(plain.Defender, art.Defender);
        Assert.Equal(0, plain.ArtCost);
        Assert.Equal(2, art.ArtCost);
        Assert.Equal((art.Attacker.Doubles ? 2 : 1) + 2, art.AttackerSpendsAtMost);
    }

    [Fact]
    public void AnArtResolvesAtTheNumbersItsForecastPrinted()
    {
        var state = Beside();
        var forecast = Queries.Forecast(state, Arts, state.Find("hale")!, state.Find("brigand-1")!, art: "sunder")!;

        var result = Try(state, new Attack("hale", "brigand-1", null, "sunder"));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Equal(new ArtDeclared("hale", "sunder", "iron_sword", 2), result.Events.OfType<ArtDeclared>().Single());
        Assert.IsType<CombatFought>(result.Events[result.Events.ToList().FindIndex(e => e is ArtDeclared) + 1]);
        foreach (var strike in result.Events.OfType<CombatFought>().Single().Strikes.Where(s => s.AttackerId == "hale" && s.Hit && !s.Crit))
        {
            Assert.Equal(forecast.Attacker.Damage, strike.Damage);
        }
    }

    [Fact]
    public void AnArtsCostIsPaidOnAMiss()
    {
        var state = Beside();
        var before = Uses(state);

        var result = Try(state, new Attack("hale", "brigand-1", null, "wild"));

        Assert.True(result.Accepted, result.Rejection?.Message);
        var strikes = result.Events.OfType<CombatFought>().Single().Strikes.Where(s => s.AttackerId == "hale").ToList();
        Assert.NotEmpty(strikes);
        Assert.All(strikes, s => Assert.False(s.Hit));
        Assert.Equal(before - strikes.Count - 2, Uses(result.Next));
    }

    [Fact]
    public void APlainAttackSpendsOneUsePerStrikeAndNoArtEvent()
    {
        var state = Beside();
        var before = Uses(state);

        var result = Try(state, new Attack("hale", "brigand-1"));

        Assert.Empty(result.Events.OfType<ArtDeclared>());
        Assert.Equal(before - result.Events.OfType<CombatFought>().Single().Strikes.Count(s => s.AttackerId == "hale"), Uses(result.Next));
    }

    [Fact]
    public void AnArtsWtDeltaCanCostTheUnitItsDouble()
    {
        var fast = Knower with { Stats = Knower.Stats with { Spd = 14 } };
        var state = Beside(fast);
        var hale = state.Find("hale")!;
        var brigand = state.Find("brigand-1")!;

        var plain = Queries.Forecast(state, Arts, hale, brigand)!;
        var heft = Queries.Forecast(state, Arts, hale, brigand, art: "heft")!;

        Assert.True(plain.Attacker.Doubles);
        Assert.False(heft.Attacker.Doubles);
        Assert.Equal(plain.Attacker.Damage + 5, heft.Attacker.Damage);
        Assert.Equal(2, plain.AttackerSpendsAtMost);
        Assert.Equal(2, heft.AttackerSpendsAtMost);
    }

    [Fact]
    public void AnArtsRangeDeltaReachesATargetThePlainWeaponCannot()
    {
        var state = Resolver.Apply(Begin(), Arts, new Move("hale", new Coord(1, 1))).Next;

        Assert.Equal(RejectionReason.OutOfRange, Refused(state, new Attack("hale", "brigand-1")).Reason);
        Assert.Null(Queries.Forecast(state, Arts, state.Find("hale")!, state.Find("brigand-1")!));
        Assert.NotNull(Queries.Forecast(state, Arts, state.Find("hale")!, state.Find("brigand-1")!, art: "reach"));
        Assert.True(Try(state, new Attack("hale", "brigand-1", null, "reach")).Accepted);
    }

    [Fact]
    public void AnArtTheUnitDoesNotKnowIsRefused()
    {
        var state = Resolver.Apply(BattleState.From(YardMap, Arts, ValueList<Unit>.Of(Hale, Wren), 7), Arts, new Move("hale", new Coord(2, 1))).Next;

        var rejection = Refused(state, new Attack("hale", "brigand-1", null, "sunder"));

        Assert.Equal(RejectionReason.NoSuchArt, rejection.Reason);
        Assert.Equal("hale knows no art 'sunder'", rejection.Message);
        Assert.Equal(RejectionReason.NoSuchArt, Refused(Beside(), new Attack("hale", "brigand-1", null, "vigilance")).Reason);
    }

    [Fact]
    public void AnArtOnAWeaponOfAnotherTypeIsRefused()
    {
        var rejection = Refused(Beside(), new Attack("hale", "brigand-1", null, "pike"));

        Assert.Equal(RejectionReason.ArtRefused, rejection.Reason);
        Assert.Equal("hale cannot use pike: pike is a lance art and Iron Sword is a sword", rejection.Message);
    }

    [Fact]
    public void AnArtAboveTheUnitsRankIsRefused()
    {
        var rejection = Refused(Beside(), new Attack("hale", "brigand-1", null, "veteran"));

        Assert.Equal(RejectionReason.ArtRefused, rejection.Reason);
        Assert.Equal("hale cannot use veteran: rank E in sword, and veteran needs D", rejection.Message);
        var trained = Knower with { Skill = Knower.Skill.With(WeaponType.Sword, WeaponRanks.Threshold(WeaponRank.D)) };
        Assert.True(Try(Beside(trained), new Attack("hale", "brigand-1", null, "veteran")).Accepted);
    }

    [Fact]
    public void AnArtOnABrokenWeaponIsRefused()
    {
        var state = Beside(Knower.WithUses(0));

        var rejection = Refused(state, new Attack("hale", "brigand-1", null, "sunder"));

        Assert.Equal(RejectionReason.ArtRefused, rejection.Reason);
        Assert.Equal("hale cannot use sunder: Iron Sword is broken and cannot pay for an art", rejection.Message);
        Assert.True(Try(state, new Attack("hale", "brigand-1")).Accepted);
    }

    [Fact]
    public void AnArtTheWeaponCannotPayForIsRefused()
    {
        var rejection = Refused(Beside(Knower.WithUses(2)), new Attack("hale", "brigand-1", null, "sunder"));

        Assert.Equal(RejectionReason.ArtRefused, rejection.Reason);
        Assert.Equal("hale cannot use sunder: sunder costs 3 uses with the strike and Iron Sword has 2 left", rejection.Message);
        Assert.True(Try(Beside(Knower.WithUses(3)), new Attack("hale", "brigand-1", null, "sunder")).Accepted);
    }

    [Fact]
    public void ARefusedArtHasNoForecast()
    {
        var state = Beside(Knower.WithUses(0));
        var hale = state.Find("hale")!;

        Assert.Null(Queries.Forecast(state, Arts, hale, state.Find("brigand-1")!, art: "sunder"));
        Assert.Equal(RejectionReason.ArtRefused, Queries.WeaponRefusal(Arts, hale, null, "sunder")!.Reason);
        Assert.Null(Queries.WeaponRefusal(Arts, hale, null, null));
    }

    [Fact]
    public void AnArtIsNeverAppliedToACounter()
    {
        var state = Beside();
        var hale = state.Find("hale")!;
        var brigand = state.Find("brigand-1")!;
        var plainHale = BattleState.From(YardMap, Arts, ValueList<Unit>.Of(Hale, Wren), 7);
        plainHale = Resolver.Apply(plainHale, Arts, new Move("hale", new Coord(2, 1))).Next;

        var counter = Queries.Forecast(state, Arts, brigand, hale)!;

        Assert.Equal(Queries.Forecast(plainHale, Arts, plainHale.Find("brigand-1")!, plainHale.Find("hale")!)!.Defender, counter.Defender);
        Assert.Throws<ArgumentException>(() => hale.ToCombatant(state, Arts, countering: true, art: (CombatArtEffect)Sunder.Effect));
    }

    [Fact]
    public void TheLegalCommandsIncludeEveryArtTheUnitMayDeclare()
    {
        var state = Beside();

        var arts = Resolver.Legal(state, Arts).OfType<Attack>().Where(a => a.UnitId == "hale" && a.Art is not null).ToList();

        Assert.Contains(new Attack("hale", "brigand-1", null, "sunder"), arts);
        Assert.Contains(new Attack("hale", "brigand-1", null, "reach"), arts);
        Assert.DoesNotContain(arts, a => a.Art is "pike" or "veteran");
        Assert.All(arts, a => Assert.True(Try(state, a).Accepted, a.ToString()));
    }
}
