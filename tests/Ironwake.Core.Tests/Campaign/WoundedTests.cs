using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Campaign;

/// <summary>
/// The permadeath toggle and Wounded (issue 664, DESIGN section 9): with permadeath off a unit
/// that falls on a won map comes back with 2 taken from its two highest stats other than HP for
/// the next two main maps and its EXP lost; side maps neither count a wound down nor kill; the
/// captain's death still loses the map; and the toggle and a wound round-trip on the record.
/// </summary>
public class WoundedTests
{
    private static readonly GameContent Content = MapFixture.WithoutLadder(MapFixture.Content);

    private static MapDefinition Map(string id) => MapFiles.Load(MapFiles.CampaignPath(Fixture.RealContentDirectory(), Content, id), Content);

    private static MapDefinition SideMap(string id) =>
        MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), MapFiles.QuestsDirectory, id + MapFiles.Extension), Content);

    /// <summary>A record with permadeath off whose next map is <paramref name="index"/>, every recruit who arrives before it on the roster.</summary>
    private static CampaignRecord Off(int index) =>
        CampaignRecord.Start(Content, 5, permadeath: false) with
        {
            MapIndex = index,
            Roster = ValueList<Unit>.From(Content.Cast.Where(u => Content.Campaign.ArrivalIndex(u.Id) < index)),
        };

    /// <summary>
    /// The battle <paramref name="record"/> begins, won by removing every enemy (on a Seize map the
    /// captain stands on the throne), with the player units <paramref name="keep"/> keeps and
    /// <paramref name="change"/> applied to each.
    /// </summary>
    private static BattleState Won(CampaignRecord record, Func<BattleUnit, bool>? keep = null, Func<BattleUnit, BattleUnit>? change = null)
    {
        var map = Map(record.NextMap(Content).MapId);
        var opening = record.Begin(map, Content);
        var throne = Enumerable.Range(0, map.Width * map.Height).Select(i => new Coord(i % map.Width, i / map.Width)).FirstOrDefault(map.IsThrone);
        var units = opening.UnitsOf(Side.Player).Where(u => keep is null || keep(u))
            .Select(u => u.IsCaptain && map.Win == WinCondition.Seize ? u with { At = throne } : u)
            .Select(u => change is null ? u : change(u));
        return opening with { Units = ValueList<BattleUnit>.From(units), Turn = 4, History = ValueList<BattleState>.Of(opening) };
    }

    private static Stats Effective(Unit unit) => unit.EffectiveStats(Content.Class(unit.ClassId));

    [Fact]
    public void AWoundTakesTwoFromTheTwoHighestStatsOtherThanHpAndTheExp()
    {
        var wren = Content.Unit("wren") with { Exp = 63 };
        var top = Stats.All.Where(s => s != Stat.Hp).OrderByDescending(s => Effective(wren).Get(s)).Take(2).ToList();

        var wounded = Wound.Inflict(wren, Content.Class(wren.ClassId));

        Assert.Equal(0, wounded.Exp);
        Assert.Equal(wren.Level, wounded.Level);
        Assert.Equal(new Wound(Stats.Zero.With(top[0], 2).With(top[1], 2), 2), wounded.Wound);
        Assert.Equal(wren.Stats - wounded.Wound!.Penalty, wounded.Stats);
        Assert.Equal(wren.Stats.Hp, wounded.Stats.Hp);
        Assert.Equal(wren.Inventory, wounded.Inventory);
    }

    [Fact]
    public void AWoundNeverTakesAStatBelowZero()
    {
        var bare = Content.Unit("wren") with { Stats = new Stats(20, 1, 0, 0, 0, 0, 0, 0, 0) };
        var unitClass = Content.Class(bare.ClassId) with { Modifiers = new Stats(0, 9, 8, 0, 0, 0, 0, 0, 0) };

        var wounded = Wound.Inflict(bare, unitClass);

        Assert.Equal(new Stats(0, 1, 0, 0, 0, 0, 0, 0, 0), wounded.Wound!.Penalty);
        Assert.Equal(0, wounded.Stats.Str);
        Assert.Equal(bare.Stats, Wound.Heal(wounded).Stats);
    }

    [Fact]
    public void WithPermadeathOffAUnitThatFallsOnAWonMapComesBackWounded()
    {
        var record = Off(2);
        var before = record.Find("wren")!;

        var after = record.AfterBattle(Won(record, u => u.Id != "wren"), Content);

        Assert.Empty(after.Fallen);
        var wren = after.Find("wren")!;
        Assert.Equal(2, wren.Wound!.MapsLeft);
        Assert.Equal(0, wren.Exp);
        Assert.Equal(before.Stats - wren.Wound.Penalty, wren.Stats);
        Assert.Equal(before.Inventory, wren.Inventory);
        Assert.Equal(record.Roster.Select(u => u.Id), after.Roster.Select(u => u.Id));
    }

    [Fact]
    public void WithPermadeathOnAUnitThatFallsIsFallenForGood()
    {
        var record = Off(2) with { Permadeath = true };

        var after = record.AfterBattle(Won(record, u => u.Id != "wren"), Content);

        Assert.Null(after.Find("wren"));
        Assert.Equal(ValueList<string>.Of("wren"), after.Fallen);
    }

    [Fact]
    public void AWoundExpiresAfterTwoMainMaps()
    {
        var record = Off(2);
        var healthy = record.Find("wren")!;
        var wounded = record.AfterBattle(Won(record, u => u.Id != "wren"), Content);

        var one = wounded.AfterBattle(Won(wounded), Content);
        Assert.Equal("Wounded (1)", one.Find("wren")!.Wound!.Label);

        var two = one.AfterBattle(Won(one), Content);
        Assert.Null(two.Find("wren")!.Wound);
        Assert.Equal(healthy.Stats, two.Find("wren")!.Stats);
    }

    [Fact]
    public void AWoundedUnitDeploysAndFightsWithItsWoundedStats()
    {
        var record = Off(2);
        var wounded = record.AfterBattle(Won(record, u => u.Id != "wren"), Content);

        var unit = wounded.Begin(Map(wounded.NextMap(Content).MapId), Content).UnitsOf(Side.Player).Single(u => u.Id == "wren");

        Assert.Equal(wounded.Find("wren")!.Stats, unit.Unit.Stats);
        Assert.NotNull(unit.Unit.Wound);
    }

    [Fact]
    public void ALevelGainedWhileWoundedIsKeptWhenTheWoundHeals()
    {
        var record = Off(2);
        var wounded = record.AfterBattle(Won(record, u => u.Id != "wren"), Content);
        var gain = new Stats(1, 1, 0, 1, 0, 0, 0, 0, 0);
        var grown = Won(wounded, change: u => u.Id == "wren" ? u with { Unit = u.Unit with { Stats = u.Unit.Stats + gain } } : u);

        var one = wounded.AfterBattle(grown, Content);
        var two = one.AfterBattle(Won(one), Content);

        Assert.Equal(record.Find("wren")!.Stats + gain, two.Find("wren")!.Stats);
    }

    [Fact]
    public void ASideMapNeitherCountsAWoundDownNorKillsWithPermadeathOff()
    {
        var record = CampaignRecord.StartAt(Content, 701, "the_tollgate", permadeath: false);
        var wren = Wound.Inflict(record.Find("wren")!, Content.Class(record.Find("wren")!.ClassId));
        record = record with { Roster = ValueList<Unit>.From(record.Roster.Select(u => u.Id == "wren" ? wren : u)) };
        var opening = record.BeginQuest(SideMap("the_lazar_house"), "maud_1", "wren", Content);
        var maudFalls = opening with
        {
            Units = ValueList<BattleUnit>.From(opening.UnitsOf(Side.Player).Where(u => u.Id != "maud")),
            Turn = opening.Map.TurnLimit + 1,
            History = ValueList<BattleState>.Of(opening),
        };

        var after = record.AfterQuest(maudFalls, "maud_1", Content);

        Assert.Equal(2, after.Record.Find("wren")!.Wound!.MapsLeft);
        Assert.Equal(2, after.Record.Find("maud")!.Wound!.MapsLeft);
        Assert.Empty(after.Record.Fallen);
        Assert.EndsWith("; fell and came back wounded: maud", after.Text);
        Assert.Equal("side map maud_1 was fought since the last map; it opens again after the next one", after.Record.QuestRefusal("maud_1", "wren", Content));
    }

    [Fact]
    public void TheCaptainsDeathStillLosesTheMapWithPermadeathOff()
    {
        var record = Off(2);
        var opening = record.Begin(Map(record.NextMap(Content).MapId), Content);

        var captainGone = opening with { Units = ValueList<BattleUnit>.From(opening.Units.Where(u => !u.IsCaptain)) };

        Assert.Equal(BattleResult.Lost, captainGone.Outcome.Result);
        Assert.Throws<InvalidOperationException>(() => record.AfterBattle(captainGone, Content));
    }

    [Fact]
    public void ThePermadeathToggleAndAWoundRoundTripOnTheRecord()
    {
        var record = Off(2);
        var wounded = record.AfterBattle(Won(record, u => u.Id != "wren"), Content);

        var read = ProtocolJson.ReadCampaign(ProtocolJson.Campaign(wounded), Content);

        Assert.False(read.Permadeath);
        Assert.Equal(wounded.Find("wren"), read.Find("wren"));
        Assert.Contains("\"permadeath\":false", ProtocolJson.Campaign(wounded));
        Assert.True(ProtocolJson.ReadCampaign(ProtocolJson.Campaign(CampaignRecord.Start(Content, 5)), Content).Permadeath);
    }

    [Fact]
    public void ARecordWrittenBeforeTheToggleReadsAsPermadeathOn()
    {
        var json = ProtocolJson.Campaign(CampaignRecord.Start(Content, 5, permadeath: false)).Replace("\"permadeath\":false,", "");

        Assert.DoesNotContain("permadeath", json);
        Assert.True(ProtocolJson.ReadCampaign(json, Content).Permadeath);
    }

    [Fact]
    public void AWoundOutsideOneToTwoMapsIsRefusedOnRead()
    {
        var wounded = Off(2).AfterBattle(Won(Off(2), u => u.Id != "wren"), Content);
        var json = ProtocolJson.Campaign(wounded).Replace("\"mapsLeft\":2", "\"mapsLeft\":3");

        var error = Assert.Throws<ProtocolException>(() => ProtocolJson.ReadCampaign(json, Content));

        Assert.Contains("wound.mapsLeft", error.Message);
    }

    [Fact]
    public void TheCardPrintsTheWoundWithTheStatsItTook()
    {
        var wound = new Wound(Stats.Zero with { Str = 2, Dex = 2 }, 2);

        Assert.Equal("Wounded (2): Str -2, Dex -2 for 2 more main maps", Ironwake.Cli.PlaySession.WoundLine(wound));
        Assert.Equal("Wounded (1): Str -2, Dex -2 for the next main map", Ironwake.Cli.PlaySession.WoundLine(wound with { MapsLeft = 1 }));
    }
}
