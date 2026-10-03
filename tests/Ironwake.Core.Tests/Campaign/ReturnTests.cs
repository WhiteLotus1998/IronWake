using Ironwake.Cli;
using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Campaign;

/// <summary>
/// Issue 633 slice 2 (DESIGN section 14, the return): the claimant passed on at the branch comes
/// back as a foe on the field, with their own card at the pick's level, placed by <c>campaign.json</c>'s
/// <c>return</c>. The pick or the captain, beside them, may <c>talk</c> as the unit's action: the pick's
/// talk turns them, the captain's spares them, and either way they leave the board without a kill.
/// Turned, they join only if a bed is free. The record and the save carry the fate.
/// </summary>
public class ReturnTests
{
    private static readonly GameContent Content = MapFixture.Content;

    private static string Dir => Fixture.RealContentDirectory();

    private static MapDefinition Field => MapFiles.Load(MapFiles.CampaignPath(Dir, Content, "the_field"), Content);

    private static readonly Coord ReturnTile = new(8, 12);

    private static CampaignRecord AtTheField(string pick) => CampaignRecord.StartAt(Content, 633, "the_field", pick: pick);

    /// <summary>The field's opening with <paramref name="talker"/> moved beside the returned claimant, on the west.</summary>
    private static BattleState Beside(CampaignRecord record, string talker)
    {
        var opening = record.Begin(Field, Content);
        var unit = opening.Find(talker)!;
        return opening.WithUnit(unit with { At = new Coord(7, 12) });
    }

    [Fact]
    public void TheFieldNamesWhereThePassedClaimantComesBack()
    {
        var back = Content.Campaign.Maps[Content.Campaign.MapIndexOf("the_field")].Return;

        Assert.Equal(new CampaignReturn(ReturnTile, "pickets", Behavior.Guard), back);
        Assert.Single(Content.Campaign.Maps, m => m.Return is not null);
    }

    [Theory]
    [InlineData("keziah", "rook")]
    [InlineData("rook", "keziah")]
    public void ThePassedClaimantComesBackOnTheFieldAsAFoeAtThePicksLevel(string pick, string passed)
    {
        var record = AtTheField(pick);
        record = record with { Roster = ValueList<Unit>.From(record.Roster.Select(u => u.Id == pick ? u.ScaledTo(5, Content.Class(u.ClassId)) : u)) };

        var opening = record.Begin(Field, Content);

        var foe = opening.Find(passed)!;
        Assert.Equal(Side.Enemy, foe.Side);
        Assert.Equal(ReturnTile, foe.At);
        Assert.Equal("pickets", foe.Group);
        Assert.Equal(Behavior.Guard, foe.Behavior);
        Assert.Equal(5, foe.Unit.Level);
        Assert.Equal(Content.Unit(passed).ClassId, foe.Unit.ClassId);
        Assert.Equal(new ReturnBond(passed, pick), opening.Return);
        Assert.Equal(5, record.ReturnLevel(Content));
    }

    [Fact]
    public void AFallenPickSendsThePassedClaimantBackAtTheCompanysMedian()
    {
        var record = AtTheField("keziah");
        record = record with { Roster = ValueList<Unit>.From(record.Roster.Where(u => u.Id != "keziah")), Fallen = record.Fallen.Add("keziah") };

        Assert.Equal(Math.Max(Content.Unit("rook").Level, record.JoinLevel(Content)), record.ReturnLevel(Content));
        Assert.Equal(Side.Enemy, record.Begin(Field, Content).Find("rook")!.Side);
    }

    [Fact]
    public void NobodyComesBackWithoutAPick()
    {
        var record = CampaignRecord.StartAt(Content, 633, "the_field");

        var opening = record.Begin(Field, Content);

        Assert.Null(record.ReturnLevel(Content));
        Assert.Null(opening.Return);
        Assert.DoesNotContain(opening.UnitsOf(Side.Enemy), u => u.At == ReturnTile);
    }

    [Fact]
    public void NobodyComesBackOnAMapWithoutAReturn()
    {
        var record = CampaignRecord.StartAt(Content, 633, "brackwater_cut", pick: "keziah");

        Assert.Null(record.ReturnLevel(Content));
        Assert.Null(record.Begin(MapFiles.Load(MapFiles.CampaignPath(Dir, Content, "brackwater_cut"), Content), Content).Return);
    }

    [Fact]
    public void APickForACampaignOpeningBeforeTheBranchIsRefused()
    {
        var e = Assert.Throws<ArgumentException>(() => CampaignRecord.StartAt(Content, 1, "harrow_weir", pick: "rook"));

        Assert.Equal("'rook' is not a claimant offered before harrow_weir", e.Message);
        Assert.Throws<ArgumentException>(() => CampaignRecord.StartAt(Content, 1, "the_field", pick: "wren"));
    }

    [Fact]
    public void AStartAfterTheBranchWithAPickLeavesThePassedClaimantOffTheRoster()
    {
        var record = AtTheField("rook");

        Assert.Equal("rook", record.Pick);
        Assert.NotNull(record.Find("rook"));
        Assert.Null(record.Find("keziah"));
        Assert.Equal("keziah", record.Passed(Content));
    }

    [Fact]
    public void ThePicksTalkTurnsThemOffTheBoardWithoutAKill()
    {
        var state = Beside(AtTheField("keziah"), "keziah");
        var exp = state.Find("keziah")!.Unit.Exp;

        var result = Resolver.Apply(state, Content, new Talk("keziah", "rook"));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Null(result.Next.Find("rook"));
        Assert.Equal(ReturnFate.Turned, result.Next.ReturnGone);
        Assert.Contains(result.Events, e => e is UnitTalked { UnitId: "keziah", TargetId: "rook", Fate: ReturnFate.Turned });
        Assert.DoesNotContain(result.Events, e => e is UnitDied);
        Assert.Equal(exp, result.Next.Find("keziah")!.Unit.Exp);
        Assert.True(result.Next.Find("keziah")!.Acted);
    }

    [Fact]
    public void TheCaptainsTalkSparesThem()
    {
        var record = AtTheField("keziah");
        var state = Beside(record, Content.Cast[0].Id);

        var result = Resolver.Apply(state, Content, new Talk(Content.Cast[0].Id, "rook"));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Equal(ReturnFate.Spared, result.Next.ReturnGone);
        Assert.Contains(result.Events, e => e is UnitTalked { Fate: ReturnFate.Spared });
    }

    [Fact]
    public void ATalkFromAnyoneButThePickOrTheCaptainIsRefused()
    {
        var state = Beside(AtTheField("keziah"), "teodor");

        var result = Resolver.Apply(state, Content, new Talk("teodor", "rook"));

        Assert.Equal(RejectionReason.CannotTalk, result.Rejection!.Reason);
        Assert.Equal("teodor cannot talk to rook: only keziah or the captain can", result.Rejection.Message);
    }

    [Fact]
    public void ATalkFromOutsideTheNextTileIsRefused()
    {
        var state = AtTheField("keziah").Begin(Field, Content);

        var result = Resolver.Apply(state, Content, new Talk("keziah", "rook"));

        Assert.Equal(RejectionReason.CannotTalk, result.Rejection!.Reason);
        Assert.Equal("keziah cannot talk to rook: talking needs them side by side", result.Rejection.Message);
    }

    [Fact]
    public void ATalkToAnyOtherEnemyIsRefused()
    {
        var state = Beside(AtTheField("keziah"), "keziah");
        var other = state.UnitsOf(Side.Enemy).First(u => u.Id != "rook");

        var result = Resolver.Apply(state, Content, new Talk("keziah", other.Id));

        Assert.Equal(RejectionReason.CannotTalk, result.Rejection!.Reason);
        Assert.Contains("only a claimant who came back as a foe can be talked round", result.Rejection.Message);
    }

    [Fact]
    public void ATalkIsOfferedOnlyToThoseWhoMayTalk()
    {
        var state = Beside(AtTheField("keziah"), "keziah");

        var legal = Resolver.Legal(state, Content).OfType<Talk>().ToList();

        Assert.Equal(new[] { new Talk("keziah", "rook") }, legal);
        Assert.Empty(Resolver.Legal(AtTheField("keziah").Begin(Field, Content), Content).OfType<Talk>());
    }

    [Fact]
    public void KillingTheReturnedClaimantRecordsThemFallen()
    {
        var state = AtTheField("keziah").Begin(Field, Content);

        Assert.Equal(ReturnFate.Fell, state.WithoutUnit("rook").ReturnGone);
        Assert.Null(state.WithoutUnit(state.UnitsOf(Side.Enemy).First(u => u.Id != "rook").Id).ReturnGone);
    }

    /// <summary>The field won from <paramref name="state"/>: the enemy gone, the board's history its opening.</summary>
    private static BattleState Won(BattleState state, BattleState opening) =>
        state with { Units = ValueList<BattleUnit>.From(state.UnitsOf(Side.Player)), Turn = 4, History = ValueList<BattleState>.Of(opening) };

    [Fact]
    public void TurnedWithABedFreeTheyJoinTheCompanyAsTheyTookTheField()
    {
        var record = AtTheField("keziah");
        var opening = Beside(record, "keziah");
        var talked = Resolver.Apply(opening, Content, new Talk("keziah", "rook")).Next;

        var after = record.AfterBattle(Won(talked, opening), Content);

        Assert.Equal(ClaimantFate.Turned, after.Returned);
        Assert.Equal(opening.Find("rook")!.Unit, after.Find("rook"));
        Assert.Equal(after.Roster.Select(u => u.Id), Content.Cast.Select(u => u.Id).Where(id => after.Find(id) is not null));
    }

    [Fact]
    public void TurnedWithNoBedFreeTheyAreTurnedAway()
    {
        var record = AtTheField("keziah");
        var beds = record.Beds(Content)!.Value;
        record = record with { Fallen = ValueList<string>.From(Enumerable.Range(0, beds - record.Roster.Count).Select(i => "lost" + i)) };
        var opening = Beside(record, "keziah");
        var talked = Resolver.Apply(opening, Content, new Talk("keziah", "rook")).Next;

        var after = record.AfterBattle(Won(talked, opening), Content);

        Assert.Equal(ClaimantFate.TurnedAway, after.Returned);
        Assert.Null(after.Find("rook"));
    }

    [Fact]
    public void SparedKilledOrLeftStandingTheyNeverJoin()
    {
        var record = AtTheField("keziah");
        var captain = Content.Cast[0].Id;
        var spareOpening = Beside(record, captain);
        var spared = Resolver.Apply(spareOpening, Content, new Talk(captain, "rook")).Next;
        var opening = record.Begin(Field, Content);

        Assert.Equal(ClaimantFate.Spared, record.AfterBattle(Won(spared, spareOpening), Content).Returned);
        Assert.Equal(ClaimantFate.Fell, record.AfterBattle(Won(opening.WithoutUnit("rook"), opening), Content).Returned);
        Assert.Equal(ClaimantFate.Stood, record.AfterBattle(opening with { Units = ValueList<BattleUnit>.From(opening.Units.Where(u => u.Side == Side.Player || u.Id == "rook")), Turn = 4, History = ValueList<BattleState>.Of(opening) }, Content).Returned);
        Assert.All(new[] { spared, opening.WithoutUnit("rook") }, end => Assert.Null(record.AfterBattle(Won(end, opening), Content).Find("rook")));
    }

    [Theory]
    [InlineData(ClaimantFate.Turned)]
    [InlineData(ClaimantFate.TurnedAway)]
    [InlineData(ClaimantFate.Spared)]
    [InlineData(ClaimantFate.Fell)]
    [InlineData(ClaimantFate.Stood)]
    public void TheFateRoundTripsThroughTheSave(ClaimantFate fate)
    {
        var record = AtTheField("keziah") with { Returned = fate };

        Assert.Equal(fate, ProtocolJson.ReadCampaign(ProtocolJson.Campaign(record), Content).Returned);
        Assert.DoesNotContain("\"returned\"", ProtocolJson.Campaign(AtTheField("keziah")));
    }

    [Fact]
    public void ASavedFateThatIsNotOneIsRefused()
    {
        var json = ProtocolJson.Campaign(AtTheField("keziah") with { Returned = ClaimantFate.Fell }).Replace("\"returned\":\"fell\"", "\"returned\":\"won\"");

        var e = Assert.Throws<ProtocolException>(() => ProtocolJson.ReadCampaign(json, Content));

        Assert.Contains("returned 'won' is not one of turned, turnedAway, spared, fell, stood", e.Message);
    }

    [Fact]
    public void TheBoardAndTheCampPrintTheReturn()
    {
        var record = AtTheField("keziah");
        var opening = record.Begin(Field, Content);
        var level = record.ReturnLevel(Content);

        Assert.Equal("Rook came back with the enemy: Keziah's talk turns her, the captain's spares her", Returned.Line(opening, Content, UnitNames.Of(opening, Content)));
        Assert.Contains("Rook came back with the enemy: Keziah's talk turns her, the captain's spares her", MapRenderer.Render(opening, Content, null));
        Assert.Equal(
            new[]
            {
                $"Rook (Skyrider, level {level}) rides with the enemy on this map.",
                $"Keziah's talk turns her; the captain's spares her (talk <unit> rook). Turned, she joins only if a bed is free (beds: {record.BedsTaken}/{record.Beds(Content)}), and a death never frees one.",
            },
            CampaignSession.BranchLines(record, Content));
        Assert.Equal(new[] { "Rook came back with the enemy: spared, and gone." }, CampaignSession.BranchLines(record with { Returned = ClaimantFate.Spared, MapIndex = record.MapIndex + 1 }, Content));
    }

    [Fact]
    public void TheClientOffersTheTalkBesideThem()
    {
        var record = AtTheField("keziah");
        var session = new ClientSession(Content, Beside(record, "keziah"));
        session.Select(new Coord(7, 12));

        var row = Assert.Single(session.Actions(), r => r.Command is Talk);

        Assert.Equal("Talk to Rook", row.Label);
        Assert.Null(row.Refusal);
    }

    [Fact]
    public void TheTalkRoundTripsThroughTheProtocol()
    {
        var talk = new Talk("keziah", "rook");

        Assert.Equal(talk, ProtocolJson.ReadCommand(ProtocolJson.Command(talk)));
    }

    /// <summary>The shipped content's files with <paramref name="maps"/> as the campaign's maps.</summary>
    private static ContentFiles With(string maps) =>
        ContentSerializer.Write(Content) with { Campaign = new ContentFile(ContentFiles.CampaignName, $$"""{ "startingPurse": 0, "certificationPrice": 0, "maps": [ {{maps}} ] }""") };

    [Fact]
    public void AReturnRoundTripsThroughTheSerializer()
    {
        var content = ContentLoader.Parse(With("""{ "map": "one", "reward": 0, "stock": [], "branch": ["keziah", "rook"] }, { "map": "two", "reward": 0, "stock": [], "return": { "at": "3,4", "group": "line", "behavior": "hold" } }"""));

        Assert.Equal(new CampaignReturn(new Coord(3, 4), "line", Behavior.Hold), ContentLoader.Parse(ContentSerializer.Write(content)).Campaign.Maps[1].Return);
    }

    [Theory]
    [InlineData("""{ "map": "one", "reward": 0, "stock": [], "return": { "at": "3,4", "group": "line", "behavior": "hold" } }""", "one", "return", "must come after the map with the branch")]
    [InlineData("""{ "map": "one", "reward": 0, "stock": [], "branch": ["keziah", "rook"], "return": { "at": "3,4", "group": "line", "behavior": "hold" } }""", "one", "return", "must come after the map with the branch")]
    [InlineData("""{ "map": "one", "reward": 0, "stock": [], "branch": ["keziah", "rook"] }, { "map": "two", "reward": 0, "stock": [], "return": { "at": "3,4", "group": "line", "behavior": "hold" } }, { "map": "three", "reward": 0, "stock": [], "return": { "at": "3,4", "group": "line", "behavior": "hold" } }""", "three", "return", "only one map may bring the passed claimant back")]
    [InlineData("""{ "map": "one", "reward": 0, "stock": [], "branch": ["keziah", "rook"] }, { "map": "two", "reward": 0, "stock": [], "return": { "at": "3", "group": "line", "behavior": "hold" } }""", "two", "return.at", "is not a tile")]
    [InlineData("""{ "map": "one", "reward": 0, "stock": [], "branch": ["keziah", "rook"] }, { "map": "two", "reward": 0, "stock": [], "return": { "at": "3,4", "group": "", "behavior": "hold" } }""", "two", "return.group", "must name the map's group")]
    [InlineData("""{ "map": "one", "reward": 0, "stock": [], "branch": ["keziah", "rook"] }, { "map": "two", "reward": 0, "stock": [], "return": { "at": "3,4", "group": "line", "behavior": "boss" } }""", "two", "return.behavior", "unknown behavior 'boss'")]
    public void ABadReturnIsRefusedNamingTheMapAndTheField(string maps, string entry, string field, string message)
    {
        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(With(maps)));

        Assert.Equal((ContentFiles.CampaignName, entry, field), (e.File, e.Entry, e.Field));
        Assert.Contains(message, e.Message);
    }

    [Fact]
    public void TheShippedReturnTileIsFreeAndStandableForBothClaimantsInTheMapsOwnGroup()
    {
        var field = Field;
        var back = Content.Campaign.Maps[Content.Campaign.MapIndexOf("the_field")].Return!;

        Assert.DoesNotContain(field.Placements, p => p.At == back.At);
        Assert.Contains(field.Placements.OfType<EnemyPlacement>(), p => p.Group == back.Group);
        foreach (var claimant in new[] { "keziah", "rook" })
        {
            Assert.True(field.TerrainAt(back.At, Content).IsPassable(Content.Class(Content.Unit(claimant).ClassId).Movement));
        }
    }
}
