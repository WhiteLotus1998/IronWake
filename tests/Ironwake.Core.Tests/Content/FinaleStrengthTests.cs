using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// The keep finale's two strongest units (issue 692, slice 4; DECISIONS/0150), held on the shipped
/// content against every body that can stand on the keep: each cast member raised to
/// <see cref="ExpectedFinaleLevel"/> by average growth in its own class, a cadet in every class it
/// could certify into, carrying every
/// stocked weapon that class wields, and each barracks hire at <see cref="ExpectedFinaleLevel"/> less
/// the barracks' levels below. Strong: more HP and a larger stat total than any of them. Beatable: each cast member has a
/// stocked weapon that hurts them. Fair: held
/// by the pair rule (<see cref="PairRule"/>: one strike, no double, no crit), one combat never kills
/// one at full HP on open ground, so a unit standing in a pair survives it; alone, the captain can
/// die to it on the double or a crit.
/// </summary>
public class FinaleStrengthTests
{
    /// <summary>The level the company is expected to stand at on the keep (provisional; the measured slice reports the real one).</summary>
    public const int ExpectedFinaleLevel = 8;

    private static readonly GameContent Content = MapFixture.Content;

    /// <summary>The class every cast cadet may certify out of into any other, so a cadet is read in each.</summary>
    private const string Cadet = "cadet";

    public static readonly TheoryData<string> Finale = new() { "finale_lord", "finale_hunter" };

    private static IEnumerable<Unit> Company()
    {
        var classes = Content.Classes.Values.Where(c => !c.Hidden).ToList();
        foreach (var member in Content.Cast)
        {
            var raised = member.AtLevel(ExpectedFinaleLevel, Content.Class(member.ClassId));
            if (member.ClassId != Cadet)
            {
                yield return raised;
                continue;
            }

            foreach (var unitClass in classes)
            {
                yield return raised with { ClassId = unitClass.Id };
            }
        }

        foreach (var hire in Content.Campaign.Keep.Hires)
        {
            yield return Barracks.Recruit(hire, ExpectedFinaleLevel - Barracks.LevelsBelow, Content);
        }
    }

    private static IEnumerable<Weapon> Arms(Unit unit)
    {
        var unitClass = Content.Class(unit.ClassId);
        return Content.Weapons.Values.Where(w => w.Price is not null && !w.Heals && unitClass.CanUse(w.Type)).OrderBy(w => w.Id, StringComparer.Ordinal);
    }

    private static Combatant Fighting(Unit unit, Weapon weapon)
    {
        var plain = Content.TerrainById("plain");
        var hp = unit.EffectiveStats(Content.Class(unit.ClassId)).Hp;
        return Content.CombatantOf(unit, weapon, plain, hp);
    }

    [Theory]
    [MemberData(nameof(Finale))]
    public void AFinaleUnitHasMoreHpAndALargerStatTotalThanAnyoneInTheCompany(string templateId)
    {
        var boss = Content.Unit(templateId);
        var stats = boss.EffectiveStats(Content.Class(boss.ClassId));

        foreach (var unit in Company())
        {
            var theirs = unit.EffectiveStats(Content.Class(unit.ClassId));
            Assert.True(stats.Hp > theirs.Hp, $"{templateId} has {stats.Hp} hp, {unit.Id} as a {unit.ClassId} at level {unit.Level} has {theirs.Hp}");
            Assert.True(Total(stats) > Total(theirs), $"{templateId} totals {Total(stats)}, {unit.Id} as a {unit.ClassId} at level {unit.Level} totals {Total(theirs)}");
        }
    }

    [Theory]
    [MemberData(nameof(Finale))]
    public void AFinaleUnitHeldByThePairRuleNeverKillsAFullHpUnitOnOpenGround(string templateId)
    {
        var boss = Content.Unit(templateId);
        var failures = new List<string>();
        foreach (var weapon in boss.Inventory.Items.Select(i => Content.Weapon(i.ItemId)))
        {
            var held = Fighting(boss, weapon) with { PairHeld = true };
            foreach (var unit in Company())
            {
                foreach (var arm in Arms(unit))
                {
                    var target = Fighting(unit, arm);
                    var forecast = Core.Combat.Forecast(held, target, weapon.MinRange, RollScheme.TwoRollAverage);
                    var dealt = forecast.Attacker.Damage * forecast.Attacker.StrikesPerRound * (forecast.Attacker.Doubles ? 2 : 1);
                    if (dealt >= target.Hp || forecast.Attacker.CritChance > 0)
                    {
                        failures.Add($"{weapon.Id} deals {dealt} at crit {forecast.Attacker.CritChance} to {unit.Id} as a {unit.ClassId} with {arm.Id} ({target.Hp} hp)");
                    }
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Theory]
    [MemberData(nameof(Finale))]
    public void AFinaleUnitCanKillTheCaptainAloneOnTheDoubleOrACrit(string templateId)
    {
        var boss = Content.Unit(templateId);
        var striker = Fighting(boss, Content.Weapon(boss.Inventory.Items[0].ItemId));
        var captain = Content.Cast[0].AtLevel(ExpectedFinaleLevel, Content.Class(Content.Cast[0].ClassId));
        var target = Fighting(captain, Content.Weapon("iron_sword"));
        var forecast = Core.Combat.Forecast(striker, target, striker.Weapon!.MinRange, RollScheme.TwoRollAverage);
        var rounds = forecast.Attacker.Doubles ? 2 : 1;

        Assert.True(forecast.Attacker.CritChance > 0, $"{templateId} has no crit on the captain, so the pair rule holds nothing back");
        Assert.True(forecast.Attacker.Damage * (rounds + Core.Combat.CritMultiplier - 1) >= target.Hp, $"{templateId} alone deals at most {forecast.Attacker.Damage * (rounds + Core.Combat.CritMultiplier - 1)} to the captain's {target.Hp} hp");
    }

    /// <summary>The least a cast member's best stocked weapon must deal a finale unit per strike, so the plan, not one caster, beats him.</summary>
    public const int LeastHurt = 3;

    [Theory]
    [MemberData(nameof(Finale))]
    public void EveryCastMemberHasAStockedWeaponThatHurtsAFinaleUnit(string templateId)
    {
        var boss = Content.Unit(templateId);
        var defender = Fighting(boss, Content.Weapon(boss.Inventory.Items[0].ItemId));
        var failures = new List<string>();
        foreach (var member in Content.Cast)
        {
            var raised = member.AtLevel(ExpectedFinaleLevel, Content.Class(member.ClassId));
            var best = Arms(raised).Max(arm => Core.Combat.Damage(Fighting(raised, arm), defender));
            if (best < LeastHurt)
            {
                failures.Add($"{member.Id} as a {member.ClassId} deals at most {best} to {templateId}");
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    private static int Total(Stats stats) => stats.Hp + stats.Str + stats.Mag + stats.Dex + stats.Spd + stats.Lck + stats.Def + stats.Res + stats.Cha;
}
