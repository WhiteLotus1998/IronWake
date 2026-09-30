using System.Text;
using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Client;

/// <summary>
/// Showcase slice 6 (issue 516): the sound set. Each cue has its file under the Godot project's
/// <c>assets/sound/</c>, readable as PCM and named in <c>LICENSES</c>; the beats say which cue
/// plays when; the WAV reader refuses what it cannot play.
/// </summary>
public class ClientSoundTests
{
    private static string Root() => Path.GetFullPath(Path.Combine(Fixture.RealContentDirectory(), ".."));

    private static string ClipPath(Cue cue) => Path.Combine(Root(), "src", "Ironwake.Godot", "assets", "sound", Sound.File(cue));

    [Fact]
    public void EveryCueHasAClipThatReadsAsPcm()
    {
        Assert.Equal(6, Sound.All.Count);
        foreach (var cue in Sound.All)
        {
            var wav = WavPcm.Parse(File.ReadAllBytes(ClipPath(cue)));
            Assert.Equal(16, wav.Bits);
            Assert.InRange(wav.Seconds, 0.02, 20);
        }
    }

    [Fact]
    public void EveryClipIsNamedInLicensesWithItsLicence()
    {
        var licences = File.ReadAllText(Path.Combine(Root(), "LICENSES"));
        var sound = licences[licences.IndexOf("Sound", StringComparison.Ordinal)..];
        Assert.Contains("CC0", sound, StringComparison.Ordinal);
        foreach (var cue in Sound.All)
        {
            Assert.Contains(Sound.File(cue), sound, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AStrikeSoundsItsKindOnEachNumberAndADeathOnItsBeat()
    {
        var content = ContentLoader.Load(Fixture.RealContentDirectory());
        var map = MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "maps", "the_tollgate.map"), content);
        var unit = BattleState.From(map, content, content.Cast, 1).Units[0];
        var none = new Dictionary<string, int>();
        var pops = new[]
        {
            new Pop(unit.At, "7", PopKind.Damage, unit.Id, 10),
            new Pop(unit.At, "miss", PopKind.Miss, "x", 5),
            new Pop(unit.At, "21", PopKind.Crit, unit.Id, 0),
        };
        var strike = new Beat(unit.Id, null, Array.Empty<Coord>(), unit.At, unit.At, pops, none, null);
        Assert.Equal(new (Cue, int?)[] { (Cue.Hit, 0), (Cue.Miss, 1), (Cue.Crit, 2) }, Sound.Cues(strike));

        var death = new Beat(unit.Id, null, Array.Empty<Coord>(), unit.At, null, Array.Empty<Pop>(), none, new FallenMark(unit, unit.At));
        Assert.Equal(new (Cue, int?)[] { (Cue.Fall, null) }, Sound.Cues(death));

        var walk = new Beat(unit.Id, unit.At, Array.Empty<Coord>(), unit.At, null, Array.Empty<Pop>(), none, null);
        Assert.Empty(Sound.Cues(walk));
    }

    private static byte[] Wav(ushort encoding, ushort bits, bool withData = true)
    {
        var data = new byte[] { 1, 2, 3, 4 };
        var fmt = new List<byte>();
        fmt.AddRange(Encoding.ASCII.GetBytes("fmt "));
        fmt.AddRange(BitConverter.GetBytes(16));
        fmt.AddRange(BitConverter.GetBytes(encoding));
        fmt.AddRange(BitConverter.GetBytes((ushort)1));
        fmt.AddRange(BitConverter.GetBytes(22050));
        fmt.AddRange(BitConverter.GetBytes(22050 * bits / 8));
        fmt.AddRange(BitConverter.GetBytes((ushort)(bits / 8)));
        fmt.AddRange(BitConverter.GetBytes(bits));
        var body = new List<byte>(Encoding.ASCII.GetBytes("WAVE"));
        body.AddRange(Encoding.ASCII.GetBytes("LIST"));
        body.AddRange(BitConverter.GetBytes(3));
        body.AddRange(new byte[] { 9, 9, 9, 0 });
        body.AddRange(fmt);
        if (withData)
        {
            body.AddRange(Encoding.ASCII.GetBytes("data"));
            body.AddRange(BitConverter.GetBytes(data.Length));
            body.AddRange(data);
        }

        var file = new List<byte>(Encoding.ASCII.GetBytes("RIFF"));
        file.AddRange(BitConverter.GetBytes(body.Count));
        file.AddRange(body);
        return file.ToArray();
    }

    [Fact]
    public void TheWavReaderSkipsOtherChunksIncludingAnOddSizedOne()
    {
        var wav = WavPcm.Parse(Wav(1, 16));
        Assert.Equal((22050, 1, 16), (wav.Rate, wav.Channels, wav.Bits));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, wav.Data);
    }

    [Theory]
    [InlineData(2, 16, true, "compressed")]
    [InlineData(1, 24, true, "24-bit")]
    [InlineData(1, 16, false, "no data chunk")]
    public void TheWavReaderRefusesWhatItCannotPlay(int encoding, int bits, bool withData, string why)
    {
        var e = Assert.Throws<FormatException>(() => WavPcm.Parse(Wav((ushort)encoding, (ushort)bits, withData)));
        Assert.Contains(why, e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheWavReaderRefusesAFileThatIsNotRiff()
    {
        var e = Assert.Throws<FormatException>(() => WavPcm.Parse(Encoding.ASCII.GetBytes("OggS and more bytes")));
        Assert.Contains("not a RIFF WAVE", e.Message, StringComparison.Ordinal);
    }
}
