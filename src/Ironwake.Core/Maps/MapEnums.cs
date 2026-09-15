namespace Ironwake.Core;

/// <summary>Which army a placed unit belongs to.</summary>
public enum Side
{
    Player,
    Enemy,
}

/// <summary>Enemy group behaviors from DESIGN.md section 8. Boss is Hold plus never leaving its tile.</summary>
public enum Behavior
{
    Aggressive,
    Hold,
    Guard,
    Boss,
}

/// <summary>Win conditions from DESIGN.md section 7, one per map.</summary>
public enum WinCondition
{
    Rout,
    Seize,
    DefeatBoss,
    Survive,
    Escape,
}

/// <summary>
/// What a <c>P</c> line in a map file asks the roster for: the captain, a specific
/// recruit, or any recruit (a deployment slot the roster fills in order).
/// </summary>
public enum PlayerSlot
{
    Captain,
    NamedRecruit,
    AnyRecruit,
}
