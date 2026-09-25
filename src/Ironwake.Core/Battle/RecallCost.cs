namespace Ironwake.Core;

/// <summary>
/// What a Recall to a history state gives back (DESIGN.md section 7, issue 75): the
/// difference between that state and the present, in the terms a player weighs before
/// spending a charge. A rewind undoes both sides' work, so the kills, EXP and levels the
/// player earned since are given back, the HP the enemies lost since comes back to them,
/// reinforcements that arrived since have not arrived yet, and the player units lost and
/// the HP the player lost since are returned. Pure: it reads two states and changes neither.
/// </summary>
/// <param name="ToIndex">The history index the Recall would return to.</param>
/// <param name="Turn">The turn of that state.</param>
/// <param name="KillsGivenBack">Enemies alive then and dead now, in id order.</param>
/// <param name="ExpGivenBack">EXP player units earned since, levels counted at 100 each, over the units alive both then and now.</param>
/// <param name="LevelsGivenBack">Level-ups player units gained since, over the same units.</param>
/// <param name="EnemyHpBack">HP the enemies alive then have lost since, a dead one counted from its HP then to 0.</param>
/// <param name="ArrivalsUndone">Enemies on the board now that were not on it then (map-event spawns), in id order.</param>
/// <param name="UnitsReturned">Player units alive then and dead now, in id order.</param>
/// <param name="HpReturned">HP the player units alive then have lost since, a dead one counted from its HP then to 0; a unit healed since counts nothing.</param>
public sealed record RecallCost(
    int ToIndex,
    int Turn,
    ValueList<string> KillsGivenBack,
    int ExpGivenBack,
    int LevelsGivenBack,
    int EnemyHpBack,
    ValueList<string> ArrivalsUndone,
    ValueList<string> UnitsReturned,
    int HpReturned)
{
    /// <summary>
    /// The cost of recalling from <paramref name="state"/> to its history state
    /// <paramref name="index"/>. The index must be in the history; whether the Recall is legal
    /// (charges, phase) is the resolver's question, not this one's.
    /// </summary>
    public static RecallCost Of(BattleState state, int index)
    {
        if (index < 0 || index >= state.History.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"history holds {state.History.Count} states; there is no state {index}");
        }

        var then = state.History[index];
        var kills = new List<string>();
        var enemyHp = 0;
        foreach (var enemy in then.UnitsOf(Side.Enemy))
        {
            var now = state.Find(enemy.Id);
            if (now is null)
            {
                kills.Add(enemy.Id);
            }

            enemyHp += Math.Max(0, enemy.Hp - (now?.Hp ?? 0));
        }

        var arrivals = state.UnitsOf(Side.Enemy).Where(u => then.Find(u.Id) is null).Select(u => u.Id).ToList();

        var returned = new List<string>();
        var hp = 0;
        var exp = 0;
        var levels = 0;
        foreach (var unit in then.UnitsOf(Side.Player))
        {
            var now = state.Find(unit.Id);
            if (now is null)
            {
                returned.Add(unit.Id);
            }
            else
            {
                exp += Math.Max(0, TotalExp(now.Unit) - TotalExp(unit.Unit));
                levels += Math.Max(0, now.Unit.Level - unit.Unit.Level);
            }

            hp += Math.Max(0, unit.Hp - (now?.Hp ?? 0));
        }

        kills.Sort(string.CompareOrdinal);
        arrivals.Sort(string.CompareOrdinal);
        returned.Sort(string.CompareOrdinal);
        return new RecallCost(
            index,
            then.Turn,
            ValueList<string>.From(kills),
            exp,
            levels,
            enemyHp,
            ValueList<string>.From(arrivals),
            ValueList<string>.From(returned),
            hp);
    }

    /// <summary>Whether the Recall undoes none of the things this record counts: only moves and waits since.</summary>
    public bool IsEmpty =>
        KillsGivenBack.Count == 0 && ExpGivenBack == 0 && LevelsGivenBack == 0 && EnemyHpBack == 0
        && ArrivalsUndone.Count == 0 && UnitsReturned.Count == 0 && HpReturned == 0;

    private static int TotalExp(Unit unit) => unit.Level * 100 + unit.Exp;
}
