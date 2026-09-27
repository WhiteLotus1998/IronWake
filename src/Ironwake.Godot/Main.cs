using Godot;
using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Core;
using CoreSide = Ironwake.Core.Side;

namespace Ironwake.Godot;

/// <summary>
/// The thin renderer, slice 1 (issue 347): flat tiles per terrain, units as letters in side
/// colours with their hp, the selected unit's reach, the forecast from a hovered tile against
/// each target in range, and the event log in the console's own text. Every number comes from
/// <see cref="ClientSession"/>, which asks the core; nothing here knows a rule.
/// Arguments after <c>--</c>: <c>--map &lt;name|path&gt;</c>, <c>--seed N</c>, <c>--content dir</c>,
/// and for the headless gate <c>--parity &lt;script&gt; &lt;out&gt;</c>, which writes the client's
/// event log for the script and quits. <c>--script &lt;file&gt;</c> opens the battle with a script's
/// commands already played, <c>--select &lt;x,y&gt;</c> and <c>--hover &lt;x,y&gt;</c> set the pointer
/// as a click and a hover would, and <c>--screenshot &lt;file.png&gt;</c> saves one rendered frame
/// and quits (issue 352; it needs a display, so CI runs it under xvfb). Keys: E ends the phase, Space steps the enemy phase one
/// event, C continues it to the end, Escape clears the selection.
/// </summary>
public partial class Main : Node2D
{
    private const int Tile = 40;
    private const int LogLines = 30;
    private static readonly Vector2 Board = new(16, 48);

    private ClientSession? _client;
    private char[] _letters = Array.Empty<char>();
    private Coord? _hover;
    private string _error = "";
    private string? _screenshot;
    private int _framesDrawn;

    public override void _Ready()
    {
        var args = OS.GetCmdlineUserArgs();
        var contentDir = Arg(args, "--content") ?? ProjectSettings.GlobalizePath("res://../../content");
        var mapArg = Arg(args, "--map") ?? "the_tollgate";
        var seed = ulong.TryParse(Arg(args, "--seed"), out var parsed) ? parsed : 1UL;
        try
        {
            var content = ContentLoader.Load(contentDir);
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
            if (Arg(args, "--script") is { } script)
            {
                Ironwake.Client.Script.Apply(_client, File.ReadAllText(script));
            }

            if (CoordArg(args, "--select") is { } select)
            {
                _client.Select(select);
            }

            _hover = CoordArg(args, "--hover");
            _screenshot = Arg(args, "--screenshot");
        }
        catch (Exception e) when (e is ContentException or MapException or IOException)
        {
            _error = "ERROR: " + e.Message;
            GD.PrintErr(_error);
            if (Array.IndexOf(args, "--parity") >= 0 || Array.IndexOf(args, "--screenshot") >= 0)
            {
                GetTree().Quit(1);
            }
        }
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
                    case Key.Escape:
                        _client.ClearSelection();
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

    private Coord? TileAt(Vector2 position)
    {
        if (_client is null)
        {
            return null;
        }

        var local = position - Board;
        var at = new Coord((int)Mathf.Floor(local.X / Tile), (int)Mathf.Floor(local.Y / Tile));
        return _client.State.Map.Contains(at) ? at : null;
    }

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;
        if (_client is null)
        {
            DrawString(font, new Vector2(16, 32), _error, fontSize: 16, modulate: Colors.White);
            return;
        }

        var state = _client.State;
        var map = state.Map;
        DrawString(font, new Vector2(16, 28), Header(state), fontSize: 18, modulate: Colors.White);
        var reach = _client.Reach;
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var at = new Coord(x, y);
                var rect = new Rect2(Board + new Vector2(x * Tile, y * Tile), new Vector2(Tile - 1, Tile - 1));
                DrawRect(rect, TerrainColour(map.TerrainIdAt(at)));
                if (map.IsExit(at))
                {
                    DrawRect(rect, new Color(1f, 1f, 1f, 0.9f), filled: false, width: 3);
                }

                if (reach is not null && reach.CanEnd(at))
                {
                    DrawRect(rect, new Color(0.35f, 0.6f, 1f, 0.45f));
                }

                if (_hover == at)
                {
                    DrawRect(rect, Colors.Yellow, filled: false, width: 2);
                }
            }
        }

        foreach (var unit in state.Units)
        {
            var corner = Board + new Vector2(unit.At.X * Tile, unit.At.Y * Tile);
            var centre = corner + new Vector2(Tile / 2f, Tile / 2f);
            if (!Dusk.Seen(state, unit))
            {
                DrawString(font, corner + new Vector2(13, 27), Dusk.Unseen.ToString(), fontSize: 20, modulate: Colors.Gray);
                continue;
            }

            var colour = unit.Side == CoreSide.Player ? new Color(0.2f, 0.45f, 0.95f) : new Color(0.85f, 0.2f, 0.2f);
            DrawCircle(centre, Tile * 0.42f, unit.Acted && unit.Side == CoreSide.Player ? colour.Darkened(0.5f) : colour);
            if (unit.Id == _client.Selected)
            {
                DrawArc(centre, Tile * 0.46f, 0, Mathf.Tau, 24, Colors.White, 2);
            }

            DrawString(font, corner + new Vector2(13, 22), _letters[unit.PlacementIndex].ToString(), fontSize: 18, modulate: Colors.White);
            DrawString(font, corner + new Vector2(4, 37), unit.Hp.ToString(), fontSize: 11, modulate: Colors.White);
        }

        var panel = new Vector2(Board.X + map.Width * Tile + 16, 48);
        var line = 0;
        foreach (var text in PanelLines())
        {
            DrawString(font, panel + new Vector2(0, line * 16), text, fontSize: 13, modulate: Colors.White);
            line++;
        }
    }

    private IEnumerable<string> PanelLines()
    {
        var client = _client!;
        if (client.Status is { } status)
        {
            yield return "! " + status;
        }

        if (client.EnemyPhasePlaying)
        {
            yield return "enemy phase: Space steps, C continues";
        }

        if (_hover is { } tile && client.Selected is not null)
        {
            foreach (var forecast in client.Hover(tile))
            {
                foreach (var text in forecast.Text.Split('\n'))
                {
                    yield return text;
                }
            }
        }

        yield return "";
        yield return "-- event log --";
        var log = client.Log.SelectMany(entry => entry.Split('\n')).ToList();
        foreach (var text in log.Skip(Math.Max(0, log.Count - LogLines)))
        {
            yield return text;
        }
    }

    private static string Header(BattleState state) =>
        state.Outcome.IsOver
            ? $"{state.Map.Name}  battle {(state.Outcome.Result == BattleResult.Won ? "won" : "lost")}: {state.Outcome.Reason}"
            : $"{state.Map.Name}  turn {state.Turn} of {state.Map.TurnLimit}  {state.Phase.ToString().ToLowerInvariant()} phase  {MapRenderer.WinName(state.Map.Win)}  recall {state.RecallCharges}";

    private static Color TerrainColour(string id) => id switch
    {
        "plain" => new Color(0.55f, 0.7f, 0.4f),
        "road" => new Color(0.75f, 0.68f, 0.5f),
        "forest" => new Color(0.2f, 0.45f, 0.2f),
        "hill" => new Color(0.6f, 0.55f, 0.35f),
        "mountain" => new Color(0.45f, 0.4f, 0.38f),
        "water" => new Color(0.2f, 0.4f, 0.7f),
        "fort" => new Color(0.6f, 0.5f, 0.45f),
        "wall" => new Color(0.25f, 0.25f, 0.28f),
        "throne" => new Color(0.8f, 0.65f, 0.2f),
        _ => new Color(0.5f, 0.5f, 0.5f),
    };
}
