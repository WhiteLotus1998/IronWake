using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Client;

/// <summary>
/// Showcase slice 5 (issue 515): the how-to-play text, the three turn-1 callouts dismissed by
/// doing what they say, and the end card, on the Tollgate.
/// </summary>
public class ClientScreensTests
{
    private static ClientSession Fresh(ulong seed)
    {
        var content = ContentLoader.Load(Fixture.RealContentDirectory());
        var map = MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "maps", "the_tollgate.map"), content);
        return new ClientSession(content, BattleState.From(map, content, content.Cast, seed));
    }

    [Fact]
    public void RecallIsExplainedInTheRecallCardsFirstLine()
    {
        var sentence = Screens.RecallSentence;
        Assert.EndsWith(".", sentence, StringComparison.Ordinal);
        Assert.Contains("the dice remember", sentence, StringComparison.Ordinal);
        Assert.Contains(Screens.HowTo, section => section.Heading == "Recall" && section.Lines[0] == sentence);
    }

    [Fact]
    public void TheHowToPlayNamesTheFourCommandsTheForecastRecallAndTheGoal()
    {
        var text = string.Join("\n", Screens.HowTo.SelectMany(s => s.Lines.Prepend(s.Heading)));
        foreach (var word in new[] { "Move:", "Strike:", "Wait:", "End:", "forecast", "Recall", "GOAL" })
        {
            Assert.Contains(word, text, StringComparison.Ordinal);
        }

        Assert.All(text, c => Assert.True(c < 128, $"non-ASCII {(int)c}"));
    }

    [Fact]
    public void TheCalloutsGoSelectForecastEndEachDismissedByDoingIt()
    {
        var client = Fresh(113);
        var callouts = new Callouts();
        callouts.Observe(client, null);
        Assert.Equal(Callout.Select, callouts.Showing);

        var captain = client.State.UnitsOf(Side.Player).First(u => u.IsCaptain);
        client.Select(captain.At);
        callouts.Observe(client, null);
        Assert.Equal(Callout.Forecast, callouts.Showing);

        callouts.Observe(client, captain.At);
        Assert.Equal(Callout.Forecast, callouts.Showing);

        callouts.Observe(client, new Coord(0, 0));
        Assert.Equal(Callout.Forecast, callouts.Showing);

        var reach = client.Reach!;
        var map = client.State.Map;
        var stop = Enumerable.Range(0, map.Width * map.Height).Select(i => new Coord(i % map.Width, i / map.Width))
            .First(tile => tile != captain.At && reach.CanEnd(tile));
        callouts.Observe(client, stop);
        Assert.Equal(Callout.End, callouts.Showing);

        client.Submit(new EndPhase());
        callouts.Observe(client, null);
        Assert.Null(callouts.Showing);
    }

    [Fact]
    public void AForecastOnAnEnemyInReachAlsoDismissesTheSecondCallout()
    {
        // The first shipped map, seed and unit with a strike to price on turn 1, found by search.
        var content = ContentLoader.Load(Fixture.RealContentDirectory());
        var found = Directory.GetFiles(Path.Combine(Fixture.RealContentDirectory(), "maps"), "*.map").OrderBy(f => f, StringComparer.Ordinal)
            .SelectMany(file => new ulong[] { 1, 7, 113 }.Select(seed => new ClientSession(content, BattleState.From(MapFiles.Load(file, content), content, content.Cast, seed))))
            .SelectMany(client => client.State.UnitsOf(Side.Player).Select(unit => (client, unit)))
            .Select(pair =>
            {
                pair.client.Select(pair.unit.At);
                var map = pair.client.State.Map;
                var tile = Enumerable.Range(0, map.Width * map.Height).Select(i => new Coord(i % map.Width, i / map.Width))
                    .Cast<Coord?>().FirstOrDefault(t => pair.client.Hover(t!.Value).Count > 0);
                return (pair.client, tile);
            })
            .First(pair => pair.tile is not null);
        var callouts = new Callouts();
        callouts.Observe(found.client, null);
        Assert.Equal(Callout.Forecast, callouts.Showing);

        callouts.Observe(found.client, found.tile);

        Assert.Equal(Callout.End, callouts.Showing);
    }

    [Fact]
    public void EndingThePhaseEarlyDismissesEveryCalloutForGood()
    {
        var client = Fresh(113);
        var callouts = new Callouts();
        client.Submit(new EndPhase());
        client.Continue();
        callouts.Observe(client, null);
        Assert.Null(callouts.Showing);

        Assert.Equal(Side.Player, client.State.Phase);
        client.Select(client.State.UnitsOf(Side.Player).First().At);
        callouts.Observe(client, null);
        Assert.Null(callouts.Showing);
    }

    [Fact]
    public void EachCalloutSaysWhatDismissesIt()
    {
        Assert.Contains("select", Callouts.Text(Callout.Select), StringComparison.Ordinal);
        Assert.Contains("Point at a tile", Callouts.Text(Callout.Forecast), StringComparison.Ordinal);
        Assert.Contains("press E", Callouts.Text(Callout.End), StringComparison.Ordinal);
    }

    [Fact]
    public void NoEndCardWhileTheBattleIsOn()
    {
        Assert.Null(Fresh(113).Ending);
    }

    [Fact]
    public void AWonBattleNamesWhoItCostAndTheTurn()
    {
        var client = Fresh(113);
        Ironwake.Client.Script.Apply(client, File.ReadAllText(Path.Combine(Fixture.RealContentDirectory(), "..", "docs", "transcripts", "2026-09-27-the_tollgate-113.script")));

        var card = client.Ending;

        Assert.NotNull(card);
        Assert.True(card!.Won);
        Assert.Equal("Won", card.Headline);
        Assert.Equal($"turn {client.State.Turn} of 10", card.Turn);
        var start = client.State.History[0];
        var lost = start.UnitsOf(Side.Player).Where(u => client.State.Find(u.Id) is null).Select(u => u.Unit.Name).ToList();
        if (lost.Count == 0)
        {
            Assert.Equal("Nobody left behind.", card.Line);
        }
        else
        {
            Assert.StartsWith("Won, and it cost ", card.Line, StringComparison.Ordinal);
            Assert.All(lost, name => Assert.Contains(name, card.Line, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void ASeizeLostOnTheClockSaysSoWithoutCoordinatesAndTheLogKeepsThem()
    {
        var client = Fresh(113);
        for (var turn = 0; turn < 12 && !client.State.Outcome.IsOver; turn++)
        {
            client.Submit(new EndPhase());
            client.Continue();
        }

        var card = client.Ending;

        Assert.NotNull(card);
        Assert.False(card!.Won);
        Assert.Equal("Lost", card.Headline);
        Assert.Equal("Turn 10 ran out with the captain short of the gate.", card.Line);
        Assert.DoesNotMatch(@"\d+,\d+", card.Line);
        var verdict = Ironwake.Core.Objective.Verdict(client.State, client.Content)!;
        Assert.Contains("7,1", verdict, StringComparison.Ordinal);
        Assert.Equal("turn 10 of 10", card.Turn);
        Assert.Equal(11, client.State.Turn);
    }

    [Fact]
    public void AnyOtherLossSaysWhyInTheConsolesWords()
    {
        var client = Fresh(113);
        var captain = client.State.UnitsOf(Side.Player).Single(u => u.IsCaptain);
        var state = client.State.WithoutUnit(captain.Id);
        Assert.Equal(LossCause.Captain, state.Outcome.Cause);

        var card = EndCard.Of(state, client.Content);

        var verdict = Ironwake.Core.Objective.Verdict(state, client.Content)!;
        Assert.Equal("Lost because the captain fell.", verdict);
        Assert.Equal("The captain fell.", card.Line);
    }

    [Fact]
    public void TheCardWaitsForTheEnemyPhaseToPlayOut()
    {
        var client = Fresh(113);
        for (var turn = 0; turn < 12; turn++)
        {
            client.Submit(new EndPhase());
            while (client.Step())
            {
                if (client.EnemyPhasePlaying)
                {
                    Assert.Null(client.Ending);
                }
            }

            if (client.State.Outcome.IsOver)
            {
                break;
            }
        }

        Assert.NotNull(client.Ending);
    }
}
