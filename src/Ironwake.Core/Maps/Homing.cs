namespace Ironwake.Core;

/// <summary>
/// The <c>goes_home:</c> header (issue 1372, round 484): an enemy group whose placed members keep
/// their posts. A member standing on its post strikes out as any woken unit does; standing off it,
/// after a strike took it there, it plans from one tile only, the reachable tile nearest its post
/// (<see cref="EnemyAi.HomeTile"/>), striking from there if it can and else ending there. So a bait
/// that pulls it off its post opens the post for one player phase, and the next enemy phase it walks
/// back and answers from home. A spawned member has no post and is unbound. Unlike 0080's guard boss,
/// which goes home only under the Defeat Boss veto, the rule binds on any win condition.
/// </summary>
public sealed record Homing(string Group)
{
    /// <summary>Whether the rule binds <paramref name="unit"/>: an enemy of the named group.</summary>
    public bool Binds(BattleUnit unit) => unit.Side == Side.Enemy && unit.Group == Group;

    /// <summary>The header's value as <see cref="MapDefinition"/> files write it: <c>hall</c>.</summary>
    public override string ToString() => Group;

    /// <summary>
    /// The rule line under the unit rows:
    /// <c>goes_home: the hall group strikes out from its posts, and off them walks home and strikes only from there</c>.
    /// </summary>
    public string Line() => $"goes_home: the {Group} group strikes out from its posts, and off them walks home and strikes only from there";
}
