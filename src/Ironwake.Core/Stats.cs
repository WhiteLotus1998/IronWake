namespace Ironwake.Core;

/// <summary>The nine stats from DESIGN.md section 3, in the order growth rolls are drawn (issue 8).</summary>
public enum Stat
{
    Hp,
    Str,
    Mag,
    Dex,
    Spd,
    Lck,
    Def,
    Res,
    Cha,
}

/// <summary>
/// A full set of the nine stats. Used for a unit's base stats, class modifiers, and
/// growth rates (percent), so values may be negative when they are modifiers.
/// </summary>
public readonly record struct Stats(int Hp, int Str, int Mag, int Dex, int Spd, int Lck, int Def, int Res, int Cha)
{
    public static Stats Zero { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0);

    /// <summary>Every stat, in draw order.</summary>
    public static IReadOnlyList<Stat> All { get; } = Enum.GetValues<Stat>();

    public int Get(Stat stat) => stat switch
    {
        Stat.Hp => Hp,
        Stat.Str => Str,
        Stat.Mag => Mag,
        Stat.Dex => Dex,
        Stat.Spd => Spd,
        Stat.Lck => Lck,
        Stat.Def => Def,
        Stat.Res => Res,
        Stat.Cha => Cha,
        _ => throw new ArgumentOutOfRangeException(nameof(stat), stat, "unknown stat"),
    };

    public Stats With(Stat stat, int value) => stat switch
    {
        Stat.Hp => this with { Hp = value },
        Stat.Str => this with { Str = value },
        Stat.Mag => this with { Mag = value },
        Stat.Dex => this with { Dex = value },
        Stat.Spd => this with { Spd = value },
        Stat.Lck => this with { Lck = value },
        Stat.Def => this with { Def = value },
        Stat.Res => this with { Res = value },
        Stat.Cha => this with { Cha = value },
        _ => throw new ArgumentOutOfRangeException(nameof(stat), stat, "unknown stat"),
    };

    /// <summary>Applies a function to every stat and returns the result.</summary>
    public Stats Map(Func<Stat, int, int> f) => new(
        f(Stat.Hp, Hp), f(Stat.Str, Str), f(Stat.Mag, Mag), f(Stat.Dex, Dex), f(Stat.Spd, Spd),
        f(Stat.Lck, Lck), f(Stat.Def, Def), f(Stat.Res, Res), f(Stat.Cha, Cha));

    public static Stats operator +(Stats a, Stats b) => new(
        a.Hp + b.Hp, a.Str + b.Str, a.Mag + b.Mag, a.Dex + b.Dex, a.Spd + b.Spd,
        a.Lck + b.Lck, a.Def + b.Def, a.Res + b.Res, a.Cha + b.Cha);

    public static Stats operator -(Stats a, Stats b) => new(
        a.Hp - b.Hp, a.Str - b.Str, a.Mag - b.Mag, a.Dex - b.Dex, a.Spd - b.Spd,
        a.Lck - b.Lck, a.Def - b.Def, a.Res - b.Res, a.Cha - b.Cha);

    public override string ToString() =>
        $"HP {Hp} Str {Str} Mag {Mag} Dex {Dex} Spd {Spd} Lck {Lck} Def {Def} Res {Res} Cha {Cha}";
}
