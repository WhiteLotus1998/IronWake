using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The dash (DESIGN.md 13.27, experiment): on a <c>dash: on</c> map a player unit that has neither
/// moved nor acted may move <see cref="Winded.ExtraMov"/> tiles past its Move as its whole turn, and
/// is struck at <see cref="Winded.Hit"/> more hit until the player phase begins again. A map without
/// the header, a unit that moved or was shoved, an enemy, and a tile past the dash's reach are refused.
/// </summary>
public class DashTests
{
    private static string Field(bool dash, string units) =>
        $"""
        name: Field
        size: 16x5
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1
        {(dash ? "dash: on\nshove: on" : "")}

        ................
        ................
        ................
        ................
        ................

        units:
        {units}
        """.Replace("\n\n\n", "\n\n");

    private const string Line = """
        P captain 0,2
        P recruit:wren 0,4
        E brigand 15,2 group:field behavior:aggressive

        """;

    private static BattleState Start(bool dash = true, string units = Line) =>
        BattleFixture.Start(map: Field(dash, units));

    private static BattleState Apply(BattleState state, params Command[] commands)
    {
        foreach (var command in commands)
        {
            var result = state.Try(command);
            Assert.True(result.Accepted, result.Rejection?.Message);
            state = result.Next;
        }

        return state;
    }

    private static int Mov(BattleState state, string unit) => Starter.Class(state.Find(unit)!.Unit.ClassId).Mov;

    [Fact]
    public void ADashMovesTwoPastTheMoveAndIsTheWholeTurn()
    {
        var start = Start();
        var far = new Coord(Mov(start, "hale") + Winded.ExtraMov, 2);

        Assert.Equal(RejectionReason.OutOfReach, start.Try(new Move("hale", far)).Rejection?.Reason);
        var result = start.Try(new Dash("hale", far));
        Assert.True(result.Accepted, result.Rejection?.Message);

        var hale = result.Next.Find("hale")!;
        Assert.Equal(far, hale.At);
        Assert.True(hale.Moved && hale.Acted && hale.Winded);
        Assert.Null(hale.Canto);
        Assert.Contains(result.Events, e => e is UnitMoved { UnitId: "hale" });
        Assert.Contains(new UnitWinded("hale"), result.Events);
        Assert.Equal(RejectionReason.AlreadyActed, result.Next.Try(new Wait("hale")).Rejection?.Reason);
    }

    [Fact]
    public void ADashPastItsReachIsRefused()
    {
        var start = Start();
        var tooFar = new Coord(Mov(start, "hale") + Winded.ExtraMov + 1, 2);

        Assert.Equal(RejectionReason.OutOfReach, start.Try(new Dash("hale", tooFar)).Rejection?.Reason);
    }

    [Fact]
    public void AMapWithoutTheHeaderRefusesTheDash()
    {
        var result = Start(dash: false).Try(new Dash("hale", new Coord(1, 2)));

        Assert.Equal(RejectionReason.CannotDash, result.Rejection?.Reason);
    }

    [Fact]
    public void AUnitThatMovedOrWasShovedCannotDash()
    {
        var moved = Apply(Start(), new Move("hale", new Coord(1, 2)));
        Assert.Equal(RejectionReason.CannotDash, moved.Try(new Dash("hale", new Coord(3, 2))).Rejection?.Reason);

        var shoved = Apply(Start(units: """
            P captain 0,2
            P recruit:wren 1,2
            E brigand 15,2 group:field behavior:aggressive

            """), new Shove("hale", "wren"));
        Assert.Equal(RejectionReason.CannotDash, shoved.Try(new Dash("wren", new Coord(4, 2))).Rejection?.Reason);
    }

    [Fact]
    public void AnEnemyNeverDashes()
    {
        var enemyPhase = Apply(Start(), new EndPhase());

        Assert.Equal(RejectionReason.CannotDash, enemyPhase.Try(new Dash("brigand-1", new Coord(14, 2))).Rejection?.Reason);
    }

    [Fact]
    public void AWindedUnitIsStruckAtFifteenMore()
    {
        var start = Start(units: """
            P captain 0,2
            E brigand 4,2 group:field behavior:aggressive

            """);
        var brigand = start.Find("brigand-1")!;
        var plain = start.Find("hale")! with { At = new Coord(3, 2) };
        var winded = plain with { Winded = true };

        var open = brigand.ToCombatant(start.WithUnit(plain), Starter, against: plain).HitModifier;
        var tired = brigand.ToCombatant(start.WithUnit(winded), Starter, against: winded).HitModifier;

        Assert.Equal(15, Winded.Hit);
        Assert.Equal(open + Winded.Hit, tired);
    }

    [Fact]
    public void TheWindComesBackWhenThePlayerPhaseBegins()
    {
        var state = Apply(Start(), new Dash("hale", new Coord(3, 2)), new EndPhase());
        Assert.True(state.Find("hale")!.Winded);

        state = Apply(state, new EndPhase());

        Assert.False(state.Find("hale")!.Winded);
    }

    [Fact]
    public void ThreatFromATileOnlyADashReachesIsPricedWinded()
    {
        const string units = """
            P captain 0,2
            E brigand 9,2 group:field behavior:aggressive

            """;
        var start = Start(units: units);
        var hale = start.Find("hale")!;
        var tile = new Coord(Mov(start, "hale") + Winded.ExtraMov, 2);
        Assert.False(Queries.CanStandOn(start, Starter, hale, tile));

        var line = Assert.Single(Queries.Threats(start, Starter, hale, tile)!);
        var standing = start.WithUnit(hale with { At = tile, Moved = true, Acted = true, Winded = true });
        var expected = Assert.Single(Queries.Threats(standing, Starter, standing.Find("hale")!, tile)!);

        var rested = start.WithUnit(hale with { At = tile, Moved = true, Acted = true });
        var plain = Assert.Single(Queries.Threats(rested, Starter, rested.Find("hale")!, tile)!);

        Assert.Equal(expected.Forecast.Attacker.HitChance, line.Forecast.Attacker.HitChance);
        Assert.True(line.Forecast.Attacker.HitChance > plain.Forecast.Attacker.HitChance);
        Assert.Null(Queries.Threats(Start(dash: false, units: units), Starter, hale, tile));
    }

    [Fact]
    public void TheHeaderRoundTripsAndPrintsItsRule()
    {
        var map = MapFixture.Parse(Field(true, Line), "test.map");
        Assert.True(map.DashEnabled);
        Assert.True(MapFixture.Parse(MapFormat.Write(map, Starter), "again.map").DashEnabled);
        Assert.False(MapFixture.Parse(Field(false, Line), "plain.map").DashEnabled);

        Assert.Contains(Winded.Legend, MapRenderer.Render(Start(), Starter));
        Assert.DoesNotContain(Winded.Legend, MapRenderer.Render(Start(dash: false), Starter));
    }

    [Fact]
    public void AWindedUnitReadsBackFromTheProtocol()
    {
        var state = Apply(Start(), new Dash("hale", new Coord(3, 2)));
        var flat = state with { History = ValueList<BattleState>.Empty };

        Assert.Contains("\"winded\":true", ProtocolJson.State(flat, Starter));
        Assert.Equal(flat, ProtocolJson.ReadState(ProtocolJson.State(flat, Starter), Starter));
    }
}
