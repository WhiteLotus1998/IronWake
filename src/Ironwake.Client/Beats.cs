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
public sealed record Pop(Coord At, string Text, PopKind Kind, string TargetId, int TargetHpAfter)
{
    /// <summary>
    /// True for the strike that kills (issue 514, round 151): a hit that leaves its target at 0.
    /// It is drawn at least the unit glyph's size, outlined, and held through the death beat, so
    /// the loudest number on screen is always the one that killed someone.
    /// </summary>
    public bool Lethal => Kind != PopKind.Miss && TargetHpAfter == 0;
}

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
    /// <summary>
    /// On a death beat, the number of the strike that killed (issue 514): the renderer holds it
    /// over the fallen unit's tile through the fade and the hold, after the strike beat has ended.
    /// </summary>
    public Pop? Held { get; init; }

    /// <summary>
    /// On a death beat, whether the RECALL chip pulses (issue 514, round 152): a player unit
    /// killed in the enemy phase with a charge left, so the death and its answer sit together.
    /// The pulse starts on the hold (<see cref="Rhythm.Fade"/>), never on the number's beat.
    /// </summary>
    public bool Pulse { get; init; }

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
/// sides' HP before and after with each strike's result in order. A strike that kills names the
/// fallen unit (issue 544, round 148), so the card can put the death in its headline.
/// </summary>
public sealed record ActCard(string ActorId, string ActorName, string ActorClassId, string Doing, ActSide? Attacker, ActSide? Defender, string? Fallen = null)
{
    /// <summary>What the card leads with: the fallen unit's death when the act killed, else the actor's name.</summary>
    public string Headline => Fallen is { } name ? $"{name} falls" : ActorName;

    /// <summary>The line under the headline: what the actor did, or on a kill who struck the blow.</summary>
    public string Subtitle => Fallen is null ? Doing : Attacker is { HpAfter: 0 } ? $"falls on the counter, striking {Defender?.Name}" : $"struck down by {ActorName}";
}

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
                var fallen = c.TargetHpAfter == 0 ? t.Unit.Name : c.AttackerHpAfter == 0 ? a.Unit.Name : null;
                var result = fallen is null ? "both stand" : $"{fallen} falls";
                return new ActCard(a.Id, a.Unit.Name, a.Unit.ClassId, $"strikes {t.Unit.Name}: {result}", Side(a, c.AttackerHpAfter), Side(t, c.TargetHpAfter), fallen);
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

/// <summary>
/// The rhythm beats play to (issue 544, round 148), in seconds at normal speed: a walk and a miss
/// are quick, a hit gets a beat, a crit a longer one, and a death holds still before the next act,
/// so the one story beat of a phase is not played at the weight of a miss. The renderer scales
/// every number by its speed; skipping a beat skips its hold.
/// </summary>
public static class Rhythm
{
    /// <summary>The lean before a strike's first number lands.</summary>
    public const float Lead = 0.15f;

    /// <summary>How long a death's token takes to shrink into its mark.</summary>
    public const float Fade = 0.5f;

    /// <summary>The stillness after a death, before anything else moves.</summary>
    public const float DeathHold = 0.6f;

    /// <summary>The pause after an act before the next is shown.</summary>
    public const float Gap = 0.3f;

    /// <summary>How long one strike holds the stage before the next: a miss is quick, a hit gets a beat, a crit more.</summary>
    public static float StrikeStep(Pop pop) => pop.Kind switch
    {
        PopKind.Miss => 0.35f,
        PopKind.Crit => 0.75f,
        _ => 0.55f,
    };

    /// <summary>When each of a strike beat's numbers lands, from the beat's start.</summary>
    public static IReadOnlyList<float> PopTimes(Beat beat)
    {
        var times = new float[beat.Pops.Count];
        var t = Lead;
        for (var i = 0; i < beat.Pops.Count; i++)
        {
            times[i] = t;
            t += StrikeStep(beat.Pops[i]);
        }

        return times;
    }

    /// <summary>
    /// How long a Recall's scrub runs (issue 514): the board folds back through every state the
    /// rewind passes, each getting its share of this second.
    /// </summary>
    public const float Scrub = 1.0f;

    /// <summary>
    /// When the RECALL chip starts to pulse on a death beat that carries one (issue 514, round
    /// 152): on the hold, after the fade, so the lethal number and the pulse never share a beat.
    /// </summary>
    public const float PulseStart = Fade;

    /// <summary>
    /// Where a scrub of <paramref name="frames"/> states stands at <paramref name="u"/> of its
    /// length: the step it is on (from frame <c>Step</c> to frame <c>Step + 1</c>) and how far
    /// through that step, each step an equal share. Past the end it rests on the last frame.
    /// </summary>
    public static (int Step, float Through) ScrubAt(int frames, float u)
    {
        if (frames < 2 || u >= 1)
        {
            return (Math.Max(0, frames - 2), 1);
        }

        var f = Math.Max(0, u) * (frames - 1);
        var step = Math.Min((int)f, frames - 2);
        return (step, f - step);
    }

    /// <summary>A beat's whole length: a walk by its steps, a strike by its numbers, a death by its fade and hold.</summary>
    public static float Length(Beat beat) =>
        beat.IsMove ? 0.1f + 0.07f * (beat.Path.Count + 1)
        : beat.IsStrike ? Lead + beat.Pops.Sum(StrikeStep) + 0.1f
        : beat.Fell is not null ? Fade + DeathHold
        : 0;
}
