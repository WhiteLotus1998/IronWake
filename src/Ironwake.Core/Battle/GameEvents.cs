namespace Ironwake.Core;

/// <summary>
/// What happened when a command was applied. Events are the log: the CLI and every
/// renderer draw from these, never by diffing states (DESIGN.md section 2).
/// </summary>
public abstract record GameEvent;

public sealed record UnitMoved(string UnitId, Coord From, Coord To, ValueList<Coord> Path) : GameEvent;

/// <summary>A combat, strike by strike, with both sides' HP when it ended.</summary>
public sealed record CombatFought(
    string AttackerId, string TargetId, int Turn, Side Phase, ValueList<StrikeEvent> Strikes, int AttackerHpAfter, int TargetHpAfter) : GameEvent;

public sealed record UnitDied(string UnitId, Side Side, Coord At) : GameEvent;

/// <summary>EXP earned from one combat (DESIGN.md section 6), with the total toward the next level after it.</summary>
public sealed record ExpGained(string UnitId, int Amount, int ExpAfter) : GameEvent;

/// <summary>A level gained: <see cref="Gains"/> holds 1 for each stat that rose.</summary>
public sealed record LeveledUp(string UnitId, int NewLevel, Stats Gains) : GameEvent;

public sealed record UnitWaited(string UnitId) : GameEvent;

public sealed record PhaseEnded(Side Side, int Turn) : GameEvent;

public sealed record PhaseBegan(Side Side, int Turn) : GameEvent;

/// <summary>Healing terrain at the start of the owner's phase (DESIGN.md section 4). Amount is what was actually gained.</summary>
public sealed record UnitHealed(string UnitId, int Amount, int HpAfter) : GameEvent;

public sealed record Recalled(int ToIndex, int ChargesLeft) : GameEvent;

/// <summary>Why a Guard group woke (DESIGN.md section 8), the loudest cause first.</summary>
public enum WakeCause
{
    /// <summary>A member of the group died, at any distance.</summary>
    Death,

    /// <summary>A combat involved a tile within the wake radius plus 2 of a member.</summary>
    Noise,

    /// <summary>A player unit stands within the wake radius of a member.</summary>
    Proximity,
}

/// <summary>A Guard group woke; its members behave as Aggressive from now on.</summary>
public sealed record GroupWoke(string Group, WakeCause Cause) : GameEvent;
