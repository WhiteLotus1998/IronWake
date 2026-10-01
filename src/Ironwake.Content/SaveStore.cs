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
}
