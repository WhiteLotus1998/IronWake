using System.Text;

namespace Ironwake.Core;

/// <summary>
/// The console view of a map: the grid with units drawn over the terrain, then a legend
/// naming every unit letter and every terrain glyph on the map, a healing glyph with its
/// percent. Player units are uppercase letters in placement order, enemies lowercase,
/// bosses <c>!</c>. The view
/// is for reading, not for parsing; the map file itself is written by the Content
/// project's map writer. Output is plain ASCII. Given a <see cref="Reach"/>, the tiles
/// the unit may end on are drawn as <see cref="ReachGlyph"/> and a line under the
/// legend says whose reach it is. While any Guard group is asleep, both views print
/// <see cref="WakeLegend"/> under the unit rows. On an Escape map both views draw every
/// exit tile no unit stands on as <see cref="ExitGlyph"/> and print <see cref="ExitLegend"/>
/// under the unit rows (issue 267), so the objective is on the screen and not only in the
/// map file's header; a reach glyph covers an exit the unit can end on.
/// </summary>
public static class MapRenderer
{
    public const char BossGlyph = '!';

    public const char ReachGlyph = '*';

    public const char ExitGlyph = '>';

    /// <summary>A closed chest no unit stands on (issue 649).</summary>
    public const char ChestGlyph = '$';

    /// <summary>The rule a map with chests prints after its chest list (issue 649).</summary>
    public const string ChestRule = "a unit on a chest or beside it opens it as its action, unless an enemy stands on it; what fits goes to its pack, the rest to the wagon, kept only if the map is won";

    /// <summary>The legend a <c>shove: on</c> map prints under its exits (DESIGN.md 13.12, experiment).</summary>
    public const string PincerLegend = "pincer: a unit struck from beside it while a foe of the striker stands directly behind it is struck at +15 Acc, counters too";

    /// <summary>The legend a <c>brace: on</c> map prints (DESIGN.md 13.14, experiment).</summary>
    public const string BraceLegend = "brace: a unit that waits on the tile it began its turn on is struck at -15 Acc until its side's next phase";

    /// <summary>The legend a <c>break: on</c> map prints (DESIGN.md 13.22, experiment).</summary>
    /// <summary>The messenger's rule (DESIGN.md 13.24, experiment), printed under any board whose map has one.</summary>
    public const string MessengerRule = "messenger: never strikes; once awake it runs for the road each phase and, if it reaches it, leaves and the word is out; kill it or stand on its path";

    public const string BreakLegend = "break: when a boss falls, each of his group at or below half hp flees the board (not a kill, no EXP)";

    /// <summary>The legend a <c>wildfire: on</c> map prints (DESIGN.md 13.15, experiment).</summary>
    public const string WildfireLegend = "wildfire: a Cinder hit on a unit in forest sets the tile alight (%); a unit on fire at its phase start loses 20 percent (never below 1); each player phase fire burns out to plain and lights the forest beside it";

    /// <summary>
    /// Under the wildfire legend while anything burns (DESIGN.md 13.15): the tiles on fire and the
    /// forest the next player phase start sets alight, the front's mark, row-major, as
    /// <c>fire: burning 6,5 5,6; next front 6,4 7,5</c>. Null when nothing burns.
    /// </summary>
    public static string? FireLine(MapDefinition map)
    {
        var burning = Wildfire.Burning(map);
        if (burning.Count == 0)
        {
            return null;
        }

        var front = Wildfire.NextFront(map);
        return $"fire: burning {string.Join(" ", burning)}; next front {(front.Count == 0 ? "none" : string.Join(" ", front))}";
    }

    /// <summary>The legend a <c>signatures: on</c> map prints (DESIGN.md 13.18, experiment).</summary>
    public const string SignaturesLegend = "signatures: each recruit with a signature plays it, flaw included; show <unit> prints it";

    /// <summary>
    /// Under the signatures legend on a board (DESIGN.md 13.18): one line per living player unit
    /// that plays a signature, in board order, in the words <c>show</c> uses, so Wren's radius is printed.
    /// </summary>
    public static IEnumerable<string> SignatureLines(BattleState state, GameContent content) =>
        state.UnitsOf(Side.Player)
            .Select(u => (Unit: u, Kind: Signatures.Of(state, content, u)))
            .Where(x => x.Kind is not null)
            .Select(x => $"  {x.Unit.Id}: {Signatures.Describe(x.Kind!.Value)}");

    /// <summary>The legend an <c>overwatch: hold</c> map prints (DESIGN.md 13.17b, experiment).</summary>
    public const string OverwatchHoldLegend = "overwatch (hold): a unit that has not moved this turn may watch as its action; the first foe to end a move where its weapon reaches is struck once before it acts, no counter; a strike on the watcher ends the watch";

    /// <summary>The legend an <c>overwatch: on</c> map prints (DESIGN.md 13.17, experiment).</summary>
    public const string OverwatchLegend = "overwatch: a unit whose weapon reaches 2 may watch; the first foe to end a move two tiles from it is struck once before it acts, no counter; a strike on the watcher ends the watch";

    /// <summary>The legend a <c>cover: on</c> map prints (DESIGN.md 13.19, experiment).</summary>
    public const string CoverLegend = "cover: a unit beside an ally may cover it; the first strike aimed at the ally while they stand side by side swaps them and strikes the coverer on the ally's tile";

    /// <summary>
    /// Under the overwatch legend while any unit watches (DESIGN.md 13.17): each watcher in id
    /// order with the ring's tiles inside the map, row-major, as
    /// <c>watches: archer-1 at 5,5 over 5,3 4,4 ...</c>. Null when nobody watches.
    /// </summary>
    public static string? WatchLine(BattleState state, GameContent content)
    {
        var watchers = state.Units.Where(u => u.Watching).OrderBy(u => u.Id, StringComparer.Ordinal).ToList();
        if (!state.Map.OverwatchEnabled || watchers.Count == 0)
        {
            return null;
        }

        return "watches: " + string.Join("; ", watchers.Select(u => $"{u.Id} at {u.At} over {string.Join(" ", Overwatch.RingOf(state, content, u))}"));
    }

    /// <summary>The legend a <c>windup: on</c> map prints (DESIGN.md 13.16, experiment).</summary>
    public const string WindupLegend = "windup: a maul's attack raises a blow over the target's tile; at its side's next phase start it lands on whoever stands there, a sure hit; a hit from within the wielder's reach breaks it";

    /// <summary>
    /// Under the windup legend while any blow is raised (DESIGN.md 13.16): each wielder and its
    /// tile in unit order, and who stands there now, as
    /// <c>blows: toll_mauler-1 over 6,3 (teodor 14, sure)</c>, with the certain damage it lands on
    /// that unit. Null when no blow is raised.
    /// </summary>
    public static string? BlowLine(BattleState state, GameContent content)
    {
        var raised = state.Units.Where(u => u.WindupAt is not null).ToList();
        if (!state.Map.WindupEnabled || raised.Count == 0)
        {
            return null;
        }

        return "blows: " + string.Join(", ", raised.Select(u =>
        {
            var at = u.WindupAt!.Value;
            var under = state.Units.FirstOrDefault(o => o.At == at && o.Id != u.Id);
            return under is null ? $"{u.Id} over {at} (empty)" : $"{u.Id} over {at} ({under.Id} {Windup.Damage(state, content, u, under)}, sure)";
        }));
    }

    private static string? WindupMark(BattleState state, GameContent content, BattleUnit unit)
    {
        if (!state.Map.WindupEnabled)
        {
            return null;
        }

        if (unit.WindupAt is { } at)
        {
            return "winding up over " + at;
        }

        return Windup.Over(state, unit.At) is { } wielder && wielder.Id != unit.Id
            ? $"under a blow from {wielder.Id} ({Windup.Damage(state, content, wielder, unit)}, sure)"
            : null;
    }

    private static bool Burning(BattleState state, BattleUnit unit) =>
        state.Map.TerrainIdAt(unit.At) == Wildfire.FireTerrainId;

    public const string ShoveLegend = "shove: a player unit may push an ally beside it one tile straight away, as its action, if the tile beyond is open";

    public static string Render(MapDefinition map, GameContent content, Reach? reach = null)
    {
        var sb = new StringBuilder();
        sb.Append(map.Name).Append("  ").Append(map.Width).Append('x').Append(map.Height)
            .Append("  ").Append(WinName(map.Win))
            .Append("  turn limit ").Append(map.TurnLimit)
            .Append("  recall ").Append(map.RecallCharges)
            .Append("  enemy level ").Append(map.EnemyLevel);
        if (map.CheapShotsAllowed)
        {
            sb.Append("  cheap shots allowed");
        }

        sb.Append('\n');

        var letters = Letters(map, content);
        sb.Append("   ");
        for (var x = 0; x < map.Width; x++)
        {
            sb.Append((char)('0' + x % 10));
        }

        sb.Append('\n');
        for (var y = 0; y < map.Height; y++)
        {
            sb.Append(y.ToString().PadLeft(2)).Append(' ');
            var row = map.GlyphRow(y, content).ToCharArray();
            DrawExits(map, y, row);
            DrawChests(map.Chests, y, row);
            if (reach is not null)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    if (reach.CanEnd(new Coord(x, y)))
                    {
                        row[x] = ReachGlyph;
                    }
                }
            }

            for (var i = 0; i < map.Placements.Count; i++)
            {
                if (map.Placements[i].At.Y == y)
                {
                    row[map.Placements[i].At.X] = letters[i];
                }
            }

            sb.Append(row).Append('\n');
        }

        sb.Append('\n');
        for (var i = 0; i < map.Placements.Count; i++)
        {
            sb.Append(letters[i]).Append("  ").Append(Describe(map.Placements[i], map, content)).Append('\n');
        }

        if (map.Placements.OfType<EnemyPlacement>().Any(e => e.Behavior == Behavior.Guard))
        {
            sb.Append(WakeLegend(content)).Append('\n');
        }

        if (LinkLegend(map, _ => true) is { } called)
        {
            sb.Append(called).Append('\n');
        }

        if (ExitLegend(map) is { } exits)
        {
            sb.Append(exits).Append('\n');
        }

        if (ChestLegend(map.Chests, content) is { } chests)
        {
            sb.Append(chests).Append('\n');
        }

        if (map.ShoveEnabled)
        {
            sb.Append(ShoveLegend).Append('\n');
        }

        if (map.PincerEnabled)
        {
            sb.Append(PincerLegend).Append('\n');
        }

        if (map.BraceEnabled)
        {
            sb.Append(BraceLegend).Append('\n');
        }

        if (map.BreakEnabled)
        {
            sb.Append(BreakLegend).Append('\n');
        }

        if (map.Fronts.Count > 0)
        {
            sb.Append(Fronts.Rule).Append('\n');
            sb.Append("fronts: " + string.Join("; ", map.Fronts.Select(f => $"{f.Words} {string.Join(' ', f.Tiles)}"))).Append('\n');
        }

        if (PairRule.Rule(map) is { } pairRule)
        {
            sb.Append(pairRule).Append('\n');
        }

        if (map.Messenger is { } route)
        {
            sb.Append(MessengerRule).Append('\n');
            sb.Append($"messenger at {route.From}, road at {route.Road}").Append('\n');
        }

        if (map.WildfireEnabled)
        {
            sb.Append(WildfireLegend).Append('\n');
            if (FireLine(map) is { } fire)
            {
                sb.Append(fire).Append('\n');
            }
        }

        if (map.WindupEnabled)
        {
            sb.Append(WindupLegend).Append('\n');
        }

        if (map.OverwatchEnabled)
        {
            sb.Append(map.OverwatchHold ? OverwatchHoldLegend : OverwatchLegend).Append('\n');
        }

        if (map.CoverEnabled)
        {
            sb.Append(CoverLegend).Append('\n');
        }

        if (map.SignaturesEnabled)
        {
            sb.Append(SignaturesLegend).Append('\n');
        }

        sb.Append('\n').Append("terrain:");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in map.TerrainIds)
        {
            if (seen.Add(id))
            {
                var terrain = content.TerrainById(id);
                sb.Append("  ").Append(terrain.Glyph).Append(' ').Append(terrain.Label());
            }
        }

        sb.Append('\n');
        if (reach is not null)
        {
            var count = reach.Destinations.Count() - 1;
            sb.Append(ReachGlyph).Append("  reach from ").Append(reach.Origin)
                .Append(", ").Append(reach.Movement.ToString().ToLowerInvariant())
                .Append(" mov ").Append(reach.Mov)
                .Append(": ").Append(count).Append(count == 1 ? " tile" : " tiles").Append('\n');
        }

        return sb.ToString();
    }

    /// <summary>
    /// The console view of a battle: the grid with every living unit drawn where it stands,
    /// each with the letter of the placement it filled, then a legend with each unit's
    /// name, class, position, HP, terrain (with the HP a healing tile gives that unit, issue
    /// 207), and for enemies its group and how it behaves now (a sleeping Guard reads <c>guard, asleep</c>, a refugee below half HP adds <c>holds its refuge until half hp</c>, issue 215), and <c>unarmed</c> for a unit with
    /// no usable weapon, since the enemy planner prices such a unit as free damage (issue
    /// 101) and seeing it coming is the player's whole defence. The turn line names the phase and
    /// the Recall charges left; past the turn limit, where only a battle the clock decided stands, it
    /// says the battle is over after the last turn (issue 252) rather than naming a turn the map never had. Given a <see cref="Reach"/>, the tiles that unit may end on
    /// are marked as in the map view. On a dusk map (DESIGN.md 13.7) an enemy no player unit
    /// sees is drawn as <see cref="Dusk.Unseen"/> with no row of its own, only its tile on the
    /// unseen line, and <see cref="Dusk.Line"/> gives the sight now and next turn.
    /// </summary>
    public static string Render(BattleState state, GameContent content, Reach? reach = null)
    {
        var map = state.Map;
        var sb = new StringBuilder();
        sb.Append(map.Name);
        if (state.Turn > map.TurnLimit)
        {
            sb.Append("  over after turn ").Append(map.TurnLimit).Append(" of ").Append(map.TurnLimit);
        }
        else
        {
            sb.Append("  turn ").Append(state.Turn).Append(" of ").Append(map.TurnLimit)
                .Append("  ").Append(state.Phase.ToString().ToLowerInvariant()).Append(" phase");
        }

        sb.Append("  ").Append(WinName(map.Win))
            .Append("  recall ").Append(state.RecallCharges).Append('\n');
        var letters = Letters(map, content);
        sb.Append("   ");
        for (var x = 0; x < map.Width; x++)
        {
            sb.Append((char)('0' + x % 10));
        }

        sb.Append('\n');
        for (var y = 0; y < map.Height; y++)
        {
            sb.Append(y.ToString().PadLeft(2)).Append(' ');
            var row = map.GlyphRow(y, content).ToCharArray();
            DrawExits(map, y, row);
            DrawChests(state.ClosedChests, y, row);
            if (reach is not null)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    if (reach.CanEnd(new Coord(x, y)))
                    {
                        row[x] = ReachGlyph;
                    }
                }
            }

            foreach (var unit in state.Units)
            {
                if (unit.At.Y == y)
                {
                    row[unit.At.X] = Dusk.Seen(state, unit) ? letters[unit.PlacementIndex] : Dusk.Unseen;
                }
            }

            sb.Append(row).Append('\n');
        }

        sb.Append('\n');
        foreach (var unit in state.Units)
        {
            if (!Dusk.Seen(state, unit))
            {
                continue;
            }

            var terrain = map.TerrainAt(unit.At, content).Label(unit.MaxHp(content));
            var who = $"{unit.Unit.Name} L{unit.Unit.Level} {content.Class(unit.Unit.ClassId).Name.ToLowerInvariant()}";
            var hp = $"hp {unit.Hp}/{unit.MaxHp(content)}";
            sb.Append(letters[unit.PlacementIndex]).Append("  ").Append($"{unit.Id,-16} {who,-26} {unit.At,-6} {hp,-9} {terrain}");
            if (unit.Side == Side.Enemy)
            {
                var role = unit.IsBoss ? "boss" : unit.Behavior.ToString()!.ToLowerInvariant();
                if (unit.Behavior == Behavior.Guard)
                {
                    role += state.IsAwake(unit.Group!) ? ", awake" : ", asleep";
                }

                if (RetreatRule.Holds(unit, content))
                {
                    role += ", holds its refuge until half hp";
                }

                if (unit.Grudge is { } sworn)
                {
                    role += ", sworn: " + sworn;
                }

                if (unit.Braced)
                {
                    role += ", braced";
                }

                if (unit.Watching)
                {
                    role += ", watching";
                }

                if (Burning(state, unit))
                {
                    role += ", burning";
                }

                if (WindupMark(state, content, unit) is { } windup)
                {
                    role += ", " + windup;
                }

                sb.Append("  group ").Append(unit.Group).Append(", ").Append(role);
            }
            else if (unit.IsCaptain)
            {
                sb.Append("  captain");
            }

            if (unit.Side == Side.Player && unit.Braced)
            {
                sb.Append("  braced");
            }

            if (unit.Side == Side.Player && unit.Watching)
            {
                sb.Append("  watching");
            }

            if (unit.CoveredBy is { } coverer)
            {
                sb.Append("  covered by ").Append(coverer);
            }

            if (unit.Side == Side.Player && Burning(state, unit))
            {
                sb.Append("  burning");
            }

            if (unit.Side == Side.Player && WindupMark(state, content, unit) is { } mark)
            {
                sb.Append("  ").Append(mark);
            }

            if (unit.EquippedWeapon(content) is null)
            {
                sb.Append("  unarmed");
            }

            if (Messenger.Is(state, unit))
            {
                sb.Append("  messenger");
            }

            var carried = unit.Side == Side.Enemy
                ? unit.Unit.Inventory.Items.Where(stack => stack.Keepsake is not null).Select(stack => Keepsake.Name(stack.ItemId, stack.Keepsake!, content)).ToList()
                : new List<string>();
            if (carried.Count > 0)
            {
                sb.Append("  carries ").Append(string.Join(", ", carried));
            }

            if (unit.Acted)
            {
                sb.Append(unit.Canto is { } canto ? $"  canto {canto}" : "  done");
            }

            sb.Append('\n');
        }

        var unseen = state.Units.Where(u => !Dusk.Seen(state, u)).Select(u => u.At).OrderBy(c => c.Y).ThenBy(c => c.X).Select(c => c.ToString()).ToList();
        if (unseen.Count > 0)
        {
            sb.Append(Dusk.Unseen).Append("  unseen at ").Append(string.Join(' ', unseen)).Append('\n');
        }

        if (Dusk.Line(state, content, UnitNames.Of(state, content)) is { } dusk)
        {
            sb.Append(dusk).Append('\n');
        }

        if (state.Units.Any(u => u is { Behavior: Behavior.Guard, Group: { } group } && !state.IsAwake(group) && Dusk.Seen(state, u)))
        {
            sb.Append(WakeLegend(content)).Append('\n');
        }

        if (LinkLegend(map, group => !state.IsAwake(group)) is { } called)
        {
            sb.Append(called).Append('\n');
        }

        if (ExitLegend(map) is { } exits)
        {
            sb.Append(exits).Append('\n');
        }

        if (ChestLegend(state.ClosedChests, content) is { } chests)
        {
            sb.Append(chests).Append('\n');
        }

        if (map.ShoveEnabled)
        {
            sb.Append(ShoveLegend).Append('\n');
        }

        if (map.PincerEnabled)
        {
            sb.Append(PincerLegend).Append('\n');
        }

        if (map.BraceEnabled)
        {
            sb.Append(BraceLegend).Append('\n');
        }

        if (map.BreakEnabled)
        {
            sb.Append(BreakLegend).Append('\n');
        }

        if (FrontsLine(state) is { } frontsLine)
        {
            sb.Append(Fronts.Rule).Append('\n');
            sb.Append(frontsLine).Append('\n');
        }

        if (Hunt.Line(state, UnitNames.Of(state, content)) is { } huntLine)
        {
            sb.Append(Hunt.Rule).Append('\n');
            sb.Append(huntLine).Append('\n');
        }

        if (PairRule.Line(state, UnitNames.Of(state, content)) is { } pairLine)
        {
            sb.Append(PairRule.Rule(map)).Append('\n');
            sb.Append(pairLine).Append('\n');
        }

        if (Freed.Line(state, UnitNames.Of(state, content)) is { } bondLine)
        {
            sb.Append(bondLine).Append('\n');
        }

        if (MessengerLine(state, content) is { } messengerLine)
        {
            sb.Append(MessengerRule).Append('\n');
            sb.Append(messengerLine).Append('\n');
        }

        if (map.WildfireEnabled)
        {
            sb.Append(WildfireLegend).Append('\n');
            if (FireLine(map) is { } fire)
            {
                sb.Append(fire).Append('\n');
            }
        }

        if (map.OverwatchEnabled)
        {
            sb.Append(map.OverwatchHold ? OverwatchHoldLegend : OverwatchLegend).Append('\n');
            if (WatchLine(state, content) is { } watches)
            {
                sb.Append(watches).Append('\n');
            }
        }

        if (map.CoverEnabled)
        {
            sb.Append(CoverLegend).Append('\n');
        }

        if (map.WindupEnabled)
        {
            sb.Append(WindupLegend).Append('\n');
            if (BlowLine(state, content) is { } blows)
            {
                sb.Append(blows).Append('\n');
            }
        }

        if (map.SignaturesEnabled)
        {
            sb.Append(SignaturesLegend).Append('\n');
            foreach (var line in SignatureLines(state, content))
            {
                sb.Append(line).Append('\n');
            }
        }

        if (state.Keepsakes.Count > 0)
        {
            sb.Append("keepsakes: ").Append(string.Join(", ", state.Keepsakes.Select(k => $"{Keepsake.Name(k.Item.ItemId, k.FallenId, content)} at {k.At}"))).Append('\n');
        }

        if (state.Escaped.Count > 0)
        {
            sb.Append("escaped: ").Append(string.Join(' ', state.Escaped.Select(u => u.Id))).Append('\n');
        }

        if (reach is not null)
        {
            var count = reach.Destinations.Count() - 1;
            sb.Append(ReachGlyph).Append("  reach from ").Append(reach.Origin)
                .Append(": ").Append(count).Append(count == 1 ? " tile" : " tiles").Append('\n');
        }

        return sb.ToString();
    }

    /// <summary>
    /// The one line both board views print under the unit rows while any Guard group is
    /// asleep (issue 260), so the wake rule of DESIGN.md section 8 is on the screen and not
    /// only in the doc. The numbers are the content's <see cref="GameContent.WakeRadius"/>
    /// and <see cref="GameContent.NoiseRadius"/>, the ones <see cref="WakeCheck"/> reads.
    /// </summary>
    public static string WakeLegend(GameContent content) => $"asleep: {WakeCondition(content)}";

    /// <summary>
    /// The line under the wake legend on a map with a <c>wake_links:</c> header (issue 393):
    /// <c>called: group weir wakes when group ford does</c>, one clause per link whose second
    /// group <paramref name="asleep"/> says is still asleep, in header order; null when none is.
    /// </summary>
    public static string? LinkLegend(MapDefinition map, Func<string, bool> asleep)
    {
        var links = map.WakeLinks.Where(l => asleep(l.To)).Select(l => $"group {l.To} wakes when group {l.From} does").ToList();
        return links.Count == 0 ? null : "called: " + string.Join("; ", links);
    }

    /// <summary>The wake rule alone, as the legend and <c>threat</c>'s sleeping-group rows print it (issue 248).</summary>
    public static string WakeCondition(GameContent content) =>
        $"wakes if a unit ends within {content.WakeRadius} tiles of a member, a combat happens within {content.NoiseRadius}, or a member dies";

    /// <summary>
    /// What an Escape map asks, in the words of DESIGN.md section 7's outcome rule (issue 269),
    /// printed after the exit tiles in <see cref="ExitLegend"/>.
    /// </summary>
    public const string EscapeRule = "a unit that starts its turn on one may exit as its action, without moving; the captain's exit wins and leaves the rest behind";

    /// <summary>
    /// <see cref="EscapeRule"/> before issue 377, printed on a map with
    /// <see cref="MapDefinition.ExitAfterMove"/>, where a unit may move onto an exit and leave.
    /// </summary>
    public const string EscapeRuleAfterMove = "a unit on one may exit as its action; the captain's exit wins and leaves the rest behind";

    /// <summary>
    /// The one line both board views print under the unit rows on an Escape map (issue 267):
    /// the exit glyph in parentheses (never at the line's start, where the console echoes a
    /// command as <c>&gt; </c>), then every exit tile from <see cref="MapDefinition.Exits"/> in the
    /// map's own order, then <see cref="EscapeRule"/> (<see cref="EscapeRuleAfterMove"/> on a map
    /// that keeps the older rule). Null on a map with no exits.
    /// </summary>
    public static string? ExitLegend(MapDefinition map) =>
        map.Exits.Count == 0
            ? null
            : $"exits ({ExitGlyph}): {string.Join(' ', map.Exits)} ({(map.ExitAfterMove ? EscapeRuleAfterMove : EscapeRule)})";

    /// <summary>
    /// The fronts line (issue 692): each front in file order with its tiles and whether it stands or
    /// has fallen, <c>fronts: west breach 2,4 2,5 holding; gate 7,0 fallen</c>. Null on a map
    /// without fronts. The rule is <see cref="Fronts.Rule"/>, printed by <c>help</c>.
    /// </summary>
    public static string? FrontsLine(BattleState state) =>
        state.Map.Fronts.Count == 0
            ? null
            : "fronts: " + string.Join("; ", state.Map.Fronts.Select(f => $"{f.Words} {string.Join(' ', f.Tiles)} {(Fronts.HasFallen(state, f) ? "fallen" : "holding")}{Defence(state, f)}"));

    /// <summary>
    /// On a map with a hunter (issue 692), what the fronts line adds for a standing front: its
    /// defenders' summed HP and count (<see cref="Hunt.Holds"/>), and <c>hunted</c> on the front the
    /// hunter hunts. Empty without a hunter on the board, and for a fallen front.
    /// </summary>
    private static string Defence(BattleState state, Front front)
    {
        if (Hunt.On(state) is null || Hunt.Holds(state).FirstOrDefault(h => h.Front == front) is not { } hold)
        {
            return "";
        }

        return $" ({hold.Defenders.Count} defending, {hold.Hp} hp{(Hunt.Hunted(state) == front ? ", hunted" : "")})";
    }

    /// <summary>
    /// Where the messenger stands and how far the road is (DESIGN.md 13.24): its own phases at
    /// full Mov on open ground, the party left out, so a held path only makes it longer. Null
    /// when the map has no messenger. Once it is gone the line says how (issue 675):
    /// <c>messenger: fallen at 8,2; the word never left</c> or
    /// <c>messenger: gone by the road at 15,1; the word is out</c>.
    /// </summary>
    public static string? MessengerLine(BattleState state, GameContent content)
    {
        if (state.Map.Messenger is not { } route)
        {
            return null;
        }

        if (Messenger.On(state) is not { } unit)
        {
            return state.MessengerGone is { Escaped: true } escaped
                ? $"messenger: gone by the road at {escaped.At}; the word is out"
                : $"messenger: fallen at {state.MessengerGone?.At ?? route.From}; the word never left";
        }

        var phases = Messenger.PhasesToRoad(state, content, unit);
        var away = phases is { } n ? $"{n} of its phases from the road at {route.Road} on open ground" : $"no path to the road at {route.Road}";
        var awake = Messenger.Runs(state, content, unit with { Moved = false }) ? "running" : "not yet running";
        return $"messenger at {unit.At}, {awake}, {away}";
    }

    /// <summary>
    /// The chest line (issue 649): <c>chests ($): 3,1 Steel Sword, Field Dressing; 9,4 Iron Bow (</c>
    /// <see cref="ChestRule"/><c>)</c>, each closed chest in file order with its tile and contents.
    /// Null when no chest is left closed. The guard, if any, is a unit on the board; the way in is not printed.
    /// </summary>
    public static string? ChestLegend(IEnumerable<Chest> closed, GameContent content)
    {
        var listed = closed.Select(c => $"{c.At} {string.Join(", ", c.Items.Select(content.ItemName))}").ToList();
        return listed.Count == 0 ? null : $"chests ({ChestGlyph}): {string.Join("; ", listed)} ({ChestRule})";
    }

    private static void DrawChests(IEnumerable<Chest> chests, int y, char[] row)
    {
        foreach (var chest in chests)
        {
            if (chest.At.Y == y)
            {
                row[chest.At.X] = ChestGlyph;
            }
        }
    }

    private static void DrawExits(MapDefinition map, int y, char[] row)
    {
        foreach (var exit in map.Exits)
        {
            if (exit.Y == y)
            {
                row[exit.X] = ExitGlyph;
            }
        }
    }

    /// <summary>
    /// The letter each placement is drawn with, by placement index. Players take
    /// A..Z, enemies a..z, and a boss is always <see cref="BossGlyph"/>. A letter that
    /// any terrain in the content draws with is skipped on both sides, so a unit is
    /// never drawn as a tile (issue 41); the skip uses the whole content, not the
    /// terrain on this map, so a slot keeps its letter from map to map. Past the usable
    /// count on a side the letters wrap; no v1 map has that many units. Spawned units
    /// (issue 32) follow the placements in spawn order, at <see cref="MapDefinition.SpawnIndex"/>.
    /// </summary>
    public static char[] Letters(MapDefinition map, GameContent content)
    {
        var upper = Alphabet('A', content);
        var lower = Alphabet('a', content);
        var placements = map.Placements.Concat(map.Spawns()).ToList();
        var letters = new char[placements.Count];
        var players = 0;
        var enemies = 0;
        for (var i = 0; i < placements.Count; i++)
        {
            letters[i] = placements[i] switch
            {
                EnemyPlacement { IsBoss: true } => BossGlyph,
                EnemyPlacement => lower[enemies++ % lower.Length],
                _ => upper[players++ % upper.Length],
            };
        }

        return letters;
    }

    /// <summary>
    /// The 26 letters from <paramref name="first"/> minus every one a terrain draws with.
    /// Content whose glyphs use every letter of a case leaves nothing to draw units with
    /// and is refused, since a view with no letters would be a grid of terrain only.
    /// </summary>
    private static char[] Alphabet(char first, GameContent content)
    {
        var alphabet = new List<char>(26);
        for (var offset = 0; offset < 26; offset++)
        {
            var letter = (char)(first + offset);
            if (content.TerrainByGlyph(letter) is null)
            {
                alphabet.Add(letter);
            }
        }

        return alphabet.Count > 0
            ? alphabet.ToArray()
            : throw new InvalidOperationException($"terrain glyphs use every letter {first}..{(char)(first + 25)}; nothing is left to draw units with");
    }

    private static string Describe(Placement placement, MapDefinition map, GameContent content)
    {
        var terrain = map.TerrainAt(placement.At, content).Label();
        switch (placement)
        {
            case PlayerPlacement p:
                var slot = p.Slot switch
                {
                    PlayerSlot.Captain => "captain",
                    PlayerSlot.NamedRecruit => "recruit " + p.RecruitId,
                    _ => "recruit (any)",
                };
                return $"{slot,-22} {p.At,-6} {terrain}";
            case EnemyPlacement e:
                var unit = map.EnemyUnit(e, content);
                var who = $"{unit.Name} L{unit.Level}";
                var role = e.IsBoss ? (e.Behavior == Behavior.Guard ? "boss, guard" : "boss") : e.Behavior.ToString().ToLowerInvariant();
                return $"{who,-22} {e.At,-6} {terrain}  group {e.Group}, {role}";
            default:
                throw new ArgumentOutOfRangeException(nameof(placement), placement, "unknown placement kind");
        }
    }

    /// <summary>The win condition as the map file spells it.</summary>
    public static string WinName(WinCondition win) => win switch
    {
        WinCondition.Rout => "rout",
        WinCondition.Seize => "seize",
        WinCondition.DefeatBoss => "defeat_boss",
        WinCondition.Survive => "survive",
        WinCondition.Escape => "escape",
        _ => throw new ArgumentOutOfRangeException(nameof(win), win, "unknown win condition"),
    };
}
