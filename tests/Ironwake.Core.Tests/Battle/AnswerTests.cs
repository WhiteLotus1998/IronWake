using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The one answer (DESIGN.md 13.29, experiment): on a <c>one_answer: on</c> map a unit that
/// counters is answered until the next phase begins, on either side, and makes no further counter
/// in that time. A map without the header, and a combat the defender never struck back in, spend nothing.
/// </summary>
public class AnswerTests
{
    private static string Field(bool answer, string units) =>
        $"""
        name: Field
        size: 8x5
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1
        {(answer ? "one_answer: on" : "")}

        ........
        ........
        ........
        ........
        ........

        units:
        {units}
        """.Replace("\n\n\n", "\n\n");

    private const string Line = """
        P captain 3,1
        P recruit:wren 3,3
        E brigand 3,2 group:field behavior:aggressive

        """;

    private static BattleState Start(bool answer = true) =>
        BattleFixture.Start(map: Field(answer, Line));

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

    [Fact]
    public void AUnitThatCountersMakesNoSecondCounterThisPhase()
    {
        var start = Start();
        var first = start.Try(new Attack("wren", "brigand-1"));
        Assert.True(first.Accepted, first.Rejection?.Message);
        var fought = Assert.Single(first.Events.OfType<CombatFought>());
        Assert.Contains(fought.Strikes, s => s.AttackerId == "brigand-1");
        var brigand = first.Next.Find("brigand-1")!;
        Assert.True(brigand.Answered);

        var hale = first.Next.Find("hale")!;
        var forecast = Queries.Forecast(first.Next, Starter, hale, brigand, hale.At, null)!;
        Assert.False(forecast.Defender.Strikes);
        Assert.True(forecast.CounterAnswered);
        Assert.Equal(forecast, ProtocolJson.ReadForecast(ProtocolJson.Forecast(forecast)));

        var second = first.Next.Try(new Attack("hale", "brigand-1"));
        Assert.True(second.Accepted, second.Rejection?.Message);
        Assert.DoesNotContain(Assert.Single(second.Events.OfType<CombatFought>()).Strikes, s => s.AttackerId == "brigand-1");
    }

    [Fact]
    public void AnAnsweredUnitIsSaidToHaveAnsweredOnlyWhereItWouldHaveCountered()
    {
        var start = Start();
        var brigand = start.Find("brigand-1")! with { Answered = true };
        var state = start.WithUnit(brigand);
        var hale = state.Find("hale")!.ToCombatant(state, Starter, against: brigand);
        var answering = brigand.Answering(state, Starter, new Coord(3, 1), state.Find("hale"));

        Assert.True(answering.AnswerSpent);
        Assert.True(Ironwake.Core.Combat.Forecast(hale, answering, 1, state.Scheme).CounterAnswered);
        Assert.False(Ironwake.Core.Combat.Forecast(hale, answering with { Blind = true }, 1, state.Scheme).CounterAnswered);
    }

    [Fact]
    public void WithoutTheHeaderACounterSpendsNothing()
    {
        var next = Apply(Start(answer: false), new Attack("wren", "brigand-1"));
        var brigand = next.Find("brigand-1")!;
        Assert.False(brigand.Answered);

        var hale = next.Find("hale")!;
        Assert.True(Queries.Forecast(next, Starter, hale, brigand, hale.At, null)!.Defender.Strikes);
    }

    [Fact]
    public void AStrikeNeverAnsweredDoesNotSpendTheAnswer()
    {
        var map = Start().Map;
        Assert.False(Answer.Spends(map, "brigand-1", ValueList<StrikeEvent>.Empty.Add(new StrikeEvent(0, "wren", "brigand-1", true, false, 3, 10))));
        Assert.True(Answer.Spends(map, "brigand-1", ValueList<StrikeEvent>.Empty.Add(new StrikeEvent(0, "brigand-1", "wren", false, false, 0, 10))));
        Assert.False(Answer.Spends(Start(answer: false).Map, "brigand-1", ValueList<StrikeEvent>.Empty.Add(new StrikeEvent(0, "brigand-1", "wren", false, false, 0, 10))));
    }

    [Fact]
    public void TheAnswerComesBackWhenTheNextPhaseBegins()
    {
        var start = Start();
        var answered = start.WithUnit(start.Find("brigand-1")! with { Answered = true });

        var enemyPhase = Apply(answered, new EndPhase());

        Assert.False(enemyPhase.Find("brigand-1")?.Answered ?? false);
    }

    [Fact]
    public void TheHeaderRoundTripsAndPrintsItsRule()
    {
        var map = MapFixture.Parse(Field(true, Line), "test.map");
        Assert.True(map.OneAnswerEnabled);
        Assert.True(MapFixture.Parse(MapFormat.Write(map, Starter), "again.map").OneAnswerEnabled);
        Assert.False(MapFixture.Parse(Field(false, Line), "plain.map").OneAnswerEnabled);

        Assert.Contains(Answer.Legend, MapRenderer.Render(Start(), Starter));
        Assert.DoesNotContain(Answer.Legend, MapRenderer.Render(Start(answer: false), Starter));
    }

    [Fact]
    public void AnAnsweredUnitReadsBackFromTheProtocol()
    {
        var start = Start();
        var state = start.WithUnit(start.Find("brigand-1")! with { Answered = true });
        var flat = state with { History = ValueList<BattleState>.Empty };

        Assert.Contains("\"answered\":true", ProtocolJson.State(flat, Starter));
        Assert.Equal(flat, ProtocolJson.ReadState(ProtocolJson.State(flat, Starter), Starter));
    }
}
