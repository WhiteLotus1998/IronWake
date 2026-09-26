using Ironwake.Content;

namespace Ironwake.Core.Tests.Maps;

/// <summary>
/// The <c>arsenal: on</c> header (DESIGN.md 13.11, experiment): it parses and writes back in
/// canonical order, is off by default, and refuses any value but <c>on</c>.
/// </summary>
public class ArsenalHeaderTests
{
    private const string Yard = """
        name: Yard
        size: 6x3
        win: rout
        turn_limit: 6
        recall: 3
        enemy_level: 1
        arsenal: on

        ......
        ......
        ......

        units:
        P captain 0,1
        E brigand 4,1 group:yard behavior:aggressive

        """;

    [Fact]
    public void TheArsenalHeaderParsesAndWritesBack()
    {
        var map = MapFixture.Parse(Yard);

        Assert.True(map.ArsenalShown);
        Assert.Equal(Yard.Replace("\r\n", "\n"), MapFormat.Write(map, MapFixture.Content));
        Assert.False(MapFixture.Parse(Yard.Replace("arsenal: on\n", "")).ArsenalShown);
    }

    [Fact]
    public void TheArsenalHeaderRefusesAnyValueButOn()
    {
        var error = Assert.Throws<MapException>(() => MapFixture.Parse(Yard.Replace("arsenal: on", "arsenal: yes")));

        Assert.Contains("arsenal may only be 'on'", error.Message);
    }
}
