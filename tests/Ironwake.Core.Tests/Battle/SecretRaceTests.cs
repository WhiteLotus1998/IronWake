using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The secret path's flag (issue 1386 slice 2b, STORY's Under the Hill): a <c>shard_race: ... secret</c> header holds
/// the race and its <c>race</c> events apart until a campaign whose record meets the conditions arms it; an armed race
/// starts only while the enemy the map's <c>freed:</c> header binds stands; a taken shard's coma carries to the record
/// and the ending.
/// </summary>
public class SecretRaceTests
{
    private const string Hall = """
        name: Hall
        size: 10x5
        win: defeat_boss
        shard_race: 9,2 3 secret
        turn_limit: 10
        recall: 3
        enemy_level: 1

        ..........
        ..........
        ..........
        ..........
        ..........

        units:
        P captain 3,2
        P recruit:wren 1,2
        P recruit:ivo 0,0
        B hask_warden 4,2 group:lord behavior:boss
        E soldier 7,0 group:lord behavior:hold

        events:
        guard race 0 spawn soldier 8,2 group:shard behavior:hold
        """;

    private static GameContent Real => ContentLoader.Load(Fixture.RealContentDirectory());

    private static BattleState Start(MapDefinition map) =>
        BattleState.From(map, Starter, ValueList<Unit>.Of(Hale, Wren, Ivo), 1386);

    private static BattleUnit Hask(BattleState state) => state.Units.Single(u => u.Unit.ClassId == "iron_warden");

    private static (BattleState Next, List<GameEvent> Events) Felled(BattleState state)
    {
        var events = new List<GameEvent>();
        var next = Swallow.Take(state.WithUnit(Hask(state) with { Hp = 0 }), Starter, Hask(state).Id, events);
        return (next, events);
    }

    [Fact]
    public void ASecretRaceIsHeldApartWithItsEventsAndRoundTrips()
    {
        var map = MapFixture.Parse(Hall, "hall.map");

        Assert.Null(map.ShardRace);
        Assert.Equal(new ShardRace(new Coord(9, 2), 3, Secret: true), map.SecretRace!.Race);
        Assert.Equal("guard", Assert.Single(map.SecretRace.Events).Name);
        Assert.DoesNotContain(map.Events, e => e.Trigger is RaceTrigger);
        var text = MapFormat.Write(map, Starter);
        Assert.Contains("shard_race: 9,2 3 secret\n", text);
        Assert.Equal(map, MapFixture.Parse(text, "hall.map"));
    }

    [Fact]
    public void AnArmedSecretRaceIsOnTheBoardAndRoundTripsArmed()
    {
        var armed = MapFixture.Parse(Hall, "hall.map").ArmSecretRace();

        Assert.Null(armed.SecretRace);
        Assert.Equal(new ShardRace(new Coord(9, 2), 3, Secret: true), armed.ShardRace);
        Assert.Contains(armed.Events, e => e.Name == "guard");
        var text = MapFormat.Write(armed, Starter);
        Assert.Contains("shard_race: 9,2 3 armed\n", text);
        Assert.Equal(armed, MapFixture.Parse(text, "hall.map"));
    }

    [Fact]
    public void AnUnarmedSecretRaceRunsNothingTheFallSwallowsAtOnce()
    {
        var (state, events) = Felled(Start(MapFixture.Parse(Hall, "hall.map")));

        Assert.DoesNotContain(events, e => e is ShardRaceBegan);
        Assert.True(Hask(state).Swallowed);
    }

    [Fact]
    public void AnArmedSecretRaceRunsWhileTheBoundEnemyStands()
    {
        var (state, events) = Felled(Start(MapFixture.Parse(Hall, "hall.map").ArmSecretRace()));

        Assert.Contains(events, e => e is ShardRaceBegan);
        Assert.Equal(new Coord(9, 2), Hask(state).At);
        Assert.Contains(events, e => e is UnitSpawned { Group: "shard" } spawned && spawned.At == new Coord(8, 2));
    }

    [Fact]
    public void AnArmedSecretRaceDoesNotRunOnceTheBoundEnemyWasKilled()
    {
        var start = Start(MapFixture.Parse(Hall, "hall.map").ArmSecretRace()) with { Bond = BondFate.Fell };
        var (state, events) = Felled(start);

        Assert.DoesNotContain(events, e => e is ShardRaceBegan);
        Assert.True(Hask(state).Swallowed);
    }

    [Fact]
    public void APlainRaceIgnoresTheBond()
    {
        var plain = MapFixture.Parse(Hall.Replace(" secret", ""), "hall.map");
        var (_, events) = Felled(Start(plain) with { Bond = BondFate.Fell });

        Assert.Contains(events, e => e is ShardRaceBegan);
    }

    [Fact]
    public void TheCampaignKeepHoldsItsRaceSecret()
    {
        var content = Real;
        var keep = MapFiles.Load(MapFiles.CampaignPath(Fixture.RealContentDirectory(), content, "ironwake_keep"), content);

        Assert.Null(keep.ShardRace);
        Assert.Equal(new ShardRace(new Coord(15, 6), 5, Secret: true), keep.SecretRace!.Race);
        Assert.DoesNotContain(keep.Events, e => e.Trigger is RaceTrigger);
        var secret = content.Campaign.Maps.Single(m => m.MapId == "ironwake_keep").Secret!;
        Assert.Equal(("keziah", "pell_2"), (secret.Bearer, secret.Quest));
        Assert.Contains(content.Campaign.Quests, q => q.Id == "pell_2" && q.MemberId == "pell" && q.Part == 2);
    }

    /// <summary>A record at the keep meeting the three conditions read before it: Keziah's scythe woken, Rook turned and standing, Pell's second won.</summary>
    private static CampaignRecord AtTheKeepUnderTheHill(GameContent content)
    {
        var record = CampaignRecord.StartAt(content, 41, "ironwake_keep", pick: "keziah");
        var keziah = record.Find("keziah")!;
        var items = keziah.Inventory.Items.Select(s => s.ItemId == Kinsbane.ItemId ? s with { Fed = Kinsbane.WakeKills } : s);
        var roster = record.Roster.Select(u => u.Id == "keziah" ? u with { Inventory = new Inventory(ValueList<ItemStack>.From(items)) } : u)
            .Append(content.Unit("rook"));
        return record with
        {
            Roster = ValueList<Unit>.From(roster),
            Returned = ClaimantFate.Turned,
            QuestsWon = ValueList<QuestWon>.Of(new QuestWon("pell_2", 6)),
        };
    }

    [Fact]
    public void UnderTheHillNeedsEveryConditionTheRecordHolds()
    {
        var content = Real;
        var met = AtTheKeepUnderTheHill(content);
        Assert.True(met.UnderTheHill(content));

        var asleep = met with
        {
            Roster = ValueList<Unit>.From(met.Roster.Select(u => u.Id == "keziah"
                ? u with { Inventory = new Inventory(ValueList<ItemStack>.From(u.Inventory.Items.Select(s => s.ItemId == Kinsbane.ItemId ? s with { Fed = Kinsbane.WakeKills - 1 } : s))) }
                : u)),
        };
        Assert.False(asleep.UnderTheHill(content));
        Assert.False((met with { Returned = ClaimantFate.Spared }).UnderTheHill(content));
        Assert.False((met with { Roster = ValueList<Unit>.From(met.Roster.Where(u => u.Id != "rook")) }).UnderTheHill(content));
        Assert.False((met with { QuestsWon = ValueList<QuestWon>.Empty }).UnderTheHill(content));
        Assert.False((met with { MapIndex = met.MapIndex - 1 }).UnderTheHill(content));
    }

    [Fact]
    public void TheKeepBattleIsArmedOnlyUnderTheHill()
    {
        var content = Real;
        var keep = MapFiles.Load(MapFiles.CampaignPath(Fixture.RealContentDirectory(), content, "ironwake_keep"), content);
        var met = AtTheKeepUnderTheHill(content);

        var armed = met.Begin(keep, content);
        Assert.Equal(new ShardRace(new Coord(15, 6), 5, Secret: true), armed.Map.ShardRace);
        Assert.Contains(armed.Map.Events, e => e.Trigger is RaceTrigger);

        var plain = (met with { QuestsWon = ValueList<QuestWon>.Empty }).Begin(keep, content);
        Assert.Null(plain.Map.ShardRace);
        Assert.DoesNotContain(plain.Map.Events, e => e.Trigger is RaceTrigger);
    }

    [Fact]
    public void TheSecretPathsBearerMustBeInTheCast()
    {
        var dir = Fixture.CopyRealContent();
        var path = Path.Combine(dir, "campaign.json");
        File.WriteAllText(path, File.ReadAllText(path).Replace("\"bearer\": \"keziah\"", "\"bearer\": \"nobody\""));

        var e = Assert.Throws<ContentException>(() => ContentLoader.Load(dir));
        Assert.Contains("secret.bearer", e.Message);
        Assert.Contains("'nobody' is not in the cast", e.Message);
    }

    [Fact]
    public void AWonMapsComaJoinsTheRecord()
    {
        var content = Real;
        var keep = MapFiles.Load(MapFiles.CampaignPath(Fixture.RealContentDirectory(), content, "ironwake_keep"), content);
        var met = AtTheKeepUnderTheHill(content);
        var opening = met.Begin(keep, content);
        var end = opening with
        {
            Units = ValueList<BattleUnit>.From(opening.UnitsOf(Side.Player)),
            Fired = ValueList<string>.From(opening.Map.BossSpawns().Select(e => e.Name)),
            Coma = ValueList<string>.Of("hask"),
        };

        Assert.Equal(BattleResult.Won, end.Outcome.Result);
        Assert.Equal(new[] { "hask" }, met.Fought(end, content).Coma);
        Assert.Empty(met.Fought(end with { Coma = ValueList<string>.Empty }, content).Coma);
    }

    [Fact]
    public void TheComaCarriesToTheRecordAndTheEnding()
    {
        var content = Real;
        var record = CampaignRecord.StartAt(content, 41, "ironwake_keep", pick: "keziah") with
        {
            MapIndex = content.Campaign.Maps.Count,
            Coma = ValueList<string>.Of("hask"),
            HillChose = HillChoice.Reseal,
        };

        var ending = CampaignEnding.Of(record, content);
        Assert.Equal(3, ending.Version);
        Assert.Equal(new[] { "hask" }, ending.Coma);

        var json = ProtocolJson.Campaign(record, ending);
        Assert.Contains("\"coma\":[\"hask\"]", json.Replace(" ", ""));
        Assert.Equal(ending, ProtocolJson.ReadEnding(json));
        Assert.Equal(record.Coma, ProtocolJson.ReadCampaign(json, content).Coma);
        Assert.DoesNotContain("coma", ProtocolJson.Campaign(record with { Coma = ValueList<string>.Empty }));
    }
}
