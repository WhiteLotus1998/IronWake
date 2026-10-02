using Godot;
using Ironwake.Client;
using Ironwake.Core;
using CoreSide = Ironwake.Core.Side;

namespace Ironwake.Godot;

/// <summary>
/// The showcase's look on the board (issue 511, <c>docs/LOOK.md</c>): tiles in
/// <see cref="LookPalette"/>'s colours with ink detail and a hairline grid, the walls lit from
/// the north with a shadow on their south face, a mat under the board, each unit a class
/// silhouette on a side's disc with its HP under it (its name only while pointed at or selected, issue 512), and the marks (reach, threat,
/// hover, selection, the enemy phase's path and rings) in the same flat language. Shapes are
/// laid out on LOOK.md's 48-pixel tile and scaled to the map's tile.
/// </summary>
public partial class Main
{
    private static Color Look(Rgb rgb, float alpha = 1f) => new(Color.Color8(rgb.R, rgb.G, rgb.B), alpha);

    private static Color UiColour(string name, float alpha = 1f) => Look(LookPalette.Ui[name], alpha);

    private static Color MarkColour(string name, float alpha = 1f) => Look(LookPalette.Marks[name], alpha);

    private static Color TerrainColour(string id, float alpha = 1f) =>
        Look(LookPalette.Terrain.TryGetValue(id, out var rgb) ? rgb : LookPalette.Ui["muted"], alpha);

    /// <summary>
    /// A vendored font from <c>fonts/</c> (issue 511; OFL, the licences beside the files and in
    /// <c>LICENSES</c>): read from the source tree when run from it, from the imported resource in
    /// an exported build, and a system face by <paramref name="fallback"/> when neither is there.
    /// With <paramref name="tabular"/> the face sets numerals at one width, so columns of numbers align.
    /// </summary>
    private static Font LoadFont(string file, bool tabular, params string[] fallback)
    {
        var resource = "res://fonts/" + file;
        Font? font = null;
        var path = ProjectSettings.GlobalizePath(resource);
        if (File.Exists(path))
        {
            var loaded = new FontFile();
            if (loaded.LoadDynamicFont(path) == global::Godot.Error.Ok)
            {
                font = loaded;
            }
        }

        font ??= ResourceLoader.Exists(resource) ? GD.Load<Font>(resource) : null;
        font ??= new SystemFont { FontNames = fallback };
        if (!tabular)
        {
            return font;
        }

        var tnum = TextServerManager.GetPrimaryInterface().NameToTag("tnum");
        return new FontVariation { BaseFont = font, OpentypeFeatures = new global::Godot.Collections.Dictionary { { tnum, 1 } } };
    }

    /// <summary>LOOK.md's 48-pixel tile scaled to this map's tile.</summary>
    private float S => _tile / 48f;

    /// <summary>A tile's full square, the grid drawn over it rather than cut out of it.</summary>
    private Rect2 Cell(Coord at) => new(Board + new Vector2(at.X * _tile, at.Y * _tile), new Vector2(_tile, _tile));

    /// <summary>The mat the board sits on, and its hairline rim, so the map reads as an object on the screen.</summary>
    private void DrawMat(MapDefinition map)
    {
        var board = new Rect2(Board, new Vector2(map.Width * _tile, map.Height * _tile));
        Card(board.Grow(8), UiColour("panel"), 8);
        DrawRect(board.Grow(1.5f), UiColour("ink"), filled: false, width: 3);
    }

    /// <summary>One tile's ground: its colour, and fire's ember hatch over the forest it burns (round 129).</summary>
    private void DrawGround(MapDefinition map, Coord at, string id)
    {
        var r = Cell(at);
        if (id == "fire")
        {
            DrawRect(r, TerrainColour("forest"));
            if (TileArt("fire") is { } hatch)
            {
                DrawTextureRect(hatch, r, false);
            }
            else
            {
                Hatch(r, TerrainColour("fire"), 3 * S, 8 * S);
            }

            return;
        }

        if (TileArt(id) is { } tile)
        {
            DrawTextureRect(tile, r, false);
            return;
        }

        DrawRect(r, TerrainColour(id));
    }

    /// <summary>
    /// One tile's ink detail, drawn after the threat hatch so a glyph sits on the danger rather
    /// than under it (issue 512, round 138). Detail is ink over the tile's own colour, so terrain
    /// never adds a hue.
    /// </summary>
    private void DrawDetail(MapDefinition map, Coord at, string id)
    {
        var p = Cell(at).Position;
        var s = S;
        var ink = UiColour("ink");
        Vector2 P(float x, float y) => p + new Vector2(x * s, y * s);
        if (id != "fire" && TileArt(id, detailOnly: true) is { } detail)
        {
            // The file's detail laid again over the hatch its ground went under (issue 564); the
            // wall's lit top edge stays drawn here, since it depends on the tile above.
            DrawTextureRect(detail, Cell(at), false);
            if (id == "wall" && !IsHigh(map, new Coord(at.X, at.Y - 1)))
            {
                DrawLine(P(0, 0.5f), P(48, 0.5f), TerrainColour("mountain", 0.6f), 1);
            }

            return;
        }

        switch (id)
        {
            case "forest":
                foreach (var (dx, dy) in new[] { (13f, 26f), (28f, 20f), (22f, 34f) })
                {
                    DrawColoredPolygon(new[] { P(dx, dy - 12), P(dx + 7, dy), P(dx - 7, dy) }, new Color(ink, 0.35f));
                }

                break;
            case "water":
                foreach (var dy in new[] { 15f, 29f })
                {
                    var wave = Enumerable.Range(0, 13).Select(i => P(8 + i * 2.5f, dy - 2 * Mathf.Sin(i * Mathf.Pi / 2))).ToArray();
                    DrawPolyline(wave, MarkColour("reach", 0.35f), 1.5f * s, antialiased: true);
                }

                break;
            case "mountain":
                DrawColoredPolygon(new[] { P(6, 36), P(20, 12), P(28, 24), P(32, 18), P(40, 36) }, new Color(ink, 0.3f));
                break;
            case "hill":
                DrawColoredPolygon(Enumerable.Range(0, 13).Select(i => P(6 + i * 32 / 12f, 34 - 24 * Mathf.Sin(i * Mathf.Pi / 12))).ToArray(), new Color(ink, 0.25f));
                break;
            case "wall":
                var coursing = TerrainColour("mountain", 0.35f);
                DrawLine(P(0, 22), P(48, 22), coursing, 1);
                DrawLine(P(22, 0), P(22, 22), coursing, 1);
                DrawLine(P(11, 22), P(11, 48), coursing, 1);
                // Light from the north: the wall's top edge catches it unless another wall stands above.
                if (!IsHigh(map, new Coord(at.X, at.Y - 1)))
                {
                    DrawLine(P(0, 0.5f), P(48, 0.5f), TerrainColour("mountain", 0.6f), 1);
                }

                break;
            case "throne":
                DrawColoredPolygon(new[] { P(8, 40), P(8, 10), P(40, 10), P(40, 40), P(33, 40), P(33, 18), P(15, 18), P(15, 40) }, new Color(ink, 0.35f));
                break;
            case "fort":
                // The battlement, with the same north light: an ink shadow under its merlons.
                var battlement = new[] { P(8, 36), P(8, 14), P(14, 14), P(14, 19), P(19, 19), P(19, 14), P(25, 14), P(25, 19), P(30, 19), P(30, 14), P(36, 14), P(36, 19), P(40, 19), P(40, 36) };
                DrawColoredPolygon(battlement.Select(v => v + new Vector2(0, 2 * s)).ToArray(), new Color(ink, 0.25f));
                DrawColoredPolygon(battlement, new Color(ink, 0.3f));
                break;
        }
    }

    /// <summary>Whether a tile stands high enough to cast the board's one shadow: a wall or a mountain.</summary>
    private static bool IsHigh(MapDefinition map, Coord at) =>
        map.Contains(at) && map.TerrainIdAt(at) is "wall" or "mountain";

    /// <summary>
    /// The board's one depth cue (issue 511): light from the north, so every wall and mountain
    /// casts an ink shadow onto the tile south of it, deepest at the foot and fading down the tile.
    /// </summary>
    private void DrawShadows(MapDefinition map)
    {
        var ink = UiColour("ink");
        for (var y = 0; y < map.Height - 1; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var below = new Coord(x, y + 1);
                if (!IsHigh(map, new Coord(x, y)) || IsHigh(map, below))
                {
                    continue;
                }

                var r = Cell(below);
                var deep = _tile * 0.16f;
                DrawRect(new Rect2(r.Position, new Vector2(r.Size.X, deep)), new Color(ink, 0.42f));
                DrawRect(new Rect2(r.Position + new Vector2(0, deep), new Vector2(r.Size.X, deep)), new Color(ink, 0.18f));
            }
        }
    }

    /// <summary>The hairline ink grid at 18 percent over the whole board.</summary>
    private void DrawGrid(MapDefinition map)
    {
        var colour = UiColour("ink", 0.18f);
        for (var x = 0; x <= map.Width; x++)
        {
            DrawLine(Board + new Vector2(x * _tile, 0), Board + new Vector2(x * _tile, map.Height * _tile), colour, 1);
        }

        for (var y = 0; y <= map.Height; y++)
        {
            DrawLine(Board + new Vector2(0, y * _tile), Board + new Vector2(map.Width * _tile, y * _tile), colour, 1);
        }
    }

    /// <summary>
    /// A diagonal hatch across <paramref name="r"/>, lines running bottom-left to top-right every
    /// <paramref name="spacing"/> pixels: the enemy's threat and fire's embers, never a fill.
    /// </summary>
    private void Hatch(Rect2 r, Color colour, float width, float spacing)
    {
        var w = r.Size.X;
        var h = r.Size.Y;
        for (var c = spacing / 2; c < w + h; c += spacing)
        {
            // The line x + y = c inside the rectangle, from its lower-left end to its upper-right end.
            var start = new Vector2(Mathf.Max(0, c - h), Mathf.Min(h, c));
            var end = new Vector2(Mathf.Min(w, c), Mathf.Max(0, c - w));
            DrawLine(r.Position + start, r.Position + end, colour, width, antialiased: true);
        }
    }

    /// <summary>The selected unit's reach as a mark laid on the tile (issue 511): a thin frost wash with a frost inset edge, never a surface.</summary>
    private void DrawReachMark(Coord at)
    {
        var r = Cell(at).Grow(-3 * S);
        Card(r, MarkColour("reach", 0.16f), 4 * S);
        DrawRect(r, MarkColour("reach", 0.7f), filled: false, width: Mathf.Max(1, 1.5f * S));
    }

    /// <summary>The enemy's threat on a tile: a bone hatch, never a fill; no ground under it is warm, so it never blends to a peach (issue 564).</summary>
    /// <remarks>On a tile the dark hides (issue 601) it is laid again over the veil, so its stroke reads lighter than the veil would leave it, so dark ground never swallows it.</remarks>
    private void DrawThreatMark(Coord at, bool faint = false, bool unseen = false) =>
        Hatch(Cell(at).Grow(-1), MarkColour("threat", faint ? FaintThreat : unseen ? UnseenThreat : 0.5f), Mathf.Max(1, 1.5f * S), 9 * S);

    /// <summary>The hatch's alpha over the dark (issue 601): brighter than on seen ground, which the veil would otherwise dim to 0.19.</summary>
    private const float UnseenThreat = 0.4f;

    /// <summary>The hatch's alpha for a sleeping group's reach (issue 533): there, but never mistaken for a waking one's.</summary>
    private const float FaintThreat = 0.2f;

    /// <summary>
    /// A sleeping group's wake ring (issue 533): a dashed line in the threat mark along every edge
    /// between a tile inside the ring and one outside it, so the reach never hides that stopping in wakes it.
    /// </summary>
    private void DrawWakeRing(IReadOnlySet<Coord> ring)
    {
        foreach (var at in ring)
        {
            var r = Cell(at);
            var edges = new (Coord Next, Vector2 From, Vector2 To)[]
            {
                (new Coord(at.X, at.Y - 1), r.Position, new Vector2(r.End.X, r.Position.Y)),
                (new Coord(at.X + 1, at.Y), new Vector2(r.End.X, r.Position.Y), r.End),
                (new Coord(at.X, at.Y + 1), new Vector2(r.Position.X, r.End.Y), r.End),
                (new Coord(at.X - 1, at.Y), r.Position, new Vector2(r.Position.X, r.End.Y)),
            };
            foreach (var (next, from, to) in edges.Where(e => !ring.Contains(e.Next)))
            {
                DrawDashedLine(from, to, MarkColour("threat", 0.9f), 2 * S, 5 * S, aligned: true, antialiased: true);
            }
        }
    }

    /// <summary>The hovered tile: a dashed rectangle in the selected mark.</summary>
    private void DrawHoverMark(Coord at) => DashedRect(Cell(at).Grow(-3 * S), MarkColour("selected"), 2, 5 * S);

    private void DashedRect(Rect2 r, Color colour, float width, float dash)
    {
        var a = r.Position;
        var b = new Vector2(r.End.X, r.Position.Y);
        var c = r.End;
        var d = new Vector2(r.Position.X, r.End.Y);
        foreach (var (from, to) in new[] { (a, b), (b, c), (c, d), (d, a) })
        {
            DrawDashedLine(from, to, colour, width, dash, aligned: true, antialiased: true);
        }
    }

    /// <summary>A filled rectangle with rounded corners, the look's cards and chips.</summary>
    private void Card(Rect2 r, Color colour, float radius)
    {
        var box = new StyleBoxFlat { BgColor = colour, AntiAliasing = true };
        box.SetCornerRadiusAll((int)radius);
        box.Draw(GetCanvasItem(), r);
    }

    /// <summary>
    /// A unit (issue 511): the class silhouette on its side's disc about two thirds of a tile
    /// across, the enemy's with a hairline bone rim and the boss's with a dashed ring outside it,
    /// the captain's gold ring and crown (issue 601), the HP bar in the side's colour and the name under it while it is pointed at or selected.
    /// A unit that has acted draws its disc sunk toward the ink.
    /// </summary>
    private void DrawToken(BattleState state, BattleUnit unit)
    {
        var cell = Cell(unit.At);
        var s = S;
        var centre = TokenCentre(unit.At);
        var radius = TokenRadius * s;
        var ink = UiColour("ink");
        if (!Dusk.Seen(state, unit))
        {
            UiText(centre + new Vector2(0, 7 * s), Dusk.Unseen.ToString(), UiColour("text"), (int)(20 * s), bold: true, centred: true);
            return;
        }

        var player = unit.Side == CoreSide.Player;
        var fill = player ? Look(LookPalette.Player) : Look(LookPalette.Enemy);
        var deep = player ? Look(LookPalette.PlayerDeep) : new Color(ink, 0.55f);
        if (player && unit.Acted)
        {
            fill = fill.Lerp(ink, 0.45f);
            deep = deep.Lerp(ink, 0.45f);
        }

        var drawn = DrawTokenArt(unit, centre, radius, player && unit.Acted ? 0.45f : 0);
        if (!drawn)
        {
            DrawSetTransformMatrix(_frame * _tokenFrame * new Transform2D(0, new Vector2(1, (radius - 1) / radius), 0, centre + new Vector2(0, 3 * s)));
            DrawCircle(Vector2.Zero, radius, deep);
            DrawSetTransformMatrix(_frame * _tokenFrame);
            DrawCircle(centre, radius, fill);
        }

        var bone = Look(LookPalette.EnemyBone);
        if (!player)
        {
            // A hairline (issue 511): a negative width is Godot's one-pixel line, with no feather to thicken it.
            DrawArc(centre, radius - 0.5f, 0, Mathf.Tau, 48, bone, -1);
            if (unit.IsBoss)
            {
                for (var i = 0; i < 16; i++)
                {
                    var a = i * Mathf.Tau / 16;
                    DrawArc(centre, radius + 3 * s, a, a + Mathf.Tau / 32, 4, bone, -1);
                }
            }
        }

        if (!drawn)
        {
            DrawSilhouette(state, unit, centre + new Vector2(0, -1 * s), radius / 16f, player ? ink : bone);
        }

        if (unit.IsCaptain)
        {
            DrawCaptainMark(centre, radius);
        }

        if (unit.Id == _client!.Selected)
        {
            // The captain's ring already hugs his disc, so his selection sits outside it.
            var gap = unit.IsCaptain ? CaptainRingOut + 2.5f : 4;
            DrawArc(centre, radius + gap * s, 0, Mathf.Tau, 48, MarkColour("selected"), 2.5f, antialiased: true);
        }

        var max = unit.MaxHp(_client.Content);
        var barWidth = 30 * s;
        var bar = new Rect2(centre + new Vector2(-barWidth / 2, radius + 3 * s), new Vector2(barWidth, 4 * s));
        DrawRect(bar.Grow(1), ink);
        DrawRect(new Rect2(bar.Position, new Vector2(barWidth * Math.Clamp(unit.Hp, 0, max) / Math.Max(1, max), bar.Size.Y)), player ? Look(LookPalette.Player) : bone);
        // Names live on the unit card (round 138); a token carries its name only while pointed at or selected.
        if (unit.Id != _client.Selected && unit.At != _hover)
        {
            return;
        }

        var nameSize = Math.Max(8, (int)(9 * s));
        var name = FitName(unit.Unit.Name, nameSize, _tile - 2);
        // Under the HP bar at the tile's foot, spilling a few pixels onto the tile below (issue 564:
        // ART_SPEC's 32-pixel disc leaves the bar the tile's last 8 pixels).
        var at = new Vector2(centre.X, cell.End.Y + 5 * s);
        UiText(at + new Vector2(1, 1), name, new Color(ink, 0.8f), nameSize, bold: true, centred: true);
        UiText(at, name, UiColour("text"), nameSize, bold: true, centred: true);
    }

    /// <summary>How far past the disc the captain's gold ring's outer ink edge reaches, in 48-pixel frame units.</summary>
    private const float CaptainRingOut = 4.5f;

    /// <summary>
    /// The captain's mark (issue 601): a gold ring on the disc between two ink edges, which no
    /// other token wears, and a gold crown outlined in ink rising from the disc's top edge, its
    /// base sunk into the disc so the whole mark stays inside his own tile. The unit whose death
    /// loses the map is the first one found.
    /// </summary>
    private void DrawCaptainMark(Vector2 centre, float radius)
    {
        var s = S;
        var ink = UiColour("ink");
        var gold = MarkColour("captain");
        DrawArc(centre, radius + 0.75f * s, 0, Mathf.Tau, 48, ink, 1.5f * s, antialiased: true);
        DrawArc(centre, radius + 2.5f * s, 0, Mathf.Tau, 48, gold, 2.5f * s, antialiased: true);
        DrawArc(centre, radius + (CaptainRingOut - 0.5f) * s, 0, Mathf.Tau, 48, ink, 1f * s, antialiased: true);
        var k = radius / 16f;
        var c = centre + new Vector2(0, -radius + 6 * k);
        var crown = new[] { new Vector2(-9, 0), new Vector2(-9, -9), new Vector2(-4.5f, -4), new Vector2(0, -11), new Vector2(4.5f, -4), new Vector2(9, -9), new Vector2(9, 0) }
            .Select(v => c + v * k).ToArray();
        DrawColoredPolygon(crown, gold);
        DrawPolyline(crown.Append(crown[0]).ToArray(), ink, Mathf.Max(1f, 1.2f * k), antialiased: true);
    }

    /// <summary>The name as wide as the tile allows: whole, else its last word (the noun: Brigand, Warden), else cut to fit.</summary>
    private string FitName(string name, int size, float width)
    {
        bool Fits(string text) => _bold.GetStringSize(text, fontSize: size).X <= width;
        if (Fits(name))
        {
            return name;
        }

        var first = name.Split(' ')[^1];
        while (first.Length > 1 && !Fits(first))
        {
            first = first[..^1];
        }

        return first;
    }

    /// <summary>
    /// The class silhouette (LOOK.md, <c>docs/look/silhouettes.png</c>): each one the weapon the
    /// forecast is about, drawn on LOOK.md's 16-pixel disc and scaled by <paramref name="k"/>.
    /// A pikeman whose weapon reaches 2 is the toll warden's hooked pike; a boss reaver is the
    /// bandit leader's double bit. The classes the Tollgate does not field carry a plain first
    /// shape until the slice that draws their map.
    /// </summary>
    private void DrawSilhouette(BattleState state, BattleUnit unit, Vector2 c, float k, Color colour)
    {
        var width = 2.6f * k;
        Vector2 P(float x, float y) => c + new Vector2(x * k, y * k);
        void Stroke(params Vector2[] points) => DrawPolyline(points, colour, width, antialiased: true);
        void Fill(params Vector2[] points) => DrawColoredPolygon(points, colour);
        var content = _client!.Content;
        var reach = Enumerable.Range(0, unit.Unit.Inventory.Count).Select(slot => unit.UsableWeaponAt(content, slot)).OfType<Weapon>().Select(w => w.MaxRange).DefaultIfEmpty(1).Max();
        // An advanced form draws its base class's shape until it has its own (issue 704).
        switch (content.Class(unit.Unit.ClassId).BaseId)
        {
            case "cadet":
                Stroke(P(0, -11), P(0, 10));
                Stroke(P(-6, 4), P(6, 4));
                Fill(P(-2, -8), P(0, -13), P(2, -8));
                break;
            case "pikeman":
                Stroke(P(-9, 10), P(7, -8));
                Fill(P(5, -6), P(11, -12), P(9, -4));
                if (reach >= 2)
                {
                    Stroke(P(1, -2), P(-3, -6));
                    Stroke(P(1, -2), P(5, 2));
                }

                break;
            case "sergeant":
                // The pike crossed with a short sword, the two weapons the class carries (issue 691);
                // the same shape make_art.py draws.
                Stroke(P(-9, 10), P(7, -8));
                Fill(P(5, -6), P(11, -12), P(9, -4));
                Stroke(P(8, 9), P(-5, -4));
                Stroke(P(7.5f, 3.5f), P(2.5f, 8.5f));
                break;
            case "outrider":
                // Apart from the pike at 32 px (round 170): the lance couched flatter, the pennant
                // under its head, a horseshoe at the foot; the same shape make_art.py draws.
                Stroke(P(-11, 3), P(7, -5));
                Fill(P(6.2f, -6.8f), P(12.5f, -7), P(7.8f, -3.2f));
                Fill(P(-1, -0.4f), P(-7, 2.2f), P(-5, 5));
                Stroke(Enumerable.Range(0, 9).Select(i => P(4.5f * Mathf.Cos(Mathf.Pi * i / 8), 7.5f + 4.5f * Mathf.Sin(Mathf.Pi * i / 8))).ToArray());
                break;
            case "bowman":
                Stroke(Enumerable.Range(0, 13).Select(i => Quad(P(-4, -11), P(10, 0), P(-4, 11), i / 12f)).ToArray());
                Stroke(P(-4, -11), P(-4, 11));
                Stroke(P(-9, 0), P(8, 0));
                break;
            case "adept":
                Fill(Cubic(P(0, -12), P(8, -4), P(7, 8), P(0, 9))
                    .Concat(Cubic(P(0, 9), P(-7, 8), P(-8, -2), P(-2, -6)))
                    .Concat(Cubic(P(-2, -6), P(-2, -2), P(0, 0), P(1, -2)))
                    .Concat(Cubic(P(1, -2), P(2, -6), P(0, -9), P(0, -12))).ToArray());
                break;
            case "reaver":
                Stroke(P(-6, 11), P(4, -9));
                Fill(Cubic(P(1, -5), P(6, -12), P(12, -8), P(11, -3)).Concat(Cubic(P(11, -3), P(8, -4), P(6, -1), P(5, 2))).ToArray());
                if (unit.IsBoss)
                {
                    Fill(Cubic(P(1, -5), P(-5, -10), P(-9, -4), P(-8, 1)).Concat(Cubic(P(-8, 1), P(-5, -1), P(-3, 0), P(-2, 1))).ToArray());
                }

                break;
            case "chaplain":
                Stroke(P(0, -6), P(0, 11));
                DrawArc(P(0, -9), 3.5f * k, 0, Mathf.Tau, 16, colour, width, antialiased: true);
                break;
            case "skyrider":
                Stroke(P(-10, 2), P(-4, -6), P(0, 0), P(4, -6), P(10, 2));
                Stroke(P(0, 0), P(0, 10));
                break;
            case "bulwark":
                Fill(P(-8, -9), P(8, -9), P(8, 1), P(0, 10), P(-8, 1));
                break;
            default:
                DrawCircle(c, 3 * k, colour);
                break;
        }
    }

    private static Vector2 Quad(Vector2 a, Vector2 b, Vector2 c, float t) => a.Lerp(b, t).Lerp(b.Lerp(c, t), t);

    private static IEnumerable<Vector2> Cubic(Vector2 a, Vector2 b, Vector2 c, Vector2 d) =>
        Enumerable.Range(0, 10).Select(i => Bezier(a, b, c, d, i / 10f));

    private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
    {
        var u = 1 - t;
        return u * u * u * a + 3 * u * u * t * b + 3 * u * t * t * c + t * t * t * d;
    }

    /// <summary>Text in the UI face, <paramref name="at"/> its baseline's left end, or its centre with <paramref name="centred"/>.</summary>
    private void UiText(Vector2 at, string text, Color colour, int size, bool bold = false, bool centred = false)
    {
        var font = bold ? _bold : _ui;
        if (centred)
        {
            at -= new Vector2(font.GetStringSize(text, fontSize: size).X / 2, 0);
        }

        DrawString(font, at, text, fontSize: size, modulate: colour);
    }

    private float UiWidth(string text, int size, bool bold = false) => (bold ? _bold : _ui).GetStringSize(text, fontSize: size).X;
}
