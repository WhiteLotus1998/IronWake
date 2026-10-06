namespace Ironwake.Core;

/// <summary>
/// The enemy phase as DESIGN.md section 8 writes it: a planner over a working copy, never
/// a mutator. <see cref="Plan"/> returns the command list for the whole phase; the caller
/// applies it to the real state through <see cref="Resolver.Apply"/>, the same function
/// the planner used, so the two agree. Enemies act in ascending unit id, each on the board
/// as the one before it left it, except that on a <c>pincer: on</c> map an anvil acts ahead
/// of the rest (<see cref="Anvil"/>, issue 419). A unit's plan is at most a Move then an Attack or a Wait.
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
    public static ValueList<Command> Plan(BattleState state, GameContent content) =>
        PlanWithAnvils(state, content).Commands;

    /// <summary>
    /// <see cref="Plan"/>'s commands together with the anvil plans (<see cref="Anvil"/>, issue
    /// 419) the phase took, in the order they act, so a caller can count the anvils that acted
    /// without planning again (issue 495). Empty on a map without the <c>pincer: on</c> header.
    /// </summary>
    public static (ValueList<Command> Commands, ValueList<AnvilPlan> Anvils) PlanWithAnvils(BattleState state, GameContent content)
    {
        if (state.Phase != Side.Enemy)
        {
            throw new ArgumentException("the enemy AI plans only the enemy phase", nameof(state));
        }

        var plan = new List<Command>();
        var working = state;
        var order = state.UnitsOf(Side.Enemy).Select(u => u.Id).ToList();
        var claimed = new HashSet<string>();
        var anvils = new List<AnvilPlan>();
        while (order.Count > 0)
        {
            if (working.Outcome.IsOver)
            {
                break;
            }

            var (id, anvil) = Next(working, content, order, claimed);
            order.Remove(id);
            var unit = working.Find(id);
            if (unit is null || unit.Acted)
            {
                continue;
            }

            if (anvil is not null)
            {
                claimed.Add(anvil.FollowerId);
                anvils.Add(anvil);
            }

            foreach (var command in anvil?.Commands ?? PlanUnit(working, content, unit))
            {
                var result = Resolver.Apply(working, content, command);
                if (!result.Accepted)
                {
                    throw new InvalidOperationException($"the planner produced an illegal command {command}: {result.Rejection!.Message}");
                }

                plan.Add(command);
                working = result.Next;
                if (working.Find(id) is null || working.Outcome.IsOver)
                {
                    // A watch shot (DESIGN.md 13.17) killed the mover before it acted.
                    break;
                }
            }
        }

        if (!working.Outcome.IsOver)
        {
            plan.Add(new EndPhase());
        }

        return (ValueList<Command>.From(plan), ValueList<AnvilPlan>.From(anvils));
    }

    /// <summary>
    /// The enemy that acts next among <paramref name="order"/>, the ids not yet planned in
    /// ascending order: the first of them, unless the map has the <c>pincer: on</c> header and
    /// one of them is an anvil (<see cref="Anvil"/>, issue 419), in which case the first anvil
    /// by id acts ahead of the rest with its anvil plan. Without the header, or with no anvil
    /// on the board, the order is ascending id, exactly as before issue 419.
    /// </summary>
    private static (string Id, AnvilPlan? Anvil) Next(BattleState state, GameContent content, IReadOnlyList<string> order, IReadOnlySet<string> claimed)
    {
        if (state.Map.PincerEnabled)
        {
            foreach (var id in order)
            {
                if (state.Find(id) is { } unit && Anvil(state, content, unit, claimed) is { } anvil)
                {
                    return (id, anvil);
                }
            }
        }

        return (order[0], null);
    }

    /// <summary>
    /// An enemy's plan to step in as the anvil of a pin (DESIGN.md 13.13, issue 419): the tile it
    /// moves to, the player unit that tile pins, the group-mate promised the pinned strike, the
    /// bonus that strike gains, and the commands (a Move when the tile is not its own, then its
    /// best strike from that tile if it has one, else a Wait).
    /// </summary>
    public sealed record AnvilPlan(Coord Tile, string PinnedId, string FollowerId, double Bonus, ValueList<Command> Commands);

    /// <summary>
    /// The pincer's planner arm (DESIGN.md 13.13, issue 419), on a <c>pincer: on</c> map only.
    /// For each tile T the unit may end on and each player unit U it knows of beside T, a
    /// follower is a group-mate that has not acted, is not a boss and is not already claimed,
    /// Aggressive and unmoved, not retreating, with an equipped weapon that strikes at range 1,
    /// that can end on the tile across U from T with the unit already standing on T and see U
    /// from there. The bonus is the follower's score on U from that tile pinned minus the same
    /// score with the unit off the board: the +15 hit on its expected damage, since the kill
    /// flag reads deterministic damage. The anvil's value on T is the best bonus there plus the
    /// score of its own best strike from T, if any; the exposure of T is not priced (an anvil
    /// left beside the party is the player's punish). The plan is the tile of highest value,
    /// first in reach order on a tie, and it is taken only when that value beats the unit's
    /// best strike by today's score. Null for an anchored unit (a boss, or a throne-holder),
    /// one already claimed as a follower, one sworn against a unit it knows of, one that is not
    /// Aggressive, has moved, retreats or is unarmed, one without a group, and when no tile
    /// has a follower.
    /// </summary>
    public static AnvilPlan? Anvil(BattleState state, GameContent content, BattleUnit unit, IReadOnlySet<string> claimed)
    {
        if (!state.Map.PincerEnabled || unit.Side != Side.Enemy || unit.Acted || unit.Moved || unit.IsBoss
            || unit.Group is null || claimed.Contains(unit.Id) || HoldsTheThrone(state, unit)
            || state.EffectiveBehavior(unit, content) != Behavior.Aggressive || unit.EquippedWeapon(content) is null
            || RetreatRule.Choose(state, content, unit) is not null || Messenger.Is(state, unit))
        {
            return null;
        }

        var players = state.UnitsOf(Side.Player).ToList();
        var known = Hunt.Narrow(state, unit, players.Where(p => Dusk.Knows(state, content, unit, p)).ToList());
        if (known.Count == 0 || Sworn(state, unit, known) is not null)
        {
            return null;
        }

        var followers = state.UnitsOf(Side.Enemy)
            .Where(f => f.Id != unit.Id && f.Group == unit.Group && !f.Acted && !f.Moved && !f.IsBoss && !claimed.Contains(f.Id)
                && state.EffectiveBehavior(f, content) == Behavior.Aggressive
                && f.EquippedWeapon(content) is { } weapon && weapon.InRange(1)
                && RetreatRule.Choose(state, content, f) is null && !Messenger.Is(state, f))
            .ToList();
        if (followers.Count == 0)
        {
            return null;
        }

        var playerReach = players.Select(p => state.ReachOf(p, content)).ToList();
        var reach = state.ReachOf(unit, content);
        var tiles = reach.Destinations.ToList();
        var bestStrike = Choose(state, content, unit, tiles, reach, known, playerReach, null).Best;
        var chosenValue = bestStrike?.Score ?? double.NegativeInfinity;
        AnvilPlan? chosen = null;
        foreach (var tile in tiles)
        {
            BattleState? board = null;
            BattleState? without = null;
            var bonus = 0.0;
            BattleUnit? pinned = null;
            BattleUnit? follower = null;
            foreach (var target in known)
            {
                if (tile.DistanceTo(target.At) != 1)
                {
                    continue;
                }

                var far = new Coord(2 * target.At.X - tile.X, 2 * target.At.Y - tile.Y);
                if (!state.Map.Contains(far))
                {
                    continue;
                }

                board ??= state.WithUnit(unit with { At = tile });
                without ??= board.WithoutUnit(unit.Id);
                foreach (var candidate in followers)
                {
                    if (candidate.At.DistanceTo(far) > content.Class(candidate.Unit.ClassId).Mov
                        || !board.ReachOf(candidate, content).CanEnd(far)
                        || !Dusk.Sees(board, Side.Enemy, target.At, candidate.Id, far))
                    {
                        continue;
                    }

                    var gain = Score(board, content, candidate, far, target) - Score(without, content, candidate, far, target);
                    if (gain > bonus)
                    {
                        bonus = gain;
                        pinned = target;
                        follower = candidate;
                    }
                }
            }

            if (follower is null)
            {
                continue;
            }

            var strike = BestOption(state, content, unit, new[] { tile }, reach, known, playerReach);
            var value = bonus + (strike?.Score ?? 0);
            if (value > chosenValue)
            {
                chosenValue = value;
                var equipped = unit.EquippedSlot(content);
                var commands = new List<Command>();
                if (tile != unit.At)
                {
                    commands.Add(new Move(unit.Id, tile));
                }

                commands.Add(strike is null ? new Wait(unit.Id) : new Attack(unit.Id, strike.TargetId, strike.Slot == equipped ? null : strike.Slot));
                chosen = new AnvilPlan(tile, pinned!.Id, follower.Id, bonus, ValueList<Command>.From(commands));
            }
        }

        return chosen;
    }

    /// <summary>
    /// One enemy's commands on the board as it stands: a <see cref="Retreat"/> first when
    /// <see cref="RetreatRule"/> says the unit falls back (issue 33); else the best-scoring attack from the
    /// best tile if any tile allows one, else the approach rule for an Aggressive unit
    /// not holding a Seize throne (<see cref="HoldsTheThrone"/>, issue 321), else Wait. Hold, Boss, and a sleeping Guard never move; a woken Guard is Aggressive.
    /// On a dusk map (DESIGN.md 13.7, issue 302) both the attack and the approach range only
    /// over the player units the enemy knows of (<see cref="Dusk.Knows"/>): those its side
    /// sees and those within hearing of it. One that knows of nobody and may move makes for
    /// the objective (<see cref="Drift"/>, issue 308); a unit that holds keeps its hold.
    /// On a <c>grudges: on</c> map a unit sworn against a player unit it knows of strikes that
    /// unit whenever it can and approaches it first (<see cref="Sworn"/>, DESIGN.md 13.4),
    /// unless it can strike with a keepsake: the keepsake outranks the grudge (issue 331).
    /// The attack options range over every weapon the unit can strike with, in inventory
    /// order; an Attack with a weapon other than the equipped one names its slot, which
    /// moves it to the front so the counter that follows uses it too (section 5, issue 99).
    /// A boss under the veto (<see cref="BossVetoApplies"/>) whose every strike was refused
    /// moves to the approach tile the veto passes, or stays, and then strikes the best target
    /// in reach of that tile unvetoed: the veto picks the tile, not the swing (issue 389).
    /// A guard boss with a post goes home instead (<see cref="GoesHome"/>, issue 393).
    /// </summary>
    public static IReadOnlyList<Command> PlanUnit(BattleState state, GameContent content, BattleUnit unit)
    {
        var behavior = state.EffectiveBehavior(unit, content)
            ?? throw new ArgumentException($"{unit.Id} is a player unit and has no behavior", nameof(unit));
        var weapon = unit.EquippedWeapon(content);
        var mayMove = behavior == Behavior.Aggressive && !unit.Moved;
        var reach = state.ReachOf(unit, content);
        var tiles = mayMove ? reach.Destinations.ToList() : new List<Coord> { unit.At };
        var players = state.UnitsOf(Side.Player).ToList();
        var playerReach = players.Select(p => state.ReachOf(p, content)).ToList();
        var known = Hunt.Narrow(state, unit, players.Where(p => Dusk.Knows(state, content, unit, p)).ToList());

        if (RetreatRule.Choose(state, content, unit) is { } refuge)
        {
            return new Command[] { new Retreat(unit.Id, refuge) };
        }

        if (Messenger.Is(state, unit))
        {
            var run = Messenger.Runs(state, content, unit) ? Run(state, content, unit, reach, playerReach) : null;
            return run is { } to && to != unit.At
                ? new Command[] { new Move(unit.Id, to), new Wait(unit.Id) }
                : new Command[] { new Wait(unit.Id) };
        }

        if (weapon is null)
        {
            return new Command[] { new Wait(unit.Id) };
        }

        var equipped = unit.EquippedSlot(content);
        var sworn = Sworn(state, unit, known);
        var best = Choose(state, content, unit, tiles, reach, known, playerReach, sworn).Best;
        if (best is not null)
        {
            var attack = new Attack(unit.Id, best.TargetId, best.Slot == equipped ? null : best.Slot);
            return best.Tile == unit.At
                ? new Command[] { attack }
                : new Command[] { new Move(unit.Id, best.Tile), attack };
        }

        if (!mayMove || HoldsTheThrone(state, unit))
        {
            return new Command[] { Idle(state, content, unit) };
        }

        var veto = BossVetoApplies(state, content, unit);
        var end = End(state, content, unit, weapon, tiles, reach, known, playerReach, sworn, veto);
        var swing = veto ? Choose(state, content, unit, new[] { end }, reach, known, playerReach, sworn, unvetoed: true).Best : null;
        var last = swing is null ? Idle(state, content, unit) : new Attack(unit.Id, swing.TargetId, swing.Slot == equipped ? null : swing.Slot);
        return end != unit.At
            ? new Command[] { new Move(unit.Id, end), last }
            : new Command[] { last };
    }

    /// <summary>
    /// What an enemy with no strike this phase does with its action: Watch under
    /// <see cref="Overwatch.EnemyWatches"/> (DESIGN.md 13.17), else Wait.
    /// </summary>
    private static Command Idle(BattleState state, GameContent content, BattleUnit unit) =>
        Overwatch.EnemyWatches(state, content, unit) ? new Watch(unit.Id) : new Wait(unit.Id);

    /// <summary>
    /// Where a mover with no strike this phase ends: its post when <see cref="GoesHome"/> says
    /// so (issue 393), else <see cref="Destination"/>, else where it stands; a member of a
    /// <c>holds:</c> group ends on its ground (<see cref="HeldEnd"/>, issue 1189). <see cref="PlanUnit"/>
    /// and <c>threat</c>'s swing read this one function, so they agree on the tile.
    /// </summary>
    private static Coord End(
        BattleState state, GameContent content, BattleUnit unit, Weapon weapon, IReadOnlyList<Coord> tiles, Reach reach,
        IReadOnlyList<BattleUnit> known, IReadOnlyList<Reach> playerReach, BattleUnit? sworn, bool veto, bool inDaylight = false) =>
        veto && GoesHome(state, content, unit, tiles, reach, known, playerReach, sworn, inDaylight) is { } home
            ? home
            : state.Map.Holds is { } held && held.Binds(unit)
                ? HeldEnd(state, content, unit, weapon, reach, known, playerReach, sworn, veto, held)
                : Destination(state, content, unit, weapon, reach, known, playerReach, sworn, veto) ?? unit.At;

    /// <summary>
    /// A held group holds its ground (issue 1189, round 402; DESIGN.md section 8): a member of the
    /// <c>holds:</c> group with no strike this phase approaches only over tiles of its ground, so
    /// a woken group waits on the edge of its ground instead of marching out; standing off it, after
    /// a strike took it there, it walks back, ending on the reachable tile of its ground nearest the
    /// unit it would approach, or, with none in reach, on the reachable tile nearest its ground (then
    /// the lower movement cost, then the reach's order). A strike is still taken from any tile it can
    /// reach, so the rule decides where it waits, never what it answers.
    /// </summary>
    private static Coord HeldEnd(
        BattleState state, GameContent content, BattleUnit unit, Weapon weapon, Reach reach,
        IReadOnlyList<BattleUnit> known, IReadOnlyList<Reach> playerReach, BattleUnit? sworn, bool veto, HeldGround held)
    {
        if (Destination(state, content, unit, weapon, reach, known, playerReach, sworn, veto, held) is { } inside && held.Contains(inside))
        {
            return inside;
        }

        if (held.Contains(unit.At))
        {
            return unit.At;
        }

        return reach.Destinations
            .Select((tile, order) => (tile, order))
            .OrderBy(t => held.DistanceTo(t.tile))
            .ThenBy(t => reach.CostTo(t.tile) ?? int.MaxValue)
            .ThenBy(t => t.order)
            .First().tile;
    }

    /// <summary>
    /// A guard boss goes home (issue 393, DESIGN.md section 8): when the boss veto refused a strike
    /// it could otherwise make this phase, a boss whose map behavior is Guard and that has a post
    /// (<see cref="Post"/>) ends on the reachable tile nearest its post, by Manhattan distance,
    /// then its own tile, then the lower movement cost, then the reach's order; that tile is not
    /// vetoed, and the swing of issue 389 follows from it. With no strike in reach at all, a guard
    /// boss standing on its post holds there (issue 1138): the vetoed approach looks one phase
    /// ahead and the refusal another, so stepping off on a tile safe now sent him home the next
    /// phase, a shuttle with a heal on every return. Null when the rule does not apply: not a
    /// guard boss, no post, or no strike in reach while off the post, in which case it approaches
    /// as any woken guard does. So a boss the gathered party outweighs holds the ground it guards
    /// instead of running from it.
    /// </summary>
    public static Coord? GoesHome(
        BattleState state, GameContent content, BattleUnit unit, IReadOnlyList<Coord> tiles, Reach reach,
        IReadOnlyList<BattleUnit> known, IReadOnlyList<Reach> playerReach, BattleUnit? sworn, bool inDaylight = false)
    {
        if (unit is not { IsBoss: true, Behavior: Behavior.Guard } || Post(state, unit) is not { } post)
        {
            return null;
        }

        if (Choose(state, content, unit, tiles, reach, known, playerReach, sworn, inDaylight, unvetoed: true).Best is null)
        {
            return unit.At == post ? post : null;
        }

        return reach.Destinations
            .Select((tile, order) => (tile, order))
            .OrderBy(t => t.tile.DistanceTo(post))
            .ThenBy(t => t.tile == unit.At ? 0 : 1)
            .ThenBy(t => reach.CostTo(t.tile) ?? int.MaxValue)
            .ThenBy(t => t.order)
            .First().tile;
    }

    /// <summary>
    /// An enemy's post: the tile of the map placement it filled, where it stood when the battle
    /// began. Null for a unit a map event spawned, which has no placement, and for a player unit.
    /// </summary>
    public static Coord? Post(BattleState state, BattleUnit unit) =>
        unit.PlacementIndex >= 0 && unit.PlacementIndex < state.Map.Placements.Count
            && state.Map.Placements[unit.PlacementIndex] is EnemyPlacement placement
            ? placement.At
            : null;

    /// <summary>
    /// Where a mover with no strike this phase ends: the objective drift on a dusk map when it
    /// knows of nobody, else the approach toward the unit it is sworn against, else toward the
    /// nearest it knows of; null means it stays. Under the boss veto (<paramref name="veto"/>)
    /// a refused tile is no destination, and so is a tile off the <paramref name="held"/> ground.
    /// </summary>
    private static Coord? Destination(
        BattleState state, GameContent content, BattleUnit unit, Weapon weapon, Reach reach,
        IReadOnlyList<BattleUnit> known, IReadOnlyList<Reach> playerReach, BattleUnit? sworn, bool veto, HeldGround? held = null)
    {
        Func<Coord, bool>? refused = veto ? tile => BossVetoRefuses(state, content, unit, tile) : null;
        if (held is not null)
        {
            var vetoed = refused;
            refused = tile => !held.Contains(tile) || (vetoed?.Invoke(tile) ?? false);
        }
        if (Routes.CrossingFor(state, unit) is { } crossing
            && MarchTo(state, content, unit, new[] { crossing }, reach, playerReach, refused) is { } drifting)
        {
            return drifting;
        }

        if (Hunt.Is(state, unit) && Hunt.Hunted(state) is { } front)
        {
            return (known.Count == 0 ? null : Approach(state, content, unit, weapon, reach, known, playerReach, refused))
                ?? March(state, content, unit, front, reach, playerReach, refused);
        }

        return known.Count == 0 && Dusk.Sight(state) is not null
            ? Drift(state, content, unit, reach, playerReach, refused)
            : (sworn is null ? null : Approach(state, content, unit, weapon, reach, new[] { sworn }, playerReach, refused))
                ?? Approach(state, content, unit, weapon, reach, known, playerReach, refused);
    }

    /// <summary>
    /// The throne-holder rule (issue 321, DESIGN.md section 8): an enemy standing on a
    /// Seize map's throne at the start of its action leaves it only to strike this phase.
    /// <see cref="PlanUnit"/> reads it after the attack options, so a holder with a strike
    /// from any reachable tile still moves and strikes, and one without Waits on the throne
    /// instead of approaching or drifting. Strikes are untouched, so <c>threat</c> and the
    /// exposure sum, which count only strikes, already agree with it.
    /// </summary>
    public static bool HoldsTheThrone(BattleState state, BattleUnit unit) =>
        unit.Side == Side.Enemy && state.Map.Win == WinCondition.Seize && state.Map.IsThrone(unit.At);

    /// <summary>
    /// The boss veto (issue 385, DESIGN.md section 8): on a Defeat Boss map the boss is the
    /// enemy's captain, and it plans under the rule the Sim's captain plays by (section 11).
    /// It applies while the boss may choose where it ends, Aggressive and not yet moved; a
    /// boss that holds keeps striking from its own tile.
    /// </summary>
    public static bool BossVetoApplies(BattleState state, GameContent content, BattleUnit unit) =>
        unit.Side == Side.Enemy && unit.IsBoss && state.Map.Win == WinCondition.DefeatBoss
        && !unit.Moved && state.EffectiveBehavior(unit, content) == Behavior.Aggressive;

    /// <summary>
    /// Whether the boss veto refuses <paramref name="unit"/> ending on <paramref name="tile"/>,
    /// after attacking <paramref name="target"/> with the weapon in <paramref name="slot"/> when
    /// one is named: the no-crit exposure sum there (<see cref="Exposure.OfBoss"/>, the counter it
    /// takes included) reaches its current HP. A refused strike is no option and a refused
    /// tile no approach, so with every tile refused the boss holds. The sum is arithmetic, so
    /// a party too thin to kill it on a tile leaves that tile open.
    /// </summary>
    public static bool BossVetoRefuses(BattleState state, GameContent content, BattleUnit unit, Coord tile, BattleUnit? target = null, int? slot = null) =>
        Exposure.OfBoss(state, content, unit, tile, target, slot) >= unit.Hp;

    /// <summary>
    /// The attack <paramref name="unit"/> would make on <paramref name="target"/> if the
    /// planner chose that target on the board as it stands (issue 217): the best tile and
    /// weapon slot among its options against that one unit, by <see cref="PlanUnit"/>'s
    /// own score and order, so when the planner's best option is this target the strike
    /// named here is the strike that comes. Null when the unit would retreat, has no
    /// weapon, or has no option against the target; a unit that holds strikes only from
    /// its own tile, and one that has moved only from where it stands. On a dusk map a target
    /// the unit does not know of (<see cref="Dusk.Knows"/>) is no option; given
    /// <paramref name="inDaylight"/>, the dark is ignored, both that and the sight a strike
    /// needs, which is how <c>threat</c> names an enemy the dark alone keeps off the unit.
    /// </summary>
    public static EnemyStrike? StrikeOn(BattleState state, GameContent content, BattleUnit unit, BattleUnit target, bool inDaylight = false)
    {
        var behavior = state.EffectiveBehavior(unit, content)
            ?? throw new ArgumentException($"{unit.Id} is a player unit and has no behavior", nameof(unit));
        if (unit.EquippedWeapon(content) is null || RetreatRule.Choose(state, content, unit) is not null || Messenger.Is(state, unit))
        {
            return null;
        }

        if ((!inDaylight && !Dusk.Knows(state, content, unit, target)) || Hunt.Spares(state, unit, target))
        {
            return null;
        }

        var reach = state.ReachOf(unit, content);
        var tiles = behavior == Behavior.Aggressive && !unit.Moved ? reach.Destinations.ToList() : new List<Coord> { unit.At };
        var players = state.UnitsOf(Side.Player).ToList();
        var playerReach = players.Select(p => state.ReachOf(p, content)).ToList();
        var known = Hunt.Narrow(state, unit, players.Where(p => inDaylight || Dusk.Knows(state, content, unit, p)).ToList());
        if (Sworn(state, unit, known) is { } sworn)
        {
            var chosen = Choose(state, content, unit, tiles, reach, known, playerReach, sworn, inDaylight);
            if (chosen.Keepsake)
            {
                var onTarget = target.Id == sworn.Id || chosen.Best!.TargetId != sworn.Id
                    ? BestOption(state, content, unit, tiles, reach, new[] { target }, playerReach, inDaylight, keepsakesOnly: true)
                    : null;
                return onTarget is null ? null : new EnemyStrike(onTarget.Tile, onTarget.Slot);
            }

            if (chosen.Best is { } swornStrike && swornStrike.TargetId == sworn.Id && sworn.Id != target.Id)
            {
                return null;
            }
        }

        var best = BestOption(state, content, unit, tiles, reach, new[] { target }, playerReach, inDaylight);
        if (best is null && BossVetoApplies(state, content, unit))
        {
            best = SwingFromEnd(state, content, unit, tiles, reach, known, playerReach, target, inDaylight);
        }

        return best is null ? null : new EnemyStrike(best.Tile, best.Slot);
    }

    /// <summary>
    /// Why a boss under the veto does not strike <paramref name="target"/> this phase, when the
    /// veto is the reason (issue 565, DESIGN.md section 8): null unless <see cref="BossVetoApplies"/>,
    /// <see cref="StrikeOn"/> names no strike on the target, the boss has a strike on it from
    /// some reachable tile once the veto is lifted, and the veto refuses every one of them. Then
    /// the refused striking tile nearest the boss, by movement cost and then the reach's order,
    /// and the tile <see cref="PlanUnit"/> ends it on, read from the same plan the enemy phase
    /// runs. Unpriced: it names the rule, not the numbers. On a dusk map a target the boss does
    /// not know of is no refusal, as it is no strike.
    /// </summary>
    public static VetoRefusal? Refusal(BattleState state, GameContent content, BattleUnit unit, BattleUnit target)
    {
        if (!BossVetoApplies(state, content, unit) || unit.EquippedWeapon(content) is null
            || RetreatRule.Choose(state, content, unit) is not null || Messenger.Is(state, unit) || !Dusk.Knows(state, content, unit, target)
            || Hunt.Spares(state, unit, target) || StrikeOn(state, content, unit, target) is not null)
        {
            return null;
        }

        var reach = state.ReachOf(unit, content);
        var tiles = reach.Destinations.ToList();
        var playerReach = state.UnitsOf(Side.Player).Select(p => state.ReachOf(p, content)).ToList();
        var targets = new[] { target };
        if (BestOption(state, content, unit, tiles, reach, targets, playerReach) is not null
            || BestOption(state, content, unit, tiles, reach, targets, playerReach, unvetoed: true) is null)
        {
            return null;
        }

        var refused = tiles
            .Select((tile, order) => (tile, order))
            .Where(t => Arms(content, state.Carrying(unit, t.tile)).Any(arm =>
                arm.Weapon.InRange(t.tile.DistanceTo(target.At))
                && Dusk.Sees(state, unit.Side, target.At, unit.Id, t.tile)
                && BossVetoRefuses(state, content, state.Carrying(unit, t.tile), t.tile, target, arm.Slot)))
            .OrderBy(t => reach.CostTo(t.tile) ?? int.MaxValue)
            .ThenBy(t => t.order)
            .Select(t => (Coord?)t.tile)
            .FirstOrDefault();
        if (refused is not { } tile)
        {
            return null;
        }

        var ends = PlanUnit(state, content, unit).OfType<Move>().Select(m => m.To).DefaultIfEmpty(unit.At).Single();
        return new VetoRefusal(tile, ends);
    }

    /// <summary>
    /// The strike on <paramref name="target"/> a boss under the veto makes from the tile it
    /// ends on when every strike was refused (issue 389): null unless the veto left it no
    /// strike at all, and then the swing <see cref="PlanUnit"/> takes from its end tile
    /// (<see cref="End"/>: its post for a guard boss, else its approach tile or its own), on this target, or the best on this target from
    /// there when the swing goes to someone else by score alone.
    /// </summary>
    private static AttackOption? SwingFromEnd(
        BattleState state, GameContent content, BattleUnit unit, IReadOnlyList<Coord> tiles, Reach reach,
        IReadOnlyList<BattleUnit> known, IReadOnlyList<Reach> playerReach, BattleUnit target, bool inDaylight)
    {
        var sworn = Sworn(state, unit, known);
        if (Choose(state, content, unit, tiles, reach, known, playerReach, sworn, inDaylight).Best is not null)
        {
            return null;
        }

        var end = new[] { End(state, content, unit, unit.EquippedWeapon(content)!, tiles, reach, known, playerReach, sworn, veto: true, inDaylight) };
        var swing = Choose(state, content, unit, end, reach, known, playerReach, sworn, inDaylight, unvetoed: true);
        if (swing.Best is not { } strike)
        {
            return null;
        }

        if (strike.TargetId == target.Id)
        {
            return strike;
        }

        return swing.Keepsake || strike.TargetId == sworn?.Id
            ? null
            : BestOption(state, content, unit, end, reach, new[] { target }, playerReach, inDaylight, unvetoed: true);
    }

    /// <summary>
    /// A grudge strike and what it cost the planner (issue 331): the sworn unit struck and the
    /// planner's score for that strike, beside the best strike the unit had on anyone else
    /// and its score, both null when the sworn unit was the only one in reach. Sim traces
    /// and the console print it beside the strike, so whether a grudge redirected an enemy
    /// reads off a log.
    /// </summary>
    public sealed record GrudgeStrike(string SwornId, double Score, string? AlternativeId, double? AlternativeScore)
    {
        /// <summary>
        /// The log line for <paramref name="unitId"/>'s grudge strike, plain ASCII, scores to one
        /// decimal; units by the names a reader sees when <paramref name="names"/> is given (issue 615).
        /// </summary>
        public string Line(string unitId, UnitNames? names = null) =>
            FormattableString.Invariant($"grudge: {(names ?? UnitNames.None)[unitId]} strikes sworn {(names ?? UnitNames.None)[SwornId]} (score {Score:0.0}); ")
            + (AlternativeId is { } other
                ? FormattableString.Invariant($"best alternative {(names ?? UnitNames.None)[other]} (score {AlternativeScore:0.0})")
                : "no other strike in reach");
    }

    /// <summary>
    /// The grudge line to print beside <paramref name="command"/> in an enemy phase (issue 331):
    /// asked on the board before the unit's first command, so a Move and the Attack after it
    /// share one answer. <paramref name="pending"/> holds the answer between the two.
    /// Returns the line when the command is the grudge strike, else null; units by the names a
    /// reader sees when <paramref name="names"/> is given (issue 615), by id for a script's comment.
    /// </summary>
    public static string? GrudgeLog(BattleState state, GameContent content, Command command, Dictionary<string, GrudgeStrike?> pending, UnitNames? names = null)
    {
        var id = command switch { Move m => m.UnitId, Attack a => a.UnitId, _ => null };
        if (id is null || state.Find(id) is not { Side: Side.Enemy } unit || unit.Grudge is null)
        {
            return null;
        }

        if (!unit.Moved || !pending.ContainsKey(id))
        {
            pending[id] = GrudgeChoice(state, content, unit);
        }

        return command is Attack attack && pending[id] is { } choice && choice.SwornId == attack.TargetId ? choice.Line(id, names) : null;
    }

    /// <summary>
    /// The grudge strike <see cref="PlanUnit"/> makes with <paramref name="unit"/> on the board
    /// as it stands, or null when its plan is not a strike on the unit it is sworn against
    /// for the grudge's sake: it is sworn on nobody it knows, it retreats, it has no weapon,
    /// it cannot reach the sworn unit, or a keepsake strike outranks the grudge.
    /// </summary>
    public static GrudgeStrike? GrudgeChoice(BattleState state, GameContent content, BattleUnit unit)
    {
        if (unit.Side != Side.Enemy || unit.Grudge is null || unit.EquippedWeapon(content) is null || RetreatRule.Choose(state, content, unit) is not null || Messenger.Is(state, unit))
        {
            return null;
        }

        var behavior = state.EffectiveBehavior(unit, content);
        var reach = state.ReachOf(unit, content);
        var tiles = behavior == Behavior.Aggressive && !unit.Moved ? reach.Destinations.ToList() : new List<Coord> { unit.At };
        var players = state.UnitsOf(Side.Player).ToList();
        var playerReach = players.Select(p => state.ReachOf(p, content)).ToList();
        var known = Hunt.Narrow(state, unit, players.Where(p => Dusk.Knows(state, content, unit, p)).ToList());
        if (Sworn(state, unit, known) is not { } sworn)
        {
            return null;
        }

        var chosen = Choose(state, content, unit, tiles, reach, known, playerReach, sworn);
        if (chosen.Keepsake || chosen.Best is not { } strike || strike.TargetId != sworn.Id)
        {
            return null;
        }

        var others = known.Where(p => p.Id != sworn.Id).ToList();
        var alternative = BestOption(state, content, unit, tiles, reach, others, playerReach);
        return new GrudgeStrike(sworn.Id, strike.Score, alternative?.TargetId, alternative?.Score);
    }

    /// <summary>
    /// The strike the planner takes among <paramref name="known"/>, in precedence order (issue 331):
    /// a keepsake strike on the sworn unit, then any keepsake strike, then any strike on the
    /// sworn unit, then the best strike by score. The keepsake tiers apply only to a sworn unit,
    /// so an unsworn unit's choice is the score's alone, as before the grudge arm.
    /// <c>Keepsake</c> is true when a keepsake tier chose.
    /// </summary>
    private static (AttackOption? Best, bool Keepsake) Choose(
        BattleState state, GameContent content, BattleUnit unit, IReadOnlyList<Coord> tiles, Reach reach,
        IReadOnlyList<BattleUnit> known, IReadOnlyList<Reach> playerReach, BattleUnit? sworn, bool inDaylight = false, bool unvetoed = false)
    {
        if (sworn is not null)
        {
            var keepsake = BestOption(state, content, unit, tiles, reach, new[] { sworn }, playerReach, inDaylight, keepsakesOnly: true, unvetoed)
                ?? BestOption(state, content, unit, tiles, reach, known, playerReach, inDaylight, keepsakesOnly: true, unvetoed);
            if (keepsake is not null)
            {
                return (keepsake, true);
            }

            if (BestOption(state, content, unit, tiles, reach, new[] { sworn }, playerReach, inDaylight, unvetoed: unvetoed) is { } grudge)
            {
                return (grudge, false);
            }
        }

        return (BestOption(state, content, unit, tiles, reach, known, playerReach, inDaylight, unvetoed: unvetoed), false);
    }

    /// <summary>
    /// The player unit <paramref name="unit"/> is sworn against (DESIGN.md 13.4, experiment), if it
    /// is among <paramref name="candidates"/>; null otherwise. <see cref="PlanUnit"/> strikes it
    /// whenever any tile and weapon reach it, whatever the score says, takes its best other strike
    /// only when none does, and approaches it before anyone else; <see cref="StrikeOn"/> names no
    /// strike on another unit while the sworn one is in reach, so <c>threat</c> agrees.
    /// </summary>
    public static BattleUnit? Sworn(BattleState state, BattleUnit unit, IEnumerable<BattleUnit> candidates) =>
        unit.Grudge is { } id ? candidates.FirstOrDefault(p => p.Id == id) : null;

    /// <summary>
    /// The best-scoring attack option over <paramref name="tiles"/>, every usable weapon in
    /// inventory order, and <paramref name="targets"/>, by <see cref="AttackOption.Beats"/>;
    /// null when no weapon reaches any target from any tile. The tile's exposure counts
    /// every player unit through <paramref name="playerReach"/>, whichever targets are asked.
    /// On a dusk map a target the unit's side cannot see from where it would strike is no
    /// option (DESIGN.md 13.7), the same rule the resolver holds, unless <paramref name="inDaylight"/>.
    /// Given <paramref name="keepsakesOnly"/>, only strikes with a keepsake count (issue 331).
    /// A strike the boss veto refuses is no option (<see cref="BossVetoRefuses"/>, issue 385),
    /// checked only for an option that would beat the best so far, unless <paramref name="unvetoed"/>:
    /// the swing from a boss's chosen end tile is not vetoed (issue 389).
    /// </summary>
    private static AttackOption? BestOption(
        BattleState state, GameContent content, BattleUnit unit, IReadOnlyList<Coord> tiles, Reach reach,
        IReadOnlyList<BattleUnit> targets, IReadOnlyList<Reach> playerReach, bool inDaylight = false, bool keepsakesOnly = false, bool unvetoed = false)
    {
        var movement = content.Class(unit.Unit.ClassId).Movement;
        var own = Arms(content, unit);
        var veto = !unvetoed && BossVetoApplies(state, content, unit);
        var scorched = Avoided(state);
        AttackOption? best = null;
        foreach (var tile in tiles)
        {
            var carrier = state.Carrying(unit, tile);
            var arms = ReferenceEquals(carrier, unit) ? own : Arms(content, carrier);
            var avoid = state.Map.TerrainAt(tile, content).AvoidFor(movement);
            var exposure = playerReach.Count(r => r.CanEnd(tile));
            var cost = reach.CostTo(tile)!.Value;
            foreach (var arm in arms)
            {
                if (keepsakesOnly && carrier.Unit.Inventory.Items[arm.Slot].Keepsake is null)
                {
                    continue;
                }

                foreach (var target in targets)
                {
                    if (!arm.Weapon.InRange(tile.DistanceTo(target.At)) || (!inDaylight && !Dusk.Sees(state, unit.Side, target.At, unit.Id, tile)))
                    {
                        continue;
                    }

                    var option = new AttackOption(Score(state, content, arm.Armed, tile, target), target.Id, tile, avoid, exposure, cost, arm.Slot, scorched.Contains(tile));
                    if ((best is null || option.Beats(best)) && !(veto && BossVetoRefuses(state, content, carrier, tile, target, arm.Slot)))
                    {
                        best = option;
                    }
                }
            }
        }

        return best;
    }

    /// <summary>
    /// The weapons <paramref name="carrier"/> may strike with, by slot in inventory order, each
    /// with the unit as it strikes (that slot moved to the front). <see cref="BestOption"/>
    /// reads a unit as it would be on each tile, holding the keepsakes it would take there
    /// (<see cref="BattleState.Carrying"/>). A carrier strikes with a keepsake whenever it can
    /// wield one (DESIGN.md 13.8, issue 295): the grudge overrides the score, so when any
    /// keepsake is usable only keepsakes are offered; one it cannot wield it only carries.
    /// </summary>
    private static List<(int Slot, Weapon Weapon, BattleUnit Armed)> Arms(GameContent content, BattleUnit carrier)
    {
        var arms = Enumerable.Range(0, carrier.Unit.Inventory.Count)
            .Where(slot => carrier.UsableWeaponAt(content, slot) is not null)
            .Select(slot => (Slot: slot, Weapon: carrier.UsableWeaponAt(content, slot)!, Armed: carrier.WithSlotInFront(slot)))
            .ToList();
        var grudges = arms.Where(arm => carrier.Unit.Inventory.Items[arm.Slot].Keepsake is not null).ToList();
        return grudges.Count > 0 ? grudges : arms;
    }

    /// <summary>
    /// Section 8's target score for <paramref name="attacker"/> striking <paramref name="target"/>
    /// from <paramref name="from"/>. Expected damage on both lines carries the crit
    /// expectation, <c>Damage * (1 + 2 * CritChance / 100)</c> (plain <c>Damage</c> when the crit grounds instead, issue 723), times the strikes that side
    /// makes, capped at the HP it could remove; the kill flag reads deterministic damage
    /// only, so an attack lethal only on a crit is never priced as a kill, and only over the
    /// strikes the attacker lives to make, so a second strike a plain counter would kill it
    /// before is not counted (<see cref="CombatForecast.AttackerDamageLivedFor"/>, issue 315). The attacker
    /// strikes with its equipped weapon; <see cref="PlanUnit"/> scores another slot by
    /// passing the unit with that slot moved to the front. A strike on an ally a cover would
    /// swap out (DESIGN.md 13.19) is scored against the coverer on the ally's tile
    /// (<see cref="CoverRule.Swapped"/>), so the planner prices the swap.
    /// </summary>
    public static double Score(BattleState state, GameContent content, BattleUnit attacker, Coord from, BattleUnit target)
    {
        if (CoverRule.Swapped(state, target) is ({ } covered, { } coverer, _))
        {
            state = covered;
            target = coverer;
        }

        var weapon = attacker.EquippedWeapon(content)
            ?? throw new ArgumentException($"{attacker.Id} has no weapon to score with", nameof(attacker));
        var there = attacker with { At = from };
        var me = content.CombatantOf(attacker.Unit, Grounding.ForMap(state.Map, weapon), state.Map.TerrainAt(from, content), attacker.Hp, hitModifier: Brace.StrikeHit(state, there, target) + Signatures.StrikeHit(state, content, there, countering: false), beside: Formation.Beside(state, content, there)) with { Aura = Formation.Aura(state, content, there) };
        var them = target.Answering(state, content, from, there);
        var forecast = Combat.Forecast(me, them, from.DistanceTo(target.At), state.Scheme);

        var strikes = forecast.Attacker.StrikeCount;
        var canKill = forecast.AttackerDamageLivedFor(attacker.Hp) >= target.Hp;
        var dealt = Math.Min(target.Hp, Expected(forecast.Attacker, strikes));
        var score = (canKill ? KillBonus : 0) + dealt * Combat.HitProbability(forecast.Attacker.HitChance, state.Scheme);

        if (forecast.Defender.Strikes)
        {
            var counterStrikes = forecast.Defender.StrikeCount;
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
            if (content.Weapons.TryGetValue(item.ItemId, out var weapon) && weapon.Heals && unit.Unit.CanWield(weapon, unitClass))
            {
                return true;
            }
        }

        return false;
    }

    private static double Expected(SideForecast side, int strikes) => side.CritGrounds ? side.Damage * strikes : side.Damage * (1 + 2 * side.CritChance / 100.0) * strikes;

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
        IReadOnlyList<BattleUnit> players, IReadOnlyList<Reach> playerReach, Func<Coord, bool>? refused = null)
    {
        var movement = Grounding.MovementOf(unit, content);
        Occupant OccupantAt(Coord at) => at == unit.At ? Occupant.None : state.OccupantAt(at, unit.Side);

        var footing = content.AbilitiesOf(unit.Unit);
        Distances? chosen = null;
        var chosenCost = int.MaxValue;
        foreach (var target in players)
        {
            var distances = Movement.DistancesTo(state.Map, content, AttackTiles(state, content, unit, weapon, target, movement), movement, OccupantAt, footing);
            var cost = distances.From(unit.At);
            if (cost is { } c && c < chosenCost)
            {
                chosen = distances;
                chosenCost = c;
            }
        }

        return chosen is null ? null : Toward(state, content, unit, chosen, reach, playerReach, refused);
    }

    /// <summary>
    /// Where the hunter marches when no defender of the hunted front is in its plan (issue 692,
    /// <see cref="Hunt"/>): the reachable tile with the lowest remaining path cost to any of the
    /// front's tiles, the party left out of the field as in <see cref="Drift"/>, ties as
    /// <see cref="Approach"/> breaks them; null when no reachable tile has a path, which means Wait.
    /// </summary>
    public static Coord? March(BattleState state, GameContent content, BattleUnit unit, Front front, Reach reach, IReadOnlyList<Reach> playerReach, Func<Coord, bool>? refused = null) =>
        MarchTo(state, content, unit, front.Tiles, reach, playerReach, refused);

    /// <summary>
    /// The reachable tile with the lowest remaining path cost to any of <paramref name="goal"/>, the
    /// party left out of the field as in <see cref="Drift"/>, ties as <see cref="Approach"/> breaks
    /// them; null when no reachable tile has a path. The hunter's march (<see cref="March"/>) and the
    /// untaken route's drift on its crossing (<see cref="Routes"/>, issue 81) both read it.
    /// </summary>
    public static Coord? MarchTo(BattleState state, GameContent content, BattleUnit unit, IReadOnlyCollection<Coord> goal, Reach reach, IReadOnlyList<Reach> playerReach, Func<Coord, bool>? refused = null)
    {
        var movement = Grounding.MovementOf(unit, content);
        Occupant GroundOnly(Coord at) =>
            at == unit.At || state.UnitAt(at) is { Side: Side.Player } ? Occupant.None : state.OccupantAt(at, unit.Side);
        var distances = Movement.DistancesTo(state.Map, content, goal, movement, GroundOnly);
        return distances.From(unit.At) is null ? null : Toward(state, content, unit, distances, reach, playerReach, refused);
    }

    /// <summary>
    /// Where the messenger runs this phase (DESIGN.md 13.24): the reachable tile with the lowest
    /// remaining path cost to its road under <see cref="Messenger.Field"/>, ties as
    /// <see cref="Approach"/> breaks them; null when no reachable tile has a path.
    /// </summary>
    public static Coord? Run(BattleState state, GameContent content, BattleUnit unit, Reach reach, IReadOnlyList<Reach> playerReach)
    {
        var field = Messenger.Field(state, content, unit);
        return field.From(unit.At) is null ? null : Toward(state, content, unit, field, reach, playerReach);
    }

    /// <summary>
    /// DESIGN.md 13.7's third arm (issue 308): where an Aggressive enemy that knows of no
    /// player unit moves on a dusk map. It knows the ground, not the party, so it makes for
    /// the objective: on Escape the exit nearest by its own movement cost, on Seize the
    /// throne. The destination is the reachable tile with the lowest remaining path cost to
    /// any such tile nobody else stands on, ties as <see cref="Approach"/> breaks them. The
    /// path cost is measured as if the party were not there (issue 326): player units neither
    /// block nor slow the field, so one unit on a lone gap does not freeze the chase. The move
    /// itself stays legal, so the unit stops at the best tile it can reach along that field,
    /// up against the blocker. Null, which means Wait, on Rout, Defeat Boss and Survive, and
    /// when no objective tile is free or reachable. Stateless: nothing is remembered between phases.
    /// </summary>
    public static Coord? Drift(BattleState state, GameContent content, BattleUnit unit, Reach reach, IReadOnlyList<Reach> playerReach, Func<Coord, bool>? refused = null)
    {
        var map = state.Map;
        var objective = map.Win switch
        {
            WinCondition.Escape => map.Exits.ToList(),
            WinCondition.Seize => Enumerable.Range(0, map.Height)
                .SelectMany(y => Enumerable.Range(0, map.Width).Select(x => new Coord(x, y)))
                .Where(map.IsThrone)
                .ToList(),
            _ => new List<Coord>(),
        };
        objective.RemoveAll(tile => state.UnitAt(tile) is { } standing && standing.Id != unit.Id);
        if (objective.Count == 0)
        {
            return null;
        }

        var movement = Grounding.MovementOf(unit, content);
        Occupant GroundOnly(Coord at) =>
            at == unit.At || state.UnitAt(at) is { Side: Side.Player } ? Occupant.None : state.OccupantAt(at, unit.Side);
        var distances = Movement.DistancesTo(map, content, objective, movement, GroundOnly);
        return distances.From(unit.At) is null ? null : Toward(state, content, unit, distances, reach, playerReach, refused);
    }

    /// <summary>
    /// The reachable tile with the lowest remaining path cost under <paramref name="chosen"/>,
    /// ties by highest terrain avoid, then fewest player units whose reach set contains the
    /// tile, then cost from the mover, then row-major; null when no reachable tile has a path.
    /// On a <c>wildfire: on</c> map a tile that is burning or that the next front lights
    /// (<see cref="Wildfire.Scorched"/>) loses to every tile that is not, ahead of the path
    /// (DESIGN.md 13.15): a mover with nothing to hit gains nothing by standing in fire.
    /// A tile <paramref name="refused"/> names is skipped (the boss veto, issue 385), checked
    /// only for a tile that would beat the best so far.
    /// </summary>
    private static Coord? Toward(BattleState state, GameContent content, BattleUnit unit, Distances chosen, Reach reach, IReadOnlyList<Reach> playerReach, Func<Coord, bool>? refused = null)
    {
        var movement = content.Class(unit.Unit.ClassId).Movement;
        Coord? destination = null;
        var scorched = Avoided(state);
        var bestKey = (Scorch: int.MaxValue, Remaining: int.MaxValue, Avoid: int.MinValue, Exposure: int.MaxValue, Cost: int.MaxValue);
        foreach (var tile in reach.Destinations)
        {
            if (chosen.From(tile) is not { } remaining)
            {
                continue;
            }

            var key = (
                Scorch: scorched.Contains(tile) ? 1 : 0,
                Remaining: remaining,
                Avoid: -state.Map.TerrainAt(tile, content).AvoidFor(movement),
                Exposure: playerReach.Count(r => r.CanEnd(tile)),
                Cost: reach.CostTo(tile)!.Value);
            if (key.CompareTo(bestKey) < 0 && !(refused?.Invoke(tile) ?? false))
            {
                bestKey = key;
                destination = tile;
            }
        }

        return destination;
    }

    /// <summary>
    /// The tiles from which <paramref name="unit"/>'s weapon reaches <paramref name="target"/>
    /// and on which it could end a move: inside the map, enterable by its movement type (or its own
    /// tile, where a grounded flier may be stranded, issue 703), and occupied by nobody but itself.
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

                if (tile != unit.At && !state.Map.TerrainAt(tile, content).IsPassable(movement))
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
    /// The tiles a mover ranks below every other stop: <see cref="Wildfire.Scorched"/> on a
    /// <c>wildfire: on</c> map, and on a <c>windup: on</c> map every tile under a raised blow
    /// (<see cref="Windup.Marked"/>, DESIGN.md 13.16), since a unit left there is struck for sure.
    /// </summary>
    private static IReadOnlySet<Coord> Avoided(BattleState state)
    {
        var scorched = Wildfire.Scorched(state.Map);
        var marked = Windup.Marked(state);
        return marked.Count == 0 ? scorched : scorched.Concat(marked).ToHashSet();
    }

    /// <summary>
    /// One scored attack. <see cref="Beats"/> is section 8's whole order: higher score,
    /// then, on a <c>wildfire: on</c> map, a tile that is not <see cref="Wildfire.Scorched"/>
    /// (fire breaks ties between strikes and never cancels one, DESIGN.md 13.15), then lower target id, then higher terrain avoid on the tile, then fewer player
    /// units whose reach set contains it, then lower cost from the mover, then row-major.
    /// An option equal to the best on the whole order does not beat it, so of two weapons
    /// scoring the same the earlier slot strikes.
    /// </summary>
    private sealed record AttackOption(double Score, string TargetId, Coord Tile, int Avoid, int Exposure, int Cost, int Slot, bool Scorched = false)
    {
        public bool Beats(AttackOption other)
        {
            if (Score != other.Score)
            {
                return Score > other.Score;
            }

            if (Scorched != other.Scorched)
            {
                return !Scorched;
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

/// <summary>An enemy's strike as the planner would make it: the tile it strikes from and the inventory slot of the weapon it swings.</summary>
public sealed record EnemyStrike(Coord From, int Slot);

/// <summary>
/// A boss's refusal <see cref="EnemyAi.Refusal"/> names (issue 565): the nearest tile it could
/// strike the unit from that the veto refused (<paramref name="Refused"/>), and the tile its plan
/// ends on instead (<paramref name="Ends"/>), its own tile when it holds.
/// </summary>
public sealed record VetoRefusal(Coord Refused, Coord Ends);

