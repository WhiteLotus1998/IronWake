namespace Ironwake.Core;

/// <summary>Movement types from DESIGN.md section 3. Terrain cost and effective damage key on these.</summary>
public enum MovementType
{
    Infantry,
    Cavalry,
    Flying,
    Armored,
}

/// <summary>Weapon types from DESIGN.md section 5, gauntlets the Phase 3 seventh (issue 70).</summary>
public enum WeaponType
{
    Sword,
    Lance,
    Axe,
    Bow,
    Reason,
    Faith,

    /// <summary>Physical, range 1, targets Def; an attack with it is a round of two strikes and spends one use per combat (issue 70).</summary>
    Gauntlet,
}

public static class WeaponTypeExtensions
{
    /// <summary>Reason and Faith attack with Mag and target Res; their uses refresh each map.</summary>
    public static bool IsMagic(this WeaponType type) => type is WeaponType.Reason or WeaponType.Faith;

    /// <summary>The strikes one attack or counter with this type makes before the other side answers: two for gauntlets, one otherwise (issue 70).</summary>
    public static int StrikesPerRound(this WeaponType type) => type == WeaponType.Gauntlet ? 2 : 1;

    /// <summary>Whether every strike spends a use; a gauntlet spends one use per combat however many strikes it makes (issue 70).</summary>
    public static bool SpendsPerStrike(this WeaponType type) => type != WeaponType.Gauntlet;
}
