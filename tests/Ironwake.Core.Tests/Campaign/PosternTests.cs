using System.Text.Json.Nodes;
using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Campaign;

/// <summary>
/// The hire who is not what the menu says (issue 691, amended by issue 706): Bet Lowry's request
/// opens at the first camp after map 8 at which she stands in the company, never before and never
/// without her. The Postern takes her and three allies; a win makes her a Sergeant, a hidden class
/// whose mastery, Unsworn (+10 hit and +10 crit against the oath-bound), she holds at once, and
/// whose Hold the Gate braces her on a Wait in place on any map. Alive at the end, her ending line
/// is her own.
/// </summary>
public class PosternTests
{
    private static readonly GameContent Content = MapFixture.Content;

    private static MapDefinition Postern() =>
        MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), MapFiles.QuestsDirectory, "the_postern" + MapFiles.Extension), Content);

    /// <summary>A record before map <paramref name="mapId"/> with Bet hired from the barracks.</summary>
    private static CampaignRecord WithBet(string mapId)
    {
        var record = CampaignRecord.StartAt(Content, 691, mapId) with { Purse = 5000 };
        var built = record.BuildRoom("barracks", Content);
        Assert.True(built.Accepted, built.Text);
        var hired = built.Record.Hire("bet", Content);
        Assert.True(hired.Accepted, hired.Text);
        return hired.Record;
    }

    private static readonly string[] Allies = { "wren", "teodor", "maud" };

    /// <summary>The Postern's opening with the enemies gone and Bet on the seat, decided as a win.</summary>
    private static BattleState Won(CampaignRecord record)
    {
        var opening = record.BeginQuest(Postern(), "bet_postern", Allies, Content);
        var units = opening.UnitsOf(Side.Player).Select(u => u.Id == "bet" ? u with { At = new Coord(8, 3) } : u);
        var won = opening with { Units = ValueList<BattleUnit>.From(units), History = ValueList<BattleState>.Of(opening) };
        Assert.Equal(BattleResult.Won, won.Outcome.Result);
        return won;
    }

    [Fact]
    public void BetsRequestIsNotOfferedBeforeTheCampAfterMapEight()
    {
        Assert.DoesNotContain(WithBet("brackwater_cut").QuestsOffered(Content), q => q.Id == "bet_postern");
        Assert.Contains(WithBet("ironwake_keep").QuestsOffered(Content), q => q.Id == "bet_postern");
        Assert.Equal("side map bet_postern is not open before this map", WithBet("brackwater_cut").QuestRefusal("bet_postern", Allies, Content));
    }

    [Fact]
    public void BetsRequestIsNotOfferedWithoutHer()
    {
        var without = CampaignRecord.StartAt(Content, 691, "ironwake_keep");

        Assert.DoesNotContain(without.QuestsOffered(Content), q => q.Id == "bet_postern");
        Assert.DoesNotContain(Content.Campaign.Quests.Where(q => q.Id != "bet_postern"), q => q.MemberId == "bet");
    }

    [Fact]
    public void BetsRequestDoesNotFireTwice()
    {
        var after = WithBet("ironwake_keep").AfterQuest(Won(WithBet("ironwake_keep")), "bet_postern", Content);

        Assert.True(after.Accepted, after.Text);
        Assert.DoesNotContain(after.Record.QuestsOffered(Content), q => q.Id == "bet_postern");
        Assert.Equal("side map bet_postern is won already", after.Record.QuestRefusal("bet_postern", Allies, Content));
    }

    [Fact]
    public void ThePosternTakesBetAndThreeAlliesEachOnce()
    {
        var map = Postern();
        var record = WithBet("ironwake_keep");

        Assert.Equal(3, CampaignRecord.QuestAllies(map));
        Assert.Equal(4, map.Deploy);
        Assert.Equal("The Postern takes 3 allies, not 1", CampaignRecord.QuestAlliesRefusal(map, new[] { "wren" }));
        Assert.Null(CampaignRecord.QuestAlliesRefusal(map, Allies));
        Assert.Equal("wren is named twice; pick each ally once", record.QuestRefusal("bet_postern", new[] { "wren", "wren", "maud" }, Content));
        Assert.Equal("captain is the captain and stays with the company; pick another ally", record.QuestRefusal("bet_postern", new[] { "captain", "wren", "maud" }, Content));
        Assert.Null(record.QuestRefusal("bet_postern", Allies, Content));

        var opening = record.BeginQuest(map, "bet_postern", Allies, Content);
        Assert.Equal(new[] { "bet", "maud", "teodor", "wren" }, opening.UnitsOf(Side.Player).Select(u => u.Id).Order(StringComparer.Ordinal));
        Assert.Equal(new Coord(0, 3), opening.Find("bet")!.At);
    }

    [Fact]
    public void ThePosternsEnemiesAreAllOathBound()
    {
        var opening = WithBet("ironwake_keep").BeginQuest(Postern(), "bet_postern", Allies, Content);

        Assert.All(opening.UnitsOf(Side.Enemy), e => Assert.True(opening.Map.IsOathbound(e), e.Id));
        Assert.All(opening.UnitsOf(Side.Player), p => Assert.False(opening.Map.IsOathbound(p), p.Id));
        Assert.Equal(WinCondition.Seize, opening.Map.Win);
    }

    [Fact]
    public void AWonPosternMakesBetASergeantWithUnswornMastered()
    {
        var record = WithBet("ironwake_keep");
        var after = record.AfterQuest(Won(record), "bet_postern", Content);
        var bet = after.Record.Find("bet")!;

        Assert.Equal("bet wins bet_postern; bet becomes a Sergeant; nobody fell", after.Text);
        Assert.Equal("sergeant", bet.ClassId);
        Assert.Contains("unsworn", bet.Abilities);
        Assert.Equal(Content.Class("sergeant").MasteryPoints, bet.Mastery.Points("sergeant"));
        Assert.Contains(Content.AbilitiesOf(bet), a => a.Id == "hold_the_gate");
        Assert.Equal(record.Find("bet")!.Level, bet.Level);
        Assert.Equal("pikeman", record.Find("bet")!.ClassId);
    }

    [Fact]
    public void ALostPosternLeavesBetAPikeman()
    {
        var record = WithBet("ironwake_keep");
        var opening = record.BeginQuest(Postern(), "bet_postern", Allies, Content);
        var lost = opening with { Turn = opening.Map.TurnLimit + 1, History = ValueList<BattleState>.Of(opening) };

        var after = record.AfterQuest(lost, "bet_postern", Content);

        Assert.Equal(BattleResult.Lost, lost.Outcome.Result);
        Assert.Equal("pikeman", after.Record.Find("bet")!.ClassId);
    }

    [Fact]
    public void TheSergeantIsHiddenAndNeverCertifiedIntoOrOutOf()
    {
        var sergeant = Content.Class("sergeant");
        var record = WithBet("ironwake_keep");
        var promoted = record.AfterQuest(Won(record), "bet_postern", Content).Record with { Purse = 5000 };

        Assert.True(sergeant.Hidden);
        Assert.Equal(new[] { WeaponType.Lance, WeaponType.Sword }, sergeant.Weapons);
        Assert.Equal(4, sergeant.Mov);
        Assert.Contains(Certifications.Check(record.Find("wren")! with { Level = 20 }, sergeant), r => r.Requirement == "hidden");
        Assert.False(record.Certify("wren", "sergeant", Content).Accepted);
        Assert.Equal("bet cannot be promoted to Cadet: bet is a Sergeant, earned and kept", promoted.Certify("bet", "cadet", Content).Text);
        Assert.DoesNotContain(Content.Classes.Values, c => c.Hidden && c.Id != "sergeant");
    }

    [Fact]
    public void UnswornAddsTenHitAndTenCritAgainstTheOathBoundOnly()
    {
        var map = MapFixture.Parse("""
            name: Oath
            size: 5x3
            win: rout
            turn_limit: 10
            recall: 3
            enemy_level: 1
            oathbound: sworn

            .....
            .....
            .....

            units:
            P captain 0,1
            E soldier 1,1 group:sworn behavior:hold
            E soldier 3,1 group:free behavior:hold
            """, "oath.map");
        var bet = Barracks.Recruit(Content.Campaign.Keep.Hire("bet")!, 1, Content);
        var sergeant = CampaignRecord.Promote(bet, Content.Class("sergeant"));
        var state = BattleState.From(map, Content, ValueList<Unit>.Of(sergeant), 7);
        var plain = BattleState.From(map, Content, ValueList<Unit>.Of(sergeant with { Abilities = ValueList<string>.Empty }), 7);
        var unit = state.Find("bet")!;

        var sworn = Queries.Forecast(state, Content, unit, state.Find("soldier-1")!)!;
        var swornPlain = Queries.Forecast(plain, Content, plain.Find("bet")!, plain.Find("soldier-1")!)!;
        var free = Queries.Forecast(state, Content, unit, state.Find("soldier-2")!, new Coord(2, 1))!;
        var freePlain = Queries.Forecast(plain, Content, plain.Find("bet")!, plain.Find("soldier-2")!, new Coord(2, 1))!;

        Assert.True(state.Map.IsOathbound(state.Find("soldier-1")!));
        Assert.False(state.Map.IsOathbound(state.Find("soldier-2")!));
        Assert.Equal(swornPlain.Attacker.HitChance + 10, sworn.Attacker.HitChance);
        Assert.Equal(swornPlain.Attacker.CritChance + 10, sworn.Attacker.CritChance);
        Assert.Equal(freePlain.Attacker.HitChance, free.Attacker.HitChance);
        Assert.Equal(freePlain.Attacker.CritChance, free.Attacker.CritChance);
    }

    [Fact]
    public void HoldTheGateBracesASergeantOnAMapWithoutBrace()
    {
        var map = MapFixture.Parse("""
            name: Gate
            size: 5x3
            win: rout
            turn_limit: 10
            recall: 3
            enemy_level: 1

            .....
            .....
            .....

            units:
            P captain 0,1
            P recruit:bet 0,0
            E soldier 4,1 group:field behavior:hold
            """, "gate.map");
        var captain = Content.Cast[0];
        var bet = Barracks.Recruit(Content.Campaign.Keep.Hire("bet")!, 1, Content);
        var state = BattleState.From(map, Content, ValueList<Unit>.Of(captain, CampaignRecord.Promote(bet, Content.Class("sergeant"))), 7);
        var pikeman = BattleState.From(map, Content, ValueList<Unit>.Of(captain, bet), 7);

        Assert.False(map.BraceEnabled);
        Assert.True(state.Do(new Wait("bet")).Find("bet")!.Braced);
        Assert.False(pikeman.Do(new Wait("bet")).Find("bet")!.Braced);
        Assert.False(state.Do(new Wait("captain")).Find("captain")!.Braced);
        Assert.False(state.Do(new Move("bet", new Coord(1, 0))).Do(new Wait("bet")).Find("bet")!.Braced);
    }

    [Fact]
    public void BetsEndingLineIsHerOwnOnceThePosternIsWon()
    {
        var record = WithBet("ironwake_keep");
        var promoted = record.AfterQuest(Won(record), "bet_postern", Content).Record;

        Assert.Equal(new[] { "Bet Lowry served at the keep." }, CampaignSession.EndingLines(record, Content));
        Assert.Equal(new[] { "Bet Lowry swore first, the one the oath could not hold, choosing it." }, CampaignSession.EndingLines(promoted, Content));
    }

    [Fact]
    public void BetsHireCardFollowsTheBarracksFormula()
    {
        var bet = Barracks.Recruit(Content.Campaign.Keep.Hire("bet")!, 1, Content);

        Assert.Equal(Barracks.Growths("pikeman", Content), bet.Growths);
        Assert.Equal("pikeman", bet.ClassId);
        Assert.Empty(bet.Abilities);
    }

    [Fact]
    public void NoListingNamesTheSergeantBeforeThePosternIsWon()
    {
        var record = WithBet("ironwake_keep");
        var map = MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), MapFiles.KeepDirectory, "ironwake_keep" + MapFiles.Extension), Content);
        var camp = string.Join("\n", CampaignSession.CampLines(Fixture.RealContentDirectory(), Content, record, map));

        Assert.DoesNotContain("Sergeant", camp, StringComparison.Ordinal);
        Assert.DoesNotContain("Sergeant", string.Join("\n", Content.Campaign.Quest("bet_postern")!.Before), StringComparison.Ordinal);
        Assert.Contains("bet_postern: Bet Lowry's request, The Postern (quest bet_postern <ally> <ally> <ally>)", camp, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePosternIsCanonical()
    {
        var path = Path.Combine(Fixture.RealContentDirectory(), MapFiles.QuestsDirectory, "the_postern.map");
        var text = File.ReadAllText(path).ReplaceLineEndings("\n");

        Assert.Equal(text, MapFormat.Write(MapFormat.Parse(path, text, Content), Content));
    }

    [Theory]
    [InlineData("oathbound: nobody", "oathbound: no enemy is in group 'nobody'")]
    [InlineData("oathbound: sworn, sworn", "oathbound: group 'sworn' is listed twice")]
    public void TheOathboundHeaderNamesGroupsOnTheMapOnce(string header, string error)
    {
        var text = $"""
            name: Oath
            size: 3x1
            win: rout
            turn_limit: 10
            recall: 3
            enemy_level: 1
            {header}

            ...

            units:
            P captain 0,0
            E soldier 2,0 group:sworn behavior:hold
            """;

        var thrown = Assert.Throws<MapException>(() => MapFixture.Parse(text, "oath.map"));
        Assert.Contains(error, thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheLoaderHoldsAHiresQuestToItsShape()
    {
        Assert.Equal(("bet_postern", "member"), Fails(q => q.Remove("opensAfter")));
        Assert.Equal(("bet_postern", "opensAfter"), Fails(q => q["opensAfter"] = "nowhere"));
        Assert.Equal(("bet_postern", "promotes"), Fails(q => q["promotes"] = "pikeman"));
        Assert.Equal(("maud_1", "ending"), Fails(q => q["ending"] = "Maud stayed.", "maud_1"));
    }

    /// <summary>The loader's entry and field for the real campaign with one quest edited.</summary>
    private static (string?, string?) Fails(Action<JsonObject> edit, string questId = "bet_postern")
    {
        var files = ContentSerializer.Write(Content);
        var campaign = JsonNode.Parse(files.Campaign!.Text)!;
        edit(campaign["quests"]!.AsArray().OfType<JsonObject>().Single(q => (string?)q["id"] == questId));

        var error = Assert.Throws<ContentException>(() => ContentLoader.Parse(files with { Campaign = files.Campaign with { Text = campaign.ToJsonString() } }));
        return (error.Entry, error.Field);
    }
}
