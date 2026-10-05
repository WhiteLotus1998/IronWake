using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Campaign;

/// <summary>
/// A lethal forecast on the returned claimant names the loss first (issue 1068, Design Table round 372):
/// <c>Rook falls for good (the claimant)</c> opens the row, ahead of any gain, on <c>threat</c>'s counter row,
/// on any attacker's forecast and on a forecast with the claimant striking; absent on a row that cannot kill
/// and on an ordinary enemy. One helper, <see cref="Returned.Falls"/>, on either arm of the branch.
/// </summary>
public class ClaimantFallsTests
{
    private static readonly GameContent Content = MapFixture.Content;

    private const string Clause = "Rook falls for good (the claimant)";

    private const string Field =
        """
        name: Claimant Field
        size: 7x5
        win: defeat_boss
        turn_limit: 10
        enemy_level: 1
        kinsbane: keziah

        .......
        .......
        .......
        .......
        .......

        units:
        P captain 3,2
        P recruit:keziah 1,2
        B bandit_leader 6,0 group:keep behavior:boss
        E brigand 1,1 group:oath behavior:hold

        """;

    /// <summary>The field with Rook returned on 2,2, between Keziah and the captain, at <paramref name="rookHp"/> HP, or full.</summary>
    private static BattleState Start(int? rookHp = null)
    {
        var state = BattleState.From(MapFormat.Parse("claimant.map", Field, Content), Content, Content.Cast, 1);
        var rook = Content.Cast.Single(u => u.Id == "rook");
        state = state.WithReturned(rook, new Coord(2, 2), "oath", Behavior.Hold, "keziah", Content);
        return rookHp is { } hp ? state.WithUnit(state.Find("rook")! with { Hp = hp }) : state;
    }

    private static string Threat(BattleState state, string unitId)
    {
        var unit = state.Find(unitId)!;
        return PlaySession.ThreatText(state, Content, unit, unit.At, Queries.Threats(state, Content, unit, unit.At)!, Queries.SleepingThreats(state, Content, unit, unit.At)!);
    }

    private static string[] Forecast(BattleState state, string unitId, string targetId)
    {
        var unit = state.Find(unitId)!;
        var target = state.Find(targetId)!;
        var forecast = Queries.Forecast(state, Content, unit, target, unit.At, null, null)!;
        return PlaySession.ForecastText(state, Content, unit, target, forecast, unit.At, false).Split('\n');
    }

    private static BattleState AxeInFront(BattleState state)
    {
        var keziah = state.Find("keziah")!;
        var axe = keziah.Unit.Inventory.Items.ToList().FindIndex(s => s.ItemId == "iron_axe");
        return state.WithUnit(keziah.WithSlotInFront(axe));
    }

    [Fact]
    public void FallsNamesOnlyTheReturnedClaimantByName()
    {
        var state = Start(1);
        var names = UnitNames.Of(state, Content);

        Assert.Equal(Clause, Returned.Falls(state, state.Find("rook")!, names));
        Assert.Null(Returned.Falls(state, state.Find("brigand-1")!, names));
        Assert.Null(Returned.Falls(state, state.Find("keziah")!, names));
        Assert.Equal("a; b", Returned.Lead("a", "b"));
        Assert.Equal("b", Returned.Lead(null, "b"));
    }

    [Fact]
    public void ThreatsKinsbaneCounterRowLeadsWithTheClaimantFallingBeforeTheFeed()
    {
        var keziah = Start(1).Find("keziah")!;

        var text = Threat(Start(1), "keziah");

        Assert.Contains($"    Counter kills on hit: {Clause}; Keziah +{Kinsbane.FeedHeal} HP, to max {keziah.MaxHp(Content)} (Kinsbane feeds, fed 1)", text);
    }

    [Fact]
    public void ThreatsLethalCounterWithoutKinsbaneStillNamesTheClaimantFalling()
    {
        var text = Threat(AxeInFront(Start(1)), "keziah");

        Assert.Contains($"    Counter kills on hit: {Clause}", text);
        Assert.DoesNotContain("Kinsbane feeds", text);
    }

    [Fact]
    public void ThreatNamesNoClaimantOnACounterThatCannotKillHerOrOnAnOrdinaryEnemy()
    {
        var sturdy = Threat(Start(), "captain");
        var ordinary = Threat(Start().WithUnit(Start().Find("brigand-1")! with { Hp = 1 }), "keziah");

        Assert.Contains("Rook", sturdy);
        Assert.DoesNotContain("falls for good", sturdy);
        Assert.Contains($"    Counter kills on hit: Keziah +{Kinsbane.FeedHeal} HP", ordinary);
        Assert.DoesNotContain(ordinary.Split('\n'), l => l.Contains("Counter kills on hit: Keziah", StringComparison.Ordinal) && l.Contains("falls for good", StringComparison.Ordinal));
    }

    [Fact]
    public void AnyAttackersLethalForecastOnTheClaimantNamesHerFallingFirst()
    {
        var lines = Forecast(Start(1), "captain", "rook");

        Assert.Equal($"  Kills on hit: {Clause}", lines[1]);
    }

    [Fact]
    public void KeziahsKinsbaneForecastOnTheClaimantLeadsWithTheLossOnceThenTheFeed()
    {
        var lines = Forecast(Start(1), "keziah", "rook");

        Assert.StartsWith($"  Kills on hit: {Clause}; Keziah +{Kinsbane.FeedHeal} HP", lines[1]);
        Assert.Single(lines, l => l.Contains("falls for good", StringComparison.Ordinal));
    }

    [Fact]
    public void AForecastThatCannotKillTheClaimantOrKillsAnOrdinaryEnemyNamesNoClaimant()
    {
        var sturdy = Forecast(Start(), "captain", "rook");
        var ordinary = Start();
        ordinary = ordinary.WithUnit(ordinary.Find("brigand-1")! with { Hp = 1 });

        Assert.DoesNotContain(sturdy, l => l.Contains("falls for good", StringComparison.Ordinal));
        Assert.DoesNotContain(Forecast(ordinary, "keziah", "brigand-1"), l => l.Contains("falls for good", StringComparison.Ordinal));
    }

    [Fact]
    public void AForecastWithTheClaimantStrikingNamesHerFallingOnALethalCounter()
    {
        var lines = Forecast(AxeInFront(Start(1)), "rook", "keziah");

        Assert.Equal($"  Counter kills on hit: {Clause}", lines[1]);
    }

    [Fact]
    public void OnRooksPickTheReturnedKeziahIsTheOneNamed()
    {
        var map = Field.Replace("kinsbane: keziah\n", "").Replace("P recruit:keziah 1,2", "P recruit:rook 1,2");
        var state = BattleState.From(MapFormat.Parse("claimant.map", map, Content), Content, Content.Cast, 1);
        state = state.WithReturned(Content.Cast.Single(u => u.Id == "keziah"), new Coord(2, 2), "oath", Behavior.Hold, "rook", Content);
        state = state.WithUnit(state.Find("keziah")! with { Hp = 1 });

        Assert.Equal("  Kills on hit: Keziah falls for good (the claimant)", Forecast(state, "captain", "keziah")[1]);
    }
}
