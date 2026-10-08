namespace Ironwake.Core;

/// <summary>
/// The three companies the keep finale is measured with (issue 692) and a hand chair can field
/// (issue 1217): full is the whole cast plus the first hires up to <see cref="CampaignRecord.CompanyCap"/>;
/// depleted is the captain, <see cref="FinaleCompanies.DepletedStory"/> story members and hires up
/// to the cap; floor is the captain, <see cref="FinaleCompanies.FloorStory"/> story members and every hire.
/// </summary>
public enum FinaleCompany
{
    Full,
    Depleted,
    Floor,
}

/// <summary>
/// The finale companies' rosters (issue 692), in one place so the Sim's measurement and
/// <c>play --company</c> field the same units at the same levels (issue 1217).
/// </summary>
public static class FinaleCompanies
{
    /// <summary>Story members beside the captain in the depleted company (issue 692).</summary>
    public const int DepletedStory = 5;

    /// <summary>Story members beside the captain in the floor company (issue 692).</summary>
    public const int FloorStory = 3;

    /// <summary>A company's name as printed and as <c>--company</c> takes it.</summary>
    public static string Name(FinaleCompany company) => company switch
    {
        FinaleCompany.Full => "full",
        FinaleCompany.Depleted => "depleted",
        _ => "floor",
    };

    /// <summary>The company <paramref name="name"/> names, or null for any other text.</summary>
    public static FinaleCompany? Parse(string name) => name switch
    {
        "full" => FinaleCompany.Full,
        "depleted" => FinaleCompany.Depleted,
        "floor" => FinaleCompany.Floor,
        _ => null,
    };

    /// <summary>
    /// The roster of <paramref name="company"/> in deploy order, captain first: story members in
    /// cast order as the campaign first fields them (<see cref="CampaignRecord.Kitted"/>, issue 1395:
    /// the issued weapon, the bound heirloom, a half-grown drake) raised to <paramref name="level"/> and to the ranks a
    /// campaign earns by the keep (<see cref="KeepMenu.FinaleRanks"/>, issue 1395 layer 2), then hires in the keep's hire order at
    /// <paramref name="level"/> less <see cref="Barracks.LevelsBelow"/>.
    /// </summary>
    public static ValueList<Unit> Roster(GameContent content, FinaleCompany company, int level)
    {
        var story = company switch
        {
            FinaleCompany.Full => content.Cast.Count - 1,
            FinaleCompany.Depleted => DepletedStory,
            _ => FloorStory,
        };
        var hiresLevel = Math.Max(Unit.MinLevel, level - Barracks.LevelsBelow);
        var members = content.Cast.Take(1 + Math.Min(story, content.Cast.Count - 1)).Select(u => content.Campaign.Keep.FinaleRanked(CampaignRecord.Kitted(u, content).ScaledTo(level, content.Class(u.ClassId)))).ToList();
        var hires = content.Campaign.Keep.Hires.Select(h => Barracks.Recruit(h, hiresLevel, content));
        members.AddRange(company == FinaleCompany.Floor ? hires : hires.Take(Math.Max(0, CampaignRecord.CompanyCap - members.Count)));
        return ValueList<Unit>.From(members);
    }

    /// <summary>
    /// The header line for <paramref name="roster"/> fielded as <paramref name="company"/>, for
    /// example <c>company: depleted (captain, 5 story, 6 hires)</c>: the captain, then the story
    /// members and hires the roster carries.
    /// </summary>
    public static string Line(FinaleCompany company, ValueList<Unit> roster, GameContent content)
    {
        var hires = roster.Count(u => Barracks.IsHire(u, content));
        var story = roster.Count - 1 - hires;
        return $"company: {Name(company)} (captain, {story} story, {hires} {(hires == 1 ? "hire" : "hires")})";
    }
}
