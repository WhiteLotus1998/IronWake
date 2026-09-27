using Godot;
using Ironwake.Cli;
using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Core;
using CoreSide = Ironwake.Core.Side;

namespace Ironwake.Godot;

/// <summary>
/// The thin renderer, slices 1 and 2 and the readability pass (issues 347, 353, 349): flat tiles in
/// <see cref="Palette"/>'s colours with their glyphs and a legend, the dark shaded, units as
/// letters on blue circles and orange squares with their hp, the selected unit's reach, the forecast from a hovered tile against
/// each target in range and the threat on the selected unit there, the Recall browser, and the
/// event log in the console's own text. Every number comes from
/// <see cref="ClientSession"/>, which asks the core; nothing here knows a rule.
/// Arguments after <c>--</c>: <c>--map &lt;name|path&gt;</c>, <c>--seed N</c>, <c>--content dir</c>,
/// and for the headless gate <c>--parity &lt;script&gt; &lt;out&gt;</c>, which writes the client's
/// event log for the script and quits. <c>--script &lt;file&gt;</c> opens the battle with a script's
/// commands already played, <c>--select &lt;x,y&gt;</c> and <c>--hover &lt;x,y&gt;</c> set the pointer
/// as a click and a hover would, and <c>--screenshot &lt;file.png&gt;</c> saves one rendered frame
/// and quits (issue 352; it needs a display, so CI runs it under xvfb); <c>--recall</c> opens the
/// Recall browser; <c>--enemy-steps N</c> ends the player phase and reveals N enemy events. Keys: E
/// ends the phase, Space reveals the enemy phase's next event and marks it on the board, C skips
/// to its end, R opens or closes the Recall browser, where a click on a state's row recalls it,
/// Escape clears the selection and closes the browser. An exported build finds <c>content/</c>
/// beside its executable.
/// With <c>--campaign</c> (issue 360) it plays the campaign through <see cref="CampaignClient"/>
/// instead, from the first map or from <c>--from &lt;map&gt;</c>: the between-map screen shows the
/// console's roster, shop, deployment and keep lines; a click on a unit's row selects it, a click on
/// a ware buys it for that unit, B benches or unbenches it, M or a click on the march row marches,
/// and L leaves a decided battle. <c>--campaign-parity &lt;script&gt; &lt;out&gt;</c> writes the
/// presenter's event log for a <c>campaign --script</c> file and quits.
/// </summary>
public partial class Main : Node2D
{
    private const int ViewWidth = 1280;
    private const int ViewHeight = 720;
    private const int Margin = 16;
    private const int Top = 48;
    private const int PanelWidth = 500;
    private const int FontSize = 14;
    private const int LineHeight = 17;
    private const int LogLines = 30;
    private const int RecallRowsShown = 40;
    private const string KeyLine = "click: select, move, attack   E: end phase   Space: next enemy event   C: skip to end   R: recall   Esc: clear";
    private const float PanelBottom = ViewHeight - 30;
    private static readonly Vector2 Board = new(Margin, Top);
    private static readonly Vector2 PanelOrigin = new(ViewWidth - PanelWidth - Margin, Top);

    private static readonly Color Background = new(0.13f, 0.14f, 0.16f);
    private static readonly Color Box = new(0.2f, 0.22f, 0.27f);
    private static readonly Color Ink = new(0.93f, 0.93f, 0.9f);
    private static readonly Color Muted = new(0.65f, 0.66f, 0.68f);
    private static readonly Color Accent = new(0.55f, 0.78f, 1f);
    private static readonly Color Link = new(0.6f, 0.82f, 1f);
    private static readonly Color Warn = new(1f, 0.75f, 0.35f);

    /// <summary>The pointer and the enemy-phase mark: a yellow, which every common colour-vision deficiency keeps apart from both sides.</summary>
    private static readonly Color Mark = new(1f, 0.9f, 0.1f);

    /// <summary>One monospaced face for every line, so forecast numbers and hp align as they do in the console.</summary>
    private readonly Font _mono = new SystemFont { FontNames = new[] { "DejaVu Sans Mono", "Consolas", "Cascadia Mono", "Liberation Mono", "Menlo", "monospace" } };

    /// <summary>The tile size for the open map, the largest up to 44 px that leaves room for the legend and the panel.</summary>
    private int _tile = 40;

    /// <summary>How many characters of the monospaced face fit across the panel.</summary>
    private int Columns => (int)(PanelWidth / _mono.GetStringSize("M", fontSize: FontSize).X);

    /// <summary>The clickable regions drawn last frame (Recall rows, between-map rows) and what a click on each does.</summary>
    private readonly List<(Rect2 Area, Action Click)> _hits = new();

    private ClientSession? _client;
    private CampaignClient? _campaign;

    /// <summary>The roster unit selected on the between-map screen, which a ware is bought for and B benches.</summary>
    private string? _screenUnit;

    private char[] _letters = Array.Empty<char>();
    private Coord? _hover;
    private string _error = "";
    private string? _screenshot;
    private int _framesDrawn;
    private bool _recallOpen;

    public override void _Ready()
    {
        var args = OS.GetCmdlineUserArgs();
        var contentDir = Arg(args, "--content") ?? DefaultContent();
        var mapArg = Arg(args, "--map") ?? "the_tollgate";
        var seed = ulong.TryParse(Arg(args, "--seed"), out var parsed) ? parsed : 1UL;
        try
        {
            var content = ContentLoader.Load(contentDir);
            if (Array.IndexOf(args, "--campaign") >= 0 || Array.IndexOf(args, "--campaign-parity") >= 0)
            {
                var from = Arg(args, "--from");
                _campaign = new CampaignClient(content, contentDir, from is null ? CampaignRecord.Start(content, seed) : CampaignRecord.StartAt(content, seed, from));
                var campaignParity = Array.IndexOf(args, "--campaign-parity");
                if (campaignParity >= 0 && campaignParity + 2 < args.Length)
                {
                    File.WriteAllText(args[campaignParity + 2], Ironwake.Client.Script.PlayCampaign(_campaign, File.ReadAllText(args[campaignParity + 1])));
                    GetTree().Quit(0);
                    return;
                }

                _screenshot = Arg(args, "--screenshot");
                return;
            }

            var mapPath = File.Exists(mapArg) ? mapArg : Path.Combine(contentDir, "maps", mapArg + ".map");
            var map = MapFiles.Load(mapPath, content);
            var state = BattleState.From(map, content, content.Cast, seed);
            var parity = Array.IndexOf(args, "--parity");
            if (parity >= 0 && parity + 2 < args.Length)
            {
                File.WriteAllText(args[parity + 2], Ironwake.Client.Script.Play(content, state, File.ReadAllText(args[parity + 1])));
                GetTree().Quit(0);
                return;
            }

            _client = new ClientSession(content, state);
            _letters = MapRenderer.Letters(map, content);
            _tile = TileFor(map);
            if (Arg(args, "--script") is { } script)
            {
                Ironwake.Client.Script.Apply(_client, File.ReadAllText(script));
            }

            if (int.TryParse(Arg(args, "--enemy-steps"), out var steps) && _client.Submit(new EndPhase()))
            {
                for (var step = 0; step < steps && _client.Step(); step++)
                {
                }
            }

            if (CoordArg(args, "--select") is { } select)
            {
                _client.Select(select);
            }

            _hover = CoordArg(args, "--hover");
            _recallOpen = Array.IndexOf(args, "--recall") >= 0;
            _screenshot = Arg(args, "--screenshot");
        }
        catch (Exception e) when (e is ContentException or MapException or IOException or ArgumentException)
        {
            _error = "ERROR: " + e.Message;
            GD.PrintErr(_error);
            if (Array.IndexOf(args, "--parity") >= 0 || Array.IndexOf(args, "--campaign-parity") >= 0 || Array.IndexOf(args, "--screenshot") >= 0)
            {
                GetTree().Quit(1);
            }
        }
    }

    /// <summary>
    /// The repo's <c>content/</c> when run from the source tree; in an exported build, which has
    /// no source tree, the <c>content/</c> folder beside the executable.
    /// </summary>
    private static string DefaultContent()
    {
        var source = ProjectSettings.GlobalizePath("res://../../content");
        return OS.HasFeature("editor") || Directory.Exists(source)
            ? source
            : Path.Combine(Path.GetDirectoryName(OS.GetExecutablePath()) ?? ".", "content");
    }

    private static string? Arg(string[] args, string name)
    {
        var at = Array.IndexOf(args, name);
        return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
    }

    private static Coord? CoordArg(string[] args, string name)
    {
        var parts = Arg(args, name)?.Split(',');
        return parts is { Length: 2 } && int.TryParse(parts[0], out var x) && int.TryParse(parts[1], out var y) ? new Coord(x, y) : null;
    }

    /// <summary>
    /// The screenshot mode: waits a few frames so the board has been drawn at least once, saves
    /// the viewport as a PNG and quits, with exit code 1 when the image cannot be written.
    /// </summary>
    public override void _Process(double delta)
    {
        if (_screenshot is null || ++_framesDrawn < 3)
        {
            return;
        }

        var saved = GetViewport().GetTexture().GetImage().SavePng(_screenshot);
        GetTree().Quit(saved == global::Godot.Error.Ok ? 0 : 1);
        _screenshot = null;
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (_campaign is not null && _client is null)
        {
            ScreenInput(input);
            return;
        }

        if (_client is null)
        {
            return;
        }

        switch (input)
        {
            case InputEventMouseMotion motion:
                _hover = TileAt(motion.Position);
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click when TileAt(click.Position) is { } at:
                _client.Click(at);
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click when HitAt(click.Position) is { } hit:
                hit();
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }:
                _client.ClearSelection();
                break;
            case InputEventKey { Pressed: true, Echo: false } key:
                switch (key.Keycode)
                {
                    case Key.E:
                        _client.Submit(new EndPhase());
                        break;
                    case Key.Space:
                        _client.Step();
                        break;
                    case Key.C:
                        _client.Continue();
                        break;
                    case Key.R:
                        _recallOpen = !_recallOpen;
                        break;
                    case Key.Escape:
                        _client.ClearSelection();
                        _recallOpen = false;
                        break;
                    case Key.L when _campaign is not null:
                        _campaign.Leave();
                        SyncBattle();
                        break;
                    default:
                        return;
                }

                break;
            default:
                return;
        }

        QueueRedraw();
    }

    /// <summary>Opens the campaign's battle when one has begun, and returns to the screen when it is left.</summary>
    private void SyncBattle()
    {
        _client = _campaign!.Battle;
        _recallOpen = false;
        _hover = null;
        if (_client is not null)
        {
            _letters = MapRenderer.Letters(_client.State.Map, _client.Content);
            _tile = TileFor(_client.State.Map);
        }
    }

    private static int TileFor(MapDefinition map) =>
        Math.Min(44, Math.Min((ViewWidth - PanelWidth - 3 * Margin) / map.Width, (ViewHeight - Top - 110) / map.Height));

    /// <summary>The between-map screen's input: a click on a row does what it offers; M marches; B benches or unbenches the selected unit.</summary>
    private void ScreenInput(InputEvent input)
    {
        var campaign = _campaign!;
        switch (input)
        {
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click:
                HitAt(click.Position)?.Invoke();
                break;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.M }:
                campaign.March();
                break;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.B } when _screenUnit is { } unit:
                if (campaign.Record.Benched.Contains(unit))
                {
                    campaign.Unbench(unit);
                }
                else
                {
                    campaign.Bench(unit);
                }

                break;
            default:
                return;
        }

        SyncBattle();
        QueueRedraw();
    }

    /// <summary>
    /// The between-map screen: the console's own lines, with the roster rows selecting a unit, a
    /// row per ware buying it for that unit, and a march row; then the status and the event log.
    /// </summary>
    private IEnumerable<(string Text, Action? Click, bool Selected)> ScreenLines()
    {
        var campaign = _campaign!;
        var record = campaign.Record;
        var content = campaign.Content;
        if (campaign.Status is { } status)
        {
            foreach (var text in status.Split('\n'))
            {
                yield return ("! " + text, null, false);
            }
        }

        if (campaign.NextMap is { } map)
        {
            yield return (CampaignSession.ScreenHeading(record, content, map), null, false);
            yield return ("roster: click a unit to select it; B benches or unbenches it", null, false);
            foreach (var unit in record.Roster)
            {
                var id = unit.Id;
                yield return (CampaignSession.UnitLines(record, content, unit, detail: false)[0], () => _screenUnit = id, id == _screenUnit);
            }

            if (record.Fallen.Count > 0)
            {
                yield return (CampaignSession.RosterLines(record, content)[^1], null, false);
            }

            foreach (var text in CampaignSession.ShopLines(record, content))
            {
                yield return (text, null, false);
            }

            yield return (_screenUnit is null ? "wares: select a unit to buy for it" : $"wares: click one to buy it for {_screenUnit}", null, false);
            foreach (var ware in campaign.Stock)
            {
                var id = ware;
                yield return ("  " + CampaignSession.WareText(content, id), _screenUnit is { } buyer ? () => campaign.Buy(id, buyer) : null, false);
            }

            yield return (CampaignSession.DeploymentLine(record, content, map), null, false);
            if (record.KeepMenuRefusal(content) is null)
            {
                foreach (var text in campaign.KeepLines())
                {
                    yield return (text, null, false);
                }
            }

            yield return ($"[ march to {map.Name} ]  (M)", () => campaign.March(), false);
        }

        yield return ("", null, false);
        yield return ("-- event log --", null, false);
        var log = campaign.LogText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var text in log.Skip(Math.Max(0, log.Length - LogLines)))
        {
            yield return (text, null, false);
        }
    }

    private Coord? TileAt(Vector2 position)
    {
        if (_client is null)
        {
            return null;
        }

        var local = position - Board;
        var at = new Coord((int)Mathf.Floor(local.X / _tile), (int)Mathf.Floor(local.Y / _tile));
        return _client.State.Map.Contains(at) ? at : null;
    }

    /// <summary>What a click at <paramref name="position"/> does on the regions drawn last frame, or null.</summary>
    private Action? HitAt(Vector2 position) => _hits.LastOrDefault(hit => hit.Area.HasPoint(position)).Click;

    public override void _Draw()
    {
        _hits.Clear();
        DrawRect(new Rect2(Vector2.Zero, new Vector2(ViewWidth, ViewHeight)), Background);
        if (_campaign is not null && _client is null)
        {
            DrawScreen();
            return;
        }

        if (_client is null)
        {
            Text(new Vector2(Margin, 32), _error, Ink, 16);
            return;
        }

        var state = _client.State;
        Text(new Vector2(Margin, 28), Header(state), Ink, 18);
        DrawBoard(state);
        DrawLegend(state);
        DrawPanel();
        Text(new Vector2(Margin, ViewHeight - 10), KeyLine, Muted, 13);
    }

    /// <summary>The board: terrain with its glyph, exits, reach, the dark, the pointer, the units, and the enemy-phase mark.</summary>
    private void DrawBoard(BattleState state)
    {
        var map = state.Map;
        var reach = _client!.Reach;
        var dusk = Dusk.Sight(state) is not null;
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var at = new Coord(x, y);
                var rect = TileRect(at);
                var terrain = _client.Content.TerrainById(map.TerrainIdAt(at));
                DrawRect(rect, Of(Palette.TerrainOf(terrain.Id)));
                if (terrain.Glyph != '.')
                {
                    Text(rect.Position + new Vector2(3, 12), terrain.Glyph.ToString(), new Color(0, 0, 0, 0.45f), 11);
                }

                if (dusk && !Dusk.Sees(state, CoreSide.Player, at))
                {
                    DrawRect(rect, new Color(0.03f, 0.03f, 0.08f, 0.62f));
                    DrawLine(rect.Position + new Vector2(0, _tile * 0.5f), rect.Position + new Vector2(_tile * 0.5f, 0), new Color(1, 1, 1, 0.12f), 1);
                    DrawLine(rect.Position + new Vector2(0, _tile - 1), rect.Position + new Vector2(_tile - 1, 0), new Color(1, 1, 1, 0.12f), 1);
                }

                if (map.IsExit(at))
                {
                    DrawRect(Inset(rect, 2), Colors.White, filled: false, width: 3);
                }

                if (reach is not null && reach.CanEnd(at))
                {
                    DrawRect(rect, new Color(1f, 1f, 1f, 0.3f));
                    DrawRect(Inset(rect, 1), new Color(0.1f, 0.2f, 0.6f, 0.9f), filled: false, width: 1);
                }
            }
        }

        if (_client.Playing is { } walked)
        {
            DrawPath(walked);
        }

        foreach (var unit in state.Units)
        {
            DrawUnit(state, unit);
        }

        if (_client.Playing is { } mark)
        {
            DrawMark(mark);
        }

        if (_hover is { } hover)
        {
            DrawRect(Inset(TileRect(hover), 1), Mark, filled: false, width: 2);
        }
    }

    private void DrawUnit(BattleState state, BattleUnit unit)
    {
        var rect = TileRect(unit.At);
        var centre = rect.GetCenter();
        if (!Dusk.Seen(state, unit))
        {
            Text(centre + new Vector2(-_tile * 0.15f, _tile * 0.2f), Dusk.Unseen.ToString(), new Color(0.85f, 0.85f, 0.9f), (int)(_tile * 0.5f));
            return;
        }

        var player = unit.Side == CoreSide.Player;
        var fill = Of(player ? Palette.Player : Palette.Enemy);
        if (player && unit.Acted)
        {
            fill = fill.Darkened(0.55f);
        }

        var radius = _tile * 0.4f;
        if (player)
        {
            DrawCircle(centre, radius + 1.5f, Colors.Black);
            DrawCircle(centre, radius, fill);
        }
        else
        {
            var square = new Rect2(centre - new Vector2(radius, radius), new Vector2(radius * 2, radius * 2));
            DrawRect(Inset(square, -1.5f), Colors.Black);
            DrawRect(square, fill);
        }

        if (unit.Id == _client!.Selected)
        {
            DrawArc(centre, radius + 4, 0, Mathf.Tau, 32, Mark, 2);
        }

        var letter = _letters[unit.PlacementIndex].ToString();
        var size = (int)(_tile * 0.45f);
        Text(centre + new Vector2(-_mono.GetStringSize(letter, fontSize: size).X / 2, size * 0.35f), letter, player ? Colors.White : Colors.Black, size);
        var hp = unit.Hp.ToString();
        var hpSize = Math.Max(10, (int)(_tile * 0.3f));
        var hpWidth = _mono.GetStringSize(hp, fontSize: hpSize).X;
        DrawRect(new Rect2(rect.Position + new Vector2(rect.Size.X - hpWidth - 3, rect.Size.Y - hpSize - 1), new Vector2(hpWidth + 3, hpSize + 1)), new Color(0, 0, 0, 0.75f));
        Text(rect.Position + new Vector2(rect.Size.X - hpWidth - 1.5f, rect.Size.Y - 3), hp, Colors.White, hpSize);
    }

    /// <summary>The enemy-phase path: the line walked from the start tile through the path to the end tile, drawn under the units.</summary>
    private void DrawPath(Highlight mark)
    {
        var points = new List<Vector2>();
        if (mark.From is { } from)
        {
            points.Add(TileRect(from).GetCenter());
        }

        points.AddRange(mark.Path.Select(tile => TileRect(tile).GetCenter()));
        if (mark.To is { } to && mark.From is not null)
        {
            points.Add(TileRect(to).GetCenter());
        }

        if (points.Count > 1)
        {
            DrawPolyline(points.ToArray(), Colors.Black, 6);
            DrawPolyline(points.ToArray(), Mark, 3);
        }
    }

    /// <summary>
    /// The enemy-phase mark over the units: the start tile's corners dotted, the tile the unit
    /// ended on or acted from ringed, and the tile it struck bracketed in white at its corners.
    /// </summary>
    private void DrawMark(Highlight mark)
    {
        if (mark.From is { } from)
        {
            var start = Inset(TileRect(from), 4);
            for (var i = 0; i < 4; i++)
            {
                DrawCircle(start.Position + new Vector2(i % 2 * start.Size.X, i / 2 * start.Size.Y), 3, Mark);
            }
        }

        if (mark.To is { } to)
        {
            DrawRect(Inset(TileRect(to), -1), Colors.Black, filled: false, width: 5);
            DrawRect(Inset(TileRect(to), -1), Mark, filled: false, width: 3);
        }

        if (mark.Struck is { } struck)
        {
            var r = Inset(TileRect(struck), -2);
            var arm = _tile * 0.3f;
            foreach (var (corner, dx, dy) in new[] { (r.Position, 1, 1), (new Vector2(r.End.X, r.Position.Y), -1, 1), (new Vector2(r.Position.X, r.End.Y), 1, -1), (r.End, -1, -1) })
            {
                foreach (var (colour, width) in new[] { (Colors.Black, 7f), (Colors.White, 4f) })
                {
                    DrawLine(corner, corner + new Vector2(dx * arm, 0), colour, width);
                    DrawLine(corner, corner + new Vector2(0, dy * arm), colour, width);
                }
            }
        }
    }

    /// <summary>The legend under the board: each terrain on this map with its glyph, then what the shapes and marks mean.</summary>
    private void DrawLegend(BattleState state)
    {
        var map = state.Map;
        var y = Board.Y + map.Height * _tile + 22;
        var x = Board.X;
        var ids = Enumerable.Range(0, map.Height).SelectMany(row => Enumerable.Range(0, map.Width).Select(col => map.TerrainIdAt(new Coord(col, row)))).Distinct().ToList();
        foreach (var id in ids)
        {
            var terrain = _client!.Content.TerrainById(id);
            var label = $"{terrain.Glyph} {terrain.Name}";
            var width = 18 + _mono.GetStringSize(label, fontSize: 13).X + 14;
            if (x + width > PanelOrigin.X - Margin)
            {
                x = Board.X;
                y += 20;
            }

            DrawRect(new Rect2(x, y - 11, 14, 14), Of(Palette.TerrainOf(id)));
            DrawRect(new Rect2(x, y - 11, 14, 14), Colors.Black, filled: false, width: 1);
            Text(new Vector2(x + 18, y), label, Ink, 13);
            x += width;
        }

        y += 24;
        x = Board.X;
        DrawCircle(new Vector2(x + 7, y - 4), 7, Of(Palette.Player));
        x = LegendText(x + 18, y, "yours");
        DrawRect(new Rect2(x, y - 11, 14, 14), Of(Palette.Enemy));
        x = LegendText(x + 18, y, "enemy");
        DrawRect(new Rect2(x, y - 11, 14, 14), Of(Palette.TerrainOf("plain")).Lerp(Colors.White, 0.3f));
        x = LegendText(x + 18, y, "can move");
        if (map.Exits.Count > 0)
        {
            DrawRect(new Rect2(x, y - 11, 14, 14), Colors.White, filled: false, width: 2);
            x = LegendText(x + 18, y, "exit");
        }

        if (Dusk.Sight(state) is not null)
        {
            DrawRect(new Rect2(x, y - 11, 14, 14), new Color(0.03f, 0.03f, 0.08f, 0.9f));
            x = LegendText(x + 18, y, "? unseen");
        }

        DrawRect(new Rect2(x, y - 11, 14, 14), Mark, filled: false, width: 2);
        x = LegendText(x + 18, y, "enemy act");
        DrawRect(new Rect2(x, y - 11, 14, 14), Colors.White, filled: false, width: 1);
        LegendText(x + 18, y, "struck");
    }

    private float LegendText(float x, float y, string text)
    {
        Text(new Vector2(x, y), text, Ink, 13);
        return x + _mono.GetStringSize(text, fontSize: 13).X + 16;
    }

    /// <summary>
    /// The side panel, top to bottom: status, the enemy phase's keys, then either the Recall
    /// browser or the forecast and threat (boxed, first, where the eye lands), the unit panel,
    /// and the event log, newest at the bottom, the line the board marks drawn in the mark colour.
    /// </summary>
    private void DrawPanel()
    {
        var client = _client!;
        var y = PanelOrigin.Y;
        foreach (var text in StatusLines())
        {
            y = Row(y, text, Warn);
        }

        if (client.EnemyPhasePlaying)
        {
            y = Row(y, "ENEMY PHASE  Space: next event  C: skip to end", Mark) + 6;
        }

        if (_recallOpen)
        {
            y = Title(y, "RECALL  click a state to rewind to it; R closes");
            var rows = client.RecallRows;
            var states = rows.Where(r => r.State is not null).ToList();
            foreach (var row in rows.Where(r => r.State is null))
            {
                y = Row(y, row.Text, Ink);
            }

            var fit = Math.Max(1, (int)((PanelBottom - y) / LineHeight) - 1);
            var shown = Math.Min(Math.Min(RecallRowsShown, fit), states.Count);
            if (states.Count > shown)
            {
                y = Row(y, $"({states.Count - shown} earlier states not shown; recall list in the console has them all)", Muted);
            }

            foreach (var row in states.Skip(states.Count - shown))
            {
                var top = y;
                y = Row(y, row.Text, Link);
                var index = row.State!.Value;
                _hits.Add((new Rect2(PanelOrigin.X, top, PanelWidth, y - top), () => client.Recall(index)));
            }

            return;
        }

        y = Title(y, "FORECAST");
        var boxTop = y;
        var forecast = ForecastLines().ToList();
        DrawRect(new Rect2(PanelOrigin.X - 6, boxTop - LineHeight + 3, PanelWidth + 12, Wrapped(forecast).Count() * LineHeight + 6), Box);
        foreach (var text in forecast)
        {
            y = Row(y, text, Ink);
        }

        y += 10;
        var unitAt = _hover is { } over && client.UnitAt(over) is not null ? over : client.Selected is { } id ? client.State.Find(id)?.At : null;
        if (unitAt is { } at && client.Show(at) is { } show)
        {
            y = Title(y, "UNIT");
            foreach (var text in show.Take(3))
            {
                y = Row(y, text, Ink);
            }

            y += 10;
        }

        y = Title(y, "EVENT LOG");
        var log = client.Log;
        var rows2 = new List<(string Text, bool Marked)>();
        for (var i = 0; i < log.Count; i++)
        {
            var marked = i == log.Count - 1 && client.Playing is not null && client.EnemyPhasePlaying;
            foreach (var line in log[i].Split('\n'))
            {
                foreach (var row in TextLayout.Wrap(line, Columns))
                {
                    rows2.Add((row, marked));
                }
            }
        }

        var room = Math.Max(0, (int)((PanelBottom - y) / LineHeight));
        foreach (var (text, marked) in rows2.Skip(Math.Max(0, rows2.Count - room)))
        {
            if (marked)
            {
                DrawRect(new Rect2(PanelOrigin.X - 6, y - LineHeight + 4, PanelWidth + 12, LineHeight), new Color(Mark, 0.22f));
            }

            Text(new Vector2(PanelOrigin.X, y), text, marked ? Mark : Ink, FontSize);
            y += LineHeight;
        }
    }

    /// <summary>What the forecast box holds: a hint until a unit is selected and a tile hovered, then the console's forecast and threat text.</summary>
    private IEnumerable<string> ForecastLines()
    {
        var client = _client!;
        if (client.EnemyPhasePlaying)
        {
            yield return "(the enemy is acting)";
            yield break;
        }

        if (client.Selected is null)
        {
            yield return "Click one of your units (a blue circle) to select it,";
            yield return "then point at a tile to see the forecast from there.";
            yield break;
        }

        if (_hover is not { } tile)
        {
            yield return "Point at a tile to see the forecast and threat there.";
            yield break;
        }

        var any = false;
        foreach (var forecast in client.Hover(tile))
        {
            any = true;
            foreach (var text in forecast.Text.Split('\n'))
            {
                yield return text;
            }
        }

        if (client.Threat(tile) is { } threat)
        {
            any = true;
            foreach (var text in threat.Split('\n'))
            {
                yield return text;
            }
        }

        if (!any)
        {
            yield return $"{client.Selected} cannot end its move on {tile.X},{tile.Y}.";
        }
    }

    /// <summary>The between-map screen, each line wrapped to the window, a row's click region covering all its wrapped rows.</summary>
    private void DrawScreen()
    {
        Text(new Vector2(Margin, 28), _campaign!.Over ? "campaign over" : "between maps", Ink, 18);
        var columns = (int)((ViewWidth - 2 * Margin) / _mono.GetStringSize("M", fontSize: FontSize).X);
        var y = (float)Top;
        foreach (var (text, click, selected) in ScreenLines())
        {
            var top = y;
            foreach (var row in TextLayout.Wrap(text, columns))
            {
                if (y > ViewHeight - Margin)
                {
                    return;
                }

                Text(new Vector2(Margin, y), row, selected ? Mark : click is null ? Ink : Link, FontSize);
                y += LineHeight;
            }

            if (click is not null)
            {
                _hits.Add((new Rect2(0, top - LineHeight + 4, ViewWidth, y - top), click));
            }
        }
    }

    private IEnumerable<string> Wrapped(IEnumerable<string> lines) => lines.SelectMany(line => TextLayout.Wrap(line, Columns));

    /// <summary>Draws one console line wrapped to the panel, returning the baseline below it.</summary>
    private float Row(float y, string line, Color colour)
    {
        foreach (var row in TextLayout.Wrap(line, Columns))
        {
            if (y > PanelBottom)
            {
                break;
            }

            Text(new Vector2(PanelOrigin.X, y), row, colour, FontSize);
            y += LineHeight;
        }

        return y;
    }

    private float Title(float y, string title)
    {
        Text(new Vector2(PanelOrigin.X, y), title, Accent, FontSize);
        DrawLine(new Vector2(PanelOrigin.X, y + 4), new Vector2(PanelOrigin.X + PanelWidth, y + 4), Accent, 1);
        return y + LineHeight + 4;
    }

    private void Text(Vector2 at, string text, Color colour, int size) =>
        DrawString(_mono, at, text, fontSize: size, modulate: colour);

    private Rect2 TileRect(Coord at) => new(Board + new Vector2(at.X * _tile, at.Y * _tile), new Vector2(_tile - 1, _tile - 1));

    private static Rect2 Inset(Rect2 rect, float by) => rect.Grow(-by);

    private static Color Of(Rgb rgb) => Color.Color8(rgb.R, rgb.G, rgb.B);

    private IEnumerable<string> StatusLines()
    {
        var client = _client!;
        if (client.Status is { } status)
        {
            foreach (var text in status.Split('\n'))
            {
                yield return "! " + text;
            }
        }

        if (_campaign is not null)
        {
            if (_campaign.Status is { } refusal)
            {
                yield return "! " + refusal;
            }

            if (client.State.Outcome.IsOver && !client.EnemyPhasePlaying)
            {
                yield return "the battle is decided: L leaves it";
            }
        }
    }

    private static string Header(BattleState state) =>
        state.Outcome.IsOver
            ? $"{state.Map.Name}  battle {(state.Outcome.Result == BattleResult.Won ? "won" : "lost")}: {state.Outcome.Reason}"
            : $"{state.Map.Name}  turn {state.Turn} of {state.Map.TurnLimit}  {state.Phase.ToString().ToLowerInvariant()} phase  {MapRenderer.WinName(state.Map.Win)}  recall {state.RecallCharges}";
}
