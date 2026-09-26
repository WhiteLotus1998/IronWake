using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Campaign;

/// <summary>
/// Certification trials in the campaign (issue 252, DESIGN sections 9 and 13.6): a trial stands
/// in for the seal after the class's requirements, one attempt per unit and class per camp, a pass
/// carries the trial's EXP and weapon points but no mastery, and a failure changes nothing but the
/// attempt. The shipped content's campaign and trials.
/// </summary>
public class CampaignTrialTests
{
    private static GameContent Content => MapFixture.Content;

    private static MapDefinition Trial(string id) =>
        MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "trials", id + ".map"), Content);

    private static CampaignRecord Start() => CampaignRecord.Start(Content, 5);

    /// <summary>The outrider trial's opening with the candidate on the throne beyond the gate and the gate's hexer dead: a pass.</summary>
    private static BattleState Passed(CampaignRecord record, string unitId, Func<Unit, Unit> earned)
    {
        var opening = record.BeginTrial(Trial("outrider_trial"), unitId, Content);
        var candidate = opening.UnitsOf(Side.Player).Single();
        var units = ValueList<BattleUnit>.Of(candidate with { At = new Coord(3, 0), Unit = earned(candidate.Unit) });
        return opening with { Units = units, History = ValueList<BattleState>.Of(opening) };
    }

    [Fact]
    public void TheShippedCampaignOffersTheBulwarkAndOutriderTrialsEachOnDiskForItsOwnClass()
    {
        Assert.Equal(new[] { ("bulwark", "bulwark_trial"), ("outrider", "outrider_trial") }, Content.Campaign.Trials.Select(t => (t.ClassId, t.MapId)));
        Assert.All(Content.Campaign.Trials, t => Assert.Equal(t.ClassId, Trial(t.MapId).Certification!.ClassId));
    }

    [Fact]
    public void ATrialMapThatCertifiesAnotherClassIsRefused()
    {
        Assert.Equal("the trial map 'Trial of the Bulwark' does not certify 'outrider'", CampaignRecord.TrialMapRefusal(Trial("bulwark_trial"), "outrider"));
        Assert.Null(CampaignRecord.TrialMapRefusal(Trial("outrider_trial"), "outrider"));
    }

    [Fact]
    public void AClassWithNoTrialIsRefusedAndNeedsASeal()
    {
        Assert.Equal("Pikeman has no trial; certify with a seal", Start().TrialRefusal("wren", "pikeman", Content));
    }

    [Fact]
    public void ATrialComesAfterTheClassRequirements()
    {
        var bulwark = Content.Class("bulwark") with
        {
            Certification = new CertificationRequirements(5, ValueList<(WeaponType, WeaponRank)>.Empty, Stats.Zero),
        };
        var content = Content with { Classes = Content.Classes.SetItem("bulwark", bulwark) };

        Assert.Equal("wren cannot certify as Bulwark: needs level 5, has 1", CampaignRecord.Start(content, 5).TrialRefusal("wren", "bulwark", content));
        Assert.Null(Start().TrialRefusal("wren", "bulwark", Content));
    }

    [Fact]
    public void ATrialIsRefusedForAStrangerAnUnknownClassAndTheClassTheUnitIsIn()
    {
        var record = Start();

        Assert.Equal("no unit 'nobody' on the roster", record.TrialRefusal("nobody", "outrider", Content));
        Assert.Equal("no class 'wizard'", record.TrialRefusal("wren", "wizard", Content));
        Assert.StartsWith("ansgar cannot certify as Outrider: ", record.TrialRefusal("ansgar", "outrider", Content));
    }

    [Fact]
    public void APassCertifiesWithNoSealAndCarriesTheTrialsExpAndRanksButNoMastery()
    {
        var record = Start();
        var before = record.Find("wren")!;
        var end = Passed(record, "wren", u => u with { Exp = 30, Skill = u.Skill.Add(WeaponType.Sword, 5), Mastery = u.Mastery.With("outrider", 1) });
        Assert.Equal(BattleResult.Won, end.Outcome.Result);

        var result = record.AfterTrial(end, "wren", Content);
        var after = result.Record.Find("wren")!;

        Assert.True(result.Accepted);
        Assert.Equal("wren passes the Outrider trial and certifies from Cadet to Outrider with no seal; L1 exp 30", result.Text);
        Assert.Equal(record.Purse, result.Record.Purse);
        Assert.Equal("outrider", after.ClassId);
        Assert.Equal(30, after.Exp);
        Assert.Equal(5, after.Skill.Points(WeaponType.Sword));
        Assert.Equal(before.Mastery, after.Mastery);
        Assert.Equal(before.Inventory, after.Inventory);
        Assert.Equal(before.Abilities, after.Abilities);
        Assert.Equal(ValueList<TrialAttempt>.Of(new TrialAttempt("wren", "outrider")), result.Record.TrialsTried);
    }

    [Fact]
    public void AFailedTrialChangesNothingButTheAttemptAndItOpensAgainAfterTheNextMap()
    {
        var record = Start();
        var opening = record.BeginTrial(Trial("outrider_trial"), "wren", Content);
        var end = opening with { Turn = 2, History = ValueList<BattleState>.Of(opening) };
        Assert.Equal(BattleResult.Lost, end.Outcome.Result);

        var result = record.AfterTrial(end, "wren", Content);

        Assert.Equal("wren fails the Outrider trial and stays a Cadet; it opens again after the next map", result.Text);
        Assert.Equal(record.Roster, result.Record.Roster);
        Assert.Equal("wren has tried the Outrider trial since the last map; it opens again after the next one", result.Record.TrialRefusal("wren", "outrider", Content));
        Assert.Null(result.Record.TrialRefusal("teodor", "outrider", Content));

        var map = MapFiles.Load(Path.Combine(MapFixture.MapsDirectory, "old_mill_road.map"), Content);
        var battle = result.Record.Begin(map, Content);
        var won = battle with { Units = ValueList<BattleUnit>.From(battle.UnitsOf(Side.Player)), History = ValueList<BattleState>.Of(battle) };
        var next = result.Record.AfterBattle(won, Content);

        Assert.Empty(next.TrialsTried);
        Assert.Null(next.TrialRefusal("wren", "outrider", Content));
    }

    [Fact]
    public void ATrialResultIsTakenOnlyFromADecidedTrialForAUnitOnTheRoster()
    {
        var record = Start();
        var opening = record.BeginTrial(Trial("outrider_trial"), "wren", Content);
        var map = MapFiles.Load(Path.Combine(MapFixture.MapsDirectory, "old_mill_road.map"), Content);

        Assert.Throws<InvalidOperationException>(() => record.AfterTrial(opening, "wren", Content));
        Assert.Throws<ArgumentException>(() => record.AfterTrial(record.Begin(map, Content), "wren", Content));
        Assert.Throws<ArgumentException>(() => record.BeginTrial(Trial("outrider_trial"), "nobody", Content));
        Assert.Throws<ArgumentException>(() => record.AfterTrial(opening with { Turn = 2 }, "nobody", Content));
    }

    [Fact]
    public void ACandidateWhoFallsInATrialIsNotFallen()
    {
        var record = Start();
        var opening = record.BeginTrial(Trial("outrider_trial"), "wren", Content);
        var end = opening with { Units = ValueList<BattleUnit>.From(opening.UnitsOf(Side.Enemy)), History = ValueList<BattleState>.Of(opening) };
        Assert.Equal(BattleResult.Lost, end.Outcome.Result);

        var after = record.AfterTrial(end, "wren", Content).Record;

        Assert.Empty(after.Fallen);
        Assert.Equal(record.Find("wren"), after.Find("wren"));
    }

    [Fact]
    public void ATrialPlaysOnASeedNoMapOfTheCampaignUses()
    {
        var record = Start();
        var mapSeeds = Enumerable.Range(0, Content.Campaign.Maps.Count).Select(i => (record with { MapIndex = i }).BattleSeed).ToList();

        Assert.All(Enumerable.Range(0, Content.Campaign.Maps.Count), i => Assert.DoesNotContain((record with { MapIndex = i }).TrialSeed(Content), mapSeeds));
        Assert.Equal(record.TrialSeed(Content), record.BeginTrial(Trial("bulwark_trial"), "wren", Content).Seed);
    }

    [Fact]
    public void TheTrialsTriedRoundTripThroughTheProtocolAndAnOlderRecordReadsAsNoneTried()
    {
        var record = Start() with { TrialsTried = ValueList<TrialAttempt>.Of(new TrialAttempt("wren", "outrider"), new TrialAttempt("pell", "bulwark")) };

        var json = ProtocolJson.Campaign(record);
        var older = ProtocolJson.Campaign(Start()).Replace(",\"trialsTried\":[]", "");

        Assert.Contains("\"trialsTried\":[{\"unit\":\"wren\",\"class\":\"outrider\"},{\"unit\":\"pell\",\"class\":\"bulwark\"}]", json);
        Assert.Equal(record, ProtocolJson.ReadCampaign(json, Content));
        Assert.DoesNotContain("trialsTried", older);
        Assert.Empty(ProtocolJson.ReadCampaign(older, Content).TrialsTried);
    }
}
