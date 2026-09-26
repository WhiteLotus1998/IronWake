using System.Collections.Immutable;

namespace Ironwake.Core;

/// <summary>
/// Everything loaded from the content directory, keyed by id. Core receives this already
/// validated; it never reads files itself. Lookups by id throw a <see cref="KeyNotFoundException"/>
/// naming the id, since a missing id after validation is a programming error.
/// <see cref="WakeRadius"/> is the Guard wake radius of DESIGN.md section 8, in tiles,
/// Manhattan: one number for every map, read from <c>rules.json</c>. <see cref="Items"/>
/// are the consumables of <c>items.json</c>; an inventory entry names a weapon or an item.
/// </summary>
public sealed record GameContent(
    ImmutableSortedDictionary<string, UnitClass> Classes,
    ImmutableSortedDictionary<string, Weapon> Weapons,
    ImmutableSortedDictionary<string, Terrain> Terrain,
    ImmutableSortedDictionary<string, Unit> Units,
    ImmutableSortedDictionary<string, Item> Items,
    int WakeRadius)
{
    /// <summary>
    /// The player's roster in roster order, the captain first (DESIGN.md section 9; issue 13):
    /// the units of <c>units/cast.json</c> in file order. A bare <c>recruit</c> slot on a map
    /// takes the next undeployed recruit in this order, so the order decides who fights the
    /// early maps. Empty when the content has no cast file.
    /// </summary>
    public ValueList<Unit> Cast { get; init; } = ValueList<Unit>.Empty;

    /// <summary>The rivalry arms and rapport table of <c>rules.json</c> (issue 16); <see cref="RivalryRules.None"/> when it has no block.</summary>
    public RivalryRules Rivalry { get; init; } = RivalryRules.None;

    /// <summary>The abilities of <c>abilities.json</c> by id (issue 66); empty when the content names none.</summary>
    public ImmutableSortedDictionary<string, Ability> Abilities { get; init; } =
        ImmutableSortedDictionary<string, Ability>.Empty.WithComparers(StringComparer.Ordinal);

    /// <summary>
    /// The difficulties of <c>rules.json</c> by id (issue 76); when any are declared, <c>normal</c>
    /// is among them and is the identity. Empty when the content declares none.
    /// </summary>
    public ImmutableSortedDictionary<string, Difficulty> Difficulties { get; init; } =
        ImmutableSortedDictionary<string, Difficulty>.Empty.WithComparers(StringComparer.Ordinal);

    /// <summary>The campaign of <c>campaign.json</c> (issue 74): its purse, seal price and maps; <see cref="CampaignRules.None"/> when the content has none.</summary>
    public CampaignRules Campaign { get; init; } = CampaignRules.None;

    /// <summary>Noise wakes a group from two tiles further out than proximity does (section 8).</summary>
    public int NoiseRadius => WakeRadius + 2;

    public UnitClass Class(string id) => Lookup(Classes, id, "class");

    public Weapon Weapon(string id) => Lookup(Weapons, id, "weapon");

    public Terrain TerrainById(string id) => Lookup(Terrain, id, "terrain");

    public Unit Unit(string id) => Lookup(Units, id, "unit");

    public Item Item(string id) => Lookup(Items, id, "item");

    public Ability Ability(string id) => Lookup(Abilities, id, "ability");

    public Difficulty Difficulty(string id) => Lookup(Difficulties, id, "difficulty");

    /// <summary>
    /// The display name of an inventory entry: the consumable's name, else the weapon's, else the
    /// id itself when neither table knows it, so a console line never throws on a stale id.
    /// </summary>
    public string ItemName(string id) =>
        Items.TryGetValue(id, out var item) ? item.Name : Weapons.TryGetValue(id, out var weapon) ? weapon.Name : id;

    /// <summary>The unit's abilities, resolved in the order it lists them, then its class's (issue 71) that it does not already list.</summary>
    public ValueList<Ability> AbilitiesOf(Unit unit) =>
        ValueList<Ability>.From(unit.Abilities.Concat(Class(unit.ClassId).Abilities.Where(id => !unit.Abilities.Contains(id))).Select(Ability));

    /// <summary>The combat arts a unit knows (issue 68): the abilities it lists whose effect is an art, in its order.</summary>
    public IEnumerable<(Ability Ability, CombatArtEffect Art)> ArtsOf(Unit unit) =>
        AbilitiesOf(unit).Where(a => a.Effect is CombatArtEffect).Select(a => (a, (CombatArtEffect)a.Effect));

    /// <summary>
    /// The numbers a unit fights with: its stats, its class modifiers, and its passive
    /// ability deltas. Max HP is this <c>Hp</c>, the same number <see cref="Combatant.Stats"/> carries.
    /// </summary>
    public Stats StatsOf(Unit unit) => unit.EffectiveStats(Class(unit.ClassId)) + AbilityRules.Passive(AbilitiesOf(unit));

    /// <summary>This unit as the section 5 formulas see it, its abilities resolved from this content.</summary>
    public Combatant CombatantOf(Unit unit, Weapon? weapon, Terrain terrain, int hp, int critAvoidModifier = 0, bool broken = false, int hitModifier = 0, int critModifier = 0) =>
        new(unit, Class(unit.ClassId), weapon, terrain, hp, critAvoidModifier, broken, hitModifier, critModifier, AbilitiesOf(unit));

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
        && DictEquals(Units, other.Units)
        && DictEquals(Items, other.Items)
        && DictEquals(Abilities, other.Abilities)
        && DictEquals(Difficulties, other.Difficulties)
        && Cast == other.Cast
        && Rivalry == other.Rivalry
        && Campaign == other.Campaign
        && WakeRadius == other.WakeRadius;

    public override int GetHashCode() =>
        HashCode.Combine(Classes.Count, Weapons.Count, Terrain.Count, Units.Count, Items.Count, WakeRadius);

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
