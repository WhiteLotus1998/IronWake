namespace Ironwake.Sim;

/// <summary>
/// Headless harness. Runs the quality gates from DESIGN.md section 11.
/// --smoke runs the per-PR gates (5-8); --full runs the per-map gates (1-4).
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--smoke")
        {
            Console.WriteLine("smoke: no gates registered yet");
            return 0;
        }

        if (args.Length > 0 && args[0] == "--full")
        {
            Console.WriteLine("full: no gates registered yet");
            return 0;
        }

        Console.WriteLine("usage: ironwake-sim --smoke | --full <map> | --full --all");
        return 2;
    }
}
