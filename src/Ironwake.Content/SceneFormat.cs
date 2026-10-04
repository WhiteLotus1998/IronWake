using System.Text;
using System.Text.RegularExpressions;
using Ironwake.Core;

namespace Ironwake.Content;

/// <summary>
/// The scene script format (issue 1001, WRITING.md process step 9, DECISIONS/0244): one plain-text
/// file per scene under <c>content/scenes</c>, named for the scene. A <c>#</c> line is a comment and a
/// blank line is nothing. The header comes first:
/// <code>
/// scene: mill_arrival
/// plays: after the_mill
/// beat: round 350
/// retired: m4, m9
/// </code>
/// <c>scene</c> is the file's name; <c>plays</c> is <c>before</c>, <c>camp</c> or <c>after</c> and a
/// campaign map id; <c>beat</c> (optional) names the beat sheet that argued for more than
/// <see cref="SceneScripts.LinesMax"/> lines; <c>retired</c> (optional) lists ids no line may take again.
/// Then each line is <c>&lt;id&gt; &lt;speaker&gt; [(if &lt;condition&gt;)] [(human:&lt;hash&gt;)]: &lt;text&gt;</c>,
/// and <c>if &lt;condition&gt;</c> on its own line opens a block that <c>end</c> closes; blocks do not
/// nest, and a line in one shows only when the block's condition and its own both hold. A condition is
/// facts joined by <c>and</c>, each optionally after <c>not</c>: <c>fallen &lt;unit&gt;</c>,
/// <c>met &lt;unit&gt;</c>, <c>pick &lt;unit&gt;</c>, <c>returned &lt;fate&gt;</c>, <c>drake [&lt;stage&gt;]</c>,
/// <c>oath &lt;side&gt;</c>, <c>quest &lt;id&gt;</c>, <c>support &lt;unit&gt; &lt;unit&gt; &lt;tier&gt;</c>
/// and <c>freed-fell</c>. Anything else fails load, naming the file, the line and the field.
/// </summary>
public static class SceneFormat
{
    /// <summary>The directory, under the content root, the scene scripts live in.</summary>
    public const string Directory = "scenes";

    private static readonly Regex Header = new(@"^(scene|plays|beat|retired):\s*(.*)$", RegexOptions.CultureInvariant);

    private static readonly Regex Line = new(
        @"^(?<id>[a-z0-9_]+) (?<speaker>[a-z0-9_]+)(?<tags>(?: \((?:if [^()]*|human:[^()]*)\))*): (?<text>.*)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex Id = new(@"^[a-z0-9_]+$", RegexOptions.CultureInvariant);

    private static readonly Regex Tag = new(@" \((?<body>[^()]*)\)", RegexOptions.CultureInvariant);

    private static readonly Regex Hash = new(@"^[0-9a-f]{4}$", RegexOptions.CultureInvariant);

    private static readonly Dictionary<string, SceneFactKind> Kinds = new(StringComparer.Ordinal)
    {
        ["fallen"] = SceneFactKind.Fallen,
        ["met"] = SceneFactKind.Met,
        ["pick"] = SceneFactKind.Pick,
        ["returned"] = SceneFactKind.Returned,
        ["drake"] = SceneFactKind.DrakeFlew,
        ["oath"] = SceneFactKind.KeziahOath,
        ["quest"] = SceneFactKind.Quest,
        ["support"] = SceneFactKind.Support,
        ["freed-fell"] = SceneFactKind.FreedUnitFell,
    };

    /// <summary>
    /// Parses and validates <paramref name="file"/> against <paramref name="content"/>, which carries
    /// everything a scene names: the units, the cast, the campaign and the support tiers. Throws
    /// <see cref="ContentException"/> naming the file, the line's id (or its line number) and the field.
    /// </summary>
    public static Scene Parse(ContentFile file, GameContent content)
    {
        string? id = null;
        string? beat = null;
        (ScenePoint Point, string MapId)? plays = null;
        var retired = new List<string>();
        var lines = new List<SceneLine>();
        SceneCondition? block = null;
        var blockOpenedAt = 0;
        var raw = file.Text.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < raw.Length; i++)
        {
            var number = i + 1;
            var where = $"line {number}";
            var text = raw[i].TrimEnd();
            if (text.FirstOrDefault(c => c < ' ' || c > '~') is var bad && bad != '\0')
            {
                throw new ContentException(file.Name, where, null, $"character U+{(int)bad:X4} is not plain ASCII");
            }

            if (text.Length == 0 || text.StartsWith('#'))
            {
                continue;
            }

            if (Header.Match(text) is { Success: true } header)
            {
                var key = header.Groups[1].Value;
                var value = header.Groups[2].Value.Trim();
                if (lines.Count > 0 || block is not null)
                {
                    throw new ContentException(file.Name, where, key, "the header comes before the first line");
                }

                switch (key)
                {
                    case "scene":
                        id = value;
                        break;
                    case "plays":
                        plays = ParsePlays(file, where, value, content);
                        break;
                    case "beat":
                        beat = value.Length > 0 ? value : throw new ContentException(file.Name, where, "beat", "names no beat sheet");
                        break;
                    default:
                        foreach (var r in value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                        {
                            if (!Id.IsMatch(r))
                            {
                                throw new ContentException(file.Name, where, "retired", $"'{r}' is not a line id (lowercase letters, digits, _)");
                            }

                            retired.Add(r);
                        }

                        break;
                }

                continue;
            }

            if (text.StartsWith("if ", StringComparison.Ordinal))
            {
                if (block is not null)
                {
                    throw new ContentException(file.Name, where, "if", $"a block is already open from line {blockOpenedAt}; blocks do not nest");
                }

                block = ParseCondition(file, where, text[3..], content);
                blockOpenedAt = number;
                continue;
            }

            if (text == "end")
            {
                if (block is null)
                {
                    throw new ContentException(file.Name, where, "end", "no block is open");
                }

                block = null;
                continue;
            }

            if (Line.Match(text) is not { Success: true } line)
            {
                throw new ContentException(file.Name, where, null, "not a header, a line ('<id> <speaker>: <text>'), 'if <condition>' or 'end'");
            }

            lines.Add(ParseLine(file, line, block, content));
        }

        if (block is not null)
        {
            throw new ContentException(file.Name, $"line {blockOpenedAt}", "if", "the block is never closed with 'end'");
        }

        var stem = Path.GetFileNameWithoutExtension(file.Name);
        if (id is null)
        {
            throw new ContentException(file.Name, null, "scene", "missing; the header names the scene");
        }

        if (id != stem)
        {
            throw new ContentException(file.Name, null, "scene", $"'{id}' is not the file's name '{stem}'");
        }

        if (plays is not { } at)
        {
            throw new ContentException(file.Name, null, "plays", "missing; say 'before', 'camp' or 'after' and a campaign map");
        }

        if (lines.Count == 0)
        {
            throw new ContentException(file.Name, null, null, "the scene has no lines");
        }

        if (lines.Count > SceneScripts.LinesMax && beat is null)
        {
            throw new ContentException(file.Name, null, "beat", $"{lines.Count} lines, over {SceneScripts.LinesMax}; a longer scene names the beat sheet that argued for it");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var l in lines)
        {
            if (!seen.Add(l.Id))
            {
                throw new ContentException(file.Name, l.Id, "id", "used by an earlier line; ids are unique in a scene");
            }

            if (retired.Contains(l.Id))
            {
                throw new ContentException(file.Name, l.Id, "id", "is retired; a retired id is never reused");
            }
        }

        return new Scene(id, at.Point, at.MapId, ValueList<SceneLine>.From(lines))
        {
            Retired = ValueList<string>.From(retired),
            Beat = beat,
        };
    }

    /// <summary>
    /// <paramref name="scene"/> as canonical text that <see cref="Parse"/> reads back equal: the header,
    /// a blank line, then each line with its whole condition on it (a block's facts first), so blocks
    /// written by hand come back as per-line conditions.
    /// </summary>
    public static string Write(Scene scene)
    {
        var sb = new StringBuilder();
        sb.Append("scene: ").Append(scene.Id).Append('\n');
        sb.Append("plays: ").Append(PointWord(scene.Point)).Append(' ').Append(scene.MapId).Append('\n');
        if (scene.Beat is { } beat)
        {
            sb.Append("beat: ").Append(beat).Append('\n');
        }

        if (scene.Retired.Count > 0)
        {
            sb.Append("retired: ").Append(string.Join(", ", scene.Retired)).Append('\n');
        }

        sb.Append('\n');
        foreach (var line in scene.Lines)
        {
            sb.Append(line.Id).Append(' ').Append(line.Speaker);
            if (line.Condition is { } condition)
            {
                sb.Append(" (if ").Append(WriteCondition(condition)).Append(')');
            }

            if (line.Lock is { } hash)
            {
                sb.Append(" (human:").Append(hash).Append(')');
            }

            sb.Append(": ").Append(line.Text).Append('\n');
        }

        return sb.ToString();
    }

    /// <summary>The word a scene's <c>plays</c> header uses for <paramref name="point"/>, and the protocol's.</summary>
    public static string PointWord(ScenePoint point) => point switch
    {
        ScenePoint.Before => "before",
        ScenePoint.Camp => "camp",
        _ => "after",
    };

    private static string WriteCondition(SceneCondition condition) =>
        string.Join(" and ", condition.Facts.Select(f =>
            (f.Negated ? "not " : "") + string.Join(' ', new[] { Kinds.First(k => k.Value == f.Kind).Key }.Concat(f.Args))));

    private static (ScenePoint, string) ParsePlays(ContentFile file, string where, string value, GameContent content)
    {
        var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        ScenePoint? point = words.Length == 2 ? words[0] switch
        {
            "before" => ScenePoint.Before,
            "camp" => ScenePoint.Camp,
            "after" => ScenePoint.After,
            _ => null,
        } : null;
        if (point is null)
        {
            throw new ContentException(file.Name, where, "plays", $"'{value}' is not 'before', 'camp' or 'after' and one map id");
        }

        if (content.Campaign.Maps.All(m => m.MapId != words[1]))
        {
            throw new ContentException(file.Name, where, "plays", $"'{words[1]}' is not a map of {ContentFiles.CampaignName}");
        }

        return (point.Value, words[1]);
    }

    private static SceneLine ParseLine(ContentFile file, Match line, SceneCondition? block, GameContent content)
    {
        var id = line.Groups["id"].Value;
        var speaker = line.Groups["speaker"].Value;
        var text = line.Groups["text"].Value.Trim();
        if (speaker != SceneScripts.Narration && !content.Units.ContainsKey(speaker))
        {
            throw new ContentException(file.Name, id, "speaker", $"'{speaker}' is not a unit id or '{SceneScripts.Narration}'");
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        if (speaker != SceneScripts.Narration && words > SceneScripts.SpokenWordsMax)
        {
            throw new ContentException(file.Name, id, "text", $"{words} words; a spoken line holds at most {SceneScripts.SpokenWordsMax}");
        }

        SceneCondition? own = null;
        string? hash = null;
        foreach (Match tag in Tag.Matches(line.Groups["tags"].Value))
        {
            var body = tag.Groups["body"].Value;
            if (body.StartsWith("if ", StringComparison.Ordinal))
            {
                own = own is null
                    ? ParseCondition(file, id, body[3..], content)
                    : throw new ContentException(file.Name, id, "if", "a line carries one condition; join facts with 'and'");
                continue;
            }

            if (hash is not null)
            {
                throw new ContentException(file.Name, id, "lock", "a line carries one lock");
            }

            hash = body["human:".Length..];
            if (!Hash.IsMatch(hash))
            {
                throw new ContentException(file.Name, id, "lock", $"'{hash}' is not 4 lowercase hex digits");
            }

            if (SceneScripts.LockHash(text) != hash)
            {
                throw new ContentException(file.Name, id, "lock", $"the text no longer matches its stamp {hash} (it hashes to {SceneScripts.LockHash(text)}); a locked line is Lotus's to change");
            }
        }

        var facts = (block?.Facts ?? ValueList<SceneFact>.Empty).Concat(own?.Facts ?? ValueList<SceneFact>.Empty).ToList();
        return new SceneLine(id, speaker, text)
        {
            Condition = facts.Count == 0 ? null : new SceneCondition(ValueList<SceneFact>.From(facts)),
            Lock = hash,
        };
    }

    private static SceneCondition ParseCondition(ContentFile file, string where, string text, GameContent content)
    {
        var facts = new List<SceneFact>();
        foreach (var term in text.Split(" and ", StringSplitOptions.TrimEntries))
        {
            var words = term.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
            var negated = words.Count > 0 && words[0] == "not";
            if (negated)
            {
                words.RemoveAt(0);
            }

            if (words.Count == 0 || !Kinds.TryGetValue(words[0], out var kind))
            {
                throw new ContentException(file.Name, where, "if", $"'{term}' names no fact; the facts are {string.Join(", ", Kinds.Keys)}");
            }

            var args = words.Skip(1).ToList();
            CheckFact(file, where, term, kind, args, content);
            facts.Add(new SceneFact(kind, ValueList<string>.From(args), negated));
        }

        return new SceneCondition(ValueList<SceneFact>.From(facts));
    }

    private static void CheckFact(ContentFile file, string where, string term, SceneFactKind kind, List<string> args, GameContent content)
    {
        void Fail(string problem) => throw new ContentException(file.Name, where, "if", $"'{term}': {problem}");

        void Arity(params int[] counts)
        {
            if (!counts.Contains(args.Count))
            {
                Fail($"takes {string.Join(" or ", counts)} {(counts.Max() == 1 ? "word" : "words")} after the fact");
            }
        }

        void Cast(string unit)
        {
            if (content.Cast.All(u => u.Id != unit))
            {
                Fail($"'{unit}' is not in the cast");
            }
        }

        void Word<T>(IReadOnlyDictionary<string, T> words)
        {
            if (!words.ContainsKey(args[0]))
            {
                Fail($"'{args[0]}' is not one of {string.Join(", ", words.Keys)}");
            }
        }

        switch (kind)
        {
            case SceneFactKind.Fallen:
                Arity(1);
                Cast(args[0]);
                break;
            case SceneFactKind.Met:
                Arity(1);
                if (content.Campaign.Maps.All(m => !m.Meets.Contains(args[0])))
                {
                    Fail($"'{args[0]}' is met at no camp");
                }

                break;
            case SceneFactKind.Pick:
                Arity(1);
                if (content.Campaign.Maps.All(m => !m.Branch.Contains(args[0])))
                {
                    Fail($"'{args[0]}' is offered at no branch");
                }

                break;
            case SceneFactKind.Returned:
                Arity(1);
                Word(SceneScripts.FateWords);
                break;
            case SceneFactKind.DrakeFlew:
                Arity(0, 1);
                if (args.Count == 1)
                {
                    Word(SceneScripts.StageWords);
                }

                break;
            case SceneFactKind.KeziahOath:
                Arity(1);
                Word(SceneScripts.OathWords);
                break;
            case SceneFactKind.Quest:
                Arity(1);
                if (content.Campaign.Quest(args[0]) is null)
                {
                    Fail($"'{args[0]}' is not a quest of {ContentFiles.CampaignName}");
                }

                break;
            case SceneFactKind.Support:
                Arity(3);
                if (Supports.Pair(content.Campaign, args[0], args[1]) is null)
                {
                    Fail($"'{args[0]}' and '{args[1]}' are not a support pair");
                }

                if (content.Rivalry.SupportTiers.All(t => t.Name != args[2]))
                {
                    Fail($"'{args[2]}' is not a support tier; the tiers are {string.Join(", ", content.Rivalry.SupportTiers.Select(t => t.Name))}");
                }

                break;
            default:
                Arity(0);
                break;
        }
    }
}
