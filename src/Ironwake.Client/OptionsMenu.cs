using System.Globalization;
using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Core;

namespace Ironwake.Client;

/// <summary>
/// The Options screen as rows a renderer draws and a click cycles (issue 677, slice 2): each row
/// is one profile key, its words, and the values a click steps through in order, wrapping. A
/// cycle goes through <see cref="Options.Set"/>, so a row can never hold a value the profile
/// would refuse. UI scale is a row since the layout reflows (issue 698, <see cref="UiLayout"/>).
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
        ("confirm-lethal", "Confirm end turn while a unit is lethal", new[] { "on", "off" }),
        ("reach-on-hover", "Show enemy reach on hover", new[] { "on", "off" }),
        ("ui-scale", "UI scale", Options.UiScales.Select(v => v.ToString(CultureInfo.InvariantCulture)).ToList()),
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
        "ui-scale" => value + "%",
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
/// (issue 558). With the lethal confirm asking (issue 1120: the <c>confirm-lethal</c> option under
/// the difficulty in play), a lethal line alone asks too. E again ends the phase; Esc goes back.
/// </summary>
public static class EndTurnConfirm
{
    /// <summary>The confirm's last line: how to answer it.</summary>
    public const string Answer = "E again ends the phase, Esc goes back.";

    /// <summary>
    /// The confirm's lines, or null when ending the phase needs no confirm: both confirms are off,
    /// the battle is decided, it is not the player's phase, or every unit has moved or acted and
    /// no unit is lethal under an asking lethal confirm.
    /// </summary>
    public static IReadOnlyList<string>? Lines(BattleState state, GameContent content, bool confirmOn, bool lethalOn = false)
    {
        if ((!confirmOn && !lethalOn) || state.Outcome.IsOver || state.Phase != Side.Player)
        {
            return null;
        }

        var names = UnitNames.Of(state, content);
        var unmoved = confirmOn ? state.UnitsOf(Side.Player).Where(u => !u.Moved && !u.Acted).ToList() : new List<BattleUnit>();
        var lethal = Queries.Lethal(state, content);
        if (unmoved.Count == 0 && (!lethalOn || lethal.Count == 0))
        {
            return null;
        }

        var lines = new List<string>();
        if (unmoved.Count > 0)
        {
            var who = string.Join(", ", unmoved.Select(u => names[u.Id]));
            lines.Add(unmoved.Count == 1 ? $"1 unit has not moved: {who}." : $"{unmoved.Count} units have not moved: {who}.");
        }

        lines.AddRange(lethal.Select(l => PlaySession.LethalLine(l, names)));
        lines.Add(Answer);
        return lines;
    }
}
