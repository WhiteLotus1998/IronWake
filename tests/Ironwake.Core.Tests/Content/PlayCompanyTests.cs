using System.Text.RegularExpressions;
using Ironwake.Content;
using Ironwake.Sim;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// <c>play --level N --company full|depleted|floor</c> (issue 1217): a hand chair fields the Sim's
/// finale company, the same units at the same levels, on both maps of the depleted pair (round 411),
/// and the flag is refused where it cannot mean that.
/// </summary>
[Collection("console")]
public class PlayCompanyTests
{
    private static string Repo => Directory.GetParent(Fixture.RealContentDirectory())!.FullName;

    private static string HaskScenery => Path.Combine(Repo, "docs", "samples", "ironwake_keep_hask_scenery.map");

    private static string CampaignKeep => Path.Combine(Fixture.RealContentDirectory(), "keep", "ironwake_keep.map");

    private static string Play(out int exit, params string[] args)
    {
        var script = Path.Combine(Path.GetTempPath(), "ironwake-company-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllText(script, "");
        try
        {
            var code = 0;
            var output = ConsoleCapture.Run(() => code = Ironwake.Cli.Program.Main(["play", .. args, "--script", script, "--content", Fixture.RealContentDirectory()]));
            exit = code;
            return output;
        }
        finally
        {
            File.Delete(script);
        }
    }

    private static bool Fields(string output, Unit unit) =>
        Regex.IsMatch(output, $@"(?m)^\S\s+{Regex.Escape(unit.Id)}\s+{Regex.Escape(unit.Name)} L{unit.Level} ");

    [Fact]
    public void PlayCompanyDepletedFieldsTheSimsDepletedRosterOnBothMapsOfThePair()
    {
        var content = ContentLoader.Load(Fixture.RealContentDirectory());
        var roster = FinaleRun.Roster(content, FinaleCompany.Depleted, FinaleRun.DefaultLevel);
        var absent = content.Cast.Where(u => roster.All(r => r.Id != u.Id)).ToList();

        foreach (var map in new[] { HaskScenery, CampaignKeep })
        {
            var output = Play(out var exit, map, "--seed", "3", "--level", "8", "--company", "depleted");

            Assert.NotEqual(2, exit);
            Assert.Contains("\ncompany: depleted (captain, 5 story, 6 hires)\n", output);
            Assert.All(roster, u => Assert.True(Fields(output, u), $"{u.Id} L{u.Level} is not fielded on {map}"));
            Assert.NotEmpty(absent);
            Assert.All(absent, u => Assert.DoesNotMatch($@"(?m)^\S\s+{Regex.Escape(u.Id)}\s", output));
        }
    }

    [Fact]
    public void PlayCompanyFieldsEachCompanysRosterAndNamesItInTheHeader()
    {
        var content = ContentLoader.Load(Fixture.RealContentDirectory());
        foreach (var company in new[] { FinaleCompany.Full, FinaleCompany.Floor })
        {
            var roster = FinaleCompanies.Roster(content, company, 7);
            var hires = roster.Count(u => Barracks.IsHire(u, content));

            var output = Play(out _, CampaignKeep, "--seed", "3", "--level", "7", "--company", FinaleCompanies.Name(company));

            Assert.Contains($"\ncompany: {FinaleCompanies.Name(company)} (captain, {roster.Count - 1 - hires} story, {hires} {(hires == 1 ? "hire" : "hires")})\n", output);
            Assert.All(roster, u => Assert.True(Fields(output, u), $"{u.Id} L{u.Level} is not fielded as {company}"));
        }
    }

    [Fact]
    public void TheSimsFinaleRosterIsTheOneTheCliFields()
    {
        var content = ContentLoader.Load(Fixture.RealContentDirectory());
        foreach (var company in new[] { FinaleCompany.Full, FinaleCompany.Depleted, FinaleCompany.Floor })
        {
            Assert.Equal(FinaleCompanies.Roster(content, company, 8), FinaleRun.Fielded(content, company, 8).Cast);
        }
    }

    [Fact]
    public void TheFinaleCompanyIsKittedAsTheCampaignFieldsIt()
    {
        var content = ContentLoader.Load(Fixture.RealContentDirectory());
        var roster = FinaleCompanies.Roster(content, FinaleCompany.Full, 8);

        Assert.Equal("kinsbane", roster.Single(u => u.Id == "keziah").Inventory.Items[0].ItemId);
        Assert.Contains(roster.Single(u => u.Id == "teodor").Inventory.Items, s => s.ItemId == "family_lance");
        Assert.Equal(DrakeStage.HalfGrown, roster.Single(u => u.Id == "rook").Drake?.Stage);
        Assert.All(content.Cast.Skip(1), u => Assert.Equal(
            FinaleCompanies.Stocked(content.Campaign.Keep.FinaleRanked(CampaignRecord.Kitted(u, content).ScaledTo(8, content.Class(u.ClassId))), content),
            roster.Single(r => r.Id == u.Id)));
    }

    [Fact]
    public void TheFinaleCompanyCarriesTheKeepsBestStockWithinItsRanks()
    {
        var content = ContentLoader.Load(Fixture.RealContentDirectory());
        var roster = FinaleCompanies.Roster(content, FinaleCompany.Full, 8);
        var gained = roster
            .Select(u => (u.Id, Before: content.Campaign.Keep.FinaleRanked(CampaignRecord.Kitted(content.Cast.FirstOrDefault(c => c.Id == u.Id) ?? u, content).ScaledTo(8, content.Class(u.ClassId))), After: u))
            .Where(p => content.Cast.Any(c => c.Id == p.Id))
            .Select(p => $"{p.Id}: {string.Join(",", p.After.Inventory.Items.Select(s => s.ItemId).Except(p.Before.Inventory.Items.Select(s => s.ItemId)))}")
            .Where(line => !line.EndsWith(": ", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(["pell: bolt", "rook: steel_lance"], gained);
        Assert.Equal(["bolt", "cinder", "gust"], roster.Single(u => u.Id == "pell").Inventory.Items.Select(s => s.ItemId));
        Assert.Equal(["steel_lance", "iron_lance"], roster.Single(u => u.Id == "rook").Inventory.Items.Select(s => s.ItemId));
    }

    [Fact]
    public void AFinaleMemberTheStockOutranksNothingForIsUnchanged()
    {
        var content = ContentLoader.Load(Fixture.RealContentDirectory());
        var wren = CampaignRecord.Kitted(content.Cast.Single(u => u.Id == "wren"), content).ScaledTo(8, content.Class("cadet"));

        Assert.Equal(wren.Inventory.Items, FinaleCompanies.Stocked(wren, content).Inventory.Items);
    }

    [Fact]
    public void AFullPackDropsItsLastStackForTheStockWeapon()
    {
        var content = ContentLoader.Load(Fixture.RealContentDirectory());
        var rook = content.Campaign.Keep.FinaleRanked(content.Cast.Single(u => u.Id == "rook").ScaledTo(8, content.Class("skyrider")));
        var full = rook with { Inventory = new Inventory(ValueList<ItemStack>.From([
            new ItemStack("iron_lance", 40), new ItemStack("field_dressing", 3), new ItemStack("field_dressing", 3), new ItemStack("field_dressing", 3), new ItemStack("salve", 1)])) };

        var stocked = FinaleCompanies.Stocked(full, content);

        Assert.Equal(["steel_lance", "iron_lance", "field_dressing", "field_dressing", "field_dressing"], stocked.Inventory.Items.Select(s => s.ItemId));
    }

    [Fact]
    public void AFinaleRankRaisesAMembersPointsAndNeverLowersThem()
    {
        var shipped = ContentLoader.Load(Fixture.RealContentDirectory());
        var ranks = ValueList<(string Id, WeaponSkill Skill)>.From([
            ("pell", WeaponSkill.Zero.With(WeaponType.Reason, 100)),
            ("maud", WeaponSkill.Zero.With(WeaponType.Faith, 1)),
        ]);
        var content = shipped with { Campaign = shipped.Campaign with { Keep = shipped.Campaign.Keep with { FinaleRanks = ranks } } };
        var roster = FinaleCompanies.Roster(content, FinaleCompany.Full, 8);

        Assert.Equal(100, roster.Single(u => u.Id == "pell").Skill.Points(WeaponType.Reason));
        Assert.Equal(WeaponRanks.Threshold(WeaponRank.D), roster.Single(u => u.Id == "maud").Skill.Points(WeaponType.Faith));
        Assert.Equal(0, roster.Single(u => u.Id == "wren").Skill.Points(WeaponType.Sword));
    }

    [Fact]
    public void TheShippedFinaleRanksReachTheFinaleCompany()
    {
        var content = ContentLoader.Load(Fixture.RealContentDirectory());
        var roster = FinaleCompanies.Roster(content, FinaleCompany.Full, 8);

        Assert.NotEmpty(content.Campaign.Keep.FinaleRanks);
        Assert.All(content.Campaign.Keep.FinaleRanks, r => Assert.All(r.Skill.All.Where(t => t.Points > 0), t =>
            Assert.True(roster.Single(u => u.Id == r.Id).Skill.Points(t.Type) >= t.Points, $"{r.Id} {t.Type}")));
    }

    [Fact]
    public void PlayCompanyWithoutALevelIsRefused()
    {
        var output = Play(out var exit, CampaignKeep, "--company", "depleted");

        Assert.Equal(2, exit);
        Assert.Contains("ERROR: --company fields a finale company at a level; give --level", output);
    }

    [Fact]
    public void PlayCompanyWithACandidateIsRefused()
    {
        var output = Play(out var exit, CampaignKeep, "--level", "8", "--company", "depleted", "--candidate", "wren");

        Assert.Equal(2, exit);
        Assert.Contains("ERROR: --company fields a whole company and --candidate a single trial candidate; give one", output);
    }

    [Fact]
    public void PlayCompanyNamingNoCompanyIsRefused()
    {
        var output = Play(out var exit, CampaignKeep, "--level", "8", "--company", "half");

        Assert.Equal(2, exit);
        Assert.Contains("ERROR: unexpected argument '--company'", output);
    }

    [Fact]
    public void PlayCompanyOnAMapThatDoesNotDeployAllIsRefused()
    {
        var output = Play(out var exit, "the_tollgate", "--level", "8", "--company", "depleted");

        Assert.Equal(2, exit);
        Assert.Contains("ERROR: --company needs a 'deploy: all' map to seat the whole company, and 'The Tollgate' deploys 6", output);
    }

    [Fact]
    public void PlayCompanyOnAMapThatNamesAnAbsentMemberIsRefusedByName()
    {
        // One bare slot of the sample becomes Maud's named slot, and the depleted company has no Maud.
        var text = File.ReadAllText(HaskScenery).ReplaceLineEndings("\n").Replace("P recruit 15,6\n", "P recruit:maud 15,6\n", StringComparison.Ordinal);
        var map = Path.Combine(Path.GetTempPath(), "ironwake-company-" + Guid.NewGuid().ToString("N") + ".map");
        File.WriteAllText(map, text);
        try
        {
            var output = Play(out var exit, map, "--level", "8", "--company", "depleted");

            Assert.Equal(2, exit);
            Assert.Contains("ERROR: 'Ironwake Keep (Hask scenery)' places Maud by name, and the depleted company has no Maud", output);
        }
        finally
        {
            File.Delete(map);
        }
    }

    [Fact]
    public void PlayLevelWithoutACompanyStillFieldsTheMapsDeploy()
    {
        var output = Play(out _, CampaignKeep, "--seed", "3", "--level", "8");

        Assert.DoesNotContain("company:", output);
        Assert.Matches(@"(?m)^\S\s+maud\s+Maud L8 ", output);
    }
}
