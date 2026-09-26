using Ironwake.Content;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Maps;

/// <summary>The starter content and the design doc's example map, shared by the map tests.</summary>
internal static class MapFixture
{
    public static readonly GameContent Content = ContentLoader.Load(Fixture.RealContentDirectory());

    /// <summary>
    /// <paramref name="content"/> with every class's certification requirements removed, for tests
    /// of what certifying does rather than of the shipped ladder (issue 72).
    /// </summary>
    public static GameContent WithoutLadder(GameContent content) => content with
    {
        Classes = content.Classes.SetItems(content.Classes.Select(c => KeyValuePair.Create(c.Key, c.Value with { Certification = CertificationRequirements.None }))),
    };

    /// <summary>The example from DESIGN.md section 10, as the sample file writes it.</summary>
    public const string OldMillRoad = """
        name: Old Mill Road
        size: 12x10
        win: rout
        turn_limit: 20
        recall: 3
        enemy_level: 1

        ............
        ..^^....n...
        ..^^..F.nn..
        ============
        ..~~........
        ..~~.....#..
        .........#..
        ..........^^
        ............
        ............

        units:
        P captain 1,8
        P recruit:wren 2,8
        E soldier 9,1 group:mill behavior:guard
        E archer 10,2 group:mill behavior:guard
        E brigand 6,5 group:road behavior:aggressive
        B bandit_leader 10,1 group:mill behavior:boss

        """;

    public static MapDefinition Parse(string text, string file = "test.map") => MapFormat.Parse(file, text, Content);

    /// <summary>The example map with one line replaced, for the error tests.</summary>
    public static string Replacing(string original, string replacement) => OldMillRoad.Replace(original + "\n", replacement + "\n");

    public static string MapsDirectory => Path.Combine(Fixture.RealContentDirectory(), MapFiles.MapsDirectory);
}
