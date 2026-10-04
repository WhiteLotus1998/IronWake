namespace Ironwake.Core;

/// <summary>Where in the campaign a scene plays (issue 1001, WRITING.md process step 9).</summary>
public enum ScenePoint
{
    /// <summary>After <c>march</c>, just before the map's battle begins.</summary>
    Before,

    /// <summary>When the map's camp opens, after its before card.</summary>
    Camp,

    /// <summary>Once the map is won, after its after card.</summary>
    After,
}

/// <summary>The record facts a scene's condition may name (issue 1001); the set is closed.</summary>
public enum SceneFactKind
{
    /// <summary><c>fallen &lt;unit&gt;</c>: the unit is among the record's fallen.</summary>
    Fallen,

    /// <summary><c>met &lt;unit&gt;</c>: the side character was met at a camp.</summary>
    Met,

    /// <summary><c>pick &lt;unit&gt;</c>: the claimant picked at the branch.</summary>
    Pick,

    /// <summary><c>returned &lt;fate&gt;</c>: the passed claimant's fate on the map that brought them back.</summary>
    Returned,

    /// <summary><c>drake</c> or <c>drake &lt;stage&gt;</c>: a rider's drake has flown, at any stage or at that one.</summary>
    DrakeFlew,

    /// <summary><c>oath &lt;side&gt;</c>: the side of the hunger Keziah's oath fell on.</summary>
    KeziahOath,

    /// <summary><c>quest &lt;id&gt;</c>: the side map was won.</summary>
    Quest,

    /// <summary><c>support &lt;a&gt; &lt;b&gt; &lt;tier&gt;</c>: the pair stands at that support tier or higher.</summary>
    Support,

    /// <summary><c>freed-fell</c>: the enemy a won map's <c>freed:</c> header bound was killed before her boss fell.</summary>
    FreedUnitFell,
}

/// <summary>
/// One fact of a scene condition (issue 1001): its kind, its arguments as written, and whether a
/// leading <c>not</c> turns it round.
/// </summary>
public sealed record SceneFact(SceneFactKind Kind, ValueList<string> Args, bool Negated);

/// <summary>A scene condition (issue 1001): facts joined by <c>and</c>, holding when every one holds. There is no <c>or</c>; two lines say it.</summary>
public sealed record SceneCondition(ValueList<SceneFact> Facts);

/// <summary>
/// One line of a scene script (issue 1001): its stable <see cref="Id"/>, never reused once retired;
/// its <see cref="Speaker"/>, a unit id, an incidental the scene declares, <see cref="SceneScripts.Narration"/>
/// or <see cref="SceneScripts.Rules"/>; and its text. A line shows
/// only while its <see cref="Condition"/> holds, the line's own facts and its block's together. A line
/// Lotus has stamped carries <see cref="Lock"/>, <see cref="SceneScripts.LockHash"/> of its text.
/// </summary>
public sealed record SceneLine(string Id, string Speaker, string Text)
{
    public SceneCondition? Condition { get; init; }

    public string? Lock { get; init; }
}

/// <summary>
/// An incidental speaker a scene declares (round 345): an unnamed person with at most
/// <see cref="SceneScripts.IncidentalLinesMax"/> lines and no voice sheet, by a scene-local id and the
/// name a line prints under (<c>The keeper</c>).
/// </summary>
public sealed record SceneIncidental(string Id, string Name);

/// <summary>
/// A scene script (issue 1001), one file under <c>content/scenes</c>: its id (the file's name), the
/// campaign point and main-line map it plays at, its lines in order, the ids it has retired, the
/// incidental speakers it declares, and the beat sheet that argued for more than
/// <see cref="SceneScripts.LinesMax"/> lines, when one did.
/// </summary>
public sealed record Scene(string Id, ScenePoint Point, string MapId, ValueList<SceneLine> Lines)
{
    public ValueList<string> Retired { get; init; } = ValueList<string>.Empty;

    public ValueList<SceneIncidental> Incidentals { get; init; } = ValueList<SceneIncidental>.Empty;

    public string? Beat { get; init; }
}

/// <summary>Queries and constants over scene scripts (issue 1001, WRITING.md).</summary>
public static class SceneScripts
{
    /// <summary>The speaker of a line nobody says.</summary>
    public const string Narration = "narration";

    /// <summary>
    /// The speaker of a system line (round 345, WRITING.md's Scope): a rules line a card used to carry,
    /// such as the commands a lesson teaches or the line naming who must not fall. Printed bare, outside
    /// <see cref="LinesMax"/>, under no word cap and none of WRITING's rules 1 to 11.
    /// </summary>
    public const string Rules = "rules";

    /// <summary>The most lines one incidental speaker has in a scene (round 345).</summary>
    public const int IncidentalLinesMax = 2;

    /// <summary>The most words a spoken line holds (WRITING.md).</summary>
    public const int SpokenWordsMax = 25;

    /// <summary>The most lines a scene holds, its <see cref="Rules"/> lines aside, unless its <see cref="Scene.Beat"/> names the beat sheet that argued for more (WRITING.md).</summary>
    public const int LinesMax = 40;

    /// <summary>The words a <c>returned</c> fact takes, the campaign record's own names (PROTOCOL.md).</summary>
    public static readonly IReadOnlyDictionary<string, ClaimantFate> FateWords = new Dictionary<string, ClaimantFate>(StringComparer.Ordinal)
    {
        ["turned"] = ClaimantFate.Turned,
        ["turnedAway"] = ClaimantFate.TurnedAway,
        ["spared"] = ClaimantFate.Spared,
        ["fell"] = ClaimantFate.Fell,
        ["stood"] = ClaimantFate.Stood,
    };

    /// <summary>The words a <c>drake</c> fact takes, the campaign record's own names (PROTOCOL.md).</summary>
    public static readonly IReadOnlyDictionary<string, DrakeStage> StageWords = new Dictionary<string, DrakeStage>(StringComparer.Ordinal)
    {
        ["half-grown"] = DrakeStage.HalfGrown,
        ["grown"] = DrakeStage.Grown,
        ["unbroken"] = DrakeStage.Unbroken,
    };

    /// <summary>The words an <c>oath</c> fact takes, the campaign record's own names (PROTOCOL.md).</summary>
    public static readonly IReadOnlyDictionary<string, OathSide> OathWords = new Dictionary<string, OathSide>(StringComparer.Ordinal)
    {
        ["fed"] = OathSide.Fed,
        ["spared"] = OathSide.Spared,
        ["refused"] = OathSide.Refused,
        ["other"] = OathSide.Other,
    };

    /// <summary>
    /// The lock of a line Lotus has stamped (WRITING.md process step 9): 4 lowercase hex of the trimmed
    /// text, FNV-1a 32 over its characters' low bytes (the text is plain ASCII) folded to 16 bits.
    /// Stable across runtimes, unlike <see cref="string.GetHashCode()"/>.
    /// </summary>
    public static string LockHash(string text)
    {
        var hash = 2166136261u;
        foreach (var c in text.Trim())
        {
            hash ^= (byte)c;
            hash *= 16777619u;
        }

        return ((hash >> 16) ^ (hash & 0xFFFF)).ToString("x4", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>The scenes of <paramref name="content"/> that play at <paramref name="point"/> of <paramref name="mapId"/>, in id order.</summary>
    public static IReadOnlyList<Scene> At(GameContent content, ScenePoint point, string mapId) =>
        content.Scenes.Where(s => s.Point == point && s.MapId == mapId).OrderBy(s => s.Id, StringComparer.Ordinal).ToList();

    /// <summary>The lines of <paramref name="scene"/> whose condition holds on <paramref name="record"/>, in order.</summary>
    public static IReadOnlyList<SceneLine> Shown(Scene scene, CampaignRecord record, GameContent content) =>
        scene.Lines.Where(l => l.Condition is null || Holds(l.Condition, record, content)).ToList();

    /// <summary>Whether every fact of <paramref name="condition"/> holds on <paramref name="record"/>.</summary>
    public static bool Holds(SceneCondition condition, CampaignRecord record, GameContent content) =>
        condition.Facts.All(f => Holds(f, record, content) != f.Negated);

    private static bool Holds(SceneFact fact, CampaignRecord record, GameContent content) => fact.Kind switch
    {
        SceneFactKind.Fallen => record.Fallen.Contains(fact.Args[0]),
        SceneFactKind.Met => record.Met.Contains(fact.Args[0]),
        SceneFactKind.Pick => record.Pick == fact.Args[0],
        SceneFactKind.Returned => record.Returned == FateWords[fact.Args[0]],
        SceneFactKind.DrakeFlew => fact.Args.Count == 0 ? record.DrakeFlew is not null : record.DrakeFlew == StageWords[fact.Args[0]],
        SceneFactKind.KeziahOath => record.KeziahOath == OathWords[fact.Args[0]],
        SceneFactKind.Quest => record.WonQuestIds.Contains(fact.Args[0]),
        SceneFactKind.Support => SupportReached(fact.Args[0], fact.Args[1], fact.Args[2], record, content),
        _ => record.FreedUnitFell,
    };

    private static bool SupportReached(string a, string b, string tierName, CampaignRecord record, GameContent content)
    {
        if (Supports.TierOf(content, a, b, record.RapportOf(a, b)) is not { } reached)
        {
            return false;
        }

        var tiers = content.Rivalry.SupportTiers;
        var at = tiers.FirstOrDefault(t => t.Name == tierName)?.At ?? int.MaxValue;
        return reached.At >= at;
    }
}
