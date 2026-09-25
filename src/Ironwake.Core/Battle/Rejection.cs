namespace Ironwake.Core;

/// <summary>Why a command was refused. One value per rule, each with a test that shows it firing.</summary>
public enum RejectionReason
{
    NoSuchUnit,
    NotThisSide,
    AlreadyMoved,
    AlreadyActed,
    OutOfReach,
    NoSuchTarget,
    NotAnEnemy,
    NoWeapon,
    OutOfRange,
    EmptySlot,
    NotUsable,
    NoTarget,
    NotAnAlly,
    NothingToHeal,
    NoRecallCharges,
    NoSuchHistoryIndex,
    NotAPlayerPhase,
    CannotRetreat,
    BattleOver,
    UnknownCommand,
}

/// <summary>A refused command: the rule it broke and a sentence a player can read.</summary>
public sealed record Rejection(RejectionReason Reason, string Message);

/// <summary>
/// The outcome of <see cref="Resolver.Apply"/>. A rejected command leaves
/// <see cref="Next"/> equal to the state it was applied to, emits no events, and names
/// its <see cref="Rejection"/>; the resolver never throws for an illegal command.
/// </summary>
public sealed record ApplyResult(BattleState Next, ValueList<GameEvent> Events, Rejection? Rejection)
{
    public bool Accepted => Rejection is null;
}
