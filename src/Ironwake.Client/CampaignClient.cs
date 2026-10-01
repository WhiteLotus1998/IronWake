using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Core;

namespace Ironwake.Client;

/// <summary>
/// The thin renderer's campaign presenter (issue 360): what <c>ironwake campaign</c> does between
/// maps, beside <see cref="ClientSession"/>, with no rules in it. Buy, repair, certify, trial,
/// build, bench and unbench are the record's own actions, refused with the core's text; march
/// opens the next map as a <see cref="ClientSession"/> and leave takes the record back once the
/// battle is decided. Every line the screen shows is the console's, from the public statics on
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

    public CampaignClient(GameContent content, string contentDir, CampaignRecord record, RollScheme scheme = RollScheme.TwoRollAverage)
    {
        Content = content;
        _contentDir = contentDir;
        Record = record;
        _scheme = scheme;
    }

    public GameContent Content { get; }

    /// <summary>The campaign as the last screen action or battle left it.</summary>
    public CampaignRecord Record { get; private set; }

    /// <summary>The battle or trial being played, or null on the between-map screen.</summary>
    public ClientSession? Battle { get; private set; }

    /// <summary>Whether the battle being played is a certification trial.</summary>
    public bool InTrial => _trial is not null;

    /// <summary>Whether the campaign is over: every map won, or a battle lost and left.</summary>
    public bool Over { get; private set; }

    /// <summary>The last refusal for the status bar, never part of the event log.</summary>
    public string? Status { get; private set; }

    /// <summary>The campaign's event log as <c>campaign --log</c> writes it, each line ending in <c>\n</c>, the open battle's lines included.</summary>
    public string LogText => string.Concat(_log.Select(line => line + "\n")) + (Battle?.LogText ?? "");

    /// <summary>The next map as the record would fight it, or null once the campaign is over.</summary>
    public MapDefinition? NextMap => Over || Record.IsFinished(Content) ? null : CampaignSession.MapFor(_contentDir, Content, Record, Record.NextMap(Content).MapId);

    /// <summary>
    /// The between-map screen's lines, as the console prints them on arriving at it: the heading,
    /// the roster, the shop, the deployment, and the keep's menu once the raid is fought, after the
    /// map's text card when it has one (issue 631).
    /// </summary>
    public IReadOnlyList<string> ScreenLines()
    {
        if (NextMap is not { } map)
        {
            return Array.Empty<string>();
        }

        var lines = new List<string>(CampaignSession.BeforeCard(Record, Content, map)) { CampaignSession.ScreenHeading(Record, Content, map) };
        lines.AddRange(CampaignSession.RosterLines(Record, Content, typed: true));
        lines.AddRange(CampaignSession.ShopLines(Record, Content));
        lines.AddRange(CampaignSession.QuestLines(_contentDir, Content, Record));
        lines.Add(CampaignSession.DeploymentLine(Record, Content, map));
        if (Record.KeepMenuRefusal(Content) is null)
        {
            lines.AddRange(KeepLines());
        }

        return lines;
    }

    /// <summary>The keep's menu as the console prints it, on the keep this record would fight.</summary>
    public IReadOnlyList<string> KeepLines() => CampaignSession.KeepLines(_contentDir, Content, Record);

    /// <summary>The ids the shop sells before the next map, each what <see cref="Buy"/> takes.</summary>
    public IReadOnlyList<string> Stock => Over || Record.IsFinished(Content) ? Array.Empty<string>() : CampaignSession.Stock(Record, Content);

    public bool Buy(string itemId, string unitId) => Screen(() => Record.Buy(itemId, unitId, Content));

    /// <summary>Repairs the weapon in <paramref name="slot"/>, counted from 0.</summary>
    public bool Repair(string unitId, int slot) => Screen(() => Record.Repair(unitId, slot, Content));

    public bool Certify(string unitId, string classId) => Screen(() => Record.Certify(unitId, classId, Content));

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
            return true;
        }

        if (battle.State.Outcome.Result != BattleResult.Won)
        {
            _log.Add(CampaignSession.LostLine(battle.State, Content));
            Over = true;
            return true;
        }

        var before = Record;
        Record = Record.AfterBattle(battle.State, Content);
        _log.Add(CampaignSession.WonLine(before, Record, battle.State, Content));
        if (Record.IsFinished(Content))
        {
            _log.Add(CampaignSession.CampaignWonLine(Record, Content));
            Over = true;
        }

        return true;
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
