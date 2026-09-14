namespace Ironwake.Cli;

/// <summary>
/// Console front end for Ironwake. Commands arrive with issue 11; until then this
/// prints usage and exits non-zero so a script cannot mistake it for a working game.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        Console.WriteLine("ironwake cli (rules version " + Core.RulesVersion.Current + ")");
        Console.WriteLine("usage: ironwake <play|validate> ...   (no commands implemented yet)");
        return args.Length == 0 ? 0 : 2;
    }
}
