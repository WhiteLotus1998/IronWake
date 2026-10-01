using System.Globalization;
using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Core;

namespace Ironwake.Client;

/// <summary>
/// The Options screen as rows a renderer draws and a click cycles (issue 677, slice 2): each row
/// is one profile key, its words, and the values a click steps through in order, wrapping. A
/// cycle goes through <see cref="Options.Set"/>, so a row can never hold a value the profile
/// would refuse. UI scale is not a row yet: the window already stretches the whole screen, and a
/// larger type needs a layout that reflows (DECISIONS/0141, issue 698).
/// </summary>
public static class OptionsMenu
{
    /// <summary>The volume steps a click on the volume row walks: 0 to 100 by 20.</summary>
    public static readonly IReadOnlyList<int> VolumeSteps = new[] { 0, 20, 40, 60, 80, 100 };

    /// <summary>The rows in screen order: the profile key, the row's words, and its values in cycle order.</summary>
    public static readonly IReadOnlyList<(string Key, string Label, IReadOnlyList<string> Values)> Rows = new (string, string, IReadOnlyList<string>)[]
    {
        ("speed", "Enemy phase speed", Options.Speeds),
        ("scenes", "Battle scenes", Options.SceneValues),
        ("confirm-end-turn", "Confirm end turn while units are unmoved", new[] { "on", "off" }),
        ("reach-on-hover", "Show enemy reach on hover", new[] { "on", "off" }),
        ("sound", "Sound", new[] { "on", "off" }),
        ("volume", "Volume", VolumeSteps.Select(v => v.ToString(CultureInfo.InvariantCulture)).ToList()),
    };

    /// <summary>The value <paramref name="options"/> holds for the row keyed <paramref name="key"/>, as the profile writes it.</summary>
    public static string Value(Options options, string key)
    {
        var prefix = key + ": ";
        return options.Lines().First(line => line.StartsWith(prefix, StringComparison.Ordinal))[prefix.Length..];
    }

    /// <summary>A row's value in words: the scenes as the footer says them, the rest as written.</summary>
    public static string Words(string key, string value) => key switch
    {
        "scenes" => value switch { "all" => "all", "map" => "map only", _ => "key moments" },
        _ => value,
    };

    /// <summary>
    /// The options after a click on the row keyed <paramref name="key"/>: its next value, wrapping
    /// to the first. A volume between the steps moves to the next step above it.
    /// </summary>
    public static Options Cycle(Options options, string key)
    {
        var values = Rows.First(r => r.Key == key).Values;
        var current = Value(options, key);
        var at = values.ToList().IndexOf(current);
        string next;
        if (at < 0 && key == "volume")
        {
            var volume = options.Volume;
            next = (VolumeSteps.FirstOrDefault(v => v > volume, VolumeSteps[0])).ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            next = values[(at + 1) % values.Count];
        }

        return Options.Set(options, key, next).Options;
    }
}

/// <summary>
/// The end-turn confirm (issue 677): with the option on, ending a player phase while any of the
/// player's units has neither moved nor acted asks first, saying how many and who, and the
/// lines <c>end</c> prints for a unit the coming enemy phase kills if every strike lands
/// (issue 558). E again ends the phase; Esc goes back.
/// </summary>
public static class EndTurnConfirm
{
    /// <summary>The confirm's last line: how to answer it.</summary>
    public const string Answer = "E again ends the phase, Esc goes back.";

    /// <summary>
    /// The confirm's lines, or null when ending the phase needs no confirm: the option is off,
    /// the battle is decided, it is not the player's phase, or every unit has moved or acted.
    /// </summary>
    public static IReadOnlyList<string>? Lines(BattleState state, GameContent content, bool confirmOn)
    {
        if (!confirmOn || state.Outcome.IsOver || state.Phase != Side.Player)
        {
            return null;
        }

        var names = UnitNames.Of(state, content);
        var unmoved = state.UnitsOf(Side.Player).Where(u => !u.Moved && !u.Acted).ToList();
        if (unmoved.Count == 0)
        {
            return null;
        }

        var who = string.Join(", ", unmoved.Select(u => names[u.Id]));
        var lines = new List<string>
        {
            unmoved.Count == 1 ? $"1 unit has not moved: {who}." : $"{unmoved.Count} units have not moved: {who}.",
        };
        lines.AddRange(Queries.Lethal(state, content).Select(l => PlaySession.LethalLine(l, names)));
        lines.Add(Answer);
        return lines;
    }
}
