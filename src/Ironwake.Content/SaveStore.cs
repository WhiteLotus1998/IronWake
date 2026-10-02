using Ironwake.Content.Protocol;
using Ironwake.Core;

namespace Ironwake.Content;

/// <summary>
/// The campaign's saves on disk (issue 663, DESIGN section 9): each save is the campaign record as
/// the protocol writes it (<see cref="ProtocolJson.Campaign"/>), one file per save under one
/// directory. Autosaves are taken at the start of every camp and keep the last three, <c>auto-1</c>
/// the newest; named saves are the player's; the suspend is the one battle a quit left open, read
/// once and then deleted. There is no save inside a battle: Recall is the in-battle tool.
/// </summary>
public sealed class SaveStore
{
    public const string Extension = ".json";
    public const string AutoPrefix = "auto-";
    public const int AutosavesKept = 3;
    public const string SuspendFile = "suspend.txt";
    public const int MaxNameLength = 32;

    /// <summary>
    /// The player's profile (issue 664): one difficulty id a line, each a difficulty a campaign has
    /// been won on, which is what unlocks a difficulty's <see cref="Difficulty.UnlockedBy"/>. It
    /// lives beside the saves and outside every record, so loading an old save never locks it again.
    /// </summary>
    public const string ProfileFile = "profile.txt";

    public SaveStore(string directory)
    {
        Directory = directory;
    }

    /// <summary>Where the saves live.</summary>
    public string Directory { get; }

    private string PathOf(string name) => Path.Combine(Directory, name + Extension);

    private string SuspendPath => Path.Combine(Directory, SuspendFile);

    /// <summary>
    /// Why <paramref name="name"/> cannot name a save the player writes, or null: one to 32 lower-case
    /// letters, digits, hyphens or underscores, and not an autosave's name.
    /// </summary>
    public static string? NameRefusal(string name)
    {
        if (name.Length == 0 || name.Length > MaxNameLength || !name.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' or '_'))
        {
            return $"a save's name is 1 to {MaxNameLength} lower-case letters, digits, hyphens or underscores, not '{name}'";
        }

        return name.StartsWith(AutoPrefix, StringComparison.Ordinal) ? $"names starting '{AutoPrefix}' are the autosaves'; pick another" : null;
    }

    /// <summary>
    /// The autosave at the start of a camp: <c>auto-2</c> moves to <c>auto-3</c> (the old <c>auto-3</c>
    /// is dropped), <c>auto-1</c> to <c>auto-2</c>, and <paramref name="record"/> is written as <c>auto-1</c>.
    /// </summary>
    public void Autosave(CampaignRecord record)
    {
        System.IO.Directory.CreateDirectory(Directory);
        for (var i = AutosavesKept - 1; i >= 1; i--)
        {
            var from = PathOf(AutoPrefix + i);
            if (File.Exists(from))
            {
                File.Move(from, PathOf(AutoPrefix + (i + 1)), overwrite: true);
            }
        }

        File.WriteAllText(PathOf(AutoPrefix + 1), ProtocolJson.Campaign(record) + "\n");
    }

    /// <summary>
    /// Rewrites <c>auto-1</c> with <paramref name="record"/> without moving the older autosaves
    /// (issue 786): a camp left for the title keeps what was spent there, and the last three camps
    /// stay the last three, since <c>auto-1</c> was already this camp's autosave.
    /// </summary>
    public void RewriteAutosave(CampaignRecord record)
    {
        System.IO.Directory.CreateDirectory(Directory);
        File.WriteAllText(PathOf(AutoPrefix + 1), ProtocolJson.Campaign(record) + "\n");
    }

    /// <summary>Writes <paramref name="record"/> as the named save, replacing one of that name; the refusal, or null once written.</summary>
    public string? Save(string name, CampaignRecord record)
    {
        if (NameRefusal(name) is { } refusal)
        {
            return refusal;
        }

        System.IO.Directory.CreateDirectory(Directory);
        File.WriteAllText(PathOf(name), ProtocolJson.Campaign(record) + "\n");
        return null;
    }

    /// <summary>The record saved as <paramref name="name"/>, or why it cannot be read: no such save, or a file the protocol refuses.</summary>
    public (CampaignRecord? Record, string? Refusal) Load(string name, GameContent content)
    {
        var path = PathOf(name);
        if (name.Length == 0 || name.Contains('/') || name.Contains('\\') || !File.Exists(path))
        {
            return (null, $"no save '{name}' in {Directory}");
        }

        try
        {
            return (ProtocolJson.ReadCampaign(File.ReadAllText(path), content), null);
        }
        catch (ProtocolException e)
        {
            return (null, $"save '{name}' cannot be read: {e.Message}");
        }
    }

    /// <summary>Every save's name, the autosaves first, newest first, then the named saves in name order.</summary>
    public IReadOnlyList<string> Names()
    {
        if (!System.IO.Directory.Exists(Directory))
        {
            return Array.Empty<string>();
        }

        var names = System.IO.Directory.GetFiles(Directory, "*" + Extension)
            .Select(Path.GetFileNameWithoutExtension)
            .OfType<string>()
            .ToList();
        var autos = Enumerable.Range(1, AutosavesKept).Select(i => AutoPrefix + i).Where(names.Contains);
        var named = names.Where(n => NameRefusal(n) is null).OrderBy(n => n, StringComparer.Ordinal);
        return autos.Concat(named).ToList();
    }

    /// <summary>
    /// Writes the suspend: the record the battle began from, then every battle line typed in it,
    /// one a line. A later suspend replaces it.
    /// </summary>
    public void Suspend(CampaignRecord record, IEnumerable<string> battleLines)
    {
        System.IO.Directory.CreateDirectory(Directory);
        File.WriteAllText(SuspendPath, ProtocolJson.Campaign(record) + "\n" + string.Concat(battleLines.Select(l => l + "\n")));
    }

    /// <summary>Whether a suspend is waiting.</summary>
    public bool HasSuspend => File.Exists(SuspendPath);

    /// <summary>
    /// Reads the suspend and deletes it, so it resumes once: the record and the battle lines, or why
    /// it cannot be read (none waiting, or a record the protocol refuses; a refused file is deleted too).
    /// </summary>
    public (CampaignRecord? Record, IReadOnlyList<string> Lines, string? Refusal) TakeSuspend(GameContent content)
    {
        if (!HasSuspend)
        {
            return (null, Array.Empty<string>(), $"no suspended battle in {Directory}");
        }

        var lines = File.ReadAllText(SuspendPath).ReplaceLineEndings("\n").Split('\n');
        File.Delete(SuspendPath);
        try
        {
            var record = ProtocolJson.ReadCampaign(lines[0], content);
            return (record, lines.Skip(1).Where(l => l.Length > 0).ToList(), null);
        }
        catch (ProtocolException e)
        {
            return (null, Array.Empty<string>(), $"the suspended battle cannot be read: {e.Message}");
        }
    }

    private string ProfilePath => Path.Combine(Directory, ProfileFile);

    private IReadOnlyList<string> ProfileLines() =>
        File.Exists(ProfilePath) ? File.ReadAllLines(ProfilePath) : Array.Empty<string>();

    /// <summary>The difficulty ids a campaign has been won on, from the profile (its lines without a colon); none when there is no profile yet.</summary>
    public IReadOnlyList<string> Won() =>
        ProfileLines().Select(l => l.Trim()).Where(l => l.Length > 0 && !Options.IsOptionLine(l)).Distinct().ToList();

    /// <summary>Records a campaign won on <paramref name="difficulty"/> in the profile, its options kept; true when it was not there before.</summary>
    public bool RecordWin(string difficulty)
    {
        var won = Won();
        if (won.Contains(difficulty))
        {
            return false;
        }

        WriteProfile(won.Append(difficulty).ToList(), ReadOptions().Options);
        return true;
    }

    /// <summary>
    /// The player's options from the profile (issue 677), the defaults where it sets none, and a
    /// warning for each line that could not be read.
    /// </summary>
    public (Options Options, IReadOnlyList<string> Warnings) ReadOptions() => Options.Read(ProfileLines());

    /// <summary>Writes <paramref name="options"/> to the profile (issue 677), the won difficulties kept: each option takes effect on change, so a renderer writes on every change.</summary>
    public void WriteOptions(Options options) => WriteProfile(Won(), options);

    private void WriteProfile(IReadOnlyList<string> won, Options options)
    {
        System.IO.Directory.CreateDirectory(Directory);
        var lines = won.ToList();
        if (options != new Options() || ProfileLines().Any(l => Options.IsOptionLine(l.Trim())))
        {
            lines.AddRange(options.Lines());
        }

        File.WriteAllLines(ProfilePath, lines);
    }

    /// <summary>
    /// The save Continue loads (issue 677): the newest written of every save, autosave or named, or
    /// null when there is none. A tie in the write time goes to the earlier name in <see cref="Names"/>,
    /// so <c>auto-1</c> beats a named save written in the same instant.
    /// </summary>
    public string? Newest()
    {
        string? newest = null;
        var newestTime = DateTime.MinValue;
        foreach (var name in Names())
        {
            var time = File.GetLastWriteTimeUtc(PathOf(name));
            if (newest is null || time > newestTime)
            {
                (newest, newestTime) = (name, time);
            }
        }

        return newest;
    }
}
