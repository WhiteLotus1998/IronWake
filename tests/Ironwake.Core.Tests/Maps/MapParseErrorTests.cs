using Ironwake.Content;

namespace Ironwake.Core.Tests.Maps;

/// <summary>One case per parse or validation error, each checking the line it names.</summary>
public class MapParseErrorTests
{
    private static MapException Fails(string text)
    {
        var e = Assert.Throws<MapException>(() => MapFixture.Parse(text, "bad.map"));
        Assert.Equal("bad.map", e.File);
        Assert.StartsWith("bad.map", e.Message);
        return e;
    }

    private static void Fails(string text, int line, string fragment)
    {
        var e = Fails(text);
        Assert.Equal(line, e.Line);
        Assert.Contains(fragment, e.Problem);
        if (line > 0)
        {
            Assert.Contains("line " + line, e.Message);
        }
    }

    [Fact]
    public void EmptyFile() => Fails("\n\n", 0, "empty");

    [Theory]
    [InlineData("name: Old Mill Road", "name")]
    [InlineData("size: 12x10", "size")]
    [InlineData("win: rout", "win")]
    [InlineData("turn_limit: 20", "turn_limit")]
    public void MissingRequiredHeader(string original, string key) =>
        Fails(MapFixture.Replacing(original, "").Replace("\n\n\n", "\n\n"), 0, "missing '" + key + "'");

    [Fact]
    public void HeaderLineWithoutColon() => Fails(MapFixture.Replacing("win: rout", "win rout"), 3, "key: value");

    [Fact]
    public void UnknownHeaderKey() => Fails(MapFixture.Replacing("win: rout", "win: rout\nfog: yes"), 4, "unknown header key 'fog'");

    [Fact]
    public void DuplicateHeaderKey() => Fails(MapFixture.Replacing("win: rout", "win: rout\nwin: seize"), 4, "twice");

    [Fact]
    public void EmptyHeaderValue() => Fails(MapFixture.Replacing("name: Old Mill Road", "name:"), 1, "no value");

    [Theory]
    [InlineData("size: 12", "WIDTHxHEIGHT")]
    [InlineData("size: twelve x ten", "WIDTHxHEIGHT")]
    [InlineData("size: 0x10", "1..64")]
    [InlineData("size: 12x65", "1..64")]
    public void BadSize(string replacement, string fragment) => Fails(MapFixture.Replacing("size: 12x10", replacement), 2, fragment);

    [Fact]
    public void UnknownWinCondition() => Fails(MapFixture.Replacing("win: rout", "win: conquer"), 3, "rout, seize, defeat_boss, survive, escape");

    [Theory]
    [InlineData("turn_limit: 20", "turn_limit: 0", 4)]
    [InlineData("turn_limit: 20", "turn_limit: soon", 4)]
    [InlineData("recall: 3", "recall: -1", 5)]
    [InlineData("enemy_level: 1", "enemy_level: 31", 6)]
    public void IntegerHeaderOutOfRange(string original, string replacement, int line) =>
        Fails(MapFixture.Replacing(original, replacement), line, "must be an integer");

    [Fact]
    public void CheapShotsOnlyAcceptsAllowed() =>
        Fails(MapFixture.Replacing("enemy_level: 1", "enemy_level: 1\ncheap_shots: yes"), 7, "'allowed'");

    [Fact]
    public void GridRowTooWide() => Fails(MapFixture.Replacing("============", "============="), 11, "13 wide");

    [Fact]
    public void GridRowTooNarrow() => Fails(MapFixture.Replacing("============", "==========="), 11, "11 wide");

    [Fact]
    public void GridTooFewRows() => Fails(MapFixture.OldMillRoad.Replace("..........^^\n", ""), 17, "9 rows");

    [Fact]
    public void GridTooManyRows() => Fails(MapFixture.Replacing("..........^^", "..........^^\n............"), 18, "more than 10 rows");

    [Fact]
    public void UnknownGlyph() => Fails(MapFixture.Replacing("..~~........", "..~~...?...."), 12, "glyph '?' at column 7");

    [Fact]
    public void MissingUnitsBlock() => Fails(MapFixture.OldMillRoad[..MapFixture.OldMillRoad.IndexOf("units:", StringComparison.Ordinal)], 0, "missing 'units:'");

    [Fact]
    public void SomethingElseWhereUnitsShouldBe() => Fails(MapFixture.Replacing("units:", "army:"), 19, "expected 'units:'");

    [Fact]
    public void BadUnitPrefix() => Fails(MapFixture.Replacing("P captain 1,8", "X captain 1,8"), 20, "P, E, or B");

    [Fact]
    public void UnitLineTooShort() => Fails(MapFixture.Replacing("P captain 1,8", "P captain"), 20, "prefix, a unit, and a position");

    [Theory]
    [InlineData("P captain 1;8", "x,y")]
    [InlineData("P captain 1,eight", "x,y")]
    [InlineData("P captain 12,8", "outside the 12x10 grid")]
    [InlineData("P captain 1,-1", "outside the 12x10 grid")]
    public void BadPosition(string replacement, string fragment) => Fails(MapFixture.Replacing("P captain 1,8", replacement), 20, fragment);

    [Fact]
    public void TriggerIsNotAnAttribute() =>
        Fails(MapFixture.Replacing("E soldier 9,1 group:mill behavior:guard", "E soldier 9,1 group:mill behavior:guard trigger:0,0,4,4"), 22, "unknown unit attribute 'trigger'");

    [Fact]
    public void AttributeWithoutValue() =>
        Fails(MapFixture.Replacing("E soldier 9,1 group:mill behavior:guard", "E soldier 9,1 group: behavior:guard"), 22, "key:value");

    [Fact]
    public void AttributeRepeated() =>
        Fails(MapFixture.Replacing("E soldier 9,1 group:mill behavior:guard", "E soldier 9,1 group:mill group:road behavior:guard"), 22, "'group' appears twice");

    [Fact]
    public void PlayerUnitsTakeNoAttributes() => Fails(MapFixture.Replacing("P captain 1,8", "P captain 1,8 group:mill"), 20, "no attributes");

    [Fact]
    public void BadPlayerSlot() => Fails(MapFixture.Replacing("P captain 1,8", "P sergeant 1,8"), 20, "'captain', 'recruit', or 'recruit:<id>'");

    [Fact]
    public void EmptyRecruitId() => Fails(MapFixture.Replacing("P recruit:wren 2,8", "P recruit: 2,8"), 21, "'recruit:<id>'");

    [Fact]
    public void UnknownEnemyTemplate() => Fails(MapFixture.Replacing("E soldier 9,1 group:mill behavior:guard", "E dragon 9,1 group:mill behavior:guard"), 22, "unknown enemy template 'dragon'");

    [Fact]
    public void EnemyNeedsAGroup() => Fails(MapFixture.Replacing("E soldier 9,1 group:mill behavior:guard", "E soldier 9,1 behavior:guard"), 22, "group:<name>");

    [Fact]
    public void EnemyNeedsABehavior() => Fails(MapFixture.Replacing("E soldier 9,1 group:mill behavior:guard", "E soldier 9,1 group:mill"), 22, "behavior:aggressive");

    [Fact]
    public void UnknownBehavior() => Fails(MapFixture.Replacing("E soldier 9,1 group:mill behavior:guard", "E soldier 9,1 group:mill behavior:sleepy"), 22, "unknown behavior 'sleepy'");

    [Fact]
    public void BossBehaviorBelongsOnABLine() => Fails(MapFixture.Replacing("E soldier 9,1 group:mill behavior:guard", "E soldier 9,1 group:mill behavior:boss"), 22, "B line");

    [Fact]
    public void ABLineCannotBeAggressive() => Fails(MapFixture.Replacing("B bandit_leader 10,1 group:mill behavior:boss", "B bandit_leader 10,1 group:mill behavior:aggressive"), 25, "always boss");

    [Fact]
    public void TwoUnitsOnOneTile() => Fails(MapFixture.Replacing("P recruit:wren 2,8", "P recruit:wren 1,8"), 21, "occupied by the unit on line 20");

    [Fact]
    public void PlayerOnAWall() => Fails(MapFixture.Replacing("P recruit:wren 2,8", "P recruit:wren 9,5"), 21, "cannot start on Wall");

    [Fact]
    public void PlayerOnWater() => Fails(MapFixture.Replacing("P recruit:wren 2,8", "P recruit:wren 2,4"), 21, "cannot start on Water");

    [Fact]
    public void InfantryEnemyOnWater() => Fails(MapFixture.Replacing("E brigand 6,5 group:road behavior:aggressive", "E brigand 3,5 group:road behavior:aggressive"), 24, "Brigand (infantry) cannot start on Water");

    [Fact]
    public void AFlyingEnemyMayStartOnWater()
    {
        var map = MapFixture.Parse(MapFixture.Replacing("E brigand 6,5 group:road behavior:aggressive", "E wingrider 3,5 group:road behavior:aggressive"));

        Assert.Equal("wingrider", ((EnemyPlacement)map.Placements[4]).TemplateId);
    }

    [Fact]
    public void NoCaptain() => Fails(MapFixture.Replacing("P captain 1,8", "P recruit 1,8"), 0, "exactly one 'P captain', got 0");

    [Fact]
    public void TwoCaptains() => Fails(MapFixture.Replacing("P recruit:wren 2,8", "P captain 2,8"), 0, "exactly one 'P captain', got 2");

    [Fact]
    public void SeizeNeedsAThrone() => Fails(MapFixture.Replacing("win: rout", "win: seize"), 0, "no throne tile");

    [Fact]
    public void DefeatBossNeedsABoss() =>
        Fails(MapFixture.Replacing("win: rout", "win: defeat_boss").Replace("B bandit_leader 10,1 group:mill behavior:boss\n", ""), 0, "no B line");

    [Fact]
    public void LoadingAMissingFileNamesIt()
    {
        var e = Assert.Throws<MapException>(() => MapFiles.Load("/no/such.map", MapFixture.Content));

        Assert.Equal(0, e.Line);
        Assert.StartsWith("/no/such.map: file not found", e.Message);
    }
}
