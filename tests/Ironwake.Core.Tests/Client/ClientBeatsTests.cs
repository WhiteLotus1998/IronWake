using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Client;

/// <summary>
/// Showcase slice 3 (issue 513): what the client animates, read from the events alone. A move's
/// walk, a strike's numbers, a death's ghost and its mark for the rest of the phase, and the
/// enemy-act card, on the Tollgate's seed 113 turn 4, where the rider arrives and kills Teodor.
/// </summary>
public class ClientBeatsTests
{
    private static ClientSession TurnFour()
    {
        var content = ContentLoader.Load(Fixture.RealContentDirectory());
        var map = MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "maps", "the_tollgate.map"), content);
        var client = new ClientSession(content, BattleState.From(map, content, content.Cast, 113));
        var script = File.ReadAllText(Path.Combine(Fixture.RealContentDirectory(), "..", "docs", "screenshots", "the_tollgate-113-enemy.script"));
        Ironwake.Client.Script.Apply(client, script);
        return client;
    }

    private static void StepUntil(ClientSession client, string prefix)
    {
        while (client.Step())
        {
            if (client.Playing!.Line.StartsWith(prefix, StringComparison.Ordinal))
            {
                return;
            }
        }

        Assert.Fail($"no enemy line starts with '{prefix}'");
    }

    [Fact]
    public void AnEnemyMoveLineCarriesOneWalkBeatAlongItsMarkedPath()
    {
        var client = TurnFour();
        client.Submit(new EndPhase());
        StepUntil(client, "rider-1 moves");

        var beat = Assert.Single(client.Beats);
        Assert.True(beat.IsMove);
        Assert.Equal("rider-1", beat.UnitId);
        Assert.Equal(client.Playing!.From, beat.From);
        Assert.Equal(client.Playing.To, beat.To);
        Assert.Equal(client.Playing.Path, beat.Path);
    }

    [Fact]
    public void AStrikeBeatRisesOneNumberPerStrikeOffTheUnitStruck()
    {
        var client = TurnFour();
        client.Submit(new EndPhase());
        StepUntil(client, "rider-1 moves");
        var teodor = client.State.Find("teodor")!;
        StepUntil(client, "rider-1 attacks teodor");

        var beat = Assert.Single(client.Beats);
        Assert.True(beat.IsStrike);
        Assert.Equal(teodor.At, beat.Struck);
        Assert.Equal(teodor.Hp, beat.HpBefore["teodor"]);
        var onTeodor = beat.Pops.Where(p => p.TargetId == "teodor").ToList();
        Assert.NotEmpty(onTeodor);
        Assert.All(onTeodor, p => Assert.Equal(teodor.At, p.At));
        Assert.Equal(0, onTeodor[^1].TargetHpAfter);
        Assert.All(beat.Pops, p => Assert.Equal(p.Kind == PopKind.Miss, p.Text == "miss"));
    }

    [Fact]
    public void AKilledUnitStaysAsAGhostUntilItsDeathLineThenLeavesAMarkForTheRestOfThePhase()
    {
        var client = TurnFour();
        client.Submit(new EndPhase());
        StepUntil(client, "rider-1 attacks teodor");

        Assert.Null(client.State.Find("teodor"));
        Assert.Contains(client.Ghosts, g => g.Id == "teodor");
        Assert.DoesNotContain(client.Fallen, f => f.Unit.Id == "teodor");

        Assert.True(client.Step());
        Assert.StartsWith("teodor falls", client.Playing!.Line, StringComparison.Ordinal);
        var beat = Assert.Single(client.Beats);
        Assert.Equal("teodor", beat.Fell!.Unit.Id);
        Assert.Empty(client.Ghosts);
        var mark = Assert.Single(client.Fallen, f => f.Unit.Id == "teodor");
        Assert.Equal(new Coord(7, 4), mark.At);

        client.Continue();
        Assert.Contains(client.Fallen, f => f.Unit.Id == "teodor");
    }

    [Fact]
    public void TheMarksOfTheEnemyPhaseClearOnThePlayersFirstCommand()
    {
        var client = TurnFour();
        client.Submit(new EndPhase());
        client.Continue();
        Assert.NotEmpty(client.Fallen);

        var captain = client.State.Find("captain")!;
        Assert.True(client.Submit(new Wait(captain.Id)));

        Assert.Empty(client.Fallen);
    }

    [Fact]
    public void APlayerPhaseDeathIsMarkedUntilThePhaseEnds()
    {
        var client = TurnFour();

        var mark = Assert.Single(client.Fallen);
        Assert.Equal("archer-2", mark.Unit.Id);
        Assert.Equal(new Coord(5, 5), mark.At);

        client.Submit(new EndPhase());
        Assert.Empty(client.Fallen);
    }

    [Fact]
    public void ARecallClearsTheMarks()
    {
        var client = TurnFour();
        Assert.NotEmpty(client.Fallen);

        Assert.True(client.Recall(0));

        Assert.Empty(client.Fallen);
    }

    [Fact]
    public void APlayerCommandsBeatsPlayItsEventsInOrderTheStrikeThenTheDeath()
    {
        var content = ContentLoader.Load(Fixture.RealContentDirectory());
        var map = MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "maps", "the_tollgate.map"), content);
        var client = new ClientSession(content, BattleState.From(map, content, content.Cast, 113));
        var lines = File.ReadAllLines(Path.Combine(Fixture.RealContentDirectory(), "..", "docs", "screenshots", "the_tollgate-113-enemy.script"));
        var kill = Array.LastIndexOf(lines, "attack captain archer-2");
        Ironwake.Client.Script.Apply(client, string.Join("\n", lines.Take(kill)));
        var serial = client.BeatSerial;

        Assert.True(client.Submit(Ironwake.Client.Script.Parse(lines[kill], client.State)!));

        Assert.Equal(serial + 1, client.BeatSerial);
        Assert.Equal(2, client.Beats.Count);
        Assert.True(client.Beats[0].IsStrike);
        Assert.Equal("captain", client.Beats[0].UnitId);
        Assert.Equal("archer-2", client.Beats[1].Fell!.Unit.Id);
    }

    [Fact]
    public void APlayerMoveIsOneWalkBeat()
    {
        var content = ContentLoader.Load(Fixture.RealContentDirectory());
        var map = MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "maps", "the_tollgate.map"), content);
        var client = new ClientSession(content, BattleState.From(map, content, content.Cast, 113));
        var pell = client.State.Find("pell")!;

        Assert.True(client.Submit(new Move("pell", new Coord(4, 8))));

        var beat = Assert.Single(client.Beats);
        Assert.Equal("pell", beat.UnitId);
        Assert.Equal(pell.At, beat.From);
        Assert.Equal(new Coord(4, 8), beat.To);
    }

    [Fact]
    public void TheEnemyActCardNamesTheActorAndBothSidesOfAStrike()
    {
        var client = TurnFour();
        client.Submit(new EndPhase());
        StepUntil(client, "rider-1 moves");
        var teodor = client.State.Find("teodor")!;
        StepUntil(client, "rider-1 attacks teodor");

        var act = client.Act!;
        Assert.Equal("rider-1", act.ActorId);
        Assert.Equal("teodor", act.Defender!.Id);
        Assert.Equal(teodor.Hp, act.Defender.HpBefore);
        Assert.Equal(0, act.Defender.HpAfter);
        Assert.Contains("falls", act.Doing, StringComparison.Ordinal);
        Assert.Equal(client.Beats[0].Pops.Count(p => p.TargetId == "teodor"), act.Attacker!.Strikes.Count);
    }

    [Fact]
    public void TheEnemyActCardIsGoneOnceThePlayerActs()
    {
        var client = TurnFour();
        client.Submit(new EndPhase());
        client.Continue();
        Assert.NotNull(client.Act);

        client.Submit(new Wait(client.State.Find("captain")!.Id));

        Assert.Null(client.Act);
    }

    [Fact]
    public void AStepInPlaceIsNotAWalk()
    {
        var client = TurnFour();
        var unit = client.State.Find("captain")!;

        Assert.Null(Beat.Of(new UnitMoved(unit.Id, unit.At, unit.At, ValueList<Coord>.Empty), client.State, client.State));
    }
}
