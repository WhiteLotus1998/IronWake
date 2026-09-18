using System.Text;

namespace Ironwake.Core;

/// <summary>
/// A battle at one moment: the map, every living unit, whose phase it is, the turn, the
/// campaign seed, the Recall charges left, and the history of prior states (DESIGN.md
/// sections 2 and 7). Immutable; <see cref="Resolver.Apply"/> is the only way forward and
/// <see cref="Recall"/> the only way back. The state carries the seed and nothing else
/// about chance: every roll is keyed (section 5), so there is no stream position to save.
/// </summary>
/// <param name="Map">The map as authored. Starting positions are history; <see cref="Units"/> says where everyone is.</param>
/// <param name="Units">Every living unit, in ascending id order.</param>
/// <param name="Turn">The turn counter, from 1.</param>
/// <param name="Phase">Whose phase it is.</param>
/// <param name="Seed">The campaign seed every roll derives from.</param>
/// <param name="Scheme">How hit rolls are read, section 5.</param>
/// <param name="RecallCharges">Recall charges left on this map.</param>
/// <param name="History">Every prior state, oldest first, each stored with an empty history of its own so the record stays finite.</param>
public sealed record BattleState(
    MapDefinition Map,
    ValueList<BattleUnit> Units,
    int Turn,
    Side Phase,
    ulong Seed,
    RollScheme Scheme,
    int RecallCharges,
    ValueList<BattleState> History)
{
    /// <summary>
    /// The opening state of a map. <paramref name="roster"/> is the player's units in
    /// order: the first is the captain and fills the captain's slot, a named slot takes
    /// the roster unit with that id, and bare recruit slots are filled by the remaining
    /// roster units in roster order. Roster units past the map's slots are benched.
    /// Enemies come from <see cref="MapDefinition.EnemyUnit"/> and are named
    /// <c>template-n</c>, counting placements of that template from 1. A roster that
    /// cannot fill the map, or a unit that cannot stand where the map puts it, is a
    /// content or programming error and throws with the slot named.
    /// </summary>
    public static BattleState From(
        MapDefinition map, GameContent content, ValueList<Unit> roster, ulong seed, RollScheme scheme = RollScheme.TwoRollAverage)
    {
        if (roster.Count == 0)
        {
            throw new ArgumentException("the roster needs a captain", nameof(roster));
        }

        var units = new List<BattleUnit>();
        var deployed = new HashSet<string>(StringComparer.Ordinal);
        var named = new HashSet<string>(StringComparer.Ordinal);
        foreach (var placement in map.Placements)
        {
            if (placement is PlayerPlacement { Slot: PlayerSlot.NamedRecruit, RecruitId: { } id })
            {
                named.Add(id);
            }
        }

        var nextBare = 1;
        var perTemplate = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var placement in map.Placements)
        {
            switch (placement)
            {
                case PlayerPlacement p:
                    var unit = Fill(p, roster, named, deployed, ref nextBare);
                    units.Add(Place(unit, Side.Player, p.At, map, content));
                    break;
                case EnemyPlacement e:
                    var count = perTemplate.GetValueOrDefault(e.TemplateId) + 1;
                    perTemplate[e.TemplateId] = count;
                    var enemy = map.EnemyUnit(e, content) with { Id = e.TemplateId + "-" + count };
                    units.Add(Place(enemy, Side.Enemy, e.At, map, content) with { Group = e.Group, Behavior = e.Behavior, IsBoss = e.IsBoss });
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(map), placement, "unknown placement kind");
            }
        }

        units.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        for (var i = 1; i < units.Count; i++)
        {
            if (units[i].Id == units[i - 1].Id)
            {
                throw new ArgumentException($"two units share the id '{units[i].Id}'", nameof(roster));
            }
        }

        return new BattleState(map, ValueList<BattleUnit>.From(units), 1, Side.Player, seed, scheme, map.RecallCharges, ValueList<BattleState>.Empty);
    }

    private static Unit Fill(PlayerPlacement slot, ValueList<Unit> roster, HashSet<string> named, HashSet<string> deployed, ref int nextBare)
    {
        Unit? unit;
        switch (slot.Slot)
        {
            case PlayerSlot.Captain:
                unit = roster[0];
                break;
            case PlayerSlot.NamedRecruit:
                unit = null;
                for (var i = 1; i < roster.Count; i++)
                {
                    if (roster[i].Id == slot.RecruitId)
                    {
                        unit = roster[i];
                    }
                }

                if (unit is null)
                {
                    throw new ArgumentException($"the map wants recruit '{slot.RecruitId}' at {slot.At} and the roster has no recruit with that id", nameof(roster));
                }

                break;
            default:
                unit = null;
                while (nextBare < roster.Count && unit is null)
                {
                    if (!deployed.Contains(roster[nextBare].Id) && !named.Contains(roster[nextBare].Id))
                    {
                        unit = roster[nextBare];
                    }

                    nextBare++;
                }

                if (unit is null)
                {
                    throw new ArgumentException($"the map has a recruit slot at {slot.At} and the roster has no recruit left to fill it", nameof(roster));
                }

                break;
        }

        if (!deployed.Add(unit.Id))
        {
            throw new ArgumentException($"roster unit '{unit.Id}' is deployed twice; the slot at {slot.At} asks for it again", nameof(roster));
        }

        return unit;
    }

    private static BattleUnit Place(Unit unit, Side side, Coord at, MapDefinition map, GameContent content)
    {
        var unitClass = content.Class(unit.ClassId);
        var terrain = map.TerrainAt(at, content);
        if (!terrain.IsPassable(unitClass.Movement))
        {
            throw new ArgumentException(Movement.CannotStandMessage(at, terrain, unitClass.Movement), nameof(unit));
        }

        return new BattleUnit(unit, side, at, unit.EffectiveStats(unitClass).Hp, false, false);
    }

    /// <summary>The living unit with an id, or null.</summary>
    public BattleUnit? Find(string id)
    {
        foreach (var unit in Units)
        {
            if (unit.Id == id)
            {
                return unit;
            }
        }

        return null;
    }

    /// <summary>The living unit standing on a tile, or null.</summary>
    public BattleUnit? UnitAt(Coord at)
    {
        foreach (var unit in Units)
        {
            if (unit.At == at)
            {
                return unit;
            }
        }

        return null;
    }

    /// <summary>Who stands on a tile, seen from a mover's side: the occupancy answer <see cref="Movement.Reach"/> asks for.</summary>
    public Occupant OccupantAt(Coord at, Side moverSide) =>
        UnitAt(at) switch
        {
            null => Occupant.None,
            var unit when unit.Side == moverSide => Occupant.Ally,
            _ => Occupant.Enemy,
        };

    public IEnumerable<BattleUnit> UnitsOf(Side side)
    {
        foreach (var unit in Units)
        {
            if (unit.Side == side)
            {
                yield return unit;
            }
        }
    }

    /// <summary>Where a unit may move, by the section 4 rule on the board as it stands.</summary>
    public Reach ReachOf(BattleUnit unit, GameContent content)
    {
        var unitClass = content.Class(unit.Unit.ClassId);
        return Movement.Reach(Map, content, unit.At, unitClass.Movement, unitClass.Mov, at => OccupantAt(at, unit.Side));
    }

    /// <summary>This state with one unit replaced by id. The unit must exist.</summary>
    public BattleState WithUnit(BattleUnit unit)
    {
        for (var i = 0; i < Units.Count; i++)
        {
            if (Units[i].Id == unit.Id)
            {
                return this with { Units = Units.SetItem(i, unit) };
            }
        }

        throw new ArgumentException($"no living unit with id '{unit.Id}'", nameof(unit));
    }

    /// <summary>This state with a unit removed by id. The unit must exist.</summary>
    public BattleState WithoutUnit(string id)
    {
        for (var i = 0; i < Units.Count; i++)
        {
            if (Units[i].Id == id)
            {
                return this with { Units = Units.RemoveAt(i) };
            }
        }

        throw new ArgumentException($"no living unit with id '{id}'", nameof(id));
    }

    /// <summary>
    /// The state as text, one line per fact, the same bytes for the same state on every
    /// machine. Gate 6 compares replays through this; it is the seed of issue 25's protocol.
    /// History is summarised by its length; each prior state is canonical on its own.
    /// </summary>
    public string Canonical()
    {
        var sb = new StringBuilder();
        sb.Append("map ").Append(Map.Name).Append(' ').Append(Map.Width).Append('x').Append(Map.Height).Append('\n');
        sb.Append("turn ").Append(Turn).Append(" phase ").Append(Phase)
            .Append(" seed ").Append(Seed).Append(" scheme ").Append(Scheme)
            .Append(" recall ").Append(RecallCharges).Append(" history ").Append(History.Count).Append('\n');
        foreach (var unit in Units)
        {
            sb.Append("unit ").Append(unit.Id).Append(' ').Append(unit.Side).Append(' ').Append(unit.At)
                .Append(" hp ").Append(unit.Hp)
                .Append(unit.Moved ? " moved" : " unmoved").Append(unit.Acted ? " acted" : " ready")
                .Append(" class ").Append(unit.Unit.ClassId)
                .Append(" level ").Append(unit.Unit.Level).Append(" exp ").Append(unit.Unit.Exp)
                .Append(" stats ").Append(unit.Unit.Stats)
                .Append(" items");
            foreach (var item in unit.Unit.Inventory.Items)
            {
                sb.Append(' ').Append(item.ItemId).Append('x').Append(item.Uses);
            }

            if (unit.Side == Side.Enemy)
            {
                sb.Append(" group ").Append(unit.Group).Append(' ').Append(unit.Behavior).Append(unit.IsBoss ? " boss" : "");
            }

            sb.Append('\n');
        }

        return sb.ToString();
    }
}
