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
    /// least the target's current HP; raw 99 prints as 100 under two rolls and still
    /// counts, and a probable kill counts in full. Certainly woken is section 8's three
    /// causes through <see cref="WakeCheck"/>, on the board <see cref="Board"/> builds
    /// (issue 128); a group this command does not wake is counted from where it stands,
    /// the conservative reading in that regime. Certainly missing is a raw hit of 0
    /// (issue 126). Everything else is a worst case over the board at the moment of the
    /// command: a sleeping group a later command wakes, and an ally whose body blocks a
    /// path and then dies inside the cycle, are the enemy's choices and the cycle's, not
    /// this command's certainty, so the veto is not a guarantee over the cycle
    /// (DECISIONS/0025) and gate 1's captain-loss count is where that regime is seen.
    /// </summary>
    public static ExposureSum Of(BattleState state, GameContent content, BattleUnit unit, Coord tile, BattleUnit? target = null, int? slot = null)
    {
        var (board, counter, counterCrit) = Plan(state, content, unit, tile, target, slot);
        var moved = board.Find(unit.Id)!;
        var noCrit = counter;
        var withCrit = counterCrit;
        var me = moved.ToCombatant(board.Map, content);
        foreach (var enemy in board.UnitsOf(unit.Side == Side.Player ? Side.Enemy : Side.Player))
        {
            var weapon = enemy.EquippedWeapon(content);
            if (weapon is null)
            {
                continue;
            }

            var movement = content.Class(enemy.Unit.ClassId).Movement;
            var mayMove = board.EffectiveBehavior(enemy) == Behavior.Aggressive;
            var reach = board.ReachOf(enemy, content);
            var worst = (Plain: 0, Crit: 0);
            var found = false;
            foreach (var from in EnemyAi.AttackTiles(board, content, enemy, weapon, moved, movement))
            {
                if (from != enemy.At && (!mayMove || !reach.CanEnd(from)))
                {
                    continue;
                }

                var striker = new Combatant(enemy.Unit, content.Class(enemy.Unit.ClassId), weapon, board.Map.TerrainAt(from, content), enemy.Hp, 0, enemy.WeaponBroken(content));
                var forecast = Combat.Forecast(striker, me, from.DistanceTo(tile), state.Scheme);
                var here = Worst(forecast.Attacker);
                if (!found || here.Plain > worst.Plain || (here.Plain == worst.Plain && here.Crit > worst.Crit))
                {
                    worst = here;
                    found = true;
                }
            }

            noCrit += worst.Plain;
            withCrit += worst.Crit;
        }

        return new ExposureSum(counter, noCrit, withCrit);
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
        var noisy = new List<Coord>();
        var died = new List<string>();
        if (target is not null)
        {
            var (armed, weapon, rejection) = Resolver.ChooseWeapon(moved, content, slot);
            var distance = tile.DistanceTo(target.At);
            if (rejection is null && weapon!.InRange(distance))
            {
                var forecast = Combat.Forecast(armed.ToCombatant(board.Map, content), target.ToCombatant(board.Map, content), distance, state.Scheme);
                noisy.Add(tile);
                noisy.Add(target.At);
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
    /// plain damage reaches the HP. One strike, never the double, because the counter
    /// falls between the strikes.
    /// </summary>
    public static bool KillsWithCertainty(SideForecast attacker, int hp) =>
        attacker.Strikes && attacker.HitChance >= 100 && attacker.Damage >= hp;

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

        var strikes = side.Doubles ? 2 : 1;
        return (side.Damage * strikes, side.Damage * Combat.CritMultiplier * strikes);
    }
}
