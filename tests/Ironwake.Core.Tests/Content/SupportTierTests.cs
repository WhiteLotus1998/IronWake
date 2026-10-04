using System.Text.Json.Nodes;
using Ironwake.Content;

namespace Ironwake.Core.Tests.Content;

/// <summary>The C/B/A support tiers on 13.1's rapport (issue 77, slice 2), in <c>rules.json</c>'s rivalry block.</summary>
public class SupportTierTests
{
    private static readonly Lazy<GameContent> Real = new(() => ContentLoader.Load(Fixture.RealContentDirectory()));

    private static GameContent Content => Real.Value;

    /// <summary>The real content written back, its rivalry block's tiers rewritten by <paramref name="edit"/>.</summary>
    private static ContentFiles WithTiers(Action<JsonArray> edit)
    {
        var files = ContentSerializer.Write(Content);
        var root = JsonNode.Parse(files.Rules.Text)!.AsObject();
        edit(root["rivalry"]!["supportTiers"]!.AsArray());
        return files with { Rules = new ContentFile(files.Rules.Name, root.ToJsonString()) };
    }

    private static ContentException Refused(Action<JsonArray> edit) =>
        Assert.Throws<ContentException>(() => ContentLoader.Parse(WithTiers(edit)));

    [Theory]
    [InlineData(0, null)]
    [InlineData(15, null)]
    [InlineData(16, "C")]
    [InlineData(27, "C")]
    [InlineData(28, "B")]
    [InlineData(47, "B")]
    [InlineData(48, "A")]
    [InlineData(500, "A")]
    public void A_support_pair_stands_at_the_highest_tier_its_rapport_reaches(int points, string? tier)
    {
        Assert.Equal(tier, Supports.TierOf(Content, "pell", "maud", points)?.Name);
        Assert.Equal(tier, Supports.TierOf(Content, "maud", "pell", points)?.Name);
    }

    [Fact]
    public void Two_people_who_are_no_support_pair_never_reach_a_tier()
    {
        Assert.Null(Supports.Pair(Content.Campaign, "wren", "brannock"));
        Assert.Null(Supports.TierOf(Content, "wren", "brannock", 500));
    }

    [Fact]
    public void The_C_tier_is_where_a_rivalry_ends()
    {
        Assert.Equal(Content.Rivalry.OverwriteAt, Content.Rivalry.SupportTiers[0].At);
    }

    [Fact]
    public void The_tiers_survive_a_write_and_a_read()
    {
        var again = ContentLoader.Parse(ContentSerializer.Write(Content));
        Assert.Equal(Content.Rivalry.SupportTiers, again.Rivalry.SupportTiers);
    }

    [Fact]
    public void Content_without_tiers_has_none()
    {
        var files = WithTiers(_ => { });
        var root = JsonNode.Parse(files.Rules.Text)!.AsObject();
        root["rivalry"]!.AsObject().Remove("supportTiers");
        var loaded = ContentLoader.Parse(files with { Scenes = null, Rules = new ContentFile(files.Rules.Name, root.ToJsonString()) });
        Assert.Empty(loaded.Rivalry.SupportTiers);
        Assert.Null(loaded.Rivalry.TierFor(500));
    }

    [Fact]
    public void Tiers_other_than_C_B_and_A_are_refused()
    {
        var error = Refused(t => t.RemoveAt(2));
        Assert.Equal("rivalry.supportTiers", error.Field);
    }

    [Fact]
    public void Tiers_out_of_order_are_refused()
    {
        var error = Refused(t => t[0]!["tier"] = "B");
        Assert.Equal("tier", error.Field);
        Assert.Contains("rivalry.supportTiers[0]", error.Entry);
    }

    [Fact]
    public void A_first_tier_under_the_overwrite_is_refused()
    {
        var error = Refused(t => t[0]!["at"] = 15);
        Assert.Equal("at", error.Field);
        Assert.Contains("rivals and supported at once", error.Message);
    }

    [Fact]
    public void A_tier_that_does_not_rise_is_refused()
    {
        var error = Refused(t => t[1]!["at"] = 16);
        Assert.Equal("at", error.Field);
        Assert.Contains("rivalry.supportTiers[1]", error.Entry);
    }

    [Theory]
    [InlineData("hit")]
    [InlineData("avoid")]
    [InlineData("crit")]
    public void A_higher_tier_giving_less_is_refused(string field)
    {
        var error = Refused(t =>
        {
            t[1]![field] = 20;
            t[2]![field] = 19;
        });
        Assert.Equal(field, error.Field);
        Assert.Contains("rivalry.supportTiers[2]", error.Entry);
    }

    [Fact]
    public void A_negative_bonus_is_refused()
    {
        var error = Refused(t => t[0]!["hit"] = -1);
        Assert.Equal("hit", error.Field);
    }
}
