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

/// <summary>An item or a healing spell was used (section 7's Item action); a <see cref="UnitHealed"/> for the target follows.</summary>
public sealed record ItemUsed(string UnitId, string ItemId, string TargetId, int UsesLeft) : GameEvent;

/// <summary>An attack named a weapon slot other than the equipped one; the weapon is at the front of the inventory from now on.</summary>
public sealed record WeaponEquipped(string UnitId, string ItemId) : GameEvent;

/// <summary>A physical weapon reached zero uses on this strike; it stays in the inventory and fights at the section 5 fallback.</summary>
public sealed record WeaponBroke(string UnitId, string ItemId) : GameEvent;

/// <summary>A spell's last use this battle was spent; it refreshes at the next map.</summary>
public sealed record SpellSpent(string UnitId, string ItemId) : GameEvent;

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

/// <summary>
/// A map event fired (issue 32). <paramref name="Blocked"/> is true when its tile was held:
/// a spawn tile with a unit on it, or a terrain change its occupant could not stand on.
/// A blocked event is spent all the same. Unless blocked, the action's own event follows.
/// </summary>
public sealed record MapEventFired(string Name, bool Blocked) : GameEvent;

/// <summary>A map event changed a tile's terrain.</summary>
public sealed record TerrainChanged(Coord At, string TerrainId) : GameEvent;

/// <summary>A map event brought an enemy onto the board.</summary>
public sealed record UnitSpawned(string UnitId, Coord At, string Group, Behavior Behavior) : GameEvent;

/// <summary>A map event set a flag.</summary>
public sealed record FlagSet(string Flag) : GameEvent;
