namespace Ironwake.Core;

/// <summary>A unit's starting tile on a map, from the <c>units:</c> block of a map file.</summary>
public abstract record Placement(Coord At)
{
    public abstract Side Side { get; }
}

/// <summary>
/// A player deployment tile. <see cref="RecruitId"/> is set only for
/// <see cref="PlayerSlot.NamedRecruit"/>. The roster (issue 13) decides who stands here;
/// the map only says what kind of unit it wants.
/// </summary>
public sealed record PlayerPlacement(Coord At, PlayerSlot Slot, string? RecruitId = null) : Placement(At)
{
    public override Side Side => Side.Player;
}

/// <summary>
/// An enemy from a template in the content's unit list, scaled to the map's
/// <see cref="MapDefinition.EnemyLevel"/> when the template is below it; the scaled
/// unit comes from <see cref="MapDefinition.EnemyUnit"/>, not from here. Every enemy
/// belongs to a group with one behavior (DESIGN.md section 8). A boss is any enemy
/// written on a <c>B</c> line; its behavior is always <see cref="Behavior.Boss"/>.
/// </summary>
public sealed record EnemyPlacement(Coord At, string TemplateId, string Group, Behavior Behavior, bool IsBoss) : Placement(At)
{
    public override Side Side => Side.Enemy;
}
