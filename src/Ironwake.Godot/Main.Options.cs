using Godot;
using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Core;

namespace Ironwake.Godot;

/// <summary>
/// The title screen and Options, slice 2 (issue 677, DECISIONS/0141): the options read from the
/// profile at start and written on every change, the Options screen (from either title or the
/// pause menu), the campaign's title (Continue, New game, Load, Options, Quit) that
/// <c>--campaign</c> opens on, and the end-turn confirm. The words are the client's
/// (<see cref="Screens"/>, <see cref="OptionsMenu"/>, <see cref="EndTurnConfirm"/>); this file only
/// lays them out and keeps the profile in step.
/// </summary>
public partial class Main
{
    /// <summary>The options in force: the profile's, or the defaults for a render or a parity run, which never read or write it.</summary>
    private Options _options = new();

    /// <summary>The saves and the profile beside them, or null for a run that keeps neither.</summary>
    private SaveStore? _store;

    /// <summary>Where Esc on the Options screen goes back to: a title, or the battle with the pause menu still up.</summary>
    private Screen _optionsBack = Screen.Title;

    /// <summary>New game's picks: the difficulty's index in <see cref="Screens.NewGameDifficulties"/> and permadeath.</summary>
    private int _newDifficulty;
    private bool _newPermadeath = true;

    /// <summary>New game's captain (issue 681): the origin's index in the campaign's origins and the pronoun.</summary>
    private int _newOrigin;
    private Pronoun _newCaptain = Pronoun.He;

    /// <summary>The end-turn confirm's lines while it is up, else null.</summary>
    private IReadOnlyList<string>? _confirm;

    /// <summary>Whether the window shows one of this file's screens rather than the battle, the showcase's title or the camp.</summary>
    private bool OnMenuScreen => _screen is Screen.Options or Screen.CampaignTitle or Screen.NewGame or Screen.Load;

    /// <summary>The saves folder: <c>--saves</c>, else <c>saves/</c> under Godot's user data folder.</summary>
    private static string SavesDir(string[] args) => Arg(args, "--saves") ?? Path.Combine(OS.GetUserDataDir(), CampaignSessionSaves);

    private const string CampaignSessionSaves = Ironwake.Cli.CampaignSession.DefaultSavesDirectory;

    /// <summary>Reads the profile's options, printing any line it could not read, and applies them.</summary>
    private void LoadOptions()
    {
        if (_store is null)
        {
            return;
        }

        var (options, warnings) = _store.ReadOptions();
        foreach (var warning in warnings)
        {
            GD.PrintErr("WARNING: " + warning);
        }

        _options = options;
        ApplyOptions();
    }

    /// <summary>Puts the options in force: the speed, the scenes on any open battle, and the sound.</summary>
    private void ApplyOptions()
    {
        _speed = GameSpeed.IndexOf(_options.Speed);
        if (_client is not null)
        {
            _client.SceneSetting = Scenes.FromOption(_options.Scenes);
        }

        _muted = !_options.Sound;
        var master = AudioServer.GetBusIndex("Master");
        AudioServer.SetBusMute(master, _muted);
        AudioServer.SetBusVolumeDb(master, _options.Volume == 0 ? -80 : Mathf.LinearToDb(_options.Volume / 100f));
    }

    /// <summary>Takes <paramref name="options"/> as the options in force and writes them to the profile, so a change lasts.</summary>
    private void SetOptions(Options options)
    {
        _options = options;
        ApplyOptions();
        _store?.WriteOptions(options);
    }

    /// <summary>The speed buttons and S: the speed picked, kept in the profile.</summary>
    private void PickSpeed(int index) => SetOptions(_options with { Speed = GameSpeed.Speeds[index].Name });

    /// <summary>B: the next scene setting, kept in the profile.</summary>
    private void CycleScenes() =>
        SetOptions(_options with { Scenes = Scenes.ToOption(Scenes.Next(_client?.SceneSetting ?? Scenes.FromOption(_options.Scenes))) });

    /// <summary>M and the Sound rows: sound on or off, kept in the profile.</summary>
    private void ToggleMute() => SetOptions(_options with { Sound = !_options.Sound });

    /// <summary>Opens the Options screen, Esc coming back to <paramref name="back"/>.</summary>
    private void OpenOptions(Screen back) => (_optionsBack, _screen) = (back, Screen.Options);

    /// <summary>
    /// E in a battle: ends the phase, unless the confirm is on and a unit has neither moved nor
    /// acted, when it asks first; E again with the confirm up ends it.
    /// </summary>
    private void EndPressed()
    {
        if (_confirm is null && EndTurnConfirm.Lines(_client!.State, _client.Content, _options.ConfirmEndTurn) is { } lines)
        {
            _confirm = lines;
            return;
        }

        _confirm = null;
        _client!.Submit(new EndPhase());
    }

    /// <summary>The confirm's keys: E or Enter ends the phase, Esc goes back; nothing else reaches the board. True when the input was the confirm's.</summary>
    private bool ConfirmInput(InputEvent input)
    {
        if (_confirm is null)
        {
            return false;
        }

        switch (input)
        {
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.E or Key.Enter or Key.KpEnter }:
                EndPressed();
                return true;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }:
                _confirm = null;
                return true;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click:
                PressAt(click.Position);
                return true;
            default:
                return input is not InputEventMouseMotion;
        }
    }

    /// <summary>The end-turn confirm: the board dimmed under a card with who has not moved, the lethal lines, and the two keys.</summary>
    private void DrawConfirm()
    {
        if (_confirm is not { } lines)
        {
            return;
        }

        _hits.Clear();
        DrawRect(new Rect2(Vector2.Zero, new Vector2(ViewWidth, ViewHeight)), UiColour("ink", 0.72f));
        var width = 560f;
        var wrapped = lines.Take(lines.Count - 1).Select(line => WrapUi(line, 15, width - 80)).ToList();
        var height = 150 + wrapped.Sum(w => w.Count * 21 + 6);
        var rect = new Rect2((ViewWidth - width) / 2, (ViewHeight - height) / 2, width, height);
        Card(rect, Box, 14);
        Card(new Rect2(rect.Position, new Vector2(width, 6)), Look(LookPalette.Player), 3);
        var x = rect.Position.X + 40;
        var y = rect.Position.Y + 48;
        DrawString(_caps, new Vector2(x, y), "END THE PHASE?", fontSize: 13, modulate: Muted);
        y += 34;
        for (var i = 0; i < wrapped.Count; i++)
        {
            foreach (var text in wrapped[i])
            {
                UiText(new Vector2(x, y), text, i == 0 ? Ink : EnemyMark, 15, bold: i == 0);
                y += 21;
            }

            y += 6;
        }

        KeyRow(x, y + 20, new (string, string, Action)[]
        {
            ("E", "end the phase", EndPressed),
            ("Esc", "go back", () => _confirm = null),
        });
    }

    /// <summary>Keycaps with what each does, left to right from <paramref name="x"/>, each a click.</summary>
    private void KeyRow(float x, float y, IEnumerable<(string Key, string Does, Action Act)> keys)
    {
        foreach (var (key, does, act) in keys)
        {
            var keyWidth = UiWidth(key, 12, bold: true) + 16;
            Card(new Rect2(x, y - 16, keyWidth, 24), UiColour("ink"), 5);
            UiText(new Vector2(x + keyWidth / 2, y + 1), key, Ink, 12, bold: true, centred: true);
            UiText(new Vector2(x + keyWidth + 8, y + 1), does, Muted, 14);
            var end = x + keyWidth + 8 + UiWidth(does, 14);
            _hits.Add((new Rect2(x, y - 20, end - x, 32), act));
            x = end + 28;
        }
    }

    /// <summary>The keys and clicks of this file's screens.</summary>
    private void MenuScreenInput(InputEvent input)
    {
        if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click)
        {
            PressAt(click.Position);
            return;
        }

        if (input is not InputEventKey { Pressed: true, Echo: false } key)
        {
            return;
        }

        var name = key.Keycode == Key.Escape ? "Esc" : OS.GetKeycodeString(key.Keycode);
        switch (_screen)
        {
            case Screen.Options when name == "Esc":
                _screen = _optionsBack;
                break;
            case Screen.Options when int.TryParse(name, out var row) && row >= 1 && row <= OptionsMenu.Rows.Count:
                SetOptions(OptionsMenu.Cycle(_options, OptionsMenu.Rows[row - 1].Key));
                break;
            case Screen.CampaignTitle when key.Keycode is Key.Enter or Key.KpEnter:
                PickTitle(Screens.DefaultTitleChoice(_store?.Newest() is not null));
                break;
            case Screen.CampaignTitle:
                foreach (var choice in Screens.CampaignTitle(_store?.Newest() is not null))
                {
                    if (Screens.Key(choice) == name)
                    {
                        PickTitle(choice);
                        break;
                    }
                }

                break;
            case Screen.NewGame when name == "Esc":
                _screen = Screen.CampaignTitle;
                break;
            case Screen.NewGame when key.Keycode is Key.Enter or Key.KpEnter:
                BeginNewGame();
                break;
            case Screen.NewGame when name == "D":
                _newDifficulty++;
                break;
            case Screen.NewGame when name == "P":
                _newPermadeath = !_newPermadeath;
                break;
            case Screen.NewGame when name == "O":
                _newOrigin++;
                break;
            case Screen.NewGame when name == "G":
                _newCaptain = NextCaptain(_newCaptain);
                break;
            case Screen.Load when name == "Esc":
                _screen = Screen.CampaignTitle;
                break;
        }
    }

    /// <summary>Does what a campaign title line says.</summary>
    private void PickTitle(TitleChoice choice)
    {
        switch (choice)
        {
            case TitleChoice.Continue when _store?.Newest() is { } newest:
                LoadCampaign(newest);
                break;
            case TitleChoice.NewGame:
                (_screen, _newDifficulty, _newPermadeath, _newOrigin, _newCaptain) = (Screen.NewGame, NewGameDefault(), true, 0, Pronoun.He);
                break;
            case TitleChoice.Load:
                _screen = Screen.Load;
                break;
            case TitleChoice.Options:
                OpenOptions(Screen.CampaignTitle);
                break;
            case TitleChoice.Quit:
                GetTree().Quit(0);
                break;
        }
    }

    /// <summary>New game's difficulties, those the profile has unlocked.</summary>
    private IReadOnlyList<Difficulty> NewGameDifficulties() => Screens.NewGameDifficulties(_content!, _store?.Won() ?? Array.Empty<string>());

    /// <summary>The difficulty New game opens on: the campaign's normal one where it is offered, else the first.</summary>
    private int NewGameDefault() => Math.Max(0, NewGameDifficulties().ToList().FindIndex(d => d.Id == CampaignRecord.NormalDifficulty));

    /// <summary>The captain pronoun New game steps to from <paramref name="current"/> (issue 681): he, then she.</summary>
    private static Pronoun NextCaptain(Pronoun current) => current == Pronoun.He ? Pronoun.She : Pronoun.He;

    /// <summary>Starts a fresh campaign on New game's picks.</summary>
    private void BeginNewGame()
    {
        var difficulties = NewGameDifficulties();
        var difficulty = difficulties[_newDifficulty % difficulties.Count];
        var origins = _content!.Campaign.Origins;
        var origin = origins.Count == 0 ? null : origins[_newOrigin % origins.Count].Id;
        StartCampaign(CampaignRecord.Start(_content!, _seed, difficulty.Id, _newPermadeath, origin, origins.Count == 0 ? null : _newCaptain));
    }

    /// <summary>Loads the save <paramref name="name"/> into the campaign, or says why it cannot be.</summary>
    private void LoadCampaign(string name)
    {
        var (record, refusal) = _store!.Load(name, _content!);
        if (record is null)
        {
            _error = "ERROR: " + refusal;
            return;
        }

        StartCampaign(record);
    }

    /// <summary>Opens the camp on <paramref name="record"/>, the battle closed and the screens left.</summary>
    private void StartCampaign(CampaignRecord record)
    {
        _campaign = new CampaignClient(_content!, _contentDir, record, saves: _store);
        (_client, _screen, _error) = (null, Screen.Battle, "");
    }

    /// <summary>Draws this file's screen.</summary>
    private void DrawMenuScreen()
    {
        switch (_screen)
        {
            case Screen.Options:
                DrawOptions();
                break;
            case Screen.CampaignTitle:
                DrawCampaignTitle();
                break;
            case Screen.NewGame:
                DrawNewGame();
                break;
            default:
                DrawLoad();
                break;
        }
    }

    /// <summary>A heading in spaced capitals over a rule, as the how-to-play screen opens.</summary>
    private void Heading(string text)
    {
        DrawString(_caps, new Vector2(64, 64), text, fontSize: 13, modulate: Muted);
        DrawLine(new Vector2(64, 72), new Vector2(ViewWidth - 64, 72), Rule, 1);
    }

    /// <summary>A wide row with its words, a value on the right and a keycap, clickable; the first row of a list is lit amber.</summary>
    private void MenuRow(float y, string label, string? value, string key, bool lit, Action act)
    {
        var amber = Look(LookPalette.Player);
        var rect = new Rect2(ViewWidth / 2f - 320, y, 640, 44);
        Card(rect, lit ? Look(LookPalette.Player, 0.16f) : Box, 10);
        if (lit)
        {
            Ring(rect, Look(LookPalette.Player, 0.7f), 10, 1.5f);
        }

        UiText(new Vector2(rect.Position.X + 22, y + 28), label, lit ? amber : Ink, 16, bold: true);
        var keyWidth = UiWidth(key, 12, bold: true) + 16;
        var cap = new Rect2(rect.End.X - 18 - keyWidth, y + 11, keyWidth, 22);
        Card(cap, UiColour("ink"), 5);
        UiText(new Vector2(cap.GetCenter().X, y + 27), key, Muted, 12, bold: true, centred: true);
        if (value is not null)
        {
            UiText(new Vector2(cap.Position.X - 16 - UiWidth(value, 16, bold: true), y + 28), value, lit ? amber : Ink, 16, bold: true);
        }

        _hits.Add((rect, act));
    }

    /// <summary>
    /// The Options screen (issue 677): a row per option with its value, a click or its number
    /// stepping it on, each change in force and in the profile at once; then, for a campaign in
    /// play, its difficulty and permadeath, read-only, since a difficulty is lowered at a camp.
    /// </summary>
    private void DrawOptions()
    {
        Heading("OPTIONS");
        var y = 104f;
        for (var i = 0; i < OptionsMenu.Rows.Count; i++)
        {
            var (key, label, _) = OptionsMenu.Rows[i];
            var rowKey = key;
            MenuRow(y, label, OptionsMenu.Words(key, OptionsMenu.Value(_options, key)), (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), false,
                () => SetOptions(OptionsMenu.Cycle(_options, rowKey)));
            y += 54;
        }

        if (_campaign is not null)
        {
            UiText(new Vector2(ViewWidth / 2f - 320, y + 24), "This campaign: " + Ironwake.Cli.CampaignSession.RulesLine(_campaign.Record, _content!) + " (lowered only at a camp)", Muted, 14);
        }

        KeyRow(64, ViewHeight - 33, new (string, string, Action)[] { ("Esc", "back", () => _screen = _optionsBack) });
    }

    /// <summary>
    /// The campaign's title (issue 677): the name, Continue first and lit when a save exists, then
    /// New game, Load, Options and Quit, and one dry line under them.
    /// </summary>
    private void DrawCampaignTitle()
    {
        var centre = ViewWidth / 2f;
        var name = new FontVariation { BaseFont = _bold, SpacingGlyph = 14 };
        const string title = "IRONWAKE";
        var width = name.GetStringSize(title, fontSize: 76).X;
        DrawString(name, new Vector2(centre - width / 2 + 7, 220), title, fontSize: 76, modulate: Ink);
        DrawLine(new Vector2(centre - 48, 248), new Vector2(centre + 48, 248), Look(LookPalette.Player), 3);
        var hasSave = _store?.Newest() is not null;
        var lit = Screens.DefaultTitleChoice(hasSave);
        var y = 300f;
        foreach (var choice in Screens.CampaignTitle(hasSave))
        {
            var picked = choice;
            var value = choice == TitleChoice.Continue ? _store!.Newest() : null;
            MenuRow(y, Screens.Label(choice), value, choice == lit ? "Enter" : Screens.Key(choice), choice == lit, () => PickTitle(picked));
            y += 54;
        }

        if (_error.Length > 0)
        {
            UiText(new Vector2(centre, y + 20), _error, EnemyMark, 14, centred: true);
        }

        UiText(new Vector2(centre, ViewHeight - 28), Screens.TitleFooter, Muted, 12, centred: true);
    }

    /// <summary>New game (issue 677): the difficulty and permadeath, and the captain's origin and pronoun (issue 681), each a row a click or its key steps on, then Enter begins.</summary>
    private void DrawNewGame()
    {
        Heading("NEW GAME");
        var difficulties = NewGameDifficulties();
        var difficulty = difficulties[_newDifficulty % difficulties.Count];
        var lines = Screens.NewGameLines(difficulty, _newPermadeath);
        var y = 120f;
        MenuRow(y, lines[0], null, "D", false, () => _newDifficulty++);
        MenuRow(y + 54, lines[1], null, "P", false, () => _newPermadeath = !_newPermadeath);
        var captain = Screens.CaptainLines(_content!, _newOrigin, _newCaptain);
        if (captain.Count > 0)
        {
            MenuRow(y + 108, captain[0], null, "O", false, () => _newOrigin++);
            MenuRow(y + 162, captain[1], null, "G", false, () => _newCaptain = NextCaptain(_newCaptain));
            y += 108;
        }

        MenuRow(y + 128, "Begin", null, "Enter", true, BeginNewGame);
        KeyRow(64, ViewHeight - 33, new (string, string, Action)[] { ("Esc", "back to the title", () => _screen = Screen.CampaignTitle) });
    }

    /// <summary>Load (issue 677): every save, the autosaves newest first, then the named ones, each a row a click loads.</summary>
    private void DrawLoad()
    {
        Heading("LOAD");
        var names = _store?.Names() ?? Array.Empty<string>();
        var y = 104f;
        if (names.Count == 0)
        {
            UiText(new Vector2(ViewWidth / 2f, y + 28), "No saves yet. Every camp autosaves.", Muted, 15, centred: true);
        }

        foreach (var save in names.Take(10))
        {
            var picked = save;
            MenuRow(y, save, null, "click", false, () => LoadCampaign(picked));
            y += 54;
        }

        if (_error.Length > 0)
        {
            UiText(new Vector2(ViewWidth / 2f, y + 20), _error, EnemyMark, 14, centred: true);
        }

        KeyRow(64, ViewHeight - 33, new (string, string, Action)[] { ("Esc", "back to the title", () => _screen = Screen.CampaignTitle) });
    }
}
