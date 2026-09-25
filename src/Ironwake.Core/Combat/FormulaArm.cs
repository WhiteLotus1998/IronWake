namespace Ironwake.Core;

/// <summary>
/// The arms of issue 158: a <see cref="CombatFormula"/> and, for two of them, an adjustment
/// to the loaded content. The Sim's hit-band table and the CLI's <c>--formula</c> read the
/// same arm through <see cref="FormulaArms"/>, so a hand play and a measurement of one arm
/// are the same game for the same seed. <see cref="Main"/> is what ships.
/// </summary>
public enum FormulaArm
{
    /// <summary>Main as it ships: burden against Str / 5, speed once in avoid, content as loaded.</summary>
    Main,

    /// <summary>Arm 1: burden against the whole of Str.</summary>
    FullStr,

    /// <summary>Arm 2: arm 1 with attack speed counted twice in avoid.</summary>
    FullStrSpeedTwice,

    /// <summary>Arm 3: content only, every iron-tier weapon's hit 15 lower.</summary>
    IronHit15,

    /// <summary>Arm 4: arm 2's formulas with arm 3's content.</summary>
    FullStrSpeedTwiceIronHit15,
}

public static class FormulaArms
{
    public const int IronHitCut = 15;

    /// <summary>The content an arm plays: main's as loaded, or the iron tier (every weapon whose id starts with <c>iron_</c>) with its hit cut by 15.</summary>
    public static GameContent Content(GameContent content, FormulaArm arm)
    {
        if (arm != FormulaArm.IronHit15 && arm != FormulaArm.FullStrSpeedTwiceIronHit15)
        {
            return content;
        }

        var weapons = content.Weapons.ToBuilder();
        foreach (var (id, weapon) in content.Weapons)
        {
            if (id.StartsWith("iron_", StringComparison.Ordinal))
            {
                weapons[id] = weapon with { Hit = weapon.Hit - IronHitCut };
            }
        }

        return content with { Weapons = weapons.ToImmutable() };
    }

    /// <summary>The section 5 formulas an arm fights under.</summary>
    public static CombatFormula Formula(FormulaArm arm) => arm switch
    {
        FormulaArm.Main or FormulaArm.IronHit15 => CombatFormula.Standard,
        FormulaArm.FullStr => CombatFormula.FullStrBurden,
        FormulaArm.FullStrSpeedTwice or FormulaArm.FullStrSpeedTwiceIronHit15 => CombatFormula.FullStrBurdenSpeedTwice,
        _ => throw new ArgumentOutOfRangeException(nameof(arm), arm, "unknown arm"),
    };

    public static string Name(FormulaArm arm) => arm switch
    {
        FormulaArm.Main => "main",
        FormulaArm.FullStr => "arm 1 (full Str)",
        FormulaArm.FullStrSpeedTwice => "arm 2 (full Str, speed twice)",
        FormulaArm.IronHit15 => "arm 3 (iron hit -15)",
        FormulaArm.FullStrSpeedTwiceIronHit15 => "arm 4 (arm 2 + iron hit -15)",
        _ => throw new ArgumentOutOfRangeException(nameof(arm), arm, "unknown arm"),
    };

    /// <summary>The word a command line names an arm by: <c>standard</c>, then <c>arm1</c> to <c>arm4</c>.</summary>
    public static string Key(FormulaArm arm) => arm switch
    {
        FormulaArm.Main => "standard",
        FormulaArm.FullStr => "arm1",
        FormulaArm.FullStrSpeedTwice => "arm2",
        FormulaArm.IronHit15 => "arm3",
        FormulaArm.FullStrSpeedTwiceIronHit15 => "arm4",
        _ => throw new ArgumentOutOfRangeException(nameof(arm), arm, "unknown arm"),
    };

    /// <summary>The arm a <c>--formula</c> word names, or null for any word <see cref="Key"/> does not print.</summary>
    public static FormulaArm? Parse(string text)
    {
        foreach (var arm in Enum.GetValues<FormulaArm>())
        {
            if (Key(arm) == text)
            {
                return arm;
            }
        }

        return null;
    }

    /// <summary>The <c>--scheme</c> word: <c>one</c> is one roll, <c>two</c> the two-roll average; anything else is null.</summary>
    public static RollScheme? ParseScheme(string text) => text switch
    {
        "one" => RollScheme.OneRoll,
        "two" => RollScheme.TwoRollAverage,
        _ => null,
    };
}
