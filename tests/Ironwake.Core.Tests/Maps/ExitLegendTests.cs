using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Battle;

namespace Ironwake.Core.Tests.Maps;

/// <summary>
/// Issue 267 (and 268): on an Escape map both board views draw every exit tile no unit
/// stands on as the exit glyph and print one line naming the exits and the rule, built from
/// the map's exit list and never a literal; a map with no exits prints neither.
/// </summary>
public class ExitLegendTests
{
    private static readonly string EscapeText = MapFixture.OldMillRoad
        .Replace("win: rout", "win: escape")
        .Replace("enemy_level: 1\n", "enemy_level: 1\nexit: 0,9 1,9 1,8\n");

    private const string Line = "exits (>): 0,9 1,9 1,8 (a unit on one may exit as its action; the captain's exit wins and leaves the rest behind)";

    private static string[] Rows(string view) => view.Split('\n');

    [Fact]
    public void TheExitLegendListsTheMapsExitsInItsOwnOrderWithTheRule()
    {
        Assert.Equal(Line, MapRenderer.ExitLegend(MapFixture.Parse(EscapeText)));
    }

    [Fact]
    public void TheExitLegendFollowsTheExitHeader()
    {
        var map = MapFixture.Parse(EscapeText.Replace("exit: 0,9 1,9 1,8", "exit: 11,9 10,9"));

        Assert.Equal("exits (>): 11,9 10,9 (a unit on one may exit as its action; the captain's exit wins and leaves the rest behind)", MapRenderer.ExitLegend(map));
    }

    [Fact]
    public void AMapWithNoExitsHasNoExitLegendOrGlyph()
    {
        var map = MapFixture.Parse(MapFixture.OldMillRoad);

        Assert.Null(MapRenderer.ExitLegend(map));
        Assert.DoesNotContain("exits", MapRenderer.Render(map, MapFixture.Content));
        Assert.DoesNotContain(MapRenderer.ExitGlyph, MapRenderer.Render(BattleFixture.Start(map: MapFixture.OldMillRoad), MapFixture.Content));
    }

    [Fact]
    public void TheMapViewDrawsAnEmptyExitAsTheExitGlyphAndAPlacementOverIt()
    {
        var rows = Rows(MapRenderer.Render(MapFixture.Parse(EscapeText), MapFixture.Content));

        // The header and the column ruler come first, so row y is line 2 + y; the captain's slot covers 1,8.
        Assert.Equal(" 9 >>..........", rows[2 + 9]);
        Assert.Equal(" 8 .AB.........", rows[2 + 8]);
        Assert.Contains(Line, rows);
    }

    [Fact]
    public void TheBattleViewDrawsEmptyExitsAndAUnitStandingOnOneHidesTheGlyph()
    {
        var state = BattleFixture.Start(map: EscapeText);
        var captain = state.UnitsOf(Side.Player).Single(u => u.IsCaptain);
        var moved = state.WithUnit(captain with { At = new Coord(0, 9) });

        var before = Rows(MapRenderer.Render(state, MapFixture.Content));
        var after = Rows(MapRenderer.Render(moved, MapFixture.Content));

        Assert.Equal(" 9 >>..........", before[2 + 9]);
        Assert.Equal(" 8 .AB.........", before[2 + 8]);
        Assert.Equal(" 9 A>..........", after[2 + 9]);
        Assert.Equal(" 8 .>B.........", after[2 + 8]);
        Assert.Contains(Line, before);
        Assert.Contains(Line, after);
    }

    [Fact]
    public void AReachGlyphCoversAnExitTheUnitCanEndOn()
    {
        var state = BattleFixture.Start(map: EscapeText);
        var wren = state.Find("wren")!;

        var rows = Rows(MapRenderer.Render(state, MapFixture.Content, state.ReachOf(wren, MapFixture.Content)));

        Assert.Equal(" 9 ******......", rows[2 + 9]);
        Assert.Contains(Line, rows);
    }

    [Fact]
    public void TheExitGlyphIsNoTerrainGlyph()
    {
        Assert.Null(MapFixture.Content.TerrainByGlyph(MapRenderer.ExitGlyph));
    }

    [Fact]
    public void BrackwaterCutsOpeningBoardShowsItsSixExits()
    {
        var map = MapFixture.Parse(File.ReadAllText(Path.Combine(MapFixture.MapsDirectory, "brackwater_cut.map")), "brackwater_cut.map");
        var view = MapRenderer.Render(map, MapFixture.Content);

        Assert.Contains("exits (>): 19,3 19,4 19,5 19,6 19,7 19,8 (a unit on one may exit as its action; the captain's exit wins and leaves the rest behind)\n", view);
        Assert.Equal(6, Rows(view).Count(r => r.EndsWith(MapRenderer.ExitGlyph)));
    }

    [Fact]
    public void TheFullProtocolStateCarriesTheExitsInItsMapText()
    {
        var state = BattleFixture.Start(map: EscapeText);

        Assert.Contains("exit: 0,9 1,9 1,8", ProtocolJson.State(state, MapFixture.Content));
    }
}
