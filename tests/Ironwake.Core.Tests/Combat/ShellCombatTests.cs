namespace Ironwake.Core.Tests.Combat;

/// <summary>
/// Obsidian Armor's one-hit shell in the combat formulas (issue 1403, Lotus, DECISIONS/0361): the shell's Def counts
/// against the first hit that lands on its wearer alone, so the forecast, the resolver and every lethal read the
/// planners share price it per strike. Wren (iron sword, Atk 12, doubles) on the forest brigand (Def 2, forest 1):
/// 9 a hit plain, 0 on a +20 shell.
/// </summary>
public class ShellCombatTests
{
    private static Combatant Shelled(int shell = 20) => CombatFixture.BrigandInForest() with { Shell = shell };

    private static ScriptedRng NoCrits(params int[] missed)
    {
        var rng = new ScriptedRng(0);
        for (var i = 0; i < 2; i++)
        {
            rng.Set(RollKey.Combat(1, Side.Player, "wren", "brigand", i, CombatRoll.Crit), 99);
            rng.Set(RollKey.Combat(1, Side.Player, "wren", "brigand", i, CombatRoll.HitA), missed.Contains(i) ? 99 : 0);
        }

        rng.Set(RollKey.Combat(1, Side.Player, "brigand", "wren", 0, CombatRoll.HitA), 99);
        return rng;
    }

    [Theory]
    [InlineData(0, 9, 9)]
    [InlineData(2, 9, 7)]
    [InlineData(9, 9, 0)]
    [InlineData(20, 9, 0)]
    public void TheShellsDefCountsAgainstTheFirstHitAlone(int shell, int plain, int first)
    {
        var side = Ironwake.Core.Combat.Forecast(CombatFixture.WrenOnPlain(), Shelled(shell), 1, RollScheme.OneRoll).Attacker;

        Assert.Equal(plain, side.Damage);
        Assert.Equal(first, side.FirstHit(crit: false));
        Assert.Equal(shell, side.Shell);
        Assert.Equal(plain - first, side.ShellTurned);
    }

    [Fact]
    public void AShellOnTheFirstHitTriplesTheCritOnTheShelledDamage()
    {
        var side = Ironwake.Core.Combat.Forecast(CombatFixture.WrenOnPlain(), Shelled(5), 1, RollScheme.OneRoll).Attacker;

        Assert.Equal(4, side.FirstHit(crit: false));
        Assert.Equal(12, side.FirstHit(crit: true));
    }

    [Fact]
    public void ADoubledSecondStrikeMeetsNoShell()
    {
        var result = CombatResolver.Resolve(CombatFixture.WrenOnPlain(), Shelled(), 1, new CombatContext(1, Side.Player), NoCrits(), RollScheme.OneRoll);

        Assert.Equal([(true, 0), (true, 9)], result.Strikes.Where(s => s.AttackerId == "wren").Select(s => (s.Hit, s.Damage)));
    }

    [Fact]
    public void AMissedFirstStrikeLeavesTheShellForTheSecond()
    {
        var result = CombatResolver.Resolve(CombatFixture.WrenOnPlain(), Shelled(), 1, new CombatContext(1, Side.Player), NoCrits(0), RollScheme.OneRoll);

        Assert.Equal([(false, 0), (true, 0)], result.Strikes.Where(s => s.AttackerId == "wren").Select(s => (s.Hit, s.Damage)));
    }

    [Fact]
    public void MagicMeetsTheShellButItsResIsUntouched()
    {
        var side = Ironwake.Core.Combat.Forecast(CombatFixture.HexerOnPlain(), Shelled(), 1, RollScheme.OneRoll).Attacker;

        Assert.Equal(side.Damage, side.FirstHit(crit: false));
        Assert.Equal(0, side.ShellTurned);
    }

    [Fact]
    public void TheLethalReadsThePlannersShareCountTheShellOnTheFirstHit()
    {
        var plain = Ironwake.Core.Combat.Forecast(CombatFixture.WrenOnPlain(), CombatFixture.BrigandInForest(), 1, RollScheme.OneRoll);
        var shelled = Ironwake.Core.Combat.Forecast(CombatFixture.WrenOnPlain(), Shelled(), 1, RollScheme.OneRoll);
        var counter = Ironwake.Core.Combat.Forecast(CombatFixture.BrigandInForest(), CombatFixture.WrenOnPlain() with { Shell = 20 }, 1, RollScheme.OneRoll);

        Assert.Equal(18, plain.AttackerDamageLivedFor(20));
        Assert.Equal(9, shelled.AttackerDamageLivedFor(20));
        Assert.Equal(0, counter.Attacker.FirstHit(crit: false));
        Assert.Equal(counter.Defender.Damage * counter.Defender.StrikeCount, counter.CounterIfAllLand);
    }
}
