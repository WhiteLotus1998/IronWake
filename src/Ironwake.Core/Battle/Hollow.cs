namespace Ironwake.Core;

/// <summary>
/// What a Hollow is bound to (issue 1284, <see cref="Hollow"/>): the caster that raised it, the unit whose body it
/// rose from, and <paramref name="Phases"/>, how many of its own side's phases are still to begin before it
/// crumbles. It crumbles as its side's phase ends with <paramref name="Phases"/> at 0.
/// </summary>
public sealed record HollowMark(string RaiserId, string FallenId, int Phases);

/// <summary>
/// Dark's raise dead (issue 1284, Lotus's #1247 rulings, DECISIONS/0307: "a fallen enemy rises on your side as a
/// Hollow, cooler than a zombie, not too strong"). A tome naming <see cref="RiderKind.Hollow"/> on a school whose
/// rider is <see cref="RiderKind.Drain"/> is cast through the Item action on a body
/// (<see cref="BattleState.Bodies"/>): an enemy unit that died this map, whichever side casts (issue 1286), its tile empty, in the tome's
/// range, named by the tile or by the fallen unit's id. It spends one use and the caster's action, earns no EXP, and
/// is once a map per caster (<see cref="BattleUnit.RaiseSpent"/>). The body leaves the list and a Hollow stands on
/// its tile, on the caster's side:
/// <list type="bullet">
/// <item>The fallen unit's class and stats and its equipped weapon alone, at half its max HP rounded up
/// (<see cref="RisenHp"/>); id <c>hollow-</c> and the fallen id, named <c>Hollow</c> and the fallen name.</item>
/// <item>It rises having moved and acted, and acts in its side's next <see cref="Phases"/> phases; it crumbles as
/// the last of them ends, or the moment its raiser is off the board (<see cref="HollowCrumbled"/>).</item>
/// <item>It moves, strikes and waits, nothing else (<see cref="Refusal"/>). It earns no EXP, rank or mastery,
/// leaves no keepsake and no body, has no group, and wakes no one by dying.</item>
/// <item>It is never the captain nor a protected unit, so it never holds a map; the campaign reads only its roster.</item>
/// </list>
/// Never the company's dead: permadeath means gone. Everything is board state, so Recall restores it. An enemy raiser
/// casts it through <see cref="EnemyAi.Raise"/> (issue 1286); <see cref="Resolver.Legal"/> and the Sim's player do not offer it.
/// </summary>
public static class Hollow
{
    /// <summary>How many of its own side's phases a Hollow acts in before it crumbles (issue 1284's lean).</summary>
    public const int Phases = 3;

    /// <summary>The id prefix a Hollow's id carries before the fallen unit's id.</summary>
    public const string IdPrefix = "hollow-";

    /// <summary>Whether <paramref name="weapon"/> raises the dead.</summary>
    public static bool Raises(GameContent content, Weapon? weapon) =>
        content.RiderOf(weapon) is { Kind: RiderKind.Hollow };

    /// <summary>The HP a Hollow rises with: half the fallen unit's max HP, rounded up, at least 1.</summary>
    public static int RisenHp(int maxHp) => Math.Max(1, (maxHp + 1) / 2);

    /// <summary>
    /// The board's record of a unit that died (issue 1284): <paramref name="fallen"/> joins
    /// <see cref="BattleState.Bodies"/> where it fell, unless it was a Hollow, which leaves no body.
    /// </summary>
    public static BattleState LeaveBody(BattleState state, BattleUnit fallen) =>
        fallen.Hollow is not null ? state : state with { Bodies = state.Bodies.Add(fallen with { Hp = 0 }) };

    /// <summary>The body on <paramref name="at"/>, the newest when two fell there; null when none lies there.</summary>
    public static BattleUnit? BodyAt(BattleState state, Coord at) => state.Bodies.LastOrDefault(b => b.At == at);

    /// <summary>
    /// Why <paramref name="command"/> is refused because the unit it names is a Hollow: a Hollow moves, strikes and
    /// waits (Move, Dash, Canto, Fall back, Shove, Attack, Wait), and nothing else. Null when it is allowed.
    /// </summary>
    public static Rejection? Refusal(BattleState state, Command command)
    {
        var unitId = command switch
        {
            UseItem c => c.UnitId,
            Retreat c => c.UnitId,
            Exit c => c.UnitId,
            Recover c => c.UnitId,
            Open c => c.UnitId,
            Drop c => c.UnitId,
            Carry c => c.UnitId,
            Breathe c => c.UnitId,
            Watch c => c.UnitId,
            Cover c => c.UnitId,
            Talk c => c.UnitId,
            TakeShard c => c.UnitId,
            _ => null,
        };
        return unitId is not null && state.Find(unitId) is { Hollow: not null } hollow
            ? new Rejection(RejectionReason.NotUsable, $"{hollow.Id} is a Hollow: it moves, strikes and waits, nothing else")
            : null;
    }

    /// <summary>
    /// <paramref name="caster"/> raises <paramref name="body"/> with the tome in <paramref name="slot"/>, named in the
    /// command as <paramref name="named"/>: the wield, use, range and the body's side are the caller's checks. Refused
    /// when the caster's raise is spent or the tile is occupied. Otherwise emits <see cref="ItemUsed"/>,
    /// <see cref="HollowRaised"/> and, on the tome's last use, <see cref="SpellSpent"/>; the caster has moved and acted.
    /// </summary>
    public static (BattleState, Rejection?) Raise(
        BattleState state, GameContent content, BattleUnit caster, int slot, Weapon spell, BattleUnit body, string named, List<GameEvent> events)
    {
        if (caster.RaiseSpent)
        {
            return (state, new Rejection(RejectionReason.NotUsable, $"{caster.Id} has raised the dead once this map; it raises once a map"));
        }

        if (state.UnitAt(body.At) is { } standing)
        {
            return (state, new Rejection(RejectionReason.NotUsable, $"{standing.Id} stands on {body.At}; a body rises only from an empty tile"));
        }

        var stack = caster.Unit.Inventory.Items[slot];
        var usesLeft = stack.Uses - 1;
        var equipped = body.EquippedSlot(content);
        var inventory = equipped < 0 ? Inventory.Empty : Inventory.Empty.Add(body.Unit.Inventory.Items[equipped] with { Keepsake = null });
        var hp = RisenHp(body.MaxHp(content));
        var id = IdPrefix + body.Id;
        var risen = new BattleUnit(
            body.Unit with { Id = id, Name = "Hollow " + body.Unit.Name, Inventory = inventory },
            caster.Side,
            body.At,
            hp,
            Moved: true,
            Acted: true,
            Behavior: caster.Side == Side.Enemy ? Core.Behavior.Aggressive : null,
            PlacementIndex: body.PlacementIndex)
        {
            Hollow = new HollowMark(caster.Id, body.Id, Phases),
        };

        events.Add(new ItemUsed(caster.Id, spell.Id, named, usesLeft));
        events.Add(new HollowRaised(caster.Id, id, body.Id, body.At, hp, Phases));
        if (usesLeft == 0)
        {
            events.Add(new SpellSpent(caster.Id, spell.Id));
        }

        var after = caster with
        {
            Moved = true,
            Acted = true,
            RaiseSpent = true,
            Unit = caster.Unit with { Inventory = caster.Unit.Inventory.Replace(slot, stack with { Uses = usesLeft }) },
        };
        var bodies = state.Bodies.ToList();
        bodies.RemoveAt(bodies.LastIndexOf(body));
        var next = state.WithUnit(after) with { Bodies = ValueList<BattleUnit>.From(bodies) };
        return (next.WithRisen(risen), null);
    }

    /// <summary>
    /// The Hollows across a phase change from <paramref name="ended"/> to <paramref name="begins"/>: one whose side's
    /// phase ended with none of its phases still to begin crumbles (<see cref="HollowCrumbled"/>); otherwise one is
    /// counted off when its side's phase begins.
    /// </summary>
    public static BattleState AtPhaseChange(BattleState state, Side ended, Side begins, List<GameEvent> events)
    {
        foreach (var unit in state.Units.Where(u => u.Hollow is not null).ToList())
        {
            var mark = unit.Hollow!;
            if (unit.Side == ended && mark.Phases == 0)
            {
                events.Add(new HollowCrumbled(unit.Id, RaiserFell: false));
                state = state.WithoutUnit(unit.Id);
            }
            else if (unit.Side == begins)
            {
                state = state.WithUnit(unit with { Hollow = mark with { Phases = mark.Phases - 1 } });
            }
        }

        return state;
    }

    /// <summary>After every accepted command: each Hollow whose raiser is no longer on the board crumbles (<see cref="HollowCrumbled"/>).</summary>
    public static BattleState After(BattleState state, List<GameEvent> events)
    {
        foreach (var unit in state.Units.Where(u => u.Hollow is { } mark && state.Find(mark.RaiserId) is null).ToList())
        {
            events.Add(new HollowCrumbled(unit.Id, RaiserFell: true));
            state = state.WithoutUnit(unit.Id);
        }

        return state;
    }

    /// <summary>
    /// When the Hollow crumbles, in player words, by how many of its side's phases are still to begin:
    /// <c>as this player phase ends</c>, <c>as the next player phase ends</c>, or <c>after 3 more player phases</c>.
    /// </summary>
    public static string Crumbles(BattleUnit unit, HollowMark mark)
    {
        var word = Frost.Word(unit.Side);
        return mark.Phases switch
        {
            0 => $"as this {word} phase ends",
            1 => $"as the next {word} phase ends",
            var owed => $"after {owed} more {word} phases",
        };
    }

    /// <summary>The unit card's line for a Hollow: <c>hollow: raised by pell, crumbles after 3 more player phases or when pell falls</c>; null for any other unit.</summary>
    public static string? CardLine(BattleUnit unit, UnitNames names) =>
        unit.Hollow is { } mark
            ? $"hollow: raised by {names[mark.RaiserId]}, crumbles {Crumbles(unit, mark)} or when {names[mark.RaiserId]} falls; earns nothing, leaves no body"
            : null;
}
