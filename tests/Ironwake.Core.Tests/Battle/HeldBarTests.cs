using Ironwake.Content.Protocol;
using Ironwake.Content;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Issue 1259, the held bar (experiment, the Lazar House sample): a terrain change marked <c>held</c>
/// lasts only while a player unit stands on its enter tile, and gives way after any command that
/// leaves the tile empty; under <c>arrivals: wait</c> a spawn its tile keeps out is not spent but
/// waits, landing at the first enemy phase that starts with the tile open, one a tile a phase,
/// the oldest first.
/// </summary>
public class HeldBarTests
{
    private const string Lanes = """
        name: Lanes
        size: 6x4
        win: survive
        turn_limit: 6
        recall: 3
        enemy_level: 1
        arrivals: wait

        ......
        ......
        ......
        ......

        units:
        P captain 3,2
        P recruit 2,2

        events:
        bar enter 3,1 terrain 3,0 # held
        n1 turn 1 enemy spawn brigand 3,0 group:n behavior:aggressive
        n2 turn 1 enemy spawn hexer 3,0 group:n behavior:aggressive
        e1 turn 1 enemy spawn brigand 5,2 group:e behavior:aggressive
        """;

    private static readonly Coord Holder = new(3, 1);
    private static readonly Coord Lane = new(3, 0);

    private static BattleState Start(string map = Lanes) => BattleFixture.Start(7, ValueList<Unit>.Of(Hale, Wren), map);

    private static BattleState Barred() => Start().Do(new Move("hale", Holder));

    [Fact]
    public void AStopOnTheHolderTileRaisesTheBar()
    {
        var state = Barred();

        Assert.Equal("wall", state.Map.TerrainIdAt(Lane));
        Assert.True(state.HasFired("bar"));
        Assert.Equal(new HeldBar("bar", Holder, Lane, "wall", "plain"), Assert.Single(state.Bars));
    }

    [Fact]
    public void LeavingTheHolderTileGivesTheBarBackAndUnfiresIt()
    {
        var turn2 = Barred().Do(new EndPhase()).Do(new EndPhase());

        var result = turn2.Try(new Move("hale", new Coord(2, 1)));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Contains(new BarReleased("bar", Holder, Lane, "plain"), result.Events);
        Assert.Contains(new TerrainChanged(Lane, "plain"), result.Events);
        Assert.Equal("plain", result.Next.Map.TerrainIdAt(Lane));
        Assert.False(result.Next.HasFired("bar"));
        Assert.Empty(result.Next.Bars);
    }

    [Fact]
    public void ABarGivenBackFiresAgainOnTheNextStop()
    {
        var turn2 = Barred().Do(new EndPhase()).Do(new EndPhase()).Do(new Move("hale", new Coord(2, 1)));

        var again = turn2.Do(new Move("wren", Holder));

        Assert.Equal("wall", again.Map.TerrainIdAt(Lane));
        Assert.Single(again.Bars);
    }

    [Fact]
    public void ABarGivesWayWhenItsHolderIsGoneByAnyMeans()
    {
        var events = new List<GameEvent>();

        var gone = HeldBars.After(Barred().WithoutUnit("hale"), events);

        Assert.Equal("plain", gone.Map.TerrainIdAt(Lane));
        Assert.Contains(new BarReleased("bar", Holder, Lane, "plain"), events);
    }

    [Fact]
    public void ABarStaysWhileItsHolderStays()
    {
        var events = new List<GameEvent>();
        var barred = Barred();

        Assert.Same(barred, HeldBars.After(barred, events));
        Assert.Empty(events);
    }

    [Fact]
    public void ABlockedSpawnWaitsAtItsTileUnderArrivalsWait()
    {
        var result = Barred().Try(new EndPhase());

        Assert.Contains(new ArrivalWaits("n1", "brigand", Lane, "wall"), result.Events);
        Assert.Contains(new ArrivalWaits("n2", "hexer", Lane, "wall"), result.Events);
        Assert.Equal(ValueList<string>.Of("n1", "n2"), result.Next.Waiting);
        Assert.DoesNotContain(result.Events, e => e is UnitSpawned { At: { X: 3, Y: 0 } });
    }

    [Fact]
    public void ASpawnHeldByAUnitWaitsToo()
    {
        var result = Start().Do(new Move("hale", new Coord(5, 2))).Try(new EndPhase());

        Assert.Contains(new ArrivalWaits("e1", "brigand", new Coord(5, 2), null), result.Events);
        Assert.Contains("e1", result.Next.Waiting);
    }

    [Fact]
    public void AWaitingArrivalLandsAtTheFirstEnemyPhaseThatStartsWithItsTileOpenOneATilePerPhase()
    {
        var turn2 = Barred().Do(new EndPhase()).Do(new EndPhase()).Do(new Move("hale", new Coord(2, 1)));

        var result = turn2.Try(new EndPhase());

        var landed = Assert.Single(result.Events.OfType<UnitSpawned>());
        Assert.Equal(Lane, landed.At);
        Assert.StartsWith("brigand", landed.UnitId, StringComparison.Ordinal);
        Assert.Contains(new MapEventFired("n1", false), result.Events);
        Assert.Equal(ValueList<string>.Of("n2"), result.Next.Waiting);
    }

    [Fact]
    public void AWaitingArrivalStaysWhileItsTileIsHeld()
    {
        var turn2 = Barred().Do(new EndPhase()).Do(new EndPhase());

        var result = turn2.Try(new EndPhase());

        Assert.DoesNotContain(result.Events, e => e is UnitSpawned { At: { X: 3, Y: 0 } });
        Assert.Equal(ValueList<string>.Of("n1", "n2"), result.Next.Waiting);
    }

    [Fact]
    public void WithoutTheHeaderABlockedSpawnIsSpent()
    {
        var result = Start(Lanes.Replace("arrivals: wait\n", "")).Do(new Move("hale", Holder)).Try(new EndPhase());

        Assert.Contains(new MapEventFired("n1", true, "wall"), result.Events);
        Assert.Empty(result.Next.Waiting);
        Assert.DoesNotContain(result.Events, e => e is ArrivalWaits);
    }

    [Fact]
    public void ARecallRestoresTheBarAndTheQueue()
    {
        var waited = Barred().Do(new EndPhase()).Do(new EndPhase());
        var released = waited.Do(new Move("hale", new Coord(2, 1))).Do(new EndPhase());

        var back = released.Do(new Recall(waited.History.Count));

        Assert.Equal("wall", back.Map.TerrainIdAt(Lane));
        Assert.Single(back.Bars);
        Assert.Equal(ValueList<string>.Of("n1", "n2"), back.Waiting);
    }

    [Fact]
    public void TheBoardNamesEachBarItsHolderAndWhatWaitsBehindIt()
    {
        var waited = Barred().Do(new EndPhase());

        Assert.Equal("bar (3,0): barred while hale holds 3,1; waiting: brigand, hexer", HeldBars.Line(waited, Starter));
        Assert.Contains("bar (3,0): barred while hale holds 3,1", MapRenderer.Render(waited, Starter));
        Assert.Equal("bar (3,0): open, barred only while one of yours holds 3,1", HeldBars.Line(Start(), Starter));
    }

    [Fact]
    public void BarsAndTheQueueRoundTripThroughTheProtocolState()
    {
        var waited = Barred().Do(new EndPhase());

        var back = ProtocolJson.ReadState(ProtocolJson.State(waited, Starter), Starter);

        Assert.Equal(waited.Bars, back.Bars);
        Assert.Equal(waited.Waiting, back.Waiting);
        Assert.Contains("bars bar/3,1/3,0/wall/plain", waited.Canonical());
        Assert.Contains("waiting n1 n2", waited.Canonical());
    }

    [Fact]
    public void HeldAndArrivalsWaitRoundTripThroughTheMapFile()
    {
        var map = MapFixture.Parse(Lanes);

        var text = MapFormat.Write(map, Starter);

        Assert.Contains("bar enter 3,1 terrain 3,0 # held", text);
        Assert.Contains("arrivals: wait", text);
        Assert.True(MapFixture.Parse(text).ArrivalsWait);
    }

    [Fact]
    public void AHeldChangeNeedsAnEnterTriggerOnOneTile()
    {
        var turn = Assert.Throws<MapException>(() => MapFixture.Parse(Lanes.Replace("bar enter 3,1 terrain", "bar turn 2 enemy terrain")));
        var two = Assert.Throws<MapException>(() => MapFixture.Parse(Lanes.Replace("bar enter 3,1 terrain", "bar enter 3,1 2,1 terrain")));

        Assert.Contains("a held terrain change needs an enter trigger on one tile", turn.Message);
        Assert.Contains("a held terrain change needs an enter trigger on one tile", two.Message);
    }

    [Fact]
    public void ASpawnOnATileAnEventRetilesMayStandOnItsNewTerrainInTheMapText()
    {
        var walled = Lanes.Replace("......\n......\n......\n......", "...#..\n......\n......\n......");

        Assert.True(MapFixture.Parse(walled).ArrivalsWait);
        var refused = Assert.Throws<MapException>(() => MapFixture.Parse(walled.Replace("bar enter 3,1 terrain 3,0 # held", "bar enter 3,1 terrain 4,0 # held")));
        Assert.Contains("cannot start on Wall at 3,0", refused.Message);
    }

    [Fact]
    public void AnArrivalsHeaderOtherThanWaitIsRefused()
    {
        var error = Assert.Throws<MapException>(() => MapFixture.Parse(Lanes.Replace("arrivals: wait", "arrivals: on")));

        Assert.Contains("arrivals may only be 'wait'", error.Message);
    }

    [Fact]
    public void ArrivalsWaitWithNoSpawnToHoldBackIsRefused()
    {
        var bare = string.Join("\n", Lanes.Split('\n').Where(line => !line.Contains(" spawn ", StringComparison.Ordinal)));

        var error = Assert.Throws<MapException>(() => MapFixture.Parse(bare));

        Assert.Contains("arrivals: wait needs a spawn event", error.Message);
    }
}
