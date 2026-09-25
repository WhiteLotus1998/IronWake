namespace Ironwake.Core.Tests.Combat;

/// <summary>
/// Synthetic units for the section 5 tests, with numbers chosen so every formula's
/// hand computation fits in a comment. Wren (cadet, iron sword) fights a brigand (reaver,
/// heavy axe) standing in a forest; a hexer, an archer, a rider, and a wingrider cover
/// magic, range, effective damage, and the flyer exception.
/// </summary>
internal static class CombatFixture
{
    private static readonly ValueList<int?> Ground = ValueList<int?>.Of(1, 1, 1, 1);

    public static readonly Terrain Plain = new("plain", "Plain", '.', Ground, 0, 0, 0, 0, false);
    public static readonly Terrain Forest = new("forest", "Forest", 'F', Ground, 20, 1, 0, 0, false);
    public static readonly Terrain Fort = new("fort", "Fort", 'n', Ground, 20, 2, 2, 20, true);

    public static readonly Weapon IronSword = new("iron_sword", "Iron Sword", WeaponType.Sword, 5, 90, 0, 5, 1, 1, 40, ValueList<MovementType>.Empty);
    public static readonly Weapon HeavyAxe = new("heavy_axe", "Heavy Axe", WeaponType.Axe, 7, 70, 0, 8, 1, 1, 30, ValueList<MovementType>.Empty);
    public static readonly Weapon Ridgeblade = new("ridgeblade", "Ridgeblade", WeaponType.Sword, 6, 85, 10, 6, 1, 1, 25, ValueList<MovementType>.Of(MovementType.Cavalry));
    public static readonly Weapon Spark = new("spark", "Spark", WeaponType.Reason, 4, 85, 0, 4, 1, 2, 20, ValueList<MovementType>.Empty);
    public static readonly Weapon ShortBow = new("short_bow", "Short Bow", WeaponType.Bow, 5, 85, 0, 5, 2, 2, 40, ValueList<MovementType>.Empty);

    public static readonly UnitClass Cadet = Class("cadet", MovementType.Infantry, Stats.Zero, WeaponType.Sword, WeaponType.Lance, WeaponType.Axe);
    public static readonly UnitClass Reaver = Class("reaver", MovementType.Infantry, new Stats(2, 1, 0, 0, 0, 0, 0, 0, 0), WeaponType.Axe);
    public static readonly UnitClass Adept = Class("adept", MovementType.Infantry, Stats.Zero, WeaponType.Reason);
    public static readonly UnitClass Bowman = Class("bowman", MovementType.Infantry, Stats.Zero, WeaponType.Bow);
    public static readonly UnitClass Outrider = Class("outrider", MovementType.Cavalry, Stats.Zero, WeaponType.Sword, WeaponType.Lance);
    public static readonly UnitClass Skyrider = Class("skyrider", MovementType.Flying, Stats.Zero, WeaponType.Lance, WeaponType.Sword);

    // Wren: Str 7, Dex 6, Spd 8, Lck 5, Def 4, Res 2. Burden max(0, 5 - 7) = 0, attack speed 8.
    public static readonly Unit Wren = Unit("wren", "cadet", new Stats(20, 7, 0, 6, 8, 5, 4, 2, 3));

    // Brigand in a reaver: effective HP 22, Str 7, Dex 3, Spd 4, Lck 1, Def 2. Burden 8 - 7 = 1, attack speed 3.
    public static readonly Unit Brigand = Unit("brigand", "reaver", new Stats(20, 6, 0, 3, 4, 1, 2, 0, 1));

    // Hexer: Mag 6, Dex 5, Spd 5, Lck 2, Res 5. Burden 4 - 1 = 3, attack speed 2.
    public static readonly Unit Hexer = Unit("hexer", "adept", new Stats(16, 1, 6, 5, 5, 2, 1, 5, 2));

    public static readonly Unit Archer = Unit("archer", "bowman", new Stats(17, 5, 0, 7, 6, 2, 2, 1, 2));
    public static readonly Unit Rider = Unit("rider", "outrider", new Stats(19, 6, 0, 4, 5, 2, 3, 1, 2));
    public static readonly Unit Wingrider = Unit("wingrider", "skyrider", new Stats(17, 5, 0, 5, 7, 3, 2, 4, 2));

    public static Combatant WrenOnPlain(int hp = 20) => new(Wren, Cadet, IronSword, Plain, hp);

    public static Combatant BrigandInForest(int hp = 22, int critAvoidModifier = 0) =>
        new(Brigand, Reaver, HeavyAxe, Forest, hp, critAvoidModifier);

    public static Combatant HexerOnPlain() => new(Hexer, Adept, Spark, Plain, 16);

    public static Combatant ArcherOnPlain() => new(Archer, Bowman, ShortBow, Plain, 17);

    public static Combatant RiderOnPlain() => new(Rider, Outrider, IronSword, Plain, 19);

    public static Combatant WingriderInForest() => new(Wingrider, Skyrider, IronSword, Forest, 17);

    public static Combatant WingriderOnFort() => new(Wingrider, Skyrider, IronSword, Fort, 17);

    private static UnitClass Class(string id, MovementType movement, Stats modifiers, params WeaponType[] weapons) =>
        new(id, id, movement, 4, modifiers, ValueList<WeaponType>.Of(weapons), Stats.Zero);

    private static Unit Unit(string id, string classId, Stats stats) =>
        new(id, id, classId, 1, 0, stats, Stats.Zero, Inventory.Empty, ValueList<string>.Empty);
}

/// <summary>An <see cref="IRng"/> that answers a fixed roll unless a key is scripted, and records every key asked for.</summary>
internal sealed class ScriptedRng : IRng
{
    private readonly Dictionary<string, int> _scripted = new();

    public ScriptedRng(int defaultRoll)
    {
        DefaultRoll = defaultRoll;
    }

    public int DefaultRoll { get; }

    public List<string> Asked { get; } = new();

    public ScriptedRng Set(RollKey key, int roll)
    {
        _scripted[key.Text] = roll;
        return this;
    }

    public int Roll(RollKey key)
    {
        Asked.Add(key.Text);
        return _scripted.TryGetValue(key.Text, out var roll) ? roll : DefaultRoll;
    }
}
