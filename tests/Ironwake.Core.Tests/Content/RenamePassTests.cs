using Ironwake.Cli;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Issue 701, the rename pass: a distinctive coinage taken from another series goes and an
/// ordinary English word stays, and every number is the same. Issue 1446 took the ids too: the
/// Senses, Steady Aim and Move Again have ids of their own words, and the command is <c>again</c>.
/// </summary>
[Collection("console")]
public class RenamePassTests
{
    private static GameContent Content => MapFixture.Content;

    [Theory]
    [InlineData("sword_sense", "Sword Sense")]
    [InlineData("lance_sense", "Lance Sense")]
    [InlineData("axe_sense", "Axe Sense")]
    [InlineData("bow_sense", "Bow Sense")]
    [InlineData("fist_sense", "Fist Sense")]
    [InlineData("faith_sense", "Faith Sense")]
    [InlineData("lore_sense", "Lore Sense")]
    [InlineData("move_again", "Move Again")]
    [InlineData("steady_aim", "Steady Aim")]
    public void EachRenamedAbilityShowsItsPlainName(string id, string name)
    {
        Assert.Equal(name, Content.Ability(id).Name);
    }

    [Theory]
    [InlineData(WeaponType.Sword, "sword")]
    [InlineData(WeaponType.Lance, "lance")]
    [InlineData(WeaponType.Axe, "axe")]
    [InlineData(WeaponType.Bow, "bow")]
    [InlineData(WeaponType.Reason, "lore")]
    [InlineData(WeaponType.Faith, "faith")]
    [InlineData(WeaponType.Gauntlet, "gauntlet")]
    public void EachWeaponTypeShowsItsLabel(WeaponType type, string label)
    {
        Assert.Equal(label, type.Label());
    }

    [Fact]
    public void TheWeaponLineAndTheCardPrintAccFirstThenPower()
    {
        Assert.StartsWith("Iron Bow, bow E. Acc 70, Power 5, Crit 0, ", ItemCard.Text(Content, "iron_bow"), StringComparison.Ordinal);
        Assert.StartsWith("Cinder, lore E, fire school. Acc ", ItemCard.Text(Content, "cinder"), StringComparison.Ordinal);
    }

    private const string Yard = """
        name: Rename Yard
        size: 6x3
        win: rout
        turn_limit: 5
        recall: 0
        enemy_level: 1

        ......
        ......
        ......

        units:
        P captain 0,1
        P recruit:ansgar 0,0
        P recruit:pell 0,2
        E brigand 2,1 group:yard behavior:hold

        """;

    /// <summary>
    /// The guard: the help, every unit card, a forecast, a threat and the item cards carry none
    /// of the old labels, and the forecast puts the resolved Acc before the damage on both sides.
    /// </summary>
    [Fact]
    public void NoPlayerFacingLineCarriesAnOldLabel()
    {
        var output = RunInline(Yard, "help\nshow captain\nshow ansgar\nshow pell\nshow brigand-1\nforecast captain brigand-1 from 1,1\nforecast pell brigand-1 from 0,1\nthreat pell\nabout cinder\nabout iron_bow\nabout salve\n");

        // Command words are ids and stay, so the echoed commands are not read.
        var shown = string.Join("\n", output.Split('\n').Where(line => !line.StartsWith("> ", StringComparison.Ordinal)));
        foreach (var old in new[] { "breaker", "Canto", "canto", "Deadeye", "Reason", " Mt ", " Hit ", " mt ", "Avoid", "avoid", "Arts:", " Avo " })
        {
            Assert.DoesNotContain(old, shown);
        }

        Assert.Contains("Forecast Alder Fenn -> Brigand from 1,1 (Plain): acc ", output);
        Assert.Contains("; counter: acc ", output);
        Assert.Contains("  Abilities: Move Again (", output);
        Assert.Contains("  Ranks: lore E (0)", output);
    }

    private static string RunInline(string mapText, string scriptText)
    {
        var map = Path.Combine(Path.GetTempPath(), "ironwake-rename-" + Guid.NewGuid().ToString("N") + ".map");
        var script = Path.ChangeExtension(map, ".script");
        File.WriteAllText(map, mapText);
        File.WriteAllText(script, scriptText);
        try
        {
            return ConsoleCapture.Run(() => { Program.Main(new[] { "play", map, "--seed", "1", "--script", script, "--content", Fixture.RealContentDirectory() }); });
        }
        finally
        {
            File.Delete(map);
            File.Delete(script);
        }
    }
}
