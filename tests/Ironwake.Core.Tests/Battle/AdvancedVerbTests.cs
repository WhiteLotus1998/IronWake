using Ironwake.Content;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The second tier's verbs (issue 704, slice 2): each advanced form changes what the unit does on
/// the map. The Marksman's Long Draw takes a bow to range 3, the Warden's Far Mending takes a heal
/// one tile further, the Scholar strikes with Faith but never heals and enters at Faith D, the
/// Berserker's Blood Price heals 5 on an axe kill, and the Sky Captain moves 7. Each form names a
/// mastery of its own. Played on the shipped Saltmarsh Ford's open south rows, far from the fort.
/// </summary>
public class AdvancedVerbTests
{
    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    private static readonly string MapPath = Path.Combine(Fixture.RealContentDirectory(), "maps", "saltmarsh_ford.map");

    private static BattleState Placed(ulong seed = 704) =>
        BattleState.From(MapFiles.Load(MapPath, Shipped), Shipped, Shipped.Cast, seed);

    /// <summary>The <paramref name="index"/>th player unit put in <paramref name="classId"/> at <paramref name="at"/> carrying <paramref name="items"/>, at full HP in that class.</summary>
    private static BattleState As(BattleState state, int index, string classId, Coord at, out string id, WeaponSkill? skill = null, params string[] items)
    {
        var unit = state.UnitsOf(Side.Player).ElementAt(index);
        id = unit.Id;
        var stacks = items.Select(i => new ItemStack(i, Shipped.Weapons.TryGetValue(i, out var w) ? w.Durability : Shipped.Item(i).Uses));
        var changed = unit.Unit with { ClassId = classId, Inventory = new Inventory(ValueList<ItemStack>.From(stacks)), Skill = skill ?? unit.Unit.Skill };
        return state.WithUnit(unit with { Unit = changed, At = at, Hp = Shipped.StatsOf(changed).Hp });
    }

    private static BattleUnit Soldier(BattleState state) => state.Units.First(u => u.Side == Side.Enemy && !u.IsBoss && u.Unit.ClassId != "skyrider");

    [Theory]
    [InlineData("marksman", 3)]
    [InlineData("bowman", 2)]
    public void LongDrawTakesTheMarksmansBowToRangeThree(string classId, int far)
    {
        var state = As(Placed(), 0, classId, new Coord(2, 8), out var id, null, "iron_bow");

        Assert.Equal(2, state.Find(id)!.EquippedWeapon(Shipped)!.MinRange);
        Assert.Equal(far, state.Find(id)!.EquippedWeapon(Shipped)!.MaxRange);
    }

    [Theory]
    [InlineData("marksman", true)]
    [InlineData("bowman", false)]
    public void AMarksmanStrikesAtThreeTilesAndABowmanIsRefused(string classId, bool accepted)
    {
        var state = As(Placed(), 0, classId, new Coord(2, 8), out var id, null, "iron_bow");
        var soldier = Soldier(state);
        state = state.WithUnit(soldier with { At = new Coord(5, 8) });

        var result = Resolver.Apply(state, Shipped, new Attack(id, soldier.Id));

        Assert.Equal(accepted, result.Accepted);
        if (!accepted)
        {
            Assert.Equal(RejectionReason.OutOfRange, result.Rejection!.Reason);
            Assert.Contains("Iron Bow reaches 2-2", result.Rejection.Message);
        }
    }

    [Fact]
    public void LongDrawDoesNotReachASwordOrAnotherClasssBow()
    {
        var abilities = ValueList<Ability>.Of(Shipped.Ability("long_draw"));

        Assert.Equal(1, AbilityRules.Shape(Shipped.Weapon("iron_sword"), abilities).MaxRange);
        Assert.Equal(3, AbilityRules.Shape(Shipped.Weapon("iron_bow"), abilities).MaxRange);
        Assert.Equal(1, AbilityRules.Shape(Shipped.Weapon("salve"), abilities).MaxRange);
        Assert.Same(Shipped.Weapon("iron_bow"), AbilityRules.Shape(Shipped.Weapon("iron_bow"), ValueList<Ability>.Empty));
    }

    [Theory]
    [InlineData("warden", true)]
    [InlineData("chaplain", false)]
    public void FarMendingTakesTheWardensSalveToTwoTiles(string classId, bool accepted)
    {
        var state = As(Placed(), 0, classId, new Coord(2, 8), out var healer, null, "salve");
        var ally = state.UnitsOf(Side.Player).ElementAt(1);
        state = state.WithUnit(ally with { At = new Coord(4, 8), Hp = ally.Hp - 5 });

        var result = Resolver.Apply(state, Shipped, new UseItem(healer, 0, ally.Id));

        Assert.Equal(accepted, result.Accepted);
        if (!accepted)
        {
            Assert.Equal(RejectionReason.OutOfRange, result.Rejection!.Reason);
        }
        else
        {
            Assert.Contains(result.Events, e => e is UnitHealed h && h.UnitId == ally.Id);
        }
    }

    [Fact]
    public void FarMendingReachesHealsOnlyNotAStrikeSpell()
    {
        var abilities = ValueList<Ability>.Of(Shipped.Ability("far_mending"));

        Assert.Equal(2, AbilityRules.Shape(Shipped.Weapon("salve"), abilities).MaxRange);
        Assert.Equal(3, AbilityRules.Shape(Shipped.Weapon("beacon"), abilities).MaxRange);
        Assert.Equal(2, AbilityRules.Shape(Shipped.Weapon("radiance"), abilities).MaxRange);
        Assert.Equal(2, AbilityRules.Shape(Shipped.Weapon("iron_bow"), abilities).MaxRange);
    }

    [Fact]
    public void AScholarStrikesWithFaithButCannotWieldAHealingSpell()
    {
        var scholar = Shipped.Class("scholar");
        var unit = Shipped.Cast.Single(u => u.Id == "pell") with { ClassId = "scholar", Skill = WeaponSkill.Zero.With(WeaponType.Faith, WeaponRanks.Threshold(WeaponRank.D)) };

        Assert.True(scholar.CanUse(WeaponType.Faith));
        Assert.False(scholar.CanHealWith(WeaponType.Faith));
        Assert.True(unit.CanWield(Shipped.Weapon("radiance"), scholar));
        Assert.False(unit.CanWield(Shipped.Weapon("salve"), scholar));
        Assert.True(unit.CanWield(Shipped.Weapon("salve"), Shipped.Class("warden")));
    }

    [Fact]
    public void AScholarAskedToHealIsRefusedNamingWhy()
    {
        var state = As(Placed(), 0, "scholar", new Coord(2, 8), out var healer, WeaponSkill.Zero.With(WeaponType.Faith, WeaponRanks.Threshold(WeaponRank.D)), "salve");
        var ally = state.UnitsOf(Side.Player).ElementAt(1);
        state = state.WithUnit(ally with { At = new Coord(3, 8), Hp = ally.Hp - 5 });

        var result = Resolver.Apply(state, Shipped, new UseItem(healer, 0, ally.Id));

        Assert.Equal(RejectionReason.NotUsable, result.Rejection!.Reason);
        Assert.Equal($"{healer} cannot use salve: a Scholar strikes with faith and never heals", result.Rejection.Message);
        Assert.DoesNotContain(Resolver.Legal(state, Shipped).OfType<UseItem>(), u => u.UnitId == healer);
    }

    [Fact]
    public void AnAdeptPromotedToScholarHoldsFaithAtDAndStrikesWithRadiance()
    {
        var adept = Shipped.Cast.Single(u => u.Id == "pell") with { Level = 10, Skill = WeaponSkill.Zero.With(WeaponType.Reason, WeaponRanks.Threshold(WeaponRank.C)) };

        var scholar = Certifications.Certify(adept, Shipped.Class("scholar"));

        Assert.Equal(WeaponRank.D, scholar.Skill.Rank(WeaponType.Faith));
        Assert.Equal(WeaponRank.C, scholar.Skill.Rank(WeaponType.Reason));
        Assert.True(scholar.CanWield(Shipped.Weapon("radiance"), Shipped.Class("scholar")));
    }

    [Fact]
    public void AGrantNeverLowersARankAlreadyHeld()
    {
        var unit = Shipped.Cast.Single(u => u.Id == "pell") with { Skill = WeaponSkill.Zero.With(WeaponType.Faith, WeaponRanks.Threshold(WeaponRank.B)) };

        Assert.Equal(WeaponRank.B, Certifications.Grant(unit, Shipped.Class("scholar")).Skill.Rank(WeaponType.Faith));
        Assert.Same(unit, Certifications.Grant(unit, Shipped.Class("pikeman")));
    }

    [Theory]
    [InlineData("berserker", 5)]
    [InlineData("reaver", 0)]
    public void BloodPriceHealsTheBerserkerFiveOnAnAxeKill(string classId, int heal)
    {
        var killed = false;
        for (ulong seed = 1; seed < 300 && !killed; seed++)
        {
            var state = As(Placed(seed), 0, classId, new Coord(2, 8), out var id, null, "iron_axe");
            var unit = state.Find(id)!;
            state = state.WithUnit(unit with { Hp = 5 });
            var soldier = Soldier(state);
            state = state.WithUnit(soldier with { At = new Coord(3, 8), Hp = 1 });

            var result = Resolver.Apply(state, Shipped, new Attack(id, soldier.Id));
            Assert.True(result.Accepted, result.Rejection?.Message);
            if (result.Next.Find(soldier.Id) is not null || result.Next.Find(id) is not { } after)
            {
                continue;
            }

            killed = true;
            Assert.Equal(5 + heal, after.Hp);
            Assert.Equal(heal > 0, result.Events.Contains(new UnitHealed(id, heal, 5 + heal)));
        }

        Assert.True(killed, "no seed under 300 gave a kill");
    }

    [Fact]
    public void BloodPriceAnswersAnAxeOnlyAndNothingWithoutAKill()
    {
        var abilities = ValueList<Ability>.Of(Shipped.Ability("blood_price"));

        Assert.Equal(5, AbilityRules.KillHeal(abilities, Shipped.Weapon("iron_axe")));
        Assert.Equal(0, AbilityRules.KillHeal(abilities, Shipped.Weapon("iron_gauntlets")));
        Assert.Equal(0, AbilityRules.KillHeal(abilities, null));
    }

    [Fact]
    public void TheSkyCaptainMovesSevenAndTheDarkStillReadsOnlyWhatAnEnemyCanBe()
    {
        Assert.Equal(7, Shipped.Class("skycaptain").Mov);
        Assert.Equal(9, Shipped.LongestReach);

        var grounded = Shipped with { Units = Shipped.Units.Remove("wing_captain") };
        Assert.Equal(8, grounded.LongestReach);

    }

    [Theory]
    [InlineData("halberdier", "set_haft")]
    [InlineData("berserker", "hard_to_kill")]
    [InlineData("marksman", "still_breath")]
    [InlineData("scholar", "old_texts")]
    [InlineData("warden", "steadfast")]
    [InlineData("lancer", "couched_lance")]
    [InlineData("skycaptain", "wind_rider")]
    [InlineData("sentinel", "iron_footing")]
    public void EachAdvancedFormHasAMasteryOfItsOwn(string formId, string mastery)
    {
        var form = Shipped.Class(formId);

        Assert.Equal(mastery, form.Mastery);
        Assert.Equal(12, form.MasteryPoints);
        Assert.DoesNotContain(Shipped.Classes.Values, c => c.Id != formId && c.Mastery == mastery);
    }
}
