using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The hunt (issue 692, Marrit's rule): on a map with a <c>hunter:</c> header the enemy on that
/// tile strikes only the defenders of the standing front whose defenders' summed HP is lowest,
/// fixed as each enemy phase begins, marches on a front nobody defends, and <c>threat</c> and the
/// board name the front it hunts.
/// </summary>
public class HuntTests
{
    private const string Header = "fronts: north 5,1; south 5,5\nhunter: 8,3\n";

    /// <summary>An 11x7 field: Hale by the north breach, Wren by the south, the rider east between them.</summary>
    private static string Field(string header = Header, string extra = "") =>
        "name: Field\nsize: 11x7\nwin: rout\nturn_limit: 10\nrecall: 3\nenemy_level: 1\n"
        + header
        + "\n...........\n...........\n...........\n...........\n...........\n...........\n...........\n"
        + "\nunits:\nP captain 4,1\nP recruit:wren 4,5\nE rider 8,3 group:van behavior:aggressive\nE soldier 10,6 group:rear behavior:hold\n" + extra;

    private static readonly ValueList<Unit> Three = ValueList<Unit>.Of(Hale, Wren, Ivo);

    private static BattleState Board(int hale = 22, int wren = 20) =>
        Hurt(Start(map: Field()), hale, wren);

    private static BattleState Hurt(BattleState state, int hale, int wren)
    {
        state = state.WithUnit(state.Find("hale")! with { Hp = hale });
        return state.WithUnit(state.Find("wren")! with { Hp = wren });
    }

    private static Front Named(BattleState state, string name) => state.Map.Fronts.Single(f => f.Name == name);

    [Fact]
    public void TheHuntedFrontIsTheStandingOneWithTheLeastDefenderHp()
    {
        Assert.Equal("south", Hunt.Hunted(Board(hale: 22, wren: 20))!.Name);
        Assert.Equal("north", Hunt.Hunted(Board(hale: 10, wren: 20))!.Name);
    }

    [Fact]
    public void ATieGoesToTheEarlierFront()
    {
        Assert.Equal("north", Hunt.Hunted(Board(hale: 20, wren: 20))!.Name);
    }

    [Fact]
    public void AUnitFartherThanTheRadiusDefendsNoFront()
    {
        var state = Board();
        state = state.WithUnit(state.Find("hale")! with { At = new Coord(0, 1) });

        Assert.Null(Hunt.DefendedFrom(state.Map, new Coord(0, 1)));
        Assert.Equal(Named(state, "north"), Hunt.DefendedFrom(state.Map, new Coord(2, 1)));
        Assert.Equal(0, Hunt.Holds(state).Single(h => h.Front.Name == "north").Hp);
        Assert.Equal("north", Hunt.Hunted(state)!.Name);
    }

    [Fact]
    public void AFallenFrontIsNeverHunted()
    {
        var state = Board(hale: 10, wren: 20) with { Fallen = ValueList<string>.Of("north") };

        Assert.Equal("south", Hunt.Hunted(state)!.Name);
        var all = state with { Fallen = ValueList<string>.Of("north", "south") };
        Assert.Null(Hunt.Hunted(all));
        Assert.Equal("Rider hunts no front: every front has fallen", Hunt.Line(all, UnitNames.Of(all, Starter)));
    }

    [Fact]
    public void TheHunterStrikesOnlyTheHuntedFrontsDefenders()
    {
        var state = Start(map: Field(extra: "P recruit 5,2\n"), roster: Three);
        state = state.WithUnit(state.Find("ivo")! with { At = new Coord(5, 2), Hp = 3 });
        state = Hurt(state, 22, 20).Do(new EndPhase());
        Assert.Equal("south", Hunt.Hunted(state)!.Name);

        var plan = EnemyAi.PlanUnit(state, Starter, state.Find("rider-1")!);

        Assert.Equal("wren", plan.OfType<Attack>().Single().TargetId);
    }

    [Fact]
    public void WithoutTheHuntTheSameRiderTakesTheKill()
    {
        var state = Start(map: Field("fronts: north 5,1; south 5,5\n", "P recruit 5,2\n"), roster: Three);
        state = state.WithUnit(state.Find("ivo")! with { At = new Coord(5, 2), Hp = 3 });
        state = Hurt(state, 22, 20).Do(new EndPhase());

        var plan = EnemyAi.PlanUnit(state, Starter, state.Find("rider-1")!);

        Assert.Equal("ivo", plan.OfType<Attack>().Single().TargetId);
    }

    [Fact]
    public void TheHunterMarchesOnAFrontNobodyDefends()
    {
        var state = Board();
        state = state.WithUnit(state.Find("wren")! with { At = new Coord(0, 6) }).Do(new EndPhase());
        Assert.Equal("south", Hunt.Hunted(state)!.Name);

        var plan = EnemyAi.PlanUnit(state, Starter, state.Find("rider-1")!);

        Assert.Equal(new Command[] { new Move("rider-1", new Coord(5, 5)), new Wait("rider-1") }, plan);
        Assert.Contains(state.Try(plan[0]).Events, e => e is FrontFell { Front: "south" });
    }

    [Fact]
    public void TheHuntIsFixedAsTheEnemyPhaseBegins()
    {
        var state = Board(hale: 22, wren: 20).Do(new EndPhase());
        Assert.Equal("south", state.Hunting);

        var wounded = state.WithUnit(state.Find("hale")! with { Hp = 1 });

        Assert.Equal("south", Hunt.Hunted(wounded)!.Name);
        Assert.Equal("north", Hunt.Choose(wounded)!.Name);
        Assert.Null(state.Do(new EndPhase()).Hunting);
    }

    [Fact]
    public void ThreatPricesNoStrikeFromTheHunterOnAUnitOffTheHuntedFront()
    {
        var state = Board(hale: 22, wren: 20);
        Assert.Equal("south", Hunt.Hunted(state)!.Name);
        var rider = state.Find("rider-1")!;

        Assert.Null(EnemyAi.StrikeOn(state, Starter, rider, state.Find("hale")!));
        Assert.NotNull(EnemyAi.StrikeOn(state, Starter, rider, state.Find("wren")!));
        Assert.DoesNotContain(Queries.Threats(state, Starter, state.Find("hale")!, new Coord(4, 1))!, l => l.Enemy.Id == "rider-1");
    }

    [Fact]
    public void ThreatNamesTheUnitsFrontAndTheHunt()
    {
        var state = Board(hale: 22, wren: 20);
        var hale = state.Find("hale")!;

        var text = PlaySession.ThreatText(state, Starter, hale, hale.At, Queries.Threats(state, Starter, hale, hale.At)!, Queries.SleepingThreats(state, Starter, hale, hale.At)!);

        Assert.Contains("  Front: defends the north", text);
        Assert.Contains("  Rider hunts the south next: 1 defender, 20 hp (weakest); strikes only its defenders", text);
    }

    [Fact]
    public void ThreatFromATileReadsTheHuntWithTheUnitMoved()
    {
        var state = Board(hale: 22, wren: 20);
        var hale = state.Find("hale")!;
        var tile = new Coord(4, 4);

        var text = PlaySession.ThreatText(state, Starter, hale, tile, Queries.Threats(state, Starter, hale, tile)!, Queries.SleepingThreats(state, Starter, hale, tile)!);

        Assert.Contains("  Front: defends the south", text);
        Assert.Contains("hunts the north next: no defenders", text);
    }

    [Fact]
    public void TheBoardMarksTheHuntedFront()
    {
        var state = Board(hale: 22, wren: 20);

        Assert.Equal("fronts: north 5,1 holding (1 defending, 22 hp); south 5,5 holding (1 defending, 20 hp, hunted)", MapRenderer.FrontsLine(state));
        var board = MapRenderer.Render(state, Starter);
        Assert.Contains(Hunt.Rule, board);
        Assert.Contains("Rider hunts the south next", board);
    }

    [Fact]
    public void TheHunterHeaderRoundTrips()
    {
        var map = MapFixture.Parse(Field());

        Assert.Equal(new Coord(8, 3), map.Hunter);
        Assert.Equal(map, MapFixture.Parse(MapFormat.Write(map, Starter)));
    }

    [Theory]
    [InlineData("hunter: 8,3\n", "hunter: needs a fronts: header")]
    [InlineData("fronts: north 5,1\nhunter: 7,3\n", "hunter names 7,3 but no E line places an enemy there")]
    [InlineData("fronts: north 5,1\nhunter: 4,1\n", "hunter names 4,1 but no E line places an enemy there")]
    [InlineData("fronts: north 5,1\nhunter: east\n", "hunter: needs the hunter's tile as x,y")]
    public void ABadHunterHeaderIsRefused(string header, string expected)
    {
        var error = Assert.Throws<MapException>(() => MapFixture.Parse(Field(header)));

        Assert.Contains(expected, error.Message);
    }

    [Fact]
    public void ABossNeverHunts()
    {
        var text = Field().Replace("E rider 8,3 group:van behavior:aggressive", "B rider 8,3 group:van");

        var error = Assert.Throws<MapException>(() => MapFixture.Parse(text));

        Assert.Contains("hunter at 8,3 is a boss; a boss never hunts", error.Message);
    }
}
