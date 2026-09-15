using System.Text;

namespace Ironwake.Core;

/// <summary>
/// The console view of a map: the grid with units drawn over the terrain, then a legend
/// naming every unit letter and every terrain glyph on the map. Player units are
/// uppercase letters in placement order, enemies lowercase, bosses <c>!</c>. The view
/// is for reading, not for parsing; the map file itself is written by the Content
/// project's map writer. Output is plain ASCII.
/// </summary>
public static class MapRenderer
{
    public const char BossGlyph = '!';

    public static string Render(MapDefinition map, GameContent content)
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

        var letters = Letters(map);
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
        return sb.ToString();
    }

    /// <summary>
    /// The letter each placement is drawn with, by placement index. Players take
    /// A..Z, enemies a..z, and a boss is always <see cref="BossGlyph"/>. Past 26 on
    /// a side the letters wrap; no v1 map has that many units.
    /// </summary>
    public static char[] Letters(MapDefinition map)
    {
        var letters = new char[map.Placements.Count];
        var players = 0;
        var enemies = 0;
        for (var i = 0; i < map.Placements.Count; i++)
        {
            letters[i] = map.Placements[i] switch
            {
                EnemyPlacement { IsBoss: true } => BossGlyph,
                EnemyPlacement => (char)('a' + enemies++ % 26),
                _ => (char)('A' + players++ % 26),
            };
        }

        return letters;
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
                var unit = content.Unit(e.TemplateId);
                var level = Math.Max(unit.Level, map.EnemyLevel);
                var who = $"{unit.Name} L{level}";
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
