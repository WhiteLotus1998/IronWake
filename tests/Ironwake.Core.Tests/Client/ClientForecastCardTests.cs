using System.Text.RegularExpressions;
using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Client;

/// <summary>
/// Showcase slice 2 (issue 512): the drawn forecast's data and the unit card. Every number the
/// card draws is the number the console's forecast line prints for the same hover, since both
/// read one <see cref="CombatForecast"/>; the card adds only the source of avoid and the HP after.
/// </summary>
public class ClientForecastCardTests
{
    private static ClientSession TurnThree()
    {
        var content = ContentLoader.Load(Fixture.RealContentDirectory());
        var file = MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "maps", "the_tollgate.map"), content);
        var client = new ClientSession(content, BattleState.From(file, content, content.Cast, 113));
        var script = File.ReadAllText(Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "screenshots", "the_tollgate-113-turn3.script"));
        Ironwake.Client.Script.Apply(client, script);
        Assert.True(client.Select(new Coord(8, 7)));
        return client;
    }

    private static readonly Regex Strike = new(@"dmg (\d+)(?: x(\d+))? hit (\d+)% crit (\d+)%");

    [Fact]
    public void TheDrawnForecastsNumbersAreTheConsoleLinesNumbers()
    {
        var client = TurnThree();

        var hover = client.Hover(new Coord(7, 5)).Single();

        var first = hover.Text.Split('\n')[0];
        var sides = Strike.Matches(first);
        Assert.Equal(2, sides.Count);
        foreach (var (match, side) in new[] { (sides[0], hover.Card.Attacker), (sides[1], hover.Card.Defender) })
        {
            Assert.Equal(int.Parse(match.Groups[1].Value), side.Strike.Damage);
            Assert.Equal(match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 1, side.Strike.StrikeCount);
            Assert.Equal(int.Parse(match.Groups[3].Value), side.Strike.DisplayedHit);
            Assert.Equal(int.Parse(match.Groups[4].Value), side.Strike.CritChance);
        }
    }

    [Fact]
    public void TheForecastNamesEachSidesSourceOfAvoidBesideItsTile()
    {
        var client = TurnThree();

        var card = client.Hover(new Coord(7, 5)).Single().Card;

        Assert.Equal("Forest 7,5 +20", card.Attacker.Ground);
        Assert.Equal("Forest 6,5 +20", card.Defender.Ground);
        Assert.Equal("teodor", card.Attacker.Id);
        Assert.Equal("toll_brigand-1", card.Defender.Id);
    }

    [Fact]
    public void EachSidesHpAfterIsItsHpLessEveryStrikeTheOtherCanLandNeverBelowZero()
    {
        var client = TurnThree();

        var card = client.Hover(new Coord(7, 5)).Single().Card;

        var a = card.Attacker;
        var d = card.Defender;
        Assert.Equal(Math.Max(0, a.Hp - d.Strike.Damage * d.Strike.StrikeCount), a.After);
        Assert.Equal(Math.Max(0, d.Hp - a.Strike.Damage * a.Strike.StrikeCount), d.After);
        Assert.Equal(a.Hp - a.After, a.Cost);
    }

    [Fact]
    public void AWoundedSidesBarCarriesItsLostHpApartFromThisStrikesCost()
    {
        var client = TurnThree();
        var teodor = client.State.Find("teodor")!;
        var wounded = client.State.WithUnit(teodor with { Hp = teodor.Hp - 5 });
        var session = new ClientSession(client.Content, wounded);
        Assert.True(session.Select(new Coord(8, 7)));

        var side = session.Hover(new Coord(7, 5)).Single().Card.Attacker;

        Assert.Equal(side.MaxHp - 5, side.Hp);
        Assert.True(side.Cost > 0);
        Assert.Equal(side.MaxHp, side.After + side.Cost + (side.MaxHp - side.Hp));
    }

    [Fact]
    public void TheDoublingNoteSaysNeitherDoublesWhenNeitherDoes()
    {
        var client = TurnThree();

        var card = client.Hover(new Coord(7, 5)).Single().Card;

        Assert.False(card.Attacker.Strike.Doubles || card.Defender.Strike.Doubles);
        Assert.Equal("neither doubles", card.Doubling);
        Assert.Equal("Teodor strikes first", card.Heading);
    }

    [Fact]
    public void TheUnitCardReadsTheValuesShowPrints()
    {
        var client = TurnThree();

        var card = client.Card(new Coord(8, 7))!;
        var show = client.Show(new Coord(8, 7))!;

        Assert.Equal("Teodor", card.Name);
        Assert.Contains($"{card.Name}, {card.ClassName} L{card.Level}", show[0]);
        Assert.Contains($"hp {card.Hp}/{card.MaxHp}  str {card.Stats.Str} mag {card.Stats.Mag} dex {card.Stats.Dex} spd {card.Stats.Spd}", show[1]);
        Assert.Contains($"mov {card.Mov}", show[1]);
        Assert.StartsWith($"  weapon: {card.Weapon}", show[2]);
        Assert.Equal($"  weapon: {card.WeaponLine}", show[2]);
    }

    [Fact]
    public void TheUnitCardIsNullForAnEmptyTile()
    {
        var client = TurnThree();

        Assert.Null(client.Card(new Coord(0, 0)));
    }

    [Fact]
    public void TheStopUnderTheCardIsTheMovePreviewsValuesWhereAStrikeIsPriced()
    {
        var client = TurnThree();
        var tile = new Coord(7, 5);
        var teodor = client.State.Find("teodor")!;
        var lines = Queries.Threats(client.State, client.Content, teodor, tile)!;

        var stop = client.Stop(tile)!;

        Assert.Null(client.Preview(tile));
        Assert.Equal(lines.Count(line => !line.Raises), stop.Strikers);
        Assert.Equal(Queries.IfAllLand(lines), stop.IfAllLand);
        Assert.Equal("Forest", stop.Terrain);
    }
}
