using Godot;
using Ironwake.Client;

namespace Ironwake.Godot;

/// <summary>
/// Showcase slice 6 (issue 516): the sound. Which cue plays when is the client's
/// (<see cref="Sound"/>): each strike's number sounds as it rises and a death as its beat starts,
/// so a skipped beat is silent and a sound never runs ahead of what the board shows. A press
/// clicks, a bed of wind loops under everything, and M mutes it all. The clips are read from
/// <c>assets/sound/</c> in the source tree, or from the imported resources in an exported build;
/// a clip that is missing or unreadable is silence, never an error.
/// </summary>
public partial class Main
{
    /// <summary>How many one-shot sounds may overlap: a double strike and its counter at once.</summary>
    private const int Voices = 6;

    private readonly Dictionary<Cue, AudioStream?> _clips = new();
    private readonly List<AudioStreamPlayer> _voices = new();
    private int _nextVoice;
    private AudioStreamPlayer? _bed;

    /// <summary>True once M has muted every sound.</summary>
    private bool _muted;

    /// <summary>The clock at the last frame's sounds, so each cue plays once, as the clock passes it.</summary>
    private float _soundClock;

    /// <summary>
    /// Sound is off for any run that saves frames or plays a script to a log: the renders and the
    /// parity runs are silent, and headless runs have no audio device to open.
    /// </summary>
    private bool SoundOff => _screenshot is not null || _stripPrefix is not null || DisplayServer.GetName() == "headless";

    /// <summary>Makes the players and starts the bed; called once the battle or title is open.</summary>
    private void StartSound()
    {
        if (SoundOff)
        {
            return;
        }

        for (var i = 0; i < Voices; i++)
        {
            var voice = new AudioStreamPlayer();
            AddChild(voice);
            _voices.Add(voice);
        }

        if (Clip(Cue.Ambient) is { } bed)
        {
            _bed = new AudioStreamPlayer { Stream = bed, VolumeDb = -14 };
            AddChild(_bed);
            // The bed fades in and out at its ends, so a restart on finish is its loop.
            _bed.Finished += () => _bed.Play();
            _bed.Play();
        }
    }

    /// <summary>Plays one cue on the next free voice, unless muted or the clip is missing.</summary>
    private void Play(Cue cue, float volumeDb = 0)
    {
        if (_muted || _voices.Count == 0 || Clip(cue) is not { } clip)
        {
            return;
        }

        var voice = _voices[_nextVoice];
        _nextVoice = (_nextVoice + 1) % _voices.Count;
        voice.Stream = clip;
        voice.VolumeDb = volumeDb;
        voice.Play();
    }

    /// <summary>A click on a drawn region: the press sounds when the region does something.</summary>
    private void PressAt(Vector2 position)
    {
        if (HitAt(position) is { } act)
        {
            Play(Cue.Click);
            act();
        }
    }

    /// <summary>M: mutes or unmutes every sound, the bed included.</summary>
    private void ToggleMute()
    {
        _muted = !_muted;
        AudioServer.SetBusMute(AudioServer.GetBusIndex("Master"), _muted);
    }

    /// <summary>
    /// Plays each cue of the beats in play whose moment the clock passed since the last frame:
    /// a strike's number when it rises, a death when its beat starts.
    /// </summary>
    private void SoundBeats()
    {
        var from = _soundClock;
        _soundClock = _clock;
        if (_voices.Count == 0 || _still)
        {
            return;
        }

        for (var i = 0; i < _playing.Count; i++)
        {
            foreach (var (cue, pop) in Sound.Cues(_playing[i]))
            {
                var at = pop is { } j ? PopTime(i, j) : _beatStarts[i];
                // A beat skipped by Space or C has its end moved to now, and plays nothing after it.
                if (at > from && at <= _clock && at <= _beatsEnd)
                {
                    Play(cue);
                }
            }
        }
    }

    /// <summary>The clip for a cue, read once: the source tree's WAV, else the exported build's imported resource, else none.</summary>
    private AudioStream? Clip(Cue cue)
    {
        if (_clips.TryGetValue(cue, out var known))
        {
            return known;
        }

        var resource = "res://assets/sound/" + Sound.File(cue);
        AudioStream? clip = null;
        var path = ProjectSettings.GlobalizePath(resource);
        if (File.Exists(path))
        {
            try
            {
                var wav = WavPcm.Parse(File.ReadAllBytes(path));
                clip = new AudioStreamWav
                {
                    Format = wav.Bits == 16 ? AudioStreamWav.FormatEnum.Format16Bits : AudioStreamWav.FormatEnum.Format8Bits,
                    MixRate = wav.Rate,
                    Stereo = wav.Channels == 2,
                    // Godot reads 8-bit samples as signed; a WAV's are unsigned.
                    Data = wav.Bits == 8 ? wav.Data.Select(b => (byte)(b ^ 0x80)).ToArray() : wav.Data,
                };
            }
            catch (FormatException e)
            {
                GD.PrintErr($"sound {Sound.File(cue)}: {e.Message}");
            }
        }

        clip ??= ResourceLoader.Exists(resource) ? GD.Load<AudioStream>(resource) : null;
        _clips[cue] = clip;
        return clip;
    }
}
