namespace Ironwake.Core;

/// <summary>
/// What breaking the Kin's shard does to Frozen Iron on a map (<c>shard_breaks:</c>, issue 1386 slices 3e and 3f): nothing,
/// or <see cref="Turns"/>, which lands it on the Kin too, never below half his stage's bar (Chat's lean, rounds 551 and
/// 555; shipped to the hill, DECISIONS/0395). The held dose, <c>stills</c>, was killed (0395).
/// </summary>
public enum ShardBreak
{
    None,
    Turns,
}

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
