using System.Collections.Immutable;

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

    private UnitNames(ImmutableDictionary<string, string> names)
    {
        _names = names;
    }

    /// <summary>No names known: every id reads as itself.</summary>
    public static UnitNames None { get; } = new(ImmutableDictionary<string, string>.Empty);

    /// <summary>
    /// The names for <paramref name="state"/>'s battle: every player unit that stood on its
    /// board (deployed at the start, still standing, or escaped) and every enemy its map
    /// places or spawns.
    /// </summary>
    public static UnitNames Of(BattleState state, GameContent content)
    {
        var names = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        var players = (state.History.Count > 0 ? state.History[0].Units : Enumerable.Empty<BattleUnit>())
            .Concat(state.Units)
            .Concat(state.Escaped)
            .Where(u => u.Side == Side.Player);
        foreach (var unit in players)
        {
            names[unit.Id] = unit.Unit.Name;
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

        return new UnitNames(names.ToImmutable());
    }

    /// <summary>The name <paramref name="id"/> reads as, or the id itself when no unit by that id is known.</summary>
    public string this[string id] => _names.TryGetValue(id, out var name) ? name : id;

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
