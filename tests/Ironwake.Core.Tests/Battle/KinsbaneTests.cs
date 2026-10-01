using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The hungering weapon (DESIGN.md 13.23, experiment): the scythe drains its wielder at their phase
/// start when it fed on nothing since the last, never below 1, and starves when the drain reaches 1;
/// a kill feeds it, a hit eases a starved one; it grows 1 Mt a three kills to +5 and wakes at the cap;
/// its uses never fall below 1; once fed it binds its wielder.
/// </summary>
public class KinsbaneTests
{
    private const string Field = """
        name: Field
        size: 7x5
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1

        .......
        .......
        .......
        .......
        .......

        units:
        P captain 0,0
        P recruit:kez 2,2
        E soldier 3,2 group:near behavior:hold
        E brigand 6,4 group:far behavior:hold

        """;

    private static readonly Unit Kez = Recruit("kez", "reaver", new Stats(23, 9, 0, 4, 5, 3, 3, 0, 2), Kinsbane.ItemId, "iron_axe")
        with { Skill = WeaponSkill.Zero.With(WeaponType.Axe, WeaponRanks.Threshold(WeaponRank.D)) };

    private static BattleState Begin(ItemStack? scythe = null, int? hp = null, ulong seed = 7)
    {
        var kez = scythe is { } stack ? Kez with { Inventory = Kez.Inventory.Replace(0, stack) } : Kez;
        var state = Start(seed, ValueList<Unit>.Of(Hale, kez), Field);
        return hp is { } h ? state.WithUnit(state.Find("kez")! with { Hp = h }) : state;
    }

    private static ItemStack Scythe(int uses = 30) => new(Kinsbane.ItemId, uses);

    private static ItemStack StackOf(BattleState state) => state.Find("kez")!.Unit.Inventory.Items[Kinsbane.Slot(state.Find("kez")!.Unit, Starter)];

    /// <summary>Both phases ended from a player phase: the next player phase start, with its events.</summary>
    private static (BattleState State, List<GameEvent> Events) NextPlayerPhase(BattleState state)
    {
        var events = new List<GameEvent>();
        var enemy = state.Try(new EndPhase());
        events.AddRange(enemy.Events);
        var player = enemy.Next.Try(new EndPhase());
        events.AddRange(player.Events);
        return (player.Next, events);
    }

    /// <summary>Keziah's attack on the soldier beside her, on the first seed from 1 for which <paramref name="wanted"/> holds.</summary>
    private static ApplyResult StrikeUntil(Func<ApplyResult, bool> wanted, ItemStack scythe, int soldierHp, int? hp = null)
    {
        for (ulong seed = 1; seed < 300; seed++)
        {
            var state = Begin(scythe, hp, seed);
            state = state.WithUnit(state.Find("soldier-1")! with { Hp = soldierHp });
            var result = state.Try(new Attack("kez", "soldier-1"));
            Assert.True(result.Accepted, result.Rejection?.Message);
            if (wanted(result))
            {
                return result;
            }
        }

        throw new InvalidOperationException("no seed under 300 gives the wanted strike");
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 1)]
    [InlineData(8, 2)]
    [InlineData(14, 4)]
    [InlineData(15, 5)]
    [InlineData(40, 5)]
    public void EveryThreeKillsAddOneMtToACapOfFive(int fed, int bonus)
    {
        Assert.Equal(bonus, Kinsbane.MtBonus(fed));
        Assert.Equal(8 + bonus, Kinsbane.Form(Starter.Weapon(Kinsbane.ItemId), Scythe() with { Fed = fed }).Mt);
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(3, 4)]
    [InlineData(6, 5)]
    [InlineData(15, 6)]
    public void TheStarvedFormHalvesMtRoundedDown(int fed, int mt)
    {
        Assert.Equal(mt, Kinsbane.Form(Starter.Weapon(Kinsbane.ItemId), Scythe() with { Fed = fed, Starved = true }).Mt);
    }

    [Fact]
    public void AWeaponThatDoesNotHungerKeepsItsForm()
    {
        var axe = Starter.Weapon("iron_axe");
        Assert.Same(axe, Kinsbane.Form(axe, new ItemStack("iron_axe", 40) { Fed = 9, Starved = true }));
    }

    [Theory]
    [InlineData(25, 20, false)]
    [InlineData(7, 2, false)]
    [InlineData(6, 1, true)]
    [InlineData(3, 1, true)]
    public void AnUnfedScytheDrainsFiveAtItsWieldersPhaseStartNeverBelowOneAndStarvesAtOne(int hp, int after, bool starves)
    {
        var (state, events) = NextPlayerPhase(Begin(hp: hp));

        Assert.Equal(after, state.Find("kez")!.Hp);
        Assert.Equal(starves, StackOf(state).Starved);
        Assert.Contains(events, e => e is HungerDrained d && d.UnitId == "kez" && d.Amount == hp - after && d.HpAfter == after && d.Starved == starves);
        if (starves)
        {
            Assert.Equal(1, StackOf(state).Uses);
        }
    }

    [Fact]
    public void TheDrainComesOnlyAtTheWieldersOwnPhaseStart()
    {
        var enemy = Begin().Try(new EndPhase());

        Assert.Equal(25, enemy.Next.Find("kez")!.Hp);
        Assert.DoesNotContain(enemy.Events, e => e is HungerDrained);
    }

    [Fact]
    public void AScytheThatFedSinceTheLastPhaseStartSparesTheNextDrainAndIsHungryAgainAfter()
    {
        var (state, events) = NextPlayerPhase(Begin(Scythe() with { Ate = true, Fed = 1 }));

        Assert.Equal(25, state.Find("kez")!.Hp);
        Assert.DoesNotContain(events, e => e is HungerDrained);
        Assert.False(StackOf(state).Ate);
        Assert.Equal(5, Kinsbane.DrainComing(state.Find("kez")!, Starter));
    }

    [Fact]
    public void AStarvedWielderAtOneIsNotDrainedAgainAndNoEventIsPrinted()
    {
        var (state, events) = NextPlayerPhase(Begin(Scythe(1) with { Starved = true }, hp: 1));

        Assert.Equal(1, state.Find("kez")!.Hp);
        Assert.True(StackOf(state).Starved);
        Assert.DoesNotContain(events, e => e is HungerDrained);
    }

    [Fact]
    public void AKillFeedsTheScytheHealsTenToMaxRestoresItsUsesAndEndsTheStarvedForm()
    {
        var result = StrikeUntil(r => r.Next.Find("soldier-1") is null, Scythe(1) with { Fed = 2, Starved = true }, soldierHp: 1, hp: 5);
        var stack = StackOf(result.Next);

        Assert.Equal(15, result.Next.Find("kez")!.Hp);
        Assert.Equal(3, stack.Fed);
        Assert.True(stack.Ate);
        Assert.False(stack.Starved);
        Assert.Equal(30, stack.Uses);
        Assert.Contains(result.Events, e => e is HungerFed { UnitId: "kez", Fed: 3, MtBonus: 1, Healed: 10, HpAfter: 15, Awake: false });
    }

    [Fact]
    public void TheFeedHealStopsAtMaxHp()
    {
        var result = StrikeUntil(r => r.Next.Find("soldier-1") is null, Scythe(), soldierHp: 1, hp: 22);

        Assert.Equal(25, result.Next.Find("kez")!.Hp);
        Assert.Contains(result.Events, e => e is HungerFed { Healed: 3 });
    }

    [Fact]
    public void AStarvedScytheThatLandsAHitWithoutKillingReturnsToItsFormRestoresItsUsesAndHealsFive()
    {
        var result = StrikeUntil(
            r => r.Next.Find("soldier-1") is not null && r.Events.OfType<CombatFought>().Single().Strikes.Any(s => s.AttackerId == "kez" && s.Hit) && r.Next.Find("kez") is not null,
            Scythe(1) with { Starved = true }, soldierHp: 20, hp: 6);
        var stack = StackOf(result.Next);
        var hpAfterCombat = result.Events.OfType<CombatFought>().Single().AttackerHpAfter;

        Assert.False(stack.Starved);
        Assert.Equal(30, stack.Uses);
        Assert.Equal(0, stack.Fed);
        Assert.Equal(Math.Min(25, hpAfterCombat + 5), result.Next.Find("kez")!.Hp);
        Assert.Contains(result.Events, e => e is HungerEased { UnitId: "kez" });
    }

    [Fact]
    public void AStarvedScytheThatMissesStaysStarved()
    {
        var result = StrikeUntil(
            r => r.Next.Find("kez") is not null && !r.Events.OfType<CombatFought>().Single().Strikes.Any(s => s.AttackerId == "kez" && s.Hit),
            Scythe(1) with { Starved = true }, soldierHp: 20, hp: 20);

        Assert.True(StackOf(result.Next).Starved);
        Assert.DoesNotContain(result.Events, e => e is HungerEased or HungerFed);
    }

    [Fact]
    public void TheScythesUsesNeverFallBelowOneSoAStrikeAtOneSpendsNothing()
    {
        var result = StrikeUntil(r => r.Next.Find("kez") is not null && r.Next.Find("soldier-1") is not null, Scythe(1), soldierHp: 20);

        Assert.Equal(1, StackOf(result.Next).Uses);
        Assert.DoesNotContain(result.Events, e => e is WeaponBroke);
    }

    [Fact]
    public void AtTheCapTheScytheWakesNeitherDrainingStarvingNorHealingOnAFeed()
    {
        var (state, events) = NextPlayerPhase(Begin(Scythe() with { Fed = Kinsbane.AwakeAt }, hp: 3));
        Assert.Equal(3, state.Find("kez")!.Hp);
        Assert.False(StackOf(state).Starved);
        Assert.DoesNotContain(events, e => e is HungerDrained);

        var fed = StrikeUntil(r => r.Next.Find("soldier-1") is null, Scythe() with { Fed = Kinsbane.AwakeAt }, soldierHp: 1, hp: 10);
        Assert.Equal(10, fed.Next.Find("kez")!.Hp);
        Assert.Contains(fed.Events, e => e is HungerFed { Healed: 0, Awake: true, MtBonus: 5 });
    }

    [Fact]
    public void ItDrainsWhileCarriedUnequipped()
    {
        var carried = Kez with { Inventory = new Inventory(ValueList<ItemStack>.Of(new ItemStack("iron_axe", 40), Scythe())) };
        var (state, _) = NextPlayerPhase(Start(7, ValueList<Unit>.Of(Hale, carried), Field));

        Assert.Equal(20, state.Find("kez")!.Hp);
        Assert.Equal(0, state.Find("kez")!.EquippedSlot(Starter));
    }

    [Fact]
    public void OnceFedTheScytheIsTheEquippedWeaponAndNoOtherWeaponStrikes()
    {
        var bound = Kez with { Inventory = new Inventory(ValueList<ItemStack>.Of(new ItemStack("iron_axe", 40), Scythe() with { Fed = 1 })) };
        var state = Start(7, ValueList<Unit>.Of(Hale, bound), Field);
        var kez = state.Find("kez")!;

        Assert.Equal(1, kez.EquippedSlot(Starter));
        Assert.Null(kez.UsableWeaponAt(Starter, 0));
        var refused = state.Refused(new Attack("kez", "soldier-1", Slot: 0));
        Assert.Equal(RejectionReason.NotUsable, refused.Reason);
        Assert.Contains("Kinsbane has fed and will not be put down", refused.Message);
    }

    [Fact]
    public void AnUnfedScytheBindsNothing()
    {
        var state = Begin();

        Assert.NotNull(state.Find("kez")!.UsableWeaponAt(Starter, 1));
        Assert.True(state.Try(new Attack("kez", "soldier-1", Slot: 1)).Accepted);
    }

    [Fact]
    public void TheForecastNamesWhatAKillPaysAndTheCardNamesTheDrainComing()
    {
        var state = Begin(Scythe() with { Fed = 2 }, hp: 17);
        var kez = state.Find("kez")!;

        Assert.Contains("  kill: +10 HP to max, fed 3, Mt +1", PlaySession.HungerLines(Starter, kez, state.Find("soldier-1")!, null, counters: true));
        Assert.Equal("  Kinsbane: fed 2, Mt +0, hungry: -5 HP at the next phase start unless it kills; bound, no other weapon strikes", PlaySession.HungerLine(kez, Starter));
        var unfed = Begin().Find("kez")!;
        Assert.Empty(PlaySession.HungerLines(Starter, unfed, state.Find("soldier-1")!, 1, counters: true));
    }

    [Fact]
    public void RecallRestoresTheScythesState()
    {
        var before = Begin(hp: 20);
        var (after, _) = NextPlayerPhase(before);
        Assert.Equal(15, after.Find("kez")!.Hp);

        var index = Enumerable.Range(0, after.History.Count).Last(i => after.History[i].Phase == Side.Player);
        var recalled = after.Do(new Recall(index));
        Assert.Equal(20, recalled.Find("kez")!.Hp);
        Assert.Equal(StackOf(after.History[index]), StackOf(recalled));
    }

    [Fact]
    public void TheKinsbaneHeaderArmsItsCarrierAndRoundTrips()
    {
        var text = Field.Replace("enemy_level: 1\n", "enemy_level: 1\nkinsbane: kez\n");
        var map = MapFixture.Parse(text);
        Assert.Equal("kez", map.KinsbaneCarrier);
        Assert.Equal(map, MapFixture.Parse(MapFormat.Write(map, Starter)));

        var plain = Recruit("kez", "reaver", new Stats(23, 9, 0, 4, 5, 3, 3, 0, 2), "iron_axe");
        var armed = map.Armed(plain, Starter);
        Assert.Equal(Kinsbane.ItemId, armed.Inventory.Items[0].ItemId);
        Assert.Equal(WeaponRank.D, armed.Skill.Rank(WeaponType.Axe));
        Assert.Same(Hale, map.Armed(Hale, Starter));
    }

    [Fact]
    public void TheKinsbaneHeaderIsRefusedWithoutAPlacementForItsCarrier()
    {
        var text = Field.Replace("enemy_level: 1\n", "enemy_level: 1\nkinsbane: wren\n");
        var error = Assert.Throws<MapException>(() => MapFixture.Parse(text));
        Assert.Contains("kinsbane names 'wren'", error.Message);
    }

    [Fact]
    public void TheProtocolCarriesTheScythesState()
    {
        var state = Begin(Scythe(1) with { Fed = 4, Starved = true, Ate = true });

        Assert.Equal(state, ProtocolJson.ReadState(ProtocolJson.State(state, Starter), Starter));
    }
}
