using System.Text;

namespace Ironwake.Core;

/// <summary>
/// The console view of a map: the grid with units drawn over the terrain, then a legend
/// naming every unit letter and every terrain glyph on the map, a healing glyph with its
/// percent. Player units are uppercase letters in placement order, enemies lowercase,
/// bosses <c>!</c>. The view
/// is for reading, not for parsing; the map file itself is written by the Content
/// project's map writer. Output is plain ASCII. Given a <see cref="Reach"/>, the tiles
/// the unit may end on are drawn as <see cref="ReachGlyph"/> and a line under the
/// legend says whose reach it is. While any Guard group is asleep, both views print
/// <see cref="WakeLegend"/> under the unit rows. On an Escape map both views draw every
/// exit tile no unit stands on as <see cref="ExitGlyph"/> and print <see cref="ExitLegend"/>
/// under the unit rows (issue 267), so the objective is on the screen and not only in the
/// map file's header; a reach glyph covers an exit the unit can end on.
/// </summary>
public static class MapRenderer
{
    public const char BossGlyph = '!';

    public const char ReachGlyph = '*';

    public const char ExitGlyph = '>';

    public static string Render(MapDefinition map, GameContent content, Reach? reach = null)
    {
        var sb = new StringBuilder();
        sb.Append(map.Name).Append("  ").Append(map.Width).Append('x').Append(map.Height)
            .Append("  ").Append(WinName(map.Win))
            .Append("  turn limit ").Append(map.TurnLimit)
            .Append("  recall ").Append(map.RecallCharges)
            .Append("  enemy level ").Append(map.EnemyLevel);
        if (map.CheapShotsAllowed)
        {
            sb.Append("  cheap shots allowed");
        }

        sb.Append('\n');

        var letters = Letters(map, content);
        sb.Append("   ");
        for (var x = 0; x < map.Width; x++)
        {
            sb.Append((char)('0' + x % 10));
        }

        sb.Append('\n');
        for (var y = 0; y < map.Height; y++)
        {
            sb.Append(y.ToString().PadLeft(2)).Append(' ');
            var row = map.GlyphRow(y, content).ToCharArray();
            DrawExits(map, y, row);
            if (reach is not null)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    if (reach.CanEnd(new Coord(x, y)))
                    {
                        row[x] = ReachGlyph;
                    }
                }
            }

            for (var i = 0; i < map.Placements.Count; i++)
            {
                if (map.Placements[i].At.Y == y)
                {
                    row[map.Placements[i].At.X] = letters[i];
                }
            }

            sb.Append(row).Append('\n');
        }

        sb.Append('\n');
        for (var i = 0; i < map.Placements.Count; i++)
        {
            sb.Append(letters[i]).Append("  ").Append(Describe(map.Placements[i], map, content)).Append('\n');
        }

        if (map.Placements.OfType<EnemyPlacement>().Any(e => e.Behavior == Behavior.Guard))
        {
            sb.Append(WakeLegend(content)).Append('\n');
        }

        if (ExitLegend(map) is { } exits)
        {
            sb.Append(exits).Append('\n');
        }

        sb.Append('\n').Append("terrain:");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in map.TerrainIds)
        {
            if (seen.Add(id))
            {
                var terrain = content.TerrainById(id);
                sb.Append("  ").Append(terrain.Glyph).Append(' ').Append(terrain.Label());
            }
        }

        sb.Append('\n');
        if (reach is not null)
        {
            var count = reach.Destinations.Count() - 1;
            sb.Append(ReachGlyph).Append("  reach from ").Append(reach.Origin)
                .Append(", ").Append(reach.Movement.ToString().ToLowerInvariant())
                .Append(" mov ").Append(reach.Mov)
                .Append(": ").Append(count).Append(count == 1 ? " tile" : " tiles").Append('\n');
        }

        return sb.ToString();
    }

    /// <summary>
    /// The console view of a battle: the grid with every living unit drawn where it stands,
    /// each with the letter of the placement it filled, then a legend with each unit's
    /// name, class, position, HP, terrain (with the HP a healing tile gives that unit, issue
    /// 207), and for enemies its group and how it behaves now (a sleeping Guard reads <c>guard, asleep</c>, a refugee below half HP adds <c>holds its refuge until half hp</c>, issue 215), and <c>unarmed</c> for a unit with
    /// no usable weapon, since the enemy planner prices such a unit as free damage (issue
    /// 101) and seeing it coming is the player's whole defence. The turn line names the phase and
    /// the Recall charges left; past the turn limit, where only a battle the clock decided stands, it
    /// says the battle is over after the last turn (issue 252) rather than naming a turn the map never had. Given a <see cref="Reach"/>, the tiles that unit may end on
    /// are marked as in the map view.
    /// </summary>
    public static string Render(BattleState state, GameContent content, Reach? reach = null)
    {
        var map = state.Map;
        var sb = new StringBuilder();
        sb.Append(map.Name);
        if (state.Turn > map.TurnLimit)
        {
            sb.Append("  over after turn ").Append(map.TurnLimit).Append(" of ").Append(map.TurnLimit);
        }
        else
        {
            sb.Append("  turn ").Append(state.Turn).Append(" of ").Append(map.TurnLimit)
                .Append("  ").Append(state.Phase.ToString().ToLowerInvariant()).Append(" phase");
        }

        sb.Append("  ").Append(WinName(map.Win))
            .Append("  recall ").Append(state.RecallCharges).Append('\n');
        var letters = Letters(map, content);
        sb.Append("   ");
        for (var x = 0; x < map.Width; x++)
        {
            sb.Append((char)('0' + x % 10));
        }

        sb.Append('\n');
        for (var y = 0; y < map.Height; y++)
        {
            sb.Append(y.ToString().PadLeft(2)).Append(' ');
            var row = map.GlyphRow(y, content).ToCharArray();
            DrawExits(map, y, row);
            if (reach is not null)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    if (reach.CanEnd(new Coord(x, y)))
                    {
                        row[x] = ReachGlyph;
                    }
                }
            }

            foreach (var unit in state.Units)
            {
                if (unit.At.Y == y)
                {
                    row[unit.At.X] = letters[unit.PlacementIndex];
                }
            }

            sb.Append(row).Append('\n');
        }

        sb.Append('\n');
        foreach (var unit in state.Units)
        {
            var terrain = map.TerrainAt(unit.At, content).Label(unit.MaxHp(content));
            var who = $"{unit.Unit.Name} L{unit.Unit.Level} {content.Class(unit.Unit.ClassId).Name.ToLowerInvariant()}";
            var hp = $"hp {unit.Hp}/{unit.MaxHp(content)}";
            sb.Append(letters[unit.PlacementIndex]).Append("  ").Append($"{unit.Id,-16} {who,-26} {unit.At,-6} {hp,-9} {terrain}");
            if (unit.Side == Side.Enemy)
            {
                var role = unit.IsBoss ? "boss" : unit.Behavior.ToString()!.ToLowerInvariant();
                if (unit.Behavior == Behavior.Guard)
                {
                    role += state.IsAwake(unit.Group!) ? ", awake" : ", asleep";
                }

                if (RetreatRule.Holds(unit, content))
                {
                    role += ", holds its refuge until half hp";
                }

                sb.Append("  group ").Append(unit.Group).Append(", ").Append(role);
            }
            else if (unit.IsCaptain)
            {
                sb.Append("  captain");
            }

            if (unit.EquippedWeapon(content) is null)
            {
                sb.Append("  unarmed");
            }

            var carried = unit.Side == Side.Enemy
                ? unit.Unit.Inventory.Items.Where(stack => stack.Keepsake is not null).Select(stack => Keepsake.Name(stack.ItemId, stack.Keepsake!, content)).ToList()
                : new List<string>();
            if (carried.Count > 0)
            {
                sb.Append("  carries ").Append(string.Join(", ", carried));
            }

            if (unit.Acted)
            {
                sb.Append(unit.Canto is { } canto ? $"  canto {canto}" : "  done");
            }

            sb.Append('\n');
        }

        if (state.Units.Any(u => u is { Behavior: Behavior.Guard, Group: { } group } && !state.IsAwake(group)))
        {
            sb.Append(WakeLegend(content)).Append('\n');
        }

        if (ExitLegend(map) is { } exits)
        {
            sb.Append(exits).Append('\n');
        }

        if (state.Keepsakes.Count > 0)
        {
            sb.Append("keepsakes: ").Append(string.Join(", ", state.Keepsakes.Select(k => $"{Keepsake.Name(k.Item.ItemId, k.FallenId, content)} at {k.At}"))).Append('\n');
        }

        if (state.Escaped.Count > 0)
        {
            sb.Append("escaped: ").Append(string.Join(' ', state.Escaped.Select(u => u.Id))).Append('\n');
        }

        if (reach is not null)
        {
            var count = reach.Destinations.Count() - 1;
            sb.Append(ReachGlyph).Append("  reach from ").Append(reach.Origin)
                .Append(": ").Append(count).Append(count == 1 ? " tile" : " tiles").Append('\n');
        }

        return sb.ToString();
    }

    /// <summary>
    /// The one line both board views print under the unit rows while any Guard group is
    /// asleep (issue 260), so the wake rule of DESIGN.md section 8 is on the screen and not
    /// only in the doc. The numbers are the content's <see cref="GameContent.WakeRadius"/>
    /// and <see cref="GameContent.NoiseRadius"/>, the ones <see cref="WakeCheck"/> reads.
    /// </summary>
    public static string WakeLegend(GameContent content) => $"asleep: {WakeCondition(content)}";

    /// <summary>The wake rule alone, as the legend and <c>threat</c>'s sleeping-group rows print it (issue 248).</summary>
    public static string WakeCondition(GameContent content) =>
        $"wakes if a unit ends within {content.WakeRadius} tiles of a member, a combat happens within {content.NoiseRadius}, or a member dies";

    /// <summary>
    /// What an Escape map asks, in the words of DESIGN.md section 7's outcome rule (issue 269),
    /// printed after the exit tiles in <see cref="ExitLegend"/>.
    /// </summary>
    public const string EscapeRule = "a unit on one may exit as its action; the captain's exit wins and leaves the rest behind";

    /// <summary>
    /// The one line both board views print under the unit rows on an Escape map (issue 267):
    /// the exit glyph in parentheses (never at the line's start, where the console echoes a
    /// command as <c>&gt; </c>), then every exit tile from <see cref="MapDefinition.Exits"/> in the
    /// map's own order, then <see cref="EscapeRule"/>. Null on a map with no exits.
    /// </summary>
    public static string? ExitLegend(MapDefinition map) =>
        map.Exits.Count == 0
            ? null
            : $"exits ({ExitGlyph}): {string.Join(' ', map.Exits)} ({EscapeRule})";

    private static void DrawExits(MapDefinition map, int y, char[] row)
    {
        foreach (var exit in map.Exits)
        {
            if (exit.Y == y)
            {
                row[exit.X] = ExitGlyph;
            }
        }
    }

    /// <summary>
    /// The letter each placement is drawn with, by placement index. Players take
    /// A..Z, enemies a..z, and a boss is always <see cref="BossGlyph"/>. A letter that
    /// any terrain in the content draws with is skipped on both sides, so a unit is
    /// never drawn as a tile (issue 41); the skip uses the whole content, not the
    /// terrain on this map, so a slot keeps its letter from map to map. Past the usable
    /// count on a side the letters wrap; no v1 map has that many units. Spawned units
    /// (issue 32) follow the placements in spawn order, at <see cref="MapDefinition.SpawnIndex"/>.
    /// </summary>
    public static char[] Letters(MapDefinition map, GameContent content)
    {
        var upper = Alphabet('A', content);
        var lower = Alphabet('a', content);
        var placements = map.Placements.Concat(map.Spawns()).ToList();
        var letters = new char[placements.Count];
        var players = 0;
        var enemies = 0;
        for (var i = 0; i < placements.Count; i++)
        {
            letters[i] = placements[i] switch
            {
                EnemyPlacement { IsBoss: true } => BossGlyph,
                EnemyPlacement => lower[enemies++ % lower.Length],
                _ => upper[players++ % upper.Length],
            };
        }

        return letters;
    }

    /// <summary>
    /// The 26 letters from <paramref name="first"/> minus every one a terrain draws with.
    /// Content whose glyphs use every letter of a case leaves nothing to draw units with
    /// and is refused, since a view with no letters would be a grid of terrain only.
    /// </summary>
    private static char[] Alphabet(char first, GameContent content)
    {
        var alphabet = new List<char>(26);
        for (var offset = 0; offset < 26; offset++)
        {
            var letter = (char)(first + offset);
            if (content.TerrainByGlyph(letter) is null)
            {
                alphabet.Add(letter);
            }
        }

        return alphabet.Count > 0
            ? alphabet.ToArray()
            : throw new InvalidOperationException($"terrain glyphs use every letter {first}..{(char)(first + 25)}; nothing is left to draw units with");
    }

    private static string Describe(Placement placement, MapDefinition map, GameContent content)
    {
        var terrain = map.TerrainAt(placement.At, content).Label();
        switch (placement)
        {
            case PlayerPlacement p:
                var slot = p.Slot switch
                {
                    PlayerSlot.Captain => "captain",
                    PlayerSlot.NamedRecruit => "recruit " + p.RecruitId,
                    _ => "recruit (any)",
                };
                return $"{slot,-22} {p.At,-6} {terrain}";
            case EnemyPlacement e:
                var unit = map.EnemyUnit(e, content);
                var who = $"{unit.Name} L{unit.Level}";
                var role = e.IsBoss ? (e.Behavior == Behavior.Guard ? "boss, guard" : "boss") : e.Behavior.ToString().ToLowerInvariant();
                return $"{who,-22} {e.At,-6} {terrain}  group {e.Group}, {role}";
            default:
                throw new ArgumentOutOfRangeException(nameof(placement), placement, "unknown placement kind");
        }
    }

    /// <summary>The win condition as the map file spells it.</summary>
    public static string WinName(WinCondition win) => win switch
    {
        WinCondition.Rout => "rout",
        WinCondition.Seize => "seize",
        WinCondition.DefeatBoss => "defeat_boss",
        WinCondition.Survive => "survive",
        WinCondition.Escape => "escape",
        _ => throw new ArgumentOutOfRangeException(nameof(win), win, "unknown win condition"),
    };
}
