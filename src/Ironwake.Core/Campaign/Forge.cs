namespace Ironwake.Core;

/// <summary>The two materials the forge works (issue 647): common for shop weapons and side characters' signatures, rare for the main line's.</summary>
public enum Material
{
    Common,
    Rare,
}

/// <summary>
/// The forge's numbers (issue 647, DESIGN section 13.20), from <c>campaign.json</c>'s <c>forge</c>:
/// what one Refine step adds, as Mt (<see cref="Mt"/>) or hit (<see cref="Hit"/>), never weight
/// or crit; the purse's fee for a step (<see cref="Price"/>); and how many steps a weapon takes on
/// common material (<see cref="CommonSteps"/>) and a main-line signature on rare (<see cref="RareSteps"/>).
/// <see cref="None"/> is a campaign without a forge.
/// </summary>
public sealed record ForgeRules(int Mt, int Hit, int Price, int CommonSteps, int RareSteps)
{
    public static ForgeRules None { get; } = new(0, 0, 0, 0, 0);
}

/// <summary>
/// The forge and Refine (issue 647, DESIGN section 13.20): a keep room, opened once the smith is
/// met, where a weapon is raised a step at a time, each step one material and a fee from the purse.
/// <list type="bullet">
/// <item>A priced shop weapon takes <see cref="ForgeRules.CommonSteps"/> on common material.</item>
/// <item>A signature bound to the captain or to a member with a quest (the main line) takes
/// <see cref="ForgeRules.RareSteps"/> on rare; any other bound weapon (a side character's) refines on common.</item>
/// <item>Never: a hungering weapon (Kinsbane), a healing spell, a weapon no shop sells and no one is
/// bound to, and an heirloom short of its last stage (the smith refuses rust, <see cref="Heirloom.SmithRefuses"/>).</item>
/// </list>
/// The steps live on the stack (<see cref="ItemStack.RefineMt"/>, <see cref="ItemStack.RefineHit"/>),
/// so they travel with the weapon, and the numbers are printed as they are (pillar 2).
/// </summary>
public static class Forge
{
    /// <summary>The weapon with its stack's Refine steps added; an unrefined stack unchanged.</summary>
    public static Weapon Shape(Weapon weapon, ItemStack stack) =>
        stack.RefineMt == 0 && stack.RefineHit == 0 ? weapon : weapon with { Mt = weapon.Mt + stack.RefineMt, Hit = weapon.Hit + stack.RefineHit };

    /// <summary>Whether <paramref name="memberId"/> is on the main line: the captain, or a member with a quest (side characters have none, DESIGN section 14).</summary>
    public static bool MainLine(string memberId, GameContent content) =>
        (content.Cast.Count > 0 && content.Cast[0].Id == memberId) || content.Campaign.Quests.Any(q => q.MemberId == memberId);

    /// <summary>The material <paramref name="weapon"/> refines on, or null with the reason it never does.</summary>
    public static (Material? Material, string? Refusal) MaterialFor(Weapon weapon, GameContent content)
    {
        if (weapon.Glass)
        {
            return (null, $"{weapon.Name} is glass; the forge cannot raise its edge");
        }

        if (weapon.Hungers)
        {
            return (null, $"{weapon.Name} is never Refined; it grows on what it is fed");
        }

        if (weapon.Heals)
        {
            return (null, $"{weapon.Name} is a healing spell; the forge has nothing to raise");
        }

        if (weapon.BoundTo is { } owner)
        {
            return (MainLine(owner, content) ? Material.Rare : Material.Common, null);
        }

        return weapon.Price is null ? (null, $"{weapon.Name} is no shop's weapon; the smith will not work it") : (Material.Common, null);
    }

    /// <summary>
    /// The material as the screen names it (issue 702): <c>common material</c>, and the rare one
    /// <c>frozen iron</c>, Lotus's name; the id <c>rareMaterial</c> and <see cref="Material.Rare"/> stay.
    /// </summary>
    public static string Label(Material material) => material == Material.Rare ? "frozen iron" : "common material";

    /// <summary>How many steps a weapon on <paramref name="material"/> may take.</summary>
    public static int MaxSteps(Material material, ForgeRules rules) => material == Material.Rare ? rules.RareSteps : rules.CommonSteps;

    /// <summary>The weapon's name as the pack prints it: <c>Iron Lance +2</c> once Refined, the bare name before.</summary>
    public static string Name(string name, ItemStack stack) => stack.Refines > 0 ? $"{name} +{stack.Refines}" : name;

    /// <summary>
    /// The rare material the campaign must pay, by the "exactly enough" rule (issue 647): every
    /// signature it issues (a quest's <see cref="CampaignQuest.Pays"/> or <see cref="CampaignQuest.Names"/>) that refines on rare, at
    /// <see cref="ForgeRules.RareSteps"/> each. Kinsbane never Refines, so it never counts.
    /// </summary>
    public static int RareNeeded(GameContent content) =>
        content.Campaign.Quests.Select(q => q.Pays ?? q.Names).OfType<string>().Distinct()
            .Count(id => MaterialFor(content.Weapon(id), content).Material == Material.Rare) * content.Campaign.Forge.RareSteps;

    /// <summary>The rare material the campaign pays: every quest's payout.</summary>
    public static int RarePaid(GameContent content) => content.Campaign.Quests.Sum(q => q.Rare);

    /// <summary>
    /// Null when the rare material paid equals what the issued signatures need, else the line the
    /// validator prints (issue 647): too little leaves a signature short of its last step, too much
    /// is material nothing can spend.
    /// </summary>
    public static string? RareRefusal(GameContent content)
    {
        var need = RareNeeded(content);
        var paid = RarePaid(content);
        return paid == need ? null
            : paid < need ? $"pays {paid} frozen iron and the signatures the campaign issues need {need} to Refine fully; {need - paid} short"
            : $"pays {paid} frozen iron and the signatures the campaign issues need {need}; {paid - need} over, which nothing can spend";
    }
}
