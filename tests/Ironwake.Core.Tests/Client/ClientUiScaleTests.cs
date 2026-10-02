using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Client;

/// <summary>
/// UI scale in the Godot client (issue 698): the layout at 100, 125 and 150 read from one factor,
/// every board and its column fitting the window at each, the key strip wrapping into the rows
/// the layout keeps for it, and the Options row that cycles the scale.
/// </summary>
public class ClientUiScaleTests
{
    private static readonly GameContent Content = ContentLoader.Load(Fixture.RealContentDirectory());

    /// <summary>Every board the client can open: the campaign's, the keep's, the quests', the trials' and the samples'.</summary>
    public static IEnumerable<object[]> Boards()
    {
        var content = Fixture.RealContentDirectory();
        var samples = Path.Combine(Directory.GetParent(content)!.FullName, "docs", "samples");
        return new[] { "maps", "keep", "quests", "trials" }.Select(d => Path.Combine(content, d)).Append(samples)
            .SelectMany(d => Directory.GetFiles(d, "*.map"))
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => new object[] { Path.GetRelativePath(Directory.GetParent(content)!.FullName, f) });
    }

    private static (int Width, int Height) Size(string relative)
    {
        var map = MapFiles.Load(Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, relative), Content);
        return (map.Width, map.Height);
    }

    [Fact]
    public void AtOneHundredTheLayoutIsTheOneTheClientDrewBeforeScale()
    {
        var layout = UiLayout.For(100);

        Assert.Equal((1280, 720), (layout.ViewWidth, layout.ViewHeight));
        Assert.Equal((500, 48, 680), (layout.PanelWidth, layout.Top, layout.FooterTop));
        Assert.Equal((1, 1), (layout.TopRows, layout.KeyRows));

        // The old formula, written out with its constants: the Tollgate's 14 by 12 took a tile of 45.
        Assert.Equal(Math.Min((1280 - 500 - 40 - 32 - 12) / 14, (680 - 48 - 76 - 8) / 12), layout.TileFor(14, 12));
        Assert.Equal(45, layout.TileFor(14, 12));
    }

    [Theory]
    [InlineData(100, 1280, 720)]
    [InlineData(125, 1024, 576)]
    [InlineData(150, 853, 480)]
    public void TheCanvasIsTheWindowOverTheFactorAndNeverOverrunsIt(int scale, int width, int height)
    {
        var layout = UiLayout.For(scale);

        Assert.Equal((width, height), (layout.ViewWidth, layout.ViewHeight));
        Assert.True(layout.ViewWidth * layout.Factor <= UiLayout.WindowWidth);
        Assert.True(layout.ViewHeight * layout.Factor <= UiLayout.WindowHeight);
    }

    [Theory]
    [InlineData(125)]
    [InlineData(150)]
    public void ALargerScaleNarrowsTheColumnAndGivesTheTopBarAndTheKeyStripASecondRow(int scale)
    {
        var layout = UiLayout.For(scale);

        Assert.True(layout.PanelWidth < UiLayout.For(100).PanelWidth);
        Assert.Equal((2, 2), (layout.TopRows, layout.KeyRows));
        Assert.Equal(48 + UiLayout.TopRowHeight, layout.Top);
        Assert.Equal(layout.ViewHeight - 40 - UiLayout.KeyRowHeight, layout.FooterTop);
    }

    [Theory]
    [MemberData(nameof(Boards))]
    public void EveryBoardAndItsColumnFitInsideTheWindowAtEveryScale(string board)
    {
        var (width, height) = Size(board);
        foreach (var scale in Options.UiScales)
        {
            var layout = UiLayout.For(scale);
            var tile = layout.TileFor(width, height);

            Assert.True(tile >= 12, $"{board} at {scale}: a tile of {tile}");
            Assert.True(layout.BlockWidth(width, tile) + 2 * UiLayout.Margin <= layout.ViewWidth, $"{board} at {scale}: {layout.BlockWidth(width, tile)} across {layout.ViewWidth}");
            Assert.True(layout.Top + height * tile + UiLayout.LegendRoom + 8 <= layout.FooterTop, $"{board} at {scale}: too tall");
            Assert.True(layout.ViewWidth * layout.Factor <= UiLayout.WindowWidth);
        }
    }

    [Fact]
    public void TheTileShrinksAsTheScaleGrows()
    {
        var tiles = Options.UiScales.Select(s => UiLayout.For(s).TileFor(14, 12)).ToList();

        Assert.True(tiles[0] > tiles[1] && tiles[1] > tiles[2], string.Join(", ", tiles));
    }

    [Fact]
    public void TheColumnFollowsTheLegendAtOneHundredOnTheTollgate()
    {
        var layout = UiLayout.For(100);
        var foot = 56f + 12 * 45;

        Assert.Equal(foot + UiLayout.LegendRoom, layout.ColumnBottom(foot, 68, 236 + 4 * 17));
    }

    [Fact]
    public void AtOneHundredAndFiftyTheColumnOutgrowsAShortBoardToHoldTheForecast()
    {
        var layout = UiLayout.For(150);
        // A board 20 across and 6 down: the width sets the tile, so the board ends well above the forecast's foot.
        var tile = layout.TileFor(20, 6);
        var foot = layout.Top + 6f * tile;
        var top = layout.Top + 12f;

        var bottom = layout.ColumnBottom(foot, top, 236 + 4 * 17);

        Assert.True(foot + UiLayout.LegendRoom < top + 236 + 4 * 17);
        Assert.Equal(top + 236 + 4 * 17, bottom);
    }

    [Fact]
    public void TheColumnNeverRunsIntoTheKeyStrip()
    {
        var layout = UiLayout.For(150);

        Assert.Equal(layout.FooterTop - 8, layout.ColumnBottom(0, layout.Top + 12, 1000));
    }

    [Fact]
    public void AStripWrapsWhereTheNextItemWouldOverrun()
    {
        Assert.Equal(new[] { 0, 0, 1, 1, 2 }, UiLayout.Wrap(new float[] { 40, 40, 60, 20, 90 }, 100, 10));
    }

    [Fact]
    public void AnItemWiderThanTheStripHasARowToItself()
    {
        Assert.Equal(new[] { 0, 1, 2 }, UiLayout.Wrap(new float[] { 30, 150, 30 }, 100, 10));
    }

    /// <summary>
    /// The key strip at a generous 7.5 pixels a character for the 12-point UI face (Inter averages
    /// under 7), a keycap's 14 of padding and the 7 to its words, 14 apart: it wraps into no more
    /// rows than the layout keeps for it.
    /// </summary>
    [Theory]
    [InlineData(100)]
    [InlineData(125)]
    [InlineData(150)]
    public void TheKeyStripWrapsIntoTheRowsTheLayoutKeepsForIt(int scale)
    {
        var layout = UiLayout.For(scale);
        var spans = KeyStrip.Keys.Select(k => 7.5f * k.Key.Length + 14 + 7 + 7.5f * k.Does.Length).ToList();

        var rows = UiLayout.Wrap(spans, layout.ViewWidth - 2 * UiLayout.Margin, 14);

        Assert.True(rows.Max() + 1 <= layout.KeyRows, $"{rows.Max() + 1} rows at {scale}");
    }

    [Fact]
    public void AScaleTheProfileRefusesHasNoLayout()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => UiLayout.For(110));
    }
}
