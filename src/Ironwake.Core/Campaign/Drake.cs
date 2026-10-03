namespace Ironwake.Core;

/// <summary>The drake's three stages (issue 805, STORY draft 6), in the order it grows; it never goes back.</summary>
public enum DrakeStage
{
    /// <summary>From the rider's arrival: a flier's mount as any other.</summary>
    HalfGrown,

    /// <summary>After the rider's quest 1 is won and enough main maps are flown (<see cref="DrakeRules.GrownFlown"/>).</summary>
    Grown,

    /// <summary>After the rider's quest 2 is won.</summary>
    Unbroken,
}

/// <summary>
/// The drake a rider carries (issue 805): its stage and the main maps it has flown, a won map the
/// rider was deployed on and stood at the end of. An animal on the rider, never a weapon, so it
/// rides on <see cref="Unit.Drake"/> and not in the pack. It changes only between maps.
/// </summary>
public sealed record DrakeState(DrakeStage Stage, int Flown);

/// <summary>
/// The campaign's drake (issue 805), <c>campaign.json</c>'s <c>drake</c>: the cast member who rides
/// it, the quest 1 and the maps flown that make it Grown, and the quest 2 that makes it Unbroken.
/// </summary>
public sealed record DrakeRules(string Member, string GrownAfter, int GrownFlown, string UnbrokenAfter)
{
    /// <summary>
    /// The stage <paramref name="drake"/> has reached with <paramref name="won"/> the side maps won:
    /// Unbroken once <see cref="UnbrokenAfter"/> is won; Grown once <see cref="GrownAfter"/> is won
    /// and <see cref="GrownFlown"/> maps are flown; else Half-grown. Never below the stage it holds.
    /// </summary>
    public DrakeStage StageFor(DrakeState drake, IReadOnlyCollection<string> won)
    {
        var reached = won.Contains(UnbrokenAfter) ? DrakeStage.Unbroken
            : won.Contains(GrownAfter) && drake.Flown >= GrownFlown ? DrakeStage.Grown
            : DrakeStage.HalfGrown;
        return reached > drake.Stage ? reached : drake.Stage;
    }

    /// <summary><paramref name="unit"/> with its drake's stage read again against <paramref name="won"/>; any unit without a drake unchanged.</summary>
    public Unit Grow(Unit unit, IReadOnlyCollection<string> won) =>
        unit.Drake is { } drake && StageFor(drake, won) is var stage && stage != drake.Stage ? unit with { Drake = drake with { Stage = stage } } : unit;
}

/// <summary>What the screen prints for a drake (issue 805).</summary>
public static class Drake
{
    /// <summary>The stage's word as the screen prints it.</summary>
    public static string Word(DrakeStage stage) => stage switch
    {
        DrakeStage.HalfGrown => "half-grown",
        DrakeStage.Grown => "grown",
        _ => "unbroken",
    };

    /// <summary>
    /// The unit card's line for a rider (issue 805): the stage, and while Half-grown under the campaign's
    /// <see cref="CampaignRules.Drake"/> what grows it and how many maps are flown. Null for a unit with no drake.
    /// </summary>
    public static string? Card(Unit unit, GameContent content)
    {
        if (unit.Drake is not { } drake)
        {
            return null;
        }

        if (drake.Stage != DrakeStage.HalfGrown || content.Campaign.Drake is not { } rules)
        {
            return $"Drake: {Word(drake.Stage)}.";
        }

        var said = Referent.For(content, unit);
        return $"Drake: half-grown; grows once {unit.Name}'s first quest is won and {said.Subject} {(said.Plural ? "have" : "has")} flown {rules.GrownFlown} maps ({drake.Flown} so far).";
    }
}
