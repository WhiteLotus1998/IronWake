using Ironwake.Cli;
using Ironwake.Core;

namespace Ironwake.Client;

/// <summary>How a strike's number rises off its target (issue 513): damage, a miss, or a crit, which is drawn louder.</summary>
public enum PopKind
{
    Damage,
    Miss,
    Crit,
}

/// <summary>
/// One strike's number as it rises off its target (issue 513): the tile it rises from, the text
/// (the damage, or <c>miss</c>), how loud it is, the unit struck and its HP once the strike lands.
/// </summary>
public sealed record Pop(Coord At, string Text, PopKind Kind, string TargetId, int TargetHpAfter);

/// <summary>
/// A unit fallen this phase (issue 513, round 141): the unit as it stood before the strike that
/// killed it, so its token can fade and its tile keep a mark for the rest of the phase.
/// </summary>
public sealed record FallenMark(BattleUnit Unit, Coord At);

/// <summary>
/// What the client animates for one event (issue 513), read from the event alone: a move's walk
/// (the unit, its start, its path and its end), a strike's lunge (the attacker, the tile it struck
/// from and the tile it struck) with each strike's number, both sides' HP before the first strike,
/// or a death. A renderer plays a beat over the state the event left; it adds nothing the state
/// and the event do not say, and a skipped beat changes nothing.
/// </summary>
public sealed record Beat(
    string? UnitId,
    Coord? From,
    IReadOnlyList<Coord> Path,
    Coord? To,
    Coord? Struck,
    IReadOnlyList<Pop> Pops,
    IReadOnlyDictionary<string, int> HpBefore,
    FallenMark? Fell)
{
    private static readonly IReadOnlyDictionary<string, int> NoHp = new Dictionary<string, int>();

    public bool IsMove => From is not null && To is not null;

    public bool IsStrike => Pops.Count > 0;

    /// <summary>
    /// The beat for <paramref name="e"/>, or null when there is nothing to animate: the state
    /// after its command says where a combat's sides stood (a cover's swap included), the state
    /// before it their HP before the first strike and who a death took.
    /// </summary>
    public static Beat? Of(GameEvent e, BattleState before, BattleState after)
    {
        var none = Array.Empty<Coord>();
        switch (e)
        {
            case UnitMoved m when m.From != m.To:
                return new Beat(m.UnitId, m.From, m.Path, m.To, null, Array.Empty<Pop>(), NoHp, null);
            case Cantoed m when m.From != m.To:
                return new Beat(m.UnitId, m.From, m.Path, m.To, null, Array.Empty<Pop>(), NoHp, null);
            case CombatFought c when before.Find(c.AttackerId) is { } attacker && before.Find(c.TargetId) is { } target:
                var at = new Dictionary<string, Coord> { [attacker.Id] = after.Find(attacker.Id)?.At ?? attacker.At, [target.Id] = after.Find(target.Id)?.At ?? target.At };
                var pops = c.Strikes.Select(s => new Pop(
                    at[s.TargetId],
                    s.Hit ? s.Damage.ToString(System.Globalization.CultureInfo.InvariantCulture) : "miss",
                    !s.Hit ? PopKind.Miss : s.Crit ? PopKind.Crit : PopKind.Damage,
                    s.TargetId,
                    s.TargetHpAfter)).ToList();
                var hp = new Dictionary<string, int> { [attacker.Id] = attacker.Hp, [target.Id] = target.Hp };
                return new Beat(attacker.Id, null, none, at[attacker.Id], at[target.Id], pops, hp, null);
            case UnitDied d when before.Find(d.UnitId) is { } dead:
                return new Beat(d.UnitId, null, none, d.At, null, Array.Empty<Pop>(), NoHp, new FallenMark(dead, d.At));
            default:
                return null;
        }
    }
}

/// <summary>
/// The enemy-act card (issue 513, round 141): the forecast card's slot during the enemy phase,
/// read after the fact. Who acted, what it did in the console's words, and for a strike both
/// sides' HP before and after with each strike's result in order.
/// </summary>
public sealed record ActCard(string ActorId, string ActorName, string ActorClassId, string Doing, ActSide? Attacker, ActSide? Defender);

/// <summary>One side of a fought act: the unit, its HP before and after out of its max, and its strikes' results (<c>7</c>, <c>miss</c>, <c>crit 21</c>).</summary>
public sealed record ActSide(string Id, string Name, string ClassId, bool IsBoss, int HpBefore, int HpAfter, int MaxHp, IReadOnlyList<string> Strikes);

public static class ActCards
{
    /// <summary>
    /// The card for an enemy-phase event, or null for an event that is not a unit's act (a
    /// notice, a gain, a dark line): a strike names both sides; a move, wait or heal only the actor.
    /// </summary>
    public static ActCard? Of(GameEvent e, BattleState before, BattleState after, GameContent content)
    {
        BattleUnit? Find(string id) => before.Find(id) ?? after.Find(id);
        ActCard Solo(string id, string doing) =>
            Find(id) is { } u ? new ActCard(u.Id, u.Unit.Name, u.Unit.ClassId, doing, null, null) : new ActCard(id, id, "", doing, null, null);
        switch (e)
        {
            case CombatFought c when before.Find(c.AttackerId) is { } a && before.Find(c.TargetId) is { } t:
                ActSide Side(BattleUnit u, int hpAfter) => new(u.Id, u.Unit.Name, u.Unit.ClassId, u.IsBoss, u.Hp, hpAfter, u.MaxHp(content),
                    c.Strikes.Where(s => s.AttackerId == u.Id).Select(s => !s.Hit ? "miss" : s.Crit ? $"crit {s.Damage}" : s.Damage.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList());
                var result = c.TargetHpAfter == 0 ? $"{t.Unit.Name} falls" : c.AttackerHpAfter == 0 ? $"{a.Unit.Name} falls" : "both stand";
                return new ActCard(a.Id, a.Unit.Name, a.Unit.ClassId, $"strikes {t.Unit.Name}: {result}", Side(a, c.AttackerHpAfter), Side(t, c.TargetHpAfter));
            case UnitMoved m:
                return Solo(m.UnitId, $"moves to {m.To.X},{m.To.Y}");
            case Cantoed m:
                return Solo(m.UnitId, $"moves on to {m.To.X},{m.To.Y}");
            case UnitWaited w:
                return Solo(w.UnitId, w.Braced ? "waits, braced" : "waits");
            case UnitHealed h:
                return Solo(h.UnitId, PlaySession.Describe(h, content));
            case UnitSpawned s:
                return Solo(s.UnitId, $"arrives at {s.At.X},{s.At.Y}");
            default:
                return null;
        }
    }
}
