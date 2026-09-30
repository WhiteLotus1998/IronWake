using Godot;
using Ironwake.Client;
using Ironwake.Core;
using CoreSide = Ironwake.Core.Side;

namespace Ironwake.Godot;

/// <summary>
/// Showcase slice 3 (issue 513): motion over both phases. Each set of <see cref="ClientSession.Beats"/>
/// plays in order: a move walks its path, a strike lunges and every strike's number rises off its
/// target (a miss says so, a crit is louder), a death fades the token and leaves a mark on its tile
/// for the rest of the phase. The enemy phase plays itself at the speed S sets; Space jumps to the
/// next event, C to the end. Nothing here hides state: the board is always the state the protocol
/// has after the revealed events, or the one before the strike being shown, and the event log steps
/// line by line beside it. <c>--strip &lt;prefix&gt; &lt;count&gt; &lt;seconds&gt;</c> saves
/// <c>count</c> frames <c>seconds</c> apart on a fixed clock and quits.
/// </summary>
public partial class Main
{
    private static readonly (string Name, float Factor)[] Speeds = { ("slow", 1.6f), ("normal", 1f), ("fast", 0.5f) };

    /// <summary>How long a strike's number takes to rise and fade.</summary>
    private const float PopLife = 1.0f;

    /// <summary>The fixed frame step on the strip's clock.</summary>
    private const float StripStep = 1f / 30;

    private int _speed = 1;

    /// <summary>Seconds since the battle opened: real time, or the strip's fixed clock.</summary>
    private float _clock;

    private int _beatSerialSeen = -1;
    private float _beatsStart;
    private IReadOnlyList<Beat> _playing = Array.Empty<Beat>();
    private float[] _beatStarts = Array.Empty<float>();
    private IReadOnlyList<float>[] _popTimes = Array.Empty<IReadOnlyList<float>>();
    private float _beatsEnd;

    /// <summary>True for a single still frame: beats count as played and the enemy phase waits for keys.</summary>
    private bool _still;

    private string? _stripPrefix;
    private int _stripCount;
    private float _stripEvery;
    private int _stripSaved;
    private int _stripFrames;
    private float _stripStart;

    private float Factor => Speeds[_speed].Factor;

    private int _scrubSerialSeen;
    private float _scrubStart = float.NegativeInfinity;
    private IReadOnlyList<BattleState> _scrubbing = Array.Empty<BattleState>();

    /// <summary>Each step's length in the scrub playing (issue 515): a hold where a unit comes back, a blur elsewhere.</summary>
    private IReadOnlyList<float> _scrubLengths = Array.Empty<float>();

    /// <summary>The scrub's whole length.</summary>
    private float _scrubLength;

    /// <summary>Each token's offset while the scrub slides it home, in pixels from its tile in the frame drawn.</summary>
    private readonly Dictionary<string, Vector2> _scrubShift = new();

    /// <summary>Each token's scale while the scrub stands it back up (a unit dead in the newer frame, alive in the older).</summary>
    private readonly Dictionary<string, float> _scrubRise = new();

    /// <summary>Units in the newer frame and not the older (the rider not yet arrived), shrinking away through the step.</summary>
    private readonly List<(BattleUnit Unit, float Scale)> _scrubLeaving = new();

    /// <summary>The Recall to play once the enemy phase has played and held (<c>--recall-after</c>), for the strip.</summary>
    private int? _recallAfter;
    private float _phaseDoneAt = float.NaN;

    /// <summary>True while a Recall's scrub is folding the board back (issue 514).</summary>
    private bool Scrubbing => _clock < _scrubStart + _scrubLength && _scrubbing.Count > 1;

    /// <summary>
    /// The state the board draws: the client's, or during a scrub the frame the rewind has
    /// reached, with each token's slide and rise set for this frame (issue 514). The turn chip,
    /// the terrain and fired events all read from it, so they count down and fold back with it.
    /// </summary>
    private BattleState Shown()
    {
        _scrubShift.Clear();
        _scrubRise.Clear();
        _scrubLeaving.Clear();
        if (!Scrubbing)
        {
            return _client!.State;
        }

        var (step, through) = Rhythm.ScrubAt(_scrubLengths, _clock - _scrubStart);
        var from = _scrubbing[step];
        var to = _scrubbing[step + 1];
        var e = through * through * (3 - 2 * through);
        foreach (var unit in to.Units)
        {
            if (from.Find(unit.Id) is { } was)
            {
                if (was.At != unit.At)
                {
                    _scrubShift[unit.Id] = (Cell(was.At).Position - Cell(unit.At).Position) * (1 - e);
                }
            }
            else
            {
                _scrubRise[unit.Id] = Math.Max(0.05f, e);
            }
        }

        foreach (var unit in from.Units.Where(u => to.Find(u.Id) is null))
        {
            _scrubLeaving.Add((unit, 1 - e));
        }

        return to;
    }

    private bool Animating => _clock < _beatsEnd;

    /// <summary>A beat's length at the current speed, by the client's <see cref="Rhythm"/> (issue 544).</summary>
    private float BeatLength(Beat beat) => Factor * Rhythm.Length(beat);

    /// <summary>Starts playing the client's newest beats when they have changed since the last frame.</summary>
    private void SyncBeats()
    {
        if (_client is null || _client.BeatSerial == _beatSerialSeen)
        {
            return;
        }

        _beatSerialSeen = _client.BeatSerial;
        _playing = _client.Beats;
        _beatsStart = _clock;
        _beatStarts = new float[_playing.Count];
        _popTimes = _playing.Select(Rhythm.PopTimes).ToArray();
        var t = _clock;
        for (var i = 0; i < _playing.Count; i++)
        {
            _beatStarts[i] = t;
            t += BeatLength(_playing[i]);
        }

        // The act that decides the battle holds still before the end card rises (issue 578).
        _beatsEnd = _still ? _clock : _clock + Factor * Rhythm.Total(_playing, _client.Ending is not null);
        if (_still)
        {
            _beatsStart = float.NegativeInfinity;
        }
    }

    /// <summary>Ends the beats in play at once, their end state being what the board already holds.</summary>
    private void FinishBeats()
    {
        _beatsEnd = _clock;
        _beatsStart = float.NegativeInfinity;
    }

    /// <summary>
    /// Advances the clock and the enemy phase: once the beats in play have ended and the gap after
    /// them has passed, the next event is revealed. Then the strip's frames, when asked for.
    /// </summary>
    private void Advance(double delta)
    {
        _clock += _stripPrefix is not null ? StripStep : (float)delta;
        SyncBeats();

        // The strip's Recall: once the enemy phase has played out and the player's phase has held a beat.
        if (_recallAfter is { } index && _client is { EnemyPhasePlaying: false } done && !Animating)
        {
            if (float.IsNaN(_phaseDoneAt))
            {
                _phaseDoneAt = _clock;
            }
            else if (_clock - _phaseDoneAt >= 1.2f)
            {
                _recallAfter = null;
                done.Recall(index);
            }
        }

        if (_client is { } c && c.ScrubSerial != _scrubSerialSeen)
        {
            _scrubSerialSeen = c.ScrubSerial;
            _scrubbing = c.Scrub;
            _scrubLengths = c.ScrubLengths;
            _scrubLength = _scrubLengths.Sum();
            _scrubStart = _still ? float.NegativeInfinity : _clock;
        }

        if (_client is { EnemyPhasePlaying: true } client && !_still && _clock >= _beatsEnd + Rhythm.Gap * Factor * (_playing.Count == 0 ? 0.5f : 1))
        {
            client.Step();
            SyncBeats();
        }

        QueueRedraw();
    }

    /// <summary>Saves the strip's frame when its time has come; quits after the last.</summary>
    private void SaveStripFrame()
    {
        if (_stripPrefix is null)
        {
            return;
        }

        // The first frames are not yet drawn; the strip's clock starts on the first that is.
        if (++_stripFrames < 3)
        {
            _stripStart = _clock;
            return;
        }

        if (_clock - _stripStart < _stripSaved * _stripEvery)
        {
            return;
        }

        var file = $"{_stripPrefix}-{_stripSaved:D2}.png";
        if (GetViewport().GetTexture().GetImage().SavePng(file) != global::Godot.Error.Ok)
        {
            GetTree().Quit(1);
            _stripPrefix = null;
            return;
        }

        if (++_stripSaved >= _stripCount)
        {
            GetTree().Quit(0);
            _stripPrefix = null;
        }
    }

    /// <summary>Where a unit's token sits now and how far it has lunged, in pixels from its tile's own place.</summary>
    private Vector2 Shift(string id, Coord at)
    {
        if (_scrubShift.TryGetValue(id, out var sliding))
        {
            return sliding;
        }

        // A still capture shows every beat already played (issue 578): no token mid-walk or mid-lunge.
        if (_still)
        {
            return Vector2.Zero;
        }

        for (var i = 0; i < _playing.Count; i++)
        {
            var beat = _playing[i];
            if (beat.UnitId != id)
            {
                continue;
            }

            var start = _beatStarts[i];
            var length = BeatLength(beat);
            if (beat.IsMove && _clock < start + length)
            {
                // Not yet walked: it stands where the beat starts it; walking: along the path.
                var points = new List<Coord> { beat.From!.Value };
                points.AddRange(beat.Path);
                points.Add(beat.To!.Value);
                var u = Math.Clamp((_clock - start) / length, 0, 1);
                u = u * u * (3 - 2 * u);
                var f = u * (points.Count - 1);
                var k = Math.Min((int)f, points.Count - 2);
                var from = Cell(points[k]).Position;
                var to = Cell(points[k + 1]).Position;
                return from.Lerp(to, f - k) - Cell(at).Position;
            }
        }

        // A lunge: the striker leans toward the tile it strikes as each of its strikes lands.
        for (var i = 0; i < _playing.Count; i++)
        {
            var beat = _playing[i];
            if (!beat.IsStrike || beat.Struck is not { } struck || beat.To is not { } from)
            {
                continue;
            }

            for (var j = 0; j < beat.Pops.Count; j++)
            {
                var pop = beat.Pops[j];
                var striker = pop.TargetId == beat.UnitId ? struck : from;
                var target = pop.TargetId == beat.UnitId ? from : struck;
                if (striker != at)
                {
                    continue;
                }

                var step = Factor * Rhythm.StrikeStep(pop);
                var t0 = PopTime(i, j) - Factor * Rhythm.Lead;
                var u = (_clock - t0) / step;
                if (u is > 0 and < 1)
                {
                    var dir = new Vector2(target.X - striker.X, target.Y - striker.Y).Normalized();
                    return dir * _tile * 0.3f * Mathf.Sin(Mathf.Pi * Math.Min(1, u * 1.6f));
                }
            }
        }

        return Vector2.Zero;
    }

    /// <summary>The HP a token shows: the state's, or before the strike on show until that strike lands.</summary>
    private int ShownHp(string id, int hp)
    {
        for (var i = _playing.Count - 1; i >= 0; i--)
        {
            var beat = _playing[i];
            if (!beat.HpBefore.TryGetValue(id, out var before) || _clock >= _beatStarts[i] + BeatLength(beat))
            {
                continue;
            }

            var shown = before;
            for (var j = 0; j < beat.Pops.Count; j++)
            {
                if (beat.Pops[j].TargetId == id && _clock >= PopTime(i, j))
                {
                    shown = beat.Pops[j].TargetHpAfter;
                }
            }

            return shown;
        }

        return hp;
    }

    /// <summary>When strike <paramref name="j"/> of beat <paramref name="i"/> lands: at the height of its lunge, on the client's rhythm.</summary>
    private float PopTime(int i, int j) => _beatStarts[i] + Factor * _popTimes[i][j];

    /// <summary>
    /// The tokens: every unit on the board where its beat puts it, then the units killed and not
    /// yet read out, whole until their death's beat and fading through it.
    /// </summary>
    private void DrawTokens(BattleState state)
    {
        var fading = new HashSet<string>();
        for (var i = 0; i < _playing.Count; i++)
        {
            if (_playing[i].Fell is { } fell && _clock < _beatStarts[i] + BeatLength(_playing[i]))
            {
                fading.Add(fell.Unit.Id);
            }
        }

        foreach (var mark in _client!.Fallen.Where(f => !fading.Contains(f.Unit.Id)))
        {
            DrawFallenMark(mark);
        }

        foreach (var unit in state.Units)
        {
            DrawTokenMoved(state, unit with { Hp = ShownHp(unit.Id, unit.Hp) }, _scrubRise.GetValueOrDefault(unit.Id, 1));
        }

        foreach (var (leaving, scale) in _scrubLeaving.Where(l => l.Scale > 0.05f))
        {
            DrawTokenMoved(state, leaving, scale);
        }

        foreach (var ghost in _client.Ghosts)
        {
            DrawTokenMoved(state, ghost with { Hp = ShownHp(ghost.Id, 0) }, 1);
        }

        for (var i = 0; i < _playing.Count; i++)
        {
            if (_playing[i].Fell is not { } fell || !fading.Contains(fell.Unit.Id))
            {
                continue;
            }

            // The token shrinks into its mark over the fade; the hold after it is the mark alone.
            var u = Math.Clamp((_clock - _beatStarts[i]) / (Factor * Rhythm.Fade), 0, 1);
            if (u >= 1)
            {
                DrawFallenMark(fell);
                continue;
            }

            if (u <= 0)
            {
                DrawTokenMoved(state, fell.Unit with { Hp = ShownHp(fell.Unit.Id, 0) }, 1);
                continue;
            }

            DrawFallenMark(fell);
            DrawTokenMoved(state, fell.Unit with { Hp = 0 }, 1 - u);
        }
    }

    /// <summary>
    /// A token shifted by its beat and scaled by <paramref name="scale"/> about its centre, then
    /// sunk toward the tile under it as the scale falls, which is how a death fades.
    /// </summary>
    private void DrawTokenMoved(BattleState state, BattleUnit unit, float scale)
    {
        var centre = Cell(unit.At).Position + new Vector2(_tile / 2f, 19 * S);
        var shift = Shift(unit.Id, unit.At);
        _tokenFrame = new Transform2D(0, new Vector2(scale, scale), 0, centre + shift - scale * centre);
        DrawSetTransformMatrix(_tokenFrame);
        DrawToken(state, unit);
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        _tokenFrame = Transform2D.Identity;
    }

    /// <summary>The transform a token is being drawn under, so its own shadow ellipse can compose with it.</summary>
    private Transform2D _tokenFrame = Transform2D.Identity;

    /// <summary>
    /// A fallen unit's mark (round 141, issue 544): its disc greyed to the lost colour inside its
    /// side's ring, so a player reads whose it was, its silhouette dimmed under a full cross in
    /// ink edged with the text colour, which cannot merge with the glyph or read as one slash.
    /// </summary>
    private void DrawFallenMark(FallenMark mark)
    {
        var centre = Cell(mark.At).Position + new Vector2(_tile / 2f, 19 * S);
        var radius = 12 * S;
        var side = mark.Unit.Side == CoreSide.Player ? Look(LookPalette.Player) : Look(LookPalette.EnemyBone);
        DrawCircle(centre, radius, UiColour("lost", 0.85f));
        DrawArc(centre, radius, 0, Mathf.Tau, 32, side, 2.5f * S, antialiased: true);
        DrawSilhouette(_client!.State, mark.Unit, centre, radius / 16f, UiColour("ink", 0.35f));
        var d = radius * 0.62f;
        foreach (var (a, b) in new[] { (new Vector2(-d, -d), new Vector2(d, d)), (new Vector2(-d, d), new Vector2(d, -d)) })
        {
            DrawLine(centre + a, centre + b, UiColour("ink"), 4.5f * S, antialiased: true);
        }

        foreach (var (a, b) in new[] { (new Vector2(-d, -d), new Vector2(d, d)), (new Vector2(-d, d), new Vector2(d, -d)) })
        {
            DrawLine(centre + a, centre + b, UiColour("text"), 2 * S, antialiased: true);
        }
    }

    /// <summary>
    /// The numbers in the air: each strike's rises off its target from the moment it lands and
    /// fades as it climbs; a crit is larger with its word above it, a miss is smaller and muted.
    /// </summary>
    private void DrawPops()
    {
        for (var i = 0; i < _playing.Count; i++)
        {
            for (var j = 0; j < _playing[i].Pops.Count; j++)
            {
                var beat = _playing[i];
                var pop = beat.Pops[j];
                var age = _clock - PopTime(i, j);
                var life = PopLife * Math.Max(Factor, 0.7f);

                // A number clears once the next strike on the other side lands (issue 544), so a
                // counter's number never stacks on the first one's.
                if (j + 1 < beat.Pops.Count && beat.Pops[j + 1].TargetId != pop.TargetId)
                {
                    life = Math.Min(life, PopTime(i, j + 1) - PopTime(i, j));
                }

                if (pop.Lethal)
                {
                    life = Math.Max(life, BeatLength(beat) - (PopTime(i, j) - _beatStarts[i]) + Rhythm.Gap * Factor);
                }

                if (age < 0 || age > life)
                {
                    continue;
                }

                var u = age / life;
                var alpha = u < 0.6f ? 1 : 1 - (u - 0.6f) / 0.4f;
                var rise = _tile * (0.25f + 0.4f * (1 - (1 - u) * (1 - u)));

                // It rises on the far side of its target from the striker, never into the striker's tile.
                var striker = pop.TargetId == beat.UnitId ? beat.Struck : beat.To;
                var away = striker is { } s0 && s0.X != pop.At.X ? Math.Sign(pop.At.X - s0.X) : pop.TargetId == beat.UnitId ? -1 : 1;
                var at = Cell(pop.At).Position + new Vector2(_tile / 2f + away * _tile * 0.3f, 14 * S - rise + _tile * 0.25f);
                if (pop.Lethal)
                {
                    // The killing number (issue 514): glyph-sized and outlined, it holds at its
                    // height until the death beat takes it over.
                    DrawLethal(pop, Math.Min(1, age / (0.15f * Factor)), 1);
                    continue;
                }

                var (size, colour) = pop.Kind switch
                {
                    PopKind.Crit => ((int)(30 * S), MarkColour("struck", alpha)),
                    PopKind.Miss => ((int)(15 * S), UiColour("text", alpha * 0.85f)),
                    _ => ((int)(22 * S), MarkColour("struck", alpha)),
                };
                if (pop.Kind == PopKind.Crit)
                {
                    Outlined(at + new Vector2(0, -size * 0.9f), "CRIT", (int)(11 * S), Look(LookPalette.Player, alpha), alpha);
                }

                Outlined(at, pop.Text, size, colour, alpha);
            }
        }
    }

    /// <summary>
    /// The lethal number held over the death beat (issue 514): drawn where the strike left it
    /// through the fade and the hold, fading only over the hold's last fifth.
    /// </summary>
    private void DrawHeld()
    {
        for (var i = 0; i < _playing.Count; i++)
        {
            if (_playing[i].Held is not { } pop)
            {
                continue;
            }

            var length = BeatLength(_playing[i]);
            var age = _clock - _beatStarts[i];
            if (age < 0 || age > length)
            {
                continue;
            }

            var tail = 0.2f * length;
            DrawLethal(pop, 1, age < length - tail ? 1 : (length - age) / tail);
        }
    }

    /// <summary>
    /// A killing number: at least the unit glyph's size (the token's 24 px disc), in the struck
    /// colour with a heavy ink edge, set above its target's tile where every number rises to.
    /// <paramref name="grow"/> eases it in over its first instant.
    /// </summary>
    private void DrawLethal(Pop pop, float grow, float alpha)
    {
        var size = (int)(34 * S * (0.7f + 0.3f * grow));
        var at = Cell(pop.At).Position + new Vector2(_tile / 2f, 19 * S - _tile * 0.55f);
        var ink = UiColour("ink", alpha);
        var e = Math.Max(2, size / 9f);
        for (var k = 0; k < 8; k++)
        {
            var d = new Vector2(Mathf.Cos(k * Mathf.Pi / 4), Mathf.Sin(k * Mathf.Pi / 4)) * e;
            UiText(at + d, pop.Text, ink, size, bold: true, centred: true);
        }

        UiText(at, pop.Text, MarkColour("struck", alpha), size, bold: true, centred: true);
    }

    /// <summary>
    /// How strongly the RECALL chip pulses now (issue 514, round 152): 0 unless a death beat that
    /// pulses is on its hold, then a swell from the hold's start that fades out over a second.
    /// </summary>
    private float PulseNow()
    {
        for (var i = 0; i < _playing.Count; i++)
        {
            if (!_playing[i].Pulse)
            {
                continue;
            }

            var age = _clock - (_beatStarts[i] + Factor * Rhythm.PulseStart);
            if (age >= 0 && age < 1.6f)
            {
                return (0.5f + 0.5f * Mathf.Sin(age * Mathf.Tau * 1.25f - Mathf.Pi / 2)) * (age < 1.0f ? 1 : (1.6f - age) / 0.6f);
            }
        }

        return 0;
    }

    /// <summary>Bold UI text centred on <paramref name="at"/> with a four-way ink edge so it reads over any tile.</summary>
    private void Outlined(Vector2 at, string text, int size, Color colour, float alpha)
    {
        var ink = UiColour("ink", alpha * 0.9f);
        var e = Math.Max(1, size / 12f);
        foreach (var d in new[] { new Vector2(-e, 0), new Vector2(e, 0), new Vector2(0, -e), new Vector2(0, e), new Vector2(e, e) })
        {
            UiText(at + d, text, ink, size, bold: true, centred: true);
        }

        UiText(at, text, colour, size, bold: true, centred: true);
    }

    /// <summary>
    /// The enemy phase's header (round 141): a chip in the top bar's language with the speed and
    /// the keys beside it, where the mono line was.
    /// </summary>
    private float DrawEnemyHeader(float y)
    {
        var top = y - LineHeight + 2;
        var label = "ENEMY PHASE";
        var width = _caps.GetStringSize(label, fontSize: 11).X + 28;
        Card(new Rect2(PanelOrigin.X - 4, top, width, 26), Box, 13);
        DrawString(_caps, new Vector2(PanelOrigin.X + 10, top + 17), label, fontSize: 11, modulate: EnemyMark);
        UiText(new Vector2(PanelOrigin.X + width + 8, top + 18), $"speed {Speeds[_speed].Name} (S)  |  Space next  |  C to the end", Muted, 12);
        return top + 26 + LineHeight + 6;
    }

    /// <summary>
    /// The enemy-act card (round 141), in the forecast's slot and language read after the fact:
    /// who acted and what it did; for a strike, both sides with their HP bars from before to after
    /// (what the act cost hatched) and each strike's result in order. Returns the card's bottom.
    /// </summary>
    private float DrawActCard(float top, ActCard act)
    {
        var x0 = PanelOrigin.X;
        Vector2 P(float x, float y) => new(x0 + x, top + y);
        var fought = act.Attacker is not null && act.Defender is not null;
        var height = fought ? 170f : 96f;
        Card(new Rect2(x0 - 12, top, PanelWidth + 24, height), Box, 10);
        DrawString(_caps, P(8, 24), "ENEMY ACT", fontSize: 11, modulate: EnemyMark);
        DrawUnitDisc(P(34, 62), 20, UnitById(act.ActorId));
        // A killing act leads with the death, in the fallen unit's name and the struck colour (issue 544).
        UiText(P(66, 58), act.Headline, act.Fallen is null ? Ink : MarkColour("struck"), act.Fallen is null ? 18 : 22, bold: true);
        UiText(P(66, 78), act.Subtitle, Muted, 13);
        if (!fought)
        {
            return top + height;
        }

        var barWidth = (PanelWidth - 36) / 2f;
        ActSideRow(P(8, 104), barWidth, act.Attacker!, alignRight: false);
        ActSideRow(P(PanelWidth - 8 - barWidth, 104), barWidth, act.Defender!, alignRight: true);
        return top + height;
    }

    private void ActSideRow(Vector2 at, float width, ActSide side, bool alignRight)
    {
        var player = UnitById(side.Id)?.Side == CoreSide.Player;
        var colour = player ? Look(LookPalette.Player) : Look(LookPalette.EnemyBone);
        var edge = alignRight ? at.X + width : at.X;
        void Put(float y, string text, Color c, int size, bool bold = false)
        {
            if (alignRight)
            {
                RightText(new Vector2(edge, y), text, c, size, bold);
            }
            else
            {
                UiText(new Vector2(edge, y), text, c, size, bold);
            }
        }

        Put(at.Y, side.Name, Ink, 13, bold: true);
        HpBar(new Rect2(at + new Vector2(0, 8), new Vector2(width, 12)), side.HpBefore, side.HpAfter, side.MaxHp, colour);
        Put(at.Y + 40, $"{side.HpBefore} → {side.HpAfter}" + (side.HpAfter == 0 ? "  falls" : ""), Ink, 15, bold: true);
        Put(at.Y + 60, side.Strikes.Count == 0 ? "no strike" : "struck: " + string.Join("  ", side.Strikes), Muted, 12);
    }

    /// <summary>A unit by id wherever the client still holds it: on the board, killed and not yet read out, or fallen this phase.</summary>
    private BattleUnit? UnitById(string id) =>
        _client!.State.Find(id) ?? _client.Ghosts.FirstOrDefault(g => g.Id == id) ?? _client.Fallen.FirstOrDefault(f => f.Unit.Id == id)?.Unit;
}
