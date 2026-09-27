namespace Ironwake.Core;

/// <summary>
/// The map's objective in player words (issue 374), one source for the console and the client:
/// what wins, what loses, why a lost battle was lost, and the notices a player needs mid-battle
/// on a Seize map (every enemy gone with the throne still open, a unit other than the captain on
/// the throne). A renderer prints these lines; nothing here decides the outcome, which stays
/// <see cref="BattleState.Outcome"/>.
/// </summary>
public static class Objective
{
    /// <summary>
    /// The objective line: which unit must do what by which turn, then what loses the battle,
    /// the captain named with the letter the board draws it with.
    /// </summary>
    public static string Line(BattleState state, GameContent content)
    {
        var map = state.Map;
        var captain = Captain(state, content);
        var by = $"by the end of turn {map.TurnLimit}";
        var win = map.Win switch
        {
            WinCondition.Seize => $"the captain, {captain}, must stand on the throne at {Thrones(map)} {by}; only the captain seizes",
            WinCondition.Rout => $"defeat every enemy {by}",
            WinCondition.DefeatBoss => $"defeat the boss ({MapRenderer.BossGlyph}) {by}",
            WinCondition.Survive => $"hold until the end of turn {map.TurnLimit}",
            WinCondition.Escape => $"the captain, {captain}, must exit from an exit tile ({MapRenderer.ExitGlyph}) {by}; anyone still on the board is left behind",
            _ => throw new ArgumentOutOfRangeException(nameof(state), map.Win, "unknown win condition"),
        };
        var lose = map.ProtectId is { } protect
            ? $"lost if the captain or {protect} falls"
            : "lost if the captain falls";
        return $"objective: {win}; {lose}";
    }

    /// <summary>
    /// Why a lost battle was lost, naming what was missed: the captain or the protected unit by
    /// name, or on a timeout what the objective still lacked. Null while ongoing or won.
    /// </summary>
    public static string? Verdict(BattleState state, GameContent content)
    {
        var outcome = state.Outcome;
        if (outcome.Result != BattleResult.Lost)
        {
            return null;
        }

        var map = state.Map;
        switch (outcome.Cause)
        {
            case LossCause.Captain:
                return $"lost because the captain, {Captain(state, content)}, fell";
            case LossCause.Protected:
                return $"lost because {outcome.Reason}; this map is lost if {map.ProtectId} falls or is left behind";
        }

        var missed = map.Win switch
        {
            WinCondition.Seize => state.Units.FirstOrDefault(u => u.IsCaptain) is { } captain
                ? $"the captain ended at {captain.At}, not on the throne at {Thrones(map)}"
                : $"the captain never stood on the throne at {Thrones(map)}",
            WinCondition.Rout => state.UnitsOf(Side.Enemy).Count() is var left && left == 1 ? "1 enemy still stands" : $"{left} enemies still stand",
            WinCondition.DefeatBoss => "the boss still stands",
            WinCondition.Escape => "the captain never exited",
            _ => "the objective was not met",
        };
        return $"lost because turn {map.TurnLimit} ended and {missed}";
    }

    /// <summary>
    /// The lines a command's result earns on a Seize map, printed after its events: that the last
    /// enemy is gone and the throne is still the objective, and that a unit other than the captain
    /// ending a move on the throne seizes nothing. Empty on every other map and once the battle is over.
    /// </summary>
    public static IReadOnlyList<string> Notices(BattleState before, BattleState after, GameContent content, Command command)
    {
        var map = after.Map;
        if (map.Win != WinCondition.Seize || after.Outcome.IsOver)
        {
            return Array.Empty<string>();
        }

        var lines = new List<string>();
        var mover = command switch
        {
            Move m => m.UnitId,
            Canto c => c.UnitId,
            _ => null,
        };
        if (mover is not null && after.Find(mover) is { Side: Side.Player, IsCaptain: false } unit && map.IsThrone(unit.At))
        {
            lines.Add($"{unit.Id} stands on the throne, but only the captain, {Captain(after, content)}, seizes");
        }

        if (before.UnitsOf(Side.Enemy).Any() && !after.UnitsOf(Side.Enemy).Any())
        {
            lines.Add($"no enemy is left, but the map is not won: the captain, {Captain(after, content)}, must still stand on the throne at {Thrones(map)} by the end of turn {map.TurnLimit} (now turn {after.Turn})");
        }

        return lines;
    }

    /// <summary>The captain as the objective names it: the unit's name and the letter the board draws it with.</summary>
    private static string Captain(BattleState state, GameContent content)
    {
        var captain = state.Units.FirstOrDefault(u => u.IsCaptain)
            ?? state.Escaped.FirstOrDefault(u => u.IsCaptain)
            ?? state.History.SelectMany(h => h.Units).FirstOrDefault(u => u.IsCaptain);
        if (captain is null)
        {
            return "the captain";
        }

        var letters = MapRenderer.Letters(state.Map, content);
        return $"{captain.Unit.Name} ({letters[captain.PlacementIndex]})";
    }

    private static string Thrones(MapDefinition map)
    {
        var tiles = new List<Coord>();
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                if (map.IsThrone(new Coord(x, y)))
                {
                    tiles.Add(new Coord(x, y));
                }
            }
        }

        var row = tiles.Count > 2 && tiles.All(t => t.Y == tiles[0].Y) && tiles[^1].X - tiles[0].X == tiles.Count - 1;
        return row ? $"any tile from {tiles[0]} to {tiles[^1]}" : string.Join(" or ", tiles);
    }
}
