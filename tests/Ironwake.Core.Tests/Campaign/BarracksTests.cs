using System.Text.Json.Nodes;
using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Campaign;

/// <summary>
/// Issue 690, the barracks (DESIGN section 13.20): a keep room opened after the raid that adds
/// beds and a list of hires, each joining at the base class below the company's average level
/// with growths below the cast's, from the one purse, through the company cap and the beds.
/// </summary>
public class BarracksTests
{
    private static GameContent Content => MapFixture.Content;

    private static KeepMenu Menu => Content.Campaign.Keep;

    private static int IndexOf(string id) => Content.Campaign.Maps.Select(m => m.MapId).ToList().IndexOf(id);

    /// <summary>A record past the raid with <paramref name="purse"/> in hand and the barracks built.</summary>
    private static CampaignRecord WithBarracks(int purse = 5000)
    {
        var record = CampaignRecord.StartAt(Content, 690, "sallow_grange") with { Purse = purse };
        var built = record.BuildRoom("barracks", Content);
        Assert.True(built.Accepted, built.Text);
        return built.Record;
    }

    /// <summary>The record with its last <paramref name="n"/> roster members fallen, beds kept.</summary>
    private static CampaignRecord LoseLast(CampaignRecord record, int n) => record with
    {
        Roster = ValueList<Unit>.From(record.Roster.Take(record.Roster.Count - n)),
        Fallen = ValueList<string>.From(record.Fallen.Concat(record.Roster.Skip(record.Roster.Count - n).Select(u => u.Id))),
    };

    [Fact]
    public void TheBarracksIsRefusedBeforeTheRaid()
    {
        var beforeRaid = CampaignRecord.StartAt(Content, 690, "ironwake_raid") with { Purse = 5000 };

        var refused = beforeRaid.BuildRoom("barracks", Content);

        Assert.False(refused.Accepted);
        Assert.Equal("Barracks opens once ironwake_raid is won", refused.Text);
        Assert.True((beforeRaid with { MapIndex = beforeRaid.MapIndex + 1 }).BuildRoom("barracks", Content).Accepted);
    }

    [Fact]
    public void TheBarracksAddsTwoBedsAndItsWingOneMoreOnlyAfterIt()
    {
        var record = CampaignRecord.StartAt(Content, 690, "sallow_grange") with { Purse = 5000 };

        var wing = record.BuildRoom("barracks_wing", Content);
        Assert.False(wing.Accepted);
        Assert.Equal("Barracks wing needs the Barracks built first", wing.Text);

        var barracks = WithBarracks();
        Assert.Equal(Menu.Beds + 2, barracks.Beds(Content));
        var winged = barracks.BuildRoom("barracks_wing", Content);
        Assert.True(winged.Accepted, winged.Text);
        Assert.Equal(Menu.Beds + 3, winged.Record.Beds(Content));
        Assert.Equal(new[] { 500, 300 }, new[] { Menu.Room("barracks")!.Price, Menu.Room("barracks_wing")!.Price });
    }

    [Fact]
    public void AHireIsRefusedUntilTheRoomThatListsThemIsBuilt()
    {
        var bare = CampaignRecord.StartAt(Content, 690, "sallow_grange") with { Purse = 5000 };

        Assert.Equal("corin is hired once the Barracks is built", bare.HireRefusal("corin", Content));
        Assert.Equal("tamsin is hired once the Barracks wing is built", WithBarracks().HireRefusal("tamsin", Content));
        Assert.Equal("the barracks lists no 'nobody'", WithBarracks().HireRefusal("nobody", Content));
    }

    [Fact]
    public void AHireCostsThePriceAndJoinsTheEndOfTheRoster()
    {
        var record = LoseLast(WithBarracks(1000), 2);

        var hired = record.Hire("corin", Content);

        Assert.True(hired.Accepted, hired.Text);
        Assert.Equal(1000 - 500 - Menu.HirePrice, hired.Record.Purse);
        Assert.Equal("corin", hired.Record.Roster[^1].Id);
        Assert.Equal(record.Living + 1, hired.Record.Living);
        Assert.Equal(300, Menu.HirePrice);
    }

    [Fact]
    public void AHireIsRefusedOnAShortPurseWithThePrice()
    {
        var record = LoseLast(WithBarracks(500 + 299), 1);

        Assert.Equal("a hire costs 300 and the purse holds 299", record.HireRefusal("corin", Content));
        Assert.False(record.Hire("corin", Content).Accepted);
    }

    [Fact]
    public void AHireIsRefusedByTheCompanyCapWithTheLine()
    {
        var full = WithBarracks();
        var padded = full with { Roster = ValueList<Unit>.From(full.Roster.Concat(new[] { "spare", "spare2" }.Select(id => full.Roster[1] with { Id = id })).Take(CampaignRecord.CompanyCap)) };

        Assert.Equal(CampaignRecord.CompanyCap, padded.Living);
        Assert.Equal("company full (12): corin will not join", padded.HireRefusal("corin", Content));
        Assert.Equal("Company full (12): Corin Ashby will not join", CampaignSession.Text(padded, Content, padded.Hire("corin", Content).Text));
    }

    [Fact]
    public void AHireIsRefusedWhenNoBedIsFree()
    {
        var record = LoseLast(WithBarracks(), 3);
        var beds = Content with { Campaign = Content.Campaign with { Keep = Menu with { Beds = record.BedsTaken - 2 } } };

        Assert.Equal(0, record.FreeBeds(beds));
        Assert.Equal("no bed free: corin will not join", record.HireRefusal("corin", beds));
    }

    [Fact]
    public void AHireIsNeverHiredTwiceAndAFallenHireStaysFallen()
    {
        var hired = LoseLast(WithBarracks(), 2).Hire("bet", Content).Record;

        Assert.Equal("bet is already in the company", hired.HireRefusal("bet", Content));
        var fell = LoseLast(hired, 1);
        Assert.Equal("bet has fallen", fell.HireRefusal("bet", Content));
        Assert.DoesNotContain(fell.HiresOffered(Content), h => h.Id == "bet");
        Assert.Contains("Bet Lowry", CampaignSession.RosterLines(fell, Content).Last());
    }

    [Fact]
    public void AHiresGrowthsAreTheCastClassLessTenWithAFloorOfFive()
    {
        var pikemen = Content.Cast.Skip(1).Where(u => u.ClassId == "pikeman").ToList();
        var growths = Barracks.Growths("pikeman", Content);

        foreach (var stat in Stats.All)
        {
            var mean = pikemen.Sum(u => u.Growths.Get(stat)) / pikemen.Count;
            Assert.Equal(Math.Max(5, mean - 10), growths.Get(stat));
        }

        Assert.Equal(5, growths.Mag);
        Assert.True(Stats.All.All(s => growths.Get(s) < pikemen[0].Growths.Get(s) || growths.Get(s) == 5));
    }

    [Fact]
    public void ACadetHireIsReadFromTheCadetsButNeverTheCaptain()
    {
        var cadets = Content.Cast.Skip(1).Where(u => u.ClassId == "cadet").ToList();

        Assert.DoesNotContain(Barracks.Models("cadet", Content), u => u.Id == Content.Cast[0].Id);
        Assert.Equal(Math.Max(5, (cadets.Sum(u => u.Growths.Cha) / cadets.Count) - 10), Barracks.Growths("cadet", Content).Cha);
    }

    [Fact]
    public void AHireJoinsTwoLevelsBelowTheCompanysAverageNeverBelowOne()
    {
        var record = WithBarracks();
        var levelled = record with { Roster = ValueList<Unit>.From(record.Roster.Select((u, i) => u with { Level = i < 5 ? 8 : 5 })) };
        var mean = levelled.Roster.Sum(u => u.Level) / levelled.Roster.Count;

        Assert.Equal(1, Barracks.JoinLevel(record));
        Assert.Equal(mean - 2, Barracks.JoinLevel(levelled));

        var unit = Barracks.Recruit(Menu.Hire("ines")!, 5, Content);
        var growths = Barracks.Growths("bowman", Content);
        var ottilie = Content.Unit("ottilie");
        var levelling = growths + Content.Class("bowman").GrowthModifiers;
        Assert.Equal(ottilie.Stats.Dex + (levelling.Dex * 4 / 100), unit.Stats.Dex);
        Assert.Equal((5, 0, "bowman"), (unit.Level, unit.Exp, unit.ClassId));
        Assert.Equal(new[] { new ItemStack("iron_bow", 40) }, unit.Inventory.Items);
    }

    [Fact]
    public void AHireHasNoRegionHooksAbilitiesOrSignatureSoNoRivalryOrSupports()
    {
        var unit = Barracks.Recruit(Menu.Hire("corin")!, 3, Content);

        Assert.Null(unit.Region);
        Assert.Empty(unit.Hooks);
        Assert.Empty(unit.Abilities);
        Assert.False(Content.Signatures.ContainsKey(unit.Id));
        Assert.Equal(Pronoun.He, unit.Pronoun);
    }

    [Fact]
    public void TheEndingLinePrintsForALivingHireOnly()
    {
        var hired = LoseLast(WithBarracks(), 2).Hire("mattias", Content).Record.Hire("ines", Content).Record;
        var oneFell = LoseLast(hired, 1);

        Assert.Equal(new[] { "Mattias Grue served at the keep.", "Ines Tarrow served at the keep." }, CampaignSession.EndingLines(hired, Content));
        Assert.Equal(new[] { "Mattias Grue served at the keep." }, CampaignSession.EndingLines(oneFell, Content));
        Assert.Empty(CampaignSession.EndingLines(CampaignRecord.Start(Content, 1), Content));
    }

    [Fact]
    public void TheKeepPanelPrintsEachHireCardAndLineOnceTheBarracksIsBuilt()
    {
        var bare = CampaignRecord.StartAt(Content, 690, "sallow_grange");
        Assert.DoesNotContain(CampaignSession.RoomLines(bare, Content), l => l.StartsWith("Hires", StringComparison.Ordinal));
        Assert.Contains("  barracks: Barracks, 500, +2 beds and 4 hires at 300, built 0 of 1", CampaignSession.RoomLines(bare, Content));

        var lines = CampaignSession.RoomLines(WithBarracks(), Content);
        Assert.Contains("Hires: 300 each, joining at L1, the company's average less 2; hire <id>", lines);
        Assert.Contains(lines, l => l.StartsWith("  corin: Corin Ashby (he), Cadet L1, Iron Sword; hp ", StringComparison.Ordinal) && l.Contains("; growths hp "));
        Assert.Contains("    Cooked for a garrison twenty years. Still counts heads at supper.", lines);
        Assert.DoesNotContain(lines, l => l.StartsWith("  tamsin", StringComparison.Ordinal));
        var bet = Barracks.Recruit(Menu.Hire("bet")!, 1, Content).EffectiveStats(Content.Class("pikeman"));
        Assert.Contains(lines, l => l.StartsWith($"  bet: Bet Lowry (she), Pikeman L1, Iron Lance; hp {bet.Hp} str {bet.Str} ", StringComparison.Ordinal));
    }

    [Fact]
    public void AHireSurvivesASaveWithTheirCardAndPronoun()
    {
        var hired = LoseLast(WithBarracks(), 1).Hire("tamsin", Content);
        Assert.False(hired.Accepted);
        var record = LoseLast(WithBarracks(), 1).Hire("wat", Content);
        Assert.False(record.Accepted);
        var winged = LoseLast(WithBarracks(), 1).BuildRoom("barracks_wing", Content).Record.Hire("tamsin", Content).Record;

        var read = ProtocolJson.ReadCampaign(ProtocolJson.Campaign(winged), Content);

        Assert.Equal(winged.Roster[^1], read.Roster[^1]);
        Assert.Equal(Pronoun.She, read.Roster[^1].Pronoun);
    }

    private static bool Flies(Unit unit) => Content.Class(unit.ClassId).Movement == MovementType.Flying;

    [Fact]
    public void AKeziahPickCampaignHiresAFlierFromTheWingAtTheRaidsCamp()
    {
        var bothSides = WithBarracks();
        var keziahPick = bothSides with { Roster = ValueList<Unit>.From(bothSides.Roster.Where(u => u.Id != "rook")) };
        Assert.Contains(keziahPick.Roster, u => u.Id == "keziah");
        Assert.DoesNotContain(keziahPick.Roster, Flies);
        Assert.Equal("edda is hired once the Barracks wing is built", keziahPick.HireRefusal("edda", Content));

        var winged = keziahPick.BuildRoom("barracks_wing", Content);
        Assert.True(winged.Accepted, winged.Text);
        var hired = winged.Record.Hire("edda", Content);

        Assert.True(hired.Accepted, hired.Text);
        Assert.Equal("edda", Assert.Single(hired.Record.Roster, Flies).Id);
        Assert.Equal(new[] { "tamsin", "wat", "edda" }, Menu.Room("barracks_wing")!.Hires);
        Assert.Equal("ironwake_raid", Menu.Room("barracks_wing")!.After);
    }

    [Fact]
    public void TheSkyriderHireIsReadFromRookAndGrowsBelowHer()
    {
        var rook = Content.Unit("rook");
        var edda = Barracks.Recruit(Menu.Hire("edda")!, 1, Content);

        Assert.Equal(("skyrider", Pronoun.She, rook.Stats), (edda.ClassId, edda.Pronoun, edda.Stats));
        Assert.Equal(new[] { new ItemStack("iron_lance", Content.Weapons["iron_lance"].Durability) }, edda.Inventory.Items);
        Assert.All(Stats.All, s => Assert.Equal(Math.Max(Barracks.GrowthFloor, rook.Growths.Get(s) - Barracks.GrowthPenalty), edda.Growths.Get(s)));
        Assert.True(Stats.All.Sum(s => edda.Growths.Get(s)) < Stats.All.Sum(s => rook.Growths.Get(s)));
        Assert.Null(edda.Region);
    }

    private static string Keep(Func<JsonNode, JsonNode> edit, out ContentFiles files)
    {
        files = ContentSerializer.Write(Content);
        var campaign = JsonNode.Parse(files.Campaign!.Text)!;
        edit(campaign["keep"]!);
        return campaign.ToJsonString();
    }

    private static ContentException Fails(Func<JsonNode, JsonNode> edit)
    {
        var text = Keep(edit, out var files);
        return Assert.Throws<ContentException>(() => ContentLoader.Parse(files with { Campaign = files.Campaign! with { Text = text } }));
    }

    private static JsonNode Hire(JsonNode keep, string id) => keep["hires"]!.AsArray().First(h => (string)h!["id"]! == id)!;

    [Fact]
    public void TheShippedHiresRoundTripThroughTheSerializer()
    {
        Assert.Equal(Menu, ContentLoader.Parse(ContentSerializer.Write(Content)).Campaign.Keep);
        Assert.Equal(7, Menu.Hires.Count);
        Assert.Equal(2, Menu.Hires.Select(h => h.Pronoun).Distinct().Count());
    }

    [Fact]
    public void AHireIsRefusedOnLoadForABadClassItemOrId()
    {
        Assert.Equal(("keep.hires.corin", "class"), Entry(Fails(k => { Hire(k, "corin")["class"] = "skyrider_x"; return k; })));
        Assert.Equal(("keep.hires.bet", "items"), Entry(Fails(k => { Hire(k, "bet")["items"] = new JsonArray("iron_bow"); return k; })));
        Assert.Equal(("keep.hires.bet", "items"), Entry(Fails(k => { Hire(k, "bet")["items"] = new JsonArray("no_such_item"); return k; })));
        Assert.Equal(("keep.hires.wren", "id"), Entry(Fails(k => { Hire(k, "corin")["id"] = "wren"; return k; })));
        Assert.Equal(("keep.hires.ines", "line"), Entry(Fails(k => { Hire(k, "ines")["line"] = " "; return k; })));
    }

    [Fact]
    public void AHireClassWithOnlyTheCaptainIsRefusedOnLoadAndHasNoCard()
    {
        var files = ContentSerializer.Write(Content);
        var castFile = files.Units.Single(f => f.Name.EndsWith("cast.json", StringComparison.Ordinal));
        var cast = JsonNode.Parse(castFile.Text)!;
        foreach (var unit in cast["units"]!.AsArray().Where(u => (string)u!["id"]! is "wren" or "brannock"))
        {
            unit!["class"] = "outrider";
            unit["inventory"] = new JsonArray(JsonNode.Parse("""{ "item": "iron_sword" }"""));
        }

        var units = files.Units.Select(f => f == castFile ? f with { Text = cast.ToJsonString() } : f).ToList();
        var error = Assert.Throws<ContentException>(() => ContentLoader.Parse(files with { Units = units }));

        Assert.Equal(("keep.hires.corin", "class"), Entry(error));
        Assert.Contains("no cast member but the captain is a cadet", error.Message);
        var captainOnly = Content with { Cast = ValueList<Unit>.From(Content.Cast.Take(1)) };
        Assert.Throws<InvalidOperationException>(() => Barracks.Growths("cadet", captainOnly));
    }

    [Fact]
    public void ARoomsHiresAndRequiresMustNameTheKeepsOwn()
    {
        Assert.Equal("rooms.barracks.hires", Fails(k => { k["rooms"]!.AsArray()[2]!["hires"] = new JsonArray("corin", "nobody"); return k; }).Field);
        Assert.Equal("rooms.barracks_wing.requires", Fails(k => { k["rooms"]!.AsArray()[3]!["requires"] = "chapel"; return k; }).Field);
        Assert.Equal("hires.wat", Fails(k => { k["rooms"]!.AsArray()[3]!["hires"] = new JsonArray("tamsin"); return k; }).Field);
        Assert.Equal("hirePrice", Fails(k => { k.AsObject().Remove("hirePrice"); return k; }).Field);
    }

    [Fact]
    public void AFinaleRankNamesACastMemberATypeTheirClassUsesAndAtLeastOnePoint()
    {
        Assert.Equal("finaleRanks.nobody", Fails(k => { k["finaleRanks"] = JsonNode.Parse("""{ "nobody": { "sword": 40 } }"""); return k; }).Field);
        Assert.Equal("finaleRanks.pell.bow", Fails(k => { k["finaleRanks"] = JsonNode.Parse("""{ "pell": { "bow": 40 } }"""); return k; }).Field);
        Assert.Equal("finaleRanks.pell.reason", Fails(k => { k["finaleRanks"] = JsonNode.Parse("""{ "pell": { "reason": 0 } }"""); return k; }).Field);
        Assert.Equal("finaleRanks.pell.reason", Fails(k => { k["finaleRanks"] = JsonNode.Parse("""{ "pell": { "reason": "C" } }"""); return k; }).Field);
        Assert.Equal("finaleRanks.pell", Fails(k => { k["finaleRanks"] = JsonNode.Parse("""{ "pell": 40 }"""); return k; }).Field);
    }

    private static (string? Entry, string? Field) Entry(ContentException e) => (e.Entry, e.Field);
}

[Collection("console")]
public class BarracksTranscriptTests
{
    [Fact]
    public void TheJournaledBarracksCampReplaysToItsTranscript()
    {
        var transcripts = Path.Combine(Directory.GetParent(Ironwake.Core.Tests.Content.Fixture.RealContentDirectory())!.FullName, "docs", "transcripts");
        var script = Path.Combine(transcripts, "2026-10-01-campaign-690-barracks.script");
        var args = new[] { "campaign", "--from", "ironwake_raid", "--seed", "690", "--script", script, "--content", Ironwake.Core.Tests.Content.Fixture.CurveFreeContentDirectory() };

        var output = ConsoleCapture.Run(() => Ironwake.Cli.Program.Main(args));

        Assert.Contains("Bet Lowry joins as a Pikeman at L1 for 300, the purse holds 700; beds: 12/14\n", output);
        Assert.Contains("Deploys to Sallow Grange: Alder Fenn, Ansgar, Wren, Pell, Ottilie (deploy 5 of 11; 1 slot stands empty)", output);
        Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
    }
}
