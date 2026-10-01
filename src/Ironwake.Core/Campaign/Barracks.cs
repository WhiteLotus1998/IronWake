namespace Ironwake.Core;

/// <summary>
/// A soldier the barracks may hire (issue 690, DESIGN section 13.20): an id, a name, a pronoun,
/// a base class, the items they join with, and one dry line for the hire menu. A hire has no
/// region (so no rivalry), no supports, no signature, no quests and no epilogue card; the stats
/// and growths are not written here but derived from the cast by <see cref="Barracks"/>.
/// </summary>
public sealed record KeepHire(string Id, string Name, Pronoun Pronoun, string ClassId, ValueList<string> Items, string Line);

/// <summary>
/// The barracks' hiring rules (issue 690): who a hire is when they join, derived from the cast so
/// a hire is always a little worse than the people with stories, and printed before the purse pays.
/// </summary>
public static class Barracks
{
    /// <summary>How far below the story cast's growths a hire's are, per stat (issue 690).</summary>
    public const int GrowthPenalty = 10;

    /// <summary>The lowest growth a hire is given in any stat (issue 690).</summary>
    public const int GrowthFloor = 5;

    /// <summary>How many levels below the living company's average a hire joins (issue 690).</summary>
    public const int LevelsBelow = 2;

    /// <summary>The ending line a hire alive at the end of the campaign gets in place of an epilogue card (issue 690).</summary>
    public static string EndingLine(string name) => $"{name} served at the keep.";

    /// <summary>
    /// The cast members whose card a hire of <paramref name="classId"/> is read from: every cast
    /// member of that class but the captain, whose card is the campaign's own (issue 681).
    /// </summary>
    public static IReadOnlyList<Unit> Models(string classId, GameContent content) =>
        content.Cast.Skip(1).Where(u => u.ClassId == classId).ToList();

    /// <summary>
    /// A hire's growths (issue 690): the mean of the class's cast members' growths, rounded down,
    /// less <see cref="GrowthPenalty"/>, never below <see cref="GrowthFloor"/>.
    /// </summary>
    public static Stats Growths(string classId, GameContent content)
    {
        var models = Models(classId, content);
        if (models.Count == 0)
        {
            throw new InvalidOperationException($"no cast member but the captain is a {classId}, so a hire of that class has no card to read");
        }

        return Stats.Zero.Map((stat, _) => Math.Max(GrowthFloor, (models.Sum(u => u.Growths.Get(stat)) / models.Count) - GrowthPenalty));
    }

    /// <summary>
    /// The level a hire joins at now (issue 690): the living company's mean level, rounded down,
    /// less <see cref="LevelsBelow"/>, never below 1.
    /// </summary>
    public static int JoinLevel(CampaignRecord record) =>
        record.Roster.Count == 0 ? Unit.MinLevel : Math.Max(Unit.MinLevel, (record.Roster.Sum(u => u.Level) / record.Roster.Count) - LevelsBelow);

    /// <summary>
    /// <paramref name="hire"/> as they join at <paramref name="level"/> (issue 690): the class's
    /// cast members' level-1 stats averaged and rounded down, plus each level past 1 at the hire's
    /// growths with the class's growth modifiers (what a level-up rolls against) taken as an average, rounded down, so the card printed on the menu is the card
    /// that joins and no roll is spent; EXP 0, no ranks, no mastery, no abilities, no region, the
    /// hire's items at full uses.
    /// </summary>
    public static Unit Recruit(KeepHire hire, int level, GameContent content)
    {
        var models = Models(hire.ClassId, content);
        var growths = Growths(hire.ClassId, content);
        var levelling = growths + content.Class(hire.ClassId).GrowthModifiers;
        var stats = Stats.Zero.Map((stat, _) => (models.Sum(u => u.Stats.Get(stat)) / models.Count) + (Math.Clamp(levelling.Get(stat), 0, 100) * (level - 1) / 100));
        var items = hire.Items.Select(id => new ItemStack(id, content.Weapons.TryGetValue(id, out var weapon) ? weapon.Durability : content.Item(id).Uses));
        return new Unit(hire.Id, hire.Name, hire.ClassId, level, 0, stats, growths, new Inventory(ValueList<ItemStack>.From(items)), ValueList<string>.Empty, Region: null, Personality: hire.Line)
        {
            Pronoun = hire.Pronoun,
        };
    }

    /// <summary>Whether <paramref name="unit"/> came from the barracks (issue 690) rather than the cast.</summary>
    public static bool IsHire(Unit unit, GameContent content) => content.Campaign.Keep.Hire(unit.Id) is not null;
}
