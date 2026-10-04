using System.Text.Json;
using Ironwake.Cli;
using Ironwake.Content;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// The count over the protocol (issue 928, PROTOCOL.md): the <c>count</c> query carries each player
/// unit's count and the console's line, and <c>end</c> names in <c>countPassed</c> the units whose
/// last start the phase's end passes.
/// </summary>
public class EscapeCountProtocolTests
{
    private static GameContent Real => ContentLoader.Load(Fixture.RealContentDirectory());

    private static BattleState Brackwater(GameContent content) =>
        BattleState.From(MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "maps", "brackwater_cut.map"), content), content, content.Cast, 283);

    [Fact]
    public void TheCountQueryCarriesEachPlayerUnitsCountAndTheConsoleLine()
    {
        var content = Real;
        var state = Brackwater(content);
        var expected = EscapeCount.Of(state, content);

        using var answer = JsonDocument.Parse(new ProtocolSession(content, state, new StringWriter()).Answer("""{"query":"count"}"""));

        var root = answer.RootElement;
        Assert.True(root.GetProperty("ok").GetBoolean());
        var counts = root.GetProperty("counts").EnumerateArray().ToList();
        Assert.Equal(expected.Select(c => c.Unit.Id), counts.Select(c => c.GetProperty("unit").GetString()));
        Assert.Equal(expected.Select(c => c.Phases), counts.Select(c => (int?)c.GetProperty("phases").GetInt32()));
        Assert.Equal(expected.Select(c => c.LastStart), counts.Select(c => (int?)c.GetProperty("lastStart").GetInt32()));
        Assert.Equal(EscapeCount.Line(state, content, UnitNames.Of(state, content)), root.GetProperty("text").GetString());
    }

    [Fact]
    public void EndNamesTheUnitsWhoseLastStartItPassesInCountPassed()
    {
        var content = Real;
        var start = Brackwater(content);
        var captain = start.UnitsOf(Side.Player).First(u => u.IsCaptain);
        var lastStart = EscapeCount.Of(start, content).Single(c => c.Unit.Id == captain.Id).LastStart!.Value;

        using var onIt = JsonDocument.Parse(new ProtocolSession(content, start with { Turn = lastStart }, new StringWriter()).Answer("""{"type":"end"}"""));
        using var before = JsonDocument.Parse(new ProtocolSession(content, start with { Turn = lastStart - 1 }, new StringWriter()).Answer("""{"type":"end"}"""));

        Assert.Contains(captain.Id, onIt.RootElement.GetProperty("countPassed").EnumerateArray().Select(e => e.GetString()));
        Assert.DoesNotContain(captain.Id, before.RootElement.GetProperty("countPassed").EnumerateArray().Select(e => e.GetString()));
    }
}
