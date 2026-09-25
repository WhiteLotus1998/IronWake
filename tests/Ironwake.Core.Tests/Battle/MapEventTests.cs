using Ironwake.Content;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Map events (issue 32, DESIGN.md section 10): the <c>events:</c> block parses and writes
/// back canonically, a turn event fires once at the start of its phase, an enter event
/// fires on ending a move on its tile and never on passing through, spawned units take
/// ids fixed by the file, and a held tile blocks an event and spends it.
/// </summary>
public class MapEventTests
{
    /// <summary>
    /// An 8x4 yard split by a wall at column 3. Hale at 0,1 and Wren at 0,2; two Hold
    /// enemies east of the wall. The lever at 2,0 opens 3,0, the reinforcement arrives at
    /// 7,0 on enemy phase 2, the flag is set on player phase 2, the flood drowns 0,3 on
    /// player phase 3, and 1,1 is a tile the only short path from 0,1 to 2,1 crosses.
    /// </summary>
    private const string Gate = """
        name: Gate
        size: 8x4
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1

        ...#....
        ...#....
        ...#....
        ...#....

        units:
        P captain 0,1
        P recruit:wren 0,2
        E soldier 6,1 group:yard behavior:hold
        E brigand 7,3 group:yard behavior:hold

        events:
        lever enter 2,0 terrain 3,0 =
        reinforce turn 2 enemy spawn brigand 7,0 group:east behavior:aggressive
        alarm turn 2 player flag alarm
        crossed enter 1,1 flag crossed
        flood turn 3 player terrain 0,3 ~

        """;

    private static BattleState Start() => BattleFixture.Start(map: Gate);

    private static BattleState EndPhases(BattleState state, int count)
    {
        for (var i = 0; i < count; i++)
        {
            state = state.Do(new EndPhase());
        }

        return state;
    }

    private static IReadOnlyList<GameEvent> EndPhaseEvents(ref BattleState state)
    {
        var result = state.Try(new EndPhase());
        Assert.True(result.Accepted, result.Rejection?.Message);
        state = result.Next;
        return result.Events;
    }

    [Fact]
    public void TheEventsBlockParsesEachTriggerAndEachAction()
    {
        var map = MapFixture.Parse(Gate);

        Assert.Equal(
            new[]
            {
                new MapEvent("lever", new EnterTrigger(new Coord(2, 0)), new ChangeTerrain(new Coord(3, 0), "road")),
                new MapEvent("reinforce", new TurnTrigger(2, Side.Enemy), new SpawnEnemy(new EnemyPlacement(new Coord(7, 0), "brigand", "east", Behavior.Aggressive, false))),
                new MapEvent("alarm", new TurnTrigger(2, Side.Player), new SetFlag("alarm")),
                new MapEvent("crossed", new EnterTrigger(new Coord(1, 1)), new SetFlag("crossed")),
                new MapEvent("flood", new TurnTrigger(3, Side.Player), new ChangeTerrain(new Coord(0, 3), "water")),
            },
            map.Events);
    }

    [Fact]
    public void TheEventsBlockWritesBackCanonically()
    {
        var map = MapFixture.Parse(Gate);
        var text = MapFormat.Write(map, Starter);

        Assert.Equal(Gate.Replace("\r\n", "\n"), text);
        Assert.Equal(map, MapFixture.Parse(text));
    }

    [Fact]
    public void AMapWithoutEventsWritesNoEventsBlock()
    {
        var text = MapFormat.Write(MapFixture.Parse(Yard), Starter);

        Assert.DoesNotContain("events:", text);
    }

    [Theory]
    [InlineData("oops", "needs a name, a trigger, and an action")]
    [InlineData("a turn 2", "turn trigger needs a turn and a phase")]
    [InlineData("a turn 0 enemy flag x", "1..10")]
    [InlineData("a turn 11 enemy flag x", "1..10")]
    [InlineData("a turn 2 dusk flag x", "phase must be player or enemy")]
    [InlineData("a turn 1 player flag x", "turn 1 player never begins")]
    [InlineData("a enter 9,0 flag x", "outside the 8x4 grid")]
    [InlineData("a enter", "enter trigger needs a tile")]
    [InlineData("a dawn 2 flag x", "unknown event trigger 'dawn'")]
    [InlineData("a turn 2 enemy", "needs an action")]
    [InlineData("a turn 2 enemy explode 1,1", "unknown event action 'explode'")]
    [InlineData("a turn 2 enemy terrain 1,1 ?", "unknown terrain glyph '?'")]
    [InlineData("a turn 2 enemy terrain 1,1", "terrain action needs a tile and one glyph")]
    [InlineData("a turn 2 enemy flag", "flag action needs one name")]
    [InlineData("a turn 2 enemy spawn brigand 5,1 group:g behavior:aggressive", "not on the edge")]
    [InlineData("a turn 2 enemy spawn brigand 7,0 group:g", "behavior:aggressive")]
    [InlineData("a turn 2 enemy spawn brigand 7,0 group:g behavior:boss", "B line")]
    [InlineData("a turn 2 enemy spawn dragon 7,0 group:g behavior:hold", "unknown enemy template 'dragon'")]
    [InlineData("a turn 2 enemy spawn brigand 3,0 group:g behavior:hold", "cannot start on Wall")]
    [InlineData("lever turn 2 enemy flag x", "event name 'lever' is already used on line 20")]
    [InlineData("a:b turn 2 enemy flag x", "starts with its name")]
    public void AMalformedEventLineIsRefusedNamingItsLine(string line, string fragment)
    {
        var text = Gate.Replace("flood turn 3 player terrain 0,3 ~", "flood turn 3 player terrain 0,3 ~\n" + line);
        var e = Assert.Throws<MapException>(() => MapFixture.Parse(text, "bad.map"));

        Assert.Equal(25, e.Line);
        Assert.Contains(fragment, e.Problem);
        Assert.Contains("line 25", e.Message);
    }

    [Fact]
    public void ATurnEventFiresOnceAtTheStartOfItsPhase()
    {
        var state = Start();
        Assert.DoesNotContain(EndPhaseEvents(ref state), e => e is MapEventFired);
        Assert.DoesNotContain(EndPhaseEvents(ref state), e => e is MapEventFired { Name: "reinforce" });
        Assert.Equal((2, Side.Player), (state.Turn, state.Phase));
        Assert.Null(state.Find("brigand-2"));

        var enemyTwo = EndPhaseEvents(ref state);
        Assert.Equal((2, Side.Enemy), (state.Turn, state.Phase));
        Assert.Equal(
            new GameEvent[] { new MapEventFired("reinforce", false), new UnitSpawned("brigand-2", new Coord(7, 0), "east", Behavior.Aggressive) },
            enemyTwo.SkipWhile(e => e is not MapEventFired).ToArray());
        var spawned = state.Find("brigand-2")!;
        Assert.Equal((Side.Enemy, "east", Behavior.Aggressive, false), (spawned.Side, spawned.Group, spawned.Behavior, spawned.Moved));
        Assert.Equal(spawned.MaxHp(Starter), spawned.Hp);
        Assert.True(state.HasFired("reinforce"));

        for (var i = 0; i < 4; i++)
        {
            Assert.DoesNotContain(EndPhaseEvents(ref state), e => e is MapEventFired { Name: "reinforce" });
        }

        Assert.Single(state.Units, u => u.Group == "east");
    }

    [Fact]
    public void AFlagEventSetsItsFlagAtItsPhase()
    {
        var state = Start();
        EndPhaseEvents(ref state);
        Assert.False(state.HasFlag("alarm"));

        var events = EndPhaseEvents(ref state);

        Assert.Contains(new FlagSet("alarm"), events);
        Assert.True(state.HasFlag("alarm"));
        Assert.Contains("flags alarm\n", state.Canonical());
    }

    [Fact]
    public void AnEnterEventFiresOnEndingAMoveOnTheTile()
    {
        var result = Start().Try(new Move("hale", new Coord(2, 0)));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Equal(new GameEvent[] { new MapEventFired("lever", false), new TerrainChanged(new Coord(3, 0), "road") }, result.Events.Skip(1).ToArray());
        Assert.Equal("road", result.Next.Map.TerrainIdAt(new Coord(3, 0)));
        Assert.Contains(new Coord(4, 0), result.Next.ReachOf(result.Next.Find("hale")!, Starter).Destinations);
        Assert.Contains("fired lever\n", result.Next.Canonical());
        Assert.Contains("tile 3,0 road\n", result.Next.Canonical());
    }

    [Fact]
    public void AnEnterEventDoesNotFireOnPassingThroughTheTile()
    {
        var result = Start().Try(new Move("hale", new Coord(2, 1)));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Contains(new Coord(1, 1), Assert.IsType<UnitMoved>(result.Events[0]).Path);
        Assert.DoesNotContain(result.Events, e => e is MapEventFired);
        Assert.False(result.Next.HasFired("crossed"));

        var ended = result.Next.Try(new Move("wren", new Coord(1, 1)));
        Assert.Contains(new MapEventFired("crossed", false), ended.Events);
    }

    [Fact]
    public void AnEnterEventDoesNotFireForAnEnemyOnTheTile()
    {
        var state = Start();
        var enemy = state.Find("soldier-1")! with { At = new Coord(2, 0) };
        var events = new List<GameEvent>();

        var after = MapEvents.AfterMove(state.WithUnit(enemy), Starter, enemy, events);

        Assert.Empty(events);
        Assert.False(after.HasFired("lever"));
    }

    [Fact]
    public void AnEventFiresOnlyOnce()
    {
        var state = Start().Do(new Move("hale", new Coord(2, 0))).Do(new Wait("hale"));
        state = EndPhases(EndPhases(state, 2).Do(new Move("hale", new Coord(1, 0))), 2);

        var back = state.Try(new Move("hale", new Coord(2, 0)));

        Assert.True(back.Accepted, back.Rejection?.Message);
        Assert.DoesNotContain(back.Events, e => e is MapEventFired);
    }

    [Fact]
    public void AHeldSpawnTileBlocksTheReinforcementAndSpendsIt()
    {
        var start = Start();
        var state = start.WithUnit(start.Find("wren")! with { At = new Coord(7, 0) });
        state = EndPhases(state, 2);

        var events = EndPhaseEvents(ref state);

        Assert.Contains(new MapEventFired("reinforce", true), events);
        Assert.DoesNotContain(events, e => e is UnitSpawned);
        Assert.Null(state.Find("brigand-2"));
        Assert.True(state.HasFired("reinforce"));
    }

    [Fact]
    public void ATerrainChangeItsOccupantCannotStandOnIsBlocked()
    {
        var start = Start();
        var held = EndPhases(start.WithUnit(start.Find("wren")! with { At = new Coord(0, 3) }), 3);
        var events = EndPhaseEvents(ref held);

        Assert.Contains(new MapEventFired("flood", true), events);
        Assert.Equal("plain", held.Map.TerrainIdAt(new Coord(0, 3)));
        Assert.True(held.HasFired("flood"));

        var open = EndPhases(start, 4);
        Assert.Equal("water", open.Map.TerrainIdAt(new Coord(0, 3)));
    }

    [Fact]
    public void ASpawnIdIsFixedByTheFileAndNotByWhatFiredBeforeIt()
    {
        var text = Gate.Replace("alarm turn 2 player flag alarm", "early turn 2 player spawn brigand 7,2 group:east behavior:hold");
        var start = BattleFixture.Start(map: text);
        var map = start.Map;
        Assert.Equal("brigand-2", map.SpawnId(map.Events.Single(e => e.Name == "reinforce")));
        Assert.Equal("brigand-3", map.SpawnId(map.Events.Single(e => e.Name == "early")));

        var blocked = EndPhases(start.WithUnit(start.Find("wren")! with { At = new Coord(7, 2) }), 3);
        Assert.Null(blocked.Find("brigand-3"));
        Assert.NotNull(blocked.Find("brigand-2"));

        var clear = EndPhases(start, 3);
        Assert.NotNull(clear.Find("brigand-3"));
        Assert.NotNull(clear.Find("brigand-2"));
    }

    [Fact]
    public void SpawnedUnitsTakeTheSameIdsOnEveryReplay()
    {
        static string Replay() => EndPhases(Start().Do(new Move("hale", new Coord(2, 0))), 5).Canonical();

        Assert.Equal(Replay(), Replay());
        Assert.Contains("unit brigand-2 Enemy 7,0", Replay());
    }

    [Fact]
    public void ARecallRestoresTheTerrainAndTheFiredList()
    {
        var state = Start().Do(new Move("hale", new Coord(2, 0)));
        Assert.Equal("road", state.Map.TerrainIdAt(new Coord(3, 0)));

        var back = state.Do(new Recall(0));

        Assert.Equal("wall", back.Map.TerrainIdAt(new Coord(3, 0)));
        Assert.False(back.HasFired("lever"));
        Assert.Contains(new MapEventFired("lever", false), back.Try(new Move("hale", new Coord(2, 0))).Events);
    }

    [Fact]
    public void ASpawnedUnitIsDrawnWithItsOwnLetter()
    {
        var state = EndPhases(Start(), 3);
        var letters = MapRenderer.Letters(state.Map, Starter);
        var spawned = state.Find("brigand-2")!;

        Assert.Equal(state.Map.Placements.Count, spawned.PlacementIndex);
        Assert.Equal(state.Map.Placements.Count + 1, letters.Length);
        Assert.Contains(letters[spawned.PlacementIndex] + "  brigand-2", MapRenderer.Render(state, Starter));
    }

    [Fact]
    public void ASpawnedGuardGroupIsWakeCheckedOnTheCommandThatSpawnedIt()
    {
        var start = BattleFixture.Start(map: Gate.Replace("group:east behavior:aggressive", "group:east behavior:guard"));
        var near = EndPhases(start.WithUnit(start.Find("wren")! with { At = new Coord(5, 0) }), 2);
        var far = EndPhases(start, 2);

        Assert.Contains(new GroupWoke("east", WakeCause.Proximity), EndPhaseEvents(ref near));
        Assert.True(near.IsAwake("east"));
        Assert.DoesNotContain(EndPhaseEvents(ref far), e => e is GroupWoke);
        Assert.False(far.IsAwake("east"));
    }

    [Fact]
    public void TheEventsSampleIsCanonicalAndCarriesAReinforcement()
    {
        var repo = Directory.GetParent(Ironwake.Core.Tests.Content.Fixture.RealContentDirectory())!.FullName;
        var path = Path.Combine(repo, "docs", "samples", "sluice_gate.map");
        var text = File.ReadAllText(path).Replace("\r\n", "\n");
        var map = MapFixture.Parse(text, "sluice_gate.map");

        Assert.Equal(text, MapFormat.Write(map, Starter));
        Assert.Contains(map.Events, e => e is { Trigger: TurnTrigger, Action: SpawnEnemy });
    }

    [Fact]
    public void AMapWithoutEventsKeepsItsCanonicalText()
    {
        var canonical = BattleFixture.Start().Canonical();

        Assert.DoesNotContain("fired", canonical);
        Assert.DoesNotContain("flags", canonical);
    }
}
