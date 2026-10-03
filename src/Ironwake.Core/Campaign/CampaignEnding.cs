namespace Ironwake.Core;

/// <summary>Kinsbane as the campaign left it (issue 807): who carried it, how far it was fed, its teeth and whether it woke.</summary>
public sealed record EndingKinsbane(string Bearer, int Fed, int Teeth, bool Woken);

/// <summary>The drake as the campaign left it (issue 807): its stage, and whether its rider lived to the end.</summary>
public sealed record EndingDrake(DrakeStage Stage, bool RiderLived);

/// <summary>
/// What a finished campaign hands a sequel (issue 807, docs/FUTURE.md): a versioned summary of the
/// ending and the run's key choices, written on the save in a shape a later game can read without
/// this one's code. Derived from the record, never stored on it, so nothing in this game reads it
/// back. <see cref="Ending"/> is <see cref="Pending"/> until the endings are built (#634).
/// </summary>
public sealed record CampaignEnding(
    int Version,
    string Ending,
    ulong Seed,
    string Difficulty,
    bool Permadeath,
    string? CaptainOrigin,
    Pronoun? CaptainPronoun,
    string CaptainClass,
    string? Pick,
    string? Passed,
    ClaimantFate? PassedFate,
    ValueList<string> Lived,
    ValueList<string> Fallen,
    bool FreedUnitFell,
    EndingKinsbane? Kinsbane,
    EndingDrake? Drake,
    ValueList<string> Rooms)
{
    /// <summary>The shape's version: a sequel reads the fields of the version it knows and refuses a newer one.</summary>
    public const int CurrentVersion = 1;

    /// <summary>The ending's word until the endings are built (#634): the campaign was won, which scene it closed on is not yet recorded.</summary>
    public const string Pending = "pending";

    /// <summary>
    /// The ending of <paramref name="record"/>, a campaign whose every map is won: the captain's origin,
    /// pronoun and class; the branch's pick, the passed claimant and their fate on the return; who lived
    /// (the roster's ids in roster order) and who fell; whether the bound enemy fell (<see cref="CampaignRecord.FreedUnitFell"/>);
    /// Kinsbane's bearer, fed count and teeth when a living member carries it; the drake's stage and
    /// whether its rider lived, when the rider ever joined; and the keep's rooms. Refused for a campaign
    /// still marching, since the ending is written once, when it ends.
    /// </summary>
    public static CampaignEnding Of(CampaignRecord record, GameContent content)
    {
        if (!record.IsFinished(content))
        {
            throw new InvalidOperationException("the campaign is not finished; its ending is written when it ends");
        }

        var captain = record.Captain(content) ?? record.Roster[0];
        var pronoun = captain.Pronoun ?? (content.Pronouns.TryGetValue(captain.Id, out var cast) ? (Pronoun?)cast : null);
        return new CampaignEnding(
            CurrentVersion,
            Pending,
            record.Seed,
            record.Difficulty,
            record.Permadeath,
            record.Origin,
            pronoun,
            captain.ClassId,
            record.Pick,
            record.Passed(content),
            record.Returned,
            ValueList<string>.From(record.Roster.Select(u => u.Id)),
            record.Fallen,
            record.FreedUnitFell,
            KinsbaneOf(record, content),
            DrakeOf(record),
            record.Rooms);
    }

    private static EndingKinsbane? KinsbaneOf(CampaignRecord record, GameContent content)
    {
        foreach (var unit in record.Roster)
        {
            foreach (var stack in unit.Inventory.Items)
            {
                if (content.Weapons.TryGetValue(stack.ItemId, out var weapon) && weapon.Hungers)
                {
                    return new EndingKinsbane(unit.Id, stack.Fed, Core.Kinsbane.Teeth(stack.Fed), Core.Kinsbane.Woken(stack.Fed));
                }
            }
        }

        return null;
    }

    private static EndingDrake? DrakeOf(CampaignRecord record)
    {
        if (record.Roster.Select(u => u.Drake).FirstOrDefault(d => d is not null) is { } drake)
        {
            return new EndingDrake(drake.Stage, true);
        }

        return record.DrakeFlew is { } flew ? new EndingDrake(flew, false) : null;
    }
}
