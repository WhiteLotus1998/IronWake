using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Campaign;

/// <summary>
/// Teodor's quest 2 names the lance and pays its art (issue 635 slice 12, rounds 268 to 271): The
/// Warden's Gate, won, renames the Family Lance the First Warden's Lance at whatever stage it has
/// reached and pays 3 frozen iron; Turn the Key, declared only with the lance woken and named,
/// strikes at -4 Mt, once, and locks a unit it hits and leaves standing: Mov 0 on the chill's clock
/// while Teodor stands beside it, dropping to the chill when he leaves or falls.
/// </summary>
public class WardensGateTests
{
    private const string LanceId = "family_lance";

    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    private static readonly HeirloomLadder Ladder = Shipped.Weapon(LanceId).Heirloom!;

    private static readonly Coord BrigandAt = new(6, 5);

    /// <summary>The Tollgate with Teodor below the brigand at 6,5, the lance in his first slot at <paramref name="stage"/>, named or not, and the brigand at full HP.</summary>
    private static BattleState Armed(int stage, bool named, ulong seed = 646)
    {
        var state = BattleState.From(MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "maps", "the_tollgate.map"), Shipped), Shipped, Shipped.Cast, seed);
        var teodor = state.Find("teodor")!;
        var stack = new ItemStack(LanceId, 40) { Combats = 12, Stage = stage, GateOpen = true, Named = named };
        return state.WithUnit(teodor with { At = new Coord(6, 6), Unit = teodor.Unit with { Inventory = teodor.Unit.Inventory.Replace(0, stack) } });
    }

    private static string Brigand(BattleState state) => state.UnitAt(BrigandAt)!.Id;

    private static ApplyResult Key(BattleState state) => Resolver.Apply(state, Shipped, new Attack("teodor", Brigand(state), null, "turn_the_key"));

    /// <summary>The first seed on which Turn the Key from 6,6 lands on the brigand and leaves it standing, and the state after.</summary>
    private static ApplyResult Locked()
    {
        foreach (var seed in Enumerable.Range(1, 60).Select(s => (ulong)s))
        {
            var result = Key(Armed(Ladder.Turns.Count, named: true, seed));
            if (result.Accepted && result.Events.OfType<UnitLocked>().Any())
            {
                return result;
            }
        }

        throw new InvalidOperationException("no seed in 1..60 locked the brigand");
    }

    [Fact]
    public void TurnTheKeyIsALanceArtAtMinusFourThatStrikesOnceAndLocks()
    {
        var art = Assert.IsType<CombatArtEffect>(Shipped.Ability("turn_the_key").Effect);

        Assert.Equal(new CombatArtEffect(WeaponType.Lance, WeaponRank.E, 3, -4, 0, 0, 0, 0) { Single = true, Woken = true, Locks = true, Item = LanceId }, art);
        Assert.Contains("turn_the_key", Shipped.Cast.Single(u => u.Id == "teodor").Abilities);
    }

    [Theory]
    [InlineData(3, false)]
    [InlineData(2, true)]
    public void TurnTheKeyWaitsUntilTheLanceIsWokenAndNamed(int stage, bool named)
    {
        var refused = Key(Armed(stage, named));

        Assert.False(refused.Accepted);
        Assert.EndsWith("Turn the Key waits until " + Heirloom.Shape(Shipped.Weapon(LanceId), new ItemStack(LanceId, 40) { Stage = stage, Named = named }).Name + " is woken and named", refused.Rejection!.Message);
    }

    [Fact]
    public void TurnTheKeyIsDeclaredWithTheWokenNamedLance()
    {
        Assert.Equal(3, Ladder.Turns.Count);
        Assert.True(Key(Armed(Ladder.Turns.Count, named: true)).Accepted);
    }

    [Fact]
    public void AHitThatLeavesItStandingLocksItAtMovZeroWhileTeodorStandsBeside()
    {
        var result = Locked();
        var brigand = result.Next.Find(result.Events.OfType<UnitLocked>().Single().UnitId)!;

        Assert.Equal("teodor", brigand.LockedBy);
        Assert.True(Lock.Holds(result.Next, brigand));
        Assert.Equal(new[] { brigand.At }, result.Next.ReachOf(brigand, Shipped).Destinations);
    }

    [Fact]
    public void TheLockDropsToTheChillWhenTeodorLeaves()
    {
        var locked = Locked().Next;
        var brigand = locked.UnitAt(BrigandAt)!;
        var teodor = locked.Find("teodor")!;
        var away = locked.WithUnit(teodor with { At = new Coord(6, 8) });

        var events = new List<GameEvent>();
        var after = Lock.After(away, events);

        Assert.Equal(new LockDropped(brigand.Id, "teodor"), Assert.Single(events));
        Assert.Null(after.Find(brigand.Id)!.LockedBy);
        Assert.True(after.Find(brigand.Id)!.Chill > 0);
        Assert.True(after.ReachOf(after.Find(brigand.Id)!, Shipped).Destinations.Count() > 1);
    }

    [Fact]
    public void TheLockDropsWhenTeodorFalls()
    {
        var locked = Locked().Next;
        var brigand = locked.UnitAt(BrigandAt)!;

        var events = new List<GameEvent>();
        var after = Lock.After(locked.WithoutUnit("teodor"), events);

        Assert.Equal(new LockDropped(brigand.Id, "teodor"), Assert.Single(events));
        Assert.Null(after.Find(brigand.Id)!.LockedBy);
    }

    [Fact]
    public void TheLockEndsWithTheChillsClock()
    {
        var state = Locked().Next;
        var brigand = state.UnitAt(BrigandAt)!.Id;
        state = Resolver.Apply(state, Shipped, new EndPhase()).Next;
        Assert.Equal("teodor", state.Find(brigand)?.LockedBy);

        var after = Resolver.Apply(state, Shipped, new EndPhase()).Next;

        Assert.Null(after.Find(brigand)?.LockedBy);
    }

    [Fact]
    public void AMissLocksNothingAndThePlainAttackNeverLocks()
    {
        var plain = Resolver.Apply(Armed(Ladder.Turns.Count, named: true), Shipped, new Attack("teodor", Brigand(Armed(Ladder.Turns.Count, named: true))));

        Assert.True(plain.Accepted);
        Assert.Empty(plain.Events.OfType<UnitLocked>());
        Assert.All(plain.Next.Units, u => Assert.Null(u.LockedBy));
    }

    [Fact]
    public void TheCardSaysWhoHoldsTheLock()
    {
        var state = Locked().Next;
        var brigand = state.UnitAt(BrigandAt)!;

        Assert.Equal("locked by Teodor: Mov 0 while Teodor stands beside, else chilled until " + Frost.Until(Side.Enemy, false), Lock.CardLine(state, brigand, "Teodor"));
    }

    [Fact]
    public void TheProtocolCarriesTheLockAndTheName()
    {
        var state = Locked().Next;
        var brigand = state.UnitAt(BrigandAt)!.Id;

        var json = ProtocolJson.State(state, Shipped);
        var back = ProtocolJson.ReadState(json, Shipped);

        Assert.Equal("teodor", back.Find(brigand)!.LockedBy);
        Assert.True(back.Find("teodor")!.Unit.Inventory.Items[0].Named);
        Assert.Contains("\"named\":true", json);
    }

    [Fact]
    public void TheNamedLanceTakesItsTrueNameAtAnyStage()
    {
        var weapon = Shipped.Weapon(LanceId);

        Assert.Equal("the First Warden's Lance", Ladder.Named);
        Assert.Equal("the First Warden's Lance", Heirloom.Shape(weapon, new ItemStack(LanceId, 40) { Named = true }).Name);
        Assert.Equal("the First Warden's Lance", Heirloom.Name(new ItemStack(LanceId, 40) { Stage = 2, Named = true }, Shipped));
        Assert.Equal("Family Lance", Heirloom.Name(new ItemStack(LanceId, 40) { Stage = 3 }, Shipped));
    }

    [Fact]
    public void TeodorsQuestTwoIsTheWardensGateAndNamesTheLance()
    {
        var quest = Shipped.Campaign.Quest("teodor_2")!;
        var map = MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), MapFiles.QuestsDirectory, "the_wardens_gate.map"), Shipped);

        Assert.Equal(("teodor", 2, "the_wardens_gate", LanceId, 3), (quest.MemberId, quest.Part, quest.MapId, quest.Names, quest.Rare));
        Assert.Null(quest.Pays);
        Assert.Null(CampaignRecord.QuestMapRefusal(map));
        Assert.Equal(WinCondition.DefeatBoss, map.Win);
        Assert.Equal(Shipped.Campaign.Quests[^1], quest);
    }

    [Fact]
    public void TheWardensGateIsCanonical()
    {
        var path = Path.Combine(Fixture.RealContentDirectory(), MapFiles.QuestsDirectory, "the_wardens_gate.map");
        var text = File.ReadAllText(path).ReplaceLineEndings("\n");

        Assert.Equal(text, MapFormat.Write(MapFormat.Parse(path, text, Shipped), Shipped));
    }

    [Fact]
    public void AWonQuestTwoNamesTheLanceAndPaysTheIron()
    {
        var record = CampaignRecord.StartAt(Shipped, 701, "sallow_grange");
        var board = MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), MapFiles.QuestsDirectory, "the_wardens_gate.map"), Shipped);
        var opening = record.BeginQuest(board, "teodor_2", "wren", Shipped);
        var won = opening with { Units = ValueList<BattleUnit>.From(opening.UnitsOf(Side.Player)), History = ValueList<BattleState>.Of(opening) };

        var after = record.AfterQuest(won, "teodor_2", Shipped);

        Assert.Equal("teodor wins teodor_2; Family Lance is the First Warden's Lance now; the stores take 3 frozen iron; nobody fell", after.Text);
        Assert.True(after.Record.Find("teodor")!.Inventory.Items.Single(s => s.ItemId == LanceId).Named);
        Assert.Equal(record.RareMaterial + 3, after.Record.RareMaterial);
    }

    [Fact]
    public void TheSerializerKeepsTheNameTheNamingAndTheArtsFlags()
    {
        var back = ContentLoader.Parse(ContentSerializer.Write(Shipped));

        Assert.Equal(Ladder.Named, back.Weapon(LanceId).Heirloom!.Named);
        Assert.Equal(LanceId, back.Campaign.Quest("teodor_2")!.Names);
        Assert.Equal(Shipped.Ability("turn_the_key").Effect, back.Ability("turn_the_key").Effect);
    }

    [Theory]
    [InlineData(1, LanceId, "only a quest 2 names an heirloom")]
    [InlineData(2, "iron_lance", "must be an heirloom with a true name (heirloom.named) bound to 'teodor'")]
    public void ANamingOnAQuestOneOrOnAnUnnamedWeaponIsRefused(int part, string item, string problem)
    {
        var quests = part == 1
            ? ValueList<CampaignQuest>.Of(new CampaignQuest("teodor_1", "teodor", 1, "m") { Names = item })
            : ValueList<CampaignQuest>.Of(new CampaignQuest("teodor_1", "teodor", 1, "m"), new CampaignQuest("teodor_2", "teodor", 2, "n") { Names = item, Rare = 3 });
        var content = Shipped with { Campaign = Shipped.Campaign with { Quests = quests } };

        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(ContentSerializer.Write(content)));

        Assert.Equal("names", e.Field);
        Assert.Contains(problem, e.Message);
    }

    [Fact]
    public void AQuestTwoThatBothPaysAndNamesIsRefused()
    {
        var quests = ValueList<CampaignQuest>.Of(new CampaignQuest("teodor_1", "teodor", 1, "m"), new CampaignQuest("teodor_2", "teodor", 2, "n") { Names = LanceId, Pays = LanceId, Rare = 3 });
        var content = Shipped with { Campaign = Shipped.Campaign with { Quests = quests } };

        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(ContentSerializer.Write(content)));

        Assert.Equal("names", e.Field);
        Assert.Contains("not both", e.Message);
    }

    [Fact]
    public void AWokenOnlyArtOnAWeaponWithNoLadderIsRefused()
    {
        var art = (CombatArtEffect)Shipped.Ability("turn_the_key").Effect;
        var abilities = Shipped.Abilities.SetItem("turn_the_key", Shipped.Ability("turn_the_key") with { Effect = art with { Item = "iron_lance" } });
        var content = Shipped with { Abilities = abilities };

        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(ContentSerializer.Write(content)));

        Assert.Equal(("turn_the_key", "effect.woken"), (e.Entry, e.Field));
    }
}
