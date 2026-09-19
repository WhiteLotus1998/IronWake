using System.Text;

namespace Ironwake.Core;

/// <summary>
/// The console view of a map: the grid with units drawn over the terrain, then a legend
/// naming every unit letter and every terrain glyph on the map. Player units are
/// uppercase letters in placement order, enemies lowercase, bosses <c>!</c>. The view
/// is for reading, not for parsing; the map file itself is written by the Content
/// project's map writer. Output is plain ASCII. Given a <see cref="Reach"/>, the tiles
/// the unit may end on are drawn as <see cref="ReachGlyph"/> and a line under the
/// legend says whose reach it is.
/// </summary>
public static class MapRenderer
{
    public const char BossGlyph = '!';

    public const char ReachGlyph = '*';

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

        sb.Append('\n').Append("terrain:");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in map.TerrainIds)
        {
            if (seen.Add(id))
            {
                var terrain = content.TerrainById(id);
                sb.Append("  ").Append(terrain.Glyph).Append(' ').Append(terrain.Name);
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
    /// name, class, position, HP, terrain, and for enemies its group and how it behaves
    /// now (a sleeping Guard reads <c>guard, asleep</c>), and <c>unarmed</c> for a unit with
    /// no usable weapon, since the enemy planner prices such a unit as free damage (issue
    /// 101) and seeing it coming is the player's whole defence. The turn line names the phase and
    /// the Recall charges left. Given a <see cref="Reach"/>, the tiles that unit may end on
    /// are marked as in the map view.
    /// </summary>
    public static string Render(BattleState state, GameContent content, Reach? reach = null)
    {
        var map = state.Map;
        var sb = new StringBuilder();
        sb.Append(map.Name).Append("  turn ").Append(state.Turn).Append(" of ").Append(map.TurnLimit)
            .Append("  ").Append(state.Phase.ToString().ToLowerInvariant()).Append(" phase")
            .Append("  ").Append(WinName(map.Win))
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
            var terrain = map.TerrainAt(unit.At, content).Name;
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

            if (unit.Acted)
            {
                sb.Append("  done");
            }

            sb.Append('\n');
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
    /// The letter each placement is drawn with, by placement index. Players take
    /// A..Z, enemies a..z, and a boss is always <see cref="BossGlyph"/>. A letter that
    /// any terrain in the content draws with is skipped on both sides, so a unit is
    /// never drawn as a tile (issue 41); the skip uses the whole content, not the
    /// terrain on this map, so a slot keeps its letter from map to map. Past the usable
    /// count on a side the letters wrap; no v1 map has that many units.
    /// </summary>
    public static char[] Letters(MapDefinition map, GameContent content)
    {
        var upper = Alphabet('A', content);
        var lower = Alphabet('a', content);
        var letters = new char[map.Placements.Count];
        var players = 0;
        var enemies = 0;
        for (var i = 0; i < map.Placements.Count; i++)
        {
            letters[i] = map.Placements[i] switch
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
        var terrain = map.TerrainAt(placement.At, content).Name;
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
                var role = e.IsBoss ? "boss" : e.Behavior.ToString().ToLowerInvariant();
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
