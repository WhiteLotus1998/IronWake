using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Supports in battle (issue 77, slice 3): a unit beside a support partner whose rapport reaches a
/// tier fights with that tier's hit, avoid and crit through its aura, the best partner's tier and
/// never the sum; rapport accrues on every main campaign map as well as behind the header, a pair
/// with the captain at the higher of the two rates, never their sum.
/// </summary>
public class SupportBonusTests
{
    /// <summary>
    /// A 6x4 yard without the rivalry header. Hale (the captain) at 0,0, Wren at 0,1 and Ivo at 0,2:
    /// Wren stands beside both. The brigand at 3,1 is awake and within reach of the column.
    /// </summary>
    private const string Yard = """
        name: Yard
        size: 6x4
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1

        ......
        ......
        ......
        ......

        units:
        P captain 0,0
        P recruit:wren 0,1
        P recruit:ivo 0,2
        E brigand 3,1 group:yard behavior:aggressive

        """;

    private static readonly SupportTier C = new("C", 16, 5, 0, 0);
    private static readonly SupportTier B = new("B", 40, 5, 5, 0);
    private static readonly SupportTier A = new("A", 72, 10, 5, 5);

    /// <summary>The starter content with the tiers above and two support pairs: Hale and Wren, Wren and Ivo. Hale and Ivo are no pair.</summary>
    private static GameContent Content { get; } = Starter with
    {
        Rivalry = Starter.Rivalry with { SupportTiers = ValueList<SupportTier>.Of(C, B, A) },
        Campaign = Starter.Campaign with
        {
            Supports = ValueList<SupportPair>.Of(new SupportPair("hale", "wren", SupportKind.Mentor), new SupportPair("ivo", "wren", SupportKind.Comedy)),
        },
    };

    private static ValueList<Unit> Cohort => ValueList<Unit>.Of(Hale with { Region = "crown" }, Wren with { Region = "aldmere" }, Ivo with { Region = "sallow" });

    private static BattleState Board(params Rapport[] rapport) =>
        BattleState.From(Maps.MapFixture.Parse(Yard, "yard.map"), Content, Cohort, 7) with { Rapport = ValueList<Rapport>.Of(rapport) };

    private static CombatBonus BonusOf(BattleState state, string id) => Supports.Bonus(state, Content, state.Find(id)!);

    [Fact]
    public void A_support_partner_beside_a_unit_gives_the_tier_its_rapport_reaches()
    {
        Assert.Equal(new CombatBonus(5, 0, 0, 0), BonusOf(Board(new Rapport("ivo", "wren", 16)), "ivo"));
        Assert.Equal(new CombatBonus(5, 5, 0, 0), BonusOf(Board(new Rapport("ivo", "wren", 40)), "ivo"));
        Assert.Equal(new CombatBonus(10, 5, 5, 0), BonusOf(Board(new Rapport("ivo", "wren", 72)), "ivo"));
    }

    [Fact]
    public void A_pair_below_C_gives_nothing()
    {
        Assert.Equal(CombatBonus.None, BonusOf(Board(new Rapport("ivo", "wren", 15)), "ivo"));
    }

    [Fact]
    public void Two_partners_beside_a_unit_give_the_best_tier_not_the_sum()
    {
        var state = Board(new Rapport("hale", "wren", 72), new Rapport("ivo", "wren", 16));

        Assert.Equal(new CombatBonus(10, 5, 5, 0), BonusOf(state, "wren"));
    }

    [Fact]
    public void A_partner_out_of_reach_of_one_tile_gives_nothing()
    {
        var state = Board(new Rapport("ivo", "wren", 72));
        state = state.WithUnit(state.Find("ivo")! with { At = new Coord(0, 3) });

        Assert.Equal(CombatBonus.None, BonusOf(state, "ivo"));
    }

    [Fact]
    public void Rapport_between_two_who_are_no_support_pair_gives_nothing()
    {
        var state = Board(new Rapport("hale", "ivo", 500));
        state = state.WithUnit(state.Find("ivo")! with { At = new Coord(1, 0) });

        Assert.Equal(CombatBonus.None, BonusOf(state, "ivo"));
    }

    [Fact]
    public void The_support_bonus_reaches_the_forecast_through_the_aura()
    {
        var bare = Board();
        var supported = Board(new Rapport("ivo", "wren", 72));
        var brigand = bare.Find("brigand-1")!;

        var without = Core.Combat.Forecast(bare.Find("ivo")!.ToCombatant(bare, Content, against: brigand), brigand.Answering(bare, Content, new Coord(0, 2)), 1, bare.Scheme);
        var with = Core.Combat.Forecast(supported.Find("ivo")!.ToCombatant(supported, Content, against: brigand), brigand.Answering(supported, Content, new Coord(0, 2)), 1, supported.Scheme);

        Assert.Equal(new CombatBonus(10, 5, 5, 0), supported.Find("ivo")!.ToCombatant(supported, Content).Aura);
        Assert.Equal(Math.Min(100, without.Attacker.HitChance + 10), with.Attacker.HitChance);
    }

    [Fact]
    public void Off_the_header_and_off_the_campaign_nothing_accrues()
    {
        var result = Resolver.Apply(Board(), Content, new EndPhase());

        Assert.DoesNotContain(result.Events, e => e is RapportGained);
    }

    [Fact]
    public void On_a_campaign_map_a_support_pair_accrues_off_the_header()
    {
        var state = Board() with { CampaignMap = 1 };
        var amount = Rivalry.RateOf(state.Find("ivo")!, Content) + Rivalry.RateOf(state.Find("wren")!, Content);

        var result = Resolver.Apply(state, Content, new EndPhase());

        Assert.Contains(new RapportGained("ivo", "wren", amount, amount), result.Events);
        Assert.DoesNotContain(result.Events, e => e is RivalryEnded);
    }

    [Fact]
    public void A_pair_with_the_captain_accrues_at_the_captains_rate_when_it_is_the_higher()
    {
        var state = Board() with { CampaignMap = 1 };
        var captain = Rivalry.RateOf(state.Find("hale")!, Content);
        Assert.True(captain > Rivalry.RateOf(state.Find("wren")!, Content));

        var result = Resolver.Apply(state, Content, new EndPhase());

        Assert.Contains(new RapportGained("hale", "wren", captain, captain), result.Events);
    }

    [Fact]
    public void A_pair_with_the_captain_accrues_at_the_recruits_rate_when_it_is_the_higher()
    {
        var cohort = ValueList<Unit>.Of(
            Hale with { Region = "crown", Stats = Hale.Stats with { Cha = 0 } },
            Wren with { Region = "aldmere", Stats = Wren.Stats with { Cha = 9 } },
            Ivo with { Region = "sallow" });
        var state = BattleState.From(Maps.MapFixture.Parse(Yard, "yard.map"), Content, cohort, 7) with { CampaignMap = 1 };
        var recruit = Rivalry.RateOf(state.Find("wren")!, Content);
        Assert.True(recruit > Rivalry.RateOf(state.Find("hale")!, Content));

        var result = Resolver.Apply(state, Content, new EndPhase());

        Assert.Contains(new RapportGained("hale", "wren", recruit, recruit), result.Events);
    }

    [Fact]
    public void Off_the_header_two_recruits_who_are_no_support_pair_accrue_nothing()
    {
        var content = Content with { Campaign = Content.Campaign with { Supports = ValueList<SupportPair>.Of(new SupportPair("hale", "wren", SupportKind.Mentor)) } };
        var state = BattleState.From(Maps.MapFixture.Parse(Yard, "yard.map"), content, Cohort, 7) with { CampaignMap = 1 };

        var result = Resolver.Apply(state, content, new EndPhase());

        Assert.DoesNotContain(result.Events, e => e is RapportGained { A: "ivo", B: "wren" });
    }

    [Fact]
    public void A_captain_who_is_no_partner_accrues_nothing()
    {
        var content = Content with { Campaign = Content.Campaign with { Supports = ValueList<SupportPair>.Of(new SupportPair("ivo", "wren", SupportKind.Comedy)) } };
        var state = BattleState.From(Maps.MapFixture.Parse(Yard, "yard.map"), content, Cohort, 7) with { CampaignMap = 1 };

        var result = Resolver.Apply(state, content, new EndPhase());

        Assert.DoesNotContain(result.Events, e => e is RapportGained { A: "hale" } or RapportGained { B: "hale" });
    }

    [Fact]
    public void A_support_pair_crossing_a_tier_announces_the_tier_it_reaches()
    {
        var state = Board(new Rapport("ivo", "wren", C.At - 1)) with { CampaignMap = 1 };

        var result = Resolver.Apply(state, Content, new EndPhase());

        Assert.Contains(new SupportReached("ivo", "wren", "C"), result.Events);
    }

    [Fact]
    public void A_support_pair_already_at_its_tier_announces_nothing()
    {
        var state = Board(new Rapport("ivo", "wren", C.At)) with { CampaignMap = 1 };

        var result = Resolver.Apply(state, Content, new EndPhase());

        Assert.Contains(result.Events, e => e is RapportGained { A: "ivo", B: "wren" });
        Assert.DoesNotContain(result.Events, e => e is SupportReached { A: "ivo", B: "wren" });
    }

    [Fact]
    public void A_support_pair_crossing_two_tiers_in_one_phase_announces_only_the_higher()
    {
        var content = Content with { Rivalry = Content.Rivalry with { SupportTiers = ValueList<SupportTier>.Of(C, B with { At = C.At + 1 }, A) } };
        var state = BattleState.From(Maps.MapFixture.Parse(Yard, "yard.map"), content, Cohort, 7) with { CampaignMap = 1, Rapport = ValueList<Rapport>.Of(new Rapport("ivo", "wren", C.At - 1)) };
        Assert.True(Rivalry.RateOf(state.Find("ivo")!, content) + Rivalry.RateOf(state.Find("wren")!, content) >= 2);

        var result = Resolver.Apply(state, content, new EndPhase());

        Assert.Equal(new[] { new SupportReached("ivo", "wren", "B") }, result.Events.OfType<SupportReached>().Where(e => e.A == "ivo").ToArray());
    }

    [Fact]
    public void Rapport_between_two_who_are_no_support_pair_announces_no_tier()
    {
        var content = Content with { Campaign = Content.Campaign with { Supports = ValueList<SupportPair>.Of(new SupportPair("hale", "wren", SupportKind.Mentor)) } };
        var state = BattleState.From(Maps.MapFixture.Parse(Yard.Replace("enemy_level: 1", "enemy_level: 1\nrivalry: symmetric"), "yard.map"), content, Cohort, 7) with { Rapport = ValueList<Rapport>.Of(new Rapport("ivo", "wren", C.At - 1)) };

        var result = Resolver.Apply(state, content, new EndPhase());

        Assert.Contains(result.Events, e => e is RapportGained { A: "ivo", B: "wren" });
        Assert.DoesNotContain(result.Events, e => e is SupportReached { A: "ivo", B: "wren" });
    }
}
