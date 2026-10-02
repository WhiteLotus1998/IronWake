using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Campaign;

/// <summary>
/// The campaign's enemy-level curve and template swaps (issue 704, rounds 223 to 226): a
/// <c>campaign.json</c> map's <c>enemyLevel</c> replaces its file's in the campaign only, and its
/// <c>swap</c> fields another template on a placement's tile, keeping the placement's group,
/// behaviour and boss flag. The standalone map file is never changed, and a difficulty's offset
/// is added on top of the curve.
/// </summary>
public class CampaignCurveTests
{
    private static readonly GameContent Content = MapFixture.Content;

    private static MapDefinition Map(string id) => MapFiles.Load(MapFiles.CampaignPath(Fixture.RealContentDirectory(), Content, id), Content);

    private static CampaignMap Entry(string id) => Content.Campaign.Maps.Single(m => m.MapId == id);

    private static ContentFiles With(string maps) =>
        Fixture.Files() with { Campaign = new ContentFile(ContentFiles.CampaignName, $$"""{ "startingPurse": 0, "certificationPrice": 0, "maps": [ {{maps}} ] }""") };

    [Fact]
    public void TheShippedCurveBendsHarrowWeirBackToItsFileAndRaisesTheRaid()
    {
        Assert.Equal(new int?[] { 1, 1, 2, 3, 2, 6, 7, 6, 8 }, Content.Campaign.Maps.Select(m => m.EnemyLevel));
    }

    [Fact]
    public void TheCampaignFightsAMapAtItsCurveLevel()
    {
        var map = Map("brackwater_cut");
        var fought = Entry("brackwater_cut").Prepare(map);

        Assert.Equal(3, map.EnemyLevel);
        Assert.Equal(6, fought.EnemyLevel);
        Assert.All(fought.Placements.OfType<EnemyPlacement>(), p => Assert.Equal(6, fought.EnemyUnit(p, Content).Level));
    }

    [Fact]
    public void TheCampaignsBattleIsFoughtOnThePreparedMap()
    {
        var record = CampaignRecord.Start(Content, 7) with { MapIndex = Content.Campaign.Maps.ToList().FindIndex(m => m.MapId == "sallow_grange") };

        var battle = record.Begin(Map("sallow_grange"), Content);

        Assert.Equal(7, battle.Map.EnemyLevel);
        Assert.Equal("halberdier", battle.UnitsOf(Side.Enemy).Single(u => u.At == new Coord(7, 5)).Unit.ClassId);
    }

    [Fact]
    public void ADifficultysOffsetIsAddedOnTopOfTheCurve()
    {
        var record = CampaignRecord.Start(Content, 7) with { MapIndex = Content.Campaign.Maps.ToList().FindIndex(m => m.MapId == "ironwake_raid"), Difficulty = "tactician" };
        var offset = Content.Difficulty("tactician").EnemyLevelOffset;

        var battle = record.Begin(Map("ironwake_raid"), Content);

        Assert.NotEqual(0, offset);
        Assert.Equal(6 + offset, battle.Map.EnemyLevel);
    }

    [Fact]
    public void AStandaloneMapIsUnchangedByTheCurve()
    {
        Assert.Equal(2, Map("ironwake_raid").EnemyLevel);
        Assert.Equal(3, Map("sallow_grange").EnemyLevel);
        Assert.Equal("soldier", Map("sallow_grange").Placements.OfType<EnemyPlacement>().Single(p => p.At == new Coord(7, 5)).TemplateId);
    }

    [Fact]
    public void ASwapFieldsTheNamedTemplateKeepingGroupBehaviourAndBoss()
    {
        var map = Map("sallow_grange");
        var before = map.Placements.OfType<EnemyPlacement>().Single(p => p.At == new Coord(7, 5));

        var after = Entry("sallow_grange").Prepare(map).Placements.OfType<EnemyPlacement>().Single(p => p.At == new Coord(7, 5));

        Assert.Equal(before with { TemplateId = "veteran" }, after);
    }

    [Fact]
    public void AMapWithNoCurvePointAndNoSwapsIsFoughtAsItsFileReads()
    {
        var map = Map("the_tollgate");

        Assert.Equal(map, new CampaignMap("the_tollgate", 0, ValueList<string>.Empty).Prepare(map));
    }

    [Fact]
    public void ASwapOnATileWithNoEnemyPlacementThrows()
    {
        var entry = new CampaignMap("sallow_grange", 0, ValueList<string>.Empty) { Swaps = ValueList<TemplateSwap>.Of(new TemplateSwap(new Coord(0, 0), "veteran")) };

        var e = Assert.Throws<InvalidOperationException>(() => entry.Prepare(Map("sallow_grange")));

        Assert.Contains("swap at 0,0 names a tile with no enemy placement", e.Message);
    }

    [Fact]
    public void EveryShippedSwapLandsOnAnEnemyPlacement()
    {
        foreach (var entry in Content.Campaign.Maps)
        {
            var fought = entry.Prepare(Map(entry.MapId));
            foreach (var swap in entry.Swaps)
            {
                Assert.Contains(fought.Placements, p => p is EnemyPlacement e && e.At == swap.At && e.TemplateId == swap.TemplateId);
            }
        }
    }

    [Fact]
    public void SallowFieldsOneTemplateAtMostAndNoMapBeforeItFieldsAny()
    {
        var sallow = Content.Campaign.Maps.ToList().FindIndex(m => m.MapId == "sallow_grange");

        Assert.True(Content.Campaign.Maps[sallow].Swaps.Count <= 1);
        Assert.All(Content.Campaign.Maps.Take(sallow), m => Assert.Empty(m.Swaps));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public void AnEnemyLevelOutsideTheLevelRangeIsRefused(int level)
    {
        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(With($$"""{ "map": "one", "reward": 0, "stock": [], "enemyLevel": {{level}} }""")));

        Assert.Equal(("one", "enemyLevel"), (e.Entry, e.Field));
    }

    [Fact]
    public void ASwapKeyThatIsNotATileIsRefused()
    {
        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(With("""{ "map": "one", "reward": 0, "stock": [], "swap": { "north": "recruit" } }""")));

        Assert.Equal(("one", "swap"), (e.Entry, e.Field));
        Assert.Contains("'north' is not a tile; expected x,y", e.Message);
    }

    [Fact]
    public void ASwapNamingNoTemplateIsRefused()
    {
        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(With("""{ "map": "one", "reward": 0, "stock": [], "swap": { "1,2": "nobody" } }""")));

        Assert.Equal(("one", "swap"), (e.Entry, e.Field));
        Assert.Contains("'nobody' is not a unit template", e.Message);
    }

    [Fact]
    public void ASwapNamingACastMemberIsRefused()
    {
        var captain = Content.Cast[0].Id;
        var files = ContentSerializer.Write(Content) with
        {
            Campaign = new ContentFile(ContentFiles.CampaignName, $$"""{ "startingPurse": 0, "certificationPrice": 0, "maps": [ { "map": "one", "reward": 0, "stock": [], "swap": { "1,2": "{{captain}}" } } ] }"""),
        };

        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(files));

        Assert.Equal(("one", "swap"), (e.Entry, e.Field));
        Assert.Contains("is in the cast, not an enemy template", e.Message);
    }

    [Fact]
    public void TheCurveAndSwapsRoundTripThroughTheSerializer()
    {
        var reloaded = ContentLoader.Parse(ContentSerializer.Write(Content));

        Assert.Equal(Content.Campaign.Maps.Select(m => (m.EnemyLevel, m.Swaps)), reloaded.Campaign.Maps.Select(m => (m.EnemyLevel, m.Swaps)));
    }
}
