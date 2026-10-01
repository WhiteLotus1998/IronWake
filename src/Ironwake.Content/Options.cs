using System.Globalization;

namespace Ironwake.Content;

/// <summary>
/// The player's options (issue 677), kept in the profile beside the saves (<see cref="SaveStore.ProfileFile"/>)
/// as one <c>key: value</c> line each, and read by any renderer: the enemy phase's speed
/// (<c>1x</c>, <c>2x</c>, <c>5x</c>, the top bar's three, or <c>instant</c>, which still logs every
/// act), the battle scenes (<c>key</c> moments, <c>all</c>, <c>map</c> only), whether ending a turn
/// with units unmoved asks first, whether hovering an enemy shows its reach, the UI scale (100, 125
/// or 150), and the sound with its volume (0 to 100). None of them changes a rule, so none is on
/// a campaign record.
/// </summary>
public sealed record Options
{
    public static readonly IReadOnlyList<string> Speeds = new[] { "1x", "2x", "5x", "instant" };
    public static readonly IReadOnlyList<string> SceneValues = new[] { "key", "all", "map" };
    public static readonly IReadOnlyList<int> UiScales = new[] { 100, 125, 150 };

    public string Speed { get; init; } = "1x";

    public string Scenes { get; init; } = "key";

    public bool ConfirmEndTurn { get; init; } = true;

    public bool ReachOnHover { get; init; } = true;

    public int UiScale { get; init; } = 100;

    public bool Sound { get; init; } = true;

    public int Volume { get; init; } = 80;

    /// <summary>The options as the profile writes them, one <c>key: value</c> line each, in a fixed order.</summary>
    public IReadOnlyList<string> Lines() => new[]
    {
        "speed: " + Speed,
        "scenes: " + Scenes,
        "confirm-end-turn: " + OnOff(ConfirmEndTurn),
        "reach-on-hover: " + OnOff(ReachOnHover),
        "ui-scale: " + UiScale.ToString(CultureInfo.InvariantCulture),
        "sound: " + OnOff(Sound),
        "volume: " + Volume.ToString(CultureInfo.InvariantCulture),
    };

    /// <summary>Whether <paramref name="line"/> is an option line (it has a colon) rather than a won difficulty's id.</summary>
    public static bool IsOptionLine(string line) => line.Contains(':');

    /// <summary>
    /// The options the profile's <paramref name="lines"/> set, every other option at its default,
    /// and one warning per line that could not be read (an unknown key or a value out of range),
    /// naming the line and the field. A bad line keeps the default rather than refusing the
    /// profile, since a profile is the player's file, never content.
    /// </summary>
    public static (Options Options, IReadOnlyList<string> Warnings) Read(IEnumerable<string> lines)
    {
        var options = new Options();
        var warnings = new List<string>();
        var number = 0;
        foreach (var raw in lines)
        {
            number++;
            var line = raw.Trim();
            if (!IsOptionLine(line))
            {
                continue;
            }

            var split = line.IndexOf(':');
            var key = line[..split].Trim();
            var value = line[(split + 1)..].Trim();
            var read = Set(options, key, value);
            if (read.Refusal is { } refusal)
            {
                warnings.Add($"{SaveStore.ProfileFile} line {number}, '{key}': {refusal}; kept the default");
            }
            else
            {
                options = read.Options;
            }
        }

        return (options, warnings);
    }

    /// <summary>The options with <paramref name="key"/> set to <paramref name="value"/>, or why that cannot be set.</summary>
    public static (Options Options, string? Refusal) Set(Options options, string key, string value)
    {
        switch (key)
        {
            case "speed":
                return Speeds.Contains(value) ? (options with { Speed = value }, null) : (options, "must be one of " + string.Join(", ", Speeds));
            case "scenes":
                return SceneValues.Contains(value) ? (options with { Scenes = value }, null) : (options, "must be one of " + string.Join(", ", SceneValues));
            case "confirm-end-turn":
                return ParseOnOff(value) is { } confirm ? (options with { ConfirmEndTurn = confirm }, null) : (options, "must be on or off");
            case "reach-on-hover":
                return ParseOnOff(value) is { } reach ? (options with { ReachOnHover = reach }, null) : (options, "must be on or off");
            case "ui-scale":
                return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var scale) && UiScales.Contains(scale)
                    ? (options with { UiScale = scale }, null)
                    : (options, "must be one of " + string.Join(", ", UiScales));
            case "sound":
                return ParseOnOff(value) is { } sound ? (options with { Sound = sound }, null) : (options, "must be on or off");
            case "volume":
                return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var volume) && volume <= 100
                    ? (options with { Volume = volume }, null)
                    : (options, "must be 0 to 100");
            default:
                return (options, "is not an option; expected speed, scenes, confirm-end-turn, reach-on-hover, ui-scale, sound or volume");
        }
    }

    private static string OnOff(bool value) => value ? "on" : "off";

    private static bool? ParseOnOff(string value) => value switch
    {
        "on" => true,
        "off" => false,
        _ => null,
    };
}
