using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Issue 1461, forms slice a2: on a <c>forms: on</c> map every enemy knows the basic form of each
/// weapon type it carries and declares it under the Sim player's rule, when it kills on its hit and the
/// plain strike does not and its Grit pays; <c>threat</c> prices the form it would declare.
/// </summary>
public class EnemyFormTests
{
    private static GameContent Forms => Starter;

    private static BattleState Begin(bool forms = true) =>
        BattleState.From(forms ? YardMap with { FormsEnabled = true } : YardMap, Forms, ValueList<Unit>.Of(Hale, Wren), 7).WithoutUnit("wren");

    /// <summary>Hale beside the brigand in the enemy phase, at one HP more than the brigand's plain hit, the brigand holding <paramref name="grit"/>.</summary>
    private static BattleState Exposed(int grit, bool forms = true, int spare = 1)
    {
        var state = Resolver.Apply(Begin(forms), Forms, new Move("hale", new Coord(2, 1))).Next;
        state = Resolver.Apply(state, Forms, new EndPhase()).Next;
        var brigand = state.Find("brigand-1")!;
        var plain = Queries.Forecast(state, Forms, brigand, state.Find("hale")!)!;
        state = state.WithUnit(state.Find("hale")! with { Hp = plain.Attacker.Damage + spare });
        return state.WithUnit(brigand with { Grit = grit });
    }

    private static string BasicOfBrigand(BattleState state)
    {
        var type = Forms.Weapon(state.Find("brigand-1")!.Unit.Inventory.Items[0].ItemId).Type;
        return Forms.Abilities.Values.Single(a => a.Effect is CombatArtEffect { Basic: true } art && art.Weapon == type).Id;
    }

    [Fact]
    public void OnAFormsMapEveryUnitOfEitherSideKnowsTheBasicFormOfItsWeaponType()
    {
        var state = Begin();
        var brigand = state.Find("brigand-1")!;
        var hale = state.Find("hale")!;
        var haleBasics = hale.Unit.Inventory.Items.Where(i => Forms.Weapons.ContainsKey(i.ItemId)).Select(i => Forms.Weapons[i.ItemId].Type).Distinct()
            .SelectMany(type => Forms.Abilities.Values.Where(a => a.Effect is CombatArtEffect { Basic: true } art && art.Weapon == type).Select(a => a.Id))
            .Except(Forms.ArtsOf(hale.Unit).Select(a => a.Ability.Id));

        Assert.Equal(new[] { BasicOfBrigand(state) }, Forms.FormsOf(brigand, forms: true).Select(f => f.Ability.Id));
        Assert.Empty(Forms.FormsOf(brigand, forms: false));
        Assert.NotEmpty(haleBasics);
        Assert.Equal(Forms.ArtsOf(hale.Unit).Select(a => a.Ability.Id).Concat(haleBasics), Forms.FormsOf(hale, forms: true).Select(f => f.Ability.Id));
        Assert.Equal(Forms.ArtsOf(hale.Unit).Select(a => a.Ability.Id), Forms.FormsOf(hale, forms: false).Select(f => f.Ability.Id));
    }

    [Fact]
    public void AnEnemyDeclaresItsBasicFormWhenItKillsOnItsHitAndThePlainStrikeDoesNot()
    {
        var state = Exposed(grit: 2);

        var plan = EnemyAi.PlanUnit(state, Forms, state.Find("brigand-1")!);

        var attack = Assert.IsType<Attack>(plan[^1]);
        Assert.Equal("hale", attack.TargetId);
        Assert.Equal(BasicOfBrigand(state), attack.Art);
        var result = Resolver.Apply(state, Forms, attack);
        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Equal(2, result.Events.OfType<FormDeclared>().Single().Grit);
    }

    [Fact]
    public void AnEnemyBelowTheFormsGritStrikesPlainAndTheOfferIsCountedUnaffordable()
    {
        var state = Exposed(grit: 1);
        var brigand = state.Find("brigand-1")!;

        var offers = EnemyAi.LethalForms(state, Forms, brigand, brigand.At, state.Find("hale")!, 0);

        Assert.Equal(new[] { new FormOffer(BasicOfBrigand(state), 2, false, offers.Single().Hit) }, offers);
        Assert.Null(EnemyAi.Form(state, Forms, brigand, brigand.At, state.Find("hale")!, 0));
        Assert.All(EnemyAi.PlanUnit(state, Forms, brigand).OfType<Attack>(), a => Assert.Null(a.Art));
    }

    [Fact]
    public void AnEnemyNeverDeclaresAFormWhereThePlainStrikeAlreadyKills()
    {
        var state = Exposed(grit: 3, spare: 0);
        var brigand = state.Find("brigand-1")!;

        Assert.Empty(EnemyAi.LethalForms(state, Forms, brigand, brigand.At, state.Find("hale")!, 0));
    }

    [Fact]
    public void OffTheFormsHeaderAnEnemyDeclaresNoForm()
    {
        var state = Exposed(grit: 3, forms: false);
        var brigand = state.Find("brigand-1")!;

        Assert.Empty(EnemyAi.LethalForms(state, Forms, brigand, brigand.At, state.Find("hale")!, 0));
        Assert.All(EnemyAi.PlanUnit(state, Forms, brigand).OfType<Attack>(), a => Assert.Null(a.Art));
    }

    [Fact]
    public void ThreatPricesTheFormAnEnemyWouldDeclareWithTheGritItWillHold()
    {
        var state = Begin();
        state = Resolver.Apply(state, Forms, new EndPhase()).Next;
        state = Resolver.Apply(state, Forms, new EndPhase()).Next;
        var brigand = state.Find("brigand-1")!;
        Assert.Equal(1, brigand.Grit);
        var hale = state.Find("hale")!;
        var tile = new Coord(brigand.At.X - 1, brigand.At.Y);
        var plainLine = Queries.Threats(state, Forms, hale, tile)!.Single(l => l.Enemy.Id == "brigand-1");
        Assert.Null(plainLine.Form);

        var weak = state.WithUnit(hale with { Hp = plainLine.Forecast.Attacker.Damage + 1 });
        var line = Queries.Threats(weak, Forms, weak.Find("hale")!, tile)!.Single(l => l.Enemy.Id == "brigand-1");

        Assert.Equal(BasicOfBrigand(state), line.Form);
        Assert.Equal(2, line.Enemy.Grit);
        Assert.True(line.Forecast.Attacker.Damage >= plainLine.Forecast.Attacker.Damage + 1);
    }

    [Fact]
    public void ThreatSaysWhichFormTheEnemyDeclaresAndItsGrit()
    {
        var state = Begin();
        state = Resolver.Apply(state, Forms, new EndPhase()).Next;
        state = Resolver.Apply(state, Forms, new EndPhase()).Next;
        var brigand = state.Find("brigand-1")!;
        var tile = new Coord(brigand.At.X - 1, brigand.At.Y);
        var hale = state.Find("hale")!;
        var plain = Queries.Threats(state, Forms, hale, tile)!.Single(l => l.Enemy.Id == "brigand-1");
        state = state.WithUnit(hale with { Hp = plain.Forecast.Attacker.Damage + 1 });
        hale = state.Find("hale")!;

        var text = Ironwake.Cli.PlaySession.ThreatText(state, Forms, hale, tile, Queries.Threats(state, Forms, hale, tile)!, Array.Empty<SleepingThreat>());

        Assert.Contains($" (slot 1) as {Forms.Ability(BasicOfBrigand(state)).Name} (2 of its 2 Grit): ", text);
    }

    [Fact]
    public void ASecondBasicFormForOneWeaponTypeIsRefusedOnLoad()
    {
        var dir = Fixture.CopyRealContent();
        var path = Path.Combine(dir, "abilities.json");
        var text = File.ReadAllText(path);
        File.WriteAllText(path, text.Replace("\"cost\": 1, \"mt\": -2, \"hit\": 25, \"grit\": 2", "\"cost\": 1, \"mt\": -2, \"hit\": 25, \"grit\": 2, \"basic\": true"));

        var error = Assert.Throws<ContentException>(() => ContentLoader.Load(dir));

        Assert.Contains("heavy_cut", error.Message);
        Assert.Contains("effect.basic", error.Message);
        Assert.Contains("feint is already the basic form for sword", error.Message);
    }

    [Fact]
    public void TheShippedBasicFormsAreOneAWeaponType()
    {
        var shipped = ContentLoader.Load(Fixture.RealContentDirectory());

        var basics = shipped.Abilities.Values.Where(a => a.Effect is CombatArtEffect { Basic: true }).ToDictionary(a => ((CombatArtEffect)a.Effect).Weapon, a => a.Id);

        Assert.Equal(new Dictionary<WeaponType, string> { [WeaponType.Sword] = "heavy_cut", [WeaponType.Lance] = "long_thrust", [WeaponType.Axe] = "cleave", [WeaponType.Bow] = "aimed_shot", [WeaponType.Reason] = "overcast" }, basics);
    }
}
