using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Ironwake.Core;

/// <summary>
/// The name a reader sees for each unit id (issue 609): the unit's display name, so narration
/// says "Toll Warden" where a command types <c>toll_warden-1</c>. Enemies that share a display
/// name on the map are numbered in file order, placements first and then spawn events, the
/// order <see cref="MapDefinition.SpawnId"/> counts in, so "Archer 1" is the same archer for the
/// whole battle whether or not a later archer ever arrives. An id the map and the board never
/// held reads as itself.
/// </summary>
public sealed class UnitNames
{
    private readonly ImmutableDictionary<string, string> _names;
    private readonly ImmutableDictionary<string, Pronoun> _pronouns;
    private readonly ImmutableDictionary<string, MapEventAction> _events;
    private readonly ImmutableHashSet<string> _riders;

    private UnitNames(ImmutableDictionary<string, string> names, ImmutableDictionary<string, Pronoun> pronouns, ImmutableDictionary<string, MapEventAction> events, ImmutableHashSet<string>? riders = null)
    {
        _names = names;
        _pronouns = pronouns;
        _events = events;
        _riders = riders ?? ImmutableHashSet<string>.Empty;
    }

    /// <summary>No names known: every id reads as itself.</summary>
    public static UnitNames None { get; } = new(
        ImmutableDictionary<string, string>.Empty,
        ImmutableDictionary<string, Pronoun>.Empty,
        ImmutableDictionary<string, MapEventAction>.Empty);

    /// <summary>
    /// The names for <paramref name="state"/>'s battle: every player unit that stood on its
    /// board (deployed at the start, still standing, or escaped) and every enemy its map
    /// places or spawns, with each player unit's pronoun, the one chosen for it (issue 681) or the cast file's (issue 615), and what
    /// each of the map's events does, so a fired event reads by its effect, not its id.
    /// </summary>
    public static UnitNames Of(BattleState state, GameContent content)
    {
        var names = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        var players = (state.History.Count > 0 ? state.History[0].Units : Enumerable.Empty<BattleUnit>())
            .Concat(state.Units)
            .Concat(state.Escaped)
            .Where(u => u.Side == Side.Player);
        var pronouns = ImmutableDictionary.CreateBuilder<string, Pronoun>(StringComparer.Ordinal);
        var riders = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        foreach (var unit in players)
        {
            names[unit.Id] = unit.Unit.Name;
            if (unit.Unit.Drake is not null)
            {
                riders.Add(unit.Id);
            }

            if ((unit.Unit.Pronoun ?? (content.Pronouns.TryGetValue(unit.Id, out var cast) ? cast : null)) is { } pronoun)
            {
                pronouns[unit.Id] = pronoun;
            }
        }

        var map = state.Map;
        var enemies = new List<(string Id, string Name)>();
        var perTemplate = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var placement in map.Placements.OfType<EnemyPlacement>())
        {
            var count = perTemplate.GetValueOrDefault(placement.TemplateId) + 1;
            perTemplate[placement.TemplateId] = count;
            enemies.Add((placement.TemplateId + "-" + count, map.EnemyUnit(placement, content).Name));
        }

        foreach (var spawn in map.Events.Where(e => e.Action is SpawnEnemy))
        {
            enemies.Add((map.SpawnId(spawn), map.EnemyUnit(((SpawnEnemy)spawn.Action).Placement, content).Name));
        }

        foreach (var group in enemies.GroupBy(e => e.Name, StringComparer.Ordinal))
        {
            var members = group.ToList();
            for (var i = 0; i < members.Count; i++)
            {
                names[members[i].Id] = members.Count == 1 ? members[i].Name : $"{members[i].Name} {i + 1}";
            }
        }

        // A board unit the map does not account for (a test's hand-built state) keeps its own name.
        foreach (var unit in state.Units.Where(u => u.Side == Side.Enemy && !names.ContainsKey(u.Id)))
        {
            names[unit.Id] = unit.Unit.Name;
        }

        // A Hollow (issue 1284) reads as the body it rose from, on the board or crumbled: "Hollow Brigand 1".
        foreach (var (id, fallen) in names.Where(n => !n.Key.StartsWith(Hollow.IdPrefix, StringComparison.Ordinal)).ToList())
        {
            names[Hollow.IdPrefix + id] = "Hollow " + fallen;
        }

        // The returned claimant (issue 633) is placed by the campaign, not the map, and keeps their name and pronoun after leaving the board.
        if (state.Return is { } bond
            && (state.History.Count > 0 ? state.History[0].Units : state.Units).Concat(state.Units).FirstOrDefault(u => u.Id == bond.UnitId) is { } returned)
        {
            names[returned.Id] = returned.Unit.Name;
            if (returned.Unit.Drake is not null)
            {
                riders.Add(returned.Id);
            }

            if ((returned.Unit.Pronoun ?? (content.Pronouns.TryGetValue(returned.Id, out var cast) ? cast : null)) is { } pronoun)
            {
                pronouns[returned.Id] = pronoun;
            }
        }

        var events = ImmutableDictionary.CreateBuilder<string, MapEventAction>(StringComparer.Ordinal);
        foreach (var mapEvent in map.Events)
        {
            events[mapEvent.Name] = mapEvent.Action;
        }

        return new UnitNames(names.ToImmutable(), pronouns.ToImmutable(), events.ToImmutable(), riders.ToImmutable());
    }

    /// <summary>
    /// The names for the campaign's between-map screen (issue 615): every unit on
    /// <paramref name="record"/>'s roster by its own name, and every fallen unit by its cast
    /// name or, for a hire (issue 690), the barracks' name for them, each with its pronoun, the one chosen for it (issue 681) or the cast file's. The screen has no board, so no enemy
    /// and no map event is known.
    /// </summary>
    public static UnitNames Of(CampaignRecord record, GameContent content)
    {
        var names = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        var pronouns = ImmutableDictionary.CreateBuilder<string, Pronoun>(StringComparer.Ordinal);
        foreach (var hire in content.Campaign.Keep.Hires)
        {
            names[hire.Id] = hire.Name;
            pronouns[hire.Id] = hire.Pronoun;
        }

        foreach (var unit in content.Cast.Concat(record.Roster))
        {
            names[unit.Id] = unit.Name;
            if ((unit.Pronoun ?? (content.Pronouns.TryGetValue(unit.Id, out var cast) ? cast : null)) is { } pronoun)
            {
                pronouns[unit.Id] = pronoun;
            }
        }

        return new UnitNames(names.ToImmutable(), pronouns.ToImmutable(), ImmutableDictionary<string, MapEventAction>.Empty);
    }

    /// <summary>Whether <paramref name="id"/> took the field on a drake (issue 805): a player unit, or the returned claimant, that rides one.</summary>
    public bool Rides(string id) => _riders.Contains(id);

    /// <summary>The name <paramref name="id"/> reads as, or the id itself when no unit by that id is known.</summary>
    public string this[string id] => _names.TryGetValue(id, out var name) ? name : id;

    /// <summary>
    /// How a line refers back to <paramref name="id"/> once it has named the unit (issue 615): its
    /// pronoun when the cast file gives one, else its name again, never "it".
    /// </summary>
    public Referent Refer(string id) =>
        _pronouns.TryGetValue(id, out var pronoun) ? Referent.For(pronoun, this[id]) : new Referent(this[id], this[id], this[id] + "'s", false, this[id]);

    /// <summary>A sleeping group as a reader sees it (issue 615): <c>the mill group</c>.</summary>
    public static string Group(string group) => $"the {group} group";

    /// <summary>
    /// The line for a fired map event (issue 615), by what it does rather than by its map id:
    /// <c>reinforcements arrive</c> or, when a unit holds the tile, <c>reinforcements are
    /// blocked: a unit holds 7,0</c>, and when the tile's terrain bars the spawn, named by
    /// <paramref name="barredBy"/>, <c>reinforcements are blocked: 8,0 is wall</c> (issue 655);
    /// <c>the ground changes</c> or <c>7,3 does not change: a unit holds it</c>. A flag event,
    /// or one the map does not list, keeps its name.
    /// </summary>
    public string Event(string name, bool blocked, string? barredBy = null) => _events.GetValueOrDefault(name) switch
    {
        SpawnEnemy spawn => !blocked ? "reinforcements arrive"
            : barredBy is null ? $"reinforcements are blocked: a unit holds {spawn.Placement.At}"
            : $"reinforcements are blocked: {spawn.Placement.At} is {barredBy.ToLowerInvariant()}",
        ChangeTerrain change => blocked ? $"{change.At} does not change: a unit holds it" : "the ground changes",
        _ => $"event {name}" + (blocked ? " is blocked: its tile is held" : ""),
    };

    /// <summary>
    /// A refusal as a reader sees it (issue 615): every unit id this battle knows reads as its
    /// name, an id quoted as it was typed (<c>'wren'</c>) stays as typed, and each line is in
    /// sentence case: <c>wren cannot move to 3,4: ...</c> reads <c>Wren cannot move to 3,4: ...</c>,
    /// <c>captain is at full HP</c> reads <c>Alder Fenn is at full HP</c>. A word after an
    /// article is a role, not a unit: <c>the captain is dead</c> stays as it is.
    /// </summary>
    public string Message(string text) => Sentence(Named(text));

    /// <summary>
    /// <paramref name="text"/> with every unit id this battle knows read as its name, as
    /// <see cref="Message"/> reads it, but with no sentence case, for a clause set inside a line:
    /// <c>Battle lost: the captain is dead</c>, <c>Campaign lost on ...: wren is dead</c>.
    /// </summary>
    public string Named(string text) =>
        IdToken.Replace(text, m => m.Value[0] == '\'' ? m.Value : this[m.Value]);

    private static readonly Regex IdToken = new(@"'[^'\s]*'|(?<![\w-])(?<!\b(?:the|a|an) )[a-z][a-z0-9_]*(?:-[0-9]+)?(?![\w-])", RegexOptions.CultureInvariant);

    /// <summary>
    /// <paramref name="text"/> in sentence case (issue 609): on each line, the first character
    /// after the indent and any banner dashes is upper-cased when it is a lower-case letter. A
    /// line that opens with a coordinate or a number is left as it is.
    /// </summary>
    public static string Sentence(string text) =>
        string.Join('\n', text.Split('\n').Select(SentenceLine));

    private static string SentenceLine(string line)
    {
        var i = 0;
        while (i < line.Length && (line[i] == ' ' || line[i] == '-'))
        {
            i++;
        }

        return i < line.Length && char.IsLower(line[i])
            ? string.Concat(line.AsSpan(0, i), char.ToUpperInvariant(line[i]).ToString(), line.AsSpan(i + 1))
            : line;
    }
}
