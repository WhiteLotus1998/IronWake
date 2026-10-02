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
    /// The objective line in the player's words (issue 609, Lotus's own line for the Tollgate):
    /// what to do and by when, then who must survive, as two short sentences. Where the seize
    /// tile is and the rule clauses that hang off the objective are <see cref="Rules"/>, printed
    /// by <c>help</c>, never in the headline.
    /// </summary>
    public static string Line(BattleState state, GameContent content)
    {
        var map = state.Map;
        var by = $"by the end of turn {map.TurnLimit}";
        var win = map.Win switch
        {
            WinCondition.Seize => $"Get the captain to the {SeizeName(content)} {by}.",
            WinCondition.Rout => $"Defeat every enemy {by}.",
            WinCondition.DefeatBoss => $"Defeat the boss {by}.",
            WinCondition.Survive => $"Hold out until the end of turn {map.TurnLimit}.",
            WinCondition.Escape => $"Get the captain out through an exit {by}.",
            _ => throw new ArgumentOutOfRangeException(nameof(state), map.Win, "unknown win condition"),
        };
        var captain = Rank(state, content);
        var survivors = map.ProtectId is { } protect ? $"{captain} and {UnitNames.Of(state, content)[protect]} must survive." : $"{captain} must survive.";
        return $"{win} {survivors}";
    }

    /// <summary>
    /// The clauses the objective line leaves out (issue 609), in sentence case, one per line:
    /// where the seize tile is and that only the captain seizes, how an exit works, the boss's
    /// mark. The console's <c>help</c> prints them under the commands; empty when the map's
    /// objective needs nothing more than its line.
    /// </summary>
    public static IReadOnlyList<string> Rules(BattleState state, GameContent content)
    {
        var map = state.Map;
        var captain = Captain(state, content);
        var rules = map.Win switch
        {
            WinCondition.Seize => new[] { $"The captain, {captain}, must stand on the {SeizeName(content)} at {Thrones(map)}. Only the captain seizes." },
            WinCondition.Escape => new[] { $"The captain, {captain}, must exit from an exit tile ({MapRenderer.ExitGlyph}).{(map.ExitAfterMove ? "" : " A unit that starts its turn on an exit may leave.")} Anyone still on the board is left behind." },
            WinCondition.DefeatBoss => new[] { $"The boss is drawn {MapRenderer.BossGlyph} on the board." },
            _ => Array.Empty<string>(),
        };
        return map.Fronts.Count == 0 ? rules : rules.Append(Fronts.Rule).ToArray();
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
                return CaptainUnit(state) is not { } fallen ? "Lost because the captain fell."
                    : Leads(fallen, content) ? $"Lost because {fallen.Unit.Name} fell." : $"Lost because the captain, {fallen.Unit.Name}, fell.";
            case LossCause.Protected:
                var protect = UnitNames.Of(state, content)[map.ProtectId!];
                return outcome.Reason.EndsWith("left behind", StringComparison.Ordinal)
                    ? $"Lost because {protect} was left behind. This map is lost if {protect} falls or is left behind."
                    : $"Lost because {protect} fell. This map is lost if {protect} falls or is left behind.";
        }

        var missed = map.Win switch
        {
            WinCondition.Seize => state.Units.FirstOrDefault(u => u.IsCaptain) is { } captain
                ? $"the captain ended at {captain.At}, not on the {SeizeName(content)} at {Thrones(map)}"
                : $"the captain never stood on the {SeizeName(content)} at {Thrones(map)}",
            WinCondition.Rout => state.UnitsOf(Side.Enemy).Count() is var left && left == 1 ? "1 enemy still stands" : $"{left} enemies still stand",
            WinCondition.DefeatBoss => "the boss still stands",
            WinCondition.Escape => "the captain never exited",
            _ => "the objective was not met",
        };
        return $"Lost because turn {map.TurnLimit} ended and {missed}.";
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
            lines.Add(UnitNames.Sentence($"{UnitNames.Of(after, content)[unit.Id]} stands on the {SeizeName(content)}, but only the captain, {Captain(after, content, letters: false)}, seizes."));
        }

        if (before.UnitsOf(Side.Enemy).Any() && !after.UnitsOf(Side.Enemy).Any())
        {
            lines.Add($"No enemy is left, but the map is not won: the captain, {Captain(after, content, letters: false)}, must still stand on the {SeizeName(content)} at {Thrones(map)} by the end of turn {map.TurnLimit} (now turn {after.Turn}).");
        }

        return lines;
    }

    /// <summary>
    /// The seize tile as the player reads it (issue 569): the throne terrain's display name,
    /// lowercased, so every line here, the protocol's <c>seizeName</c> and the client's legend,
    /// how-to-play and end card read one value. The rules still call the tile the throne;
    /// without a throne terrain in the content the name is its id.
    /// </summary>
    public static string SeizeName(GameContent content) =>
        content.Terrain.TryGetValue(MapDefinition.ThroneTerrainId, out var throne)
            ? throne.Name.ToLowerInvariant()
            : MapDefinition.ThroneTerrainId;

    /// <summary>The captain by rank and surname, as the objective line names him: "Captain Fenn" for Alder Fenn; "The captain" when no captain was ever on the board.</summary>
    private static string Rank(BattleState state, GameContent content) =>
        CaptainUnit(state) is not { } captain ? "The captain"
            : Leads(captain, content) ? captain.Unit.Name
            : "Captain " + captain.Unit.Name.Split(' ')[^1];

    /// <summary>
    /// Whether the unit in the captain slot is another member of the cast standing in it (issue
    /// 635): a side map's member or a trial's candidate, named plainly rather than given the
    /// captain's rank. A unit the cast does not list is the captain of its own board.
    /// </summary>
    private static bool Leads(BattleUnit captain, GameContent content) =>
        content.Cast.Count > 0 && captain.Id != content.Cast[0].Id && content.Cast.Any(u => u.Id == captain.Id);

    private static BattleUnit? CaptainUnit(BattleState state) =>
        state.Units.FirstOrDefault(u => u.IsCaptain)
            ?? state.Escaped.FirstOrDefault(u => u.IsCaptain)
            ?? state.History.SelectMany(h => h.Units).FirstOrDefault(u => u.IsCaptain);

    /// <summary>The captain as the rules and verdicts name it: the unit's name, then the letter the board draws it with when <paramref name="letters"/> is true.</summary>
    private static string Captain(BattleState state, GameContent content, bool letters = true)
    {
        var captain = CaptainUnit(state);
        if (captain is null)
        {
            return "the captain";
        }

        if (!letters)
        {
            return captain.Unit.Name;
        }

        var glyphs = MapRenderer.Letters(state.Map, content);
        return $"{captain.Unit.Name} ({glyphs[captain.PlacementIndex]})";
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
