using Ironwake.Core;

namespace Ironwake.Sim;

/// <summary>
/// The Sim's answer to the branch (issue 633): at the branch's camp the Sim's campaigns pick Rook,
/// the claimant every measurement before the branch was taken with (she joined at the raid's camp,
/// issue 763), so a reading taken across the branch compares with the readings before it.
/// </summary>
public static class SimPick
{
    /// <summary>The claimant the Sim picks: Rook when the branch offers her, else the branch's first.</summary>
    public static string Claimant(CampaignMap map) => map.Branch.Contains("rook") ? "rook" : map.Branch[0];

    /// <summary>
    /// <paramref name="record"/> with the pick made when its next map offers a branch and none is made yet,
    /// and the meeting made when its next map offers one (issue 633 slice 3): the Sim meets the first side
    /// character offered whenever a bed is free, as the campaign did when every side character was on the
    /// roster from the first map. Else unchanged.
    /// </summary>
    public static CampaignRecord Made(CampaignRecord record, GameContent content)
    {
        if (record.IsFinished(content))
        {
            return record;
        }

        if (record.Pick is null && record.NextMap(content).Branch.Count > 0)
        {
            record = record.PickClaimant(Claimant(record.NextMap(content)), content).Record;
        }

        return record.NextMap(content).Meets is { Count: > 0 } meets ? record.Meet(meets[0], content).Record : record;
    }

    /// <summary>The side character the Sim meets at its next camp, or null when none is offered, one is met, or no bed is free.</summary>
    public static string? Meeting(CampaignRecord record, GameContent content) =>
        !record.IsFinished(content) && record.NextMap(content).Meets is { Count: > 0 } meets && record.Meet(meets[0], content).Accepted ? meets[0] : null;
}
