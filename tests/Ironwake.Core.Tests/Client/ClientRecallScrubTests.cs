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
    public void TheScrubStandsOnTheStepItsLengthsReachAndRestsOnTheLastFrame()
    {
        var lengths = new[] { 0.04f, 0.3f, 0.04f };
        Assert.Equal((0, 0f), Rhythm.ScrubAt(lengths, 0));
        Assert.Equal((0, 0.5f), Rhythm.ScrubAt(lengths, 0.02f));
        Assert.Equal((1, 0.5f), Rhythm.ScrubAt(lengths, 0.19f));
        var (step, through) = Rhythm.ScrubAt(lengths, 0.36f);
        Assert.Equal(2, step);
        Assert.Equal(0.5f, through, 4);
        Assert.Equal((2, 1f), Rhythm.ScrubAt(lengths, 0.5f));
        Assert.Equal((2, 1f), Rhythm.ScrubAt(lengths, 7));
        Assert.Equal((0, 1f), Rhythm.ScrubAt(Array.Empty<float>(), 0));
    }

    [Fact]
    public void TheScrubHoldsOnTheStepThatBringsTeodorBackAndBlursTheRest()
    {
        var client = TurnFour();
        client.Submit(new EndPhase());
        client.Continue();

        Assert.True(client.Recall(49));

        var lengths = client.ScrubLengths;
        Assert.Equal(client.Scrub.Count - 1, lengths.Count);
        var rise = Enumerable.Range(0, lengths.Count).Single(i => client.Scrub[i].Find("teodor") is null && client.Scrub[i + 1].Find("teodor") is not null);
        Assert.Equal(Rhythm.ScrubHold, lengths[rise]);
        Assert.All(lengths.Where((_, i) => client.Scrub[i + 1].Units.All(u => client.Scrub[i].Find(u.Id) is not null)), length => Assert.True(length <= Rhythm.ScrubStep));
        Assert.True(lengths.Sum() <= Rhythm.ScrubCap + 1e-4f);
    }

    [Fact]
    public void ALongScrubSqueezesItsPlainStepsUnderTheCapDownToAFrame()
    {
        var client = TurnFour();
        var still = client.State;

        var sixty = Rhythm.ScrubLengths(Enumerable.Repeat(still, 61).ToList());
        var thousand = Rhythm.ScrubLengths(Enumerable.Repeat(still, 1001).ToList());
        var short3 = Rhythm.ScrubLengths(Enumerable.Repeat(still, 4).ToList());

        Assert.All(sixty, length => Assert.Equal(Rhythm.ScrubCap / 60, length, 5));
        Assert.All(thousand, length => Assert.Equal(Rhythm.ScrubFrame, length));
        Assert.All(short3, length => Assert.Equal(Rhythm.ScrubStep, length));
    }

    [Fact]
    public void HoldsWinOverTheCap()
    {
        // Back and forth between the phase's end (Teodor dead) and the Recall's state (Teodor
        // alive): every step into the Recall's state brings him back, six holds past the cap.
        var client = TurnFour();
        client.Submit(new EndPhase());
        client.Continue();
        var after = client.State;
        Assert.True(client.Recall(49));
        var before = client.State;
        var frames = Enumerable.Range(0, 13).Select(i => i % 2 == 0 ? after : before).ToList();

        var lengths = Rhythm.ScrubLengths(frames);

        Assert.Equal(12, lengths.Count);
        Assert.All(lengths.Where((_, i) => i % 2 == 0), length => Assert.Equal(Rhythm.ScrubHold, length));
        Assert.All(lengths.Where((_, i) => i % 2 == 1 && after.Units.All(u => before.Find(u.Id) is not null)), length => Assert.Equal(Rhythm.ScrubFrame, length));
        Assert.True(lengths.Sum() > Rhythm.ScrubCap);
    }

    [Fact]
    public void ARecallMarksTheLogLinesItUndidAndNotItsOwnOrEarlierOnes()
    {
        var client = TurnFour();
        var before = client.Log.Count;
        client.Submit(new EndPhase());
        client.Continue();
        var death = client.Log.ToList().FindIndex(line => line.StartsWith("Teodor falls", StringComparison.Ordinal));
        var left = client.Log.Count;

        Assert.True(client.Recall(49));

        Assert.True(death >= 0);
        Assert.True(client.Undone(death));
        Assert.True(client.Undone(left - 1));
        Assert.True(client.Undone(before));
        Assert.False(client.Undone(0));
        Assert.False(client.Undone(client.Log.Count - 1));
        Assert.Contains(Enumerable.Range(0, before), i => !client.Undone(i));
    }

    [Fact]
    public void NoLineIsUndoneWithoutARecall()
    {
        var content = ContentLoader.Load(Fixture.RealContentDirectory());
        var map = MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "maps", "the_tollgate.map"), content);
        var client = new ClientSession(content, BattleState.From(map, content, content.Cast, 113));
        for (var turn = 0; turn < 3; turn++)
        {
            client.Submit(new EndPhase());
            client.Continue();
        }

        Assert.NotEmpty(client.Log);

        Assert.All(Enumerable.Range(0, client.Log.Count), i => Assert.False(client.Undone(i)));
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
