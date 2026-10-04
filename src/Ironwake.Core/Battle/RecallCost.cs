namespace Ironwake.Core;

/// <summary>
/// What a Recall to a history state gives back (DESIGN.md section 7, issue 75): the
/// difference between that state and the present, in the terms a player weighs before
/// spending a charge. A rewind undoes both sides' work, so the kills, EXP and levels the
/// player earned since are given back, the HP the enemies lost since comes back to them, a
/// returned claimant talked off the board since (issue 633) comes back as a talk, not a kill,
/// a messenger gone by its road or a bound enemy freed since (issue 829) comes back as that, not a kill,
/// reinforcements that arrived since have not arrived yet, and the player units lost and
/// the HP the player lost since are returned. Pure: it reads two states and changes neither.
/// </summary>
/// <param name="ToIndex">The history index the Recall would return to.</param>
/// <param name="Turn">The turn of that state.</param>
/// <param name="KillsGivenBack">Enemies alive then and dead now, in id order; a claimant talked off the board, an escaped messenger or a freed enemy is not one (issues 826, 829).</param>
/// <param name="ExpGivenBack">EXP player units earned since, levels counted at 100 each, over the units alive both then and now.</param>
/// <param name="LevelsGivenBack">Level-ups player units gained since, over the same units.</param>
/// <param name="EnemyHpBack">HP the enemies alive then have lost since, a dead one counted from its HP then to 0; a talked claimant, an escaped messenger or a freed enemy left with their HP and is not counted.</param>
/// <param name="ArrivalsUndone">Enemies on the board now that were not on it then (map-event spawns), in id order.</param>
/// <param name="UnitsReturned">Player units alive then and dead now, in id order; a unit that left through an exit since is not dead (issue 269).</param>
/// <param name="HpByUnit">HP each player unit alive then has lost since, a dead one counted from its HP then to 0, in id order; a unit that lost nothing, or was healed since, is not listed (issue 552).</param>
public sealed record RecallCost(
    int ToIndex,
    int Turn,
    ValueList<string> KillsGivenBack,
    int ExpGivenBack,
    int LevelsGivenBack,
    int EnemyHpBack,
    ValueList<string> ArrivalsUndone,
    ValueList<string> UnitsReturned,
    ValueList<HpReturn> HpByUnit)
{
    /// <summary>
    /// The talk a Recall gives back (issue 826): the returned claimant on the board then and talked
    /// off it since, with the fate the talk gave, or null. A talk is not a kill (DECISIONS/0193).
    /// </summary>
    public TalkReturn? TalkGivenBack { get; init; }

    /// <summary>
    /// The escape a Recall gives back (issue 829): the messenger on the board then and gone by its
    /// road since, by id, or null. An escape is not a kill (DESIGN.md 13.24).
    /// </summary>
    public string? EscapeGivenBack { get; init; }

    /// <summary>
    /// The freeing a Recall gives back (issue 829): the bound enemy on the board then and freed by
    /// her bond's boss falling since, by id, or null. A freeing is not a kill (issue 750).
    /// </summary>
    public string? FreedGivenBack { get; init; }

    /// <summary>HP the player units alive then have lost since, summed over <see cref="HpByUnit"/>.</summary>
    public int HpReturned => HpByUnit.Sum(entry => entry.Hp);

    /// <summary>The HP a Recall returns to one unit, alive now or not; the whole of its HP then when it is dead now.</summary>
    public int HpFor(string id) => HpByUnit.FirstOrDefault(entry => entry.Id == id)?.Hp ?? 0;

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
        TalkReturn? talk = null;
        string? escape = null;
        string? freed = null;
        foreach (var enemy in then.UnitsOf(Side.Enemy))
        {
            var now = state.Find(enemy.Id);
            if (now is null && TalkedSince(then, state, enemy) is { } fate)
            {
                talk = new TalkReturn(enemy.Id, fate);
                continue;
            }

            if (now is null && EscapedSince(then, state, enemy))
            {
                escape = enemy.Id;
                continue;
            }

            if (now is null && FreedSince(then, state, enemy))
            {
                freed = enemy.Id;
                continue;
            }

            if (now is null)
            {
                kills.Add(enemy.Id);
            }

            enemyHp += Math.Max(0, enemy.Hp - (now?.Hp ?? 0));
        }

        var arrivals = state.UnitsOf(Side.Enemy).Where(u => then.Find(u.Id) is null).Select(u => u.Id).ToList();

        var returned = new List<string>();
        var hp = new List<HpReturn>();
        var exp = 0;
        var levels = 0;
        foreach (var unit in then.UnitsOf(Side.Player))
        {
            var now = state.Find(unit.Id) ?? state.Escaped.FirstOrDefault(u => u.Id == unit.Id);
            if (now is null)
            {
                returned.Add(unit.Id);
            }
            else
            {
                exp += Math.Max(0, TotalExp(now.Unit) - TotalExp(unit.Unit));
                levels += Math.Max(0, now.Unit.Level - unit.Unit.Level);
            }

            var lost = unit.Hp - (now?.Hp ?? 0);
            if (lost > 0)
            {
                hp.Add(new HpReturn(unit.Id, lost));
            }
        }

        kills.Sort(string.CompareOrdinal);
        arrivals.Sort(string.CompareOrdinal);
        returned.Sort(string.CompareOrdinal);
        hp.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        return new RecallCost(
            index,
            then.Turn,
            ValueList<string>.From(kills),
            exp,
            levels,
            enemyHp,
            ValueList<string>.From(arrivals),
            ValueList<string>.From(returned),
            ValueList<HpReturn>.From(hp))
        {
            TalkGivenBack = talk,
            EscapeGivenBack = escape,
            FreedGivenBack = freed,
        };
    }

    /// <summary>
    /// The fate a talk gave <paramref name="enemy"/> between <paramref name="then"/> and
    /// <paramref name="now"/>, or null when they are not the returned claimant or were not talked
    /// off the board since: the bond names them, and the present state's fate is Turned or Spared
    /// where the earlier state had none.
    /// </summary>
    private static ReturnFate? TalkedSince(BattleState then, BattleState now, BattleUnit enemy) =>
        Returned.Is(then, enemy) && then.ReturnGone is null && now.ReturnGone is ReturnFate.Turned or ReturnFate.Spared
            ? now.ReturnGone
            : null;

    /// <summary>
    /// Whether <paramref name="enemy"/> left the board by the messenger's road between
    /// <paramref name="then"/> and <paramref name="now"/>: they are the messenger, the earlier state
    /// has no messenger fate, and the present one says escaped.
    /// </summary>
    private static bool EscapedSince(BattleState then, BattleState now, BattleUnit enemy) =>
        Messenger.Is(then, enemy) && then.MessengerGone is null && now.MessengerGone is { Escaped: true };

    /// <summary>
    /// Whether <paramref name="enemy"/> was freed between <paramref name="then"/> and
    /// <paramref name="now"/>: the bond names them, the earlier state has no bond fate, and the
    /// present one says freed.
    /// </summary>
    private static bool FreedSince(BattleState then, BattleState now, BattleUnit enemy) =>
        Freed.IsBound(then, enemy) && then.Bond is null && now.Bond == BondFate.Freed;

    /// <summary>Whether the Recall undoes none of the things this record counts: only moves and waits since.</summary>
    public bool IsEmpty =>
        KillsGivenBack.Count == 0 && TalkGivenBack is null && EscapeGivenBack is null && FreedGivenBack is null && ExpGivenBack == 0 && LevelsGivenBack == 0 && EnemyHpBack == 0
        && ArrivalsUndone.Count == 0 && UnitsReturned.Count == 0 && HpReturned == 0;

    private static int TotalExp(Unit unit) => unit.Level * 100 + unit.Exp;
}

/// <summary>The HP a Recall returns to one player unit (issue 552).</summary>
/// <param name="Id">The unit's id.</param>
/// <param name="Hp">HP it lost since the target state; for a unit dead now, its HP then.</param>
public sealed record HpReturn(string Id, int Hp);

/// <summary>The talk a Recall gives back (issue 826): the claimant talked off the board and the fate the talk gave.</summary>
/// <param name="Id">The returned claimant's id.</param>
/// <param name="Fate">Turned by the pick's talk or spared by the captain's.</param>
public sealed record TalkReturn(string Id, ReturnFate Fate);
