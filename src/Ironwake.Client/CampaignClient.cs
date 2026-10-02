using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Core;

namespace Ironwake.Client;

/// <summary>
/// The thin renderer's campaign presenter (issue 360): what <c>ironwake campaign</c> does between
/// maps, beside <see cref="ClientSession"/>, with no rules in it. Buy, repair, drop, take, refine,
/// certify, trial, quest, hire, build, bench and unbench are the record's own actions, refused
/// with the core's text; march opens the next map as a <see cref="ClientSession"/> and leave
/// takes the record back once the battle is decided. Every line the screen shows is the console's, from the public statics on
/// <see cref="CampaignSession"/>, and the event log is what <c>campaign --log</c> writes for the
/// same commands, which the campaign parity test checks byte for byte.
/// </summary>
public sealed class CampaignClient
{
    private readonly List<string> _log = new();
    private readonly string _contentDir;
    private readonly RollScheme _scheme;

    /// <summary>The unit playing the open certification trial, or null outside a trial.</summary>
    private string? _trial;

    /// <summary>Where the campaign autosaves at each camp and records a win (issue 677), or null for a run that keeps no saves (the parity scripts).</summary>
    private readonly SaveStore? _saves;

    public CampaignClient(GameContent content, string contentDir, CampaignRecord record, RollScheme scheme = RollScheme.TwoRollAverage, SaveStore? saves = null)
    {
        Content = content;
        _contentDir = contentDir;
        Record = record;
        _scheme = scheme;
        _saves = saves;
        Autosave();
        QueueBeforeCard();
    }

    public GameContent Content { get; }

    /// <summary>The campaign as the last screen action or battle left it.</summary>
    public CampaignRecord Record { get; private set; }

    /// <summary>The battle or trial being played, or null on the between-map screen.</summary>
    public ClientSession? Battle { get; private set; }

    /// <summary>The side map being played as the battle (issue 786), or null outside one.</summary>
    private string? _quest;

    /// <summary>Whether the battle being played is a certification trial.</summary>
    public bool InTrial => _trial is not null;

    /// <summary>Whether the battle being played is a side map (issue 786).</summary>
    public bool InQuest => _quest is not null;

    /// <summary>Whether the campaign is over: every map won, or a battle lost and left.</summary>
    public bool Over { get; private set; }

    /// <summary>
    /// The line every story card is drawn under (issue 786): the cards are placeholder text until
    /// the storyline is written (issue 656), so notes on them land on the shape, not the words.
    /// </summary>
    public const string PlaceholderMark = "Placeholder story: these words stand in until the storyline is written.";

    /// <summary>The story cards still to show, oldest first: screen text, never part of the event log.</summary>
    private readonly Queue<IReadOnlyList<string>> _cards = new();

    /// <summary>
    /// The story card to show before anything else (issue 786), as the console prints it: a map's
    /// before card on arriving at its camp, its after card once it is won, a side map's cards, and
    /// the lost card; null when none is waiting. Showing one is the renderer's; the record's
    /// actions do not wait on it, so a parity script plays the same with or without it.
    /// </summary>
    public IReadOnlyList<string>? Card => _cards.Count == 0 ? null : _cards.Peek();

    /// <summary>Puts away the card <see cref="Card"/> shows, bringing up the next one waiting.</summary>
    public void DismissCard()
    {
        if (_cards.Count > 0)
        {
            _cards.Dequeue();
        }
    }

    /// <summary>The last refusal for the status bar, never part of the event log.</summary>
    public string? Status { get; private set; }

    /// <summary>The campaign's event log as <c>campaign --log</c> writes it, each line ending in <c>\n</c>, the open battle's lines included.</summary>
    public string LogText => string.Concat(_log.Select(line => line + "\n")) + (Battle?.LogText ?? "");

    /// <summary>The next map as the record would fight it, or null once the campaign is over.</summary>
    public MapDefinition? NextMap => Over || Record.IsFinished(Content) ? null : CampaignSession.MapFor(_contentDir, Content, Record, Record.NextMap(Content).MapId);

    /// <summary>
    /// The between-map screen's lines, as the console prints them on arriving at it: the map's text
    /// card when it has one (issue 631), then the camp as one view (issue 678), heading, Roster,
    /// Keep, Quests, Shop and the march line.
    /// </summary>
    public IReadOnlyList<string> ScreenLines()
    {
        if (NextMap is not { } map)
        {
            return Array.Empty<string>();
        }

        var lines = new List<string>(CampaignSession.BeforeCard(Record, Content, map));
        lines.AddRange(CampaignSession.TurnedAwayLines(Record, Content));
        lines.AddRange(CampaignSession.CampLines(_contentDir, Content, Record, map, typed: true));
        return lines;
    }

    /// <summary>The camp's Keep panel as the console prints it (issue 678).</summary>
    public IReadOnlyList<string> KeepPanelLines() => CampaignSession.KeepPanelLines(_contentDir, Content, Record);

    /// <summary>The camp's Quests panel as the console prints it (issue 678).</summary>
    public IReadOnlyList<string> QuestPanelLines() => CampaignSession.QuestPanelLines(_contentDir, Content, Record);

    /// <summary>The keep's menu as the console prints it, on the keep this record would fight.</summary>
    public IReadOnlyList<string> KeepLines() => CampaignSession.KeepLines(_contentDir, Content, Record);

    /// <summary>The ids the shop sells before the next map, each what <see cref="Buy"/> takes.</summary>
    public IReadOnlyList<string> Stock => Over || Record.IsFinished(Content) ? Array.Empty<string>() : CampaignSession.Stock(Record, Content);

    public bool Buy(string itemId, string unitId) => Screen(() => Record.Buy(itemId, unitId, Content));

    /// <summary>Repairs the weapon in <paramref name="slot"/>, counted from 0.</summary>
    public bool Repair(string unitId, int slot) => Screen(() => Record.Repair(unitId, slot, Content));

    public bool Certify(string unitId, string classId) => Screen(() => Record.Certify(unitId, classId, Content));

    /// <summary>Hires <paramref name="hireId"/> at the barracks (issue 690).</summary>
    public bool Hire(string hireId) => Screen(() => Record.Hire(hireId, Content));

    /// <summary>Discards the stack in <paramref name="slot"/>, counted from 0 (issue 786).</summary>
    public bool Drop(string unitId, int slot) => Screen(() => Record.Drop(unitId, slot, Content));

    /// <summary>Moves wagon entry <paramref name="index"/>, counted from 0, into the unit's pack (issue 786).</summary>
    public bool Take(string unitId, int index) => Screen(() => Record.TakeFromWagon(unitId, index, Content));

    /// <summary>Refines the weapon in <paramref name="slot"/>, counted from 0, by <c>mt</c> or <c>hit</c> at the forge (issue 786).</summary>
    public bool Refine(string unitId, int slot, string stat) => Screen(() => Record.Refine(unitId, slot, stat, Content));

    /// <summary>Buys the keep's room <paramref name="roomId"/> (issue 786).</summary>
    public bool BuildRoom(string roomId) => Screen(() => Record.BuildRoom(roomId, Content));

    public bool Bench(string unitId) => NextMap is { } map && Screen(() => Record.Bench(unitId, map));

    public bool Unbench(string unitId) => Screen(() => Record.Unbench(unitId));

    public bool Build(string editId, Coord at)
    {
        try
        {
            return Screen(() => Record.Build(editId, at, CampaignSession.BareKeep(_contentDir, Content), Content));
        }
        catch (MapException e)
        {
            return Refuse(e.Message);
        }
    }

    /// <summary>
    /// Opens a certification trial as the battle, with the console's opening lines in the log.
    /// False, with the refusal in <see cref="Status"/>, as the console refuses it.
    /// </summary>
    public bool Trial(string unitId, string classId)
    {
        if (!OnScreen())
        {
            return false;
        }

        var (trial, refusal) = CampaignSession.TrialFor(_contentDir, Content, Record, unitId, classId);
        if (trial is null)
        {
            return Refuse(refusal!);
        }

        _log.AddRange(CampaignSession.TrialLines(Record, Content, trial, unitId, classId));
        Battle = new ClientSession(Content, Record.BeginTrial(trial, unitId, Content, _scheme));
        _trial = unitId;
        Status = null;
        return true;
    }

    /// <summary>
    /// Opens the side map <paramref name="questId"/> as the battle with <paramref name="allyIds"/>
    /// (issue 786), its opening lines in the log as the console writes them. False, with the
    /// refusal in <see cref="Status"/>, as the console refuses it.
    /// </summary>
    public bool Quest(string questId, IReadOnlyList<string> allyIds)
    {
        if (!OnScreen())
        {
            return false;
        }

        var (map, refusal) = CampaignSession.QuestFor(_contentDir, Content, Record, questId, allyIds);
        if (map is null)
        {
            return Refuse(refusal!);
        }

        QueueCard(CampaignSession.QuestBeforeCard(Content, map, questId));
        _log.AddRange(CampaignSession.QuestOpening(Record, Content, map, questId, allyIds));
        Battle = new ClientSession(Content, Record.BeginQuest(map, questId, allyIds, Content, _scheme));
        _quest = questId;
        Status = null;
        return true;
    }

    /// <summary>Opens the next map as the battle, with its opening line in the log.</summary>
    public bool March()
    {
        if (!OnScreen() || NextMap is not { } map)
        {
            return false;
        }

        _log.Add(CampaignSession.MapLine(Record, Content, map));
        Battle = new ClientSession(Content, Record.Begin(map, Content, _scheme));
        Status = null;
        return true;
    }

    /// <summary>
    /// Leaves a decided battle, as <c>leave</c> does: a trial's result is applied and the screen
    /// returns; a won map pays its reward and the screen returns, or the campaign is won; a lost
    /// one ends the campaign. False, with the console's refusal, while the battle is undecided.
    /// </summary>
    public bool Leave()
    {
        if (Battle is not { } battle)
        {
            return Refuse("there is no battle to leave");
        }

        if (!battle.State.Outcome.IsOver || battle.EnemyPhasePlaying)
        {
            return Refuse("the battle is not decided; leave comes after it is won or lost");
        }

        _log.AddRange(battle.Log);
        Battle = null;
        Status = null;
        if (_trial is { } trialUnit)
        {
            _trial = null;
            var result = Record.AfterTrial(battle.State, trialUnit, Content);
            Record = result.Record;
            _log.Add(CampaignSession.Text(Record, Content, result.Text));
            Autosave();
            return true;
        }

        if (_quest is { } questId)
        {
            _quest = null;
            var result = Record.AfterQuest(battle.State, questId, Content);
            Record = result.Record;
            _log.Add(CampaignSession.Text(Record, Content, result.Text));
            if (battle.State.Outcome.Result == BattleResult.Won)
            {
                QueueCard(CampaignSession.QuestAfterCard(Content, battle.State.Map, questId));
            }

            Autosave();
            return true;
        }

        if (battle.State.Outcome.Result != BattleResult.Won)
        {
            _log.Add(CampaignSession.LostLine(battle.State, Content));
            QueueCard(CampaignSession.LostCard);
            Over = true;
            return true;
        }

        var before = Record;
        Record = Record.AfterBattle(battle.State, Content);
        _log.Add(CampaignSession.WonLine(before, Record, battle.State, Content));
        QueueCard(CampaignSession.AfterCard(before, Content, battle.State.Map));
        if (Record.IsFinished(Content))
        {
            _log.Add(CampaignSession.CampaignWonLine(Record, Content));
            _log.AddRange(CampaignSession.EndingLines(Record, Content));
            Over = true;
            _saves?.RecordWin(Record.Difficulty);
        }

        Autosave();
        QueueBeforeCard();
        return true;
    }

    private void QueueCard(IReadOnlyList<string> lines)
    {
        if (lines.Count > 0)
        {
            _cards.Enqueue(lines);
        }
    }

    /// <summary>The next map's before card, queued on arriving at its camp, as the console's screen opens with it.</summary>
    private void QueueBeforeCard()
    {
        if (NextMap is { } map)
        {
            QueueCard(CampaignSession.BeforeCard(Record, Content, map));
        }
    }

    /// <summary>
    /// Autosaves the record as <c>ironwake campaign</c> does on arriving at a camp, so the title's
    /// Continue finds it (issue 677): only with a save store, and only while a map is still to march to.
    /// The event log is untouched, so a run with saves logs what one without them does.
    /// </summary>
    private void Autosave()
    {
        if (_saves is not null && !Over && !Record.IsFinished(Content))
        {
            _saves.Autosave(Record);
        }
    }

    private bool Screen(Func<ScreenResult> action)
    {
        if (!OnScreen())
        {
            return false;
        }

        var result = action();
        if (!result.Accepted)
        {
            return Refuse(result.Text);
        }

        Record = result.Record;
        _log.Add(CampaignSession.Text(Record, Content, result.Text));
        Status = null;
        return true;
    }

    private bool OnScreen()
    {
        if (Battle is not null)
        {
            return Refuse("a battle is open; leave it once it is decided");
        }

        return !Over || Refuse("the campaign is over");
    }

    /// <summary>Shows a refusal in the status as the console's <c>ERROR:</c> line reads it, by name, in sentence case (issue 615).</summary>
    private bool Refuse(string reason)
    {
        Status = CampaignSession.Text(Record, Content, reason);
        return false;
    }
}
