using System.Collections.Immutable;

namespace Ironwake.Core;

/// <summary>
/// Everything loaded from the content directory, keyed by id. Core receives this already
/// validated; it never reads files itself. Lookups by id throw a <see cref="KeyNotFoundException"/>
/// naming the id, since a missing id after validation is a programming error.
/// </summary>
public sealed record GameContent(
    ImmutableSortedDictionary<string, UnitClass> Classes,
    ImmutableSortedDictionary<string, Weapon> Weapons,
    ImmutableSortedDictionary<string, Terrain> Terrain,
    ImmutableSortedDictionary<string, Unit> Units)
{
    public UnitClass Class(string id) => Lookup(Classes, id, "class");

    public Weapon Weapon(string id) => Lookup(Weapons, id, "weapon");

    public Terrain TerrainById(string id) => Lookup(Terrain, id, "terrain");

    public Unit Unit(string id) => Lookup(Units, id, "unit");

    /// <summary>Finds the terrain drawn with a glyph, or null if no terrain uses it.</summary>
    public Terrain? TerrainByGlyph(char glyph)
    {
        foreach (var terrain in Terrain.Values)
        {
            if (terrain.Glyph == glyph)
            {
                return terrain;
            }
        }

        return null;
    }

    private static T Lookup<T>(ImmutableSortedDictionary<string, T> table, string id, string kind) =>
        table.TryGetValue(id, out var value)
            ? value
            : throw new KeyNotFoundException($"no {kind} with id '{id}'");

    public bool Equals(GameContent? other) =>
        other is not null
        && DictEquals(Classes, other.Classes)
        && DictEquals(Weapons, other.Weapons)
        && DictEquals(Terrain, other.Terrain)
        && DictEquals(Units, other.Units);

    public override int GetHashCode() =>
        HashCode.Combine(Classes.Count, Weapons.Count, Terrain.Count, Units.Count);

    private static bool DictEquals<T>(ImmutableSortedDictionary<string, T> a, ImmutableSortedDictionary<string, T> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        foreach (var (key, value) in a)
        {
            if (!b.TryGetValue(key, out var otherValue) || !EqualityComparer<T>.Default.Equals(value, otherValue))
            {
                return false;
            }
        }

        return true;
    }
}
