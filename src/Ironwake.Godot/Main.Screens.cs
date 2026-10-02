using Godot;
using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Core;

namespace Ironwake.Godot;

/// <summary>
/// Showcase slice 5 (issue 515): the title screen the exported build opens on, the one
/// how-to-play screen, the three turn-1 callouts, and the end card with play again. The words
/// are the client's (<see cref="Screens"/>, <see cref="Callouts"/>, <see cref="EndCard"/>); this
/// file only lays them out. <c>--screen title|howto</c> opens either screen and
/// <c>--callouts</c> turns the callouts on for a battle opened by <c>--map</c>, for render.sh.
/// </summary>
public partial class Main
{
    private enum Screen
    {
        Battle,
        Title,
        HowTo,

        /// <summary>The Options screen (issue 677).</summary>
        Options,

        /// <summary>The campaign's title (issue 677): Continue, New game, Load, Options, Quit.</summary>
        CampaignTitle,

        /// <summary>New game's difficulty and permadeath.</summary>
        NewGame,

        /// <summary>The list of saves to load.</summary>
        Load,
    }

    /// <summary>What the window shows: the battle, or the title or how-to-play screen before it.</summary>
    private Screen _screen = Screen.Battle;

    /// <summary>
    /// The pause menu is up (issue 629): the battle stays drawn, dimmed, under it, and its clock
    /// stops. It stays set while the how-to-play screen is opened from it, so Esc there comes
    /// back to the menu.
    /// </summary>
    private bool _paused;

    /// <summary>
    /// The showcase's title was reached from the campaign's (issue 786): its last choice and Esc
    /// go back there rather than quit.
    /// </summary>
    private bool _campaignHome;

    /// <summary>One battle (issue 786): the Tollgate opened under the showcase's title, the campaign's title behind it.</summary>
    private void OpenOneBattle()
    {
        _mapArg = Screens.OneBattleMap;
        StartBattle(_seed, callouts: true);
        _screen = Screen.Title;
    }

    /// <summary>The showcase title's last choice: back to the campaign's title when it came from there, else quit.</summary>
    private void LeaveTitle()
    {
        if (_campaignHome)
        {
            _screen = Screen.CampaignTitle;
        }
        else
        {
            GetTree().Quit(0);
        }
    }

    /// <summary>The turn-1 callouts, on for a battle opened from the title; null when off.</summary>
    private Callouts? _callouts;

    /// <summary>Where the footer drew the E key, for the third callout to light.</summary>
    private Rect2 _endKey;

    /// <summary>The end card is up once the battle is decided, every beat and the scrub have played, outside the campaign (which has its own leave).</summary>
    private bool EndCardShown => _campaign is null && _client?.Ending is not null && !Animating && !Scrubbing;

    /// <summary>Opens the map this client started from on <paramref name="seed"/>, fresh.</summary>
    private void StartBattle(ulong seed, bool callouts)
    {
        var content = _content!;
        var mapPath = File.Exists(_mapArg) ? _mapArg : Path.Combine(_contentDir, "maps", _mapArg + ".map");
        var map = MapFiles.Load(mapPath, content);
        _seed = seed;
        _client = new ClientSession(content, BattleState.From(map, content, content.Cast, seed)) { SceneSetting = Scenes.FromOption(_options.Scenes) };
        _tile = TileFor(map);
        _confirm = null;
        _callouts = callouts ? new Callouts() : null;
        (_hover, _logOpen, _threatShown, _paused) = (null, false, false, false);
        (_beatSerialSeen, _scrubSerialSeen) = (-1, -1);
        _screen = Screen.Battle;
        SyncBeats();
    }

    /// <summary>The title's and the how-to-play screen's keys and clicks.</summary>
    private void TitleInput(InputEvent input)
    {
        switch (input)
        {
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click:
                PressAt(click.Position);
                break;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.M }:
                ToggleMute();
                break;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.Enter or Key.KpEnter or Key.Space } when _paused:
                (_screen, _paused) = (Screen.Battle, false);
                break;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.Enter or Key.KpEnter or Key.Space }:
                StartBattle(_seed, callouts: true);
                break;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.H } when _screen == Screen.Title:
                _screen = Screen.HowTo;
                break;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.O } when _screen == Screen.Title:
                OpenOptions(Screen.Title);
                break;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }:
                if (_screen == Screen.HowTo)
                {
                    // From the pause menu, back to it; from the title, back to the title.
                    _screen = _paused ? Screen.Battle : Screen.Title;
                }
                else
                {
                    LeaveTitle();
                }

                break;
        }
    }

    /// <summary>The end card's keys and clicks: Enter plays the map again on the next seed, Escape goes to the title; the board under it takes nothing but the pointer. True when the input was the card's.</summary>
    private bool EndCardInput(InputEvent input)
    {
        switch (input)
        {
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.Enter or Key.KpEnter }:
                StartBattle(_seed + 1, callouts: false);
                return true;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }:
                _screen = Screen.Title;
                return true;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click:
                PressAt(click.Position);
                return true;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.M }:
                ToggleMute();
                return true;
            default:
                return input is not InputEventMouseMotion;
        }
    }

    private void DrawTitleOrHowTo()
    {
        if (_screen == Screen.Title)
        {
            DrawTitle();
        }
        else
        {
            DrawHowTo();
        }
    }

    /// <summary>
    /// The title (issue 515): the name in spaced capitals over an amber rule, the tagline, the
    /// two sides as they stand on the board, and the three choices, each a keycap and a click.
    /// </summary>
    private void DrawTitle()
    {
        var centre = ViewWidth / 2f;
        var amber = Look(LookPalette.Player);

        // The two sides across a road: the company's amber discs, the road, the enemy's slate ones.
        var road = 196f;
        DrawLine(new Vector2(centre - 220, road), new Vector2(centre + 220, road), UiColour("lost"), 2);
        for (var i = 0; i < 4; i++)
        {
            DrawCircle(new Vector2(centre - 200 + i * 34, road), 11, amber);
        }

        for (var i = 0; i < 3; i++)
        {
            var at = new Vector2(centre + 132 + i * 34, road);
            DrawCircle(at, 11, Look(LookPalette.Enemy));
            DrawArc(at, 11, 0, Mathf.Tau, 32, EnemyMark, 1.2f, antialiased: true);
        }

        var name = new FontVariation { BaseFont = _bold, SpacingGlyph = 14 };
        const string title = "IRONWAKE";
        var width = name.GetStringSize(title, fontSize: 76).X;
        DrawString(name, new Vector2(centre - width / 2 + 7, 318), title, fontSize: 76, modulate: Ink);
        DrawLine(new Vector2(centre - 48, 346), new Vector2(centre + 48, 346), amber, 3);
        UiText(new Vector2(centre, 384), Screens.Tagline, Muted, 17, centred: true);

        // Four choices since Options joined (issue 677): set closer so the mute key clears the footer.
        var y = 424f;
        foreach (var (label, key) in Screens.SingleBattleTitle(_campaignHome))
        {
            var first = label == Screens.TitleChoices[0].Label;
            var rect = new Rect2(centre - 150, y, 300, 44);
            Card(rect, first ? Look(LookPalette.Player, 0.16f) : Box, 10);
            if (first)
            {
                Ring(rect, Look(LookPalette.Player, 0.7f), 10, 1.5f);
            }

            UiText(new Vector2(rect.Position.X + 22, y + 28), label, first ? amber : Ink, 17, bold: true);
            var keyWidth = UiWidth(key, 12, bold: true) + 16;
            var cap = new Rect2(rect.End.X - 18 - keyWidth, y + 11, keyWidth, 22);
            Card(cap, UiColour("ink"), 5);
            UiText(new Vector2(cap.GetCenter().X, y + 27), key, Muted, 12, bold: true, centred: true);
            Action act = label switch
            {
                "Play" => () => StartBattle(_seed, callouts: true),
                "How to play" => () => _screen = Screen.HowTo,
                "Options" => () => OpenOptions(Screen.Title),
                _ => LeaveTitle,
            };
            _hits.Add((rect, act));
            y += 50;
        }

        // The mute key (issue 516), a keycap and what it does now, clickable like the choices.
        var does = _muted ? "sound off" : "sound on";
        var capWidth = UiWidth(Sound.MuteKey, 12, bold: true) + 16;
        var row = capWidth + 8 + UiWidth(does, 13);
        var mx = centre - row / 2;
        Card(new Rect2(mx, y + 4, capWidth, 22), Box, 5);
        UiText(new Vector2(mx + capWidth / 2, y + 20), Sound.MuteKey, Ink, 12, bold: true, centred: true);
        UiText(new Vector2(mx + capWidth + 8, y + 20), does, Muted, 13);
        _hits.Add((new Rect2(mx, y, row, 30), ToggleMute));

        UiText(new Vector2(centre, ViewHeight - 28), "One map, the Tollgate. Four of the company against the toll road's keepers.", Muted, 12, centred: true);
    }

    /// <summary>
    /// The how-to-play screen (issue 515): four cards in a two by two grid, the goal, a turn, the
    /// forecast and Recall, the Recall card edged in amber since its first line is the one a
    /// stranger has to get; then how to leave.
    /// </summary>
    private void DrawHowTo()
    {
        const float side = 64;
        const float gap = 24;
        var cardWidth = (ViewWidth - 2 * side - gap) / 2;
        DrawString(_caps, new Vector2(side, 64), "HOW TO PLAY", fontSize: 13, modulate: Muted);
        DrawLine(new Vector2(side, 72), new Vector2(ViewWidth - side, 72), Rule, 1);
        var sections = Screens.HowTo;
        var rows = new[] { sections.Take(2).ToList(), sections.Skip(2).Take(2).ToList() };
        var wraps = rows.Select(row => row.Select(section => section.Lines.Select((line, j) => WrapUi(line, 15, cardWidth - 48, bold: section.Heading == "Recall" && j == 0)).ToList()).ToList()).ToList();
        var heights = wraps.Select(row => row.Max(lines => 58 + lines.Sum(l => l.Count * 21 + 8))).ToList();

        // The grid sits centred between the heading's rule and the keys.
        var y = 72 + Math.Max(24, (ViewHeight - 72 - 64 - heights.Sum() - gap) / 2f);
        for (var r = 0; r < rows.Length; r++)
        {
            var row = rows[r];
            var wrapped = wraps[r];
            var height = heights[r];
            for (var i = 0; i < row.Count; i++)
            {
                var x = side + i * (cardWidth + gap);
                var recall = row[i].Heading == "Recall";
                Card(new Rect2(x, y, cardWidth, height), Box, 12);
                if (recall)
                {
                    Card(new Rect2(x, y, 5, height), Look(LookPalette.Player), 2);
                }

                DrawString(_caps, new Vector2(x + 24, y + 34), row[i].Heading.ToUpperInvariant(), fontSize: 12, modulate: recall ? Look(LookPalette.Player) : Muted);
                var ly = y + 64;
                for (var j = 0; j < wrapped[i].Count; j++)
                {
                    // A command line ("Move: click a lit tile.") sets its command in bold, the four read as a list.
                    var line = row[i].Lines[j];
                    var colon = line.IndexOf(": ", StringComparison.Ordinal);
                    if (colon > 0 && !line[..colon].Contains(' ', StringComparison.Ordinal))
                    {
                        var label = line[..(colon + 1)];
                        UiText(new Vector2(x + 24, ly), label, Ink, 15, bold: true);
                        UiText(new Vector2(x + 24 + UiWidth(label + " ", 15, bold: true), ly), line[(colon + 2)..], Ink, 15);
                        ly += 21 + 2;
                        continue;
                    }

                    foreach (var text in wrapped[i][j])
                    {
                        UiText(new Vector2(x + 24, ly), text, j == 0 ? Ink : Muted, 15, bold: recall && j == 0);
                        ly += 21;
                    }

                    ly += 8;
                }
            }

            y += height + gap;
        }

        var kx = side;
        var leave = _paused
            ? new (string, string, Action)[]
            {
                ("Enter", "back to the battle", () => (_screen, _paused) = (Screen.Battle, false)),
                ("Esc", "back to the menu", () => _screen = Screen.Battle),
            }
            : new (string, string, Action)[]
            {
                ("Enter", "play the Tollgate", () => StartBattle(_seed, callouts: true)),
                ("Esc", "back to the title", () => _screen = Screen.Title),
            };
        foreach (var (key, does, act) in leave)
        {
            var width = UiWidth(key, 12, bold: true) + 16;
            var cap = new Rect2(kx, ViewHeight - 50, width, 24);
            Card(cap, Box, 5);
            UiText(new Vector2(cap.GetCenter().X, ViewHeight - 33), key, Ink, 12, bold: true, centred: true);
            UiText(new Vector2(kx + width + 8, ViewHeight - 33), does, Muted, 13);
            var end = kx + width + 8 + UiWidth(does, 13);
            _hits.Add((new Rect2(kx, ViewHeight - 54, end - kx, 32), act));
            kx = end + 28;
        }
    }

    /// <summary>
    /// The turn-1 callout on screen (issue 515), moved on by what the player has done. All three
    /// sit at the foot of the column, whose log is empty on turn 1, so none covers a unit, a tile
    /// or the legend it asks about (issue 516, round 163); the third lights the footer's E key.
    /// </summary>
    private void DrawCallout()
    {
        if (_callouts is null || _client is null || EndCardShown)
        {
            return;
        }

        _callouts.Observe(_client, _hover);
        if (_callouts.Showing is not { } callout)
        {
            return;
        }

        var step = (int)callout + 1;
        var lines = WrapUi(Callouts.Text(callout), 14, PanelWidth - 32);
        var width = Math.Max(lines.Max(line => UiWidth(line, 14)), UiWidth("STEP 3 OF 3", 10, bold: true)) + 32;
        var height = 40 + lines.Count * 19;
        var amber = Look(LookPalette.Player);
        var x = PanelOrigin.X - 8;
        var y = PanelBottom - height;
        var rect = new Rect2(x, y, width, height);
        Card(rect.Grow(2), amber, 10);
        Card(rect, Box, 9);
        if (callout == Callout.End)
        {
            Ring(_endKey.Grow(3), amber, 7, 2);
        }

        UiText(new Vector2(x + 16, y + 22), $"STEP {step} OF 3", amber, 10, bold: true);
        var ly = y + 42;
        foreach (var line in lines)
        {
            UiText(new Vector2(x + 16, ly), line, Ink, 14);
            ly += 19;
        }
    }

    /// <summary>
    /// The end card (issue 515): the board dimmed under a card that says won or lost, the line on
    /// what it cost or why, the turn, and the two ways on: play again and the title.
    /// </summary>
    private void DrawEndCard()
    {
        if (!EndCardShown || _client!.Ending is not { } end)
        {
            return;
        }

        DrawRect(new Rect2(Vector2.Zero, new Vector2(ViewWidth, ViewHeight)), UiColour("ink", 0.72f));
        var side = end.Won ? Look(LookPalette.Player) : EnemyMark;
        var lines = WrapUi(end.Line, 17, 460);
        var width = 540f;
        var height = 214 + lines.Count * 24;
        var rect = new Rect2((ViewWidth - width) / 2, (ViewHeight - height) / 2, width, height);
        Card(rect, Box, 14);
        Card(new Rect2(rect.Position, new Vector2(width, 6)), side, 3);
        var x = rect.Position.X + 40;
        var y = rect.Position.Y + 44;
        DrawString(_caps, new Vector2(x, y), end.Turn.ToUpperInvariant(), fontSize: 11, modulate: Muted);
        UiText(new Vector2(x, y + 54), end.Headline, side, 46, bold: true);
        y += 96;
        foreach (var line in lines)
        {
            UiText(new Vector2(x, y), line, Ink, 17);
            y += 24;
        }

        y += 26;
        foreach (var (key, does, act) in new (string, string, Action)[]
        {
            ("Enter", "play again", () => StartBattle(_seed + 1, callouts: false)),
            ("Esc", "title", () => _screen = Screen.Title),
        })
        {
            var keyWidth = UiWidth(key, 12, bold: true) + 16;
            Card(new Rect2(x, y - 16, keyWidth, 24), UiColour("ink"), 5);
            UiText(new Vector2(x + keyWidth / 2, y + 1), key, Ink, 12, bold: true, centred: true);
            UiText(new Vector2(x + keyWidth + 8, y + 1), does, Muted, 14);
            var end2 = x + keyWidth + 8 + UiWidth(does, 14);
            _hits.Add((new Rect2(x, y - 20, end2 - x, 32), act));
            x = end2 + 28;
        }
    }

    /// <summary>The pause menu's keys and clicks (issue 629); the board under it takes nothing but the pointer.</summary>
    private void PauseInput(InputEvent input)
    {
        switch (input)
        {
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click:
                PressAt(click.Position);
                break;
            case InputEventKey { Pressed: true, Echo: false } key when Screens.PauseKey(key.Keycode == Key.Escape ? "Esc" : OS.GetKeycodeString(key.Keycode)) is { } choice:
                Pick(choice);
                break;
        }
    }

    /// <summary>Does what a pause menu choice says.</summary>
    private void Pick(PauseChoice choice)
    {
        switch (choice)
        {
            case PauseChoice.Resume:
                _paused = false;
                break;
            case PauseChoice.HowToPlay:
                _screen = Screen.HowTo;
                break;
            case PauseChoice.Sound:
                ToggleMute();
                break;
            case PauseChoice.Options:
                // Esc on Options comes back to the battle with the menu still up.
                OpenOptions(Screen.Battle);
                break;
            default:
                (_screen, _paused) = (Screen.Title, false);
                break;
        }
    }

    /// <summary>
    /// The pause menu (issue 629): the board dimmed under a card with the four choices, each a
    /// row with its keycap, clickable like the title's; the sound row says whether sound is on.
    /// </summary>
    private void DrawPauseMenu()
    {
        if (!_paused || EndCardShown)
        {
            return;
        }

        // Only the menu's rows take a click while it is up, not the board or the column under it.
        _hits.Clear();
        DrawRect(new Rect2(Vector2.Zero, new Vector2(ViewWidth, ViewHeight)), UiColour("ink", 0.72f));
        var amber = Look(LookPalette.Player);
        var width = 380f;
        var height = 96 + Screens.PauseChoices.Count * 56;
        var rect = new Rect2((ViewWidth - width) / 2, (ViewHeight - height) / 2, width, height);
        Card(rect, Box, 14);
        Card(new Rect2(rect.Position, new Vector2(width, 6)), amber, 3);
        DrawString(_caps, new Vector2(rect.Position.X + 40, rect.Position.Y + 48), "PAUSED", fontSize: 13, modulate: Muted);
        var y = rect.Position.Y + 72;
        foreach (var (choice, label, key) in Screens.PauseChoices)
        {
            var first = choice == PauseChoice.Resume;
            var row = new Rect2(rect.Position.X + 40, y, width - 80, 44);
            Card(row, first ? Look(LookPalette.Player, 0.16f) : UiColour("ink", 0.5f), 10);
            if (first)
            {
                Ring(row, Look(LookPalette.Player, 0.7f), 10, 1.5f);
            }

            var text = choice == PauseChoice.Sound ? (_muted ? "Sound off" : "Sound on") : label;
            UiText(new Vector2(row.Position.X + 20, y + 28), text, first ? amber : Ink, 17, bold: true);
            var keyWidth = UiWidth(key, 12, bold: true) + 16;
            var cap = new Rect2(row.End.X - 16 - keyWidth, y + 11, keyWidth, 22);
            Card(cap, UiColour("ink"), 5);
            UiText(new Vector2(cap.GetCenter().X, y + 27), key, Muted, 12, bold: true, centred: true);
            var picked = choice;
            _hits.Add((row, () => Pick(picked)));
            y += 56;
        }
    }

    /// <summary>An outline with rounded corners at <paramref name="radius"/>, the ring a rounded chip or card wears.</summary>
    private void Ring(Rect2 r, Color colour, float radius, float width)
    {
        var box = new StyleBoxFlat { DrawCenter = false, BorderColor = colour, AntiAliasing = true };
        box.SetBorderWidthAll((int)Math.Round(width));
        box.SetCornerRadiusAll((int)radius);
        box.Draw(GetCanvasItem(), r);
    }

    /// <summary>Text in the UI face broken at word breaks into lines no wider than <paramref name="width"/> pixels.</summary>
    private List<string> WrapUi(string text, int size, float width, bool bold = false)
    {
        var lines = new List<string>();
        var line = "";
        foreach (var word in text.Split(' '))
        {
            var next = line.Length == 0 ? word : line + " " + word;
            if (line.Length > 0 && UiWidth(next, size, bold) > width)
            {
                lines.Add(line);
                next = word;
            }

            line = next;
        }

        lines.Add(line);
        return lines;
    }
}
