namespace Ironwake.Core;

/// <summary>
/// The worst a unit could take over one cycle, the question behind the captain veto of
/// issue 12 and the exposure counters of the two Cha spikes (issue 85, issue 16), one
/// function for all three (Design Table, seventh round). <see cref="NoCrit"/> is every
/// strike landing at its plain damage, doubles included; <see cref="WithCrit"/> is the
/// same strikes at the crit multiplier. The hard veto reads the no-crit sum against
/// current HP; the crit sum orders among tiles that pass and never forbids.
/// </summary>
/// <param name="Counter">The worst-case counter on the attack the unit is about to make; zero when the plan has no attack.</param>
/// <param name="NoCrit">Counter plus every enemy that can reach the tile, no crit landing.</param>
/// <param name="WithCrit">Counter plus every enemy that can reach the tile, every strike a crit.</param>
public sealed record ExposureSum(int Counter, int NoCrit, int WithCrit);

public static class Exposure
{
    /// <summary>
    /// The exposure of <paramref name="unit"/> ending its cycle on <paramref name="tile"/>,
    /// after attacking <paramref name="target"/> from there when one is named. Every unit
    /// of the other side whose reach-plus-range covers the tile is counted at the
    /// forecast's own numbers from the best attack tile it can end on. The sum counts the
    /// board the enemy phase will find as far as this command certainly determines it
    /// (Design Table, twelfth and thirteenth rounds; DECISIONS/0024 to 0026): an enemy
    /// that is certainly dead is not on it, a group this command certainly wakes is awake
    /// on it, and a strike that certainly misses is not thrown on it. Certainly dead is
    /// exact and constant-free (issue 117): the raw hit at 100, so the resolved
    /// probability is one under either scheme, and the first strike's plain damage at
    /// least the target's current HP; raw 99 prints as 99 under two rolls and still
    /// counts, and a probable kill counts in full. Certainly woken is section 8's three
    /// causes through <see cref="WakeCheck"/>, on the board <see cref="Board"/> builds
    /// (issue 128); a group this command does not wake is counted from where it stands,
    /// the conservative reading in that regime. Certainly missing is a raw hit of 0
    /// (issue 126). Everything else is a worst case over the board at the moment of the
    /// command: a sleeping group a later command wakes, and an ally whose body blocks a
    /// path and then dies inside the cycle, are the enemy's choices and the cycle's, not
    /// this command's certainty, so the veto is not a guarantee over the cycle
    /// (DECISIONS/0025) and gate 1's captain-loss count is where that regime is seen.
    /// An enemy is counted at the worst of every weapon it can strike with, since its
    /// attack options range over all of them (section 8); the counter to the unit's own
    /// attack is the weapon the target holds in front now, the one it last swung.
    /// </summary>
    public static ExposureSum Of(BattleState state, GameContent content, BattleUnit unit, Coord tile, BattleUnit? target = null, int? slot = null)
    {
        var (board, counter, counterCrit) = Plan(state, content, unit, tile, target, slot);
        var moved = board.Find(unit.Id)!;
        var noCrit = counter;
        var withCrit = counterCrit;
        var me = moved.ToCombatant(board, content, countering: true);
        foreach (var enemy in board.UnitsOf(unit.Side == Side.Player ? Side.Enemy : Side.Player))
        {
            var movement = content.Class(enemy.Unit.ClassId).Movement;
            var mayMove = board.EffectiveBehavior(enemy, content) == Behavior.Aggressive;
            var reach = board.ReachOf(enemy, content);
            var worst = (Plain: 0, Crit: 0);
            var found = false;
            for (var arm = 0; arm < enemy.Unit.Inventory.Count; arm++)
            {
                if (enemy.UsableWeaponAt(content, arm) is not { } weapon)
                {
                    continue;
                }

                var armed = enemy.WithSlotInFront(arm);
                foreach (var from in EnemyAi.AttackTiles(board, content, enemy, weapon, moved, movement))
                {
                    if (from != enemy.At && (!mayMove || !reach.CanEnd(from)))
                    {
                        continue;
                    }

                    var striker = content.CombatantOf(enemy.Unit, weapon, board.Map.TerrainAt(from, content), enemy.Hp, 0, armed.WeaponBroken(content)) with { PairHeld = PairRule.Holds(board, enemy, moved) };
                    var forecast = Combat.Forecast(striker, me, from.DistanceTo(tile), state.Scheme);
                    var here = Worst(forecast.Attacker);
                    if (!found || here.Plain > worst.Plain || (here.Plain == worst.Plain && here.Crit > worst.Crit))
                    {
                        worst = here;
                        found = true;
                    }
                }
            }

            noCrit += worst.Plain;
            withCrit += worst.Crit;
        }

        return new ExposureSum(counter, noCrit, withCrit);
    }

    /// <summary>
    /// The no-crit exposure of an enemy boss ending on <paramref name="tile"/>, after
    /// attacking <paramref name="target"/> from there when one is named: the question behind
    /// the boss veto (issue 385). The board and the counter are <see cref="Of"/>'s, and the
    /// party is priced as the player phase that follows can play it: every player unit may
    /// move, so a strike tile another player unit stands on is open to it, since the phase
    /// plays in any order, and each strike tile holds one striker, the worst case over the
    /// seatings (<see cref="SeatedSum"/>, the count <c>threat</c>'s total makes, issue 253).
    /// Each player unit is weighed at its worst plain damage over every weapon and tile.
    /// </summary>
    public static int OfBoss(BattleState state, GameContent content, BattleUnit boss, Coord tile, BattleUnit? target = null, int? slot = null)
    {
        var (board, counter, _) = Plan(state, content, boss, tile, target, slot);
        var moved = board.Find(boss.Id)!;
        var me = moved.ToCombatant(board, content, countering: true);
        var players = board.UnitsOf(Side.Player).ToList();
        var lines = new List<(int Weight, IReadOnlyList<Coord> Tiles)>();
        foreach (var player in players)
        {
            var reach = board.ReachOf(player, content);
            var worst = 0;
            var tiles = new HashSet<Coord>();
            for (var arm = 0; arm < player.Unit.Inventory.Count; arm++)
            {
                if (player.UsableWeaponAt(content, arm) is not { } weapon)
                {
                    continue;
                }

                foreach (var entry in reach.Entries)
                {
                    var from = entry.At;
                    if (!weapon.InRange(from.DistanceTo(tile)) || (!entry.CanEnd && !players.Any(p => p.At == from)))
                    {
                        continue;
                    }

                    var striker = content.CombatantOf(player.Unit, weapon, board.Map.TerrainAt(from, content), player.Hp, 0, player.WithSlotInFront(arm).WeaponBroken(content));
                    var here = Worst(Combat.Forecast(striker, me, from.DistanceTo(tile), state.Scheme).Attacker).Plain;
                    tiles.Add(from);
                    worst = Math.Max(worst, here);
                }
            }

            if (worst > 0)
            {
                lines.Add((worst, tiles.OrderBy(t => t).ToList()));
            }
        }

        return counter + SeatedSum(lines);
    }

    /// <summary>
    /// The heaviest total of <paramref name="lines"/> seated on distinct tiles, each line on
    /// one of its tiles and at most one line per tile, a line left without a free tile
    /// dropped. The lines are a transversal matroid over the tiles, so taking them heaviest
    /// first (ties in list order) and keeping each one an augmenting path can still seat is
    /// the maximum.
    /// </summary>
    public static int SeatedSum(IReadOnlyList<(int Weight, IReadOnlyList<Coord> Tiles)> lines) =>
        Seated(lines).Sum(index => lines[index].Weight);

    /// <summary>
    /// The indices of the <paramref name="lines"/> <see cref="SeatedSum"/> seats, in ascending
    /// order: the strikers its total sums (issue 558). One walk serves both, so a list of the
    /// strikers cannot disagree with the total.
    /// </summary>
    public static IReadOnlyList<int> Seated(IReadOnlyList<(int Weight, IReadOnlyList<Coord> Tiles)> lines)
    {
        var seated = new Dictionary<Coord, int>();
        foreach (var index in Enumerable.Range(0, lines.Count).OrderByDescending(i => lines[i].Weight).ThenBy(i => i))
        {
            Seat(index, new HashSet<Coord>());
        }

        return seated.Values.OrderBy(i => i).ToList();

        bool Seat(int index, HashSet<Coord> visited)
        {
            foreach (var at in lines[index].Tiles)
            {
                if (!visited.Add(at))
                {
                    continue;
                }

                if (!seated.TryGetValue(at, out var holder) || Seat(holder, visited))
                {
                    seated[at] = index;
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// The board the sum is priced on: <paramref name="unit"/> standing on
    /// <paramref name="tile"/>, <paramref name="target"/> removed when the named attack
    /// kills it with certainty, and every sleeping Guard group the command certainly
    /// wakes woken, by proximity to where the player's units then stand, by the noise of
    /// the fight's two tiles when the plan attacks, or by the certain death of a member.
    /// </summary>
    public static BattleState Board(BattleState state, GameContent content, BattleUnit unit, Coord tile, BattleUnit? target = null, int? slot = null) =>
        Plan(state, content, unit, tile, target, slot).Board;

    private static (BattleState Board, int Counter, int CounterCrit) Plan(BattleState state, GameContent content, BattleUnit unit, Coord tile, BattleUnit? target, int? slot)
    {
        var moved = unit with { At = tile };
        var board = state.WithUnit(moved);
        var counter = 0;
        var counterCrit = 0;
        var noisy = new List<Noise>();
        var died = new List<string>();
        if (target is not null)
        {
            var (armed, weapon, rejection) = Resolver.ChooseWeapon(moved, content, slot);
            var distance = tile.DistanceTo(target.At);
            if (rejection is null && weapon!.InRange(distance))
            {
                var forecast = Combat.Forecast(armed.ToCombatant(board, content, against: target), target.Answering(board, content, tile, armed), distance, state.Scheme);
                var radius = Signatures.NoiseRadius(state, content, armed, target);
                noisy.Add(new Noise(tile, radius));
                noisy.Add(new Noise(target.At, radius));
                if (KillsWithCertainty(forecast.Attacker, target.Hp))
                {
                    board = board.WithoutUnit(target.Id);
                    if (target.Group is { } group)
                    {
                        died.Add(group);
                    }
                }
                else
                {
                    (counter, counterCrit) = Worst(forecast.Defender);
                }
            }
        }

        foreach (var woke in WakeCheck.Run(state, board, content, noisy, died))
        {
            board = board.Wake(woke.Group);
        }

        return (board, counter, counterCrit);
    }

    /// <summary>
    /// Whether the first strike of <paramref name="attacker"/> kills a target at
    /// <paramref name="hp"/> with certainty: it strikes, its raw hit is 100 (clamped, so
    /// <see cref="Combat.HitProbability"/> is exactly one under either scheme), and its
    /// plain damage over its first round reaches the HP. The first round, never the double,
    /// because the counter falls between the rounds; a gauntlet's round is two strikes with
    /// nothing between them (issue 70).
    /// </summary>
    public static bool KillsWithCertainty(SideForecast attacker, int hp) =>
        attacker.Strikes && attacker.HitChance >= 100 && attacker.Damage * attacker.StrikesPerRound >= hp;

    /// <summary>
    /// The worst a side's strikes can do: plain and crit damage over one or two strikes,
    /// or nothing when the side does not strike or its raw hit is 0, since a strike that
    /// cannot land is a branch of probability zero under either scheme (issue 126).
    /// </summary>
    private static (int Plain, int Crit) Worst(SideForecast side)
    {
        if (!side.Strikes || side.HitChance <= 0)
        {
            return (0, 0);
        }

        var strikes = side.StrikeCount;
        return (side.Damage * strikes, side.Damage * Combat.CritMultiplier * strikes);
    }
}
