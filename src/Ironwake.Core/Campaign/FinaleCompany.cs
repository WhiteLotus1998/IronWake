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
    /// <summary>The level the company is expected to stand at on the keep, and the level the Sim reads every <c>deploy: all</c> map at (provisional).</summary>
    public const int DefaultLevel = 8;

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
    /// campaign earns by the keep (<see cref="KeepMenu.FinaleRanks"/>, issue 1395 layer 2) and stocked within them
    /// (<see cref="Stocked"/>, issue 1395 layer 3), then hires in the keep's hire order at
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
        var members = content.Cast.Take(1 + Math.Min(story, content.Cast.Count - 1)).Select(u => Stocked(content.Campaign.Keep.FinaleRanked(CampaignRecord.Kitted(u, content).ScaledTo(level, content.Class(u.ClassId))), content)).ToList();
        var hires = content.Campaign.Keep.Hires.Select(h => Barracks.Recruit(h, hiresLevel, content));
        members.AddRange(company == FinaleCompany.Floor ? hires : hires.Take(Math.Max(0, CampaignRecord.CompanyCap - members.Count)));
        return ValueList<Unit>.From(members);
    }

    /// <summary>
    /// <paramref name="unit"/> with the keep shop's best weapon in each type it now outranks its pack in
    /// (issue 1395 layer 3): for every weapon type the unit carries, the highest-ranked weapon the keep
    /// map's stock sells that the unit can wield, if it outranks every weapon of that type the unit
    /// carries, goes in front of the first of them; a type the unit carries none of gains nothing. A full pack drops its last
    /// stack to make room, as <see cref="CampaignRecord.Kitted"/> does. A unit the stock offers nothing
    /// better is returned unchanged.
    /// </summary>
    public static Unit Stocked(Unit unit, GameContent content)
    {
        if (content.Campaign.Maps.FirstOrDefault(m => m.MapId == content.Campaign.Keep.MapId) is not { } keep)
        {
            return unit;
        }

        var unitClass = content.Class(unit.ClassId);
        var items = unit.Inventory.Items.ToList();
        var offers = keep.Stock
            .Where(id => content.Weapons.ContainsKey(id))
            .Select(content.Weapon)
            .Where(w => unit.CanWield(w, unitClass))
            .GroupBy(w => w.Type)
            .Select(g => g.OrderByDescending(w => w.Rank).ThenByDescending(w => w.Price ?? 0).First());
        foreach (var offer in offers)
        {
            var carried = items.Select((s, i) => (Index: i, Weapon: content.Weapons.TryGetValue(s.ItemId, out var w) ? w : null))
                .Where(c => c.Weapon is { } w && w.Type == offer.Type)
                .ToList();
            if (carried.Count == 0 || carried.Any(c => c.Weapon!.Rank >= offer.Rank))
            {
                continue;
            }

            var at = carried[0].Index;
            if (items.Count >= Inventory.Capacity)
            {
                items.RemoveAt(items.Count - 1);
                at = Math.Min(at, items.Count);
            }

            items.Insert(at, new ItemStack(offer.Id, offer.Durability));
        }

        return unit with { Inventory = new Inventory(ValueList<ItemStack>.From(items)) };
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
