namespace Ironwake.Core.Tests.Combat;

public class KeyedRngTests
{
    [Fact]
    public void TheSameSeedAndKeyGiveTheSameRollInAnyOrder()
    {
        var key = RollKey.Growth("wren", 7, Stat.Spd);
        var first = new KeyedRng(1).Roll(key);

        var rng = new KeyedRng(1);
        for (var i = 0; i < 100; i++)
        {
            rng.Roll(RollKey.Combat(i, Side.Enemy, "brigand", "wren", 0, CombatRoll.HitA));
        }

        Assert.Equal(first, rng.Roll(key));
        Assert.Equal(first, new KeyedRng(1).Roll(RollKey.Growth("wren", 7, Stat.Spd)));
    }

    [Fact]
    public void RollsAreFixedNumbersNotAProcessHash()
    {
        // Pinned so a change to the hash is a deliberate, visible decision: it changes every seed's game.
        Assert.Equal(23, new KeyedRng(0).Roll(RollKey.Growth("wren", 2, Stat.Hp)));
        Assert.Equal(85, new KeyedRng(1).Roll(RollKey.Combat(1, Side.Player, "wren", "brigand", 0, CombatRoll.HitA)));
    }

    [Fact]
    public void RollsAreInZeroToNinetyNineAndEveryValueAppearsAboutOnePercentOfTheTime()
    {
        var rng = new KeyedRng(42);
        var counts = new int[100];
        const int draws = 200_000;
        for (var i = 0; i < draws; i++)
        {
            var roll = rng.Roll(RollKey.Growth("unit" + (i % 7), i, (Stat)(i % 9)));
            Assert.InRange(roll, 0, 99);
            counts[roll]++;
        }

        Assert.All(counts, count => Assert.InRange(count, draws / 100 * 0.85, draws / 100 * 1.15));
    }

    [Fact]
    public void DifferentSeedsDifferAndDifferentKeysDiffer()
    {
        var key = RollKey.Combat(1, Side.Player, "wren", "brigand", 0, CombatRoll.HitA);

        Assert.True(Enumerable.Range(0, 10).Select(seed => new KeyedRng((ulong)seed).Roll(key)).Distinct().Count() > 1);
        Assert.True(Enumerable.Range(0, 10).Select(turn => new KeyedRng(5).Roll(RollKey.Combat(turn, Side.Player, "wren", "brigand", 0, CombatRoll.HitA))).Distinct().Count() > 1);
    }

    [Fact]
    public void KeysAreTheirTextAndTheGrowthKeyNamesOnlyUnitLevelAndStat()
    {
        Assert.Equal("growth/wren/7/Spd", RollKey.Growth("wren", 7, Stat.Spd).Text);
        Assert.Equal("combat/3/Enemy/brigand/wren/1/Crit", RollKey.Combat(3, Side.Enemy, "brigand", "wren", 1, CombatRoll.Crit).ToString());
        Assert.Equal(RollKey.Growth("wren", 7, Stat.Spd), RollKey.Growth("wren", 7, Stat.Spd));
        Assert.NotEqual(RollKey.Growth("wren", 7, Stat.Spd), RollKey.Growth("wren", 8, Stat.Spd));
    }
}
