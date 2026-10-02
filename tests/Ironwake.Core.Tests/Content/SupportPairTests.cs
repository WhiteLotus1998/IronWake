using System.Text.Json.Nodes;
using Ironwake.Content;

namespace Ironwake.Core.Tests.Content;

/// <summary>Support pairs and their kinds in <c>campaign.json</c> (issue 77, round 192).</summary>
public class SupportPairTests
{
    private static readonly Lazy<GameContent> Real = new(() => ContentLoader.Load(Fixture.RealContentDirectory()));

    private static GameContent Content => Real.Value;

    /// <summary>The real content written back, its campaign's supports rewritten by <paramref name="edit"/>.</summary>
    private static ContentFiles WithSupports(Action<JsonArray> edit)
    {
        var files = ContentSerializer.Write(Content);
        var root = JsonNode.Parse(files.Campaign!.Text)!.AsObject();
        var supports = root["supports"]!.AsArray();
        edit(supports);
        return files with { Campaign = new ContentFile(ContentFiles.CampaignName, root.ToJsonString()) };
    }

    private static JsonObject PairOf(JsonArray supports, string a, string b) =>
        supports.Select(n => n!.AsObject()).First(o =>
            ((string)o["a"]! == a && (string)o["b"]! == b) || ((string)o["a"]! == b && (string)o["b"]! == a));

    private static ContentException Refused(ContentFiles files) =>
        Assert.Throws<ContentException>(() => ContentLoader.Parse(files));

    [Fact]
    public void Every_recruit_has_one_captain_pair_and_three_or_four_partners()
    {
        var captain = Content.Cast[0].Id;
        foreach (var recruit in Content.Cast.Skip(1))
        {
            var mine = Supports.PairsOf(Content.Campaign, recruit.Id);
            Assert.Single(mine, p => p.Involves(captain));
            Assert.InRange(mine.Count - 1, 3, 4);
        }
    }

    [Fact]
    public void Every_hook_is_a_support_pair()
    {
        foreach (var recruit in Content.Cast.Skip(1))
        {
            foreach (var hook in recruit.Hooks)
            {
                Assert.NotNull(Supports.Pair(Content.Campaign, recruit.Id, hook));
            }
        }
    }

    [Fact]
    public void No_two_of_a_recruits_pairs_share_a_kind_with_either_captain()
    {
        foreach (var captain in new Pronoun?[] { Pronoun.He, Pronoun.She, Pronoun.They })
        {
            foreach (var recruit in Content.Cast.Skip(1))
            {
                var kinds = Supports.PairsOf(Content.Campaign, recruit.Id).Select(p => p.KindFor(captain)).ToList();
                Assert.Equal(kinds.Count, kinds.Distinct().Count());
            }
        }
    }

    [Fact]
    public void A_romance_is_between_two_recruits_who_share_a_pronoun()
    {
        Assert.Contains(Content.Campaign.Supports, p => p.Kind == SupportKind.Romance
            && p.A != Content.Cast[0].Id && Content.Pronouns[p.A] == Content.Pronouns[p.B]);
    }

    [Fact]
    public void A_captain_pair_is_a_romance_only_with_a_captain_the_recruit_can_romance()
    {
        var brannock = Supports.Pair(Content.Campaign, "captain", "brannock")!;
        Assert.Equal(SupportKind.Romance, brannock.KindFor(Pronoun.He));
        Assert.Equal(brannock.Kind, brannock.KindFor(Pronoun.She));
        Assert.NotEqual(SupportKind.Romance, brannock.Kind);
        var wren = Supports.Pair(Content.Campaign, "wren", "captain")!;
        Assert.Equal(wren.Kind, wren.KindFor(Pronoun.He));
    }

    [Fact]
    public void Both_claimants_can_romance_either_captain()
    {
        foreach (var claimant in new[] { "rook", "keziah" })
        {
            var pair = Supports.Pair(Content.Campaign, "captain", claimant)!;
            Assert.Equal(SupportKind.Romance, pair.KindFor(Pronoun.He));
            Assert.Equal(SupportKind.Romance, pair.KindFor(Pronoun.She));
        }
    }

    [Fact]
    public void Supports_round_trip_through_the_serializer()
    {
        var again = ContentLoader.Parse(ContentSerializer.Write(Content));
        Assert.Equal(Content.Campaign.Supports, again.Campaign.Supports);
    }

    [Fact]
    public void A_pair_naming_someone_outside_the_cast_is_refused()
    {
        var error = Refused(WithSupports(s => PairOf(s, "wren", "pell")["b"] = "nobody"));
        Assert.Equal(ContentFiles.CampaignName, error.File);
        Assert.Equal("b", error.Field);
        Assert.Contains("'nobody' is not a cast member", error.Message);
    }

    [Fact]
    public void A_pair_of_one_unit_with_itself_is_refused()
    {
        var error = Refused(WithSupports(s => PairOf(s, "wren", "pell")["b"] = "wren"));
        Assert.Equal("b", error.Field);
        Assert.Contains("two different cast members", error.Message);
    }

    [Fact]
    public void A_pair_listed_twice_is_refused()
    {
        var error = Refused(WithSupports(s => s.Add(new JsonObject { ["a"] = "pell", ["b"] = "wren", ["kind"] = "mentor" })));
        Assert.Equal("pell/wren", error.Entry);
        Assert.Contains("is listed twice", error.Message);
    }

    [Fact]
    public void An_unknown_kind_is_refused()
    {
        var error = Refused(WithSupports(s => PairOf(s, "wren", "pell")["kind"] = "feud"));
        Assert.Equal("kind", error.Field);
        Assert.Contains("'feud' is not one of", error.Message);
    }

    [Fact]
    public void A_romance_field_between_two_recruits_is_refused()
    {
        var error = Refused(WithSupports(s => PairOf(s, "wren", "pell")["romance"] = new JsonArray("she")));
        Assert.Equal("romance", error.Field);
        Assert.Contains("only a pair with the captain", error.Message);
    }

    [Fact]
    public void A_captain_pair_whose_kind_is_romance_is_refused()
    {
        var error = Refused(WithSupports(s => PairOf(s, "captain", "wren")["kind"] = "romance"));
        Assert.Equal("kind", error.Field);
        Assert.Contains("only through its romance field", error.Message);
    }

    [Fact]
    public void A_romance_pronoun_listed_twice_is_refused()
    {
        var error = Refused(WithSupports(s => PairOf(s, "captain", "rook")["romance"] = new JsonArray("he", "he")));
        Assert.Equal("romance", error.Field);
        Assert.Contains("'he' is listed twice", error.Message);
    }

    [Fact]
    public void A_recruit_without_a_captain_pair_is_refused()
    {
        var error = Refused(WithSupports(s => s.Remove(PairOf(s, "captain", "wren"))));
        Assert.Equal("wren", error.Entry);
        Assert.Equal("supports", error.Field);
        Assert.Contains("has 0 pairs with the captain", error.Message);
    }

    [Fact]
    public void A_recruit_with_two_partners_is_refused()
    {
        var error = Refused(WithSupports(s => s.Remove(PairOf(s, "wren", "pell"))));
        Assert.Equal("wren", error.Entry);
        Assert.Contains("has 2 partners besides the captain", error.Message);
    }

    [Fact]
    public void A_recruit_with_five_partners_is_refused()
    {
        var error = Refused(WithSupports(s => s.Add(new JsonObject { ["a"] = "keziah", ["b"] = "brannock", ["kind"] = "mentor" })));
        Assert.Equal("keziah", error.Entry);
        Assert.Contains("has 5 partners besides the captain", error.Message);
    }

    [Fact]
    public void Two_pairs_of_one_kind_are_refused()
    {
        var error = Refused(WithSupports(s => PairOf(s, "wren", "teodor")["kind"] = "comedy"));
        Assert.Equal("supports", error.Field);
        Assert.Contains("share the kind comedy", error.Message);
    }

    [Fact]
    public void A_captain_romance_counts_against_a_recruit_romance()
    {
        var error = Refused(WithSupports(s => PairOf(s, "captain", "pell")["romance"] = new JsonArray("he")));
        Assert.Equal("pell", error.Entry);
        Assert.Contains("share the kind romance", error.Message);
    }

    [Fact]
    public void Supports_without_a_same_pronoun_recruit_romance_are_refused()
    {
        var error = Refused(WithSupports(s => PairOf(s, "pell", "maud")["kind"] = "respect"));
        Assert.Equal("supports", error.Field);
        Assert.Contains("no romance between two recruits who share a pronoun", error.Message);
    }
}
