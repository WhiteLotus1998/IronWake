namespace Ironwake.Core;

/// <summary>What a between-map action did: the record after it, and either the line the screen prints or the refusal.</summary>
public sealed record ScreenResult(CampaignRecord Record, string Text, bool Accepted)
{
    public static ScreenResult Refused(CampaignRecord record, string reason) => new(record, reason, false);
}

/// <summary>A certification trial tried this camp (issue 252): the unit and the class it tried for.</summary>
public sealed record TrialAttempt(string UnitId, string ClassId);

/// <summary>
/// A campaign between maps (issue 74, DESIGN section 9): the roster in roster order, the captain
/// first, each unit as its last map left it (EXP, level, ranks, mastery, weapon uses); the ids of
/// the fallen, since permadeath carries (section 1, pillar 4); the purse; the index of the next map
/// in <see cref="CampaignRules.Maps"/>; the campaign seed; the difficulty, chosen once for the whole
/// campaign (section 9); and the units benched from the next map. Immutable; every screen action
/// returns a new record. The between-map screen is the only place a record changes, and a battle
/// is the only thing between two screens.
/// </summary>
public sealed record CampaignRecord(
    ValueList<Unit> Roster,
    ValueList<string> Fallen,
    int Purse,
    int MapIndex,
    ulong Seed,
    string Difficulty,
    ValueList<string> Benched)
{
    public const string NormalDifficulty = "normal";

    /// <summary>
    /// The certification trials tried since the last map (issue 252), passed or failed: one
    /// attempt per unit and class per camp, so a failed trial opens again after the next map.
    /// </summary>
    public ValueList<TrialAttempt> TrialsTried { get; init; } = ValueList<TrialAttempt>.Empty;

    /// <summary>
    /// The edits bought for the keep (issue 288), in the order they were made. The keep the finale
    /// is fought on is the content's keep with these applied (<see cref="KeepMap"/>), so the record
    /// carries what was bought and the map is rebuilt from it, written canonically wherever it is shown.
    /// </summary>
    public ValueList<KeepWork> Keep { get; init; } = ValueList<KeepWork>.Empty;

    /// <summary>A new campaign: the cast in roster order, the starting purse, the first map, nobody benched.</summary>
    public static CampaignRecord Start(GameContent content, ulong seed, string difficulty = NormalDifficulty)
    {
        if (content.Campaign.Maps.Count == 0)
        {
            throw new InvalidOperationException("the content has no campaign: campaign.json is missing or lists no maps");
        }

        if (content.Cast.Count == 0)
        {
            throw new InvalidOperationException("the content has no cast");
        }

        if (content.Difficulties.Count > 0)
        {
            content.Difficulty(difficulty);
        }

        return new CampaignRecord(content.Cast, ValueList<string>.Empty, content.Campaign.StartingPurse, 0, seed, difficulty, ValueList<string>.Empty);
    }

    /// <summary>Whether every map of the campaign has been won.</summary>
    public bool IsFinished(GameContent content) => MapIndex >= content.Campaign.Maps.Count;

    /// <summary>The next map's campaign entry: its id, reward and the stock sold before it.</summary>
    public CampaignMap NextMap(GameContent content) =>
        IsFinished(content)
            ? throw new InvalidOperationException("the campaign is finished")
            : content.Campaign.Maps[MapIndex];

    /// <summary>
    /// The seed the next map's battle runs on: the campaign seed plus the map's index. Combat
    /// keys name no map (section 5), so one seed for every map would give the same strike on the
    /// same turn the same roll on every map, a pattern a player could learn; the first map plays
    /// on the campaign seed itself, so <c>play &lt;map&gt; --seed N</c> reproduces its rolls.
    /// </summary>
    public ulong BattleSeed => unchecked(Seed + (ulong)MapIndex);

    public Unit? Find(string unitId) => Roster.FirstOrDefault(u => u.Id == unitId);

    /// <summary>
    /// The next battle: <paramref name="map"/> (the next map, as the caller loaded it) under the
    /// campaign's difficulty, with the roster less the bench filling its slots in roster order,
    /// so benching a unit lets the next recruit take its bare slot, which is a deployment and not
    /// gate 4's ablation. A named slot whose recruit has fallen stays empty.
    /// </summary>
    public BattleState Begin(MapDefinition map, GameContent content, RollScheme scheme = RollScheme.TwoRollAverage)
    {
        var played = content.Difficulties.Count > 0 ? map.Under(content.Difficulty(Difficulty)) : map;
        var roster = Roster.Where(u => !Benched.Contains(u.Id)).ToList();
        var fallenNamed = map.Placements.OfType<PlayerPlacement>()
            .Where(p => p.Slot == PlayerSlot.NamedRecruit && p.RecruitId is { } id && Fallen.Contains(id))
            .Select(p => p.RecruitId!)
            .ToList();
        roster.AddRange(fallenNamed.Select(content.Unit));
        return BattleState.From(played, content, ValueList<Unit>.From(roster), BattleSeed, scheme, ValueList<string>.From(fallenNamed));
    }

    /// <summary>
    /// The record after a won battle: every deployed unit still standing comes back as the battle
    /// left it, with its spells refreshed and any consumable uses a <c>supplies</c> cap held back
    /// returned (the cap is what a unit brings into the battle; the rest stays in the wagon); a
    /// deployed unit missing from the board has fallen and leaves the roster, and on an Escape map
    /// a unit left behind by the captain's exit has fallen the same way (issue 269), since only
    /// the <see cref="BattleState.Survivors"/> come back; an undeployed unit
    /// is unchanged. The purse gains the map's reward, the bench is cleared, and the next map is
    /// the one after. A lost battle ends the campaign, so it has no record after it.
    /// </summary>
    public CampaignRecord AfterBattle(BattleState end, GameContent content)
    {
        if (end.Outcome.Result != BattleResult.Won)
        {
            throw new InvalidOperationException("only a won battle continues the campaign; a lost one ends it");
        }

        var opening = end.History.Count > 0 ? end.History[0] : end;
        var deployed = opening.UnitsOf(Side.Player).ToDictionary(u => u.Id, u => u.Unit, StringComparer.Ordinal);
        var standing = end.Survivors().ToDictionary(u => u.Id, u => u.Unit, StringComparer.Ordinal);
        var roster = new List<Unit>();
        var fallen = Fallen.ToList();
        foreach (var unit in Roster)
        {
            if (!deployed.TryGetValue(unit.Id, out var started))
            {
                roster.Add(unit);
            }
            else if (standing.TryGetValue(unit.Id, out var after))
            {
                roster.Add(BattleState.RefreshSpells(ReturnWithheld(unit, started, after, content), content));
            }
            else
            {
                fallen.Add(unit.Id);
            }
        }

        return this with
        {
            Roster = ValueList<Unit>.From(roster),
            Fallen = ValueList<string>.From(fallen),
            Purse = Purse + NextMap(content).Reward,
            MapIndex = MapIndex + 1,
            Benched = ValueList<string>.Empty,
            TrialsTried = ValueList<TrialAttempt>.Empty,
        };
    }

    /// <summary>
    /// <paramref name="after"/> with the consumable uses a <c>supplies</c> cap took from
    /// <paramref name="before"/> at the map's start given back: per item, the uses carried in
    /// less the uses the battle began with, topped up onto that item's stacks and then as new
    /// stacks. A stack the cap left at 1 or more is never removed by the cap, so the slots the
    /// returned uses need were the unit's before the battle.
    /// </summary>
    private static Unit ReturnWithheld(Unit before, Unit started, Unit after, GameContent content)
    {
        var items = after.Inventory.Items.ToList();
        foreach (var itemId in before.Inventory.Items.Select(s => s.ItemId).Distinct().Where(content.Items.ContainsKey))
        {
            var withheld = UsesOf(before, itemId) - UsesOf(started, itemId);
            var full = content.Item(itemId).Uses;
            for (var slot = 0; slot < items.Count && withheld > 0; slot++)
            {
                if (items[slot].ItemId == itemId && items[slot].Uses < full)
                {
                    var added = Math.Min(withheld, full - items[slot].Uses);
                    items[slot] = items[slot] with { Uses = items[slot].Uses + added };
                    withheld -= added;
                }
            }

            while (withheld > 0 && items.Count < Inventory.Capacity)
            {
                var stack = Math.Min(withheld, full);
                items.Add(new ItemStack(itemId, stack));
                withheld -= stack;
            }
        }

        return after with { Inventory = new Inventory(ValueList<ItemStack>.From(items)) };
    }

    private static int UsesOf(Unit unit, string itemId) => unit.Inventory.Items.Where(s => s.ItemId == itemId).Sum(s => s.Uses);

    /// <summary>
    /// Buys <paramref name="itemId"/> for <paramref name="unitId"/> at full uses: refused when the
    /// next map's shop does not stock it, the unit is not on the roster, its five slots are full,
    /// or the purse holds less than the price, naming the price and the balance.
    /// </summary>
    public ScreenResult Buy(string itemId, string unitId, GameContent content)
    {
        var stock = NextMap(content).Stock;
        if (!stock.Contains(itemId))
        {
            return ScreenResult.Refused(this, $"the shop does not stock '{itemId}'; it sells {string.Join(", ", stock)}");
        }

        if (Find(unitId) is not { } unit)
        {
            return ScreenResult.Refused(this, $"no unit '{unitId}' on the roster");
        }

        var (name, price, uses) = content.Weapons.TryGetValue(itemId, out var weapon)
            ? (weapon.Name, weapon.Price!.Value, weapon.Durability)
            : (content.Item(itemId).Name, content.Item(itemId).Price!.Value, content.Item(itemId).Uses);
        if (unit.Inventory.IsFull)
        {
            return ScreenResult.Refused(this, $"{unit.Id} carries {Inventory.Capacity} items already");
        }

        if (Purse < price)
        {
            return ScreenResult.Refused(this, $"{name} costs {price} and the purse holds {Purse}");
        }

        var bought = unit with { Inventory = unit.Inventory.Add(new ItemStack(itemId, uses)) };
        return new ScreenResult(Replace(bought) with { Purse = Purse - price }, $"{unit.Id} buys {name} for {price}; the purse holds {Purse - price}", true);
    }

    /// <summary>
    /// Repairs the weapon in <paramref name="slot"/> (0-based) of <paramref name="unitId"/> to its
    /// full durability at <see cref="CampaignRules.RepairPricePerUse"/> for every missing use, a
    /// broken weapon included, since repair is what makes broken a state and not a slot (section 5).
    /// Refused for an empty slot, a consumable, a spell, a weapon without a price, a weapon already
    /// at full uses, or a purse short of the cost.
    /// </summary>
    public ScreenResult Repair(string unitId, int slot, GameContent content)
    {
        if (Find(unitId) is not { } unit)
        {
            return ScreenResult.Refused(this, $"no unit '{unitId}' on the roster");
        }

        if (slot < 0 || slot >= unit.Inventory.Count)
        {
            return ScreenResult.Refused(this, $"{unit.Id} has no slot {slot + 1}; it carries {unit.Inventory.Count}");
        }

        var stack = unit.Inventory.Items[slot];
        if (!content.Weapons.TryGetValue(stack.ItemId, out var weapon))
        {
            return ScreenResult.Refused(this, $"{content.Item(stack.ItemId).Name} is not a weapon; nothing to repair");
        }

        if (weapon.IsMagic)
        {
            return ScreenResult.Refused(this, $"{weapon.Name} is a spell; its uses refresh every map");
        }

        if (CampaignRules.RepairPricePerUse(weapon) is not { } perUse)
        {
            return ScreenResult.Refused(this, $"{weapon.Name} cannot be repaired: no shop has ever sold one");
        }

        var missing = weapon.Durability - stack.Uses;
        if (missing <= 0)
        {
            return ScreenResult.Refused(this, $"{weapon.Name} is at full uses ({weapon.Durability})");
        }

        var cost = missing * perUse;
        if (Purse < cost)
        {
            return ScreenResult.Refused(this, $"repairing {weapon.Name} costs {cost} ({missing} uses at {perUse}) and the purse holds {Purse}");
        }

        var repaired = unit with { Inventory = unit.Inventory.Replace(slot, stack with { Uses = weapon.Durability }) };
        return new ScreenResult(
            Replace(repaired) with { Purse = Purse - cost },
            $"{unit.Id}'s {weapon.Name} repaired from {stack.Uses} to {weapon.Durability} uses for {cost}; the purse holds {Purse - cost}",
            true);
    }

    /// <summary>
    /// Certifies <paramref name="unitId"/> into <paramref name="classId"/> through
    /// <see cref="Certifications.Check"/> (issue 72), paying the seal, <see cref="CampaignRules.CertificationPrice"/>,
    /// from the purse. Refused naming every requirement failed, or the price and the balance.
    /// </summary>
    public ScreenResult Certify(string unitId, string classId, GameContent content)
    {
        if (Find(unitId) is not { } unit)
        {
            return ScreenResult.Refused(this, $"no unit '{unitId}' on the roster");
        }

        if (!content.Classes.TryGetValue(classId, out var target))
        {
            return ScreenResult.Refused(this, $"no class '{classId}'");
        }

        var refusals = Certifications.Check(unit, target);
        if (refusals.Count > 0)
        {
            return ScreenResult.Refused(this, $"{unit.Id} cannot certify as {target.Name}: {string.Join("; ", refusals.Select(r => r.Text))}");
        }

        var price = content.Campaign.CertificationPrice;
        if (Purse < price)
        {
            return ScreenResult.Refused(this, $"a seal costs {price} and the purse holds {Purse}");
        }

        var from = content.Class(unit.ClassId).Name;
        return new ScreenResult(
            Replace(Certifications.Certify(unit, target)) with { Purse = Purse - price },
            $"{unit.Id} certifies from {from} to {target.Name} for {price}; the purse holds {Purse - price}",
            true);
    }

    /// <summary>
    /// Why <paramref name="unitId"/> may not try the certification trial for <paramref name="classId"/>
    /// now (issue 252), or null when it may: the unit and the class must exist, the unit must not be
    /// in the class already, it must meet every requirement of <see cref="Certifications.Check"/>
    /// (a trial comes after them and stands in only for the seal), the class must have a trial, and
    /// the unit must not have tried that class's trial this camp.
    /// </summary>
    public string? TrialRefusal(string unitId, string classId, GameContent content)
    {
        if (Find(unitId) is not { } unit)
        {
            return $"no unit '{unitId}' on the roster";
        }

        if (!content.Classes.TryGetValue(classId, out var target))
        {
            return $"no class '{classId}'";
        }

        var refusals = Certifications.Check(unit, target);
        if (refusals.Count > 0)
        {
            return $"{unit.Id} cannot certify as {target.Name}: {string.Join("; ", refusals.Select(r => r.Text))}";
        }

        if (content.Campaign.TrialFor(classId) is null)
        {
            return $"{target.Name} has no trial; certify with a seal";
        }

        return TrialsTried.Contains(new TrialAttempt(unitId, classId))
            ? $"{unit.Id} has tried the {target.Name} trial since the last map; it opens again after the next one"
            : null;
    }

    /// <summary>
    /// Why <paramref name="trial"/>, the map <c>campaign.json</c> names for <paramref name="classId"/>,
    /// cannot be played as that class's trial, or null when it can: its <c>certification:</c> header
    /// must name the same class, since the loader reads the pairing without reading the map.
    /// </summary>
    public static string? TrialMapRefusal(MapDefinition trial, string classId) =>
        trial.Certification?.ClassId == classId ? null : $"the trial map '{trial.Name}' does not certify '{classId}'";

    /// <summary>
    /// The seed a trial before the next map runs on: the campaign seed plus the number of maps plus
    /// the next map's index, so no trial shares a seed with a map of the campaign.
    /// </summary>
    public ulong TrialSeed(GameContent content) => unchecked(Seed + (ulong)content.Campaign.Maps.Count + (ulong)MapIndex);

    /// <summary>
    /// The trial battle: <paramref name="trial"/> (the class's trial map, as the caller loaded it)
    /// with <paramref name="unitId"/> as the candidate, on <see cref="TrialSeed"/>. No difficulty
    /// applies, since a trial tests the class and not the campaign.
    /// </summary>
    public BattleState BeginTrial(MapDefinition trial, string unitId, GameContent content, RollScheme scheme = RollScheme.TwoRollAverage) =>
        BattleState.From(trial, content, ValueList<Unit>.From(new[] { Find(unitId) ?? throw new ArgumentException($"no unit '{unitId}' on the roster") }), TrialSeed(content), scheme);

    /// <summary>
    /// The record after a decided trial (issue 252). Either way the attempt is recorded. A pass
    /// certifies the unit into the trial's class with no seal, carrying the level, EXP, stats and
    /// weapon ranks the trial's fights left it; its own inventory, abilities and mastery points
    /// come back, so a trial earns no mastery. A failure, a candidate who fell in it included,
    /// changes nothing else: a trial is an exam, not a battle of the campaign.
    /// </summary>
    public ScreenResult AfterTrial(BattleState end, string unitId, GameContent content)
    {
        var trial = end.Map.Certification ?? throw new ArgumentException("the battle is not a certification trial");
        if (!end.Outcome.IsOver)
        {
            throw new InvalidOperationException("the trial is not decided");
        }

        var unit = Find(unitId) ?? throw new ArgumentException($"no unit '{unitId}' on the roster");
        var target = content.Class(trial.ClassId);
        var tried = this with { TrialsTried = TrialsTried.Add(new TrialAttempt(unitId, trial.ClassId)) };
        if (end.Outcome.Result != BattleResult.Won)
        {
            return new ScreenResult(tried, $"{unit.Id} fails the {target.Name} trial and stays a {content.Class(unit.ClassId).Name}; it opens again after the next map", true);
        }

        var after = end.UnitsOf(Side.Player).Single(u => u.Id == unitId).Unit;
        var certified = Certifications.Certify(unit, target) with
        {
            Level = after.Level,
            Exp = after.Exp,
            Stats = after.Stats,
            Skill = after.Skill,
        };
        return new ScreenResult(
            tried.Replace(certified),
            $"{unit.Id} passes the {target.Name} trial and certifies from {content.Class(unit.ClassId).Name} to {target.Name} with no seal; L{certified.Level} exp {certified.Exp}",
            true);
    }

    /// <summary>
    /// Benches <paramref name="unitId"/> from <paramref name="map"/>, the next map: its bare slot
    /// goes to the next recruit in roster order. Refused for the captain, the map's protected
    /// recruit, a recruit the map places by name, a unit not on the roster, or one already benched.
    /// </summary>
    public ScreenResult Bench(string unitId, MapDefinition map)
    {
        if (Find(unitId) is not { } unit)
        {
            return ScreenResult.Refused(this, $"no unit '{unitId}' on the roster");
        }

        if (unit.Id == Roster[0].Id)
        {
            return ScreenResult.Refused(this, $"{unit.Id} is the captain and leads every map");
        }

        if (map.ProtectId == unit.Id)
        {
            return ScreenResult.Refused(this, $"{map.Name} must protect {unit.Id}, who cannot be benched");
        }

        if (map.Placements.OfType<PlayerPlacement>().FirstOrDefault(p => p.RecruitId == unit.Id) is { } named)
        {
            return ScreenResult.Refused(this, $"{map.Name} places {unit.Id} by name at {named.At}");
        }

        if (Benched.Contains(unit.Id))
        {
            return ScreenResult.Refused(this, $"{unit.Id} is already benched");
        }

        return new ScreenResult(this with { Benched = Benched.Add(unit.Id) }, $"{unit.Id} is benched from {map.Name}", true);
    }

    /// <summary>Returns a benched unit to the deployment order; refused for a unit not benched.</summary>
    public ScreenResult Unbench(string unitId)
    {
        var at = Benched.IndexOf(unitId);
        return at < 0
            ? ScreenResult.Refused(this, $"{unitId} is not benched")
            : new ScreenResult(this with { Benched = Benched.RemoveAt(at) }, $"{unitId} returns to the deployment order", true);
    }

    /// <summary>
    /// The units the next battle deploys, by <see cref="Begin"/>'s own fill, in map placement order.
    /// </summary>
    public IReadOnlyList<string> Deployment(MapDefinition map, GameContent content) =>
        Begin(map, content).UnitsOf(Side.Player).OrderBy(u => u.PlacementIndex).Select(u => u.Id).ToList();

    /// <summary>
    /// Why the keep's menu is closed now, or null when it is open (issue 288): the campaign must
    /// have a keep with a raid among its maps, the raid must have been won, and a map must be left
    /// to fight. A spend before the raid would be a guess (DECISIONS/0059 decision 7).
    /// </summary>
    public string? KeepMenuRefusal(GameContent content)
    {
        var menu = content.Campaign.Keep;
        var raid = menu.RaidId.Length == 0 ? -1 : content.Campaign.Maps.Select(m => m.MapId).ToList().IndexOf(menu.RaidId);
        if (menu == KeepMenu.None || raid < 0)
        {
            return "the campaign has no keep to build";
        }

        if (MapIndex <= raid)
        {
            return $"the keep's menu opens after the raid on it ({menu.RaidId}, map {raid + 1}) is fought";
        }

        return IsFinished(content) ? "the campaign is finished" : null;
    }

    /// <summary>
    /// <paramref name="bare"/>, the content's keep as the caller loaded it, with every edit of
    /// <see cref="Keep"/> made in order: the keep the record holds.
    /// </summary>
    public MapDefinition KeepMap(MapDefinition bare, GameContent content) =>
        Keep.Aggregate(bare, (map, work) => Core.Keep.Apply(map, content.Campaign.Keep.Edit(work.EditId)
            ?? throw new InvalidOperationException($"the keep's menu has no edit '{work.EditId}'"), work.At));

    /// <summary>
    /// Buys <paramref name="editId"/> at <paramref name="at"/> for the keep (issue 288): refused
    /// while the menu is closed (<see cref="KeepMenuRefusal"/>), for an edit not on the menu, for a
    /// placement <see cref="Core.Keep.Refusal"/> refuses on the keep the record holds, or for a
    /// purse short of the price. The line says what the placement does in rules terms.
    /// </summary>
    public ScreenResult Build(string editId, Coord at, MapDefinition bare, GameContent content)
    {
        if (KeepMenuRefusal(content) is { } closed)
        {
            return ScreenResult.Refused(this, closed);
        }

        var menu = content.Campaign.Keep;
        if (menu.Edit(editId) is not { } edit)
        {
            return ScreenResult.Refused(this, $"the keep's menu has no '{editId}'; it sells {string.Join(", ", menu.Edits.Select(e => e.Id))}");
        }

        var keep = KeepMap(bare, content);
        if (Core.Keep.Refusal(keep, edit, at) is { } refusal)
        {
            return ScreenResult.Refused(this, refusal);
        }

        if (Purse < edit.Price)
        {
            return ScreenResult.Refused(this, $"{edit.Name} costs {edit.Price} and the purse holds {Purse}");
        }

        var line = Core.Keep.Describe(keep, edit, at, content);
        return new ScreenResult(
            this with { Purse = Purse - edit.Price, Keep = Keep.Add(new KeepWork(edit.Id, at)) },
            $"built for {edit.Price}, the purse holds {Purse - edit.Price}: {line}",
            true);
    }

    private CampaignRecord Replace(Unit unit) =>
        this with { Roster = ValueList<Unit>.From(Roster.Select(u => u.Id == unit.Id ? unit : u)) };
}
