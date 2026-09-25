using System.Text.Json;
using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Content.Protocol;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// <c>play --protocol</c> (issue 25): the same game over JSON lines as over <c>--script</c>,
/// the queries answering with the console's own text, and every refusal answered as
/// <c>ok: false</c> with the session going on.
/// </summary>
[Collection("console")]
public class ProtocolSessionTests
{
    /// <summary>The journaled seed-163 Tollgate line (a Recall, the rider's arrival, a seize on turn 10).</summary>
    public static string TollgateScript() =>
        File.ReadAllText(Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "transcripts", "2026-09-25-the_tollgate-163.script"));

    /// <summary>
    /// A text script's commands as protocol lines: the console's one-based slots become the
    /// core's zero-based ones, and the lines that only ask (forecast, show, map, a bare recall)
    /// are dropped, since they apply nothing.
    /// </summary>
    public static IEnumerable<string> JsonCommands(string script)
    {
        foreach (var raw in script.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var w = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Command? command = w[0] switch
            {
                "move" => new Move(w[1], Coord(w[2])),
                "attack" => new Attack(w[1], w[2], w.Length == 4 ? int.Parse(w[3]) - 1 : null),
                "item" => new UseItem(w[1], int.Parse(w[2]) - 1, w.Length == 4 ? w[3] : null),
                "wait" => new Wait(w[1]),
                "end" => new EndPhase(),
                "recall" when w.Length == 2 => new Recall(int.Parse(w[1])),
                _ => null,
            };
            if (command is not null)
            {
                yield return ProtocolJson.Command(command);
            }
        }
    }

    private static Coord Coord(string text)
    {
        var parts = text.Split(',');
        return new Coord(int.Parse(parts[0]), int.Parse(parts[1]));
    }

    /// <summary>
    /// The issue's acceptance: the seed-163 line over <c>--protocol</c> produces the events the
    /// same line produces over <c>--script</c>. Every protocol answer is accepted, every event's
    /// console text appears in the text transcript in the same order with nothing skipped
    /// between two events of one answer, and both runs win.
    /// </summary>
    [Fact]
    public void AScriptedPlaythroughOverTheProtocolProducesTheScriptsEvents()
    {
        var scriptPath = Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "transcripts", "2026-09-25-the_tollgate-163.script");
        var jsonPath = Path.Combine(Path.GetTempPath(), "ironwake-protocol-" + Guid.NewGuid().ToString("N") + ".jsonl");
        File.WriteAllLines(jsonPath, JsonCommands(TollgateScript()));
        try
        {
            var text = Run(out var textExit, "play", "the_tollgate", "--seed", "163", "--script", scriptPath, "--strict", "--content", Fixture.RealContentDirectory());
            var protocol = Run(out var protocolExit, "play", "the_tollgate", "--seed", "163", "--script", jsonPath, "--protocol", "--content", Fixture.RealContentDirectory());

            Assert.Equal(0, textExit);
            Assert.Equal(0, protocolExit);
            var answers = protocol.TrimEnd('\n').Split('\n');
            Assert.Equal(JsonCommands(TollgateScript()).Count() + 1, answers.Length);
            var events = 0;
            var at = 0;
            foreach (var answer in answers.Skip(1))
            {
                using var doc = JsonDocument.Parse(answer);
                Assert.True(doc.RootElement.GetProperty("ok").GetBoolean(), answer);
                foreach (var e in doc.RootElement.GetProperty("events").EnumerateArray())
                {
                    var line = e.GetProperty("text").GetString()! + "\n";
                    var found = text.IndexOf(line, at, StringComparison.Ordinal);
                    Assert.True(found >= 0, $"event '{line.TrimEnd()}' is not in the text transcript after offset {at}");
                    at = found + line.Length;
                    events++;
                }
            }

            Assert.True(events > 100, $"only {events} events");
            using var last = JsonDocument.Parse(answers[^1]);
            Assert.Equal("won", last.RootElement.GetProperty("state").GetProperty("outcome").GetProperty("result").GetString());
            Assert.EndsWith("battle won: seize\n", text);
        }
        finally
        {
            File.Delete(jsonPath);
        }
    }

    [Fact]
    public void TheFirstLineIsTheOpeningStateInFull()
    {
        var (content, session, output) = Session();

        session.Run(new StringReader(""));

        using var doc = JsonDocument.Parse(output.ToString().TrimEnd());
        Assert.Equal(ProtocolVersion.Current, doc.RootElement.GetProperty("protocolVersion").GetInt32());
        var state = doc.RootElement.GetProperty("state");
        Assert.Equal(session.State, ProtocolJson.ReadState(state.GetRawText(), content));
    }

    [Fact]
    public void ARefusedCommandAnswersWithTheCoresReasonAndTheSessionGoesOn()
    {
        var (_, session, _) = Session();

        Assert.Equal("""{"ok":false,"error":{"reason":"outOfReach","message":"captain cannot move to 99,0: outside the map"}}""", session.Answer("""{"type":"move","unit":"captain","to":{"x":99,"y":0}}"""));
        var core = Resolver.Apply(session.State, Content(), new Wait("nobody")).Rejection!;
        Assert.Equal(RejectionReason.NoSuchUnit, core.Reason);
        Assert.Equal("{\"ok\":false,\"error\":{\"reason\":\"noSuchUnit\",\"message\":\"" + core.Message + "\"}}", session.Answer("""{"type":"wait","unit":"nobody"}"""));
        Assert.StartsWith("""{"ok":true,"events":[{"type":"unitWaited","unit":"captain","text":"captain waits"}],"state":{""", session.Answer("""{"type":"wait","unit":"captain"}"""));
    }

    [Theory]
    [InlineData("not json", "not JSON")]
    [InlineData("[1]", "a request is a JSON object")]
    [InlineData("""{"type":"jump"}""", "type 'jump' is not a command")]
    [InlineData("""{"query":"weather"}""", "query 'weather' is not one of: state, reachable, targets, forecast, threat")]
    [InlineData("""{"query":"reachable"}""", "field 'unit' is missing")]
    public void AMalformedRequestAnswersBadRequestAndTheSessionGoesOn(string line, string message)
    {
        var (_, session, _) = Session();

        using var doc = JsonDocument.Parse(session.Answer(line));

        Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("badRequest", doc.RootElement.GetProperty("error").GetProperty("reason").GetString());
        Assert.Contains(message, doc.RootElement.GetProperty("error").GetProperty("message").GetString());
        Assert.StartsWith("{\"ok\":true", session.Answer("""{"type":"wait","unit":"captain"}"""));
    }

    [Fact]
    public void TheQueriesAnswerWhatTheCoreAnswers()
    {
        var (content, session, _) = Session();
        var wren = session.State.Find("wren")!;

        using var reach = JsonDocument.Parse(session.Answer("""{"query":"reachable","unit":"wren"}"""));
        var tiles = reach.RootElement.GetProperty("reach").GetProperty("tiles").EnumerateArray().Select(t => new Coord(t.GetProperty("x").GetInt32(), t.GetProperty("y").GetInt32())).ToList();
        Assert.Equal(Queries.Reachable(session.State, content, wren).Entries.Select(e => e.At), tiles);

        Assert.Equal("""{"ok":true,"query":"targets","unit":"wren","targets":[]}""", session.Answer("""{"query":"targets","unit":"wren"}"""));
        Assert.Equal("""{"ok":false,"error":{"reason":"noSuchUnit","message":"no living unit 'ghost'"}}""", session.Answer("""{"query":"targets","unit":"ghost"}"""));

        using var state = JsonDocument.Parse(session.Answer("""{"query":"state"}"""));
        Assert.Equal(session.State, ProtocolJson.ReadState(state.RootElement.GetProperty("state").GetRawText(), content));
    }

    /// <summary>The forecast query's text is the console's forecast line for the same question, and its numbers are the core's.</summary>
    [Fact]
    public void TheForecastQueryCarriesTheConsolesLine()
    {
        var (content, session, _) = Session();
        foreach (var line in JsonCommands(TollgateScript()).Take(40))
        {
            session.Answer(line);
        }

        var state = session.State;
        var pair = state.UnitsOf(Side.Player)
            .SelectMany(u => state.UnitsOf(Side.Enemy).Select(e => (Unit: u, Target: e)))
            .First(p => Queries.Forecast(state, content, p.Unit, p.Target) is not null);
        var forecast = Queries.Forecast(state, content, pair.Unit, pair.Target)!;

        using var doc = JsonDocument.Parse(session.Answer(ProtocolJson.Write(w => { w.WriteStartObject(); w.WriteString("query", "forecast"); w.WriteString("unit", pair.Unit.Id); w.WriteString("target", pair.Target.Id); w.WriteEndObject(); })));

        Assert.Equal(forecast, ProtocolJson.ReadForecast(doc.RootElement.GetProperty("forecast").GetRawText()));
        Assert.StartsWith(PlaySession.ForecastLine(pair.Unit, pair.Target, forecast), doc.RootElement.GetProperty("text").GetString());
    }

    [Fact]
    public void AForecastWithNoAnswerIsRefusedWithTheConsolesReason()
    {
        var (_, session, _) = Session();

        Assert.Equal("""{"ok":false,"error":{"reason":"outOfReach","message":"captain cannot move to 0,0"}}""", session.Answer("""{"query":"forecast","unit":"captain","target":"archer-1","from":{"x":0,"y":0}}"""));
        Assert.Equal("""{"ok":false,"error":{"reason":"outOfRange","message":"captain cannot attack archer-1 from 6,11"}}""", session.Answer("""{"query":"forecast","unit":"captain","target":"archer-1"}"""));
        Assert.Equal("""{"ok":false,"error":{"reason":"notThisSide","message":"archer-1 is an enemy; threat answers for a player unit"}}""", session.Answer("""{"query":"threat","unit":"archer-1"}"""));
    }

    [Fact]
    public void TheThreatQueryCarriesTheConsolesBlock()
    {
        var (_, session, _) = Session();

        Assert.Equal("""{"ok":true,"query":"threat","unit":"captain","from":{"x":6,"y":11},"threats":[],"ifAllLand":0,"text":"threat on captain at 6,11 (Plain): no enemy can strike it next phase"}""", session.Answer("""{"query":"threat","unit":"captain"}"""));
    }

    [Fact]
    public void StrictIsRefusedWithTheProtocol()
    {
        var output = Run(out var exit, "play", "the_tollgate", "--protocol", "--strict", "--content", Fixture.RealContentDirectory());

        Assert.Equal(2, exit);
        Assert.StartsWith("ERROR: --strict applies to a text script", output);
    }

    private static GameContent Content() => ContentLoader.Load(Fixture.RealContentDirectory());

    private static (GameContent Content, ProtocolSession Session, StringWriter Output) Session()
    {
        var content = Content();
        var map = MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "maps", "the_tollgate.map"), content);
        var output = new StringWriter();
        return (content, new ProtocolSession(content, BattleState.From(map, content, content.Cast, 163), output), output);
    }

    private static string Run(out int exit, params string[] args)
    {
        var code = 0;
        var output = ConsoleCapture.Run(() => code = Ironwake.Cli.Program.Main(args));
        exit = code;
        return output;
    }
}
