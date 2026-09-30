using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Client;

/// <summary>
/// Showcase slice 4 (issue 514): the Recall scrub, the lethal number held through the death
/// beat, the RECALL pulse on an enemy-phase death, the act card clearing on the flip to the
/// player, and the charge pips, on the Tollgate's seed 113 turn 4, where the rider kills Teodor.
/// </summary>
public class ClientRecallScrubTests
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

    private static Beat StepToDeathOf(ClientSession client, string id)
    {
        while (client.Step())
        {
            if (client.Beats.FirstOrDefault(b => b.Fell?.Unit.Id == id) is { } beat)
            {
                return beat;
            }
        }

        Assert.Fail($"{id} never fell");
        return null!;
    }

    [Fact]
    public void ARecallScrubsThroughEveryStateItFoldsBackNewestFirst()
    {
        var client = TurnFour();
        client.Submit(new EndPhase());
        client.Continue();
        var left = client.State;
        var serial = client.ScrubSerial;

        Assert.True(client.Recall(49));

        Assert.Equal(serial + 1, client.ScrubSerial);
        Assert.Same(left, client.Scrub[0]);
        Assert.Same(client.State, client.Scrub[^1]);
        Assert.Equal(left.History.Count - 49 + 1, client.Scrub.Count);
        for (var i = 1; i < client.Scrub.Count - 1; i++)
        {
            Assert.Same(left.History[left.History.Count - i], client.Scrub[i]);
        }

        Assert.Equal(new[] { 5, 4 }, client.Scrub.Select(s => s.Turn).Distinct().ToArray());
        Assert.Null(client.Scrub[0].Find("teodor"));
        Assert.NotNull(client.Scrub[^1].Find("teodor"));
    }

    [Fact]
    public void ARefusedRecallLeavesTheScrubAlone()
    {
        var client = TurnFour();
        var serial = client.ScrubSerial;

        Assert.False(client.Recall(client.State.History.Count + 5));

        Assert.Equal(serial, client.ScrubSerial);
        Assert.Empty(client.Scrub);
    }

    [Fact]
    public void TheScrubGivesEachStepAnEqualShareAndRestsOnTheLastFrame()
    {
        Assert.Equal((0, 0f), Rhythm.ScrubAt(5, 0));
        Assert.Equal((2, 0f), Rhythm.ScrubAt(5, 0.5f));
        Assert.Equal((3, 0.5f), Rhythm.ScrubAt(5, 0.875f));
        Assert.Equal((3, 1f), Rhythm.ScrubAt(5, 1));
        Assert.Equal((3, 1f), Rhythm.ScrubAt(5, 7));
        Assert.Equal((0, 1f), Rhythm.ScrubAt(2, 1));
    }

    [Fact]
    public void TheKillingStrikesNumberIsLethalAndOthersAreNot()
    {
        Assert.True(new Pop(new Coord(0, 0), "8", PopKind.Damage, "t", 0).Lethal);
        Assert.True(new Pop(new Coord(0, 0), "21", PopKind.Crit, "t", 0).Lethal);
        Assert.False(new Pop(new Coord(0, 0), "8", PopKind.Damage, "t", 6).Lethal);
        Assert.False(new Pop(new Coord(0, 0), "miss", PopKind.Miss, "t", 0).Lethal);
    }

    [Fact]
    public void AnEnemyPhaseDeathHoldsItsKillingNumberAndPulsesRecall()
    {
        var client = TurnFour();
        Assert.True(client.State.RecallCharges > 0);
        client.Submit(new EndPhase());

        var death = StepToDeathOf(client, "teodor");

        Assert.NotNull(death.Held);
        Assert.Equal("8", death.Held!.Text);
        Assert.Equal("teodor", death.Held.TargetId);
        Assert.True(death.Pulse);
        Assert.Equal(Rhythm.Fade, Rhythm.PulseStart);
    }

    [Fact]
    public void NoPulseWithoutACharge()
    {
        var content = ContentLoader.Load(Fixture.RealContentDirectory());
        var map = MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "maps", "the_tollgate.map"), content);
        var client = new ClientSession(content, BattleState.From(map, content, content.Cast, 113));
        var script = File.ReadAllText(Path.Combine(Fixture.RealContentDirectory(), "..", "docs", "screenshots", "the_tollgate-113-enemy.script"));
        Ironwake.Client.Script.Apply(client, script);
        client.Submit(new EndPhase());
        client.Continue();
        Assert.True(client.Recall(49));
        Assert.Equal(0, client.State.RecallCharges);
        Ironwake.Client.Script.Apply(client, "move teodor 7,4\nwait teodor");
        client.Submit(new EndPhase());

        var death = StepToDeathOf(client, "teodor");

        Assert.NotNull(death.Held);
        Assert.False(death.Pulse);
    }

    [Fact]
    public void APlayerPhaseDeathHoldsItsNumberButNeverPulses()
    {
        // Turn 4's player phase ends with the captain's kill on archer-2.
        var content = ContentLoader.Load(Fixture.RealContentDirectory());
        var map = MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "maps", "the_tollgate.map"), content);
        var fresh = new ClientSession(content, BattleState.From(map, content, content.Cast, 113));
        var lines = File.ReadAllLines(Path.Combine(Fixture.RealContentDirectory(), "..", "docs", "screenshots", "the_tollgate-113-enemy.script"));
        var kill = Array.LastIndexOf(lines, "attack captain archer-2");
        Ironwake.Client.Script.Apply(fresh, string.Join("\n", lines.Take(kill + 1)));

        var death = fresh.Beats.Single(b => b.Fell is not null);
        Assert.Equal("archer-2", death.Held!.TargetId);
        Assert.False(death.Pulse);
    }

    [Fact]
    public void TheActCardClearsOnceThePhaseHasFlippedAndItsLastBeatHasPlayed()
    {
        var client = TurnFour();
        client.Submit(new EndPhase());
        StepToDeathOf(client, "teodor");
        Assert.NotNull(client.ActShown(beatsPlaying: false));

        client.Continue();

        Assert.NotNull(client.Act);
        Assert.NotNull(client.ActShown(beatsPlaying: true));
        Assert.Null(client.ActShown(beatsPlaying: false));
    }

    [Fact]
    public void ThePipsCountTheChargesTheMapOpenedWith()
    {
        var client = TurnFour();
        var opened = client.State.History[0].RecallCharges;

        Assert.Equal(opened, client.RecallChargesAtStart);
        Assert.True(client.State.RecallCharges < opened);
    }
}
