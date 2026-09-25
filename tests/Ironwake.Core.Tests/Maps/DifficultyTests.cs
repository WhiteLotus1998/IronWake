using System.Collections.Immutable;
using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Maps;

/// <summary>
/// Difficulty as pure data (issue 76, DESIGN.md section 9): a stat percent, an enemy level
/// offset and a Recall charge override in rules.json, applied to a map once by
/// <see cref="MapDefinition.Under"/> and to every enemy through <see cref="MapDefinition.EnemyUnit"/>.
/// Normal is the identity entry of the shipped file. The Hard entry here is a fixture: its
/// numbers wait on the Table and are not content.
/// </summary>
public class DifficultyTests
{
    /// <summary>Hp 150, Str 120, Spd 125 percent, two levels above the map's floor, one Recall.</summary>
    private static readonly Difficulty Hard = new("hard", Difficulty.FullPercent with { Hp = 150, Str = 120, Spd = 125 }, 2, 1);

    private static readonly GameContent WithHard = MapFixture.Content with
    {
        Difficulties = MapFixture.Content.Difficulties.Add(Hard.Id, Hard),
    };

    private static string Rules(string difficulties) => "{ \"wakeRadius\": 4, \"difficulties\": { " + difficulties + " } }";

    private static ContentException Fails(string difficulties) =>
        Assert.Throws<ContentException>(() => ContentLoader.Parse(Fixture.Files(rules: Rules(difficulties))));

    [Fact]
    public void TheShippedRulesDeclareNormalAsTheIdentity()
    {
        var normal = MapFixture.Content.Difficulty(Difficulty.NormalId);

        Assert.True(normal.IsIdentity);
        Assert.Equal(Difficulty.FullPercent, normal.StatPercent);
        Assert.Equal(0, normal.EnemyLevelOffset);
        Assert.Null(normal.RecallCharges);
    }

    /// <summary>
    /// The acceptance's identity test: every shipped map under Normal starts a battle whose
    /// units, enemy stats included, are the same as the map's as authored, with the same
    /// header numbers, and every spawn's enemy is the same too.
    /// </summary>
    [Fact]
    public void NormalIsTheIdentityOnEveryShippedMap()
    {
        var content = MapFixture.Content;
        var normal = content.Difficulty(Difficulty.NormalId);
        foreach (var (id, map) in MapFiles.LoadAll(Fixture.RealContentDirectory(), content))
        {
            var under = map.Under(normal);

            Assert.Equal(map.EnemyLevel, under.EnemyLevel);
            Assert.Equal(map.RecallCharges, under.RecallCharges);
            Assert.Equal(
                BattleState.From(map, content, content.Cast, 7).Units,
                BattleState.From(under, content, content.Cast, 7).Units);
            foreach (var spawn in map.Spawns())
            {
                Assert.Equal(map.EnemyUnit(spawn, content), under.EnemyUnit(spawn, content));
            }

            Assert.Equal(Difficulty.NormalId, under.DifficultyId);
            Assert.True(id.Length > 0);
        }
    }

    /// <summary>
    /// A soldier on a level-3 map under the fixture Hard: the offset raises the floor to 5,
    /// which gives section 3's level-5 soldier (21 7 0 5 6 2 4 1 2), and the percents then
    /// give Hp 31.5 to 32, Str 8.4 to 8, Spd 7.5 to 8, the rest as they were.
    /// </summary>
    [Fact]
    public void AHardPercentAndOffsetReachEnemyUnitWithTheRecordsNumbers()
    {
        var map = MapFixture.Parse(MapFixture.OldMillRoad.Replace("enemy_level: 1", "enemy_level: 3")).Under(Hard);
        var soldier = map.Placements.OfType<EnemyPlacement>().First(p => p.TemplateId == "soldier");

        var unit = map.EnemyUnit(soldier, WithHard);

        Assert.Equal(5, map.EnemyLevel);
        Assert.Equal(5, unit.Level);
        Assert.Equal(new Stats(32, 8, 0, 5, 8, 2, 4, 1, 2), unit.Stats);
    }

    [Fact]
    public void ABattleUnderHardFieldsTheScaledEnemiesAndTheDifficultysRecallCharges()
    {
        var map = MapFixture.Parse(MapFixture.OldMillRoad.Replace("enemy_level: 1", "enemy_level: 3")).Under(Hard);

        var state = BattleState.From(map, WithHard, WithHard.Cast, 1);

        Assert.Equal(1, state.RecallCharges);
        Assert.Equal(new Stats(32, 8, 0, 5, 8, 2, 4, 1, 2), state.Find("soldier-1")!.Unit.Stats);
        Assert.Equal(WithHard.StatsOf(state.Find("soldier-1")!.Unit).Hp, state.Find("soldier-1")!.Hp);
        Assert.Equal(WithHard.Cast[0].Stats, state.Find("captain")!.Unit.Stats);
    }

    [Fact]
    public void ADifficultyWithoutRecallChargesKeepsTheMapsOwn()
    {
        var map = MapFixture.Parse(MapFixture.OldMillRoad.Replace("recall: 3", "recall: 2"));

        Assert.Equal(2, map.Under(Hard with { RecallCharges = null }).RecallCharges);
        Assert.Equal(0, map.Under(Hard with { RecallCharges = 0 }).RecallCharges);
    }

    [Theory]
    [InlineData(1, 29, 30)]
    [InlineData(20, 15, 30)]
    [InlineData(5, -29, 1)]
    [InlineData(5, -2, 3)]
    public void TheLevelOffsetIsHeldToTheLevelRange(int floor, int offset, int expected)
    {
        var map = MapFixture.Parse(MapFixture.OldMillRoad.Replace("enemy_level: 1", "enemy_level: " + floor));

        Assert.Equal(expected, map.Under(Hard with { EnemyLevelOffset = offset }).EnemyLevel);
    }

    [Fact]
    public void AMapAlreadyUnderADifficultyRefusesASecond()
    {
        var map = MapFixture.Parse(MapFixture.OldMillRoad).Under(Hard);

        var e = Assert.Throws<InvalidOperationException>(() => map.Under(Hard));

        Assert.Contains("already under difficulty 'hard'", e.Message);
    }

    [Theory]
    [InlineData(7, 120, 8)]
    [InlineData(15, 110, 17)]
    [InlineData(5, 50, 3)]
    [InlineData(4, 50, 2)]
    [InlineData(0, 150, 0)]
    [InlineData(9, 100, 9)]
    [InlineData(-3, 150, -5)]
    public void AStatScalesToTheNearestWholeNumberHalfAwayFromZero(int value, int percent, int expected)
    {
        Assert.Equal(expected, Difficulty.Scale(value, percent));
    }

    [Fact]
    public void AnEnemysHpNeverFallsBelowOne()
    {
        var soldier = MapFixture.Content.Unit("soldier");
        var weakened = new Difficulty("x", Difficulty.FullPercent with { Hp = 0, Str = 0 }, 0, null);

        var unit = weakened.Apply(soldier);

        Assert.Equal(1, unit.Stats.Hp);
        Assert.Equal(0, unit.Stats.Str);
    }

    /// <summary>A map event's spawn goes through the same method, so a rider arriving mid-battle is under the difficulty too.</summary>
    [Fact]
    public void ASpawnedEnemyIsUnderTheDifficulty()
    {
        var map = MapFiles.LoadAll(Fixture.RealContentDirectory(), WithHard).Single(m => m.Id == "the_tollgate").Map;
        var rider = map.Spawns().Single();

        var authored = map.EnemyUnit(rider, WithHard);
        var hard = map.Under(Hard).EnemyUnit(rider, WithHard);

        Assert.Equal(Difficulty.Scale(authored.AtLevel(authored.Level + 2, WithHard.Class(authored.ClassId)).Stats.Hp, 150), hard.Stats.Hp);
    }

    [Fact]
    public void TheDifficultyHeaderRoundTripsAndABattleStateReadsBackUnderIt()
    {
        var map = MapFixture.Parse(MapFixture.OldMillRoad).Under(Hard);
        var text = MapFormat.Write(map, WithHard);

        Assert.Contains("recall: 1\nenemy_level: 3\n", text);
        Assert.Contains("difficulty: hard\n", text);
        Assert.Equal(map, MapFormat.Parse("test.map", text, WithHard));

        var state = BattleState.From(map, WithHard, WithHard.Cast, 5);
        var back = ProtocolJson.ReadState(ProtocolJson.State(state, WithHard), WithHard);
        Assert.Equal(state, back);
        Assert.Equal("hard", back.Map.DifficultyId);
    }

    [Fact]
    public void AnUnknownDifficultyHeaderIsRefusedNamingTheLineAndTheKnownOnes()
    {
        var text = MapFixture.OldMillRoad.Replace("enemy_level: 1\n", "enemy_level: 1\ndifficulty: brutal\n");

        var e = Assert.Throws<MapException>(() => MapFixture.Parse(text));

        Assert.Equal(7, e.Line);
        Assert.Contains("difficulty names 'brutal'; the difficulties are normal", e.Message);
    }

    [Fact]
    public void AContentMapDeclaringADifficultyIsRefusedNamingItsFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "ironwake-difficulty-" + Guid.NewGuid().ToString("N"));
        var maps = Path.Combine(root, MapFiles.MapsDirectory);
        Directory.CreateDirectory(maps);
        try
        {
            var path = Path.Combine(maps, "declared.map");
            File.WriteAllText(path, MapFixture.OldMillRoad.Replace("enemy_level: 1\n", "enemy_level: 1\ndifficulty: normal\n"));

            var e = Assert.Throws<MapException>(() => MapFiles.LoadAll(root, MapFixture.Content));

            Assert.Contains("declared.map", e.Message);
            Assert.Contains("never per map", e.Message);
            Assert.Equal("normal", MapFiles.Load(path, MapFixture.Content).DifficultyId);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AWellFormedBlockLoadsWithOmittedPercentsAt100()
    {
        var content = ContentLoader.Parse(Fixture.Files(rules: Rules("\"normal\": {}, \"hard\": { \"statPercent\": { \"hp\": 150, \"str\": 120 }, \"enemyLevelOffset\": 2, \"recall\": 1 }")));

        Assert.Equal(new Difficulty("hard", new Stats(150, 120, 100, 100, 100, 100, 100, 100, 100), 2, 1), content.Difficulty("hard"));
        Assert.True(content.Difficulty("normal").IsIdentity);
    }

    [Fact]
    public void ContentWithoutABlockHasNoDifficulties()
    {
        Assert.Empty(ContentLoader.Parse(Fixture.Files()).Difficulties);
    }

    [Fact]
    public void ABlockWithoutNormalIsRefused()
    {
        var e = Fails("\"hard\": { \"enemyLevelOffset\": 2 }");

        Assert.Equal("difficulties", e.Field);
        Assert.Contains("must declare 'normal'", e.Message);
    }

    [Theory]
    [InlineData("\"statPercent\": { \"def\": 110 }")]
    [InlineData("\"enemyLevelOffset\": 1")]
    [InlineData("\"recall\": 3")]
    public void ANormalThatIsNotTheIdentityIsRefused(string fields)
    {
        var e = Fails("\"normal\": { " + fields + " }");

        Assert.Equal("difficulties.normal", e.Entry);
        Assert.Contains("must be the identity", e.Message);
    }

    [Theory]
    [InlineData("\"statPercent\": { \"def\": -1 }", "statPercent.def")]
    [InlineData("\"statPercent\": { \"might\": 110 }", "might")]
    [InlineData("\"enemyLevelOffset\": 30", "enemyLevelOffset")]
    [InlineData("\"enemyLevelOffset\": -30", "enemyLevelOffset")]
    [InlineData("\"recall\": 100", "recall")]
    [InlineData("\"recall\": -1", "recall")]
    [InlineData("\"charges\": 1", "charges")]
    public void ABadFieldIsRefusedNamingTheEntryAndField(string fields, string field)
    {
        var e = Fails("\"normal\": {}, \"hard\": { " + fields + " }");

        Assert.Equal("difficulties.hard", e.Entry);
        Assert.Equal(field, e.Field);
    }

    [Fact]
    public void TheBlockRoundTripsThroughTheSerializer()
    {
        var content = ContentLoader.Parse(Fixture.Files(rules: Rules("\"normal\": {}, \"hard\": { \"statPercent\": { \"hp\": 150 }, \"enemyLevelOffset\": -1, \"recall\": 2 }, \"soft\": { \"statPercent\": { \"str\": 80 } }")));

        var written = ContentSerializer.Write(content);
        var reloaded = ContentLoader.Parse(written);

        Assert.Equal(content.Difficulties, reloaded.Difficulties);
        Assert.Equal(written.Rules.Text, ContentSerializer.Write(reloaded).Rules.Text);
        Assert.Null(reloaded.Difficulty("soft").RecallCharges);
    }

    [Fact]
    public void TheShippedRulesRoundTripThroughTheSerializer()
    {
        var reloaded = ContentLoader.Parse(ContentSerializer.Write(MapFixture.Content));

        Assert.Equal(MapFixture.Content.Difficulties, reloaded.Difficulties);
        Assert.Equal(MapFixture.Content.Rivalry, reloaded.Rivalry);
    }

    /// <summary>Gate 6 on Hard: the same seed and commands replay byte-identical on every shipped map under the fixture Hard.</summary>
    [Fact]
    public void DeterminismGate6PassesOnHard()
    {
        var maps = MapFiles.LoadAll(Fixture.RealContentDirectory(), WithHard).Select(m => (m.Id, m.Map.Under(Hard))).ToList();

        var gate = Ironwake.Sim.Program.Gate6(WithHard, maps, new Ironwake.Sim.Gates.ForecastTally());

        Assert.True(gate.Passed, gate.Line);
    }

    /// <summary>Gates 1 and 2 run on a map under Hard through the same entry points, with the map file unchanged.</summary>
    [Fact]
    public void Gates1And2RunOnHard()
    {
        var map = MapFiles.LoadAll(Fixture.RealContentDirectory(), WithHard).Single(m => m.Id == "old_mill_road").Map.Under(Hard);

        var (gate1, games) = Ironwake.Sim.Gates.Gate1(WithHard, map, "old_mill_road", 3);
        var gate2 = Ironwake.Sim.Gates.Gate2(WithHard, map, "old_mill_road", 3);

        Assert.Equal(3, games.Count);
        Assert.StartsWith("gate 1 ", gate1.Line);
        Assert.StartsWith("gate 2 ", gate2.Line);
    }
}
