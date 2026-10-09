namespace Ironwake.Core;

/// <summary>
/// Grit (issue 1461, DECISIONS/0386 and 0398): what a basic form costs on a map with the
/// <c>forms: on</c> header (<see cref="MapDefinition.FormsEnabled"/>). Every unit starts at 0,
/// gains 1 as its side's phase begins (the player side's first phase included), and 1 for each
/// hit landed on it in a fought combat (a counter's hit included), never above <see cref="Cap"/>.
/// A kill gains nothing. A declared form spends its <see cref="CombatArtEffect.Grit"/>, hit or
/// miss, and its weapon pays only for its strikes. Off the header nothing here runs.
/// </summary>
public static class Grit
{
    /// <summary>The most Grit a unit holds.</summary>
    public const int Cap = 3;

    /// <summary>The unit's Grit once its side's phase has begun on a forms map.</summary>
    public static BattleUnit AtPhaseStart(MapDefinition map, BattleUnit unit) =>
        map.FormsEnabled ? unit with { Grit = Math.Min(Cap, unit.Grit + 1) } : unit;

    /// <summary>The unit's Grit after a fought combat's <paramref name="strikes"/>: 1 for each that landed on it.</summary>
    public static BattleUnit AfterStrikes(MapDefinition map, BattleUnit unit, IEnumerable<StrikeEvent> strikes)
    {
        if (!map.FormsEnabled)
        {
            return unit;
        }

        var taken = strikes.Count(s => s.Hit && s.TargetId == unit.Id);
        return taken == 0 ? unit : unit with { Grit = Math.Min(Cap, unit.Grit + taken) };
    }


    /// <summary>The legend a <c>forms: on</c> map prints.</summary>
    public const string Legend = "forms: a form costs Grit, not uses; Grit +1 at each of its side's phase starts and +1 for each hit taken, at most 3";

    /// <summary>The status line's mark on a forms map: "Grit n/3".</summary>
    public static string Label(BattleUnit unit) => $"Grit {unit.Grit}/{Cap}";
}
