namespace Ironwake.Core;

/// <summary>
/// The wind (DESIGN.md 13.28, experiment), behind a map's <c>wind:</c> header. The wake rule of
/// section 8 is a circle; the wind makes it a direction. A sleeping member downwind of a player
/// unit, or of a combat, hears it <see cref="Carry"/> tiles farther than the rule's radius, and one
/// upwind <see cref="Carry"/> tiles nearer; across the wind the radius is the rule's. Downwind
/// means the member lies inside the quarter the wind blows toward: its offset along the wind is
/// larger than its offset across it. The wind can turn on announced turns, and the board prints
/// the way it blows and the turns it will turn. Nothing else reads it: strikes, sight at dusk and
/// the enemy's own hearing stay as they are. <see cref="WakeCheck"/> is its one reader, so the
/// rules, <c>threat from</c> and the captain veto agree.
/// </summary>
public static class Wind
{
    /// <summary>The tiles the wind adds downwind and takes upwind (DESIGN.md 13.28, provisional).</summary>
    public const int Carry = 2;

    /// <summary>The header word for <paramref name="way"/>.</summary>
    public static string Word(WindDirection way) => way.ToString().ToLowerInvariant();

    /// <summary>The direction a header word names, or null.</summary>
    public static WindDirection? Parse(string word) => word switch
    {
        "north" => WindDirection.North,
        "east" => WindDirection.East,
        "south" => WindDirection.South,
        "west" => WindDirection.West,
        _ => null,
    };

    /// <summary>
    /// The change to a wake radius from <paramref name="source"/> (a player unit or a combat) to a
    /// sleeping member at <paramref name="member"/> while the wind blows <paramref name="way"/>:
    /// plus <see cref="Carry"/> downwind, minus <see cref="Carry"/> upwind, 0 across or on the same tile.
    /// </summary>
    public static int Shift(WindDirection way, Coord source, Coord member)
    {
        var (dx, dy) = (member.X - source.X, member.Y - source.Y);
        var (along, across) = way switch
        {
            WindDirection.East => (dx, dy),
            WindDirection.West => (-dx, dy),
            WindDirection.South => (dy, dx),
            _ => (-dy, dx),
        };

        return along > Math.Abs(across) ? Carry
            : -along > Math.Abs(across) ? -Carry
            : 0;
    }

    /// <summary>The radius <paramref name="radius"/> becomes from <paramref name="source"/> to <paramref name="member"/> on <paramref name="state"/>'s turn; unchanged on a map without wind.</summary>
    public static int Radius(BattleState state, int radius, Coord source, Coord member) =>
        state.Map.Wind is { } wind ? radius + Shift(wind.On(state.Turn), source, member) : radius;

    /// <summary>
    /// The line a <c>wind:</c> map prints under its wake legend on <paramref name="turn"/>: the way it
    /// blows, the radii it makes, and every turn still to come on which it turns. Null without wind.
    /// </summary>
    public static string? Line(MapDefinition map, GameContent content, int turn)
    {
        if (map.Wind is not { } wind)
        {
            return null;
        }

        var way = wind.On(turn);
        var near = Math.Max(0, content.WakeRadius - Carry);
        var far = content.WakeRadius + Carry;
        var line = $"wind: blowing {Word(way)}; a sleeper downwind of a unit hears it within {far} (a fight within {content.NoiseRadius + Carry}), upwind within {near} (a fight within {content.NoiseRadius - Carry})";
        var coming = wind.Shifts.Where(s => s.Turn > turn).Select(s => $"turns {Word(s.To)} on turn {s.Turn}").ToList();
        return coming.Count == 0 ? line : line + "; " + string.Join(", ", coming);
    }
}
