namespace Ironwake.Core;

/// <summary>
/// What a school's rider does (issue 1243, DECISIONS/0297). Core acts on the kind, never on a
/// school's name, so a school takes a new rider by content alone once its kind exists.
/// </summary>
public enum RiderKind
{
    /// <summary>A hit leaves the target burning (<see cref="Burning"/>).</summary>
    Burn,
}

/// <summary>
/// A school's rider, from <c>rules.json</c>'s <c>schools</c> block (issue 1243): every tome of the
/// school carries it (<see cref="GameContent.RiderOf"/>). <paramref name="Amount"/> is how hard it
/// bites and <paramref name="Phases"/> for how many of the struck side's phases.
/// </summary>
public sealed record SchoolRider(RiderKind Kind, int Amount, int Phases)
{
    /// <summary>The word content writes for <paramref name="kind"/>: <c>burn</c>.</summary>
    public static string Label(RiderKind kind) => kind.ToString().ToLowerInvariant();

    /// <summary>A count of phases as a line says it: <c>one phase</c>, <c>two phases</c>; past nine, in digits.</summary>
    public static string PhasesText(int phases)
    {
        string[] words = ["zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine"];
        var count = phases >= 0 && phases < words.Length ? words[phases] : phases.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return count + (phases == 1 ? " phase" : " phases");
    }
}
