using Ironwake.Cli;
using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Client;

/// <summary>
/// The thin renderer's parity gate (issue 347): the client's event log, played from a
/// committed script, against <c>ironwake play --log</c> for the same map, seed and script,
/// byte for byte; and the gate firing on a log one character away.
/// </summary>
[Collection("console")]
public class ClientParityTests
{
    public static string Root() => Directory.GetParent(Fixture.RealContentDirectory())!.FullName;

    public static string ScriptPath(string name) => Path.Combine(Root(), "tests", "parity", name + ".script");

    /// <summary>The console's event log for a committed parity script, as <c>play --log</c> writes it.</summary>
    public static string ConsoleLog(string map, ulong seed)
    {
        var log = Path.Combine(Path.GetTempPath(), $"ironwake-parity-{Guid.NewGuid():N}.log");
        try
        {
            ConsoleCapture.Run(() => PlaySession.Run(new[]
            {
                Path.Combine(Fixture.RealContentDirectory(), "maps", map + ".map"),
                "--seed", seed.ToString(), "--script", ScriptPath($"{map}-{seed}"), "--strict",
                "--content", Fixture.RealContentDirectory(), "--log", log,
            }));
            return File.ReadAllText(log);
        }
        finally
        {
            File.Delete(log);
        }
    }

    /// <summary>The client's event log for the same script, stepped through every enemy phase.</summary>
    public static string ClientLog(string map, ulong seed)
    {
        var content = ContentLoader.Load(Fixture.RealContentDirectory());
        var definition = MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "maps", map + ".map"), content);
        return Script.Play(content, BattleState.From(definition, content, content.Cast, seed), File.ReadAllText(ScriptPath($"{map}-{seed}")));
    }

    [Theory]
    [InlineData("brackwater_cut", 53UL)]
    [InlineData("sallow_grange", 61UL)]
    public void ClientEventLogMatchesTheConsoleByteForByte(string map, ulong seed)
    {
        var console = ConsoleLog(map, seed);
        var client = ClientLog(map, seed);

        Assert.Contains("-- enemy phase, turn 1 --", console);
        Assert.Null(Parity.FirstDifference(console, client));
    }

    [Fact]
    public void ApplyingAScriptPrefixOpensTheBattleWhereTheParityRunWas()
    {
        var content = ContentLoader.Load(Fixture.RealContentDirectory());
        var definition = MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "maps", "brackwater_cut.map"), content);
        var client = new ClientSession(content, BattleState.From(definition, content, content.Cast, 53));
        var screenshot = File.ReadAllText(Path.Combine(Root(), "docs", "screenshots", "brackwater_cut-53-turn3.script"));

        Script.Apply(client, screenshot);

        Assert.False(client.State.Outcome.IsOver);
        Assert.Equal(3, client.State.Turn);
        Assert.StartsWith(client.LogText, ClientLog("brackwater_cut", 53));
        Assert.True(client.Select(client.State.Find("dunstan")!.At));
    }

    [Fact]
    public void TheDuskParityScriptCarriesTheDarkLine()
    {
        Assert.Contains(ProtocolSession.DarkLine + "\n", ClientLog("brackwater_cut", 53));
    }

    [Fact]
    public void ParityFiresOnALogOneCharacterAway()
    {
        var console = ConsoleLog("sallow_grange", 61);
        var at = console.IndexOf("captain moves", StringComparison.Ordinal);
        var altered = console[..at] + "C" + console[(at + 1)..];

        var difference = Parity.FirstDifference(console, altered);

        Assert.NotNull(difference);
        var line = 1 + console[..at].Count(c => c == '\n');
        Assert.StartsWith($"event log differs at line {line}, column 1: console 'captain moves", difference);
        Assert.Contains("client 'Captain moves", difference);
    }

    [Fact]
    public void ParityFiresOnALogMissingItsLastLine()
    {
        var console = ConsoleLog("sallow_grange", 61);
        var cut = console[..(console.TrimEnd('\n').LastIndexOf('\n') + 1)];

        Assert.Contains("(end of log)", Parity.FirstDifference(console, cut));
    }

    [Fact]
    public void LogIsRefusedOnAProtocolRun()
    {
        var output = ConsoleCapture.Run(() => Assert.Equal(2, PlaySession.Run(new[] { "the_tollgate", "--protocol", "--log", "x.log", "--content", Fixture.RealContentDirectory() })));

        Assert.Contains("ERROR: --log applies to a text run", output);
    }
}
