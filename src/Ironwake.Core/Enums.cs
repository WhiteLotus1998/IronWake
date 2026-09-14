namespace Ironwake.Core;

/// <summary>Movement types from DESIGN.md section 3. Terrain cost and effective damage key on these.</summary>
public enum MovementType
{
    Infantry,
    Cavalry,
    Flying,
    Armored,
}

/// <summary>Weapon types from DESIGN.md section 5. Gauntlets arrive in Phase 3.</summary>
public enum WeaponType
{
    Sword,
    Lance,
    Axe,
    Bow,
    Reason,
    Faith,
}

public static class WeaponTypeExtensions
{
    /// <summary>Reason and Faith attack with Mag and target Res; their uses refresh each map.</summary>
    public static bool IsMagic(this WeaponType type) => type is WeaponType.Reason or WeaponType.Faith;
}
