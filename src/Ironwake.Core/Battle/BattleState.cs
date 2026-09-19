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
/// <param name="AwakeGroups">Guard groups that have woken (section 8), sorted by name. A woken group is a fact of the board, so a Recall restores it with the rest.</param>
public sealed record BattleState(
    MapDefinition Map,
    ValueList<BattleUnit> Units,
    int Turn,
    Side Phase,
    ulong Seed,
    RollScheme Scheme,
    int RecallCharges,
    ValueList<BattleState> History,
    ValueList<string> AwakeGroups = default)
{
    /// <summary>Whether a Guard group has woken. Groups of any other behavior are never asked about.</summary>
    public bool IsAwake(string group) => AwakeGroups.Contains(group);

    /// <summary>
    /// How an enemy behaves now (DESIGN.md section 8): a sleeping Guard as Hold, a woken
    /// Guard as Aggressive, everything else as its map says. A player unit has no behavior and gets null.
    /// </summary>
    public Behavior? EffectiveBehavior(BattleUnit unit) =>
        unit.Behavior switch
        {
            Core.Behavior.Guard => unit.Group is { } group && IsAwake(group) ? Core.Behavior.Aggressive : Core.Behavior.Hold,
            var other => other,
        };

    /// <summary>This state with a group woken. Idempotent; the list stays sorted.</summary>
    public BattleState Wake(string group)
    {
        if (IsAwake(group))
        {
            return this;
        }

        var groups = AwakeGroups.Add(group).ToList();
        groups.Sort(string.CompareOrdinal);
        return this with { AwakeGroups = ValueList<string>.From(groups) };
    }

    /// <summary>
    /// The opening state of a map. <paramref name="roster"/> is the player's units in
    /// order: the first is the captain and fills the captain's slot, a named slot takes
    /// the roster unit with that id, and bare recruit slots are filled by the remaining
    /// roster units in roster order. Roster units past the map's slots are benched.
    /// Enemies come from <see cref="MapDefinition.EnemyUnit"/> and are named
    /// <c>template-n</c>, counting placements of that template from 1. A roster that
    /// cannot fill the map, or a unit that cannot stand where the map puts it, is a
    /// content or programming error and throws with the slot named. <paramref name="benched"/>
    /// names roster units held back for gate 4's ablation (DESIGN.md section 11): the
    /// placement such a unit would have filled, named or bare, stays empty, so the bench
    /// removes a body and never shifts another recruit into the slot. The captain cannot
    /// be benched.
    /// </summary>
    public static BattleState From(
        MapDefinition map, GameContent content, ValueList<Unit> roster, ulong seed, RollScheme scheme = RollScheme.TwoRollAverage, ValueList<string> benched = default)
    {
        if (roster.Count == 0)
        {
            throw new ArgumentException("the roster needs a captain", nameof(roster));
        }

        if (benched.Contains(roster[0].Id))
        {
            throw new ArgumentException($"the captain '{roster[0].Id}' cannot be benched", nameof(benched));
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
        for (var index = 0; index < map.Placements.Count; index++)
        {
            var placement = map.Placements[index];
            switch (placement)
            {
                case PlayerPlacement p:
                    var unit = Fill(p, roster, named, deployed, ref nextBare);
                    if (benched.Contains(unit.Id))
                    {
                        break;
                    }

                    units.Add(Place(unit, Side.Player, p.At, map, content) with { IsCaptain = p.Slot == PlayerSlot.Captain, PlacementIndex = index });
                    break;
                case EnemyPlacement e:
                    var count = perTemplate.GetValueOrDefault(e.TemplateId) + 1;
                    perTemplate[e.TemplateId] = count;
                    var enemy = map.EnemyUnit(e, content) with { Id = e.TemplateId + "-" + count };
                    units.Add(Place(enemy, Side.Enemy, e.At, map, content) with { Group = e.Group, Behavior = e.Behavior, IsBoss = e.IsBoss, PlacementIndex = index });
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

        return new BattleUnit(RefreshSpells(unit, content), side, at, unit.EffectiveStats(unitClass).Hp, false, false);
    }

    /// <summary>Section 5: spells have uses per battle, so every Reason or Faith weapon starts a map at its full durability. Physical weapons carry what they have.</summary>
    public static Unit RefreshSpells(Unit unit, GameContent content)
    {
        var items = new List<ItemStack>(unit.Inventory.Count);
        foreach (var item in unit.Inventory.Items)
        {
            items.Add(content.Weapons.TryGetValue(item.ItemId, out var weapon) && weapon.IsMagic ? item with { Uses = weapon.Durability } : item);
        }

        return unit with { Inventory = new Inventory(ValueList<ItemStack>.From(items)) };
    }

    /// <summary>
    /// Whether the battle is over and why, DESIGN.md section 7, computed from the board
    /// and never stored. Checked in order: the captain dead, the protected recruit dead,
    /// the map's win condition met, the turn limit passed (a win for Survive, a loss for
    /// everything else), else ongoing. A finished battle refuses every command but Recall.
    /// </summary>
    public BattleOutcome Outcome
    {
        get
        {
            var captain = Units.FirstOrDefault(u => u.IsCaptain);
            if (captain is null)
            {
                return new BattleOutcome(BattleResult.Lost, "the captain is dead", LossCause.Captain);
            }

            if (Map.ProtectId is { } protectId && Find(protectId) is null)
            {
                return new BattleOutcome(BattleResult.Lost, $"{protectId} is dead", LossCause.Protected);
            }

            var won = Map.Win switch
            {
                WinCondition.Rout => !UnitsOf(Side.Enemy).Any(),
                WinCondition.Seize => Map.IsThrone(captain.At),
                WinCondition.DefeatBoss => !UnitsOf(Side.Enemy).Any(u => u.IsBoss),
                WinCondition.Escape => UnitsOf(Side.Player).All(u => Map.IsExit(u.At)),
                WinCondition.Survive => Turn > Map.TurnLimit,
                _ => throw new ArgumentOutOfRangeException(nameof(Map), Map.Win, "unknown win condition"),
            };
            if (won)
            {
                return new BattleOutcome(BattleResult.Won, MapRenderer.WinName(Map.Win));
            }

            if (Turn > Map.TurnLimit)
            {
                return new BattleOutcome(BattleResult.Lost, $"turn {Map.TurnLimit} passed", LossCause.Timeout);
            }

            return BattleOutcome.Ongoing;
        }
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
            .Append(" recall ").Append(RecallCharges).Append(" history ").Append(History.Count)
            .Append(" outcome ").Append(Outcome.Result).Append('\n');
        sb.Append("awake");
        foreach (var group in AwakeGroups)
        {
            sb.Append(' ').Append(group);
        }

        sb.Append('\n');
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

            if (unit.IsCaptain)
            {
                sb.Append(" captain");
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
