using Ironwake.Cli;
using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Client;

/// <summary>
/// The campaign's story cards in the client (issue 786): the presenter queues the console's own
/// before, after, side map and lost cards in the order the console prints them, a map or a lost
/// side map without a card queues none, and the cards stay out of the event log.
/// </summary>
[Collection("console")]
public class StoryCardTests
{
    private const ulong Seed = 631;

    private static readonly GameContent Content = ContentLoader.Load(Fixture.RealContentDirectory());

    private static string AloneScript() =>
        "march\n" + File.ReadAllText(Path.Combine(ClientParityTests.Root(), "docs", "transcripts", "2026-10-01-starting_alone-631.script")) + "leave\n";

    private static CampaignClient Client() => new(Content, Fixture.RealContentDirectory(), CampaignRecord.Start(Content, Seed));

    /// <summary>Every card <paramref name="client"/> holds, put away one by one.</summary>
    private static List<IReadOnlyList<string>> Drain(CampaignClient client)
    {
        var cards = new List<IReadOnlyList<string>>();
        while (client.Card is { } card)
        {
            cards.Add(card);
            client.DismissCard();
        }

        return cards;
    }

    private static string ConsoleOutput(string script, ulong seed)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ironwake-cards-{Guid.NewGuid():N}.script");
        File.WriteAllText(path, script);
        try
        {
            return ConsoleCapture.Run(() => CampaignSession.Run(new[] { "--seed", seed.ToString(), "--script", path, "--content", Fixture.RealContentDirectory() }));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Starting Alone has no before card since its scene replaced it (issue 1005): a new campaign
    /// opens on no card, and <c>march</c> queues the scene, as the console prints it after the map line.
    /// </summary>
    [Fact]
    public void ANewCampaignOpensOnNoCardAndMarchQueuesTheFirstMapsBeforeScene()
    {
        var client = Client();

        Assert.Null(client.Card);
        Script.PlayCampaign(client, "march\n");
        Assert.Equal("-- Starting Alone --", client.Card![0]);
        Assert.Contains("Hask: Don't write, Alder. Bring them home.", client.Card);
        client.DismissCard();
        Assert.Null(client.Card);
    }

    /// <summary>
    /// The Mill has no before card since its scene replaced it (issue 1005): a won Starting Alone
    /// queues its after scene and the Mill's camp opens on no card, then <c>march</c> queues the
    /// Mill's before scene, all in the order the console prints them.
    /// </summary>
    [Fact]
    public void AWonMapQueuesItsAfterSceneThenMarchQueuesTheNextMapsBeforeSceneInTheConsolesOrder()
    {
        var client = Client();
        var log = Script.PlayCampaign(client, AloneScript());
        var cards = Drain(client);
        Assert.Equal(new[] { "-- Starting Alone --", "-- After Starting Alone --" }, cards.Select(c => c[0]));

        log += Script.PlayCampaign(client, "march\n");
        cards.AddRange(Drain(client));
        var output = ConsoleOutput(AloneScript() + "march\n", Seed);

        Assert.Equal(new[] { "-- Starting Alone --", "-- After Starting Alone --", "-- The Mill --" }, cards.Select(c => c[0]));
        Assert.Contains("Alder ties the horse to the dead mill's wheel and goes on afoot.", cards[2]);
        var at = 0;
        foreach (var card in cards)
        {
            var text = string.Join("\n", card) + "\n";
            var found = output.IndexOf(text, at, StringComparison.Ordinal);
            Assert.True(found >= at, card[0]);
            at = found + text.Length;
        }

        Assert.DoesNotContain("Bring them home", log);
        Assert.DoesNotContain("-- After Starting Alone --", log);
        Assert.DoesNotContain("goes on afoot", log);
    }

    [Fact]
    public void ALostMapQueuesTheLostCard()
    {
        var client = Client();
        Script.PlayCampaign(client, "march\n" + string.Concat(Enumerable.Repeat("end\n", 12)) + "leave\n");

        Assert.True(client.Over);
        Assert.Equal(CampaignSession.LostCard, Drain(client)[^1]);
    }

    [Fact]
    public void ASideMapQueuesItsBeforeCardAndALostOneNoAfterCard()
    {
        var client = new CampaignClient(Content, Fixture.RealContentDirectory(), CampActionsTests.Stocked());
        Assert.Null(client.Card);

        Script.PlayCampaign(client, File.ReadAllText(Path.Combine(ClientParityTests.Root(), "tests", "parity", "campaign", "camp-actions-41.script")));
        var cards = Drain(client);

        Assert.Equal(new[] { "-- The Lazar House --" }, cards.Select(c => c[0]));
        Assert.Equal(CampaignSession.QuestBeforeCard(Content, MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "quests", "the_lazar_house.map"), Content), "maud_1"), cards[0]);
    }
}
