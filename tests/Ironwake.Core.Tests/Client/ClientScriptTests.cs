using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Client;

/// <summary>
/// The client's reading of a <c>play --script</c> file (issue 347): lines that change the
/// board become the commands the console would apply, with slots counted from 1, and
/// lines that only ask print no event and are skipped.
/// </summary>
public class ClientScriptTests
{
    private static BattleState Tollgate()
    {
        var content = ContentLoader.Load(Fixture.RealContentDirectory());
        var map = MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "maps", "the_tollgate.map"), content);
        return BattleState.From(map, content, content.Cast, 1);
    }

    [Theory]
    [InlineData("forecast wren brigand-1")]
    [InlineData("threat wren")]
    [InlineData("show wren")]
    [InlineData("reach wren")]
    [InlineData("map")]
    [InlineData("recall")]
    [InlineData("recall list")]
    [InlineData("# a comment")]
    [InlineData("")]
    public void LinesThatOnlyAskAreSkipped(string line)
    {
        Assert.Null(Script.Parse(line, Tollgate()));
    }

    [Fact]
    public void SlotsCountFromOneAsTheConsoleReadsThem()
    {
        var state = Tollgate();

        Assert.Equal(new Attack("wren", "brigand-1", 1, null), Script.Parse("attack wren brigand-1 2", state));
        Assert.Equal(new Attack("wren", "brigand-1", null, "smash"), Script.Parse("attack wren brigand-1 art smash", state));
        Assert.Equal(new UseItem("wren", 0, "pell"), Script.Parse("item wren 1 pell", state));
    }

    [Fact]
    public void CantoStayIsACantoToTheUnitsOwnTile()
    {
        var state = Tollgate();
        var wren = state.Find("wren")!;

        Assert.Equal(new Canto("wren", wren.At), Script.Parse("canto wren stay", state));
    }

    [Fact]
    public void BoardCommandsParseToTheCoresRecords()
    {
        var state = Tollgate();

        Assert.Equal(new Move("wren", new Coord(3, 4)), Script.Parse("move wren 3,4", state));
        Assert.Equal(new Wait("wren"), Script.Parse("wait wren", state));
        Assert.Equal(new EndPhase(), Script.Parse("end", state));
        Assert.Equal(new Recall(4), Script.Parse("recall 4", state));
        Assert.Equal(new Shove("wren", "pell"), Script.Parse("shove wren pell", state));
    }
}
