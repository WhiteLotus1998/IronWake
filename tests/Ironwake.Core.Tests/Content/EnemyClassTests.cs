using Ironwake.Content;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// The enemy's caster classes (issue 1286 slice 5, DECISIONS/0319): <c>enemy: true</c> marks a class only the
/// enemy stands in. Nobody certifies, trials, is promoted or is cast into it, the camp lists leave it out, and it
/// is the only kind of class that reaches dark (DECISIONS/0317). Four ship under placeholder names, one per
/// school the company meets before it wields: ice, earth, lightning, and dark for the mini-boss and his followers.
/// </summary>
public class EnemyClassTests
{
    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    private const string WithEnemy = """
        { "classes": [
          { "id": "cadet", "name": "Cadet", "movement": "infantry", "mov": 4, "weapons": ["sword", "reason"] },
          { "id": "grave", "name": "Grave", "enemy": true, "description": "Placeholder. A test line.", "movement": "infantry", "mov": 4, "weapons": ["reason"], "schools": ["dark"] }
        ] }
        """;

    private static ContentException Fails(ContentFiles files) => Assert.Throws<ContentException>(() => ContentLoader.Parse(files));

    [Theory]
    [InlineData("frost_caster", MagicSchool.Ice)]
    [InlineData("earth_shaper", MagicSchool.Earth)]
    [InlineData("storm_caster", MagicSchool.Lightning)]
    [InlineData("grave_caller", MagicSchool.Dark)]
    public void EachEnemyCasterClassReachesItsOneSchoolAndIsMarkedAPlaceholder(string id, MagicSchool school)
    {
        var unitClass = Shipped.Class(id);

        Assert.True(unitClass.Enemy);
        Assert.Equal(ValueList<MagicSchool>.Of(school), unitClass.Schools);
        Assert.StartsWith("Placeholder.", unitClass.Description);
        Assert.Null(unitClass.Advances);
        Assert.Equal(CertificationRequirements.None, unitClass.Certification);
    }

    [Fact]
    public void OnlyTheGraveCallerReachesDarkAmongShippedClasses()
    {
        Assert.Equal(new[] { "grave_caller" }, Shipped.Classes.Values.Where(c => c.Reaches(MagicSchool.Dark)).Select(c => c.Id));
    }

    [Fact]
    public void NoCastMemberStandsInAnEnemyClass()
    {
        Assert.DoesNotContain(Shipped.Cast, u => Shipped.Class(u.ClassId).Enemy);
    }

    [Fact]
    public void AnEnemyClassLoadsAndRoundTrips()
    {
        var content = ContentLoader.Parse(Fixture.Files(classes: WithEnemy));

        Assert.True(content.Class("grave").Enemy);
        Assert.Equal("Placeholder. A test line.", content.Class("grave").Description);
        var written = ContentSerializer.Write(content);
        Assert.Equal(content, ContentLoader.Parse(written));
    }

    [Fact]
    public void ANonEnemyClassThatReachesDarkIsRefused()
    {
        var e = Fails(Fixture.Files(classes: WithEnemy.Replace("\"enemy\": true, ", "")));

        Assert.Equal((ContentFiles.ClassesName, "grave", "schools"), (e.File, e.Entry, e.Field));
        Assert.Contains("only an enemy class reaches dark", e.Message);
    }

    [Theory]
    [InlineData("\"hidden\": true, ", "hidden")]
    [InlineData("\"captain\": true, ", "captain")]
    [InlineData("\"advances\": \"cadet\", ", "advances")]
    [InlineData("\"certification\": { \"level\": 3 }, ", "certification")]
    [InlineData("\"unique\": \"recruit\", ", "unique")]
    public void AnEnemyClassNamingAWayInIsRefused(string field, string name)
    {
        var e = Fails(Fixture.Files(classes: WithEnemy.Replace("\"enemy\": true, ", "\"enemy\": true, " + field)));

        Assert.Equal(("grave", name), (e.Entry, e.Field));
        Assert.Contains("an enemy class is first-tier and nobody joins it", e.Message);
    }

    [Fact]
    public void AClassAdvancingFromAnEnemyClassIsRefused()
    {
        var classes = WithEnemy.Replace("\n] }", ",\n  { \"id\": \"lich\", \"name\": \"Lich\", \"movement\": \"infantry\", \"mov\": 4, \"weapons\": [\"reason\"], \"advances\": \"grave\" }\n] }");

        var e = Fails(Fixture.Files(classes: classes));

        Assert.Equal(("lich", "advances"), (e.Entry, e.Field));
        Assert.Contains("'grave' is an enemy class; nobody is promoted out of it", e.Message);
    }

    [Fact]
    public void ABlankDescriptionIsRefused()
    {
        var e = Fails(Fixture.Files(classes: WithEnemy.Replace("Placeholder. A test line.", " ")));

        Assert.Equal(("grave", "description"), (e.Entry, e.Field));
    }

    [Fact]
    public void ACastMemberInAnEnemyClassIsRefused()
    {
        var cast = Fixture.Units.Replace("\"recruit\"", "\"hero\"").Replace("\"class\": \"cadet\"", "\"class\": \"grave\"");

        var e = Fails(Fixture.Files(classes: WithEnemy, cast: cast));

        Assert.Equal((ContentFiles.CastName, "hero", "class"), (e.File, e.Entry, e.Field));
        Assert.Contains("'grave' is an enemy class; no cast member stands in it", e.Message);
    }

    [Fact]
    public void AnEnemyTemplateMayStandInAnEnemyClass()
    {
        var units = Fixture.Units.Replace("\"class\": \"cadet\"", "\"class\": \"grave\"").Replace("[ { \"item\": \"iron_sword\" } ]", "[]");

        var content = ContentLoader.Parse(Fixture.Files(classes: WithEnemy, units: units));

        Assert.Equal("grave", content.Unit("recruit").ClassId);
    }

    [Fact]
    public void CertifyingIntoAnEnemyClassIsRefusedWithItsOwnReason()
    {
        var pell = Shipped.Cast.Single(u => u.Id == "pell") with { Level = 20 };

        var refusals = Certifications.Check(pell, Shipped.Class("grave_caller"));

        Assert.Contains(refusals, r => r.Requirement == "enemy" && r.Text.Contains("Grave Caller is an enemy's class"));
    }

    [Fact]
    public void ATrialIntoAnEnemyClassIsRefused()
    {
        var campaign = """{ "startingPurse": 500, "certificationPrice": 500, "maps": [ { "map": "one", "reward": 0, "stock": [] } ], "trials": { "grave": "grave_trial" } }""";

        var e = Fails(Fixture.Files(classes: WithEnemy) with { Campaign = new ContentFile(ContentFiles.CampaignName, campaign) });

        Assert.Equal((ContentFiles.CampaignName, "trials.grave"), (e.File, e.Field));
        Assert.Contains("is an enemy class; nobody trials into it", e.Message);
    }

    [Fact]
    public void AGraveCallerOnADropsTileSendsItsGrimoireToTheWagonWhenItDies()
    {
        var cinder = Shipped.Weapon("cinder");
        var hexer = Shipped.Unit("hexer");
        var content = Shipped with
        {
            Weapons = Shipped.Weapons.SetItem("test_dark_grimoire", cinder with { Id = "test_dark_grimoire", Name = "Test Dark Grimoire", School = MagicSchool.Dark, Rider = RiderKind.Drain, Ignites = false, MinMag = 8 }),
            Units = Shipped.Units.SetItem("hexer", hexer with { ClassId = "grave_caller", Inventory = Inventory.Empty.Add(new ItemStack("test_dark_grimoire", 4)) }),
        };
        var text = File.ReadAllText(Path.Combine(Fixture.RealContentDirectory(), "maps", "starting_alone.map")).Replace("enemy_level: 1\n", "enemy_level: 1\ndrops: 8,2\n");
        var map = MapFormat.Parse("drops.map", text, content);

        for (ulong seed = 1; seed < 200; seed++)
        {
            var state = BattleState.From(map, content, content.Cast, seed);
            var caller = state.Units.Single(u => u.Unit.ClassId == "grave_caller");
            state = state.WithUnit(caller with { Hp = 1 }).WithUnit(state.Find("captain")! with { At = new Coord(8, 3) });
            var result = Resolver.Apply(state, content, new Attack("captain", caller.Id));
            if (result.Next.Find(caller.Id) is null)
            {
                Assert.Contains(new TomeDropped(caller.Id, ValueList<string>.Of("test_dark_grimoire")), result.Events);
                Assert.Equal(new[] { "test_dark_grimoire" }, result.Next.Wagon);
                return;
            }
        }

        throw new InvalidOperationException("no seed under 200 killed the grave caller");
    }
}
