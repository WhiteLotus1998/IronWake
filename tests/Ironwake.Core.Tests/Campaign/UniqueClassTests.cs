using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Campaign;

/// <summary>
/// Unique classes (issue 706, DESIGN section 3): a unique class is one cast member's other door at
/// the second promotion, an advanced form of their base beside the standard one, opened by a quest's
/// win. Taking either door closes the other for good. Each names a measure it loses on to the
/// standard form, held here. Maud's Field Surgeon heals double and moves again after a heal, never
/// strikes, and heals only beside her; the Warden keeps range and the sword. Rook's Scout (slice 2)
/// sees further at dusk and counts sleeping groups, and gives up Str and its growth to the Sky Captain.
/// </summary>
public class UniqueClassTests
{
    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    private static readonly IReadOnlyCollection<string> Won = new[] { "maud_1" };

    /// <summary><paramref name="id"/> from the cast as a Chaplain at level 7 with Faith C, ready for either door.</summary>
    private static Unit Ready(string id = "maud") =>
        Shipped.Cast.Single(u => u.Id == id) with
        {
            ClassId = "chaplain",
            Level = 7,
            Skill = WeaponSkill.Zero.With(WeaponType.Faith, WeaponRanks.Threshold(WeaponRank.C)),
        };

    private static string[] Refusals(Unit unit, string classId, IReadOnlyCollection<string> won) =>
        Certifications.Check(unit, Shipped.Class(classId), Shipped.Class(unit.ClassId), false, won).Select(r => r.Requirement).ToArray();

    [Fact]
    public void TheFieldSurgeonIsMaudsOtherDoorAboveTheChaplain()
    {
        var surgeon = Shipped.Class("fieldsurgeon");

        Assert.Equal("chaplain", surgeon.Advances?.Id);
        Assert.Equal("maud", surgeon.Unique);
        Assert.Equal("maud_1", surgeon.UnlockedBy);
        Assert.Equal(SidegradeMeasure.Reach, surgeon.Loses);
        Assert.Equal(Shipped.Class("warden").Certification, surgeon.Certification);
        Assert.Equal(1000, Shipped.Campaign.SealFor(surgeon));
    }

    [Fact]
    public void AUniqueClassIsRefusedUntilItsQuestIsWon()
    {
        Assert.Equal(new[] { "unlockedBy" }, Refusals(Ready(), "fieldsurgeon", Array.Empty<string>()));
        Assert.Empty(Refusals(Ready(), "fieldsurgeon", Won));
    }

    [Fact]
    public void AUniqueClassIsRefusedToAnyOtherUnitEvenWithTheQuestWon()
    {
        var other = Ready("pell");

        Assert.Equal(new[] { "unique" }, Refusals(other, "fieldsurgeon", Won));
        Assert.Equal("Field Surgeon is maud's alone", Certifications.Check(other, Shipped.Class("fieldsurgeon"), null, false, Won)[0].Text);
        Assert.Empty(Refusals(other, "warden", Won));
    }

    [Fact]
    public void TakingTheFieldSurgeonClosesTheWardenForGood()
    {
        var surgeon = Certifications.Certify(Ready(), Shipped.Class("fieldsurgeon"), false, Won);
        var back = Certifications.Certify(surgeon, Shipped.Class("chaplain"));

        Assert.Equal(ValueList<(string, string)>.Of(("chaplain", "fieldsurgeon")), surgeon.Doors);
        Assert.Equal(new[] { "door" }, Refusals(back, "warden", Won));
        Assert.Equal("maud took the other door above Chaplain; Warden is closed", Certifications.Check(back, Shipped.Class("warden"), null, false, Won)[0].Text);
        Assert.Empty(Refusals(back, "fieldsurgeon", Won));
    }

    [Fact]
    public void TakingTheWardenClosesTheFieldSurgeonForGood()
    {
        var warden = Certifications.Certify(Ready(), Shipped.Class("warden"), false, Won);
        var back = Certifications.Certify(warden, Shipped.Class("chaplain"));

        Assert.Equal(ValueList<(string, string)>.Of(("chaplain", "warden")), warden.Doors);
        Assert.Equal(new[] { "door" }, Refusals(back, "fieldsurgeon", Won));
        Assert.Empty(Refusals(back, "warden", Won));
    }

    [Fact]
    public void AFirstTierClassRecordsNoDoor()
    {
        var pell = Shipped.Cast.Single(u => u.Id == "pell") with { Level = 3, Stats = Shipped.Cast.Single(u => u.Id == "pell").Stats with { Mag = 4, Res = 4 } };

        Assert.Empty(Certifications.Certify(pell, Shipped.Class("chaplain")).Doors);
    }

    [Fact]
    public void TheCampScreenOpensTheDoorOnlyOnceTheQuestIsWon()
    {
        var record = CampaignRecord.Start(Shipped, 706) with { Purse = 2000, Roster = ValueList<Unit>.Of(Shipped.Cast[0], Ready()) };

        var refused = record.Certify("maud", "fieldsurgeon", Shipped);
        var taken = (record with { QuestsWon = ValueList<QuestWon>.Of(new QuestWon("maud_1", 3)) }).Certify("maud", "fieldsurgeon", Shipped);

        Assert.False(refused.Accepted);
        Assert.Contains("Field Surgeon opens when the quest maud_1 is won", refused.Text);
        Assert.True(taken.Accepted, taken.Text);
        Assert.Equal("fieldsurgeon", taken.Record.Find("maud")!.ClassId);
        Assert.Equal(1000, taken.Record.Purse);
    }

    [Fact]
    public void TheDoorTakenRoundTripsThroughTheSave()
    {
        var surgeon = Certifications.Certify(Ready(), Shipped.Class("fieldsurgeon"), false, Won);
        var record = CampaignRecord.Start(Shipped, 706) with { Roster = ValueList<Unit>.Of(Shipped.Cast[0], surgeon) };

        var read = ProtocolJson.ReadCampaign(ProtocolJson.Campaign(record), Shipped);

        Assert.Equal(surgeon.Doors, read.Find("maud")!.Doors);
        Assert.DoesNotContain("\"doors\"", ProtocolJson.Campaign(CampaignRecord.Start(Shipped, 706)));
    }

    /// <summary>The sidegrade test (issue 706): the measure a unique class names, read for its unit in it and in the standard form, loses.</summary>
    [Theory]
    [InlineData("salve", 1, 2)]
    [InlineData("beacon", 1, 3)]
    public void TheFieldSurgeonLosesReachToTheWardenOnEveryHeal(string spell, int surgeon, int warden)
    {
        Assert.Equal(surgeon, Shipped.WeaponOf(Ready() with { ClassId = "fieldsurgeon" }, Shipped.Weapon(spell)).MaxRange);
        Assert.Equal(warden, Shipped.WeaponOf(Ready() with { ClassId = "warden" }, Shipped.Weapon(spell)).MaxRange);
    }

    [Fact]
    public void EveryUniqueClassLosesItsNamedMeasureToTheStandardForm()
    {
        foreach (var unique in Shipped.Classes.Values.Where(c => c.Unique is not null))
        {
            var standard = Shipped.Classes.Values.Single(c => c.Advances?.Id == unique.Advances!.Id && c.Unique is null);
            var unit = Shipped.Cast.Single(u => u.Id == unique.Unique) with { Skill = Enum.GetValues<WeaponType>().Aggregate(WeaponSkill.Zero, (skill, type) => skill.With(type, WeaponRanks.Threshold(WeaponRank.A))) };

            Assert.True(Sidegrades.Measure(unit, unique, Shipped, unique.Loses!.Value) < Sidegrades.Measure(unit, standard, Shipped, unique.Loses.Value), unique.Id);
        }
    }

    /// <summary>Rook from the cast as a Skyrider at level 7 with Lance C, ready for either door.</summary>
    private static Unit ReadyRook() =>
        Shipped.Cast.Single(u => u.Id == "rook") with
        {
            Level = 7,
            Stats = Shipped.Cast.Single(u => u.Id == "rook").Stats with { Spd = 9 },
            Skill = WeaponSkill.Zero.With(WeaponType.Lance, WeaponRanks.Threshold(WeaponRank.C)),
        };

    [Fact]
    public void TheScoutIsRooksOtherDoorAboveTheSkyrider()
    {
        var scout = Shipped.Class("scout");

        Assert.Equal("skyrider", scout.Advances?.Id);
        Assert.Equal("rook", scout.Unique);
        Assert.Equal("rook_1", scout.UnlockedBy);
        Assert.Equal(SidegradeMeasure.Damage, scout.Loses);
        Assert.Equal(Shipped.Class("skycaptain").Certification, scout.Certification);
        Assert.Equal(Shipped.Class("skycaptain").Mov, scout.Mov);
        Assert.Equal(1000, Shipped.Campaign.SealFor(scout));
    }

    [Fact]
    public void TheScoutStaysShutUntilRooksQuestIsWon()
    {
        Assert.DoesNotContain(Shipped.Campaign.Quests, q => q.Id == "rook_1");
        Assert.Equal(new[] { "unlockedBy" }, Refusals(ReadyRook(), "scout", Won));
        Assert.Empty(Refusals(ReadyRook(), "scout", new[] { "rook_1" }));
        Assert.Empty(Refusals(ReadyRook(), "skycaptain", Array.Empty<string>()));
    }

    [Fact]
    public void TakingTheScoutClosesTheSkyCaptainForGood()
    {
        var scout = Certifications.Certify(ReadyRook(), Shipped.Class("scout"), false, new[] { "rook_1" });
        var back = Certifications.Certify(scout, Shipped.Class("skyrider"));

        Assert.Equal(new[] { "door" }, Refusals(back, "skycaptain", new[] { "rook_1" }));
    }

    [Fact]
    public void TheScoutGrowsTenLessStrThanTheSkyCaptain()
    {
        Assert.Equal(Shipped.Class("skycaptain").GrowthModifiers.Str - 10, Shipped.Class("scout").GrowthModifiers.Str);
        Assert.Equal(Shipped.Class("skycaptain").GrowthModifiers with { Str = 0 }, Shipped.Class("scout").GrowthModifiers with { Str = 0 });
    }

    /// <summary>The sidegrade test (issue 706): with every lance the Scout strikes for 1 less than the Sky Captain, the Str modifier it gives up.</summary>
    [Theory]
    [InlineData("iron_lance")]
    [InlineData("steel_lance")]
    public void TheScoutLosesDamageToTheSkyCaptainWithEveryLance(string lance)
    {
        var rook = ReadyRook() with { Skill = WeaponSkill.Zero.With(WeaponType.Lance, WeaponRanks.Threshold(WeaponRank.A)) };
        int Damage(string classId) => Shipped.StatsOf(rook with { ClassId = classId }).Str + Shipped.WeaponOf(rook with { ClassId = classId }, Shipped.Weapon(lance)).Mt;

        Assert.Equal(Damage("skycaptain") - 1, Damage("scout"));
        Assert.Equal(Sidegrades.Measure(rook, Shipped.Class("skycaptain"), Shipped, SidegradeMeasure.Damage) - 1, Sidegrades.Measure(rook, Shipped.Class("scout"), Shipped, SidegradeMeasure.Damage));
    }

    [Fact]
    public void TheScoutsEyesAreData()
    {
        var abilities = Shipped.AbilitiesOf(ReadyRook() with { ClassId = "scout" });

        Assert.Equal(2, AbilityRules.ExtraSight(abilities));
        Assert.Equal(6, AbilityRules.HeadcountRadius(abilities));
        Assert.Equal(0, AbilityRules.ExtraSight(Shipped.AbilitiesOf(ReadyRook() with { ClassId = "skycaptain" })));
        Assert.Null(AbilityRules.HeadcountRadius(Shipped.AbilitiesOf(ReadyRook() with { ClassId = "skycaptain" })));
    }

    [Fact]
    public void SteadyHandsDoublesAHealAndHoldsItBesideTheSurgeon()
    {
        var hands = ValueList<Ability>.Of(Shipped.Ability("steady_hands"));
        var mending = ValueList<Ability>.Of(Shipped.Ability("far_mending"), Shipped.Ability("steady_hands"));

        Assert.Equal(2, AbilityRules.HealFactor(hands));
        Assert.Equal(1, AbilityRules.HealFactor(ValueList<Ability>.Empty));
        Assert.Equal(1, AbilityRules.Shape(Shipped.Weapon("beacon"), hands).MaxRange);
        Assert.Equal(1, AbilityRules.Shape(Shipped.Weapon("beacon"), mending).MaxRange);
        Assert.Same(Shipped.Weapon("iron_bow"), AbilityRules.Shape(Shipped.Weapon("iron_bow"), hands));
    }

    [Fact]
    public void TheFieldSurgeonHealsDoubleWhatTheChaplainHeals()
    {
        var unit = Ready();
        var terrain = Shipped.Terrain.Values.First();
        var chaplain = Shipped.CombatantOf(unit, null, terrain, 10);
        var surgeon = Shipped.CombatantOf(unit with { ClassId = "fieldsurgeon" }, null, terrain, 10);
        var salve = Shipped.Weapon("salve");

        Assert.Equal(2 * (surgeon.Stats.Mag / 2 + Ironwake.Core.Combat.HealBase + salve.HealBase), Ironwake.Core.Combat.Heal(surgeon, salve));
        Assert.Equal(chaplain.Stats.Mag / 2 + Ironwake.Core.Combat.HealBase + salve.HealBase, Ironwake.Core.Combat.Heal(chaplain, salve));
    }

    [Fact]
    public void TheFieldSurgeonNeverStrikes()
    {
        var unit = Ready() with { ClassId = "fieldsurgeon", Skill = WeaponSkill.Zero.With(WeaponType.Faith, WeaponRanks.Threshold(WeaponRank.A)) };
        var surgeon = Shipped.Class("fieldsurgeon");

        Assert.False(surgeon.CanStrikeWith(WeaponType.Faith));
        Assert.True(surgeon.CanHealWith(WeaponType.Faith));
        Assert.False(unit.CanWield(Shipped.Weapon("radiance"), surgeon));
        Assert.True(unit.CanWield(Shipped.Weapon("salve"), surgeon));
        Assert.True(unit.CanWield(Shipped.Weapon("radiance"), Shipped.Class("warden")));
    }

    private static readonly string MapPath = Path.Combine(Fixture.RealContentDirectory(), "maps", "saltmarsh_ford.map");

    /// <summary>Saltmarsh Ford's open south rows with the first player unit as a Field Surgeon at 2,8 carrying a Salve, and the second, hurt, at 3,8.</summary>
    private static BattleState Ward(out string healer, out string ally)
    {
        var state = BattleState.From(MapFiles.Load(MapPath, Shipped), Shipped, Shipped.Cast, 706);
        var first = state.UnitsOf(Side.Player).ElementAt(0);
        var unit = first.Unit with { ClassId = "fieldsurgeon", Inventory = new Inventory(ValueList<ItemStack>.Of(new ItemStack("salve", 8))), Skill = WeaponSkill.Zero.With(WeaponType.Faith, WeaponRanks.Threshold(WeaponRank.C)) };
        state = state.WithUnit(first with { Unit = unit, At = new Coord(2, 8), Hp = Shipped.StatsOf(unit).Hp });
        var second = state.UnitsOf(Side.Player).ElementAt(1);
        state = state.WithUnit(second with { At = new Coord(3, 8), Hp = second.Hp - 9 });
        healer = first.Id;
        ally = second.Id;
        return state;
    }

    [Fact]
    public void WardRoundsMovesTheSurgeonAgainAfterAHeal()
    {
        var state = Ward(out var healer, out var ally);

        var result = Resolver.Apply(state, Shipped, new UseItem(healer, 0, ally));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.NotNull(result.Next.CantoReachOf(result.Next.Find(healer)!, Shipped));
        Assert.True(Resolver.Apply(result.Next, Shipped, new Canto(healer, new Coord(2, 9))).Accepted);
    }

    [Fact]
    public void WardRoundsOwesNothingAfterAWait()
    {
        var state = Ward(out var healer, out _);

        var result = Resolver.Apply(state, Shipped, new Wait(healer));

        Assert.True(result.Accepted);
        Assert.Null(result.Next.CantoReachOf(result.Next.Find(healer)!, Shipped));
    }

    [Fact]
    public void TheSurgeonsSalveDoesNotReachTwoTiles()
    {
        var state = Ward(out var healer, out var ally);
        state = state.WithUnit(state.Find(ally)! with { At = new Coord(4, 8) });

        var result = Resolver.Apply(state, Shipped, new UseItem(healer, 0, ally));

        Assert.Equal(RejectionReason.OutOfRange, result.Rejection!.Reason);
    }

    public static TheoryData<string, string, string, string> Malformed => new()
    {
        { "unique on a first-tier class", "chaplain", "unique", "a unique class is the other door" },
        { "unlockedBy without unique", "warden", "unlockedBy", "only a unique class names it" },
        { "no measure", "fieldsurgeon", "loses", "names the measure it loses on" },
        { "nobody", "fieldsurgeon", "unique", "'nobody' must be a cast member who is not the captain" },
        { "the captain", "fieldsurgeon", "unique", "must be a cast member who is not the captain" },
        { "another's quest", "fieldsurgeon", "unlockedBy", "'bet_postern' must be a quest in the campaign whose member is 'maud'" },
        { "heals and strikes only", "fieldsurgeon", "healOnly", "also strike-only" },
        { "another's unauthored quest", "scout", "unlockedBy", "or one of theirs not yet authored ('rook_<n>')" },
        { "another's authored quest", "scout", "unlockedBy", "'maud_1' must be a quest in the campaign whose member is 'rook'" },
    };

    [Theory]
    [MemberData(nameof(Malformed))]
    public void TheLoaderRefusesAMalformedUniqueClass(string change, string entry, string field, string problem)
    {
        var surgeon = Shipped.Class("fieldsurgeon");
        var changed = change switch
        {
            "unique on a first-tier class" => Shipped.Class("chaplain") with { Unique = "maud", Loses = SidegradeMeasure.Reach },
            "unlockedBy without unique" => Shipped.Class("warden") with { UnlockedBy = "maud_1" },
            "no measure" => surgeon with { Loses = null },
            "nobody" => surgeon with { Unique = "nobody" },
            "the captain" => surgeon with { Unique = Shipped.Cast[0].Id },
            "another's quest" => surgeon with { UnlockedBy = "bet_postern" },
            "another's unauthored quest" => Shipped.Class("scout") with { UnlockedBy = "teodor_9" },
            "another's authored quest" => Shipped.Class("scout") with { UnlockedBy = "maud_1" },
            _ => surgeon with { StrikeOnly = ValueList<WeaponType>.Of(WeaponType.Faith) },
        };

        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(ContentSerializer.Write(Shipped with { Classes = Shipped.Classes.SetItem(changed.Id, changed) })));

        Assert.Equal((ContentFiles.ClassesName, entry, field), (e.File, e.Entry, e.Field));
        Assert.Contains(problem, e.Message);
    }

    [Fact]
    public void AHealOnlyCantoRoundTripsThroughTheSerializer()
    {
        var reloaded = ContentLoader.Parse(ContentSerializer.Write(Shipped));

        Assert.Equal(Shipped.Class("fieldsurgeon"), reloaded.Class("fieldsurgeon"));
        Assert.Equal(Shipped.Ability("ward_rounds"), reloaded.Ability("ward_rounds"));
        Assert.Equal(Shipped.Ability("steady_hands"), reloaded.Ability("steady_hands"));
        Assert.Equal(Shipped.Class("scout"), reloaded.Class("scout"));
        Assert.Equal(Shipped.Ability("high_watch"), reloaded.Ability("high_watch"));
        Assert.Equal(Shipped.Ability("headcount"), reloaded.Ability("headcount"));
    }
}
