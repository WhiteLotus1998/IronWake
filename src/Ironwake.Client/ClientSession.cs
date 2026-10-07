using Ironwake.Cli;
using Ironwake.Core;

namespace Ironwake.Client;

/// <summary>
/// The move preview's line (issue 511): the tile and its terrain, the move it spends of the
/// unit's Mov, the terrain's avoid for the unit, and the coming enemy phase's verdict for a
/// stop there: how many enemies <c>threat</c> prices against it, what they deal if every
/// strike lands, and the sleeping groups that could strike it awake, named without numbers as
/// <c>threat</c> names them.
/// </summary>
public sealed record MovePreview(Coord Tile, string Terrain, int Cost, int Mov, int Avoid, int Strikers, int IfAllLand, IReadOnlyList<string> Asleep)
{
    /// <summary>True when no awake enemy the player can see prices a strike on the tile.</summary>
    public bool Safe => Strikers == 0;

    /// <summary>The verdict in two words, as the dot beside it says it in colour.</summary>
    public string Verdict => Safe ? "safe here" : Strikers == 1 ? "1 strikes" : $"{Strikers} strike";

    /// <summary>The line: tile, move spent, avoid, then the verdict with its numbers.</summary>
    public string Text =>
        $"{Terrain} {Tile.X},{Tile.Y}  move {Cost} of {Mov}  evade {Avoid}  {Verdict}"
        + (Safe ? "" : $" for {IfAllLand}")
        + (Asleep.Count > 0 ? $"  asleep: {string.Join(", ", Asleep)}" : "");
}

/// <summary>One forecast shown while a tile is hovered: the target, where it stands, the console's forecast text, and the same forecast as the drawn card's data.</summary>
public sealed record HoverForecast(string TargetId, Coord TargetAt, string Text, ForecastCard Card);

/// <summary>
/// One row of the attack menu (issue 611): the core's <see cref="AttackOption"/>, its label (the
/// weapon's name, or the art's with the weapon after it), one line of numbers or the refusal in
/// the rule's words, and the drawn card for a row the resolver would accept.
/// </summary>
public sealed record MenuRow(AttackOption Option, string Label, string Line, ForecastCard? Card)
{
    /// <summary>True when choosing the row strikes; a greyed row only teaches its refusal.</summary>
    public bool Legal => Option.Legal;
}

/// <summary>
/// The attack menu open on a target (issue 611): its rows in <see cref="Queries.AttackOptions"/>'s
/// order and the row whose card the panel shows, the first legal one until another is hovered.
/// </summary>
public sealed record AttackMenu(string UnitId, string TargetId, IReadOnlyList<MenuRow> Rows, int Hovered)
{
    /// <summary>The card the panel draws: the hovered row's, or the first legal row's when the hovered one is greyed.</summary>
    public ForecastCard? Card => Rows[Hovered].Card ?? Rows.FirstOrDefault(r => r.Legal)?.Card;
}

/// <summary>
/// One row of the battle's action list (issue 786): a command the selected unit can take that
/// no click on the board names, opening a chest beside it or the captain's Commander's Word,
/// with its label, one line in the console's words (the chest's contents, the order's preview)
/// and the resolver's refusal for a row it would refuse, which the row shows greyed.
/// </summary>
public sealed record ActionRow(string Label, string Line, Command Command, string? Refusal)
{
    /// <summary>True when choosing the row applies its command; a greyed row only teaches its refusal.</summary>
    public bool Legal => Refusal is null;

    /// <summary>
    /// The target pick choosing the row arms instead of applying <see cref="Command"/> (issue 1308):
    /// an item that needs a target, a heal or a cast, is aimed by a board click, and the drake's
    /// carry and breath by clicks in steps. Null for a row that applies at once.
    /// </summary>
    public TargetPick? Pick { get; init; }
}

/// <summary>
/// A pick an action row arms (issue 1308): the next board click aims it. Its marked tiles are the
/// ones the resolver accepts, so the client decides nothing.
/// </summary>
public abstract record TargetPick(string UnitId, string Label)
{
    /// <summary>What the player clicks next, as the status line says it.</summary>
    public abstract string Prompt { get; }

    /// <summary>Whether a click on <paramref name="at"/> is taken by the pick.</summary>
    public abstract bool Marks(Coord at);

    /// <summary>The command a click on <paramref name="at"/> submits, or null when the tile submits nothing.</summary>
    public abstract Command? At(Coord at);
}

/// <summary>
/// An armed item pick (issue 1308): the unit, the slot and the art an item row named, and every
/// tile a click may aim it at with the target word the core accepts there (the unit's id, or
/// <c>x,y</c> for ground or a body the cast takes by tile).
/// </summary>
public sealed record ItemPick(string UnitId, int Slot, string? Art, string Label, IReadOnlyDictionary<Coord, string> Targets) : TargetPick(UnitId, Label)
{
    /// <inheritdoc/>
    public override string Prompt => "click a marked target";

    /// <inheritdoc/>
    public override bool Marks(Coord at) => Targets.ContainsKey(at);

    /// <inheritdoc/>
    public override UseItem? At(Coord at) => Targets.TryGetValue(at, out var word) ? new UseItem(UnitId, Slot, word, Art) : null;
}

/// <summary>One way to finish a <see cref="StepPick"/>: the tiles clicked in turn and the command the last click submits.</summary>
public sealed record PickPath(IReadOnlyList<Coord> Clicks, Command Command);

/// <summary>
/// A pick taken in steps (issue 1308, slice 2): the drake's carry (the ally, where to fly, where to
/// set it down) and its breath (the first tile of the line). <see cref="Paths"/> holds every command
/// the resolver accepts with the clicks that name it; each step marks the tiles some remaining path
/// clicks next, and the last click submits the one path left.
/// </summary>
public sealed record StepPick(string UnitId, string Label, IReadOnlyList<string> Prompts, IReadOnlyList<PickPath> Paths, int Step = 0) : TargetPick(UnitId, Label)
{
    /// <inheritdoc/>
    public override string Prompt => Prompts[Step];

    /// <summary>Whether the next click is the last one, the one that submits.</summary>
    public bool Last => Step == Prompts.Count - 1;

    /// <inheritdoc/>
    public override bool Marks(Coord at) => Paths.Any(path => path.Clicks[Step] == at);

    /// <inheritdoc/>
    public override Command? At(Coord at) => Last && Paths.FirstOrDefault(path => path.Clicks[Step] == at) is { } path ? path.Command : null;

    /// <summary>The pick's next step after a click on <paramref name="at"/>, or null when the click is the last or takes nothing.</summary>
    public StepPick? After(Coord at)
    {
        var left = Paths.Where(path => path.Clicks[Step] == at).ToList();
        return Last || left.Count == 0 ? null : this with { Paths = left, Step = Step + 1 };
    }
}

/// <summary>
/// One side of the drawn forecast (issue 512): who, with what, standing where, and the source of
/// its avoid there (the terrain's name, tile and avoid for its movement), its HP now and at most,
/// what it would have left if every strike the other side can make lands, and its own strike as
/// the core forecast it (<see cref="SideForecast"/>), the very object the console's line formats.
/// </summary>
public sealed record ForecastSide(string Id, string Name, string ClassId, bool IsBoss, string Weapon, string Terrain, Coord Tile, int Avoid, int Hp, int MaxHp, int After, SideForecast Strike)
{
    /// <summary>The source of avoid as the card prints it beside the tile: <c>Forest 7,5 +20</c>.</summary>
    public string Ground => $"{Terrain} {Tile.X},{Tile.Y} +{Avoid}";

    /// <summary>The HP this side can lose to the other's strikes: the hatched part of its bar.</summary>
    public int Cost => Hp - After;
}

/// <summary>
/// The forecast as the showcase draws it (issue 512, <c>docs/look/forecast.svg</c>): the attacker
/// on its hovered tile, the defender where it stands, and whether the attack raises a blow instead
/// of fighting (DESIGN.md 13.16), which has no roll. Every number is a field of the
/// <see cref="CombatForecast"/> <see cref="PlaySession.ForecastText"/> prints, never recomputed.
/// </summary>
public sealed record ForecastCard(ForecastSide Attacker, ForecastSide Defender, bool Raises)
{
    /// <summary>
    /// The EXP the attacker earns if every strike lands (issue 533), DESIGN.md section 6 on the
    /// card's own reading: a landing strike, and the kill when the defender's HP after is 0.
    /// Set only when that EXP would carry a player unit across a level; null otherwise.
    /// </summary>
    public int? LevelUpExp { get; init; }

    /// <summary>The line the card prints when <see cref="LevelUpExp"/> is set: <c>+34 EXP, level up</c>.</summary>
    public string? LevelUpLine => LevelUpExp is { } exp ? $"+{exp} EXP, level up" : null;

    /// <summary>Who strikes first, the card's heading.</summary>
    public string Heading => $"{Attacker.Name} strikes first";

    /// <summary>The doubling note under the pips: who doubles, or that neither does.</summary>
    public string Doubling =>
        (Attacker.Strike.Doubles, Defender.Strike.Doubles) switch
        {
            (true, true) => "both double",
            (true, false) => $"{Attacker.Name} doubles",
            (false, true) => $"{Defender.Name} doubles",
            _ => "neither doubles",
        };
}

/// <summary>
/// The drawn unit card (issue 512): the values the console's <c>show</c> prints on its first
/// three lines, as data: name, class and level, the side, HP, the stats, Mov, the equipped
/// weapon's name (or <c>unarmed</c>), and the terrain the unit stands on; and a player unit's
/// EXP toward its next level (issue 533), null for an enemy, which earns none (DECISIONS/0017).
/// </summary>
public sealed record UnitCard(string Id, string Name, string ClassId, string ClassName, int Level, Side Side, bool IsCaptain, bool IsBoss, int Hp, int MaxHp, Stats Stats, int Mov, string Weapon, string WeaponLine, string Terrain, Coord At)
{
    /// <summary>EXP toward the next level, 0 to 99, for a player unit; null for an enemy.</summary>
    public int? Exp { get; init; }
}

/// <summary>
/// One enemy's reach as the board draws it (issue 533): every tile it could strike next phase,
/// <see cref="Ironwake.Core.Threat.StruckByUnit"/>'s strike set. A sleeping Guard's reach is the
/// set it would have awake and <see cref="Asleep"/> is true, so the board draws it faint; its
/// <see cref="WakeRing"/> is every tile a player unit ending there would wake the group from
/// (within the content's wake radius of a member), empty for an enemy already awake.
/// </summary>
public sealed record EnemyReach(string Id, Coord At, IReadOnlySet<Coord> Tiles, bool Asleep, IReadOnlySet<Coord> WakeRing);

/// <summary>
/// What the board marks for the event line last revealed in the enemy phase (issue 349): the
/// line itself, the tile the actor started from, the tile it ended on or acted from, the path
/// between them, and the tile it struck. Each is null where the event has none; a line the dark
/// hides carries no tiles at all, since marking them would light the dark.
/// </summary>
public sealed record Highlight(string Line, Coord? From, Coord? To, IReadOnlyList<Coord> Path, Coord? Struck);

/// <summary>
/// The thin renderer's presenter (issue 347): everything the Godot client shows, with no
/// rules in it. Reach, targets and forecasts are core queries; every line of the event log
/// is the console's own text for an event (<see cref="PlaySession.Describe(GameEvent, GameContent, UnitNames)"/>),
/// so the log equals what <c>ironwake play --log</c> writes for the same commands, which the
/// parity gate checks byte for byte. The enemy phase does not resolve as one jump: after
/// <c>end</c> the planner's commands wait in a queue, and <see cref="Step"/> reveals one event
/// line at a time, applying each command as its first line is shown.
/// </summary>
public sealed class ClientSession
{
    private readonly List<string> _log = new();
    private readonly Queue<Command> _enemy = new();
    private readonly Queue<(Highlight Mark, Beat? Beat, ActCard? Act, bool First)> _pending = new();
    private int _actLogStart;
    private readonly List<FallenMark> _fallen = new();
    private readonly Dictionary<string, BattleUnit> _ghosts = new();

    /// <summary>
    /// The command applied from each history state, by index, as the console's <c>recall list</c>
    /// names it (issue 353): kept by the same rule as the console's, so the Recall browser's rows
    /// read the same. A Recall truncates it to the state it returns to.
    /// </summary>
    private readonly List<string> _made = new();

    /// <summary>The log's length when each history state was the board, by history index, so a Recall knows which lines it undid.</summary>
    private readonly Dictionary<int, int> _logAt = new();

    /// <summary>The log's ranges a Recall undid, start inclusive and end exclusive.</summary>
    private readonly List<(int Start, int End)> _undone = new();

    public ClientSession(GameContent content, BattleState state)
    {
        Content = content;
        State = state;
    }

    public GameContent Content { get; }

    /// <summary>The board as the client draws it: the state after the last command applied.</summary>
    public BattleState State { get; private set; }

    /// <summary>Every event revealed so far, in order, as the console prints it; a combat's entry runs over several lines.</summary>
    public IReadOnlyList<string> Log => _log;

    /// <summary>The event log as <c>play --log</c> writes it: each line ending in <c>\n</c>.</summary>
    public string LogText => string.Concat(_log.Select(line => line + "\n"));

    /// <summary>The last refusal or hint for the status bar, never part of the event log.</summary>
    public string? Status { get; private set; }

    /// <summary>
    /// The enemy-phase event last revealed by <see cref="Step"/>, its line the newest in the
    /// log, kept until the next player command so the phase's last event stays marked; null
    /// before any enemy phase and after a player command.
    /// </summary>
    public Highlight? Playing { get; private set; }

    /// <summary>
    /// What the client animates for the last thing revealed (issue 513): every beat of a player
    /// command's events at once, or the one beat of the enemy-phase line <see cref="Step"/> just
    /// showed; empty when it animates nothing. <see cref="BeatSerial"/> counts each new set.
    /// </summary>
    public IReadOnlyList<Beat> Beats { get; private set; } = Array.Empty<Beat>();

    /// <summary>Rises by one each time <see cref="Beats"/> is replaced, so a renderer knows to start playing them.</summary>
    public int BeatSerial { get; private set; }

    /// <summary>
    /// The units fallen this phase (issue 513, round 141), each with the tile it fell on, so a
    /// death is read on the board and not three log lines later. Cleared when a phase ends,
    /// by the player's first command after the enemy phase, and by a Recall.
    /// </summary>
    public IReadOnlyList<FallenMark> Fallen => _fallen;

    /// <summary>
    /// Units the enemy phase has killed whose death line is not yet shown: the board draws them
    /// where they stood until the line that names their death, so the board is never ahead of the log.
    /// </summary>
    public IReadOnlyCollection<BattleUnit> Ghosts => _ghosts.Values;

    /// <summary>
    /// The enemy-act card (issue 513): the newest act the enemy phase has shown, or null outside
    /// it. A move, wait or other act without a strike leaves the phase's last strike's card up
    /// (issue 544).
    /// </summary>
    public ActCard? Act { get; private set; }

    /// <summary>
    /// The log while the enemy phase plays (issue 544, round 148): the lines of the command being
    /// shown, from its first revealed line to the newest; the whole log once the phase has played.
    /// </summary>
    public IReadOnlyList<string> ActLog => EnemyPhasePlaying && Playing is not null ? _log.Skip(_actLogStart).ToList() : _log;

    /// <summary>
    /// The enemy-act card as the panel shows it (issue 514, round 151): the act while the enemy
    /// phase plays or its last beat is still on screen, and nothing once the phase has flipped
    /// to the player, so a stale <c>ENEMY ACT</c> never sits under the player's own phase.
    /// </summary>
    public ActCard? ActShown(bool beatsPlaying) => EnemyPhasePlaying || beatsPlaying ? Act : null;

    /// <summary>
    /// The states the last Recall folded back through (issue 514), from the state it left to the
    /// one it returned to, newest first: the renderer scrubs the board through them, each step
    /// for its <see cref="ScrubLengths"/>. Empty before any Recall. <see cref="ScrubSerial"/> counts each new one.
    /// </summary>
    public IReadOnlyList<BattleState> Scrub { get; private set; } = Array.Empty<BattleState>();

    /// <summary>How long each step of <see cref="Scrub"/> plays (issue 515), by <see cref="Rhythm.ScrubLengths"/>.</summary>
    public IReadOnlyList<float> ScrubLengths { get; private set; } = Array.Empty<float>();

    /// <summary>
    /// Whether the log line at <paramref name="index"/> of <see cref="Log"/> tells of something a
    /// Recall undid (issue 515): a line printed after the state a Recall returned to and before
    /// the Recall's own line. The log keeps it, as the console's does; the column dims it, so the
    /// log agrees with the board about what is true.
    /// </summary>
    public bool Undone(int index) => _undone.Any(range => index >= range.Start && index < range.End);

    /// <summary>
    /// How the battle ended, for the end card (issue 515), once it is decided and the enemy phase
    /// has played out; null before.
    /// </summary>
    public EndCard? Ending => State.Outcome.IsOver && !EnemyPhasePlaying ? EndCard.Of(State, Content) : null;

    /// <summary>Rises by one each time <see cref="Scrub"/> is replaced, so a renderer knows to start the rewind.</summary>
    public int ScrubSerial { get; private set; }

    /// <summary>The Recall charges the map opened with, for the pips beside the ones left (issue 514).</summary>
    public int RecallChargesAtStart => State.History.Count > 0 ? State.History[0].RecallCharges : State.RecallCharges;

    /// <summary>
    /// The map's objective in the player's words, shown for the whole battle (issue 374), the
    /// console's line without its "Objective: " label (issue 609).
    /// </summary>
    public string Objective => Ironwake.Core.Objective.Line(State, Content);

    /// <summary>Why a lost battle was lost, in the console's words, or null while ongoing or won (issue 374).</summary>
    public string? Verdict => EnemyPhasePlaying ? null : Ironwake.Core.Objective.Verdict(State, Content);

    /// <summary>
    /// The legend's hover card for a terrain (issue 610): what the ground does for a unit on it on
    /// this map, the console's <c>terrain</c> card and the protocol's <c>terrain</c> text.
    /// </summary>
    public string TerrainText(string terrainId) => TerrainCard.Text(State, Content, terrainId);

    /// <summary>The selected player unit's id, or null.</summary>
    public string? Selected { get; private set; }

    /// <summary>
    /// The selected unit's reach, as the core answers it, or null with no selection: the move a
    /// Fall back order owes it while one is owed (issue 786), else its move or Canto.
    /// </summary>
    public Reach? Reach => Selected is { } id && State.Find(id) is { } unit ? State.FallBackReachOf(unit, Content) ?? Queries.Reachable(State, Content, unit) : null;

    /// <summary>The tiles of the chests not yet opened (issue 786), which the board draws; an opened chest is gone from the board.</summary>
    public IReadOnlyList<Coord> Chests => State.ClosedChests.Select(chest => chest.At).ToList();

    /// <summary>
    /// The action list for the selected unit (issue 786), empty with none selected or while the
    /// enemy phase plays: one row per closed chest on or beside its tile (<c>open</c>), then, for the
    /// pick or the captain beside the claimant who came back as a foe, a <c>talk</c> row (issue 633), then, for
    /// the captain on a map where orders are open and unspent, one row per order (<c>order</c>),
    /// its line the console's <c>order ... preview</c> from <paramref name="from"/> (a hovered
    /// tile, to read a call from there before moving) or from where he stands; then, for a unit
    /// that has not acted, its item rows (issue 1308, <see cref="ItemRows"/>) and, on an exit tile
    /// of an Escape map, an <c>exit</c> row whose line is the console's exit warning.
    /// </summary>
    public IReadOnlyList<ActionRow> Actions(Coord? from = null)
    {
        if (EnemyPhasePlaying || Selected is not { } id || State.Find(id) is not { } unit)
        {
            return Array.Empty<ActionRow>();
        }

        var rows = new List<ActionRow>();
        var names = UnitNames.Of(State, Content);
        foreach (var chest in State.ClosedChests.Where(chest => chest.At.DistanceTo(unit.At) <= 1))
        {
            var open = new Open(unit.Id, chest.At);
            var refusal = Resolver.Apply(State, Content, open).Rejection is { } rejected ? names.Message(rejected.Message) : null;
            var holds = string.Join(", ", chest.Items.Select(Content.ItemName));
            rows.Add(new ActionRow($"Open chest {chest.At.X},{chest.At.Y}", holds, open, refusal));
        }

        if (Returned.On(State) is { } returned && (unit.IsCaptain || unit.Id == State.Return!.PickId) && returned.At.DistanceTo(unit.At) == 1)
        {
            var talk = new Talk(unit.Id, returned.Id);
            var refusal = Resolver.Apply(State, Content, talk).Rejection is { } rejected ? names.Message(rejected.Message) : null;
            var fate = Returned.FateOf(State, unit) == ReturnFate.Turned ? "turns them: off the field, joins if a bed is free" : "spares them: off the field, never joins";
            rows.Add(new ActionRow($"Talk to {names[returned.Id]}", fate, talk, refusal));
        }

        if (unit.IsCaptain && State.OrdersOpen && State.OrderCalled is null)
        {
            var at = from is { } tile && (tile == unit.At || Reach?.CanEnd(tile) == true) ? tile : (Coord?)null;
            foreach (var kind in new[] { OrderKind.Press, OrderKind.Rally, OrderKind.FallBack })
            {
                var refusal = Orders.Refusal(State) is { } why ? names.Message($"cannot call {Orders.Word(kind)}: {why}") : null;
                var word = Orders.Word(kind);
                rows.Add(new ActionRow($"Order: {char.ToUpperInvariant(word[0])}{word[1..]}", PlaySession.OrderPreview(State, Content, kind, at), new Order(kind), refusal));
            }
        }

        if (!unit.Acted)
        {
            rows.AddRange(ItemRows(unit, names));
        }

        if (State.Map.Win == WinCondition.Escape && State.Map.IsExit(unit.At) && !unit.Acted)
        {
            var exit = new Exit(unit.Id);
            var refusal = Resolver.Apply(State, Content, exit).Rejection is { } rejected ? names.Message(rejected.Message) : null;
            var line = PlaySession.ExitLine(State, Content, unit.Id, Campaign) ?? (unit.IsCaptain ? "leaves the field; the battle ends" : "leaves the field");
            rows.Add(new ActionRow("Exit", line, exit, refusal));
        }

        if (!unit.Acted)
        {
            rows.AddRange(DrakeRows(unit, names));
        }

        return rows;
    }

    private (BattleState State, string Unit, IReadOnlyList<ActionRow> Rows)? _drakeRows;

    /// <summary>
    /// The drake's rows of the action list (issue 1308, slice 2), last so none moves: <c>Carry</c>
    /// for a rider whose drake can lift where the carry is open, and <c>Breathe</c> for one whose
    /// drake can breathe where the breath is open. Each arms a <see cref="StepPick"/> of every
    /// command the resolver accepts: the carry clicks the ally, then the tile to fly to, then the
    /// tile to set it down on; the breath clicks the first tile of its line. A row with nothing the
    /// core accepts is greyed with the core's refusal, or "no carry in reach" when the rider could
    /// fly but no ally beside it can be set down anywhere.
    /// </summary>
    private IReadOnlyList<ActionRow> DrakeRows(BattleUnit unit, UnitNames names)
    {
        if (_drakeRows is { } cached && ReferenceEquals(cached.State, State) && cached.Unit == unit.Id)
        {
            return cached.Rows;
        }

        var rows = new List<ActionRow>();
        if (DrakeCarry.Open(State) && DrakeCarry.CanLift(unit))
        {
            var paths = new List<PickPath>();
            var allies = unit.At.Neighbors().Select(UnitAt).OfType<BattleUnit>().Where(u => u.Side == unit.Side).ToList();
            foreach (var ally in allies)
            {
                var reach = State.WithoutUnit(ally.Id).ReachOf(unit, Content);
                foreach (var entry in reach.Entries.Where(e => e.CanEnd))
                {
                    foreach (var setDown in entry.At.Neighbors())
                    {
                        var carry = new Carry(unit.Id, ally.Id, entry.At, setDown);
                        if (Resolver.Apply(State, Content, carry).Accepted)
                        {
                            paths.Add(new PickPath(new[] { ally.At, entry.At, setDown }, carry));
                        }
                    }
                }
            }

            string? refusal = null;
            if (paths.Count == 0)
            {
                var probe = new Carry(unit.Id, allies.FirstOrDefault()?.Id ?? "", unit.At, unit.At);
                var rejected = Resolver.Apply(State, Content, probe).Rejection;
                refusal = rejected is null || (rejected.Reason == RejectionReason.CannotCarry && RiderCanFly(unit))
                    ? (allies.Count == 0 ? "no ally beside it to carry" : "no carry in reach")
                    : names.Message(rejected.Message);
            }

            var prompts = new[] { "click the ally to lift", "click where to fly", "click where to set it down" };
            rows.Add(new ActionRow("Carry", "lift an ally beside it, fly, and set it down; the whole turn", new Carry(unit.Id, "", unit.At, unit.At), refusal)
            {
                Pick = new StepPick(unit.Id, "Carry", prompts, paths),
            });
        }

        if (Rime.Open(State) && Rime.CanBreathe(unit))
        {
            var paths = new List<PickPath>();
            Rejection? rejected = null;
            foreach (var toward in unit.At.Neighbors().Where(State.Map.Contains))
            {
                var breathe = new Breathe(unit.Id, toward);
                var result = Resolver.Apply(State, Content, breathe);
                if (result.Accepted)
                {
                    paths.Add(new PickPath(new[] { toward }, breathe));
                }
                else
                {
                    rejected ??= result.Rejection;
                }
            }

            var refusal = paths.Count == 0 && rejected is not null ? names.Message(rejected.Message) : null;
            rows.Add(new ActionRow("Breathe", "rime down a line of three: chills who stands there, freezes water; once a map", new Breathe(unit.Id, unit.At), refusal)
            {
                Pick = new StepPick(unit.Id, "Breathe", new[] { "click the first tile of the line" }, paths),
            });
        }

        _drakeRows = (State, unit.Id, rows);
        return rows;
    }

    /// <summary>
    /// Whether the core's refusals that come before the ally (the carry's phase, the rider's Move,
    /// its grounding) all pass, so an empty carry pick is about the allies and not the rider.
    /// </summary>
    private bool RiderCanFly(BattleUnit unit) => !unit.Moved && !unit.Shoved && unit.Grounded <= 0;

    /// <summary>Whether this battle is part of a campaign, so the exit row warns that left behind counts as fallen, as the console does.</summary>
    public bool Campaign { get; init; }

    /// <summary>The armed pick (issue 1308), an item's or the drake's, or null: the next board click aims it, Esc closes it.</summary>
    public TargetPick? Pick { get; private set; }

    private (BattleState State, string Unit, IReadOnlyList<ActionRow> Rows)? _itemRows;

    /// <summary>
    /// The item rows of the action list (issue 1308), after every other row so none moves: one row
    /// per inventory slot the Item action takes (a consumable, a heal, a cast that raises, sunders,
    /// armors or raises the dead; a tome is read at camp and has none), then one per heal art with a
    /// target in reach. A row whose item needs a target carries an <see cref="ItemPick"/> of the tiles
    /// the resolver accepts; one with none in reach is greyed. The line is the console's own
    /// <c>show</c> slot, uses left included.
    /// </summary>
    private IReadOnlyList<ActionRow> ItemRows(BattleUnit unit, UnitNames names)
    {
        if (_itemRows is { } cached && ReferenceEquals(cached.State, State) && cached.Unit == unit.Id)
        {
            return cached.Rows;
        }

        var rows = new List<ActionRow>();
        for (var slot = 0; slot < unit.Unit.Inventory.Count; slot++)
        {
            var stack = unit.Unit.Inventory.Items[slot];
            var name = Heirloom.Name(stack, Content);
            var line = $"{slot + 1}: {name}{Keepsake.Suffix(stack, Content)} x{stack.Uses}";
            if (Content.Items.TryGetValue(stack.ItemId, out var item))
            {
                if (item.Teaches is null)
                {
                    rows.Add(Applied($"Item: {name}", line, new UseItem(unit.Id, slot), names));
                }

                continue;
            }

            if (!Content.Weapons.ContainsKey(stack.ItemId))
            {
                continue;
            }

            var spell = Content.WeaponOf(unit.Unit, Content.Weapon(stack.ItemId));
            var byTile = Sunder.Sunders(Content, spell) || Hollow.Raises(Content, spell);
            if (Armor.Armors(Content, spell))
            {
                rows.Add(Applied($"Item: {name}", line, new UseItem(unit.Id, slot), names));
            }
            else if (spell.Heals || byTile || Earthwork.Rider(Content, spell) is not null)
            {
                rows.Add(Aimed($"Item: {name}", line, unit, slot, null, byTile, names));
                if (spell.Heals)
                {
                    foreach (var (ability, _) in Content.HealArtsOf(unit.Unit))
                    {
                        var art = Aimed($"Item: {name}, {PlaySession.AbilityName(ability.Id, Content)}", line, unit, slot, ability.Id, byTile, names);
                        if (art.Legal)
                        {
                            rows.Add(art);
                        }
                    }
                }
            }
        }

        _itemRows = (State, unit.Id, rows);
        return rows;
    }

    private ActionRow Applied(string label, string line, UseItem use, UnitNames names) =>
        new(label, line, use, Resolver.Apply(State, Content, use).Rejection is { } rejected ? names.Message(rejected.Message) : null);

    /// <summary>
    /// An item row that arms a pick: every unit's tile, and with <paramref name="byTile"/> every
    /// other tile too, probed through the resolver with the word the console would type there.
    /// With no target in reach the row is greyed with the core's refusal, or "no target in reach"
    /// when the only refusal is the missing target.
    /// </summary>
    private ActionRow Aimed(string label, string line, BattleUnit unit, int slot, string? art, bool byTile, UnitNames names)
    {
        var targets = new Dictionary<Coord, string>();
        var tiles = byTile
            ? Enumerable.Range(0, State.Map.Height).SelectMany(y => Enumerable.Range(0, State.Map.Width).Select(x => new Coord(x, y)))
            : State.Units.Select(u => u.At);
        foreach (var tile in tiles)
        {
            var word = UnitAt(tile)?.Id ?? $"{tile.X},{tile.Y}";
            if (Resolver.Apply(State, Content, new UseItem(unit.Id, slot, word, art)).Accepted)
            {
                targets[tile] = word;
            }
        }

        var bare = new UseItem(unit.Id, slot, null, art);
        string? refusal = null;
        if (targets.Count == 0)
        {
            var rejected = Resolver.Apply(State, Content, bare).Rejection;
            refusal = rejected is null || rejected.Reason == RejectionReason.NoTarget ? "no target in reach" : names.Message(rejected.Message);
        }

        return new ActionRow(label, line, bare, refusal) { Pick = new ItemPick(unit.Id, slot, art, label, targets) };
    }

    /// <summary>
    /// What aiming the armed pick at <paramref name="tile"/> would print (issue 1308): the console's
    /// own event lines for the use, the heal's amount and the HP it ends at, or the cast's effect,
    /// read from the resolver without applying it. Experience and level lines are left out; they
    /// are the result, not the choice. A step of the drake's carry before the last names what the
    /// click picks and what comes next. Empty when the tile is no target.
    /// </summary>
    public IReadOnlyList<string> PickPreview(Coord tile)
    {
        if (Pick is StepPick { Last: false } step && step.After(tile) is { } next)
        {
            var picked = UnitAt(tile) is { } lifted && step.Step == 0 ? $"lift {UnitNames.Of(State, Content)[lifted.Id]}" : $"fly to {tile}";
            return new[] { $"{picked}; then {next.Prompt}" };
        }

        if (Pick?.At(tile) is not { } use || Resolver.Apply(State, Content, use) is not { Accepted: true } result)
        {
            return Array.Empty<string>();
        }

        var names = UnitNames.Of(result.Next, Content);
        return result.Events
            .Where(e => e is not (ExpGained or LeveledUp or RankRaised or MasteryEarned))
            .Select(e => PlaySession.Describe(e, Content, names))
            .ToList();
    }

    /// <summary>
    /// The tiles aiming the armed pick at <paramref name="tile"/> would strike (issue 1308, slice 2):
    /// the breath's line, read from the core's own <see cref="Rime.LineOf"/>, so the board can mark
    /// it on hover. Empty for any other pick or a tile it does not take.
    /// </summary>
    public IReadOnlyList<Coord> PickArea(Coord tile) =>
        Pick?.At(tile) is Breathe breathe && State.Find(breathe.UnitId) is { } rider
            ? Rime.LineOf(State.Map, rider.At, breathe.Toward)
            : Array.Empty<Coord>();

    /// <summary>
    /// Takes the action row at <paramref name="row"/> of <see cref="Actions"/>: its command goes
    /// through <see cref="Submit"/>. A greyed row puts its refusal in <see cref="Status"/>.
    /// Returns the command applied, or null.
    /// </summary>
    public Command? TakeAction(int row)
    {
        var rows = Actions();
        if (row < 0 || row >= rows.Count)
        {
            return null;
        }

        if (rows[row].Refusal is { } refusal)
        {
            Status = refusal;
            return null;
        }

        if (rows[row].Pick is { } pick)
        {
            Menu = null;
            Pick = pick;
            Status = $"{pick.Label}: {pick.Prompt}";
            return null;
        }

        return Submit(rows[row].Command) ? rows[row].Command : null;
    }

    /// <summary>Whether enemy-phase events are still waiting to be shown; no player command is taken until they are.</summary>
    public bool EnemyPhasePlaying => _pending.Count > 0 || _enemy.Count > 0;

    /// <summary>The living unit on a tile, or null.</summary>
    public BattleUnit? UnitAt(Coord at) => State.Units.FirstOrDefault(u => u.At == at);

    /// <summary>
    /// Selects the player unit on a tile, if it has not acted or is owed a Canto or a Fall back
    /// move (issue 786); anything
    /// else clears the selection. True when a unit is selected.
    /// </summary>
    public bool Select(Coord at)
    {
        Selected = UnitAt(at) is { Side: Side.Player } unit && (!unit.Acted || State.CantoReachOf(unit, Content) is not null || unit.FallingBack) ? unit.Id : null;
        Inspected = Selected is null && UnitAt(at) is { Side: Side.Enemy } enemy && Dusk.Seen(State, enemy) ? enemy.Id : null;
        return Selected is not null;
    }

    /// <summary>Clears the selected unit, the inspected enemy and the attack menu.</summary>
    public void ClearSelection() => (Selected, Inspected, Menu, Pick) = (null, null, null, null);

    /// <summary>
    /// The seen enemy a click picked out when no unit of ours was selected (issue 533), whose reach
    /// the board draws; null when none is, and cleared by the next selection.
    /// </summary>
    public string? Inspected { get; private set; }

    /// <summary>The inspected enemy's reach, or null when none is inspected or it has died or gone dark since.</summary>
    public EnemyReach? InspectedReach => Inspected is { } id && State.Find(id) is { } enemy && Dusk.Seen(State, enemy) ? ReachOfEnemy(enemy) : null;

    /// <summary>
    /// The one enemy reach the board draws without the threat overlay (issues 533 and 677): the
    /// inspected enemy's, else, with <paramref name="onHover"/> (the profile's
    /// <c>reach-on-hover</c>), the seen enemy under <paramref name="hover"/> while no unit is
    /// selected and the player's phase is waiting, else none.
    /// </summary>
    public EnemyReach? ReachShown(Coord? hover, bool onHover) =>
        InspectedReach ?? (onHover && hover is { } at && Selected is null && !EnemyPhasePlaying ? EnemyReachAt(at) : null);

    /// <summary>
    /// The unit panel (issue 349): the console's <c>show</c> lines for the unit on a tile, or
    /// null for an empty tile or an enemy the dark hides, which the console refuses to show.
    /// </summary>
    public IReadOnlyList<string>? Show(Coord at) =>
        UnitAt(at) is { } unit && Dusk.Seen(State, unit) ? PlaySession.ShowLines(State, Content, unit) : null;

    /// <summary>
    /// The forecast against each enemy the selected unit could strike from <paramref name="tile"/>
    /// (issue 151's mechanic, where it is felt): one entry per target the core forecasts from
    /// there, with the console's text. Empty with no selection or from a tile it cannot stand on.
    /// </summary>
    public IReadOnlyList<HoverForecast> Hover(Coord tile)
    {
        if (Selected is not { } id || State.Find(id) is not { } unit || EnemyPhasePlaying)
        {
            return Array.Empty<HoverForecast>();
        }

        var lines = new List<HoverForecast>();
        foreach (var target in State.UnitsOf(Side.Enemy).OrderBy(u => u.Id, StringComparer.Ordinal))
        {
            if (Queries.Forecast(State, Content, unit, target, tile) is { } forecast)
            {
                lines.Add(new HoverForecast(target.Id, target.At, PlaySession.ForecastText(State, Content, unit, target, forecast, tile, tile != unit.At), CardOf(unit, target, forecast, tile)));
            }
        }

        return lines;
    }

    /// <summary>
    /// The drawn forecast's data for <paramref name="unit"/> striking <paramref name="target"/>
    /// from <paramref name="tile"/>: both sides read from the one <see cref="CombatForecast"/>.
    /// A side's HP after is its HP less the other side's damage times every strike it can make,
    /// never below 0, the no-crit reading of <c>threat</c>'s "if all land".
    /// </summary>
    private ForecastCard CardOf(BattleUnit unit, BattleUnit target, CombatForecast forecast, Coord tile, string? weaponLabel = null)
    {
        var raises = Windup.Raises(State, Resolver.ChooseWeapon(unit, Content, null).Weapon);
        ForecastSide Side(BattleUnit who, Coord at, SideForecast own, SideForecast against)
        {
            var terrain = State.Map.TerrainAt(at, Content);
            var max = who.MaxHp(Content);
            var lost = against.Strikes ? against.Damage * against.StrikeCount : 0;
            var weapon = who == unit && weaponLabel is not null ? weaponLabel : who.EquippedWeapon(Content)?.Name ?? "unarmed";
            return new ForecastSide(who.Id, who.Unit.Name, who.Unit.ClassId, who.IsBoss, weapon, terrain.Name, at,
                terrain.AvoidFor(Content.Class(who.Unit.ClassId).Movement), who.Hp, max, Math.Max(0, who.Hp - lost), own);
        }

        var attacker = Side(unit, tile, forecast.Attacker, forecast.Defender);
        var defender = Side(target, target.At, forecast.Defender, forecast.Attacker);
        return new ForecastCard(attacker, defender, raises) { LevelUpExp = raises ? null : LevelUpExp(unit, target, forecast.Attacker, defender.After) };
    }

    /// <summary>
    /// Section 6's EXP for <paramref name="unit"/>'s combat with <paramref name="target"/> if every
    /// strike lands (issue 533), when it would cross a level: a strike that can land earns the
    /// strike formula, and the kill when <paramref name="targetAfter"/> is 0. Null for an enemy
    /// attacker, a unit at the level cap, a strike that cannot land, or EXP short of the next level;
    /// a raised blow (DESIGN.md 13.16) is no combat and earns none here.
    /// </summary>
    private static int? LevelUpExp(BattleUnit unit, BattleUnit target, SideForecast strike, int targetAfter)
    {
        if (unit.Side != Side.Player || unit.Unit.Level >= Unit.MaxLevel || !strike.Strikes || strike.HitChance <= 0)
        {
            return null;
        }

        var exp = Experience.ForCombat(unit.Unit.Level, target.Unit.Level, landed: true, killed: targetAfter == 0, boss: target.IsBoss);
        return unit.Unit.Exp + exp >= Experience.LevelUpAt ? exp : null;
    }

    /// <summary>
    /// The unit card (issue 512) for the unit on a tile, or null for an empty tile or an enemy the
    /// dark hides, as <see cref="Show"/> refuses it; read from the values <c>show</c> prints.
    /// </summary>
    public UnitCard? Card(Coord at)
    {
        if (UnitAt(at) is not { } unit || !Dusk.Seen(State, unit))
        {
            return null;
        }

        var stats = Content.StatsOf(unit.Unit);
        var unitClass = Content.Class(unit.Unit.ClassId);
        return new UnitCard(unit.Id, unit.Unit.Name, unit.Unit.ClassId, unitClass.Name, unit.Unit.Level, unit.Side, unit.IsCaptain, unit.IsBoss,
            unit.Hp, stats.Hp, stats, unitClass.Mov, unit.EquippedWeapon(Content)?.Name ?? "unarmed", PlaySession.WeaponLine(unit, Content), State.Map.TerrainAt(unit.At, Content).Name, unit.At)
        {
            Exp = unit.Side == Side.Player ? unit.Unit.Exp : null,
        };
    }

    /// <summary>
    /// The move preview (issue 511): the one line the forecast slot shows for a hovered tile
    /// with no strike to price, read from the core's <c>threat</c> queries (the priced rows themselves are cut, issue 625). Null
    /// with no selection, while the enemy phase plays, from a tile the unit cannot end on, and
    /// whenever <see cref="Hover"/> has a strike to price there.
    /// </summary>
    public MovePreview? Preview(Coord tile) => Hover(tile).Count > 0 ? null : Stop(tile);

    /// <summary>
    /// The move preview's values for a stop on <paramref name="tile"/> whether or not a strike
    /// is priced there (issue 512): the drawn forecast prints it under the card, as the threat
    /// on the selected unit in the preview's own words. Null with no selection, while the enemy
    /// phase plays, and from a tile the unit cannot end on.
    /// </summary>
    public MovePreview? Stop(Coord tile)
    {
        if (Selected is not { } id || State.Find(id) is not { } unit || EnemyPhasePlaying
            || Reach?.EntryAt(tile) is not { CanEnd: true } entry
            || Queries.Threats(State, Content, unit, tile) is not { } lines)
        {
            return null;
        }

        var terrain = State.Map.TerrainAt(tile, Content);
        var strikers = lines.Count(line => !line.Raises && line.HeldBy is null);
        var asleep = (Queries.SleepingThreats(State, Content, unit, tile) ?? Array.Empty<SleepingThreat>()).Select(group => group.Group).ToList();
        return new MovePreview(tile, terrain.Name, entry.Cost, Reach!.Mov, terrain.AvoidFor(Reach.Movement), strikers, Queries.IfAllLand(lines), asleep);
    }

    /// <summary>
    /// Every tile an enemy the player can see could strike next phase (issue 511), the strike
    /// set of DESIGN.md section 8 as <see cref="Ironwake.Core.Threat.StruckByUnit"/> answers it
    /// for each seen enemy; an enemy the dark hides adds nothing, so the hatch never lights the dark.
    /// </summary>
    public IReadOnlySet<Coord> EnemyThreat =>
        State.UnitsOf(Side.Enemy).Where(enemy => Dusk.Seen(State, enemy))
            .SelectMany(enemy => Ironwake.Core.Threat.StruckByUnit(State, Content, enemy))
            .ToHashSet();

    /// <summary>
    /// Every seen enemy's reach (issue 533), in ascending id: the board's overlay reads this, so
    /// its tiles are <see cref="Ironwake.Core.Threat.StruckByUnit"/>'s and can never disagree with
    /// the planner's strike set. A sleeping Guard's tiles are read on the board with its group
    /// woken, and its wake ring from the content's wake radius; an enemy the dark hides is left out.
    /// </summary>
    public IReadOnlyList<EnemyReach> EnemyReaches =>
        State.UnitsOf(Side.Enemy).Where(enemy => Dusk.Seen(State, enemy)).OrderBy(enemy => enemy.Id, StringComparer.Ordinal)
            .Select(ReachOfEnemy).ToList();

    /// <summary>The seen enemy on <paramref name="at"/>'s reach, or null when no seen enemy stands there.</summary>
    public EnemyReach? EnemyReachAt(Coord at) =>
        UnitAt(at) is { Side: Side.Enemy } enemy && Dusk.Seen(State, enemy) ? ReachOfEnemy(enemy) : null;

    private EnemyReach ReachOfEnemy(BattleUnit enemy)
    {
        if (enemy is not { Behavior: Behavior.Guard, Group: { } group } || State.IsAwake(group))
        {
            return new EnemyReach(enemy.Id, enemy.At, Ironwake.Core.Threat.StruckByUnit(State, Content, enemy), false, new HashSet<Coord>());
        }

        var woken = State.Wake(group);
        var members = State.UnitsOf(Side.Enemy).Where(u => u.Group == group).Select(u => u.At).ToList();
        var ring = new HashSet<Coord>();
        for (var y = 0; y < State.Map.Height; y++)
        {
            for (var x = 0; x < State.Map.Width; x++)
            {
                var at = new Coord(x, y);
                if (members.Any(m => m.DistanceTo(at) <= Content.WakeRadius))
                {
                    ring.Add(at);
                }
            }
        }

        return new EnemyReach(enemy.Id, enemy.At, Ironwake.Core.Threat.StruckByUnit(woken, Content, woken.Find(enemy.Id)!), true, ring);
    }

    /// <summary>
    /// The Recall browser (issue 353): the console's <c>recall list</c> rows, each state row
    /// carrying the history index a click on it recalls.
    /// </summary>
    public IReadOnlyList<RecallRow> RecallRows => PlaySession.RecallRows(State, Content, _made);

    /// <summary>
    /// Whether the Recall browser is open. A rewind closes it (issue 629): the board it returns
    /// to is what the player wants to see next. A refused Recall leaves it open, its refusal in
    /// <see cref="Status"/>.
    /// </summary>
    public bool RecallOpen { get; set; }

    /// <summary>
    /// Rewinds to a history state, as a click on its row in the Recall browser does. The status
    /// bar then reads what the rewind gave back and that the rolls do not change, the lines the
    /// console prints under it; the event log gets only the Recall's own event, as the
    /// console's does. False, with the refusal in <see cref="Status"/>, when it is refused.
    /// </summary>
    public bool Recall(int index)
    {
        var undone = index >= 0 && index < State.History.Count ? RecallCost.Of(State, index) : null;
        var left = State;
        if (!Submit(new Recall(index)))
        {
            return false;
        }

        RecallOpen = false;

        var frames = new List<BattleState> { left };
        for (var i = left.History.Count - 1; i > index; i--)
        {
            frames.Add(left.History[i]);
        }

        frames.Add(State);
        Scrub = frames;
        ScrubLengths = Rhythm.ScrubLengths(frames);
        ScrubSerial++;

        Status = undone is null ? null : "Undone: " + PlaySession.UndoText(undone, UnitNames.Of(State, Content)) + "\n" + PlaySession.SameRolls;
        return true;
    }

    /// <summary>
    /// The attack menu (issue 611), open after a click on an enemy the selected unit has more
    /// than one legal way to strike; null otherwise. Cleared by the next click, a choice, Esc,
    /// and any command.
    /// </summary>
    public AttackMenu? Menu { get; private set; }

    /// <summary>The menu's rows for <paramref name="unit"/> striking <paramref name="target"/> from where it stands, read from <see cref="Queries.AttackOptions"/>.</summary>
    public AttackMenu MenuFor(BattleUnit unit, BattleUnit target)
    {
        var rows = Queries.AttackOptions(State, Content, unit, target).Select(option =>
        {
            var weapon = option.WeaponId is { } weaponId ? Content.ItemName(weaponId) : "no weapon";
            var label = option.Art is { } art ? $"{art.Name} ({weapon})" : weapon;
            if (option.Forecast is not { } forecast)
            {
                return new MenuRow(option, label, RefusalText(option.Refusal!), null);
            }

            var armed = Resolver.ChooseWeapon(unit, Content, option.Command.Slot).Unit;
            return new MenuRow(option, label, RowLine(forecast), CardOf(armed, target, forecast, unit.At, label));
        }).ToList();
        var first = rows.FindIndex(r => r.Legal);
        return new AttackMenu(unit.Id, target.Id, rows, Math.Max(0, first));
    }

    /// <summary>A legal row's numbers: Acc first, then damage times strikes and crit, the counter's, and the most uses the attack spends, an art's cost included.</summary>
    private static string RowLine(CombatForecast forecast)
    {
        var own = forecast.Attacker;
        var counter = forecast.Defender.Strikes
            ? $"counter acc {forecast.Defender.DisplayedHit} {forecast.Defender.Damage} x{forecast.Defender.StrikeCount}"
            : "no counter";
        return $"acc {own.DisplayedHit}  {own.Damage} x{own.StrikeCount}  {(own.CritGrounds ? "grounds" : "crit")} {own.CritChance}  {counter}  uses {forecast.AttackerSpendsAtMost}";
    }

    /// <summary>A greyed row's reason: the resolver's words after the unit's name, or "out of reach from here" for a range refusal, whose text carries coordinates.</summary>
    private string RefusalText(Rejection refusal)
    {
        if (refusal.Reason == RejectionReason.OutOfRange)
        {
            return "out of reach from here";
        }

        var colon = refusal.Message.IndexOf(": ", StringComparison.Ordinal);
        return UnitNames.Of(State, Content).Named(colon >= 0 ? refusal.Message[(colon + 2)..] : refusal.Message);
    }

    /// <summary>Shows the card of the menu row at <paramref name="row"/>, as hovering it or pressing its number does. Ignored with no menu or a row out of range.</summary>
    public void MenuHover(int row)
    {
        if (Menu is { } menu && row >= 0 && row < menu.Rows.Count)
        {
            Menu = menu with { Hovered = row };
        }
    }

    /// <summary>
    /// Strikes with the menu row at <paramref name="row"/>: its attack goes through
    /// <see cref="Submit"/>. A greyed row leaves the menu open and puts its refusal in
    /// <see cref="Status"/>. Returns the command applied, or null.
    /// </summary>
    public Command? Choose(int row)
    {
        if (Menu is not { } menu || row < 0 || row >= menu.Rows.Count)
        {
            return null;
        }

        var chosen = menu.Rows[row];
        if (!chosen.Legal)
        {
            Status = UnitNames.Of(State, Content).Message(chosen.Option.Refusal!.Message);
            return null;
        }

        Menu = null;
        return Submit(chosen.Option.Command) ? chosen.Option.Command : null;
    }

    /// <summary>Closes the attack menu without striking, or an armed item pick without using it, as Esc does; the unit stays selected.</summary>
    public void CloseMenu() => (Menu, Pick) = (null, null);

    /// <summary>
    /// Whether the selected unit has moved and not acted, so Esc or a right-click on the board
    /// asks to take its move back (issue 676) rather than clearing the selection.
    /// </summary>
    public bool CanTakeBack => !EnemyPhasePlaying && Selected is { } id && State.Find(id) is { Moved: true, Acted: false, Side: Side.Player };

    /// <summary>
    /// Takes back the selected unit's move (issue 676), as Esc or a right-click does: it stands
    /// on its start tile again, unmoved and still selected. False, with the core's refusal in
    /// <see cref="Status"/>, when the move is final.
    /// </summary>
    public bool TakeBack() => Selected is { } id && Submit(new Undo(id));

    /// <summary>
    /// What a click on a tile does with a unit selected: a unit owed a Fall back move takes it to
    /// the tile, or declines it on its own tile (issue 786); otherwise the unit itself waits, a hostile unit
    /// is attacked, through the attack menu when it has more than one legal row (issue 611) and at once otherwise, a tile it can end on is moved to (a Canto when one
    /// is owed); anything else selects what is there. Returns the command applied, or null.
    /// </summary>
    public Command? Click(Coord at)
    {
        if (EnemyPhasePlaying)
        {
            Status = "The enemy phase is playing; step or continue";
            return null;
        }

        Menu = null;
        if (Pick is { } pick)
        {
            // An armed pick (issue 1308) takes the click: a marked tile submits the use, or arms
            // the next step of a carry; anything else closes the pick and leaves the unit selected.
            Pick = null;
            if (pick.At(at) is { } use)
            {
                return Submit(use) ? use : null;
            }

            if (pick is StepPick step && step.After(at) is { } next)
            {
                Pick = next;
                Status = $"{next.Label}: {next.Prompt}";
                return null;
            }

            Status = $"{pick.Label}: no target there; the pick is closed";
            return null;
        }

        if (Selected is not { } id || State.Find(id) is not { } unit)
        {
            Select(at);
            return null;
        }

        Command? command = null;
        if (unit.FallingBack && State.FallBackReachOf(unit, Content) is { } fallBack)
        {
            // The move a Fall back order owes (issue 786): its own tile declines it, as
            // `fallback <unit> stay` does; a tile in the two-step reach takes it.
            command = at == unit.At || fallBack.CanEnd(at) ? new FallBack(unit.Id, at) : null;
        }
        else if (at == unit.At && !unit.Acted)
        {
            command = new Wait(unit.Id);
        }
        else if (UnitAt(at) is { Side: Side.Enemy } target)
        {
            var menu = MenuFor(unit, target);
            var legal = menu.Rows.Where(r => r.Legal).ToList();
            if (legal.Count > 1)
            {
                Menu = menu;
                Status = null;
                return null;
            }

            command = legal.Count == 1 ? legal[0].Option.Command : new Attack(unit.Id, target.Id);
        }
        else if (Reach is { } reach && reach.CanEnd(at))
        {
            command = unit.Acted ? new Canto(unit.Id, at) : new Move(unit.Id, at);
        }

        if (command is null)
        {
            Select(at);
            return null;
        }

        return Submit(command) ? command : null;
    }

    /// <summary>
    /// Applies a player command through the resolver and logs its events. <c>end</c> also
    /// queues the enemy phase for <see cref="Step"/>. False, with the core's refusal in
    /// <see cref="Status"/>, when it is refused or the enemy phase is still playing.
    /// </summary>
    public bool Submit(Command command)
    {
        if (EnemyPhasePlaying)
        {
            Status = "The enemy phase is playing; step or continue";
            return false;
        }

        var result = Resolver.Apply(State, Content, command);
        if (!result.Accepted)
        {
            Status = UnitNames.Of(State, Content).Message(result.Rejection!.Message);
            return false;
        }

        Status = null;
        Menu = null;
        Pick = null;
        if (command is EndPhase or Ironwake.Core.Recall || Playing is not null)
        {
            _fallen.Clear();
            _ghosts.Clear();
        }

        Playing = null;
        Act = null;
        Record(command);
        var before = State;
        State = result.Next;
        var names = UnitNames.Of(State, Content);
        _log.AddRange(result.Events.Select(e => PlaySession.Describe(e, Content, names)));
        Beats = Hold(result.Events.Select(e => Staged(e, before, State, result.Events)).OfType<Beat>(), enemyPhase: false).ToList();
        BeatSerial++;
        _fallen.AddRange(Beats.Select(b => b.Fell).OfType<FallenMark>());
        _log.AddRange(Ironwake.Core.Objective.Notices(before, State, Content, command));
        if (command is EndPhase)
        {
            Selected = null;
            foreach (var enemy in EnemyAi.Plan(State, Content))
            {
                _enemy.Enqueue(enemy);
            }
        }
        else if (Selected is { } id && State.Find(id) is { } unit && unit.Acted && State.CantoReachOf(unit, Content) is null && !unit.FallingBack)
        {
            Selected = null;
        }

        return true;
    }

    /// <summary>
    /// Reveals the next enemy-phase event line, applying the next planned command when the
    /// last one's lines are all shown. An enemy's Move or Wait no player unit sees prints the
    /// console's dark line and only the events it set off beyond the move, as the console
    /// does (DESIGN.md 13.7). False when nothing is waiting.
    /// </summary>
    public bool Step()
    {
        while (_pending.Count == 0 && _enemy.Count > 0)
        {
            var command = _enemy.Dequeue();
            var dark = command is Move or Wait && Dusk.InTheDark(State, Content, command);
            var result = Resolver.Apply(State, Content, command);
            if (!result.Accepted)
            {
                throw new InvalidOperationException($"the enemy AI's {command} was rejected: {result.Rejection!.Message}");
            }

            Record(command);
            var before = State;
            State = result.Next;
            var first = true;
            if (dark)
            {
                _pending.Enqueue((new Highlight(ProtocolSession.DarkLine, null, null, Array.Empty<Coord>(), null), null, null, first));
                first = false;
            }

            foreach (var e in result.Events.Where(e => !dark || e is not UnitMoved and not UnitWaited))
            {
                if (e is UnitDied died && before.Find(died.UnitId) is { } dead)
                {
                    _ghosts[dead.Id] = dead;
                }

                _pending.Enqueue((HighlightOf(e, PlaySession.Describe(e, Content, UnitNames.Of(State, Content)), before, State), Staged(e, before, State, result.Events), ActCards.Of(e, before, State, Content), first));
                first = false;
            }

            if (!dark)
            {
                foreach (var notice in Ironwake.Core.Objective.Notices(before, State, Content, command))
                {
                    _pending.Enqueue((new Highlight(notice, null, null, Array.Empty<Coord>(), null), null, null, first));
                    first = false;
                }
            }
        }

        if (_pending.Count == 0)
        {
            return false;
        }

        var (mark, beat, act, opens) = _pending.Dequeue();
        if (opens)
        {
            _actLogStart = _log.Count;
        }

        Playing = mark;
        _log.Add(Playing.Line);
        Beats = beat is null ? Array.Empty<Beat>() : Hold(new[] { beat }, enemyPhase: true).ToList();
        BeatSerial++;
        if (beat?.Fell is { } fell)
        {
            _ghosts.Remove(fell.Unit.Id);
            _fallen.Add(fell);
        }

        Act = act is { Attacker: null } && Act is { Attacker: not null } ? Act : act ?? Act;
        return true;
    }

    /// <summary>
    /// Which combats play as a battle scene (issue 535): the key moments unless the player has
    /// stepped the setting on. Read when a command's beats are made, so a change applies from the
    /// next command.
    /// </summary>
    public SceneSetting SceneSetting { get; set; } = SceneSetting.KeyMoments;

    /// <summary>
    /// The beat for <paramref name="e"/>, with its scene when <see cref="SceneSetting"/> plays the
    /// combat as one and its level-up card when <paramref name="events"/>, its command's events,
    /// level a unit. A command with no combat whose events level a unit (a heal's EXP, slice 2)
    /// gets the card alone, on its first heal.
    /// </summary>
    private Beat? Staged(GameEvent e, BattleState before, BattleState after, IReadOnlyList<GameEvent> events)
    {
        if (e is UnitHealed && ReferenceEquals(e, events.OfType<UnitHealed>().First()) && !events.OfType<CombatFought>().Any()
            && LevelUpCard.Of(events, after, Content) is { } card)
        {
            return Beat.CardOnly(card);
        }

        var beat = Beat.Of(e, before, after);
        if (beat is null || e is not CombatFought combat)
        {
            return beat;
        }

        return beat with
        {
            Scene = Scenes.Plays(SceneSetting, combat, before, events) ? BattleScene.Of(combat, before, after, Content) : null,
            LevelUp = LevelUpCard.Of(events, after, Content),
        };
    }

    /// <summary>The last lethal number shown, kept so the death beat that follows it can hold it (issue 514).</summary>
    private Pop? _lethal;

    /// <summary>
    /// Carries each kill's number into its death beat (issue 514): a death beat holds the lethal
    /// number struck on that unit, and in the enemy phase a player unit's death pulses the RECALL
    /// chip while a charge is left.
    /// </summary>
    private IEnumerable<Beat> Hold(IEnumerable<Beat> beats, bool enemyPhase)
    {
        foreach (var beat in beats)
        {
            if (beat.Pops.LastOrDefault(p => p.Lethal) is { } lethal)
            {
                _lethal = lethal;
            }

            if (beat.Fell is { } fell)
            {
                var held = _lethal is { } pop && pop.TargetId == fell.Unit.Id ? pop : null;
                _lethal = null;
                yield return beat with { Held = held, Pulse = enemyPhase && fell.Unit.Side == Side.Player && State.RecallCharges > 0 };
                continue;
            }

            yield return beat;
        }
    }

    /// <summary>
    /// The tiles an event names, read from the state before its command (where an attacker
    /// and its target stood) and after it (where a unit that waited, healed or gained stands).
    /// </summary>
    private static Highlight HighlightOf(GameEvent e, string line, BattleState before, BattleState after)
    {
        Coord? At(string id) => (after.Find(id) ?? before.Find(id))?.At;
        var none = Array.Empty<Coord>();
        return e switch
        {
            UnitMoved m => new Highlight(line, m.From, m.To, m.Path, null),
            Cantoed m => new Highlight(line, m.From, m.To, m.Path, null),
            UnitRetreated r => new Highlight(line, r.From, r.To, none, null),
            CombatFought c => new Highlight(line, null, before.Find(c.AttackerId)?.At, none, before.Find(c.TargetId)?.At),
            UnitDied d => new Highlight(line, null, null, none, d.At),
            UnitSpawned s => new Highlight(line, null, s.At, none, null),
            UnitWaited w => new Highlight(line, null, At(w.UnitId), none, null),
            UnitHealed h => new Highlight(line, null, At(h.UnitId), none, null),
            UnitBurned b => new Highlight(line, null, At(b.UnitId), none, null),
            BurnCashed c => new Highlight(line, null, At(c.ByUnitId), none, At(c.UnitId)),
            UnitRested r => new Highlight(line, null, At(r.UnitId), none, null),
            BlowRaised b => new Highlight(line, null, At(b.UnitId), none, b.At),
            BlowLanded b => new Highlight(line, null, At(b.UnitId), none, b.At),
            BlowFell b => new Highlight(line, null, At(b.UnitId), none, b.At),
            BlowBroken b => new Highlight(line, null, At(b.UnitId), none, b.At),
            ExpGained x => new Highlight(line, null, At(x.UnitId), none, null),
            LeveledUp l => new Highlight(line, null, At(l.UnitId), none, null),
            ItemUsed i => new Highlight(line, null, At(i.UnitId), none, At(i.TargetId)),
            _ => new Highlight(line, null, null, none, null),
        };
    }

    /// <summary>
    /// Keeps <see cref="_made"/> in step with the history for an accepted command, before the
    /// state moves, by the console's rule: a Recall truncates it, an Undo drops the move it takes
    /// back (issue 676), anything else names the command applied from the state it leaves. It
    /// also notes the log's length at the state left, and for a Recall or an Undo the lines it undoes.
    /// </summary>
    private void Record(Command command)
    {
        if (command is Recall recall)
        {
            if (_logAt.TryGetValue(recall.ToIndex, out var from) && from < _log.Count)
            {
                _undone.Add((from, _log.Count));
            }

            _made.RemoveRange(recall.ToIndex, _made.Count - recall.ToIndex);
        }
        else if (command is Undo)
        {
            var kept = State.History.Count - 1;
            if (_logAt.TryGetValue(kept, out var from) && from < _log.Count)
            {
                _undone.Add((from, _log.Count));
            }

            if (_made.Count > kept)
            {
                _made.RemoveRange(kept, _made.Count - kept);
            }
        }
        else
        {
            _logAt[State.History.Count] = _log.Count;
            if (_made.Count == State.History.Count)
            {
                _made.Add(PlaySession.CommandText(command));
            }
        }
    }

    /// <summary>Reveals every event left in the enemy phase.</summary>
    public void Continue()
    {
        while (Step())
        {
        }
    }
}
