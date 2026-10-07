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
}

/// <summary>
/// A school's rider, from <c>rules.json</c>'s <c>schools</c> block (issue 1243): the shape a tome
/// of the school takes when it names the kind (issue 1250, <see cref="GameContent.RiderOf"/>). <paramref name="Amount"/> is how hard it
/// bites and <paramref name="Phases"/> for how many of the struck side's phases; a burn's alone, 0 for
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

    /// <summary>A burn rider's <see cref="Cap"/> when rules.json names none.</summary>
    public const int DefaultCap = 4;

    /// <summary>The word content writes for <paramref name="kind"/>: <c>burn</c>, <c>chill</c>, <c>stun</c>, <c>raise</c>, <c>ember</c>.</summary>
    public static string Label(RiderKind kind) => kind.ToString().ToLowerInvariant();

    /// <summary>A count of phases as a line says it: <c>one phase</c>, <c>two phases</c>; past nine, in digits.</summary>
    public static string PhasesText(int phases)
    {
        string[] words = ["zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine"];
        var count = phases >= 0 && phases < words.Length ? words[phases] : phases.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return count + (phases == 1 ? " phase" : " phases");
    }
}
