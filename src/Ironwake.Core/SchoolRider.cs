namespace Ironwake.Core;

/// <summary>
/// What a school's rider does (issue 1243, DECISIONS/0297). Core acts on the kind, never on a
/// school's name, so a school takes a new rider by content alone once its kind exists.
/// </summary>
public enum RiderKind
{
    /// <summary>A hit leaves the target burning (<see cref="Burning"/>).</summary>
    Burn,

    /// <summary>A hit chills the target as frozen iron does (issue 1244, <see cref="Frost"/>).</summary>
    Chill,

    /// <summary>A hit from a caster of a gated class stuns the target, once a map (issue 1244, <see cref="Stun"/>).</summary>
    Stun,

    /// <summary>Cast on an ally's tile as a support action, it lays a timed terrain overlay there (issue 1245, <see cref="Earthwork"/>).</summary>
    Raise,

    /// <summary>
    /// A hit on a burning target deals the burn it still owes now and clears it (issue 1279, Last
    /// Ember's, <see cref="Burning"/>). Only a tome names it, and only where its school's rider is
    /// <see cref="Burn"/>, whose gate it reads; a school's own rider is never an ember.
    /// </summary>
    Ember,

    /// <summary>
    /// Cast through the Item action on a raised tile it drops it; a hit on a flier grounds and stuns it (issue 1281,
    /// <see cref="Core.Sunder"/>). Only a tome names it, and only where its school's rider is <see cref="Raise"/>;
    /// a school's own rider is never a sunder.
    /// </summary>
    Sunder,

    /// <summary>
    /// Cast through the Item action on the caster alone, it wears stone: more Def, less Mov, for some of its phases
    /// (issue 1282, <see cref="Core.Armor"/>). Only a tome names it, carrying its own numbers (<see cref="Weapon.Armor"/>),
    /// and only where its school's rider is <see cref="Raise"/>; a school's own rider is never armor.
    /// </summary>
    Armor,

    /// <summary>A hit heals its caster by the HP it took off the target, up to max HP (issue 1283, <see cref="Drain"/>).</summary>
    Drain,

    /// <summary>
    /// Cast through the Item action on a fallen foe's body, it raises a Hollow on the caster's side (issue 1284,
    /// <see cref="Core.Hollow"/>). Only a tome names it, and only where its school's rider is <see cref="Drain"/>,
    /// whose gate it reads; a school's own rider is never a hollow.
    /// </summary>
    Hollow,

    /// <summary>
    /// A hit curses the target (issue 1328, DECISIONS/0322, <see cref="Core.Curse"/>): it strikes at
    /// <see cref="SchoolRider.Blind"/> less Hit, and at each of its side's next phase starts loses a tick that heals the caster.
    /// </summary>
    Curse,

    /// <summary>
    /// A hit on a unit on or beside water freezes it, Mov 0 through its side's next phase, bosses Mov 1 (issue 1330,
    /// Still Water's second use, <see cref="Core.Freeze"/>). Only a tome names it, and only where its school's rider is
    /// <see cref="Chill"/>; a school's own rider is never a freeze.
    /// </summary>
    Freeze,
}

/// <summary>
/// A school's rider, from <c>rules.json</c>'s <c>schools</c> block (issue 1243): the shape a tome
/// of the school takes when it names the kind (issue 1250, <see cref="GameContent.RiderOf"/>). <paramref name="Amount"/> is how hard it
/// bites and <paramref name="Phases"/> for how many of the struck side's phases; a burn's or a curse's alone, 0 for
/// a chill, a stun or a raise, whose clocks are fixed (issues 1244, 1245).
/// </summary>
public sealed record SchoolRider(RiderKind Kind, int Amount, int Phases)
{
    /// <summary>The classes whose casters fire a stun rider (issue 1244, <see cref="Stun"/>); empty for any other kind.</summary>
    public ValueList<string> Classes { get; init; } = ValueList<string>.Empty;

    /// <summary>The terrain a raise rider lays as its overlay (issue 1245, <see cref="Earthwork"/>); null for any other kind.</summary>
    public string? Terrain { get; init; }

    /// <summary>
    /// How far a learner's Mag must clear the target's Res for this rider to fire (issue 1246,
    /// <see cref="LearnedGate"/>): it fires when Mag is above Res plus this. 0 by default. It binds
    /// only a caster who reaches the school by a primer, never one whose class reaches it. Null is
    /// no gate at all: a learner's rider fires as a class's does (rules.json <c>"gate": null</c>).
    /// </summary>
    public int? Gate { get; init; } = 0;

    /// <summary>
    /// The most stacks a burn builds to (issue 1279, DECISIONS/0308): a burning hit adds one, never
    /// past this. Read only on a <see cref="RiderKind.Burn"/> rider; rules.json <c>cap</c>, 4 when omitted.
    /// </summary>
    public int Cap { get; init; } = DefaultCap;

    /// <summary>
    /// Whether a tome may name <paramref name="tomeKind"/> on a school whose rider is <paramref name="schoolKind"/>
    /// though the two differ: an ember on a burn (issue 1279), a sunder (issue 1281) or armor (issue 1282) on a raise, a hollow on a drain (issue 1284),
    /// a drain or a hollow on a curse (issue 1328: dark is Curse, Drain Life and Hollow once its rider is the curse), and a freeze on a chill (issue 1330).
    /// </summary>
    public static bool Borrows(RiderKind tomeKind, RiderKind schoolKind) =>
        (tomeKind, schoolKind) is (RiderKind.Ember, RiderKind.Burn) or (RiderKind.Sunder, RiderKind.Raise) or (RiderKind.Armor, RiderKind.Raise) or (RiderKind.Hollow, RiderKind.Drain)
            or (RiderKind.Drain, RiderKind.Curse) or (RiderKind.Hollow, RiderKind.Curse) or (RiderKind.Freeze, RiderKind.Chill);

    /// <summary>
    /// The Hit a cursed unit loses on every strike and counter while its curse runs (issue 1328, DECISIONS/0322: 30).
    /// Read only on a <see cref="RiderKind.Curse"/> rider; rules.json <c>blind</c>, required there.
    /// </summary>
    public int Blind { get; init; }

    /// <summary>A burn rider's <see cref="Cap"/> when rules.json names none.</summary>
    public const int DefaultCap = 4;

    /// <summary>The word content writes for <paramref name="kind"/>: <c>burn</c>, <c>chill</c>, <c>stun</c>, <c>raise</c>, <c>ember</c>, <c>sunder</c>, <c>armor</c>, <c>drain</c>, <c>hollow</c>, <c>curse</c>, <c>freeze</c>.</summary>
    public static string Label(RiderKind kind) => kind.ToString().ToLowerInvariant();

    /// <summary>A count of phases as a line says it: <c>one phase</c>, <c>two phases</c>; past nine, in digits.</summary>
    public static string PhasesText(int phases)
    {
        string[] words = ["zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine"];
        var count = phases >= 0 && phases < words.Length ? words[phases] : phases.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return count + (phases == 1 ? " phase" : " phases");
    }
}
