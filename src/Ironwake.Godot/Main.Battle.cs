using Godot;
using Ironwake.Client;
using Ironwake.Core;
using CoreSide = Ironwake.Core.Side;

namespace Ironwake.Godot;

/// <summary>
/// The battle scene (issue 535, slice 1): a combat the setting picks plays over the board as
/// both combatants' clips, read from the sheets at ART_SPEC's names (mirrored for the right side;
/// a generated sheet tinted with the side's colour, 0105, a delivered one in its own, 0223), on a
/// backdrop banded from each side's ground. Hit-stop holds the contact frame, the number rises
/// there, the effects lay on the struck body, and the frame shakes by the scene's own measure. The
/// scene is the strike beat itself, timed by <see cref="BattleScene"/>, so the board's HP under it
/// keeps time, and Space or C ends it on the state the protocol already has. After a combat that
/// levels, the level-up card shows whatever the setting. B steps the setting; its chip sits at the top bar's end.
/// </summary>
public partial class Main
{
    /// <summary>
    /// The scene's frame on the 1280x720 window. The scene is art drawn at its own pixel size, so
    /// it keeps this frame at every UI scale (issue 698): at 150 the canvas is 853 wide, too narrow
    /// for it, and the scene draws at full size (<see cref="AtFullSize"/>) instead.
    /// </summary>
    private static readonly Rect2 SceneFrame = new(190, 120, 900, 430);

    /// <summary>How far the ground line sits above the frame's foot.</summary>
    private const float SceneGround = 92;

    /// <summary>How long a number rises off a struck body in the scene.</summary>
    private const float ScenePopLife = 0.8f;

    /// <summary>How far above the feet a body's centre sits on a clip: the effect pivot's 128 under the clip pivot's 232.</summary>
    private const float BodyCentre = 104;

    private readonly Dictionary<string, Texture2D?> _sheets = new();

    private IReadOnlySet<string>? _generatedArt;
    private bool _generatedArtRead;

    /// <summary>
    /// The names on <c>assets/art/generated.txt</c>, read once: the source tree's file, else the
    /// exported build's; null when neither is there, and then every sheet tints as before (issue 896).
    /// </summary>
    private IReadOnlySet<string>? GeneratedArt()
    {
        if (!_generatedArtRead)
        {
            _generatedArtRead = true;
            const string resource = "res://assets/art/generated.txt";
            var path = ProjectSettings.GlobalizePath(resource);
            if (File.Exists(path))
            {
                _generatedArt = ArtSpec.GeneratedNames(File.ReadAllLines(path));
            }
            else if (global::Godot.FileAccess.FileExists(resource) && global::Godot.FileAccess.Open(resource, global::Godot.FileAccess.ModeFlags.Read) is { } file)
            {
                using (file)
                {
                    _generatedArt = ArtSpec.GeneratedNames(file.GetAsText().Split('\n'));
                }
            }
        }

        return _generatedArt;
    }

    /// <summary>A whole sheet at its own size, loaded once, or null when no file is on disk.</summary>
    private Texture2D? Sheet(string name)
    {
        if (_sheets.TryGetValue(name, out var known))
        {
            return known;
        }

        var texture = ArtImage(name) is { } image ? ImageTexture.CreateFromImage(image) : null;
        _sheets[name] = texture;
        return texture;
    }

    /// <summary>The setting's chip at the top bar's right end: the B key and the setting in force; returns its left edge.</summary>
    private float DrawSceneChip(float down)
    {
        var value = Scenes.Label(_client!.SceneSetting)["scenes: ".Length..];
        var label = "SCENES  B";
        var width = UiWidth(label, 10, bold: true) + UiWidth(value, 14, bold: true) + 36;
        Chip(ViewWidth - Margin - width, label, value, Ink, down);
        return ViewWidth - Margin - width;
    }

    /// <summary>The beat whose scene or level-up card is on show now, with the seconds into it at normal speed.</summary>
    private (Beat Beat, float At)? SceneShown()
    {
        if (_still)
        {
            return null;
        }

        for (var i = 0; i < _playing.Count; i++)
        {
            var beat = _playing[i];
            if (beat.Scene is null && beat.LevelUp is null)
            {
                continue;
            }

            var at = (_clock - _beatStarts[i]) / Factor;
            if (at >= 0 && at < Rhythm.Length(beat))
            {
                return (beat, at);
            }
        }

        return null;
    }

    /// <summary>The scene and the level-up card over the board, when a beat on show carries either.</summary>
    private void DrawBattleScene()
    {
        if (SceneShown() is not var (beat, at))
        {
            return;
        }

        if (beat.Scene is { } scene && at < scene.Length)
        {
            AtFullSize(() => DrawScene(scene, at));
        }
        else if (beat.LevelUp is { } card && at >= beat.LevelUpAt)
        {
            DrawLevelUp(card);
        }
    }

    private void DrawScene(BattleScene scene, float t)
    {
        DrawRect(new Rect2(Vector2.Zero, new Vector2(UiLayout.WindowWidth, UiLayout.WindowHeight)), UiColour("ink", 0.72f));
        var shake = Vector2.Zero;
        foreach (var s in scene.Strikes.Where(s => s.Shake > 0 && t >= s.Contact && t < s.Contact + 0.25f))
        {
            var fade = 1 - (t - s.Contact) / 0.25f;
            shake = new Vector2(Mathf.Sin(t * 90) * 14 * s.Shake * fade, Mathf.Cos(t * 70) * 6 * s.Shake * fade);
        }

        var frame = new Rect2(SceneFrame.Position + shake, SceneFrame.Size);
        DrawBackdrop(frame, scene, t);

        var left = scene.Attacker.Left ? scene.Attacker : scene.Defender;
        var right = scene.Attacker.Left ? scene.Defender : scene.Attacker;
        var ground = frame.End.Y - SceneGround;
        var feet = new Dictionary<string, Vector2>
        {
            [left.Id] = new(frame.Position.X + frame.Size.X * 0.3f, ground),
            [right.Id] = new(frame.Position.X + frame.Size.X * 0.7f, ground),
        };

        foreach (var side in new[] { left, right })
        {
            var (clip, index, alpha) = Pose(scene, side, t);
            DrawCombatant(side, clip, index, feet[side.Id], alpha);
        }

        foreach (var s in scene.Strikes.Where(s => t >= s.Contact))
        {
            var body = feet[s.TargetId] - new Vector2(0, BodyCentre);
            foreach (var name in s.Effects)
            {
                var frames = ArtSpec.Effects(_client!.Content).FirstOrDefault(e => e.Name == name)?.Frames ?? 6;
                var index = (int)((t - s.Contact) * BattleScene.Fps);
                if (index < frames)
                {
                    DrawSheetFrame($"fx_{name}", index, body, new Vector2(128, 128), Colors.White, mirrored: false);
                }
            }

            var life = (t - s.Contact) / ScenePopLife;
            if (life < 1)
            {
                var size = s.Kind == PopKind.Crit ? 40 : s.Kind == PopKind.Miss ? 22 : 30;
                var colour = s.Kind == PopKind.Miss ? Muted : s.TargetHpAfter == 0 || s.Kind == PopKind.Crit ? MarkColour("struck") : Ink;
                var rise = feet[s.TargetId] - new Vector2(0, 236 + 40 * life);
                UiText(rise, s.Kind == PopKind.Crit ? $"crit {s.Text}" : s.Text, new Color(colour, 1 - life * life), size, bold: true, centred: true);
            }
        }

        DrawSceneBars(frame, scene, left, right, t);
    }

    /// <summary>
    /// The backdrop (slice 2, <see cref="SceneBackdrop"/>): the sky in the panel's value, then on
    /// each half its ground's three parallax layers drifting at their own speeds (a far ridge in
    /// the ground's colour darkened, the tile's own detail, a near band with tufts), fog banks
    /// over forest and water, the dusk laid over both, an enemy half's lamps lit through it, and
    /// embers rising where fire burns near. Each half is its own, so where each side stands
    /// reads at a glance.
    /// </summary>
    private void DrawBackdrop(Rect2 frame, BattleScene scene, float t)
    {
        Card(frame.Grow(4), UiColour("ink"), 10);
        DrawRect(frame, Box);
        var backdrop = scene.Backdrop;
        var horizon = frame.End.Y - SceneGround - 70;
        var half = frame.Size.X / 2;
        var speeds = SceneBackdrop.Layers.ToDictionary(l => l.Name, l => l.Speed);
        foreach (var (side, x) in new[] { (backdrop.Left, frame.Position.X), (backdrop.Right, frame.Position.X + half) })
        {
            var ground = RegionColour(side.Terrain == "fire" ? "forest" : side.Terrain);

            // The far ridge: a low silhouette in the ground's colour, darkened, drifting slowest.
            var ridge = new List<Vector2> { new(x, horizon) };
            for (var px = 0f; px <= half; px += 12)
            {
                var u = (px + t * speeds["ridge"] + x) / 90f;
                ridge.Add(new Vector2(x + px, horizon - 26 - 14 * Mathf.Sin(u) - 8 * Mathf.Sin(u * 2.3f + 1)));
            }

            ridge.Add(new Vector2(x + half, horizon));
            DrawColoredPolygon(ridge.ToArray(), ground.Darkened(0.35f));

            // The ground's own detail, drifting at the middle speed.
            var band = new Rect2(x, horizon, half, frame.End.Y - horizon);
            DrawRect(band, ground);
            if (Art($"tile_{side.Terrain}", 70) is { } tile)
            {
                var shift = (t * speeds["detail"]) % 70;
                for (var tx = x - shift; tx < x + half; tx += 70)
                {
                    var from = Math.Max(tx, x);
                    var w = Math.Min(tx + 70, x + half) - from;
                    if (w > 0)
                    {
                        DrawTextureRectRegion(tile, new Rect2(from, horizon, w, 70), new Rect2(from - tx, 0, w, 70), new Color(1, 1, 1, 0.55f));
                    }
                }
            }

            // The near band, with tufts drifting fastest.
            var nearTop = frame.End.Y - SceneGround + 6;
            DrawRect(new Rect2(x, nearTop, half, SceneGround - 6), UiColour("ink", 0.28f));
            var tuftShift = (t * speeds["near"]) % 37;
            for (var tx = x - tuftShift; tx < x + half; tx += 37)
            {
                if (tx >= x + 2 && tx <= x + half - 2)
                {
                    DrawLine(new Vector2(tx, nearTop + 14), new Vector2(tx + 3, nearTop + 6), UiColour("ink", 0.35f), 2);
                }
            }

            // Fog: two banks over the ground, breathing slowly.
            if (side.Fog > 0)
            {
                for (var k = 0; k < 2; k++)
                {
                    var y = horizon - 10 + 34 * k;
                    var a = side.Fog * (0.7f + 0.3f * Mathf.Sin(t * 0.9f + k * 2));
                    DrawRect(new Rect2(x, y, half, 26), new Color(Look(LookPalette.EnemyBone), a * 0.5f));
                }
            }
        }

        DrawLine(new Vector2(frame.Position.X, horizon), new Vector2(frame.End.X, horizon), UiColour("ink", 0.5f), 2);

        // Dusk over both halves, then lamps lit through it: the enemy's light, in bone.
        if (backdrop.Dark > 0)
        {
            DrawRect(frame, UiColour("ink", backdrop.Dark));
        }

        foreach (var (side, x) in new[] { (backdrop.Left, frame.Position.X), (backdrop.Right, frame.Position.X + half) })
        {
            if (side.Lamps)
            {
                foreach (var lx in new[] { 0.18f, 0.82f })
                {
                    var at = new Vector2(x + half * lx, horizon - 40);
                    var glow = 0.18f + 0.06f * Mathf.Sin(t * 5 + lx * 9);
                    DrawCircle(at, 26, new Color(Look(LookPalette.EnemyBone), glow));
                    DrawLine(at + new Vector2(0, 8), new Vector2(at.X, horizon + 30), UiColour("ink", 0.8f), 2);
                    DrawRect(new Rect2(at - new Vector2(5, 7), new Vector2(10, 14)), Look(LookPalette.EnemyBone));
                }
            }

            // Embers: fire's colour lifted toward bone, small, rising and fading; sparks, never a fill.
            var height = frame.End.Y - frame.Position.Y;
            foreach (var ember in side.Embers)
            {
                var rise = (ember.Phase + t * ember.Speed) % 1f;
                var ex = x + half * ember.X + 6 * Mathf.Sin(t * 3 + ember.X * 20);
                var ey = frame.End.Y - SceneGround - rise * (height - SceneGround);
                DrawRect(new Rect2(ex, ey, 3, 3), new Color(TerrainColour("fire").Lerp(Look(LookPalette.EnemyBone), 0.3f), 1 - rise));
            }
        }
    }

    /// <summary>
    /// Which clip and frame <paramref name="side"/> shows <paramref name="t"/> seconds in: the
    /// attacker's advance before the first strike; the striker's clip through its strike, its
    /// contact frame held through the hit-stop; the struck unit's reaction from contact; idle
    /// otherwise; and a fallen unit's last fall frame, fading after the fall.
    /// </summary>
    private static (string Clip, int Index, float Alpha) Pose(BattleScene scene, SceneSide side, float t)
    {
        static int Frames(string clip) => ArtSpec.Clips.First(c => c.Name == clip).Frames;
        int Idle() => (int)(t * BattleScene.Fps) % Frames("idle");

        if (scene.FallenId == side.Id)
        {
            var fall = Frames("fall") / BattleScene.Fps;
            if (t >= scene.FallStart)
            {
                var into = t - scene.FallStart;
                var alpha = into < fall ? 1 : Math.Max(0, 1 - (into - fall) / Math.Max(0.01f, scene.Length - scene.FallStart - fall));
                return ("fall", Math.Min(Frames("fall") - 1, (int)(into * BattleScene.Fps)), alpha);
            }
        }

        if (scene.Strikes.Count > 0 && t < scene.Strikes[0].Start)
        {
            return side.Id == scene.Attacker.Id ? ("advance", Math.Min(Frames("advance") - 1, (int)(t * BattleScene.Fps)), 1) : ("idle", Idle(), 1);
        }

        foreach (var s in scene.Strikes)
        {
            if (t < s.Start || t >= s.End)
            {
                continue;
            }

            if (s.AttackerId == side.Id)
            {
                var contact = s.Contact - s.Start;
                var stop = s.End - s.Start - Frames(s.AttackerClip) / BattleScene.Fps;
                var into = t - s.Start;
                var shown = into < contact ? into : into < contact + stop ? contact : into - stop;
                return (s.AttackerClip, Math.Min(Frames(s.AttackerClip) - 1, (int)(shown * BattleScene.Fps)), 1);
            }

            if (s.TargetId == side.Id && t >= s.Contact)
            {
                var index = (int)((t - s.Contact) * BattleScene.Fps);
                if (index < Frames(s.TargetClip))
                {
                    return (s.TargetClip, index, 1);
                }
            }
        }

        return ("idle", Idle(), 1);
    }

    /// <summary>
    /// One combatant's frame with its feet at <paramref name="feet"/>: the first of its sheets on
    /// disk, mirrored on the right, tinted with its side's colour when the sheet is a generated one
    /// and drawn in its own colour when it was delivered (0223); a disc and body in the side's
    /// colour when no sheet is.
    /// </summary>
    private void DrawCombatant(SceneSide side, string clip, int index, Vector2 feet, float alpha)
    {
        var tint = side.Side == CoreSide.Player ? Look(LookPalette.Player) : Look(LookPalette.Enemy).Lerp(Look(LookPalette.EnemyBone), 0.45f);
        tint.A = alpha;
        foreach (var name in side.ClipFiles(clip))
        {
            var modulate = ArtSpec.Tinted(name, GeneratedArt()) ? tint : new Color(1, 1, 1, alpha);
            if (DrawSheetFrame(name, index, feet, new Vector2(128, 232), modulate, mirrored: !side.Left))
            {
                return;
            }
        }

        DrawRect(new Rect2(feet - new Vector2(22, 120), new Vector2(44, 120)), tint);
        DrawCircle(feet - new Vector2(0, 140), 20, tint);
    }

    /// <summary>
    /// Frame <paramref name="index"/> of a one-row sheet of 256-pixel frames, its pivot at
    /// <paramref name="at"/>, mirrored about the pivot when asked; false when the sheet is not on disk.
    /// </summary>
    private bool DrawSheetFrame(string name, int index, Vector2 at, Vector2 pivot, Color modulate, bool mirrored)
    {
        if (Sheet(name) is not { } sheet)
        {
            return false;
        }

        var size = ArtSpec.ClipFrame;
        var count = Math.Max(1, sheet.GetWidth() / size);
        var source = new Rect2(Math.Clamp(index, 0, count - 1) * size, 0, size, size);
        DrawSetTransformMatrix(_frame * new Transform2D(0, new Vector2(mirrored ? -1 : 1, 1), 0, at));
        DrawTextureRectRegion(sheet, new Rect2(-pivot, new Vector2(size, size)), source, modulate);
        DrawSetTransformMatrix(_frame);
        return true;
    }

    /// <summary>Each side's name and HP bar under its half of the frame, the HP stepping down on each contact.</summary>
    private void DrawSceneBars(Rect2 frame, BattleScene scene, SceneSide left, SceneSide right, float t)
    {
        var half = frame.Size.X / 2;
        foreach (var (side, x) in new[] { (left, frame.Position.X), (right, frame.Position.X + half) })
        {
            var hp = side.HpBefore;
            foreach (var s in scene.Strikes.Where(s => s.TargetId == side.Id && t >= s.Contact))
            {
                hp = s.TargetHpAfter;
            }

            var colour = side.Side == CoreSide.Player ? Look(LookPalette.Player) : Look(LookPalette.EnemyBone);
            var y = frame.End.Y - 52;
            UiText(new Vector2(x + 24, y), side.Name, colour, 16, bold: true);
            RightText(new Vector2(x + half - 24, y), $"{hp} / {side.MaxHp}", Ink, 16, bold: true);
            var bar = new Rect2(x + 24, y + 12, half - 48, 10);
            DrawRect(bar, UiColour("ink"));
            DrawRect(new Rect2(bar.Position, new Vector2(bar.Size.X * Math.Clamp((float)hp / Math.Max(1, side.MaxHp), 0, 1), bar.Size.Y)), colour);
        }
    }

    /// <summary>The level-up card (issue 535): the unit and its new level, then each stat that rose, or its one dry line.</summary>
    private void DrawLevelUp(LevelUpCard card)
    {
        DrawRect(new Rect2(Vector2.Zero, new Vector2(ViewWidth, ViewHeight)), UiColour("ink", 0.6f));
        var lines = card.Lines();
        var width = Math.Max(380, lines.Max(l => UiWidth(l, 18, bold: true)) + 80);
        var height = 90 + 30 * (lines.Count - 1);
        var rect = new Rect2((ViewWidth - width) / 2, (ViewHeight - height) / 2, width, height);
        Card(rect, Box, 12);
        UiText(new Vector2(rect.Position.X + 32, rect.Position.Y + 40), "LEVEL UP", Look(LookPalette.Player), 12, bold: true);
        UiText(new Vector2(rect.Position.X + 32, rect.Position.Y + 64), lines[0], Ink, 20, bold: true);
        for (var i = 1; i < lines.Count; i++)
        {
            UiText(new Vector2(rect.Position.X + 32, rect.Position.Y + 64 + 30 * i), lines[i], card.Rose.Count > 0 ? Look(LookPalette.Player) : Muted, 18, bold: card.Rose.Count > 0);
        }
    }
}
