namespace Ironwake.Core;

/// <summary>
/// The enemy phase as DESIGN.md section 8 writes it: a planner over a working copy, never
/// a mutator. <see cref="Plan"/> returns the command list for the whole phase; the caller
/// applies it to the real state through <see cref="Resolver.Apply"/>, the same function
/// the planner used, so the two agree. Enemies act in ascending unit id, each on the board
/// as the one before it left it. A unit's plan is at most a Move then an Attack or a Wait.
/// The scorer prices a hit through <see cref="Combat.HitProbability"/> under the state's
/// roll scheme, the one hit function the forecast and the resolver share, and the whole
/// score is a double so the crit expectation is never eaten by integer division.
/// </summary>
public static class EnemyAi
{
    public const double KillBonus = 100;
    public const double NoCounterBonus = 10;
    public const double HealerBonus = 5;
    public const double CounterWeight = 0.5;

    /// <summary>
    /// The commands of the enemy phase in order, ending with <see cref="EndPhase"/> unless
    /// the battle was decided on the way. The state must be at the start of an enemy phase.
    /// </summary>
    public static ValueList<Command> Plan(BattleState state, GameContent content)
    {
        if (state.Phase != Side.Enemy)
        {
            throw new ArgumentException("the enemy AI plans only the enemy phase", nameof(state));
        }

        var plan = new List<Command>();
        var working = state;
        foreach (var id in state.UnitsOf(Side.Enemy).Select(u => u.Id).ToList())
        {
            if (working.Outcome.IsOver)
            {
                break;
            }

            var unit = working.Find(id);
            if (unit is null || unit.Acted)
            {
                continue;
            }

            foreach (var command in PlanUnit(working, content, unit))
            {
                var result = Resolver.Apply(working, content, command);
                if (!result.Accepted)
                {
                    throw new InvalidOperationException($"the planner produced an illegal command {command}: {result.Rejection!.Message}");
                }

                plan.Add(command);
                working = result.Next;
            }
        }

        if (!working.Outcome.IsOver)
        {
            plan.Add(new EndPhase());
        }

        return ValueList<Command>.From(plan);
    }

    /// <summary>
    /// One enemy's commands on the board as it stands: the best-scoring attack from the
    /// best tile if any tile allows one, else the approach rule for an Aggressive unit,
    /// else Wait. Hold, Boss, and a sleeping Guard never move; a woken Guard is Aggressive.
    /// The attack options range over every weapon the unit can strike with, in inventory
    /// order; an Attack with a weapon other than the equipped one names its slot, which
    /// moves it to the front so the counter that follows uses it too (section 5, issue 99).
    /// </summary>
    public static IReadOnlyList<Command> PlanUnit(BattleState state, GameContent content, BattleUnit unit)
    {
        var behavior = state.EffectiveBehavior(unit)
            ?? throw new ArgumentException($"{unit.Id} is a player unit and has no behavior", nameof(unit));
        var weapon = unit.EquippedWeapon(content);
        var movement = content.Class(unit.Unit.ClassId).Movement;
        var mayMove = behavior == Behavior.Aggressive && !unit.Moved;
        var reach = state.ReachOf(unit, content);
        var tiles = mayMove ? reach.Destinations.ToList() : new List<Coord> { unit.At };
        var players = state.UnitsOf(Side.Player).ToList();
        var playerReach = players.Select(p => state.ReachOf(p, content)).ToList();

        if (weapon is null)
        {
            return new Command[] { new Wait(unit.Id) };
        }

        var equipped = unit.EquippedSlot(content);
        var arms = Enumerable.Range(0, unit.Unit.Inventory.Count)
            .Where(slot => unit.UsableWeaponAt(content, slot) is not null)
            .Select(slot => (Slot: slot, Weapon: unit.UsableWeaponAt(content, slot)!, Armed: unit.WithSlotInFront(slot)))
            .ToList();

        AttackOption? best = null;
        foreach (var tile in tiles)
        {
            var avoid = state.Map.TerrainAt(tile, content).AvoidFor(movement);
            var exposure = playerReach.Count(r => r.CanEnd(tile));
            var cost = reach.CostTo(tile)!.Value;
            foreach (var arm in arms)
            {
                foreach (var target in players)
                {
                    if (!arm.Weapon.InRange(tile.DistanceTo(target.At)))
                    {
                        continue;
                    }

                    var option = new AttackOption(Score(state, content, arm.Armed, tile, target), target.Id, tile, avoid, exposure, cost, arm.Slot);
                    if (best is null || option.Beats(best))
                    {
                        best = option;
                    }
                }
            }
        }

        if (best is not null)
        {
            var attack = new Attack(unit.Id, best.TargetId, best.Slot == equipped ? null : best.Slot);
            return best.Tile == unit.At
                ? new Command[] { attack }
                : new Command[] { new Move(unit.Id, best.Tile), attack };
        }

        if (!mayMove)
        {
            return new Command[] { new Wait(unit.Id) };
        }

        var destination = Approach(state, content, unit, weapon, reach, players, playerReach);
        return destination is { } to && to != unit.At
            ? new Command[] { new Move(unit.Id, to), new Wait(unit.Id) }
            : new Command[] { new Wait(unit.Id) };
    }

    /// <summary>
    /// Section 8's target score for <paramref name="attacker"/> striking <paramref name="target"/>
    /// from <paramref name="from"/>. Expected damage on both lines carries the crit
    /// expectation, <c>Damage * (1 + 2 * CritChance / 100)</c>, times the strikes that side
    /// makes, capped at the HP it could remove; the kill flag reads deterministic damage
    /// only, so an attack lethal only on a crit is never priced as a kill. The attacker
    /// strikes with its equipped weapon; <see cref="PlanUnit"/> scores another slot by
    /// passing the unit with that slot moved to the front.
    /// </summary>
    public static double Score(BattleState state, GameContent content, BattleUnit attacker, Coord from, BattleUnit target)
    {
        var weapon = attacker.EquippedWeapon(content)
            ?? throw new ArgumentException($"{attacker.Id} has no weapon to score with", nameof(attacker));
        var me = new Combatant(attacker.Unit, content.Class(attacker.Unit.ClassId), weapon, state.Map.TerrainAt(from, content), attacker.Hp);
        var them = target.ToCombatant(state.Map, content);
        var forecast = Combat.Forecast(me, them, from.DistanceTo(target.At), state.Scheme, state.Formula);

        var strikes = forecast.Attacker.Doubles ? 2 : 1;
        var canKill = forecast.Attacker.Damage * strikes >= target.Hp;
        var dealt = Math.Min(target.Hp, Expected(forecast.Attacker, strikes));
        var score = (canKill ? KillBonus : 0) + dealt * Combat.HitProbability(forecast.Attacker.HitChance, state.Scheme);

        if (forecast.Defender.Strikes)
        {
            var counterStrikes = forecast.Defender.Doubles ? 2 : 1;
            var taken = Math.Min(attacker.Hp, Expected(forecast.Defender, counterStrikes));
            score -= taken * Combat.HitProbability(forecast.Defender.HitChance, state.Scheme) * CounterWeight;
        }
        else
        {
            score += NoCounterBonus;
        }

        if (IsHealer(target, content))
        {
            score += HealerBonus;
        }

        return score;
    }

    /// <summary>A unit carrying a healing spell its class can use: the only healers the content can have before issue 9.</summary>
    public static bool IsHealer(BattleUnit unit, GameContent content)
    {
        var unitClass = content.Class(unit.Unit.ClassId);
        foreach (var item in unit.Unit.Inventory.Items)
        {
            if (content.Weapons.TryGetValue(item.ItemId, out var weapon) && weapon.Heals && unitClass.CanUse(weapon.Type))
            {
                return true;
            }
        }

        return false;
    }

    private static double Expected(SideForecast side, int strikes) => side.Damage * (1 + 2 * side.CritChance / 100.0) * strikes;

    /// <summary>
    /// The approach rule of section 8 for a mover that can attack nobody this phase.
    /// Target: the player unit whose nearest attack tile has the lowest path cost from the
    /// mover's tile, no Mov budget, ties by lowest unit id; no path means not a target.
    /// Destination: the reachable tile with the lowest remaining path cost to an attack
    /// tile on that target, ties by highest terrain avoid, then fewest player units whose
    /// reach set contains the tile, then cost from the mover, then row-major. Null when
    /// there is no target, which means Wait.
    /// </summary>
    public static Coord? Approach(
        BattleState state, GameContent content, BattleUnit unit, Weapon weapon, Reach reach,
        IReadOnlyList<BattleUnit> players, IReadOnlyList<Reach> playerReach)
    {
        var movement = content.Class(unit.Unit.ClassId).Movement;
        Occupant OccupantAt(Coord at) => at == unit.At ? Occupant.None : state.OccupantAt(at, unit.Side);

        Distances? chosen = null;
        var chosenCost = int.MaxValue;
        foreach (var target in players)
        {
            var distances = Movement.DistancesTo(state.Map, content, AttackTiles(state, content, unit, weapon, target, movement), movement, OccupantAt);
            var cost = distances.From(unit.At);
            if (cost is { } c && c < chosenCost)
            {
                chosen = distances;
                chosenCost = c;
            }
        }

        if (chosen is null)
        {
            return null;
        }

        Coord? destination = null;
        var bestKey = (Remaining: int.MaxValue, Avoid: int.MinValue, Exposure: int.MaxValue, Cost: int.MaxValue);
        foreach (var tile in reach.Destinations)
        {
            if (chosen.From(tile) is not { } remaining)
            {
                continue;
            }

            var key = (
                Remaining: remaining,
                Avoid: -state.Map.TerrainAt(tile, content).AvoidFor(movement),
                Exposure: playerReach.Count(r => r.CanEnd(tile)),
                Cost: reach.CostTo(tile)!.Value);
            if (key.CompareTo(bestKey) < 0)
            {
                bestKey = key;
                destination = tile;
            }
        }

        return destination;
    }

    /// <summary>
    /// The tiles from which <paramref name="unit"/>'s weapon reaches <paramref name="target"/>
    /// and on which it could end a move: inside the map, enterable by its movement type,
    /// and occupied by nobody but itself.
    /// </summary>
    public static IEnumerable<Coord> AttackTiles(BattleState state, GameContent content, BattleUnit unit, Weapon weapon, BattleUnit target, MovementType movement)
    {
        for (var dy = -weapon.MaxRange; dy <= weapon.MaxRange; dy++)
        {
            for (var dx = -weapon.MaxRange; dx <= weapon.MaxRange; dx++)
            {
                var tile = new Coord(target.At.X + dx, target.At.Y + dy);
                if (!weapon.InRange(Math.Abs(dx) + Math.Abs(dy)) || !state.Map.Contains(tile))
                {
                    continue;
                }

                if (!state.Map.TerrainAt(tile, content).IsPassable(movement))
                {
                    continue;
                }

                var standing = state.UnitAt(tile);
                if (standing is null || standing.Id == unit.Id)
                {
                    yield return tile;
                }
            }
        }
    }

    /// <summary>
    /// One scored attack. <see cref="Beats"/> is section 8's whole order: higher score,
    /// then lower target id, then higher terrain avoid on the tile, then fewer player
    /// units whose reach set contains it, then lower cost from the mover, then row-major.
    /// An option equal to the best on the whole order does not beat it, so of two weapons
    /// scoring the same the earlier slot strikes.
    /// </summary>
    private sealed record AttackOption(double Score, string TargetId, Coord Tile, int Avoid, int Exposure, int Cost, int Slot)
    {
        public bool Beats(AttackOption other)
        {
            if (Score != other.Score)
            {
                return Score > other.Score;
            }

            var byId = string.CompareOrdinal(TargetId, other.TargetId);
            if (byId != 0)
            {
                return byId < 0;
            }

            if (Avoid != other.Avoid)
            {
                return Avoid > other.Avoid;
            }

            if (Exposure != other.Exposure)
            {
                return Exposure < other.Exposure;
            }

            if (Cost != other.Cost)
            {
                return Cost < other.Cost;
            }

            return Tile.CompareTo(other.Tile) < 0;
        }
    }
}
