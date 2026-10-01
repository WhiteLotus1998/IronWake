using Ironwake.Cli;
using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Client;

/// <summary>
/// The readability pass (issue 349): each enemy-phase step marks on the board the event its
/// log line names, the line and the mark are the same event, the dark marks nothing, and a
/// player command clears the mark; the unit panel is the console's <c>show</c>; long lines
/// wrap inside the panel.
/// </summary>
[Collection("console")]
public class ClientReadabilityTests
{
    private static ClientSession Brackwater()
    {
        var content = ContentLoader.Load(Fixture.RealContentDirectory());
        var map = MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "maps", "brackwater_cut.map"), content);
        return new ClientSession(content, BattleState.From(map, content, content.Cast, 53));
    }

    /// <summary>Ends three player phases, stepping each enemy phase, and returns each step's highlight with the board it was drawn on.</summary>
    private static List<(Highlight Mark, BattleState Before, string LogLast)> Steps(ClientSession client)
    {
        var steps = new List<(Highlight, BattleState, string)>();
        for (var turn = 0; turn < 3; turn++)
        {
            Assert.True(client.Submit(new EndPhase()));
            var before = client.State;
            while (client.Step())
            {
                steps.Add((client.Playing!, before, client.Log[^1]));
                before = client.State;
            }
        }

        return steps;
    }

    [Fact]
    public void EachStepMarksTheEventItsLogLineNames()
    {
        var steps = Steps(Brackwater());

        Assert.NotEmpty(steps);
        Assert.All(steps, s => Assert.Equal(s.LogLast, s.Mark.Line));
    }

    [Fact]
    public void AMoveMarksItsStartAndEndTiles()
    {
        var moves = Steps(Brackwater()).Where(s => s.Mark.Line.Contains(" moves ", StringComparison.Ordinal)).ToList();

        Assert.NotEmpty(moves);
        Assert.All(moves, s => Assert.Contains($" moves {s.Mark.From!.Value.X},{s.Mark.From.Value.Y} -> {s.Mark.To!.Value.X},{s.Mark.To.Value.Y}", s.Mark.Line, StringComparison.Ordinal));
    }

    [Fact]
    public void AStrikeMarksWhereTheAttackerStoodAndWhomItStruck()
    {
        var client = Brackwater();
        var strikes = Steps(client).Where(s => s.Mark.Line.Contains(" attacks ", StringComparison.Ordinal)).ToList();

        Assert.NotEmpty(strikes);
        Assert.All(strikes, s =>
        {
            var names = UnitNames.Of(s.Before, client.Content);
            var head = s.Mark.Line.Split('\n')[0];
            var attacker = Assert.Single(s.Before.Units, u => head.StartsWith(names[u.Id] + " attacks ", StringComparison.Ordinal));
            var target = Assert.Single(s.Before.Units, u => head.EndsWith(" attacks " + names[u.Id], StringComparison.Ordinal));
            Assert.Equal(attacker.At, s.Mark.To);
            Assert.Equal(target.At, s.Mark.Struck);
        });
    }

    [Fact]
    public void AnActInTheDarkMarksNoTile()
    {
        var dark = Steps(Brackwater()).Where(s => s.Mark.Line == ProtocolSession.DarkLine).ToList();

        Assert.NotEmpty(dark);
        Assert.All(dark, s => Assert.True(s.Mark.From is null && s.Mark.To is null && s.Mark.Struck is null && s.Mark.Path.Count == 0));
    }

    [Fact]
    public void ThePhasesLastMarkStaysUntilAPlayerCommand()
    {
        var client = Brackwater();
        client.Submit(new EndPhase());
        client.Continue();
        Assert.NotNull(client.Playing);

        client.Submit(new Wait(client.State.UnitsOf(Side.Player).First().Id));

        Assert.Null(client.Playing);
    }

    [Fact]
    public void TheUnitPanelIsTheConsolesShow()
    {
        var client = Brackwater();
        var rook = client.State.Find("rook")!;
        var console = ConsoleCapture.Run(() => _ = Ironwake.Cli.Program.Main(new[] { "play", Path.Combine(Fixture.RealContentDirectory(), "maps", "brackwater_cut.map"), "--seed", "53", "--script", Script("show rook"), "--content", Fixture.RealContentDirectory() }));

        var panel = client.Show(rook.At)!;

        Assert.Contains(string.Join("\n", panel) + "\n", console, StringComparison.Ordinal);
    }

    [Fact]
    public void TheUnitPanelShowsNothingForAnEnemyInTheDark()
    {
        var client = Brackwater();
        var unseen = client.State.UnitsOf(Side.Enemy).First(u => !Dusk.Seen(client.State, u));

        Assert.Null(client.Show(unseen.At));
        Assert.Null(client.Show(new Coord(0, 0)));
    }

    private static string Script(string text)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ironwake-show-{Guid.NewGuid():N}.script");
        File.WriteAllText(path, text + "\n");
        return path;
    }

    [Theory]
    [InlineData("short line", 20, new[] { "short line" })]
    [InlineData("forecast wren -> archer: dmg 5 hit 70%", 20, new[] { "forecast wren ->", "  archer: dmg 5 hit", "  70%" })]
    [InlineData("  rider-1 hits dunstan for 6", 16, new[] { "  rider-1 hits", "    dunstan for", "    6" })]
    [InlineData("abcdefghijklmnopqrstuvwxyz", 10, new[] { "abcdefghij", "  klmnopqr", "  stuvwxyz" })]
    public void WrapBreaksAtSpacesAndIndentsContinuations(string line, int columns, string[] rows)
    {
        Assert.Equal(rows, TextLayout.Wrap(line, columns));
    }

    [Fact]
    public void WrapRefusesAPanelTooNarrowToWrap()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TextLayout.Wrap("anything at all", 4).ToList());
    }
}
