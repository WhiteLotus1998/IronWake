using Ironwake.Content;
using static Ironwake.Core.Tests.Battle.BattleFixture;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The wind (DESIGN.md 13.28, experiment): on a <c>wind:</c> map a sleeping member downwind of a
/// player unit or a fight hears it <see cref="Wind.Carry"/> tiles farther than the wake rule's
/// radius, one upwind that much nearer, one across the wind at the rule's radius; the wind turns
/// on its announced turns, the board prints it, and a bad header is refused naming the field.
/// </summary>
public class WindTests
{
    private static string Field(string header) =>
        $"""
        name: Field
        size: 16x7
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1
        {header}

        ................
        ................
        ................
        ................
        ................
        ................
        ................

        units:
        P captain 0,0
        P recruit:wren 0,6
        E soldier 9,3 group:watch behavior:guard

        """.Replace("\n\n\n", "\n\n");

    private static readonly Coord Sleeper = new(9, 3);

    private static BattleState Start(string header) => BattleFixture.Start(map: Field(header));

    /// <summary>Whether Hale standing on <paramref name="at"/> wakes the watch on <paramref name="state"/>.</summary>
    private static bool Wakes(BattleState state, Coord at)
    {
        var after = state.WithUnit(state.Find("hale")! with { At = at });
        return WakeCheck.Run(state, after, Starter, Array.Empty<Noise>(), Array.Empty<string>()).Any(w => w.Group == "watch");
    }

    [Theory]
    [InlineData(WindDirection.East, 9, 3, 2)]
    [InlineData(WindDirection.East, -9, 3, -2)]
    [InlineData(WindDirection.East, 3, 3, 0)]
    [InlineData(WindDirection.East, 0, 0, 0)]
    [InlineData(WindDirection.West, -4, 1, 2)]
    [InlineData(WindDirection.South, 1, 4, 2)]
    [InlineData(WindDirection.South, 1, -4, -2)]
    [InlineData(WindDirection.North, 0, -3, 2)]
    [InlineData(WindDirection.North, 5, 1, 0)]
    public void DownwindAddsTheCarryUpwindTakesItAcrossLeavesTheRule(WindDirection way, int dx, int dy, int shift)
    {
        Assert.Equal(2, Wind.Carry);
        Assert.Equal(shift, Wind.Shift(way, new Coord(5, 5), new Coord(5 + dx, 5 + dy)));
    }

    [Fact]
    public void DownwindAUnitWakesTheGroupFromSixTiles()
    {
        Assert.False(Wakes(Start(""), new Coord(3, 3)));
        Assert.True(Wakes(Start("wind: east"), new Coord(3, 3)));
        Assert.False(Wakes(Start("wind: east"), new Coord(2, 3)));
    }

    [Fact]
    public void UpwindAUnitStandsThreeTilesOffAndTheGroupSleeps()
    {
        Assert.True(Wakes(Start(""), new Coord(6, 3)));
        Assert.False(Wakes(Start("wind: west"), new Coord(6, 3)));
        Assert.True(Wakes(Start("wind: west"), new Coord(7, 3)));
    }

    [Fact]
    public void AcrossTheWindTheRadiusIsTheRules()
    {
        Assert.True(Wakes(Start("wind: north"), new Coord(5, 3)));
        Assert.False(Wakes(Start("wind: north"), new Coord(4, 3)));
    }

    [Fact]
    public void AFightDownwindWakesTheGroupFromEightTiles()
    {
        var state = Start("wind: east");
        var noise = new[] { new Noise(new Coord(1, 3), Starter.NoiseRadius) };

        Assert.Single(WakeCheck.Run(state, state, Starter, noise, Array.Empty<string>()));
        Assert.Empty(WakeCheck.Run(state, state, Starter, new[] { new Noise(new Coord(0, 3), Starter.NoiseRadius) }, Array.Empty<string>()));
        var upwind = Start("wind: west");
        Assert.Empty(WakeCheck.Run(upwind, upwind, Starter, new[] { new Noise(new Coord(4, 3), Starter.NoiseRadius) }, Array.Empty<string>()));
    }

    [Fact]
    public void TheWindTurnsOnItsAnnouncedTurn()
    {
        var state = Start("wind: west; turn 3 east");

        Assert.Equal(WindDirection.West, state.Map.Wind!.On(2));
        Assert.Equal(WindDirection.East, state.Map.Wind.On(3));
        Assert.False(Wakes(state, new Coord(3, 3)));
        Assert.True(Wakes(state with { Turn = 3 }, new Coord(3, 3)));
    }

    [Fact]
    public void ThreatFromNamesWhatAStopWouldWakeWithTheWind()
    {
        var state = Start("wind: east");
        var hale = state.WithUnit(state.Find("hale")! with { At = new Coord(1, 3) });

        Assert.Contains(Queries.StopWakes(hale, Starter, hale.Find("hale")!, new Coord(3, 3))!, w => w.Group == "watch");
    }

    [Fact]
    public void TheBoardPrintsTheWindAndTheTurnsStillToCome()
    {
        var state = Start("wind: west; turn 3 east");
        var line = Wind.Line(state.Map, Starter, 1)!;

        Assert.Equal("wind: blowing west; a sleeper downwind of a unit hears it within 6 (a fight within 8), upwind within 2 (a fight within 4); turns east on turn 3", line);
        Assert.Contains(line, MapRenderer.Render(state, Starter));
        Assert.DoesNotContain("turns", Wind.Line(state.Map, Starter, 3)!);
        Assert.Null(Wind.Line(Start("").Map, Starter, 1));
    }

    [Fact]
    public void TheHeaderRoundTrips()
    {
        var map = MapFixture.Parse(Field("wind: west; turn 3 east; turn 6 north"));

        Assert.Contains("wind: west; turn 3 east; turn 6 north\n", MapFormat.Write(map, Starter));
        Assert.Equal(map, MapFixture.Parse(MapFormat.Write(map, Starter)));
    }

    [Theory]
    [InlineData("wind: up", "needs the way it blows first")]
    [InlineData("wind: east; 4 north", "is not a turn and a way")]
    [InlineData("wind: east; turn 4 east", "already blows east")]
    [InlineData("wind: east; turn 1 west", "from 2 to the turn limit 10")]
    [InlineData("wind: east; turn 11 west", "from 2 to the turn limit 10")]
    [InlineData("wind: east; turn 5 west; turn 4 north", "must be after the last")]
    public void ABadHeaderIsRefusedNamingTheField(string header, string message)
    {
        var error = Assert.Throws<MapException>(() => MapFixture.Parse(Field(header)));

        Assert.Contains("wind", error.Message);
        Assert.Contains(message, error.Message);
    }
}
