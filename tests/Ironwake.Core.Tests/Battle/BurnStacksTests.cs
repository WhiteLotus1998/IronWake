using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Fire's second burn (issue 1320, DECISIONS/0317): a tome's <c>burnStacks</c> sets how many stacks of its
/// school's burn a hit lays, 1 by default, only on a tome whose rider is burn. The stacks stop at the
/// rider's cap and the hit refreshes the one count as a single stack does. The placeholder tome is
/// <c>fire_dot_2</c>, a test fixture copied from Cinder; nothing in <c>content/</c> carries the field.
/// Played on the sample <c>the_tollgate_frost.map</c>, Pell against the woods brigand.
/// </summary>
public class BurnStacksTests
{
    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    private static readonly Weapon Cinder = Shipped.Weapon("cinder");

    private static readonly Weapon FireDot2 = Cinder with { Id = "fire_dot_2", Name = "Fire Dot 2", Rider = RiderKind.Burn, BurnStacks = 2 };

    private static readonly GameContent Twos = Shipped with
    {
        Weapons = Shipped.Weapons
            .SetItem("fire_dot_2", FireDot2)
            .SetItem("test_ember", Cinder with { Id = "test_ember", Name = "Test Ember", Rider = RiderKind.Ember }),
    };

    private static readonly string SamplePath = Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "samples", "the_tollgate_frost.map");

    /// <summary>Pell two tiles below the woods brigand, <paramref name="tome"/> in her first slot.</summary>
    private static BattleState Facing(ulong seed, string tome = "fire_dot_2", GameContent? content = null)
    {
        content ??= Twos;
        var state = BattleState.From(MapFiles.Load(SamplePath, content), content, content.Cast, seed);
        var pell = state.Find("pell")!;
        var unit = pell.Unit with { Inventory = pell.Unit.Inventory.Replace(0, new ItemStack(tome, content.Weapon(tome).Durability)) };
        return state.WithUnit(pell with { At = new Coord(6, 7), Unit = unit });
    }

    private static BattleUnit Brigand(BattleState state) => state.Units.Single(u => u.Side == Side.Enemy && u.Group == "woods" && u.Unit.ClassId == Shipped.Unit("toll_brigand").ClassId);

    private static bool Landed(ApplyResult result) => result.Events.OfType<CombatFought>().Single().Strikes.Any(s => s.AttackerId == "pell" && s.Hit);

    /// <summary>The first seed on which Pell's tome lands on <paramref name="brigand"/>'s shape of the brigand and leaves it standing.</summary>
    private static (BattleUnit Before, BattleUnit After, ApplyResult Result) Struck(Func<BattleUnit, BattleUnit> brigand, string tome = "fire_dot_2")
    {
        for (ulong seed = 1; seed < 400; seed++)
        {
            var state = Facing(seed, tome);
            var before = brigand(Brigand(state));
            var result = Resolver.Apply(state.WithUnit(before), Twos, new Attack("pell", before.Id));
            Assert.True(result.Accepted, result.Rejection?.Message);
            if (result.Next.Find(before.Id) is { } after && Landed(result))
            {
                return (before, after, result);
            }
        }

        throw new InvalidOperationException("no seed under 400 gave a surviving hit");
    }

    private static ContentFiles ShippedWithWeapons(Func<string, string> edit)
    {
        var files = ContentSerializer.Write(Shipped);
        return files with { Weapons = new ContentFile(files.Weapons.Name, edit(files.Weapons.Text)) };
    }

    [Fact]
    public void ATomeThatLaysTwoLaysTwoStacksFromOneHit()
    {
        var (before, after, result) = Struck(b => b);

        Assert.Equal((2, 2, 2), (after.Burn, after.BurnStacks, after.BurnPhases));
        var tick = Math.Max(1, 2 * 2 - Twos.StatsOf(before.Unit).Res / 2);
        Assert.Contains(new UnitIgnited(before.Id, "pell", tick, 2, 2, 2), result.Events);
    }

    [Fact]
    public void TheCapClampsTwoStacksOnThreeToFourAndTheCountRefreshes()
    {
        var (_, after, result) = Struck(b => b with { Burn = 2, BurnStacks = 3, BurnPhases = 1 });

        Assert.Equal((4, 2), (after.BurnStacks, after.BurnPhases));
        Assert.Equal(4, Assert.Single(result.Events.OfType<UnitIgnited>()).Stacks);

        var rider = Twos.Riders[MagicSchool.Fire];
        Assert.Equal(4, Burning.Laid(after, rider, 2).BurnStacks);
        Assert.Equal(1, Burning.Laid(after, rider with { Cap = 1 } , 2).BurnStacks);
        Assert.Equal(1, Burning.Laid(Brigand(Facing(1)), rider with { Cap = 1 }, 2).BurnStacks);
    }

    [Fact]
    public void AMissLaysNoStacks()
    {
        for (ulong seed = 1; seed < 400; seed++)
        {
            var state = Facing(seed);
            var brigand = Brigand(state);
            var result = Resolver.Apply(state, Twos, new Attack("pell", brigand.Id));
            if (result.Next.Find(brigand.Id) is { } after && !Landed(result))
            {
                Assert.Equal((0, 0), (after.BurnStacks, after.BurnPhases));
                Assert.DoesNotContain(result.Events, e => e is UnitIgnited);
                return;
            }
        }

        Assert.Fail("no seed under 400 gave a full miss");
    }

    [Fact]
    public void AKillLaysNoStacks()
    {
        for (ulong seed = 1; seed < 400; seed++)
        {
            var state = Facing(seed);
            var brigand = Brigand(state) with { Hp = 1 };
            var result = Resolver.Apply(state.WithUnit(brigand), Twos, new Attack("pell", brigand.Id));
            if (result.Next.Find(brigand.Id) is null)
            {
                Assert.DoesNotContain(result.Events, e => e is UnitIgnited);
                return;
            }
        }

        Assert.Fail("no seed under 400 gave a kill");
    }

    [Fact]
    public void ALearnedGateThatHoldsBlocksBothStacks()
    {
        var learning = Twos with
        {
            Classes = Twos.Classes.SetItem("adept", Twos.Class("adept") with { Schools = ValueList<MagicSchool>.Of(MagicSchool.Ice, MagicSchool.Lightning) }),
        };
        var (blocked, passed) = (false, false);
        for (ulong seed = 1; seed < 400 && !(blocked && passed); seed++)
        {
            foreach (var above in new[] { false, true })
            {
                var state = Facing(seed, content: learning);
                var brigand = Brigand(state);
                var pell = state.Find("pell")!;
                var res = learning.StatsOf(brigand.Unit).Res;
                var mag = (above ? res + 1 : res) - learning.Class(pell.Unit.ClassId).Modifiers.Mag;
                state = state.WithUnit(pell with { Unit = pell.Unit with { Stats = pell.Unit.Stats with { Mag = mag }, Learned = ValueList<MagicSchool>.Of(MagicSchool.Fire) } });
                var result = Resolver.Apply(state, learning, new Attack("pell", brigand.Id));
                Assert.True(result.Accepted, result.Rejection?.Message);
                if (result.Next.Find(brigand.Id) is not { } after || !Landed(result))
                {
                    continue;
                }

                Assert.Equal(above ? 2 : 0, after.BurnStacks);
                Assert.Equal(above, result.Events.Any(e => e is UnitIgnited));
                blocked |= !above;
                passed |= above;
            }
        }

        Assert.True(blocked && passed, "no seed under 400 gave a surviving hit on both sides of the gate");
    }

    [Fact]
    public void LastEmberCashesOutTheSumOfTwoTwoStackHits()
    {
        var rider = Twos.Riders[MagicSchool.Fire];
        var built = Burning.Laid(Burning.Laid(Brigand(Facing(1)), rider, 2), rider, 2);
        Assert.Equal(4, built.BurnStacks);
        var owed = Burning.Owed(Twos, built);
        Assert.Equal(Math.Max(1, 4 * 2 - Twos.StatsOf(built.Unit).Res / 2) * 2, owed);

        var (before, after, result) = Struck(b => b with { Burn = built.Burn, BurnStacks = built.BurnStacks, BurnPhases = built.BurnPhases }, "test_ember");

        var cashed = Assert.Single(result.Events.OfType<BurnCashed>());
        Assert.Equal(Math.Min(owed, cashed.HpAfter + cashed.Amount - 1), cashed.Amount);
        Assert.Equal((0, 0), (after.BurnStacks, after.BurnPhases));
        Assert.Equal(before.Id, cashed.UnitId);
    }

    [Fact]
    public void TheForecastTheCardTheLineAndTheProtocolCarryTheStacksAHitLays()
    {
        var state = Facing(1);
        var (pell, brigand) = (state.Find("pell")!, Brigand(state));
        var tick = Burning.Tick(Twos, Burning.Laid(brigand, Twos.Riders[MagicSchool.Fire], 2));

        Assert.Equal($" burn {tick} (2 stacks, 2 phases), lays 2", PlaySession.Riders(Twos, pell, brigand, null).Riders);
        Assert.Contains("A hit lays 2 stacks of burn.", ItemCard.Text(Twos, "fire_dot_2"));
        Assert.DoesNotContain("stacks of burn", ItemCard.Text(Twos, "cinder"));

        var line = PlaySession.Describe(new UnitIgnited("brigand-1", "pell", 4, 2, 2, 2), Twos, UnitNames.None);
        Assert.EndsWith("(2 stacks, 2 laid by the hit)", line);
        Assert.EndsWith("(1 stack)", PlaySession.Describe(new UnitIgnited("brigand-1", "pell", 2, 2, 1), Twos, UnitNames.None));
    }

    [Fact]
    public void ThePlannerPricesTwoStacksAboveOne()
    {
        var target = Brigand(Facing(1));
        var one = Burning.Expected(Twos, Cinder with { Rider = RiderKind.Burn }, target, 99, 1);
        var two = Burning.Expected(Twos, FireDot2, target, 99, 1);

        Assert.Equal(Burning.Owed(Twos, Burning.Laid(target, Twos.Riders[MagicSchool.Fire], 2)), two);
        Assert.True(two > one, $"{two} against {one}");
    }

    [Fact]
    public void BurnStacksLoadsDefaultsToOneAndRoundTrips()
    {
        var content = ContentLoader.Parse(ShippedWithWeapons(w => w.Replace("\"school\": \"fire\"", "\"school\": \"fire\", \"rider\": \"burn\", \"burnStacks\": 2")));
        var again = ContentLoader.Parse(ContentSerializer.Write(content));

        Assert.Equal(2, content.Weapon("cinder").BurnStacks);
        Assert.Equal(2, again.Weapon("cinder").BurnStacks);
        Assert.All(Shipped.Weapons.Values, w => Assert.Equal(1, w.BurnStacks));
        Assert.DoesNotContain("burnStacks", ContentSerializer.Write(Shipped).Weapons.Text);
    }

    [Theory]
    [InlineData("\"school\": \"fire\"", "\"school\": \"fire\", \"rider\": \"burn\", \"burnStacks\": 0", "cinder", "must be at least 1")]
    [InlineData("\"school\": \"fire\"", "\"school\": \"fire\", \"burnStacks\": 2", "cinder", "only a tome whose rider is burn")]
    [InlineData("\"school\": \"fire\"", "\"school\": \"fire\", \"rider\": \"ember\", \"burnStacks\": 2", "cinder", "only a tome whose rider is burn")]
    [InlineData("\"id\": \"bolt\"", "\"id\": \"bolt\", \"burnStacks\": 2", "bolt", "only a tome whose rider is burn")]
    public void ABadBurnStacksIsRefusedAtLoadNamingFileEntryAndField(string from, string to, string entry, string why)
    {
        var error = Assert.Throws<ContentException>(() => ContentLoader.Parse(ShippedWithWeapons(w => w.Replace(from, to))));

        Assert.Equal(ContentFiles.WeaponsName, error.File);
        Assert.Equal(entry, error.Entry);
        Assert.Equal("burnStacks", error.Field);
        Assert.Contains(why, error.Message);
    }
}
