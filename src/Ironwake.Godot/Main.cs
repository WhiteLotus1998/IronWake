using Godot;
using Ironwake.Cli;
using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Core;
using CoreSide = Ironwake.Core.Side;

namespace Ironwake.Godot;

/// <summary>
/// The thin renderer, slices 1 and 2 (issues 347, 353): flat tiles per terrain, units as letters in side
/// colours with their hp, the selected unit's reach, the forecast from a hovered tile against
/// each target in range and the threat on the selected unit there, the Recall browser, and the
/// event log in the console's own text. Every number comes from
/// <see cref="ClientSession"/>, which asks the core; nothing here knows a rule.
/// Arguments after <c>--</c>: <c>--map &lt;name|path&gt;</c>, <c>--seed N</c>, <c>--content dir</c>,
/// and for the headless gate <c>--parity &lt;script&gt; &lt;out&gt;</c>, which writes the client's
/// event log for the script and quits. <c>--script &lt;file&gt;</c> opens the battle with a script's
/// commands already played, <c>--select &lt;x,y&gt;</c> and <c>--hover &lt;x,y&gt;</c> set the pointer
/// as a click and a hover would, and <c>--screenshot &lt;file.png&gt;</c> saves one rendered frame
/// and quits (issue 352; it needs a display, so CI runs it under xvfb); <c>--recall</c> opens the
/// Recall browser. Keys: E ends the phase, Space steps the enemy phase one event, C continues it
/// to the end, R opens or closes the Recall browser, where a click on a state's row recalls it,
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
    private const int Tile = 40;
    private const int LogLines = 30;
    private const int RecallRowsShown = 40;
    private static readonly Vector2 Board = new(16, 48);

    private ClientSession? _client;
    private CampaignClient? _campaign;

    /// <summary>The roster unit selected on the between-map screen, which a ware is bought for and B benches.</summary>
    private string? _screenUnit;

    /// <summary>The between-map screen's lines as last drawn, each with what a click on it does, if anything.</summary>
    private List<(string Text, Action? Click, bool Selected)> _screen = new();
    private char[] _letters = Array.Empty<char>();
    private Coord? _hover;
    private string _error = "";
    private string? _screenshot;
    private int _framesDrawn;
    private bool _recallOpen;

    /// <summary>The side panel's lines as last drawn, each with the history state a click on it recalls, if any.</summary>
    private List<(string Text, int? Recall)> _panel = new();

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
            if (Arg(args, "--script") is { } script)
            {
                Ironwake.Client.Script.Apply(_client, File.ReadAllText(script));
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
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click when PanelRecallAt(click.Position) is { } state:
                _client.Recall(state);
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
        }
    }

    /// <summary>The between-map screen's input: a click on a row does what it offers; M marches; B benches or unbenches the selected unit.</summary>
    private void ScreenInput(InputEvent input)
    {
        var campaign = _campaign!;
        switch (input)
        {
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click:
                var line = (int)Mathf.Floor((click.Position.Y - 48 + 13) / 16);
                if (line >= 0 && line < _screen.Count && _screen[line].Click is { } action)
                {
                    action();
                }

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
        var at = new Coord((int)Mathf.Floor(local.X / Tile), (int)Mathf.Floor(local.Y / Tile));
        return _client.State.Map.Contains(at) ? at : null;
    }

    /// <summary>The side panel's top-left, where its first line's baseline sits.</summary>
    private Vector2 PanelOrigin => new(Board.X + _client!.State.Map.Width * Tile + 16, 48);

    /// <summary>The history state of the Recall browser row under a click on the side panel, or null.</summary>
    private int? PanelRecallAt(Vector2 position)
    {
        if (_client is null || !_recallOpen || position.X < PanelOrigin.X)
        {
            return null;
        }

        var line = (int)Mathf.Floor((position.Y - PanelOrigin.Y + 13) / 16);
        return line >= 0 && line < _panel.Count ? _panel[line].Recall : null;
    }

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;
        if (_campaign is not null && _client is null)
        {
            DrawString(font, new Vector2(16, 28), _campaign.Over ? "campaign over" : "between maps", fontSize: 18, modulate: Colors.White);
            _screen = ScreenLines().ToList();
            for (var line = 0; line < _screen.Count; line++)
            {
                var (text, click, selected) = _screen[line];
                var colour = selected ? Colors.Yellow : click is null ? Colors.White : new Color(0.6f, 0.8f, 1f);
                DrawString(font, new Vector2(16, 48 + line * 16), text, fontSize: 13, modulate: colour);
            }

            return;
        }

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

        var panel = PanelOrigin;
        _panel = PanelLines().ToList();
        for (var line = 0; line < _panel.Count; line++)
        {
            var (text, recall) = _panel[line];
            DrawString(font, panel + new Vector2(0, line * 16), text, fontSize: 13, modulate: recall is null ? Colors.White : new Color(0.6f, 0.8f, 1f));
        }
    }

    private IEnumerable<(string Text, int? Recall)> PanelLines()
    {
        foreach (var text in StatusLines())
        {
            yield return (text, null);
        }

        if (_recallOpen)
        {
            yield return ("-- recall: click a state to rewind to it; R closes --", null);
            var rows = _client!.RecallRows;
            var states = rows.Where(r => r.State is not null).ToList();
            foreach (var row in rows.Where(r => r.State is null))
            {
                yield return (row.Text, null);
            }

            if (states.Count > RecallRowsShown)
            {
                yield return ($"  ({states.Count - RecallRowsShown} earlier states not shown; recall list in the console has them all)", null);
            }

            foreach (var row in states.Skip(Math.Max(0, states.Count - RecallRowsShown)))
            {
                yield return (row.Text, row.State);
            }

            yield break;
        }

        foreach (var text in BoardLines())
        {
            yield return (text, null);
        }
    }

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

        if (client.EnemyPhasePlaying)
        {
            yield return "enemy phase: Space steps, C continues";
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

    private IEnumerable<string> BoardLines()
    {
        var client = _client!;
        if (_hover is { } tile && client.Selected is not null)
        {
            foreach (var forecast in client.Hover(tile))
            {
                foreach (var text in forecast.Text.Split('\n'))
                {
                    yield return text;
                }
            }

            if (client.Threat(tile) is { } threat)
            {
                foreach (var text in threat.Split('\n'))
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
