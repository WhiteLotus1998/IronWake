namespace Ironwake.Core;

/// <summary>
/// Earth's Sunder (issue 1281, Lotus's #1247 rulings, DECISIONS/0307: "Sunder also grounds a flier and stuns it
/// for a turn"; round 416's Sunder drops raised ground). A tome naming <see cref="RiderKind.Sunder"/> on a school
/// whose rider is <see cref="RiderKind.Raise"/> does two things, each spending one of its uses:
/// <list type="bullet">
/// <item>Cast through the Item action on a raised tile (<see cref="TileOverlay"/>, either side's), named by the unit
/// standing on it or by the tile, it drops it at once: the tile gives its ground back, whoever stands there. A tile
/// with no overlay, a map's fort among them, is refused. It spends the caster's action and earns no EXP.</item>
/// <item>An attack with it that hits a flier still standing after the combat grounds it (<see cref="Grounding"/>,
/// the clock a bow's crit sets) and stuns it for its side's next phase (<see cref="Stun"/>'s clock). A boss is
/// grounded and spared the stun, as lightning's spares it. Any other unit takes only the hit.</item>
/// </list>
/// It is never once a map per caster as the stun rider is: the tome's uses are its limit. Everything is board
/// state, so Recall restores it with the board.
/// </summary>
public static class Sunder
{
    /// <summary>Whether <paramref name="weapon"/> is a sundering tome.</summary>
    public static bool Sunders(GameContent content, Weapon? weapon) =>
        content.RiderOf(weapon) is { Kind: RiderKind.Sunder };

    /// <summary>Whether a hit by <paramref name="weapon"/> on <paramref name="target"/> grounds it: a sundering tome against a unit whose class flies.</summary>
    public static bool Grounds(GameContent content, Weapon? weapon, BattleUnit target) =>
        Sunders(content, weapon) && content.Class(target.Unit.ClassId).Movement == MovementType.Flying;

    /// <summary>
    /// After a combat or a strike: each flier in <paramref name="strikes"/> still standing that a hit from a sundering
    /// tome landed on is grounded (<see cref="UnitGrounded"/>) and, unless a boss, stunned (<see cref="UnitStunned"/>),
    /// both clocks set to 1. <paramref name="aWeapon"/> is what <paramref name="aId"/> struck with and
    /// <paramref name="bWeapon"/> what <paramref name="bId"/> did.
    /// </summary>
    public static BattleState AfterCombat(BattleState state, GameContent content, string aId, Weapon? aWeapon, string bId, Weapon? bWeapon, ValueList<StrikeEvent> strikes, List<GameEvent> events)
    {
        foreach (var (strikerId, weapon, targetId) in new[] { (aId, aWeapon, bId), (bId, bWeapon, aId) })
        {
            if (state.Find(targetId) is not { } target
                || !Grounds(content, weapon, target)
                || !strikes.Any(s => s.AttackerId == strikerId && s.TargetId == targetId && s.Hit))
            {
                continue;
            }

            var next = state.Phase == target.Side;
            events.Add(new UnitGrounded(target.Id, strikerId, target.Side, next));
            target = target with { Grounded = 1 };
            if (!target.IsBoss)
            {
                events.Add(new UnitStunned(target.Id, strikerId, target.Side, next));
                target = target with { Stun = 1 };
            }

            state = state.WithUnit(target);
        }

        return state;
    }

    /// <summary>
    /// The forecast's words for a sundering tome, after its strike columns: <c> grounds and stuns</c> against a flier,
    /// <c> grounds (stun: bosses spared)</c> against a flying boss; empty against anything that does not fly, or for any other weapon.
    /// </summary>
    public static string ForecastText(GameContent content, Weapon? weapon, BattleUnit target) =>
        !Grounds(content, weapon, target) ? ""
        : target.IsBoss ? " grounds (stun: bosses spared)"
        : " grounds and stuns";

    /// <summary>
    /// <paramref name="caster"/> drops the overlay on <paramref name="at"/> with the tome in <paramref name="slot"/>,
    /// named in the command as <paramref name="named"/>: the wield, use and range are the caller's checks. Refused when
    /// no overlay stands there. Otherwise the tile gives its ground back (<see cref="ItemUsed"/>, <see cref="GroundSundered"/>,
    /// <see cref="TerrainChanged"/>), and the caster has moved and acted.
    /// </summary>
    public static (BattleState, Rejection?) Drop(
        BattleState state, GameContent content, BattleUnit caster, int slot, Weapon spell, Coord at, string named, List<GameEvent> events)
    {
        if (Earthwork.At(state, at) is not { } overlay || state.Map.TerrainIdAt(at) != overlay.TerrainId)
        {
            var terrain = state.Map.TerrainAt(at, content);
            return (state, new Rejection(
                RejectionReason.NotUsable,
                $"{spell.Name} drops only raised ground; {terrain.Name} at {at} was not raised (never a map's own fort)"));
        }

        var stack = caster.Unit.Inventory.Items[slot];
        var usesLeft = stack.Uses - 1;
        events.Add(new ItemUsed(caster.Id, spell.Id, named, usesLeft));
        events.Add(new GroundSundered(caster.Id, at, overlay.TerrainId, overlay.OwnerId));
        events.Add(new TerrainChanged(at, overlay.UnderId));
        if (usesLeft == 0)
        {
            events.Add(new SpellSpent(caster.Id, spell.Id));
        }

        var next = state with
        {
            Map = state.Map.WithTerrain(at, overlay.UnderId),
            Overlays = ValueList<TileOverlay>.From(state.Overlays.Where(o => o.At != at)),
        };
        var after = caster with { Moved = true, Acted = true, Unit = caster.Unit with { Inventory = caster.Unit.Inventory.Replace(slot, stack with { Uses = usesLeft }) } };
        return (next.WithUnit(after), null);
    }

    /// <summary>Reads <paramref name="text"/> as a tile, <c>x,y</c>, or null when it is not one.</summary>
    public static Coord? TileOf(string text)
    {
        var parts = text.Split(',');
        return parts.Length == 2
            && int.TryParse(parts[0], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var x)
            && int.TryParse(parts[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var y)
            ? new Coord(x, y) : null;
    }
}
