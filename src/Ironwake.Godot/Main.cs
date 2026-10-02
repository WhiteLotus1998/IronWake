using Godot;
using Ironwake.Cli;
using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Core;
using CoreSide = Ironwake.Core.Side;

namespace Ironwake.Godot;

/// <summary>
/// The thin renderer, slices 1 and 2 and the readability pass (issues 347, 353, 349), drawn in the
/// showcase's look since its slice 1 (issue 511, <c>Main.Look.cs</c>): tiles and a legend in
/// <see cref="LookPalette"/>'s colours, the dark shaded, units as class silhouettes on the sides'
/// discs with their names and HP, the captain crowned and a Seize map's gate framed, the enemy's
/// threat hatched on T, the objective atop the panel and a lost battle's reason under it (issue 374), the selected unit's reach, the forecast from a hovered tile against
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
/// ends the phase, after which the enemy phase plays itself (<c>Main.Motion.cs</c>, issue 513) at
/// the speed S cycles; Space reveals its next event at once and marks it on the board, C skips
/// to its end, R opens or closes the Recall browser, where a click on a state's row recalls it,
/// Tab opens the event log under the forecast block (<c>--log-open</c> opens with it), Escape closes the
/// attack menu, else clears the selection and closes the browser, else opens the pause menu
/// (<c>Main.Screens.cs</c>, issue 629; <c>--paused</c> opens with it), as a click on the footer's Esc does. A click on an enemy with more
/// than one legal way to strike opens the attack menu (issue 611): 1 to 9 or a hover shows a row's
/// card, a click on it or Enter strikes. An exported build finds <c>content/</c>
/// beside its executable. Launched with nothing that names a battle (<see cref="Screens.OpensCampaign"/>,
/// issue 786), a double-click among them, it opens on the campaign's title, whose One battle opens
/// the showcase's title (<c>Main.Screens.cs</c>, issue 515), whose Play opens the Tollgate with the
/// turn-1 callouts; a decided battle shows the end card, Enter playing again on the next seed.
/// <c>--screen title</c> opens the showcase's title alone, as the exported build did before.
/// With <c>--campaign</c> (issue 360) or a bare launch it plays the campaign through <see cref="CampaignClient"/>
/// instead, from the first map or from <c>--from &lt;map&gt;</c>: the between-map screen shows the
/// console's roster, shop, deployment and keep lines; a click on a unit's row selects it, a click on
/// a ware buys it for that unit, B benches or unbenches it, a camp action's row (<see cref="CampActions"/>,
/// issue 786) takes that command, Q picks the unit for a side map's party, the wheel scrolls, M or
/// a click on the march row marches, and L leaves a decided battle. <c>--campaign-parity &lt;script&gt; &lt;out&gt;</c> writes the
/// presenter's event log for a <c>campaign --script</c> file and quits, from <c>--from</c>,
/// <c>--difficulty</c> and <c>--permadeath on|off</c> as the console's campaign reads them.
/// </summary>
public partial class Main : Node2D
{
    /// <summary>The layout at the UI scale in force (issue 698): the canvas's size, the column's width, the top bar's and the key strip's rows.</summary>
    private UiLayout _layout = UiLayout.For(100);

    /// <summary>The canvas's width at the UI scale in force: 1280 at 100, narrower as the type grows.</summary>
    private int ViewWidth => _layout.ViewWidth;

    /// <summary>The canvas's height at the UI scale in force.</summary>
    private int ViewHeight => _layout.ViewHeight;

    private const int Margin = UiLayout.Margin;

    /// <summary>Where the board's block starts, under the top bar's one or two rows.</summary>
    private int Top => _layout.Top;

    /// <summary>The column's width at the UI scale in force.</summary>
    private int PanelWidth => _layout.PanelWidth;

    private const int FontSize = 14;
    private const int LineHeight = 17;
    private const int LogLines = 30;
    private const int RecallRowsShown = 40;

    /// <summary>The room under the board for its legend, counted in the block that is centred on the screen.</summary>
    private const int LegendRoom = UiLayout.LegendRoom;

    /// <summary>The lowest baseline the key strip leaves free.</summary>
    private int FooterTop => _layout.FooterTop;

    /// <summary>
    /// How far the board and the column drop below the top bar (issue 512): the board, its legend
    /// and the column beside them are one block centred between the top bar and the footer, so a
    /// short map leaves an even margin rather than a dead quadrant under its legend.
    /// </summary>
    private float Lift => _client is null ? 0 : Math.Max(0, (FooterTop - Top - (_client.State.Map.Height * _tile + LegendRoom)) / 2f);

    /// <summary>The gap between the board's right edge and the column (issue 513): fixed, so the column follows the board.</summary>
    private const int ColumnGap = UiLayout.ColumnGap;

    /// <summary>The board's left edge: the board, the gap and the column are one block centred across the window (issue 513).</summary>
    private float Left => _client is null ? Margin : Math.Max(Margin, (ViewWidth - (_client.State.Map.Width * _tile + ColumnGap + PanelWidth + 12)) / 2f);

    private Vector2 Board => new(Left, Top + Lift);

    /// <summary>The column's top left, level with the board's top, a fixed gap right of the board.</summary>
    private Vector2 PanelOrigin => new(_client is null ? ViewWidth - PanelWidth - Margin : Board.X + _client.State.Map.Width * _tile + ColumnGap, Board.Y + 12);

    /// <summary>The column's lowest baseline (issue 512): its height follows the board's and its legend's, not the window's.</summary>
    private float PanelBottom => _client is null ? FooterTop : _layout.ColumnBottom(Board.Y + _client.State.Map.Height * _tile, PanelOrigin.Y, ForecastHeight + 4 * LineHeight);

    private static readonly Color Background = UiColour("ink");
    private static readonly Color Box = UiColour("panel");
    private static readonly Color Ink = UiColour("text");
    private static readonly Color Muted = UiColour("muted");
    private static readonly Color Rule = UiColour("lost");
    private static readonly Color Link = MarkColour("reach");
    private static readonly Color Warn = MarkColour("selected");

    /// <summary>The player's own marks: the pointer, the selection and the between-map screen's selected row.</summary>
    private static readonly Color Mark = MarkColour("selected");

    /// <summary>The enemy phase's marks on the board and in the log: bone, the enemy's own light colour.</summary>
    private static readonly Color EnemyMark = Look(LookPalette.EnemyBone);

    /// <summary>The console's monospaced face for every console line (the log, the forecast and unit text), so numbers align as they do in the console.</summary>
    private readonly Font _mono = LoadFont("JetBrainsMono-Regular.ttf", tabular: false, "DejaVu Sans Mono", "Consolas", "Menlo", "monospace");

    /// <summary>The UI face (LOOK.md: Inter, OFL) with tabular numerals, for everything that is not a console line.</summary>
    private readonly Font _ui = LoadFont("Inter-Regular.ttf", tabular: true, "DejaVu Sans", "Segoe UI", "Helvetica", "sans-serif");

    /// <summary>The UI face at 700, for names, numerals and chips.</summary>
    private readonly Font _bold = LoadFont("Inter-Bold.ttf", tabular: true, "DejaVu Sans", "Segoe UI", "Helvetica", "sans-serif");

    /// <summary>The panel titles' spaced capitals: the UI face at 700 with its letters set apart.</summary>
    private readonly FontVariation _caps;

    /// <summary>Whether the board hatches every tile a seen enemy could strike next phase, a tile no unit can stand on left bare; T toggles it.</summary>
    private bool _threatShown;

    /// <summary>Whether the column shows the event log (issue 512); Tab toggles it, and while it is closed the newest line stays on screen as one row.</summary>
    private bool _logOpen;

    /// <summary>The tile size for the open map, the largest up to 44 px that leaves room for the legend and the panel.</summary>
    private int _tile = 40;

    /// <summary>How many characters of the monospaced face fit across the panel, measured over ten so one glyph's rounding cannot overflow a line.</summary>
    private int Columns => (int)((PanelWidth - 4) / (_mono.GetStringSize("MMMMMMMMMM", fontSize: FontSize).X / 10));

    /// <summary>The clickable regions drawn last frame (Recall rows, between-map rows) and what a click on each does.</summary>
    private readonly List<(Rect2 Area, Action Click)> _hits = new();

    /// <summary>The attack menu's rows as last drawn (issue 611), so hovering one shows its card.</summary>
    private readonly List<Rect2> _menuRects = new();

    private ClientSession? _client;
    private CampaignClient? _campaign;

    /// <summary>The loaded content and the folder it came from, kept so the title's Play and the end card's play again can open a battle.</summary>
    private GameContent? _content;
    private string _contentDir = "";

    /// <summary>The map and seed the open battle was started from, for play again.</summary>
    private string _mapArg = "the_tollgate";
    private ulong _seed = 1;

    /// <summary>The roster unit selected on the between-map screen, which a ware is bought for and B benches.</summary>
    private string? _screenUnit;

    /// <summary>The allies picked with Q for a side map that takes more than one (issue 786), in the order picked.</summary>
    private readonly List<string> _screenParty = new();

    /// <summary>How many wrapped rows the between-map screen is scrolled past (issue 786), by the wheel or Page Up and Page Down.</summary>
    private int _screenScroll;

    private Coord? _hover;

    /// <summary>The pointer's position, for the legend's terrain hover (issue 610).</summary>
    private Vector2? _mouse;

    /// <summary>Each terrain entry the legend drew this frame, by its swatch and name, for the hover card (issue 610).</summary>
    private readonly List<(Rect2 Rect, string Id)> _legendTerrain = new();
    private string _error = "";
    private string? _screenshot;
    private int _framesDrawn;

    public Main()
    {
        _caps = new FontVariation { BaseFont = _bold, SpacingGlyph = 2 };
    }

    public override void _Ready()
    {
        var args = OS.GetCmdlineUserArgs();
        var contentDir = Arg(args, "--content") ?? DefaultContent();
        var mapArg = Arg(args, "--map") ?? "the_tollgate";
        var seed = ulong.TryParse(Arg(args, "--seed"), out var parsed) ? parsed : 1UL;
        try
        {
            var content = ContentLoader.Load(contentDir);
            (_content, _contentDir, _mapArg, _seed) = (content, contentDir, mapArg, seed);
            var campaignParity = Array.IndexOf(args, "--campaign-parity");
            if (campaignParity >= 0 && campaignParity + 2 < args.Length)
            {
                // The full-campaign script (issue 786) names the start it was written on, as the console's campaign does.
                var parityFrom = Arg(args, "--from");
                var parityDifficulty = Arg(args, "--difficulty") ?? CampaignRecord.NormalDifficulty;
                var parityPermadeath = Arg(args, "--permadeath") != "off";
                _campaign = new CampaignClient(content, contentDir, parityFrom is null
                    ? CampaignRecord.Start(content, seed, parityDifficulty, parityPermadeath)
                    : CampaignRecord.StartAt(content, seed, parityFrom, parityDifficulty, parityPermadeath));
                File.WriteAllText(args[campaignParity + 2], Ironwake.Client.Script.PlayCampaign(_campaign, File.ReadAllText(args[campaignParity + 1])));
                GetTree().Quit(0);
                return;
            }

            // The profile's options (issue 677) hold for a run a person plays; a render, a strip or
            // a parity run keeps the defaults and never writes the profile.
            var rendered = Array.IndexOf(args, "--screenshot") >= 0 || Array.IndexOf(args, "--strip") >= 0 || Array.IndexOf(args, "--parity") >= 0;
            if (!rendered || Arg(args, "--saves") is not null)
            {
                _store = new SaveStore(SavesDir(args));
            }

            if (!rendered)
            {
                LoadOptions();
            }

            // A render names its UI scale (issue 698), so the shots at 125 and 150 never read a profile.
            if (Arg(args, "--ui-scale") is { } uiScale)
            {
                _options = Options.Set(_options, "ui-scale", uiScale).Options;
            }

            ApplyScale();

            // A bare launch, a double-click on the exported build, opens the campaign (issue 786).
            if (Screens.OpensCampaign(args))
            {
                // A named start map plays straight in; otherwise the campaign's title comes first.
                var from = Arg(args, "--from");
                if (from is not null || rendered && Arg(args, "--screen") is null)
                {
                    _campaign = new CampaignClient(content, contentDir, from is null ? CampaignRecord.Start(content, seed) : CampaignRecord.StartAt(content, seed, from), saves: rendered ? null : _store);
                }
                else
                {
                    _screen = Arg(args, "--screen") switch
                    {
                        "options" => Screen.Options,
                        "new-game" => Screen.NewGame,
                        "load" => Screen.Load,
                        _ => Screen.CampaignTitle,
                    };
                    (_optionsBack, _newDifficulty, _campaignHome) = (Screen.CampaignTitle, NewGameDefault(), true);
                }

                _screenshot = Arg(args, "--screenshot");
                StartSound();
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

            // The exported build opens on the title (issue 515): no map named, nothing to replay.
            if (Arg(args, "--screen") is { } screen)
            {
                _screen = screen switch
                {
                    "howto" => Screen.HowTo,
                    "options" => Screen.Options,
                    _ => Screen.Title,
                };
            }
            else if (Arg(args, "--map") is null && Array.IndexOf(args, "--script") < 0 && Array.IndexOf(args, "--screenshot") < 0 && Array.IndexOf(args, "--strip") < 0)
            {
                _screen = Screen.Title;
            }

            _client = new ClientSession(content, state);
            _tile = TileFor(map);
            _callouts = Array.IndexOf(args, "--callouts") >= 0 ? new Callouts() : null;
            _client.SceneSetting = Scenes.FromOption(Arg(args, "--scenes") switch
            {
                "all" or "map" => Arg(args, "--scenes")!,
                _ => _options.Scenes,
            });
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
            _client.RecallOpen = Array.IndexOf(args, "--recall") >= 0;
            _paused = Array.IndexOf(args, "--paused") >= 0;
            _threatShown = Array.IndexOf(args, "--threat") >= 0;
            if (Arg(args, "--speed") is { } speed)
            {
                _speed = GameSpeed.IndexOf(speed);
            }

            _confirm = Array.IndexOf(args, "--confirm") >= 0 ? EndTurnConfirm.Lines(_client.State, content, true) : null;
            _logOpen = Array.IndexOf(args, "--log-open") >= 0;
            _screenshot = Arg(args, "--screenshot");
            var strip = Array.IndexOf(args, "--strip");
            if (strip >= 0 && strip + 3 < args.Length && int.TryParse(args[strip + 2], out var count) && float.TryParse(args[strip + 3], System.Globalization.CultureInfo.InvariantCulture, out var every))
            {
                (_stripPrefix, _stripCount, _stripEvery) = (args[strip + 1], count, every);
            }

            _still = _screenshot is not null;
            _recallAfter = int.TryParse(Arg(args, "--recall-after"), out var recallAfter) ? recallAfter : null;
            SyncBeats();
            StartSound();
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
        if (!_paused)
        {
            Advance(delta);
        }

        SoundBeats();
        SaveStripFrame();
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
        if (OnMenuScreen)
        {
            MenuScreenInput(input);
            QueueRedraw();
            return;
        }

        if (StoryCardShown)
        {
            StoryCardInput(input);
            QueueRedraw();
            return;
        }

        if (_campaign is not null && _client is null)
        {
            ScreenInput(input);
            return;
        }

        if (_client is null)
        {
            return;
        }

        if (_screen != Screen.Battle)
        {
            TitleInput(input);
            QueueRedraw();
            return;
        }

        if (EndCardShown && EndCardInput(input))
        {
            QueueRedraw();
            return;
        }

        if (_paused)
        {
            PauseInput(input);
            QueueRedraw();
            return;
        }

        if (ConfirmInput(input))
        {
            QueueRedraw();
            return;
        }

        switch (input)
        {
            case InputEventMouseMotion motion:
                _hover = TileAt(motion.Position);
                _mouse = motion.Position;
                if (_menuRects.FindIndex(rect => rect.HasPoint(motion.Position)) is var row and >= 0)
                {
                    _client.MenuHover(row);
                }

                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click when TileAt(click.Position) is { } at:
                Play(Cue.Click);
                _client.Click(at);
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click when HitAt(click.Position) is { } hit:
                Play(Cue.Click);
                hit();
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }:
                if (!_client.CanTakeBack || !_client.TakeBack())
                {
                    _client.ClearSelection();
                }

                break;
            case InputEventKey { Pressed: true, Echo: false } key:
                switch (key.Keycode)
                {
                    case Key.E:
                        EndPressed();
                        break;
                    case Key.Space:
                        FinishBeats();
                        _client.Step();
                        break;
                    case Key.C:
                        _client.Continue();
                        SyncBeats();
                        FinishBeats();
                        break;
                    case Key.S:
                        PickSpeed(GameSpeed.Next(_speed));
                        break;
                    case Key.R:
                        _client.RecallOpen = !_client.RecallOpen;
                        break;
                    case Key.T:
                        _threatShown = !_threatShown;
                        break;
                    case Key.Tab:
                        _logOpen = !_logOpen;
                        break;
                    case Key.B:
                        CycleScenes();
                        break;
                    case Key.M:
                        ToggleMute();
                        break;
                    case Key.Escape:
                        switch (Screens.Escape(_client.Menu is not null, _client.Selected is not null, _client.RecallOpen, _campaign is not null, _client.CanTakeBack))
                        {
                            case EscapeAction.CloseAttackMenu:
                                _client.CloseMenu();
                                break;
                            case EscapeAction.TakeBack:
                                if (!_client.TakeBack())
                                {
                                    _client.ClearSelection();
                                }

                                break;
                            case EscapeAction.Clear:
                                _client.ClearSelection();
                                _client.RecallOpen = false;
                                break;
                            default:
                                _paused = true;
                                break;
                        }

                        break;
                    case >= Key.Key1 and <= Key.Key9 when _client.Menu is not null:
                        _client.MenuHover((int)(key.Keycode - Key.Key1));
                        break;
                    case Key.Enter or Key.KpEnter when _client.Menu is { } menu:
                        _client.Choose(menu.Hovered);
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
        (_hover, _confirm) = (null, null);
        if (_client is not null)
        {
            _tile = TileFor(_client.State.Map);
            _client.SceneSetting = Scenes.FromOption(_options.Scenes);
        }
    }

    /// <summary>The tile (issue 513): as large as fills the height the block allows under the top bar and above the legend and footer, and never so wide the column does not fit.</summary>
    private int TileFor(MapDefinition map) => _layout.TileFor(map.Width, map.Height);

    /// <summary>
    /// The between-map screen's input: a click on a row does what it offers; M marches; B benches or
    /// unbenches the selected unit; Q adds it to or takes it from a side map's party (issue 786); the
    /// wheel and Page Up and Page Down scroll the screen.
    /// </summary>
    private void ScreenInput(InputEvent input)
    {
        var campaign = _campaign!;
        switch (input)
        {
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click:
                PressAt(click.Position);
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
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.Q } when _screenUnit is { } picked:
                if (!_screenParty.Remove(picked))
                {
                    _screenParty.Add(picked);
                }

                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown }:
                _screenScroll += 3;
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp }:
                _screenScroll = Math.Max(0, _screenScroll - 3);
                break;
            case InputEventKey { Pressed: true, Keycode: Key.Pagedown }:
                _screenScroll += 20;
                break;
            case InputEventKey { Pressed: true, Keycode: Key.Pageup }:
                _screenScroll = Math.Max(0, _screenScroll - 20);
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
            yield return (CampaignSession.CampPanels[0], null, false);
            yield return ("click a unit to select it; B benches or unbenches it", null, false);
            foreach (var unit in record.Roster)
            {
                var id = unit.Id;
                yield return (CampaignSession.UnitLines(record, content, unit, detail: false)[0], () => _screenUnit = id, id == _screenUnit);
            }

            foreach (var text in CampaignSession.RosterPanelLines(record, content, map).Skip(record.Roster.Count))
            {
                yield return (text, null, false);
            }

            yield return (CampaignSession.CampPanels[1], null, false);
            foreach (var text in campaign.KeepPanelLines())
            {
                yield return (text, null, false);
            }

            yield return (CampaignSession.CampPanels[2], null, false);
            foreach (var text in campaign.QuestPanelLines())
            {
                yield return (text, null, false);
            }

            yield return (CampaignSession.CampPanels[3], null, false);
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

            // Every other camp command as a row (issue 786); each row names the console command it is.
            _screenParty.RemoveAll(id => record.Find(id) is null);
            yield return ("== Camp actions ==", null, false);
            var party = _screenParty.Count == 0 ? "" : $"; side map party: {string.Join(", ", _screenParty)}";
            yield return ((_screenUnit is null ? "select a unit for its own actions" : $"actions for {_screenUnit}") + "; Q adds the selected unit to a side map's party or takes it out" + party, null, false);
            foreach (var action in CampActions.For(campaign, _screenUnit, _screenParty))
            {
                var run = action.Run;
                yield return ($"  {action.Text}  ({action.Command})", () => run(), false);
            }

            yield return ($"[ {CampaignSession.MarchLine(record, content, map)} ]  (M)", () => campaign.March(), false);
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

    /// <summary>The transform every drawing starts from: none, or the UI scale's inverse inside <see cref="AtFullSize"/>.</summary>
    private Transform2D _frame = Transform2D.Identity;

    /// <summary>
    /// Draws a composed screen at its 100 size whatever the UI scale (issue 698): the titles, New
    /// game, Load, how to play and the battle scene are laid out for the whole 1280x720 window
    /// with type from 12 up, and a smaller canvas would push them off it. They draw under the
    /// scale's inverse with the layout at 100, and their click regions are brought back into the
    /// canvas's coordinates, where a click arrives.
    /// </summary>
    private void AtFullSize(Action draw)
    {
        var layout = _layout;
        var from = _hits.Count;
        var shrink = 1 / layout.Factor;
        (_layout, _frame) = (UiLayout.For(100), Transform2D.Identity.Scaled(new Vector2(shrink, shrink)));
        DrawSetTransformMatrix(_frame);
        try
        {
            draw();
        }
        finally
        {
            (_layout, _frame) = (layout, Transform2D.Identity);
            DrawSetTransformMatrix(_frame);
        }

        for (var i = from; i < _hits.Count; i++)
        {
            var (area, click) = _hits[i];
            _hits[i] = (new Rect2(area.Position * shrink, area.Size * shrink), click);
        }
    }

    /// <summary>What a click at <paramref name="position"/> does on the regions drawn last frame, or null.</summary>
    private Action? HitAt(Vector2 position) => _hits.LastOrDefault(hit => hit.Area.HasPoint(position)).Click;

    public override void _Draw()
    {
        _hits.Clear();
        DrawRect(new Rect2(Vector2.Zero, new Vector2(ViewWidth, ViewHeight)), Background);
        if (OnMenuScreen)
        {
            DrawMenuScreen();
            return;
        }

        if (StoryCardShown)
        {
            DrawStoryCard();
            return;
        }

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

        if (_screen != Screen.Battle)
        {
            AtFullSize(DrawTitleOrHowTo);
            return;
        }

        var state = Shown();
        DrawTopBar(state);
        DrawBoard(state);
        DrawLegend(state);
        DrawPanel();
        DrawKeys();
        DrawCallout();
        DrawTerrainHover();
        DrawBattleScene();
        DrawEndCard();
        DrawConfirm();
        DrawPauseMenu();
    }

    /// <summary>
    /// The top bar (LOOK.md's layout): the map's name, then chips for the turn, the phase (amber
    /// when it is ours), the goal and the Recall charges; a decided battle's verdict replaces them.
    /// </summary>
    private void DrawTopBar(BattleState state)
    {
        UiText(new Vector2(Margin, 31), state.Map.Name, Ink, 20, bold: true);
        var x = Margin + UiWidth(state.Map.Name, 20, bold: true) + 18;
        if (state.Outcome.IsOver)
        {
            Chip(x, state.Outcome.Result == BattleResult.Won ? "WON" : "LOST", state.Outcome.Reason, Ink);
            return;
        }

        // The chip flips with the act card (issue 515): Theirs while the enemy's act is on show.
        var ours = state.Phase == CoreSide.Player && _client!.ActShown(Animating) is null;
        foreach (var (label, value, colour) in new[]
        {
            ("TURN", $"{state.Turn} / {state.Map.TurnLimit}", Ink),
            ("PHASE", ours ? "Yours" : "Theirs", ours ? Look(LookPalette.Player) : Look(LookPalette.EnemyBone)),
            ("GOAL", MapRenderer.WinName(state.Map.Win).Replace('_', ' '), Ink),
        })
        {
            x = Chip(x, label, value, colour) + 8;
        }

        RecallChip(x, state);

        // At a larger UI scale the speed buttons and the scenes chip take the bar's second row (issue 698).
        var down = (_layout.TopRows - 1) * UiLayout.TopRowHeight;
        DrawSpeedButtons(DrawSceneChip(down) - 8, down);
    }

    /// <summary>
    /// The game speed (issue 625): a chip labelled GAME SPEED with four buttons, one, two and
    /// three filled triangles for 1x, 2x and 5x, and three against a bar for instant (issue 677),
    /// ending at <paramref name="right"/>. The speed in force is filled amber with dark triangles;
    /// the others are bare with muted ones. A click picks one and the profile keeps it.
    /// </summary>
    private void DrawSpeedButtons(float right, float down)
    {
        const float button = 30, gap = 4, side = 8;
        var label = GameSpeed.Label.ToUpperInvariant();
        var labelWidth = UiWidth(label, 10, bold: true);
        var width = 14 + labelWidth + 10 + GameSpeed.Speeds.Length * (button + gap) - gap + 6;
        var x = right - width;
        Card(new Rect2(x, 12 + down, width, 28), Box, 14);
        UiText(new Vector2(x + 14, 30 + down), label, Muted, 10, bold: true);
        var amber = Look(LookPalette.Player);
        for (var i = 0; i < GameSpeed.Speeds.Length; i++)
        {
            var rect = new Rect2(x + 14 + labelWidth + 10 + i * (button + gap), 15 + down, button, 22);
            var chosen = i == _speed;
            if (chosen)
            {
                Card(rect, amber, 11);
            }

            var (_, count, bar, _) = GameSpeed.Speeds[i];
            var left = rect.GetCenter().X - (count * (side - 1) + (bar ? 3 : 0)) / 2f;
            var mid = rect.GetCenter().Y;
            for (var t = 0; t < count; t++)
            {
                var tx = left + t * (side - 1);
                DrawColoredPolygon(new[] { new Vector2(tx, mid - side / 2), new Vector2(tx + side - 1, mid), new Vector2(tx, mid + side / 2) }, chosen ? Background : Muted);
            }

            // Instant (issue 677): the triangles end on a bar, as a skip-to-end key does.
            if (bar)
            {
                DrawRect(new Rect2(left + count * (side - 1) + 1, mid - side / 2, 2, side), chosen ? Background : Muted);
            }

            var index = i;
            _hits.Add((rect, () => PickSpeed(index)));
        }
    }

    /// <summary>
    /// The RECALL chip (issue 514): a pip per charge the map opened with, filled while left and
    /// hollow once spent, ringed in amber while a death on the enemy phase pulses it, and reading
    /// REWIND while a scrub plays.
    /// </summary>
    private void RecallChip(float x, BattleState state)
    {
        var total = Math.Max(_client!.RecallChargesAtStart, state.RecallCharges);
        var label = Scrubbing ? "REWIND" : "RECALL";
        var labelWidth = UiWidth(label, 10, bold: true);
        var width = labelWidth + 36 + total * 14;
        var rect = new Rect2(x, 12, width, 28);
        var pulse = PulseNow();
        if (pulse > 0)
        {
            // The ring follows the chip's rounded ends (issue 515): a square ring reads as keyboard focus.
            var grow = 3 + 3 * pulse;
            Ring(rect.Grow(grow), Look(LookPalette.Player, 0.25f + 0.55f * pulse), 14 + grow, 2 + 2 * pulse);
        }

        Card(rect, Box, 14);
        UiText(new Vector2(x + 14, 30), label, Scrubbing || pulse > 0 ? Look(LookPalette.Player) : Muted, 10, bold: true);
        var amber = Look(LookPalette.Player);
        for (var i = 0; i < total; i++)
        {
            var centre = new Vector2(x + 22 + labelWidth + 7 + i * 14, 26);
            if (i < state.RecallCharges)
            {
                DrawCircle(centre, 5 * (1 + 0.25f * pulse), amber);
            }
            else
            {
                DrawArc(centre, 4.5f, 0, Mathf.Tau, 20, Muted, 1.5f, antialiased: true);
            }
        }
    }

    /// <summary>A chip: a spaced-capital label and a bold value on a rounded panel, <paramref name="down"/> below the bar's first row; returns its right edge.</summary>
    private float Chip(float x, string label, string value, Color colour, float down = 0)
    {
        var width = UiWidth(label, 10, bold: true) + UiWidth(value, 14, bold: true) + 36;
        Card(new Rect2(x, 12 + down, width, 28), Box, 14);
        UiText(new Vector2(x + 14, 30 + down), label, Muted, 10, bold: true);
        UiText(new Vector2(x + 22 + UiWidth(label, 10, bold: true), 31 + down), value, colour, 14, bold: true);
        return x + width;
    }

    /// <summary>The footer: each key as a keycap and what it does, wrapping onto a second row at a larger UI scale (issue 698).</summary>
    private void DrawKeys()
    {
        var spans = KeyStrip.Keys.Select(k => UiWidth(k.Key, 12, bold: true) + 14 + 7 + UiWidth(k.Does, 12)).ToList();
        var rows = UiLayout.Wrap(spans, ViewWidth - 2 * Margin, 14);
        var x = (float)Margin;
        var y = ViewHeight - 24 - (_layout.KeyRows - 1) * UiLayout.KeyRowHeight;
        for (var i = 0; i < KeyStrip.Keys.Length; i++)
        {
            var (key, does) = KeyStrip.Keys[i];
            if (i > 0 && rows[i] != rows[i - 1])
            {
                (x, y) = (Margin, y + UiLayout.KeyRowHeight);
            }

            var width = UiWidth(key, 12, bold: true) + 14;
            if (key == "E")
            {
                _endKey = new Rect2(x, y - 7, width, 22);
            }

            Card(new Rect2(x, y - 7, width, 22), Box, 5);
            UiText(new Vector2(x + width / 2, y + 9), key, Ink, 12, bold: true, centred: true);
            UiText(new Vector2(x + width + 7, y + 9), does, Muted, 12);
            if (key == "Esc" && _campaign is null)
            {
                // The menu button (issue 629): a click on Esc's keycap or its label opens the pause menu.
                _hits.Add((new Rect2(x, y - 11, width + 7 + UiWidth(does, 12), 30), () => _paused = true));
            }

            x += width + 7 + UiWidth(does, 12) + 14;
        }
    }

    /// <summary>
    /// The board (issue 511): the mat, each tile's ground and detail, the walls' shadows, the grid,
    /// the dark, exits, then the marks (the threat hatch when shown, the reach), the enemy phase's
    /// path, the units, the enemy phase's rings, and the pointer on top.
    /// </summary>
    private void DrawBoard(BattleState state)
    {
        var map = state.Map;
        var reach = _client!.Reach;
        var dusk = Dusk.Sight(state) is not null;
        DrawMat(map);
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var at = new Coord(x, y);
                DrawGround(map, at, map.TerrainIdAt(at));
            }
        }

        // The threat hatch goes under the terrain's ink (round 138): the pines sit on the danger.
        // It draws every seen enemy's reach on T, or the one enemy a click inspected (issue 533);
        // a sleeping group's reach is faint, and its wake ring is outlined after the grid.
        var reaches = ShownReaches();
        var threat = reaches.Where(r => !r.Asleep).SelectMany(r => r.Tiles).ToHashSet();
        var faint = reaches.Where(r => r.Asleep).SelectMany(r => r.Tiles).Where(at => !threat.Contains(at)).ToHashSet();
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var at = new Coord(x, y);
                var standable = _client.Content.TerrainById(map.TerrainIdAt(at)).MoveCosts.Any(cost => cost is not null);
                if (threat.Contains(at) && standable)
                {
                    DrawThreatMark(at);
                }
                else if (faint.Contains(at) && standable)
                {
                    DrawThreatMark(at, faint: true);
                }

                DrawDetail(map, at, map.TerrainIdAt(at));
            }
        }

        DrawShadows(map);
        DrawGrid(map);
        DrawWakeRing(reaches.Where(r => r.Asleep).SelectMany(r => r.WakeRing).ToHashSet());
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var at = new Coord(x, y);
                var rect = Cell(at);
                if (dusk && !Dusk.Sees(state, CoreSide.Player, at))
                {
                    DrawRect(rect, UiColour("ink", 0.62f));
                    // The hatch laid again over the dark (issue 601): under the veil it read dark
                    // on dark, and it is the priced part of an unseen tile.
                    var standable = _client.Content.TerrainById(map.TerrainIdAt(at)).MoveCosts.Any(cost => cost is not null);
                    if (standable && (threat.Contains(at) || faint.Contains(at)))
                    {
                        DrawThreatMark(at, faint: !threat.Contains(at), unseen: true);
                    }
                }

                if (map.IsExit(at))
                {
                    DrawRect(rect.Grow(-3 * S), Ink, filled: false, width: 3 * S);
                }

                if (map.Win == WinCondition.Seize && map.IsThrone(at))
                {
                    DrawRect(rect.Grow(-2 * S), UiColour("ink"), filled: false, width: 2 * S);
                }

                if (reach is not null && reach.CanEnd(at))
                {
                    DrawReachMark(at);
                }
            }
        }

        // A chest is the map's, not a unit, so dusk never hides it (dusk hides what, never where).
        foreach (var chest in _client.Chests)
        {
            DrawChest(chest);
        }

        if (_client.Playing is { } walked)
        {
            DrawPath(walked);
        }

        DrawTokens(state);
        if (_client.Playing is { } mark)
        {
            DrawMark(mark);
        }

        if (_hover is { } hover)
        {
            DrawHoverMark(hover);
        }

        DrawPops();
        DrawHeld();
        if (Scrubbing)
        {
            // The rewind (issue 514): the board dims under an amber frame while it folds back.
            var board = new Rect2(Board, new Vector2(map.Width * _tile, map.Height * _tile));
            DrawRect(board, UiColour("ink", 0.16f));
            DrawRect(board.Grow(3 * S), Look(LookPalette.Player, 0.85f), filled: false, width: 3 * S);
        }
    }

    /// <summary>The enemy-phase path: the line walked from the start tile through the path to the end tile, bone on an ink edge, drawn under the units.</summary>
    private void DrawPath(Highlight mark)
    {
        var points = new List<Vector2>();
        if (mark.From is { } from)
        {
            points.Add(Cell(from).GetCenter());
        }

        points.AddRange(mark.Path.Select(tile => Cell(tile).GetCenter()));
        if (mark.To is { } to && mark.From is not null)
        {
            points.Add(Cell(to).GetCenter());
        }

        if (points.Count > 1)
        {
            DrawPolyline(points.ToArray(), UiColour("ink", 0.8f), 6 * S, antialiased: true);
            DrawPolyline(points.ToArray(), EnemyMark, 2.5f * S, antialiased: true);
        }
    }

    /// <summary>
    /// The enemy-phase mark over the units: the start tile's corners dotted in bone, the tile the
    /// unit ended on or acted from ringed in bone, and the tile it struck bracketed at its corners
    /// in the struck mark, each on an ink edge.
    /// </summary>
    private void DrawMark(Highlight mark)
    {
        if (mark.From is { } from)
        {
            var start = Cell(from).Grow(-5 * S);
            for (var i = 0; i < 4; i++)
            {
                var corner = start.Position + new Vector2(i % 2 * start.Size.X, i / 2 * start.Size.Y);
                DrawCircle(corner, 3.5f * S, UiColour("ink"));
                DrawCircle(corner, 2.5f * S, EnemyMark);
            }
        }

        if (mark.To is { } to)
        {
            DrawRect(Cell(to).Grow(-1), UiColour("ink"), filled: false, width: 5 * S);
            DrawRect(Cell(to).Grow(-1), EnemyMark, filled: false, width: 2.5f * S);
        }

        if (mark.Struck is { } struck)
        {
            var r = Cell(struck).Grow(-1);
            var arm = _tile * 0.3f;
            foreach (var (corner, dx, dy) in new[] { (r.Position, 1, 1), (new Vector2(r.End.X, r.Position.Y), -1, 1), (new Vector2(r.Position.X, r.End.Y), 1, -1), (r.End, -1, -1) })
            {
                foreach (var (colour, width) in new[] { (UiColour("ink"), 7 * S), (MarkColour("struck"), 3.5f * S) })
                {
                    DrawLine(corner, corner + new Vector2(dx * arm, 0), colour, width);
                    DrawLine(corner, corner + new Vector2(0, dy * arm), colour, width);
                }
            }
        }
    }

    /// <summary>
    /// The legend under the board: each terrain on this map by its swatch and name, then only the
    /// marks on screen now (LOOK.md): the sides, the captain, reach, threat, exits, the gate, the
    /// dark, and the enemy phase's act and strike.
    /// </summary>
    private void DrawLegend(BattleState state)
    {
        var map = state.Map;
        var y = Board.Y + map.Height * _tile + 30;
        var x = Board.X;
        var right = PanelOrigin.X - Margin;
        _legendTerrain.Clear();
        foreach (var id in TerrainCard.OnBoard(map))
        {
            var name = _client!.Content.TerrainById(id).Name;
            if (x + 22 + UiWidth(name, 12) > right)
            {
                x = Board.X;
                y += 20;
            }

            var swatch = new Rect2(x, y - 11, 14, 14);
            DrawRect(swatch, TerrainColour(id == "fire" ? "forest" : id));
            if (id == "fire")
            {
                Hatch(swatch, TerrainColour("fire"), 2, 5);
            }

            _legendTerrain.Add((new Rect2(x - 2, y - 14, 22 + UiWidth(name, 12), 20), id));
            x = LegendText(x + 20, y, name);
        }

        y += 22;
        x = Board.X;
        var entries = new List<(Action<Rect2> Swatch, string Label)>
        {
            (r => DrawCircle(r.GetCenter(), 7, Look(LookPalette.Player)), Legend.Player),
            (r =>
            {
                DrawCircle(r.GetCenter(), 7, Look(LookPalette.Enemy));
                DrawArc(r.GetCenter(), 6.5f, 0, Mathf.Tau, 24, EnemyMark, 1, antialiased: true);
            }, Legend.Enemy),
        };
        if (state.Units.Any(u => u.IsCaptain))
        {
            entries.Add((r =>
            {
                var c = r.GetCenter() + new Vector2(0, 5);
                var crown = new[] { new Vector2(-7, 0), new Vector2(-7, -8), new Vector2(-3, -3), new Vector2(0, -10), new Vector2(3, -3), new Vector2(7, -8), new Vector2(7, 0) }.Select(v => c + v).ToArray();
                DrawColoredPolygon(crown, MarkColour("captain"));
                DrawPolyline(crown.Append(crown[0]).ToArray(), UiColour("ink"), 1.2f, antialiased: true);
            }, "captain"));
        }

        if (_client!.Reach is not null)
        {
            entries.Add((r =>
            {
                Card(r, MarkColour("reach", 0.16f), 3);
                DrawRect(r, MarkColour("reach", 0.7f), filled: false, width: 1.5f);
            }, "can move"));
        }

        var reaches = ShownReaches();
        if (reaches.Any(r => !r.Asleep))
        {
            entries.Add((r => Hatch(r, MarkColour("threat", 0.6f), 1.5f, 5), _threatShown ? "enemy can strike" : "it can strike"));
        }

        if (reaches.Any(r => r.Asleep))
        {
            entries.Add((r => Hatch(r, MarkColour("threat", FaintThreat), 1.5f, 5), "if woken"));
            entries.Add((r => DashedRect(r, MarkColour("threat", 0.9f), 2, 4), "stop inside: wakes"));
        }

        if (map.Exits.Count > 0)
        {
            entries.Add((r => DrawRect(r, Ink, filled: false, width: 2), "exit"));
        }

        if (_client!.Chests.Count > 0)
        {
            // The chest rule on screen (issue 786): opened from on or beside it, as the action.
            entries.Add((r =>
            {
                var box = new Rect2(r.Position + new Vector2(1, r.Size.Y * 0.3f), new Vector2(r.Size.X - 2, r.Size.Y * 0.6f));
                DrawRect(box, Look(LookPalette.Player, 0.9f));
                DrawRect(box, UiColour("ink"), filled: false, width: 1);
            }, "chest: open on or beside it"));
        }

        if (Dusk.Sight(state) is not null)
        {
            entries.Add((r =>
            {
                DrawRect(r, TerrainColour("plain"));
                DrawRect(r, UiColour("ink", 0.62f));
                DrawRect(r, Muted, filled: false, width: 1);
            }, "unseen"));
        }

        if (_client.Playing is { } playing)
        {
            entries.Add((r => DrawRect(r, EnemyMark, filled: false, width: 2), "enemy act"));
            if (playing.Struck is not null)
            {
                entries.Add((r => DrawRect(r, MarkColour("struck"), filled: false, width: 2), "struck"));
            }
        }

        foreach (var (swatch, label) in entries)
        {
            if (x + 22 + UiWidth(label, 12) > right)
            {
                x = Board.X;
                y += 20;
            }

            swatch(new Rect2(x, y - 11, 14, 14));
            x = LegendText(x + 20, y, label);
        }
    }

    /// <summary>
    /// The terrain card (issue 610): while the pointer is on a terrain entry in the legend, what
    /// that ground does for a unit on it, the console's <c>terrain</c> text, in a box above the
    /// entry. Hovering a board tile is unchanged.
    /// </summary>
    private void DrawTerrainHover()
    {
        if (_mouse is not { } mouse || _legendTerrain.FirstOrDefault(e => e.Rect.HasPoint(mouse)) is not { Id: not null } entry)
        {
            return;
        }

        const float width = 340;
        var columns = (int)((width - 20) / (UiWidth("abcdefghijklmnopqrstuvwxyz", 12) / 26));
        var rows = TextLayout.Wrap(_client!.TerrainText(entry.Id), columns).ToList();
        var height = rows.Count * 17 + 14;
        var x = Math.Clamp(entry.Rect.Position.X, Margin, ViewWidth - Margin - width);
        var box = new Rect2(x, entry.Rect.Position.Y - height - 6, width, height);
        Card(box, Box, 6);
        DrawRect(box, Muted, filled: false, width: 1);
        var y = box.Position.Y + 20;
        foreach (var row in rows)
        {
            UiText(new Vector2(box.Position.X + 10, y), row, Ink, 12);
            y += 17;
        }
    }

    private float LegendText(float x, float y, string text)
    {
        UiText(new Vector2(x, y), text, Muted, 12);
        return x + UiWidth(text, 12) + 16;
    }

    /// <summary>
    /// The side panel, top to bottom: status, the enemy phase's keys, then either the Recall
    /// browser or the drawn forecast with the threat on a stop under it (issue 512; boxed, first,
    /// where the eye lands), the drawn unit card, and the event log, open on Tab under any live
    /// card (issue 608; the order is <see cref="PanelLayout"/>).
    /// </summary>
    private void DrawPanel()
    {
        var client = _client!;
        var y = PanelOrigin.Y;
        y = UiRows(y, client.Objective, Ink, 13) + 4;
        foreach (var text in StatusLines())
        {
            y = Row(y, text, Warn);
        }

        if (client.EnemyPhasePlaying)
        {
            y = DrawEnemyHeader(y);
        }

        if (client.RecallOpen)
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

        _menuRects.Clear();
        if (client.Menu is { } menu && !client.EnemyPhasePlaying)
        {
            DrawLog(DrawAttackMenu(y, menu));
            return;
        }

        // An open log takes what is left of the column, never the card being read (issue 608).
        var boxTop = y - LineHeight + 4;
        var cards = _hover is { } tile && !client.EnemyPhasePlaying ? client.Hover(tile) : Array.Empty<HoverForecast>();
        var act = cards.Count > 0 ? null : client.ActShown(Animating);
        var preview = cards.Count > 0 || act is not null || _hover is not { } spot ? null : client.Preview(spot);
        var parts = PanelLayout.Parts(client.RecallOpen, _logOpen);
        if (parts.Contains(PanelPart.Forecast))
        {
            if (cards.Count > 0)
            {
                // The drawn forecast (issue 512), then the threat on the unit if it stops there, in the move preview's words.
                y = DrawForecastCard(boxTop, cards[0].Card) + 22;
                foreach (var other in cards.Skip(1))
                {
                    var o = other.Card.Defender;
                    UiText(new Vector2(PanelOrigin.X, y), $"also in reach: {o.Name} {other.TargetAt.X},{other.TargetAt.Y}  acc {other.Card.Attacker.Strike.DisplayedHit}  dmg {other.Card.Attacker.Strike.Damage}", Muted, 12);
                    y += LineHeight;
                }

                if (client.Stop(_hover!.Value) is { } stop)
                {
                    y = PreviewRow(y, stop, $"if {cards[0].Card.Attacker.Name} stops here: ");
                }
            }
            else if (act is not null)
            {
                y = DrawActCard(boxTop, act) + 22;
            }
            else if (preview is not null)
            {
                y = Title(y, "FORECAST");
                y = PreviewRow(y, preview, "");
            }
            else
            {
                y = Title(y, "FORECAST");
                var forecast = ForecastLines().ToList();
                Card(new Rect2(PanelOrigin.X - 8, y - LineHeight + 1, PanelWidth + 16, Wrapped(forecast).Count() * LineHeight + 10), Box, 8);
                foreach (var text in forecast)
                {
                    y = Row(y, text, Ink);
                }
            }

            y += 10;
        }

        // The action list (issue 786): what the selected unit can take that no board click names,
        // a chest beside it and the captain's orders, each row clicked to take it.
        var actions = client.Actions(_hover);
        if (actions.Count > 0)
        {
            y = Title(y, "ACTIONS  click to take");
            for (var i = 0; i < actions.Count; i++)
            {
                var row = actions[i];
                var top = y - LineHeight + 4;
                y = Row(y, row.Label, row.Legal ? Link : Muted);
                y = Row(y, "     " + (row.Refusal ?? row.Line), Muted);
                var index = i;
                _hits.Add((new Rect2(PanelOrigin.X, top, PanelWidth, y - top), () => client.TakeAction(index)));
            }

            y += 10;
        }

        var unitAt = _hover is { } over && client.UnitAt(over) is not null ? over : client.Selected is { } id ? client.State.Find(id)?.At : null;
        if (parts.Contains(PanelPart.UnitCard) && unitAt is { } at && client.Card(at) is { } unitCard && y - LineHeight + 4 + UnitCardHeightOf(unitCard) <= PanelBottom - 2 * LineHeight)
        {
            y = DrawUnitCard(y - LineHeight + 4, unitCard) + LineHeight + 12;
        }

        DrawLog(y);
    }

    /// <summary>
    /// The attack menu (issue 611) where the forecast card sits: the hovered row's card, then
    /// one row per way to strike, its numbers under it, a greyed row giving the rule's refusal.
    /// Hovering a row or pressing its number shows its card; a click or Enter strikes with it.
    /// </summary>
    private float DrawAttackMenu(float y, AttackMenu menu)
    {
        var client = _client!;
        if (menu.Card is { } card)
        {
            y = DrawForecastCard(y - LineHeight + 4, card) + 22;
        }

        y = Title(y, "ATTACK  hover or 1-9 shows; click or Enter strikes; Esc backs out");
        for (var i = 0; i < menu.Rows.Count; i++)
        {
            var row = menu.Rows[i];
            var top = y - LineHeight + 4;
            var colour = !row.Legal ? Muted : i == menu.Hovered ? Link : Ink;
            y = Row(y, $"{(i == menu.Hovered ? ">" : " ")}{i + 1}. {row.Label}", colour);
            y = Row(y, "     " + row.Line, Muted);
            var area = new Rect2(PanelOrigin.X, top, PanelWidth, y - top);
            _menuRects.Add(area);
            var index = i;
            _hits.Add((area, () => client.Choose(index)));
        }

        return y + 10;
    }

    /// <summary>The reaches the board draws: every seen enemy's on T, else the inspected enemy's or, with reach on hover, the hovered one's (issue 677), else none.</summary>
    private IReadOnlyList<EnemyReach> ShownReaches() =>
        _threatShown ? _client!.EnemyReaches : _client!.ReachShown(_hover, _options.ReachOnHover) is { } one ? new[] { one } : Array.Empty<EnemyReach>();

    /// <summary>The move preview's one line on a card (issue 511): the verdict's dot frost when safe and bone when struck.</summary>
    private float PreviewRow(float y, MovePreview preview, string lead)
    {
        // A narrower column at a larger UI scale (issue 698) wraps the line rather than running past the card.
        var lines = UiLines(lead + preview.Text, 13, PanelWidth - 16);
        Card(new Rect2(PanelOrigin.X - 8, y - LineHeight + 1, PanelWidth + 16, lines.Count * LineHeight + 10), Box, 8);
        DrawCircle(new Vector2(PanelOrigin.X + 5, y - 4), 5, preview.Safe ? MarkColour("reach") : EnemyMark);
        foreach (var line in lines)
        {
            UiText(new Vector2(PanelOrigin.X + 16, y), line, Ink, 13);
            y += LineHeight;
        }

        return y + 4;
    }

    /// <summary>
    /// The event log (issue 512) from <paramref name="y"/> to the column's foot, newest at the
    /// bottom, the line the board marks drawn in the mark colour: under the cards, the newest
    /// lines that fit and at least one, so the enemy phase still reads; with Tab, the whole log in
    /// what the live card leaves (issue 608).
    /// </summary>
    private void DrawLog(float y)
    {
        var client = _client!;

        // While the enemy phase plays, the column shows the act on show; Tab still opens it all (issue 544).
        var log = _logOpen ? client.Log : client.ActLog;
        var offset = client.Log.Count - log.Count;
        var rows = new List<(string Text, bool Marked, bool Undone)>();
        for (var i = 0; i < log.Count; i++)
        {
            var marked = i == log.Count - 1 && client.Playing is not null && client.EnemyPhasePlaying;
            var undone = client.Undone(offset + i);
            foreach (var line in log[i].Split('\n'))
            {
                foreach (var row in TextLayout.Wrap(line, Columns))
                {
                    rows.Add((row, marked, undone));
                }
            }
        }

        // Closed, the newest lines fill what the cards leave, one at the least; open, the log has the column.
        // On the player's phase a closed log the cards have filled the column with is left out (issue 698): its heading would be pulled up over them by more than a row.
        if (!_logOpen && !client.EnemyPhasePlaying && y - (PanelBottom - LineHeight - 4) > LineHeight)
        {
            return;
        }

        y = Title(Math.Min(y, PanelBottom - LineHeight - 4), _logOpen ? "EVENT LOG  Tab closes it" : client.EnemyPhasePlaying ? "THIS ACT  Tab for the whole log" : "EVENT LOG  Tab for the whole log");
        var room = Math.Max(0, (int)((PanelBottom + LineHeight - y) / LineHeight));
        foreach (var (text, marked, undone) in rows.Skip(Math.Max(0, rows.Count - room)))
        {
            if (marked)
            {
                DrawRect(new Rect2(PanelOrigin.X - 6, y - LineHeight + 4, PanelWidth + 12, LineHeight), new Color(EnemyMark, 0.16f));
            }

            // A line a Recall undid is dimmed (issue 515): the log keeps it, the board no longer holds it.
            Text(new Vector2(PanelOrigin.X, y), text, marked ? EnemyMark : undone ? UiColour("muted", 0.55f) : Ink, FontSize);
            y += LineHeight;
        }
    }

    /// <summary>What the forecast box holds: a hint until a unit is selected and a tile hovered, then the console's forecast text; the priced threat is cut (issue 625).</summary>
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
            yield return "Click one of your units (an amber disc) to select it,";
            yield return "then point at a tile to see the forecast from there.";
            yield break;
        }

        if (_hover is not { } tile)
        {
            yield return "Point at a tile to see the forecast there.";
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

        if (!any)
        {
            yield return $"{client.Selected} cannot end its move on {tile.X},{tile.Y}.";
        }
    }

    /// <summary>The between-map screen, each line wrapped to the window, a row's click region covering all its wrapped rows.</summary>
    private void DrawScreen()
    {
        Text(new Vector2(Margin, 28), _campaign!.Over ? "campaign over" : "between maps", Ink, 18);
        var columns = (int)((ViewWidth - 2 * Margin) / (_mono.GetStringSize("MMMMMMMMMM", fontSize: FontSize).X / 10));
        var y = (float)Top;
        var rows = ScreenLines().SelectMany(line => TextLayout.Wrap(line.Text, columns).Select(row => (row, line.Click, line.Selected))).ToList();
        var fits = (int)((ViewHeight - Margin - Top) / LineHeight) + 1;
        _screenScroll = Math.Clamp(_screenScroll, 0, Math.Max(0, rows.Count - fits));
        if (_screenScroll > 0)
        {
            Text(new Vector2(ViewWidth - Margin - 260, 28), $"scrolled {_screenScroll} rows (wheel, PgUp)", Muted, FontSize);
        }

        foreach (var (row, click, selected) in rows.Skip(_screenScroll))
        {
            if (y > ViewHeight - Margin)
            {
                Text(new Vector2(ViewWidth - Margin - 260, 28 + LineHeight), "more below (wheel, PgDn)", Muted, FontSize);
                return;
            }

            Text(new Vector2(Margin, y), row, selected ? Mark : click is null ? Ink : Link, FontSize);
            if (click is not null)
            {
                _hits.Add((new Rect2(0, y - LineHeight + 4, ViewWidth, LineHeight), click));
            }

            y += LineHeight;
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

    /// <summary>
    /// Text in the UI face wrapped at word breaks to the column's width in pixels (issue 512), a
    /// row every <paramref name="size"/> plus five pixels; returns the baseline below it.
    /// </summary>
    private float UiRows(float y, string text, Color colour, int size)
    {
        foreach (var line in UiLines(text, size, PanelWidth))
        {
            UiText(new Vector2(PanelOrigin.X, y), line, colour, size);
            y += size + 5;
        }

        return y;
    }

    /// <summary><paramref name="text"/> in the UI face broken at spaces into lines no wider than <paramref name="width"/>, one word at the least.</summary>
    private List<string> UiLines(string text, int size, float width)
    {
        var lines = new List<string>();
        var line = "";
        foreach (var word in text.Split(' '))
        {
            var next = line.Length == 0 ? word : line + " " + word;
            if (line.Length > 0 && UiWidth(next, size) > width)
            {
                lines.Add(line);
                next = word;
            }

            line = next;
        }

        lines.Add(line);
        return lines;
    }

    private float Title(float y, string title)
    {
        var split = title.IndexOf("  ", StringComparison.Ordinal);
        var head = split < 0 ? title : title[..split];
        DrawString(_caps, new Vector2(PanelOrigin.X, y), head, fontSize: 11, modulate: Muted);
        if (split >= 0)
        {
            UiText(new Vector2(PanelOrigin.X + _caps.GetStringSize(head, fontSize: 11).X + 12, y), title[(split + 2)..], Muted, 12);
        }

        DrawLine(new Vector2(PanelOrigin.X, y + 5), new Vector2(PanelOrigin.X + PanelWidth, y + 5), Rule, 1);
        return y + LineHeight + 4;
    }

    private void Text(Vector2 at, string text, Color colour, int size) =>
        DrawString(_mono, at, text, fontSize: size, modulate: colour);

    private static Color Of(Rgb rgb) => Color.Color8(rgb.R, rgb.G, rgb.B);

    private IEnumerable<string> StatusLines()
    {
        var client = _client!;
        if (client.Verdict is { } verdict)
        {
            yield return "! " + verdict;
        }

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

}
