using Ironwake.Content;

namespace Ironwake.Core.Tests.Maps;

/// <summary>
/// The <c>announce: on</c> header (issue 78): it parses and writes back in canonical order,
/// refuses any value but <c>on</c>, and refuses a map with no events to announce.
/// </summary>
public class AnnounceHeaderTests
{
    private const string Yard = """
        name: Yard
        size: 6x3
        win: rout
        turn_limit: 6
        recall: 3
        enemy_level: 1
        announce: on

        ......
        ......
        ......

        units:
        P captain 0,1
        E brigand 4,1 group:yard behavior:aggressive

        events:
        wave turn 2 enemy spawn brigand 5,0 group:east behavior:aggressive

        """;

    [Fact]
    public void TheAnnounceHeaderParsesAndWritesBack()
    {
        var map = MapFixture.Parse(Yard);

        Assert.True(map.Announced);
        Assert.Equal(Yard.Replace("\r\n", "\n"), MapFormat.Write(map, MapFixture.Content));
        Assert.False(MapFixture.Parse(Yard.Replace("announce: on\n", "")).Announced);
    }

    [Fact]
    public void TheAnnounceHeaderRefusesAnyValueButOn()
    {
        var error = Assert.Throws<MapException>(() => MapFixture.Parse(Yard.Replace("announce: on", "announce: yes")));

        Assert.Contains("announce may only be 'on'", error.Message);
    }

    [Fact]
    public void TheAnnounceHeaderRefusesAMapWithNoEvents()
    {
        var bare = Yard[..Yard.IndexOf("\nevents:", StringComparison.Ordinal)] + "\n";

        var error = Assert.Throws<MapException>(() => MapFixture.Parse(bare));

        Assert.Contains("announce: on needs an events: block", error.Message);
    }
}
