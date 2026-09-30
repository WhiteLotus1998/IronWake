using System.Text;

namespace Ironwake.Client;

/// <summary>The showcase's six sounds (issue 516): four for the fight, one for a press, one bed under it all.</summary>
public enum Cue
{
    Hit,
    Miss,
    Crit,
    Fall,
    Click,
    Ambient,
}

/// <summary>
/// Which sound plays when (issue 516), read from the beats alone so the renderer adds nothing:
/// each strike's number sounds as it rises (hit, miss or crit), and a death sounds as its beat
/// starts. The clips live under the Godot project's <c>assets/sound/</c>, one file per cue, made by
/// <c>docs/sound/make_sounds.py</c> and listed in <c>LICENSES</c>; a file dropped in under the same
/// name replaces one without a code change.
/// </summary>
public static class Sound
{
    /// <summary>Every cue, in the order <c>LICENSES</c> lists its file.</summary>
    public static readonly IReadOnlyList<Cue> All = Enum.GetValues<Cue>();

    /// <summary>The key that mutes and unmutes every sound, shown on the title and in the footer.</summary>
    public const string MuteKey = "M";

    /// <summary>The clip's file name under <c>assets/sound/</c>.</summary>
    public static string File(Cue cue) => cue.ToString().ToLowerInvariant() + ".wav";

    /// <summary>The cue a strike's number sounds with.</summary>
    public static Cue Of(PopKind kind) => kind switch
    {
        PopKind.Miss => Cue.Miss,
        PopKind.Crit => Cue.Crit,
        _ => Cue.Hit,
    };

    /// <summary>
    /// A beat's sounds, each with the strike it waits for: the index of a pop in
    /// <see cref="Beat.Pops"/>, whose number it sounds with, or null for the beat's start (a death).
    /// </summary>
    public static IReadOnlyList<(Cue Cue, int? Pop)> Cues(Beat beat)
    {
        var cues = beat.Pops.Select((pop, j) => (Of(pop.Kind), (int?)j)).ToList();
        if (beat.Fell is not null)
        {
            cues.Add((Cue.Fall, null));
        }

        return cues;
    }
}

/// <summary>
/// The samples of an uncompressed PCM WAV file (issue 516), read so the client can play a clip
/// from the source tree without Godot's import step: 8- or 16-bit, any rate, mono or stereo.
/// </summary>
public sealed record WavPcm(int Rate, int Channels, int Bits, byte[] Data)
{
    /// <summary>The clip's length in seconds.</summary>
    public double Seconds => (double)Data.Length / (Rate * Channels * (Bits / 8));

    /// <summary>
    /// Reads a RIFF WAVE file, walking its chunks for <c>fmt </c> and <c>data</c>. Throws
    /// <see cref="FormatException"/> naming what is wrong for anything else: not RIFF, compressed,
    /// a bit depth other than 8 or 16, or a missing chunk.
    /// </summary>
    public static WavPcm Parse(byte[] file)
    {
        if (file.Length < 12 || Encoding.ASCII.GetString(file, 0, 4) != "RIFF" || Encoding.ASCII.GetString(file, 8, 4) != "WAVE")
        {
            throw new FormatException("not a RIFF WAVE file");
        }

        (int Rate, int Channels, int Bits)? format = null;
        var at = 12;
        while (at + 8 <= file.Length)
        {
            var id = Encoding.ASCII.GetString(file, at, 4);
            var size = BitConverter.ToInt32(file, at + 4);
            var body = at + 8;
            if (size < 0 || body + size > file.Length)
            {
                throw new FormatException($"chunk '{id}' runs past the end of the file");
            }

            if (id == "fmt ")
            {
                var encoding = BitConverter.ToUInt16(file, body);
                if (encoding != 1)
                {
                    throw new FormatException($"compressed (format {encoding}); only PCM is read");
                }

                format = (BitConverter.ToInt32(file, body + 4), BitConverter.ToUInt16(file, body + 2), BitConverter.ToUInt16(file, body + 14));
                if (format.Value.Bits is not (8 or 16))
                {
                    throw new FormatException($"{format.Value.Bits}-bit samples; only 8 and 16 are read");
                }
            }
            else if (id == "data")
            {
                if (format is not { } f)
                {
                    throw new FormatException("data before fmt");
                }

                return new WavPcm(f.Rate, f.Channels, f.Bits, file[body..(body + size)]);
            }

            at = body + size + (size & 1);
        }

        throw new FormatException("no data chunk");
    }
}
