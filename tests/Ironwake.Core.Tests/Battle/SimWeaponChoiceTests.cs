using Ironwake.Core.Tests.Maps;
using Ironwake.Sim;

namespace Ironwake.Core.Tests.Battle;

using static BattleFixture;

/// <summary>
/// Issue 746: the Sim's player chooses a weapon for each attack, as the enemy AI does. A unit
/// carrying a sword in front and a bow behind it draws the bow when the board pays for it, the
/// attack names the slot, and the weapon tally <c>--ladder</c> and <c>--levels</c> print records
/// the strike by the weapon that made it.
/// </summary>
public class SimWeaponChoiceTests
{
    /// <summary>
    /// One row: the archer at the west end, the brigand two tiles east holding. From where it stands
    /// the bow reaches him and his axe cannot answer; the sword needs the tile beside him and takes the counter.
    /// </summary>
    private const string Lane = """
        name: Lane
        size: 3x1
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1

        ...

        units:
        P captain 0,0
        E brigand 2,0 group:yard behavior:hold

        """;

    private static readonly Unit Archer = Recruit("hale", "ranger", new Stats(22, 8, 0, 7, 8, 6, 5, 2, 9), "iron_sword", "iron_bow");

    private static BattleState Board() => Start(map: Lane, roster: ValueList<Unit>.Of(Archer));

    [Fact]
    public void TheHeuristicPlayerScoresEveryCarriedWeaponAndDrawsTheBowTheBoardPaysFor()
    {
        var state = Board();
        var hale = state.Find("hale")!;
        Assert.Equal(0, hale.EquippedSlot(Starter));
        Assert.Equal(new[] { 0, 1 }, HeuristicPlayer.Arms(Starter, hale).Select(a => a.Slot));

        var plan = HeuristicPlayer.PlanUnit(state, Starter, hale);

        var attack = Assert.IsType<Attack>(Assert.Single(plan));
        Assert.Equal(1, attack.Slot);
        Assert.Equal("iron_bow", state.Do(attack).Find("hale")!.EquippedWeapon(Starter)!.Id);
    }

    [Fact]
    public void AUnitWithOneWeaponStrikesWithItAndTheAttackNamesNoSlot()
    {
        var swordOnly = Archer with { Inventory = Inventory.Empty.Add(new ItemStack("iron_sword", 40)) };
        var state = Start(map: Lane, roster: ValueList<Unit>.Of(swordOnly));

        var plan = HeuristicPlayer.PlanUnit(state, Starter, state.Find("hale")!);

        var attack = Assert.IsType<Attack>(plan[^1]);
        Assert.Null(attack.Slot);
        Assert.Equal(new Coord(1, 0), Assert.IsType<Move>(plan[0]).To);
    }

    [Fact]
    public void TheWeaponTallyCountsAnAttackByTheSlotItNamed()
    {
        var state = Board();
        var attack = new Attack("hale", "brigand-1", 1);
        var result = Resolver.Apply(state, Starter, attack);
        Assert.True(result.Accepted);

        var strike = Assert.Single(WeaponMix.Strikes(state, Starter, attack, result.Events));

        Assert.Equal(("hale", "ranger", WeaponType.Bow, false), strike);
        Assert.Equal("bow 1/0", WeaponMix.Zero.With(strike.Type, strike.Counter).ToString());
    }

    [Fact]
    public void TheWeaponTallyCountsACounterByTheWeaponInFront()
    {
        var swordOnly = Archer with { Inventory = Inventory.Empty.Add(new ItemStack("iron_sword", 40)) };
        var state = Start(map: Lane, roster: ValueList<Unit>.Of(swordOnly)).Do(new Move("hale", new Coord(1, 0))).Do(new Wait("hale")).Do(new EndPhase());
        Assert.Equal(Side.Enemy, state.Phase);
        var attack = new Attack("brigand-1", "hale");
        var result = Resolver.Apply(state, Starter, attack);
        Assert.True(result.Accepted);

        var strikes = WeaponMix.Strikes(state, Starter, attack, result.Events).ToList();

        Assert.Equal(new[] { ("hale", "ranger", WeaponType.Sword, true) }, strikes);
        Assert.Equal("sword 1/1", WeaponMix.Zero.With(WeaponType.Sword, false).Plus(WeaponMix.Zero.With(WeaponType.Sword, true)).ToString());
    }
}
