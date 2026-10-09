namespace Ironwake.Core;

/// <summary>What a between-map action did: the record after it, and either the line the screen prints or the refusal.</summary>
public sealed record ScreenResult(CampaignRecord Record, string Text, bool Accepted)
{
    public static ScreenResult Refused(CampaignRecord record, string reason) => new(record, reason, false);
}

/// <summary>A certification trial tried this camp (issue 252): the unit and the class it tried for.</summary>
public sealed record TrialAttempt(string UnitId, string ClassId);

/// <summary>A side map won (issue 635): its id and the index of the map it was won before, which times the member's next quest.</summary>
public sealed record QuestWon(string QuestId, int MapIndex);

/// <summary>Where a member of <see cref="CampaignRecord.Fallen"/> fell (issue 678): the unit's id and the display name of the board.</summary>
public sealed record FellOn(string UnitId, string MapName);

/// <summary>What became of the claimant passed on at the branch, on the map that brought them back (issue 633).</summary>
public enum ClaimantFate
{
    /// <summary>The pick talked them round and a bed was free: they joined the company.</summary>
    Turned,

    /// <summary>The pick talked them round and no bed was free (or the company was full): they left the field and did not join.</summary>
    TurnedAway,

    /// <summary>The captain talked them round: they left the field alive and rode on.</summary>
    Spared,

    /// <summary>They died on the field.</summary>
    Fell,

    /// <summary>The map was won with them still standing against the company.</summary>
    Stood,
}

/// <summary>
/// A campaign between maps (issue 74, DESIGN section 9): the roster in roster order, the captain
/// first, each unit as its last map left it (EXP, level, ranks, mastery, weapon uses); the ids of
/// the fallen, since permadeath carries (section 1, pillar 4); the purse; the index of the next map
/// in <see cref="CampaignRules.Maps"/>; the campaign seed; the difficulty, chosen once for the whole
/// campaign (section 9); and the units benched from the next map. Immutable; every screen action
/// returns a new record. The between-map screen is the only place a record changes, and a battle
/// is the only thing between two screens.
/// </summary>
public sealed partial record CampaignRecord(
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
    /// The most living members the company holds (issue 689): the captain and eleven. The fallen
    /// keep their beds and never count toward it; a meeting or a hire past it is refused where it happens.
    /// </summary>
    public const int CompanyCap = 12;

    /// <summary>
    /// The certification trials tried since the last map (issue 252), passed or failed: one
    /// attempt per unit and class per camp, so a failed trial opens again after the next map.
    /// </summary>
    public ValueList<TrialAttempt> TrialsTried { get; init; } = ValueList<TrialAttempt>.Empty;

    /// <summary>The side maps won (issue 635), in the order they were won; a won quest is never offered again.</summary>
    public ValueList<QuestWon> QuestsWon { get; init; } = ValueList<QuestWon>.Empty;

    /// <summary>
    /// The side maps fought since the last map (issue 635), won or lost: one attempt per camp, so a
    /// side map lost on the clock opens again after the next map.
    /// </summary>
    public ValueList<string> QuestsTried { get; init; } = ValueList<string>.Empty;

    /// <summary>
    /// The duties taken at this camp (issue 1331), one a unit, in the order taken: a side map's
    /// party, the forge, rest named outright, or the yard. A unit not named here rests; a won main
    /// map clears them.
    /// </summary>
    public ValueList<UnitDuty> Duties { get; init; } = ValueList<UnitDuty>.Empty;

    /// <summary>
    /// The side maps offered at a camp the company has left (issue 1129), tried or not, in the
    /// order first offered: <see cref="QuestsOffered"/> seats a quest never offered ahead of these.
    /// A record written before it carries none, and its open quests all seat as never offered.
    /// </summary>
    public ValueList<string> QuestsSeen { get; init; } = ValueList<string>.Empty;

    /// <summary>The ids of the side maps won (<see cref="QuestsWon"/>), what a unique class's unlock reads (issue 706).</summary>
    public IReadOnlyCollection<string> WonQuestIds => QuestsWon.Select(w => w.QuestId).ToList();

    /// <summary>
    /// The board each of <see cref="Fallen"/> fell on (issue 678), in the order they fell, for the
    /// camp's roster. A record written before it carries none, and its fallen print without a board.
    /// </summary>
    public ValueList<FellOn> FellOn { get; init; } = ValueList<FellOn>.Empty;

    /// <summary>The display name of the board <paramref name="unitId"/> fell on, or null when the record does not say.</summary>
    public string? FellOnMap(string unitId) => FellOn.FirstOrDefault(f => f.UnitId == unitId)?.MapName;

    /// <summary>
    /// The edits bought for the keep (issue 288), in the order they were made. The keep the finale
    /// is fought on is the content's keep with these applied (<see cref="KeepMap"/>), so the record
    /// carries what was bought and the map is rebuilt from it, written canonically wherever it is shown.
    /// </summary>
    public ValueList<KeepWork> Keep { get; init; } = ValueList<KeepWork>.Empty;

    /// <summary>
    /// The line <see cref="SettleKeep"/> left when it dropped keep work off the menu (issue 1154,
    /// DECISIONS/0266), or null when it dropped none. Only the loaded record carries it; the
    /// protocol never writes it, so a save written after the load holds no trace of it.
    /// </summary>
    public string? KeepSettled { get; init; }

    /// <summary>
    /// The rooms bought for the keep (issue 687, DESIGN section 13.20), by room id in the order
    /// they were bought. Between maps, so a Recall never touches it.
    /// </summary>
    public ValueList<string> Rooms { get; init; } = ValueList<string>.Empty;

    /// <summary>The company's common material for the forge (issue 647), from quests; between maps, so a Recall never touches it.</summary>
    public int CommonMaterial { get; init; }

    /// <summary>The company's rare material for the forge (issue 647), from the main line's quests.</summary>
    public int RareMaterial { get; init; }

    /// <summary>
    /// The wagon (issue 679): item ids that chests sent past a full pack on a won map, oldest
    /// first, each taken at full uses with <see cref="TakeFromWagon"/>. A lost map collects nothing.
    /// </summary>
    public ValueList<string> Wagon { get; init; } = ValueList<string>.Empty;

    /// <summary>
    /// Each pair's rapport across the campaign (issue 77), sorted by pair as
    /// <see cref="BattleState.Rapport"/> is: a battle begins with it on the board and a won map,
    /// or any decided side map, writes the board's back. Support tiers read it.
    /// </summary>
    public ValueList<Rapport> Rapport { get; init; } = ValueList<Rapport>.Empty;

    /// <summary>The rapport of <paramref name="a"/> and <paramref name="b"/> on the record, 0 when they have none.</summary>
    public int RapportOf(string a, string b)
    {
        var (first, second) = string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a);
        return Rapport.FirstOrDefault(r => r.A == first && r.B == second)?.Points ?? 0;
    }

    /// <summary>The uses below which the camp warns on leaving that an equipped weapon is low (issue 647).</summary>
    public const int LowUses = 5;

    /// <summary>
    /// The warning on leaving the camp (issue 647): each unit going to the next map, the bench left
    /// out, whose equipped weapon is a physical weapon with fewer than <see cref="LowUses"/> uses, in
    /// roster order, with that weapon. Spells refresh every map, so they never warn, and a hungering
    /// weapon in its starved form is <see cref="StarvedWeapons"/>'s, not low (issue 856). It never refuses.
    /// </summary>
    public IReadOnlyList<(Unit Unit, Weapon Weapon, int Uses)> LowWeapons(GameContent content)
    {
        var low = new List<(Unit, Weapon, int)>();
        foreach (var unit in Present(content).Where(u => !Benched.Contains(u.Id)))
        {
            var holder = new BattleUnit(unit, Side.Player, default, 1, false, false);
            var slot = holder.EquippedSlot(content);
            if (slot >= 0 && holder.EquippedWeapon(content) is { IsMagic: false } weapon && unit.Inventory.Items[slot].Uses < LowUses && !(weapon.Hungers && unit.Inventory.Items[slot].Starved))
            {
                low.Add((unit, weapon, unit.Inventory.Items[slot].Uses));
            }
        }

        return low;
    }

    /// <summary>
    /// The camp's starved line (issue 856, round 281): each unit going to the next map, the bench left
    /// out, carrying a hungering weapon in its starved form, in roster order, with that weapon. The form
    /// carries over the camp on purpose and ends on the next hit it lands (DESIGN 13.23).
    /// </summary>
    public IReadOnlyList<(Unit Unit, Weapon Weapon)> StarvedWeapons(GameContent content) =>
        Present(content).Where(u => !Benched.Contains(u.Id))
            .SelectMany(u => u.Inventory.Items.Where(s => s.Starved && content.Weapons.TryGetValue(s.ItemId, out var w) && w.Hungers).Select(s => (u, content.Weapon(s.ItemId))))
            .ToList();

    /// <summary>Whether the keep's forge stands (issue 647): a room marked <see cref="KeepRoom.Forge"/> has been built.</summary>
    public bool ForgeBuilt(GameContent content) => Rooms.Any(id => content.Campaign.Keep.Room(id) is { Forge: true });

    /// <summary>
    /// Whether a fall is for good (issue 664), chosen once at the start like the difficulty and
    /// printed on the record. Off, a unit that falls on a won map or a side map comes back
    /// <see cref="Core.Wound"/>ed instead of joining <see cref="Fallen"/>; the captain's death and
    /// a <c>protect:</c> death still lose the map either way, since those are the battle's rules.
    /// </summary>
    public bool Permadeath { get; init; } = true;

    /// <summary>
    /// Whether the enemy bound by a won map's <c>freed:</c> header was killed before her boss fell
    /// (issue 750, <see cref="BattleState.Bond"/>): once true, true for the rest of the campaign. The
    /// founding's conditional paragraph reads it once #656 opens; nothing reads it before then.
    /// </summary>
    public bool FreedUnitFell { get; init; }

    /// <summary>
    /// Which side of the hunger Keziah's oath fell on (issue 635 slice 16, Design Table rounds 303 to 305):
    /// set by <see cref="AfterQuest"/> on a won side map whose member bears the hungering weapon and whose
    /// board binds an enemy with <c>freed:</c> (the Oath Stone), from how the bound man left it. Null until
    /// then, and a lost side map writes nothing. Nothing reads it until the endings (#634).
    /// </summary>
    public OathSide? KeziahOath { get; init; }

    /// <summary>
    /// The stage a rider's drake had reached when the rider fell for good (issue 805, STORY draft 6:
    /// the drake leaves the field and is seen over the fells), on a main map or a side map; null while
    /// no rider has. A fall with <see cref="Permadeath"/> off keeps the rider and the drake, so it never
    /// sets it. Once set, set for the rest of the campaign. The ending's line reads it once #634 is built.
    /// </summary>
    public DrakeStage? DrakeFlew { get; init; }

    /// <summary><see cref="DrakeFlew"/> after <paramref name="fell"/> fell for good: the first rider's stage among them, else unchanged.</summary>
    private DrakeStage? DrakeFlewAfter(IEnumerable<Unit> fell) =>
        DrakeFlew ?? fell.Select(u => u.Drake?.Stage).FirstOrDefault(s => s is not null);

    /// <summary>
    /// The difficulties this campaign was lowered from at a camp (issue 677), the one it began on
    /// first; empty when it never was. Printed on the record beside the difficulty.
    /// </summary>
    public ValueList<string> LoweredFrom { get; init; } = ValueList<string>.Empty;

    /// <summary>
    /// Lowers the campaign's difficulty at a camp (issue 677): to <paramref name="difficultyId"/>,
    /// which must stand on a lower <see cref="Core.Difficulty.Tier"/> than the one in play. Raising
    /// it is refused, as is the same difficulty, and a finished campaign changes nothing. The
    /// difficulty left is kept in <see cref="LoweredFrom"/>. There is no battle-side version: a
    /// battle is fought under the difficulty it began on.
    /// </summary>
    public ScreenResult LowerDifficulty(string difficultyId, GameContent content)
    {
        if (!content.Difficulties.TryGetValue(difficultyId, out var target))
        {
            return ScreenResult.Refused(this, $"no difficulty '{difficultyId}'; there are {string.Join(", ", content.Difficulties.Values.OrderBy(d => d.Tier).Select(d => d.DisplayName))}");
        }

        if (IsFinished(content))
        {
            return ScreenResult.Refused(this, "the campaign is finished");
        }

        var current = content.Difficulty(Difficulty);
        if (target.Id == current.Id)
        {
            return ScreenResult.Refused(this, $"the campaign is already on {current.DisplayName}");
        }

        if (target.Tier >= current.Tier)
        {
            return ScreenResult.Refused(this, $"{target.DisplayName} is not easier than {current.DisplayName}; a difficulty may be lowered at a camp, never raised");
        }

        return new ScreenResult(
            this with { Difficulty = target.Id, LoweredFrom = LoweredFrom.Add(current.Id) },
            $"difficulty lowered from {current.DisplayName} to {target.DisplayName} for the rest of the campaign; the record says so",
            true);
    }

    /// <summary>
    /// The captain's origin (issue 681), chosen at the start, or null for the cast file's captain.
    /// Printed on the captain's card; the stats it set are already on the roster.
    /// </summary>
    public string? Origin { get; init; }

    /// <summary>
    /// The claimant picked at the branch's camp (issue 633, DESIGN section 14), a cast id from that
    /// map's <see cref="CampaignMap.Branch"/>, or null before the pick (and in a campaign without a
    /// branch). Kept for the rest of the campaign: the passed claimant's return and the endings read it.
    /// </summary>
    public string? Pick { get; init; }

    /// <summary>
    /// The <see cref="MapIndex"/> on which <c>march sure</c> answered a <c>keziah_warning</c> map's
    /// question (issue 871), or null. The question is asked once per map, so a retry of the same
    /// map marches without it.
    /// </summary>
    public int? WarningConfirmed { get; init; }

    /// <summary>
    /// What became of the passed claimant on the map that brought them back (issue 633), written when
    /// it is won, or null before then (and in a campaign without a return). The endings (#634) read it.
    /// </summary>
    public ClaimantFate? Returned { get; init; }

    /// <summary>
    /// The bosses a won map left in the coma (issue 1386 slice 2b, <see cref="BattleState.Coma"/>): the shard taken and
    /// broken, alive and off the field, in the order taken. Empty until then. The epilogue, where he wakes, reads it (#634).
    /// </summary>
    public ValueList<string> Coma { get; init; } = ValueList<string>.Empty;

    /// <summary>
    /// Whether the record meets the next map's secret path before it is fought (issue 1386 slice 2b, STORY's Under the
    /// Hill): the map names the conditions (<see cref="CampaignMap.Secret"/>); the claimant it names stands in the company
    /// carrying the hungering weapon woken; both claimants stand in it, the pick and the passed one turned on the return;
    /// and the side map it names is won. The fourth condition, the guide held, not killed, is the battle's
    /// (<see cref="ShardRun.Starts"/>). False on a finished campaign and on a map without conditions.
    /// </summary>
    public bool UnderTheHill(GameContent content)
    {
        if (IsFinished(content) || NextMap(content).Secret is not { } secret)
        {
            return false;
        }

        var woken = Find(secret.Bearer) is { } bearer && bearer.Inventory.Items.Any(stack =>
            content.Weapons.TryGetValue(stack.ItemId, out var weapon) && weapon.Hungers && Core.Kinsbane.Woken(stack.Fed));
        var claimants = Pick is { } pick && Find(pick) is not null && Returned == ClaimantFate.Turned && Passed(content) is { } passed && Find(passed) is not null;
        return woken && claimants && QuestsWon.Any(w => w.QuestId == secret.Quest);
    }

    /// <summary>
    /// The side characters met so far (issue 633 slice 3, DESIGN section 14), cast ids in the order met,
    /// each from some map's <see cref="CampaignMap.Meets"/>. A side character met joins at that map's
    /// camp like a joiner; one never met never joins. The endings read it.
    /// </summary>
    public ValueList<string> Met { get; init; } = ValueList<string>.Empty;

    /// <summary>
    /// The support conversations seen so far (issue 77 slice 8), scene ids in the order seen. Each
    /// plays once; <see cref="SceneScripts.NextConversation"/> skips one seen.
    /// </summary>
    public ValueList<string> SupportsSeen { get; init; } = ValueList<string>.Empty;

    /// <summary>
    /// Plays the support conversation waiting for <paramref name="a"/> and <paramref name="b"/> at the
    /// camp (issue 77 slice 8), each by id or by name in any case: the one
    /// <see cref="SceneScripts.NextConversation"/> names, recorded in <see cref="SupportsSeen"/>, the
    /// accepted text naming it (<c>Wren and Pell talk (support C)</c>). Refused, with the reason, when the
    /// campaign is over, either is not on the living roster, the two are no support pair, the pair is
    /// below C, or every conversation of a tier it has reached is seen or unwritten. Costs nothing.
    /// </summary>
    public ScreenResult SeeSupport(string a, string b, GameContent content)
    {
        if (IsFinished(content))
        {
            return ScreenResult.Refused(this, "the campaign is over");
        }

        Unit? Named(string text) => Roster.FirstOrDefault(u =>
            string.Equals(u.Id, text, StringComparison.OrdinalIgnoreCase) || string.Equals(u.Name, text, StringComparison.OrdinalIgnoreCase));
        if (Named(a) is not { } first)
        {
            return ScreenResult.Refused(this, $"no unit '{a}' on the roster");
        }

        if (Named(b) is not { } second)
        {
            return ScreenResult.Refused(this, $"no unit '{b}' on the roster");
        }

        if (Supports.Pair(content.Campaign, first.Id, second.Id) is not { } pair)
        {
            return ScreenResult.Refused(this, $"{first.Name} and {second.Name} are no support pair");
        }

        if (Supports.TierOf(content, pair.A, pair.B, RapportOf(pair.A, pair.B)) is null)
        {
            return ScreenResult.Refused(this, $"{first.Name} and {second.Name} have not reached support {content.Rivalry.SupportTiers.OrderBy(t => t.At).First().Name}");
        }

        if (SceneScripts.NextConversation(content, this, pair) is not { Support: { } at } scene)
        {
            return ScreenResult.Refused(this, $"{first.Name} and {second.Name} have no conversation waiting");
        }

        var seen = this with { SupportsSeen = SupportsSeen.Add(scene.Id) };
        return new ScreenResult(seen, $"{Find(at.A)!.Name} and {Find(at.B)!.Name} talk (support {at.Tier})", true);
    }

    /// <summary>
    /// Meets <paramref name="unitId"/> at the camp (issue 633 slice 3, DESIGN section 14): one of the
    /// next map's <see cref="CampaignMap.Meets"/>, by id or by name in any case, who joins the company
    /// for it like a joiner (<see cref="Present"/>, at no less than <see cref="JoinLevel"/>). At most
    /// one meeting a map, and it is final. Refused when the next map offers no meeting, when the id is
    /// not one it offers, once a meeting is made here, and when no bed is free or the company is full
    /// (<c>no bed free: &lt;name&gt; will not join</c>, DECISIONS/0137): a bed held for someone else
    /// is a bed the meeting cannot have.
    /// </summary>
    public ScreenResult Meet(string unitId, GameContent content)
    {
        if (IsFinished(content) || NextMap(content).Meets.Count == 0)
        {
            return ScreenResult.Refused(this, "nobody is met at this camp");
        }

        var meets = NextMap(content).Meets;
        if (meets.FirstOrDefault(Met.Contains) is { } made)
        {
            return ScreenResult.Refused(this, $"the meeting is made: {content.Unit(made).Name} is with the company");
        }

        var named = meets.FirstOrDefault(id => string.Equals(id, unitId, StringComparison.OrdinalIgnoreCase) || string.Equals(content.Unit(id).Name, unitId, StringComparison.OrdinalIgnoreCase));
        if (named is null)
        {
            return ScreenResult.Refused(this, $"'{unitId}' is not met here; meet {string.Join(" or ", meets)}");
        }

        var name = content.Unit(named).Name;
        if (Room(content) <= Arriving(content).Count())
        {
            return ScreenResult.Refused(this, CompanyFull(content) ? $"company full ({CompanyCap}): {name} will not join" : $"no bed free: {name} will not join");
        }

        var level = Math.Max(content.Unit(named).Level, JoinLevel(content));
        var met = this with { Met = Met.Add(named) };
        return new ScreenResult(met, $"{name} joins the company at level {level}", true);
    }

    /// <summary>
    /// The level the passed claimant comes back at on the next map (issue 633, DESIGN section 14):
    /// the pick's level, or the seat's (<see cref="SeatLevel"/>) once the pick has fallen, never
    /// below their own card's. Null when the next map brings nobody back.
    /// </summary>
    public int? ReturnLevel(GameContent content)
    {
        if (IsFinished(content) || NextMap(content).Return is null || Passed(content) is not { } passed || Returned is not null)
        {
            return null;
        }

        return Math.Max(content.Unit(passed).Level, Find(Pick!)?.Level ?? SeatLevel(content));
    }

    /// <summary>
    /// The claimant passed on at the branch (issue 633): the one of the two the record did not
    /// <see cref="Pick"/>, or null before the pick and in a campaign without a branch.
    /// </summary>
    public string? Passed(GameContent content) =>
        Pick is { } pick && content.Campaign.Maps.FirstOrDefault(m => m.Branch.Contains(pick)) is { } map
            ? map.Branch.First(id => id != pick)
            : null;

    /// <summary>
    /// Picks <paramref name="unitId"/> at the branch's camp (issue 633): one of the next map's two
    /// claimants, by id or by name in any case, who joins the company for it like a joiner (<see cref="Present"/>, at no less than
    /// <see cref="SeatLevel"/>, trained in their main weapon by <see cref="Trained"/>); the other rides home and never joins. Refused when the next map
    /// offers no branch, when the id is not one of its claimants, and once the pick is made: the
    /// choice is final.
    /// </summary>
    public ScreenResult PickClaimant(string unitId, GameContent content)
    {
        if (IsFinished(content) || NextMap(content).Branch.Count == 0)
        {
            return ScreenResult.Refused(this, "no claimant is offered at this camp");
        }

        var branch = NextMap(content).Branch;
        if (Pick is { } made)
        {
            return ScreenResult.Refused(this, $"the pick is made: {content.Unit(made).Name}, and it is final");
        }

        var named = branch.FirstOrDefault(id => string.Equals(id, unitId, StringComparison.OrdinalIgnoreCase) || string.Equals(content.Unit(id).Name, unitId, StringComparison.OrdinalIgnoreCase));
        if (named is null)
        {
            return ScreenResult.Refused(this, $"'{unitId}' is not a claimant; pick {string.Join(" or ", branch)}");
        }

        unitId = named;

        var passed = branch.First(id => id != unitId);
        var level = Math.Max(content.Unit(unitId).Level, SeatLevel(content));
        var picked = this with { Pick = unitId };
        return new ScreenResult(picked, $"{content.Unit(unitId).Name} takes the seat at level {level}; {content.Unit(passed).Name} rides home", true);
    }

    /// <summary>
    /// A new campaign: the cast in roster order less every recruit who arrives on a map (issue 632),
    /// the starting purse, the first map, nobody benched. <paramref name="origin"/> (issue 681)
    /// sets the captain's card from the campaign's origins, and <paramref name="captain"/> the
    /// pronoun text uses for the captain; either left null keeps the cast file's.
    /// </summary>
    public static CampaignRecord Start(GameContent content, ulong seed, string difficulty = NormalDifficulty, bool permadeath = true, string? origin = null, Pronoun? captain = null)
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

        var chosen = origin is null ? null
            : content.Campaign.Origin(origin) ?? throw new ArgumentException(
                content.Campaign.Origins.Count == 0
                    ? $"the campaign offers no origins, so '{origin}' cannot be chosen"
                    : $"the campaign has no origin '{origin}'; it offers {string.Join(", ", content.Campaign.Origins.Select(o => o.Id))}");
        var captainId = content.Cast[0].Id;
        var roster = ArrivedBefore(content, 0).Select(u => u.Id != captainId ? u : Captain(u, chosen, captain));
        return new CampaignRecord(ValueList<Unit>.From(roster), ValueList<string>.Empty, content.Campaign.StartingPurse, 0, seed, difficulty, ValueList<string>.Empty)
        {
            Permadeath = permadeath,
            Origin = chosen?.Id,
        };
    }

    /// <summary>The cast's captain as the start's choices make them (issue 681).</summary>
    private static Unit Captain(Unit cast, CaptainOrigin? origin, Pronoun? pronoun)
    {
        var captain = origin is null ? cast : origin.Apply(cast);
        return pronoun is null ? captain : captain with { Pronoun = pronoun };
    }

    /// <summary>
    /// A new campaign that opens on <paramref name="mapId"/> instead of the first map (issue 360):
    /// the cast in roster order less the recruits who arrive on it or later (issue 632), the
    /// starting purse, nobody benched, the maps before it skipped. A
    /// map is tuned on its own named roster, so the beta can open on the first tuned map. The
    /// campaign seed is shifted back by the map's index so that its battle plays on
    /// <paramref name="seed"/> itself, as <see cref="BattleSeed"/> promises for the opening map:
    /// <c>play &lt;map&gt; --seed N</c> reproduces its rolls. Refuses a map the campaign does not list.
    /// With <paramref name="pick"/> (issue 633) a campaign opening after the branch has made it: the
    /// pick is recorded and the passed claimant is off the roster, so a map that brings them back can
    /// be played from its own camp; refused when the branch does not come before the map or does not
    /// offer that claimant. The pick carries the seat's rank floor (<see cref="Trained"/>, issue 1130)
    /// but, like every member here, their cast card's level: a map opened this way is fought on its
    /// own named roster, and <c>--level</c> raises everyone alike.
    /// </summary>
    public static CampaignRecord StartAt(GameContent content, ulong seed, string mapId, string difficulty = NormalDifficulty, bool permadeath = true, string? origin = null, Pronoun? captain = null, string? pick = null)
    {
        var start = Start(content, seed, difficulty, permadeath, origin, captain);
        var index = content.Campaign.Maps.ToList().FindIndex(m => m.MapId == mapId);
        if (index < 0)
        {
            throw new ArgumentException($"the campaign has no map '{mapId}'; it lists {string.Join(", ", content.Campaign.Maps.Select(m => m.MapId))}");
        }

        var captainId = content.Cast[0].Id;
        var startCaptain = start.Roster.Single(u => u.Id == captainId);
        var roster = ArrivedBefore(content, index).Select(u => u.Id == captainId ? startCaptain : u);
        if (pick is not null)
        {
            var branchAt = content.Campaign.Maps.ToList().FindIndex(m => m.Branch.Contains(pick));
            if (branchAt < 0 || branchAt >= index)
            {
                throw new ArgumentException($"'{pick}' is not a claimant offered before {mapId}");
            }

            var passed = content.Campaign.Maps[branchAt].Branch.First(id => id != pick);
            roster = roster.Where(u => u.Id != passed);
            roster = roster.Select(u => u.Id == pick ? Trained(u, content) : u);
        }

        var rosterList = roster.ToList();
        var met = content.Campaign.Maps.Take(index).SelectMany(m => m.Meets).Where(id => rosterList.Any(u => u.Id == id));
        return start with { Roster = ValueList<Unit>.From(rosterList), MapIndex = index, Seed = unchecked(seed - (ulong)index), Pick = pick, Met = ValueList<string>.From(met) };
    }

    /// <summary>
    /// The cast in roster order less every recruit who arrives on map <paramref name="index"/> or
    /// later (issue 632): the roster a campaign opening on that map starts with.
    /// </summary>
    private static ValueList<Unit> ArrivedBefore(GameContent content, int index) =>
        ValueList<Unit>.From(content.Cast.Where(u => content.Campaign.ArrivalIndex(u.Id) < index).Select(u => Kitted(u, content)));

    /// <summary>
    /// <paramref name="unit"/> as the campaign first fields them (issue 635, round 266): every
    /// heirloom bound to them (<see cref="Weapon.Heirloom"/>) at full uses after their cast pack, so
    /// the cast's own weapon stays in front and the heirloom is swung only by choice. The cast file
    /// is unchanged, so a battle outside the campaign carries no heirloom. A weapon the campaign
    /// issues them (<see cref="CampaignRules.Issues"/>, issue 804) goes in front at full uses beside
    /// their cast pack (issue 851, round 276): Keziah joins with Kinsbane ahead of her iron axe, so
    /// carrying the scythe is the cost and each combat is feed or survive. A full pack drops its last
    /// stack to make room.
    /// </summary>
    public static Unit Kitted(Unit unit, GameContent content)
    {
        var inventory = unit.Inventory;
        if (content.Campaign.IssuedTo(unit.Id) is { } issuedId && !inventory.Items.Any(s => s.ItemId == issuedId))
        {
            var issued = content.Weapon(issuedId);
            var items = inventory.Items.ToList();
            if (items.Count >= Inventory.Capacity)
            {
                items.RemoveAt(items.Count - 1);
            }

            items.Insert(0, new ItemStack(issued.Id, issued.Durability));

            inventory = new Inventory(ValueList<ItemStack>.From(items));
        }

        foreach (var heirloom in content.Weapons.Values.Where(w => w.Heirloom is not null && w.BoundTo == unit.Id).OrderBy(w => w.Id, StringComparer.Ordinal))
        {
            if (!inventory.IsFull && !inventory.Items.Any(s => s.ItemId == heirloom.Id))
            {
                inventory = inventory.Add(new ItemStack(heirloom.Id, heirloom.Durability));
            }
        }

        var drake = unit.Drake ?? (content.Campaign.Drake is { } rules && rules.Member == unit.Id ? new DrakeState(DrakeStage.HalfGrown, 0) : null);
        return unit with { Inventory = inventory, Drake = drake };
    }

    /// <summary>
    /// The passed claimant as they come back (issue 633, round 263): <see cref="Kitted"/>, with a
    /// hungering weapon fed to the fourth tooth's count (<see cref="Kinsbane.FedFor"/>), so a passed
    /// Keziah returns one tooth short of waking and the player who turns her grows the last; a drake
    /// comes back Grown at least (issue 805, STORY draft 6: Hask fed it better than the church did).
    /// </summary>
    public static Unit Returning(Unit unit, GameContent content)
    {
        var kitted = Kitted(unit, content);
        var items = kitted.Inventory.Items.Select(s => content.Weapons.TryGetValue(s.ItemId, out var w) && w.Hungers ? s with { Fed = Kinsbane.FedFor(Kinsbane.MtCap - 1) } : s);
        var drake = kitted.Drake is { } d && d.Stage < DrakeStage.Grown ? d with { Stage = DrakeStage.Grown } : kitted.Drake;
        return kitted with { Inventory = new Inventory(ValueList<ItemStack>.From(items)), Drake = drake };
    }

    /// <summary>
    /// The roster with the next map's arrivals and joiners joined (issues 632 and 763, DESIGN section 14),
    /// each at no less than <see cref="JoinLevel"/> (the picked claimant at <see cref="SeatLevel"/>, <see cref="Trained"/>),
    /// every unit in cast order: who the next battle may
    /// deploy and who comes back from it, deployed or not. Equal to <see cref="Roster"/> on a map nobody
    /// arrives on or joins at, and once the campaign is finished.
    /// </summary>
    public ValueList<Unit> Present(GameContent content)
    {
        if (IsFinished(content) || (NextMap(content).Arrives.Count == 0 && NextMap(content).Joins.Count == 0 && NextMap(content).Branch.Count == 0 && NextMap(content).Meets.Count == 0))
        {
            return Roster;
        }

        var level = JoinLevel(content);
        var seat = SeatLevel(content);
        var arrivals = Arriving(content).Take(Room(content)).Select(content.Unit)
            .Select(u => u.Id == Pick ? Trained(Kitted(u, content), content).ScaledTo(seat, content.Class(u.ClassId)) : Kitted(u, content).ScaledTo(level, content.Class(u.ClassId)))
            .ToList();
        var order = content.Cast.Select(u => u.Id).ToList();
        return ValueList<Unit>.From(Roster.Concat(arrivals).OrderBy(u => order.IndexOf(u.Id) is var at && at < 0 ? int.MaxValue : at));
    }

    /// <summary>
    /// The level a recruit joining now joins at, at least (issue 763, rounds 228 and 234): the living
    /// company's median level, the lower middle's and upper middle's mean rounded down on an even count.
    /// A joiner below it is raised to it on the average growth <c>play --level</c> uses
    /// (<see cref="Unit.ScaledTo"/>), never lowered. The rule is join-time, never deploy-time: a recruit
    /// left on the bench is never raised by it.
    /// </summary>
    public int JoinLevel(GameContent content)
    {
        var levels = Roster.Select(u => u.Level).Order().ToList();
        if (levels.Count == 0)
        {
            return Unit.MinLevel;
        }

        var mid = levels.Count / 2;
        return levels.Count % 2 == 1 ? levels[mid] : (levels[mid - 1] + levels[mid]) / 2;
    }

    /// <summary>
    /// The levy floor for the next map (issue 1164, round 391): its number, from 1, less
    /// <see cref="CampaignRules.LevyFloor"/>; <see cref="Unit.MinLevel"/> when the campaign keeps no
    /// floor or is finished.
    /// </summary>
    public int LevyFloorLevel(GameContent content) =>
        content.Campaign.LevyFloor <= 0 || IsFinished(content)
            ? Unit.MinLevel
            : Math.Clamp(MapIndex + 1 - content.Campaign.LevyFloor, Unit.MinLevel, Unit.MaxLevel);

    /// <summary>
    /// Whether <paramref name="unitId"/> is of the levy (issue 1164): a cast member on the roster from
    /// the first map who is not the captain, so no map brings them in by arriving, joining, a branch
    /// or a meeting.
    /// </summary>
    public static bool IsLevy(string unitId, GameContent content) =>
        content.Cast.Count > 0 && unitId != content.Cast[0].Id && content.Cast.Any(u => u.Id == unitId) && content.Campaign.ArrivalIndex(unitId) < 0;

    /// <summary>
    /// The levy members the camp's floor raises before the next map (issue 1164, round 391), each with
    /// the level they stand at and the floor, in roster order: every living levy member below
    /// <see cref="LevyFloorLevel"/>, benched and wounded included. A unit at or above it is never
    /// listed, so a fed unit keeps its distance above the floor. <c>Exp</c> is the EXP each carries
    /// through the drill (issue 1184, round 398).
    /// </summary>
    public IReadOnlyList<(string Id, int From, int To, int Exp)> Drills(GameContent content)
    {
        var floor = LevyFloorLevel(content);
        return Roster.Where(u => u.Level < floor && IsLevy(u.Id, content)).Select(u => (u.Id, u.Level, floor, u.Exp)).ToList();
    }

    /// <summary>
    /// The record with the levy drilled to the floor (issue 1164, round 391): each unit
    /// <see cref="Drills"/> names is raised on the average growth (<see cref="Unit.AtLevel"/>) and
    /// keeps its EXP (issue 1184, round 398: what a unit earned is never erased silently; EXP is
    /// under 100, so nothing overflows); weapon ranks are untouched, so rank stays the part that
    /// measures use. The record itself when nobody is below the floor.
    /// </summary>
    public CampaignRecord Drill(GameContent content)
    {
        var drills = Drills(content);
        if (drills.Count == 0)
        {
            return this;
        }

        var floor = LevyFloorLevel(content);
        var raised = Roster.Select(u => drills.Any(d => d.Id == u.Id) ? u.AtLevel(floor, content.Class(u.ClassId)) : u);
        return this with { Roster = ValueList<Unit>.From(raised) };
    }

    /// <summary>
    /// The level the picked claimant joins at, at least (issue 1130, rounds 380 and 381): the branch
    /// map's authored <see cref="CampaignMap.SeatLevel"/>, or <see cref="JoinLevel"/> when that is
    /// higher. Nothing here reads the company's top level, so feeding one unit never raises the pick
    /// (Lotus). <see cref="JoinLevel"/> alone in a campaign whose branch authors no seat.
    /// </summary>
    public int SeatLevel(GameContent content) =>
        Math.Max(BranchMap(content)?.SeatLevel ?? Unit.MinLevel, JoinLevel(content));

    /// <summary>
    /// A claimant as the seat trains them (issue 1130): rank points in their main weapon, the type of
    /// the first weapon on their cast card, raised to the branch map's <see cref="CampaignMap.SeatRank"/>
    /// (30 is rank D), never lowered. The pick joins this way and the passed claimant returns this way,
    /// so neither faces the other a rank short. Unchanged for a unit no branch names.
    /// </summary>
    public static Unit Trained(Unit unit, GameContent content)
    {
        if (content.Campaign.Maps.FirstOrDefault(m => m.Branch.Contains(unit.Id)) is not { SeatRank: > 0 } map
            || content.Unit(unit.Id).Inventory.Items.Select(s => content.Weapons.GetValueOrDefault(s.ItemId)).FirstOrDefault(w => w is not null) is not { } main
            || unit.Skill.Points(main.Type) >= map.SeatRank)
        {
            return unit;
        }

        return unit with { Skill = unit.Skill.With(main.Type, map.SeatRank) };
    }

    /// <summary>The campaign map that offers the branch, or null in a campaign without one.</summary>
    private static CampaignMap? BranchMap(GameContent content) => content.Campaign.Maps.FirstOrDefault(m => m.Branch.Count > 0);

    /// <summary>
    /// The next map's arrivals and joiners (issue 763) whom <see cref="JoinLevel"/> raises, each with the
    /// level it joins at, in content order; the turned away are left out, since they never join.
    /// </summary>
    public IReadOnlyList<(string Id, int Level)> RaisedOnJoining(GameContent content)
    {
        var level = JoinLevel(content);
        var seat = SeatLevel(content);
        return Arriving(content).Take(Room(content)).Select(id => (Id: id, Level: id == Pick ? seat : level)).Where(j => content.Unit(j.Id).Level < j.Level).ToList();
    }

    /// <summary>The next map's arrivals and joiners not yet on the roster or fallen, in content order (issues 632, 763).</summary>
    private IEnumerable<string> Arriving(GameContent content) =>
        IsFinished(content)
            ? Enumerable.Empty<string>()
            : NextMap(content).Arrives.Concat(NextMap(content).Joins).Concat(NextMap(content).Branch.Where(id => id == Pick)).Concat(NextMap(content).Meets.Where(Met.Contains)).Where(id => Find(id) is null && !Fallen.Contains(id));

    /// <summary>
    /// The keep's beds (issue 687, DESIGN section 13.20): the beds it starts with and every bought
    /// room's, or null when the campaign counts no beds.
    /// </summary>
    public int? Beds(GameContent content)
    {
        var keep = content.Campaign.Keep;
        return keep.Beds == 0 ? null : keep.Beds + Rooms.Sum(id => keep.Room(id)?.Beds ?? 0);
    }

    /// <summary>
    /// The beds held (issue 687): one per member who has joined, living or fallen. A death never
    /// frees a bed (DECISIONS/0010, 0137).
    /// </summary>
    public int BedsTaken => Roster.Count + Fallen.Count;

    /// <summary>The beds no member holds, or null when the campaign counts no beds.</summary>
    public int? FreeBeds(GameContent content) => Beds(content) is { } beds ? Math.Max(0, beds - BedsTaken) : null;

    /// <summary>The company's living members (issue 689): the roster, the wounded included, the fallen not.</summary>
    public int Living => Roster.Count;

    /// <summary>
    /// How many more members may join now (issues 687 and 689): the free beds or the places left
    /// under <see cref="CompanyCap"/>, whichever is fewer.
    /// </summary>
    public int Room(GameContent content) => Math.Min(FreeBeds(content) ?? int.MaxValue, Math.Max(0, CompanyCap - Living));

    /// <summary>
    /// Whether the cap, not the beds, is what turns the next arrival away (issue 689): the places
    /// left under <see cref="CompanyCap"/> are no more than the free beds.
    /// </summary>
    public bool CompanyFull(GameContent content) => CompanyCap - Living <= (FreeBeds(content) ?? int.MaxValue);

    /// <summary>
    /// The next map's arrivals who will not join because no bed is free (issue 687) or the company
    /// is full (issue 689), in content order: met on the map, never on the roster, never fallen,
    /// and never benched in their place. <see cref="CompanyFull"/> says which limit bound.
    /// </summary>
    public IReadOnlyList<string> TurnedAway(GameContent content) =>
        Arriving(content).Skip(Room(content)).ToList();

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

    /// <summary>The captain on the roster (the cast's first, issue 681), or null when the content has no cast or the roster lacks them.</summary>
    public Unit? Captain(GameContent content) => content.Cast.Count == 0 ? null : Find(content.Cast[0].Id);

    /// <summary>
    /// Whether <paramref name="unit"/> is the captain, the cast's first, and the content gives the captain a
    /// ladder (issue 705, <see cref="UnitClass.Captain"/>): only then is the captain held to it. Content with
    /// no captain's class leaves the captain the general classes, as before the ladder.
    /// </summary>
    public static bool IsCaptain(Unit unit, GameContent content) =>
        content.Cast.Count > 0 && content.Cast[0].Id == unit.Id && content.Classes.Values.Any(c => c.Captain);

    /// <summary>
    /// The next battle: <paramref name="map"/> (the next map, as the caller loaded it) under the
    /// campaign's difficulty, with the roster less the bench filling its slots in roster order,
    /// so benching a unit lets the next recruit take its bare slot, which is a deployment and not
    /// gate 4's ablation. A named slot whose recruit has fallen, or was turned away for want of a bed (issue 687), stays empty. The map's arrivals
    /// join the roster for it (<see cref="Present"/>, issue 632). The battle knows which campaign map
    /// it is (<see cref="BattleState.CampaignMap"/>), for an heirloom's floor (issue 646). The map is
    /// fought as the campaign lists it (<see cref="CampaignMap.Prepare"/>, issue 704): at its curve's enemy
    /// level and with its template swaps, the difficulty's offset added after. A company thinned below
    /// the map's bare slots fights it short-handed, those slots empty (issue 795).
    /// </summary>
    public BattleState Begin(MapDefinition map, GameContent content, RollScheme scheme = RollScheme.TwoRollAverage)
    {
        map = Seated(NextMap(content).Prepare(map), content);
        if (UnderTheHill(content))
        {
            map = map.ArmSecretRace();
        }
        var played = content.Difficulties.Count > 0 ? map.Under(content.Difficulty(Difficulty)) : map;
        var roster = SeatOrder(map, content);
        var turnedAway = TurnedAway(content);
        var fallenNamed = map.Placements.OfType<PlayerPlacement>()
            .Where(p => p.Slot == PlayerSlot.NamedRecruit && p.RecruitId is { } id && (Fallen.Contains(id) || turnedAway.Contains(id)))
            .Select(p => p.RecruitId!)
            .ToList();
        roster.AddRange(fallenNamed.Select(content.Unit));
        var battle = BattleState.From(played, content, ValueList<Unit>.From(roster), BattleSeed, scheme, ValueList<string>.From(fallenNamed), shortHanded: true) with { CampaignMap = MapIndex + 1, Rapport = Rapport };
        if (ReturnLevel(content) is { } level && NextMap(content).Return is { } back && Passed(content) is { } passed)
        {
            var card = Trained(Returning(content.Unit(passed), content), content);
            battle = battle.WithReturned(card.ScaledTo(level, content.Class(card.ClassId)), back.At, back.Group, back.Behavior, Pick!, content);
        }

        return battle;
    }

    /// <summary>
    /// The units <see cref="Begin"/> seats, the bench left out, in the order they fill bare slots: roster
    /// order, but on the map the pick joins (issue 1357, round 458) the pick takes the place of the
    /// first unit benched, in bench order, who stands before her, so the first seat a bench frees is
    /// hers and nobody else's tile moves. A later bench, and every later map, follows roster order.
    /// </summary>
    private List<Unit> SeatOrder(MapDefinition map, GameContent content)
    {
        var present = Present(content);
        if (map.DeploysAll)
        {
            return present.ToList();
        }

        var order = present.Select(u => u.Id).ToList();
        var seated = present.Where(u => !Benched.Contains(u.Id)).ToList();
        if (Pick is not { } pick || Find(pick) is not null || !NextMap(content).Branch.Contains(pick) || seated.FindIndex(u => u.Id == pick) is var from && from < 0
            || Benched.FirstOrDefault(id => order.IndexOf(id) is var at && at >= 0 && at < order.IndexOf(pick)) is not { } freed)
        {
            return seated;
        }

        var unit = seated[from];
        seated.RemoveAt(from);
        var to = seated.FindIndex(u => order.IndexOf(u.Id) > order.IndexOf(freed));
        seated.Insert(to < 0 ? seated.Count : to, unit);
        return seated;
    }

    /// <summary>
    /// <paramref name="map"/>'s player slots as this record fills them: the claimant slots made bare
    /// (<see cref="ClaimantSlotsBare"/>), then a <c>seen_far:</c> unit (issue 973) who is with the
    /// company, not benched and not placed by name seated by name in the first bare slot (<see cref="Bench"/>
    /// refuses to bench it, so only a record benched before the header held it out), so the sighting cannot
    /// be dodged by leaving the unit home. A map with no bare slot left seats nobody.
    /// </summary>
    private MapDefinition Seated(MapDefinition map, GameContent content)
    {
        map = ClaimantSlotsBare(map, content);
        if (map.SeenFar is not { } far || map.DeploysAll || Present(content).All(u => u.Id != far.UnitId) || Benched.Contains(far.UnitId)
            || map.Placements.Any(p => p is PlayerPlacement { RecruitId: { } id } && id == far.UnitId))
        {
            return map;
        }

        var at = map.Placements.ToList().FindIndex(p => p is PlayerPlacement { Slot: PlayerSlot.AnyRecruit });
        return at < 0
            ? map
            : map with { Placements = map.Placements.SetItem(at, new PlayerPlacement(map.Placements[at].At, PlayerSlot.NamedRecruit, far.UnitId)) };
    }

    /// <summary>
    /// <paramref name="map"/> with every slot naming a branch claimant or a side character who is not
    /// with the company (issue 633: before the branch, or passed on at it; before the meeting, or never
    /// met, slice 3) turned into a bare slot: a unit who may be
    /// absent holds a roster slot, not a named one (DESIGN section 14), so the slot is filled in roster
    /// order like any bare slot and never throws for want of her. A claimant who has fallen keeps the
    /// fallen's empty named slot.
    /// </summary>
    private MapDefinition ClaimantSlotsBare(MapDefinition map, GameContent content)
    {
        var claimants = content.Campaign.Maps.SelectMany(m => m.Branch.Concat(m.Meets)).ToHashSet(StringComparer.Ordinal);
        if (claimants.Count == 0)
        {
            return map;
        }

        var present = Present(content).Select(u => u.Id).ToHashSet(StringComparer.Ordinal);
        bool Absent(Placement p) => p is PlayerPlacement { Slot: PlayerSlot.NamedRecruit, RecruitId: { } id } && claimants.Contains(id) && !present.Contains(id) && !Fallen.Contains(id);
        return map.Placements.Any(Absent)
            ? map with { Placements = ValueList<Placement>.From(map.Placements.Select(p => Absent(p) ? new PlayerPlacement(p.At, PlayerSlot.AnyRecruit) : p)) }
            : map;
    }

    /// <summary>
    /// The trade the camp prints when the pick and a side character compete for <paramref name="map"/>'s
    /// open places (issue 844): on a map whose campaign entry both brings the passed claimant back and
    /// offers a meeting, with the pick present and not placed by name, and a side character met here
    /// or still meetable (a bed free), the bare slots left after the map's named placements, and the
    /// contenders (the side characters, then the pick), when the pick and one side character outnumber
    /// those slots. Null otherwise, and on a <c>deploy: all</c> map. Which unit a slot goes to stays
    /// <see cref="Begin"/>'s fill.
    /// </summary>
    public (int Open, IReadOnlyList<string> Contenders)? ContestedPlaces(MapDefinition map, GameContent content)
    {
        if (IsFinished(content) || NextMap(content) is not { Return: not null, Meets.Count: > 0 } entry || map.DeploysAll
            || Pick is not { } pick || Present(content).All(u => u.Id != pick))
        {
            return null;
        }

        var sides = entry.Meets.FirstOrDefault(Met.Contains) is { } made
            ? new List<string> { made }
            : entry.Meets.Where(id => Meet(id, content).Accepted).ToList();
        var players = Seated(entry.Prepare(map), content).Placements.OfType<PlayerPlacement>().ToList();
        if (sides.Count == 0 || players.Any(p => p.RecruitId == pick))
        {
            return null;
        }

        var open = players.Count(p => p.Slot == PlayerSlot.AnyRecruit);
        return open < 2 ? (open, sides.Append(pick).ToList()) : null;
    }

    /// <summary>
    /// The record after a won battle: every deployed unit still standing comes back as the battle
    /// left it, with its spells refreshed and any consumable uses a <c>supplies</c> cap held back
    /// returned (the cap is what a unit brings into the battle; the rest stays in the wagon); a
    /// deployed unit missing from the board has fallen and leaves the roster, and on an Escape map
    /// a unit left behind by the captain's exit has fallen the same way (issue 269), since only
    /// the <see cref="BattleState.Survivors"/> come back; an undeployed unit
    /// is unchanged. The map's arrivals are on the roster from here, or fallen like anyone deployed
    /// (issue 632). The purse gains the map's reward, the bench is cleared, and the next map is
    /// the one after. A lost battle ends the campaign, so it has no record after it. With
    /// <see cref="Permadeath"/> off a unit that fell comes back as it began the battle, wounded
    /// (<see cref="Wound.Inflict"/>); every other wound on the roster counts this main map down
    /// (<see cref="Wound.Tick"/>), deployed or not. A bound enemy killed on the map is recorded
    /// (<see cref="FreedUnitFell"/>, issue 750). The camp then drills the levy
    /// (<see cref="Drill"/>, issue 1164); <see cref="Fought"/> is the record before it.
    /// </summary>
    public CampaignRecord AfterBattle(BattleState end, GameContent content) => Fought(end, content).Drill(content);

    /// <summary>
    /// <see cref="AfterBattle"/> before the camp's drill (issue 1164): the console reads
    /// <see cref="Drills"/> on it to print who the floor raises.
    /// </summary>
    public CampaignRecord Fought(BattleState end, GameContent content)
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
        var gone = new List<Unit>();
        foreach (var unit in Present(content))
        {
            if (!deployed.TryGetValue(unit.Id, out var started))
            {
                roster.Add(Wound.Tick(unit));
            }
            else if (standing.TryGetValue(unit.Id, out var after))
            {
                roster.Add(Flew(Wound.Tick(BattleState.RefreshSpells(ReturnWithheld(unit, started, after, content), content)), content));
            }
            else if (!Permadeath)
            {
                roster.Add(Wound.Inflict(unit, content.Class(unit.ClassId)));
            }
            else
            {
                fallen.Add(unit.Id);
                gone.Add(unit);
            }
        }

        var won = this with
        {
            Roster = ValueList<Unit>.From(roster),
            Fallen = ValueList<string>.From(fallen),
            FellOn = FellOnAdd(fallen.Skip(Fallen.Count), end.Map.Name),
            DrakeFlew = DrakeFlewAfter(gone),
            Purse = Purse + NextMap(content).Reward,
            MapIndex = MapIndex + 1,
            Benched = ValueList<string>.Empty,
            TrialsTried = ValueList<TrialAttempt>.Empty,
            QuestsTried = ValueList<string>.Empty,
            Duties = ValueList<UnitDuty>.Empty,
            QuestsSeen = SeenAfterCamp(content),
            Wagon = ValueList<string>.From(Wagon.Concat(end.Wagon)),
            FreedUnitFell = FreedUnitFell || end.Bond == BondFate.Fell,
            Coma = ValueList<string>.From(Coma.Concat(end.Coma)),
            Rapport = end.Rapport,
        };
        return end.Return is { } bond ? won.AfterReturn(bond, opening, end, content) : won;
    }

    /// <summary>
    /// The won record with the returned claimant's fate written (issue 633): turned by the pick, they
    /// join the company as they took the field, at full uses, if a bed is free and the company has room
    /// (<see cref="Room"/>, counted after the map's fallen, since a death never frees a bed), else they
    /// are turned away; spared by the captain, they ride on; killed, they fell, and a drake under them
    /// flies (<see cref="DrakeFlew"/>, issue 805); left standing, they stood against the company to the end.
    /// </summary>
    private CampaignRecord AfterReturn(ReturnBond bond, BattleState opening, BattleState end, GameContent content)
    {
        var fate = end.ReturnGone switch
        {
            ReturnFate.Turned => Room(content) > 0 ? ClaimantFate.Turned : ClaimantFate.TurnedAway,
            ReturnFate.Spared => ClaimantFate.Spared,
            ReturnFate.Fell => ClaimantFate.Fell,
            _ => ClaimantFate.Stood,
        };
        if (fate == ClaimantFate.Fell)
        {
            return this with { Returned = fate, DrakeFlew = DrakeFlewAfter(opening.Find(bond.UnitId) is { } fell ? new[] { fell.Unit } : Array.Empty<Unit>()) };
        }

        if (fate != ClaimantFate.Turned)
        {
            return this with { Returned = fate };
        }

        var order = content.Cast.Select(u => u.Id).ToList();
        var joined = opening.Find(bond.UnitId)!.Unit;
        var roster = Roster.Append(joined).OrderBy(u => order.IndexOf(u.Id) is var at && at < 0 ? int.MaxValue : at);
        return this with { Returned = fate, Roster = ValueList<Unit>.From(roster) };
    }

    /// <summary>
    /// <paramref name="unit"/>, who stood at the end of a won main map, with one more map flown on its
    /// drake and the stage read again (issue 805); a unit without a drake unchanged.
    /// </summary>
    private Unit Flew(Unit unit, GameContent content) =>
        unit.Drake is { } drake && content.Campaign.Drake is { } rules
            ? rules.Grow(unit with { Drake = drake with { Flown = drake.Flown + 1 } }, WonQuestIds)
            : unit;

    /// <summary><see cref="FellOn"/> with each of <paramref name="ids"/> marked as fallen on <paramref name="mapName"/>.</summary>
    private ValueList<FellOn> FellOnAdd(IEnumerable<string> ids, string mapName) =>
        ValueList<FellOn>.From(FellOn.Concat(ids.Select(id => new FellOn(id, mapName))));

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
    /// next map's shop does not stock it, the unit is not on the roster, its class cannot wield the
    /// weapon's type the way the weapon is used (naming the type and the class's ranks; issue 768),
    /// its five slots are full, or the purse holds less than the price, naming the price and the
    /// balance. A weapon whose rank the unit has not reached is sold with a note naming both ranks,
    /// since a rank is earned. The refusal reads the class the unit holds now: certification and
    /// promotion happen on the same camp screen, so a change of class can always come first.
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
        var unitClass = content.Class(unit.ClassId);
        if (weapon is not null && !ClassWields(unitClass, weapon))
        {
            return ScreenResult.Refused(this, $"{unit.Id} cannot wield {name} ({weapon.Type.Label()}); ranks: {RanksText(unit, unitClass)}");
        }

        if (weapon is not null && MagicSchoolExtensions.SchoolShort(unit, unitClass, weapon) is { } school)
        {
            return ScreenResult.Refused(this, $"{unit.Id} cannot wield {name}: it {school}");
        }

        if (unit.Inventory.IsFull)
        {
            return ScreenResult.Refused(this, $"{unit.Id} carries {Inventory.Capacity} items already");
        }

        if (Purse < price)
        {
            return ScreenResult.Refused(this, $"{name} costs {price} and the purse holds {Purse}");
        }

        var bought = unit with { Inventory = unit.Inventory.Add(new ItemStack(itemId, uses)) };
        var shortRank = weapon is not null && unit.Skill.Rank(weapon.Type) < weapon.Rank
            ? $"; needs {weapon.Type.Label()} {weapon.Rank}, has {unit.Skill.Rank(weapon.Type)}"
            : "";
        return new ScreenResult(Replace(bought) with { Purse = Purse - price }, $"{unit.Id} buys {name} for {price}; the purse holds {Purse - price}{shortRank}", true);
    }

    /// <summary>Whether <paramref name="unitClass"/> wields <paramref name="weapon"/>'s type the way the weapon is used, rank aside (<see cref="Unit.CanWield"/>'s other halves).</summary>
    private static bool ClassWields(UnitClass unitClass, Weapon weapon) =>
        weapon.Heals ? unitClass.CanHealWith(weapon.Type) : unitClass.CanStrikeWith(weapon.Type);

    /// <summary>The unit's rank in each type its class uses, in class order: <c>sword C, lore E</c>.</summary>
    private static string RanksText(Unit unit, UnitClass unitClass) =>
        string.Join(", ", unitClass.Weapons.Select(t => $"{t.Label()} {unit.Skill.Rank(t)}"));

    /// <summary>
    /// Repairs the weapon in <paramref name="slot"/> (0-based) of <paramref name="unitId"/> to its
    /// full durability at <see cref="CampaignRules.RepairPricePerUse"/> for every missing use, a
    /// broken weapon included, since repair is what makes broken a state and not a slot (section 5).
    /// A glass weapon pays a quarter of the rate (issue 1403). Refused for an empty slot, a consumable, a spell,
    /// a weapon without a price, a weapon already at full uses, or a purse short of the cost.
    /// </summary>
    public ScreenResult Repair(string unitId, int slot, GameContent content)
    {
        if (Find(unitId) is not { } unit)
        {
            return ScreenResult.Refused(this, $"no unit '{unitId}' on the roster");
        }

        if (slot < 0 || slot >= unit.Inventory.Count)
        {
            return ScreenResult.Refused(this, $"{unit.Id} has no slot {slot + 1}; {Referent.For(content, unit).Subject} {Referent.For(content, unit).Verb("carries", "carry")} {unit.Inventory.Count}");
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
    /// Discards the stack in <paramref name="slot"/> (0-based) of <paramref name="unitId"/> at the
    /// camp (issue 635), so a pack of five can take a quest's payout. Nothing is refunded. Refused
    /// for a unit not on the roster, an empty slot, or a signature item, which is bound to its
    /// owner and leaves only with them.
    /// </summary>
    public ScreenResult Drop(string unitId, int slot, GameContent content)
    {
        if (Find(unitId) is not { } unit)
        {
            return ScreenResult.Refused(this, $"no unit '{unitId}' on the roster");
        }

        if (slot < 0 || slot >= unit.Inventory.Count)
        {
            return ScreenResult.Refused(this, $"{unit.Id} has no item in slot {slot + 1}");
        }

        var stack = unit.Inventory.Items[slot];
        if (content.Weapons.TryGetValue(stack.ItemId, out var weapon) && weapon.BoundTo is not null)
        {
            return ScreenResult.Refused(this, $"{weapon.Name} is bound to {weapon.BoundTo} and is never dropped");
        }

        var dropped = unit with { Inventory = unit.Inventory.RemoveAt(slot) };
        return new ScreenResult(Replace(dropped), $"{unit.Id} drops {content.ItemName(stack.ItemId)}", true);
    }

    /// <summary>
    /// Reads the primer in <paramref name="slot"/> (0-based) of <paramref name="unitId"/> at the camp
    /// (issue 1246): the unit learns the school it teaches (<see cref="Unit.Learned"/>) for the rest of
    /// the campaign, and the primer is spent. Refused for a unit not on the roster, an empty slot, an
    /// item that is not a primer, a class that does not wield Lore, and a school the unit already
    /// reaches, each with its reason. Earth is never taught; the loader refuses such a primer.
    /// </summary>
    public ScreenResult Read(string unitId, int slot, GameContent content)
    {
        if (Find(unitId) is not { } unit)
        {
            return ScreenResult.Refused(this, $"no unit '{unitId}' on the roster");
        }

        if (slot < 0 || slot >= unit.Inventory.Count)
        {
            return ScreenResult.Refused(this, $"{unit.Id} has no item in slot {slot + 1}");
        }

        var stack = unit.Inventory.Items[slot];
        if (!content.Items.TryGetValue(stack.ItemId, out var item) || item.Teaches is not { } school)
        {
            return ScreenResult.Refused(this, $"{content.ItemName(stack.ItemId)} is not a primer; only a primer is read");
        }

        var unitClass = content.Class(unit.ClassId);
        if (!unitClass.CanUse(WeaponType.Reason))
        {
            return ScreenResult.Refused(this, $"{unit.Id} cannot learn from {item.Name}: a {unitClass.Name} does not wield Lore");
        }

        if (unit.Reaches(school, unitClass))
        {
            return ScreenResult.Refused(this, $"{unit.Id} already reaches the {school.Label()} school");
        }

        var learned = unit with { Inventory = unit.Inventory.RemoveAt(slot), Learned = unit.Learned.Add(school) };
        return new ScreenResult(Replace(learned), $"{unit.Id} reads {item.Name} and learns the {school.Label()} school; its rider fires only on Mag over the target's Res", true);
    }

    /// <summary>
    /// Moves wagon entry <paramref name="index"/> (from 0) into <paramref name="unitId"/>'s next
    /// free slot at full uses (issue 679). Refused when the wagon has no such entry, the unit is not
    /// on the roster, or its pack is full.
    /// </summary>
    public ScreenResult TakeFromWagon(string unitId, int index, GameContent content)
    {
        if (index < 0 || index >= Wagon.Count)
        {
            return ScreenResult.Refused(this, Wagon.Count == 0 ? "the wagon is empty" : $"the wagon has no entry {index + 1}; it holds {Wagon.Count}");
        }

        if (Find(unitId) is not { } unit)
        {
            return ScreenResult.Refused(this, $"no unit '{unitId}' on the roster");
        }

        if (unit.Inventory.Count >= Inventory.Capacity)
        {
            return ScreenResult.Refused(this, $"{unit.Id} has no free slot");
        }

        var id = Wagon[index];
        var uses = content.Weapons.TryGetValue(id, out var weapon) ? weapon.Durability : content.Item(id).Uses;
        var taken = unit with { Inventory = unit.Inventory.Add(new ItemStack(id, uses)) };
        return new ScreenResult(Replace(taken) with { Wagon = Wagon.RemoveAt(index) }, $"{unit.Id} takes {content.ItemName(id)} from the wagon", true);
    }

    /// <summary>
    /// Certifies <paramref name="unitId"/> into <paramref name="classId"/> through
    /// <see cref="Certifications.Check(Unit, UnitClass, UnitClass)"/> (issue 72), paying the seal, <see cref="CampaignRules.SealFor"/>,
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

        var refusals = Certifications.Check(unit, target, content.Class(unit.ClassId), IsCaptain(unit, content), WonQuestIds);
        if (refusals.Count > 0)
        {
            return ScreenResult.Refused(this, $"{unit.Id} cannot be promoted to {target.Name}: {string.Join("; ", refusals.Select(r => r.Text))}");
        }

        var price = content.Campaign.SealFor(target);
        if (Purse < price)
        {
            return ScreenResult.Refused(this, $"a seal costs {price} and the purse holds {Purse}");
        }

        var from = content.Class(unit.ClassId).Name;
        return new ScreenResult(
            Replace(Certifications.Certify(unit, target, IsCaptain(unit, content), WonQuestIds)) with { Purse = Purse - price },
            $"{unit.Id} certifies from {from} to {target.Name} for {price}; the purse holds {Purse - price}",
            true);
    }

    /// <summary>
    /// Why <paramref name="unitId"/> may not try the certification trial for <paramref name="classId"/>
    /// now (issue 252), or null when it may: the unit and the class must exist, the unit must not be
    /// in the class already, it must meet every requirement of <see cref="Certifications.Check(Unit, UnitClass, UnitClass)"/>
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

        var refusals = Certifications.Check(unit, target, content.Class(unit.ClassId), IsCaptain(unit, content), WonQuestIds);
        if (refusals.Count > 0)
        {
            return $"{unit.Id} cannot be promoted to {target.Name}: {string.Join("; ", refusals.Select(r => r.Text))}";
        }

        if (content.Campaign.TrialFor(classId) is null)
        {
            return $"{target.Name} has no trial; promote with a seal";
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
        trial.Certification?.ClassId == classId ? null : $"the trial map '{trial.Name}' does not promote to '{classId}'";

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
        var certified = Certifications.Certify(unit, target, IsCaptain(unit, content), WonQuestIds) with
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

    /// <summary>Side maps an interlude offers at most (DESIGN section 14); the overflow waits for the next one.</summary>
    public const int SideMapsPerInterlude = 2;

    /// <summary>
    /// The map index from which <paramref name="quest"/> opens (issue 635, DESIGN section 14), or
    /// null while it cannot: part 1 after the member's second map, so two maps after the one they
    /// arrive on (a member on the roster from the start arrives on map 0's eve, index -1); part 2
    /// two maps after part 1 was won, and never before it is. A quest that names the map it
    /// <see cref="CampaignQuest.OpensAfter"/> (issue 691) opens at the camp after that map instead.
    /// </summary>
    public int? QuestOpensAt(CampaignQuest quest, GameContent content)
    {
        if (quest.OpensAfter is { } after)
        {
            return content.Campaign.MapIndexOf(after) + 1;
        }

        if (quest.Part == 1)
        {
            return content.Campaign.ArrivalIndex(quest.MemberId) + 2;
        }

        var first = content.Campaign.Quests.FirstOrDefault(q => q.MemberId == quest.MemberId && q.Part == 1);
        return first is not null && QuestsWon.FirstOrDefault(w => w.QuestId == first.Id) is { } won ? won.MapIndex + 2 : null;
    }

    /// <summary>
    /// The side maps this interlude offers (issue 635, DESIGN section 14): every quest not yet won
    /// whose member stands on the roster and whose opening index has come, a quest never offered
    /// before ahead of one in <see cref="QuestsSeen"/> (issue 1129), then earliest opening first
    /// and then in file order, at most <see cref="SideMapsPerInterlude"/> less the ones already won
    /// here. A quest tried here and lost stays in its seat, closed until the next map, and reopens
    /// behind the fresh ones. Empty once the campaign is finished.
    /// </summary>
    public IReadOnlyList<CampaignQuest> QuestsOffered(GameContent content)
    {
        if (IsFinished(content))
        {
            return Array.Empty<CampaignQuest>();
        }

        var seats = SideMapsPerInterlude - QuestsWon.Count(w => w.MapIndex == MapIndex);
        return content.Campaign.Quests
            .Select((quest, order) => (quest, order, opens: QuestOpensAt(quest, content)))
            .Where(q => q.opens is { } at && at <= MapIndex && Find(q.quest.MemberId) is not null && !QuestsWon.Any(w => w.QuestId == q.quest.Id))
            .OrderBy(q => QuestsSeen.Contains(q.quest.Id)).ThenBy(q => q.opens).ThenBy(q => q.order)
            .Take(Math.Max(0, seats))
            .Select(q => q.quest)
            .ToList();
    }

    /// <summary>
    /// <see cref="QuestsSeen"/> once this camp is left (issue 1129): the quests it offered join it,
    /// those already in it keeping their place.
    /// </summary>
    private ValueList<string> SeenAfterCamp(GameContent content) =>
        ValueList<string>.From(QuestsSeen.Concat(QuestsOffered(content).Select(q => q.Id).Where(id => !QuestsSeen.Contains(id))));

    /// <summary>
    /// Why <paramref name="questId"/> may not be fought now with <paramref name="allyId"/> beside
    /// its member (issue 635), or null when it may. A quest that pays an item is refused while its
    /// member's pack is full, so the payout always has a slot. Otherwise the quest must exist and be offered this
    /// interlude, not tried since the last map, and the ally must be on the roster, neither the
    /// captain (who leads the main line and whose death would end the campaign on a side map)
    /// nor the member.
    /// </summary>
    public string? QuestRefusal(string questId, string allyId, GameContent content) =>
        QuestRefusal(questId, new[] { allyId }, content);

    /// <summary>
    /// <see cref="QuestRefusal(string, string, GameContent)"/> for a side map that takes more than
    /// one ally (issue 691): each of <paramref name="allyIds"/> is checked as the one ally is, and
    /// no ally is named twice, and nobody in the party has taken another duty at this camp (issue 1331). How many the board takes is the board's (<see cref="QuestAlliesRefusal"/>).
    /// </summary>
    public string? QuestRefusal(string questId, IReadOnlyList<string> allyIds, GameContent content)
    {
        if (content.Campaign.Quest(questId) is not { } quest)
        {
            return content.Campaign.Quests.Count == 0
                ? "the campaign has no side maps"
                : $"no side map '{questId}'; the campaign has {string.Join(", ", content.Campaign.Quests.Select(q => q.Id))}";
        }

        if (!QuestsOffered(content).Contains(quest))
        {
            return QuestsWon.Any(w => w.QuestId == questId)
                ? $"side map {questId} is won already"
                : Fallen.Contains(quest.MemberId)
                    ? $"side map {questId} closed when {quest.MemberId} fell"
                    : $"side map {questId} is not open before this map";
        }

        if (QuestsTried.Contains(questId))
        {
            return $"side map {questId} was fought since the last map; it opens again after the next one";
        }

        foreach (var allyId in allyIds)
        {
            if (Find(allyId) is null)
            {
                return $"no unit '{allyId}' on the roster";
            }

            if (allyId == Roster[0].Id)
            {
                return $"{allyId} is the captain and stays with the company; pick another ally";
            }

            if (allyId == quest.MemberId)
            {
                return $"{allyId} is the side map's own; pick an ally beside {quest.MemberId}";
            }
        }

        if (allyIds.GroupBy(id => id).FirstOrDefault(g => g.Count() > 1) is { } twice)
        {
            return $"{twice.Key} is named twice; pick each ally once";
        }

        if (allyIds.Prepend(quest.MemberId).Select(id => DutyRefusal(id, Duty.Quest)).FirstOrDefault(r => r is not null) is { } busy)
        {
            return busy;
        }

        return quest.Pays is { } item && Find(quest.MemberId) is { Inventory.IsFull: true }
            ? $"{quest.MemberId} carries {Inventory.Capacity} items and {questId} pays {content.ItemName(item)}; drop one first"
            : null;
    }

    /// <summary>
    /// Why <paramref name="map"/> cannot be played as a side map (issue 635), or null when it can:
    /// one <c>captain</c> slot (the member's), at least one bare <c>recruit</c> slot (one per
    /// ally; one on a member's quest, more on a board like the Cold Kitchen, issue 691), no recruit
    /// placed by name, and no certification header.
    /// </summary>
    public static string? QuestMapRefusal(MapDefinition map)
    {
        var players = map.Placements.OfType<PlayerPlacement>().ToList();
        if (map.Certification is not null)
        {
            return $"the side map '{map.Name}' is a certification trial";
        }

        if (players.Any(p => p.Slot == PlayerSlot.NamedRecruit))
        {
            return $"the side map '{map.Name}' places a recruit by name; its slots are the member's (captain) and the ally's (recruit)";
        }

        return QuestAllies(map) >= 1
            ? null
            : $"the side map '{map.Name}' needs a bare recruit slot, for the ally";
    }

    /// <summary>How many allies the side map <paramref name="map"/> takes beside its member (issue 691): its bare recruit slots.</summary>
    public static int QuestAllies(MapDefinition map) =>
        map.Placements.OfType<PlayerPlacement>().Count(p => p.Slot == PlayerSlot.AnyRecruit);

    /// <summary>Why <paramref name="allyIds"/> do not fill <paramref name="map"/>'s ally slots (issue 691), or null when they do, one each.</summary>
    public static string? QuestAlliesRefusal(MapDefinition map, IReadOnlyList<string> allyIds)
    {
        var slots = QuestAllies(map);
        return allyIds.Count == slots
            ? null
            : slots == 1 ? $"{map.Name} takes one ally, not {allyIds.Count}" : $"{map.Name} takes {slots} allies, not {allyIds.Count}";
    }

    /// <summary>
    /// The seed a side map before the next map runs on: past every map's and every trial's seed, in
    /// a block of one seed per map for each quest, the block its place in <c>campaign.json</c>'s
    /// <c>quests</c> (issue 635 slice 5). A quest added at the end of the file moves no other side
    /// map's seed, so a journaled side-map play keeps its rolls. It equals the earlier rule (member
    /// quests first, a hire's quest past them; issue 691) for the quests that rule seeded.
    /// </summary>
    public ulong QuestSeed(string questId, GameContent content)
    {
        var maps = content.Campaign.Maps.Count;
        var place = content.Campaign.Quests.Select(q => q.Id).ToList().IndexOf(questId);
        return unchecked(Seed + (ulong)(2 * maps) + (ulong)MapIndex + ((ulong)maps * (ulong)place));
    }

    /// <summary>
    /// The side map's battle (issue 635): <paramref name="map"/> as the caller loaded it, under the
    /// campaign's difficulty, the member in its captain slot and <paramref name="allyId"/> in its
    /// bare slot, on <see cref="QuestSeed"/>. The caller has checked <see cref="QuestRefusal(string, string, GameContent)"/>.
    /// </summary>
    public BattleState BeginQuest(MapDefinition map, string questId, string allyId, GameContent content, RollScheme scheme = RollScheme.TwoRollAverage) =>
        BeginQuest(map, questId, new[] { allyId }, content, scheme);

    /// <summary>
    /// <see cref="BeginQuest(MapDefinition, string, string, GameContent, RollScheme)"/> with
    /// <paramref name="allyIds"/> in the bare slots in file order (issue 691). The caller has
    /// checked <see cref="QuestRefusal(string, IReadOnlyList{string}, GameContent)"/> and <see cref="QuestAlliesRefusal"/>.
    /// </summary>
    public BattleState BeginQuest(MapDefinition map, string questId, IReadOnlyList<string> allyIds, GameContent content, RollScheme scheme = RollScheme.TwoRollAverage)
    {
        var quest = content.Campaign.Quest(questId) ?? throw new ArgumentException($"no side map '{questId}'");
        var played = content.Difficulties.Count > 0 ? map.Under(content.Difficulty(Difficulty)) : map;
        var party = allyIds.Prepend(quest.MemberId).Select(Find).ToList();
        if (party.Any(u => u is null))
        {
            throw new ArgumentException($"{quest.MemberId} and {string.Join(", ", allyIds)} must all be on the roster");
        }

        return BattleState.From(played, content, ValueList<Unit>.From(party!), QuestSeed(questId, content), scheme) with { Rapport = Rapport, SideMap = true };
    }

    /// <summary>
    /// The record after a decided side map (issue 635). The price is permadeath and nothing else
    /// (DESIGN section 14): whoever fell on it is fallen for good and leaves the roster, and
    /// whoever stands comes back as the battle left it (EXP, levels, uses), with spells refreshed.
    /// The attempt is recorded either way, and so is the party's duty (issue 1331); a win is recorded with the map it was won before,
    /// which times the member's next quest. A loss never ends the campaign; the side map opens
    /// again after the next map unless its member fell. No purse reward: the quest's payout is
    /// the member's own, and a won quest that <see cref="CampaignQuest.Pays"/> puts that signature
    /// item in the member's pack at full uses (<see cref="QuestRefusal(string, IReadOnlyList{string}, GameContent)"/> kept a slot free). With
    /// <see cref="Permadeath"/> off whoever fell comes back wounded instead (issue 664), the member
    /// included, so the side map opens again; a side map never counts a wound down.
    /// </summary>
    public ScreenResult AfterQuest(BattleState end, string questId, GameContent content)
    {
        var quest = content.Campaign.Quest(questId) ?? throw new ArgumentException($"no side map '{questId}'");
        if (!end.Outcome.IsOver)
        {
            throw new InvalidOperationException("the side map is not decided");
        }

        var opening = end.History.Count > 0 ? end.History[0] : end;
        var deployed = opening.UnitsOf(Side.Player).ToDictionary(u => u.Id, u => u.Unit, StringComparer.Ordinal);
        var standing = end.Survivors().ToDictionary(u => u.Id, u => u.Unit, StringComparer.Ordinal);
        var roster = new List<Unit>();
        var fallen = Fallen.ToList();
        var lost = new List<string>();
        var wounded = new List<string>();
        var gone = new List<Unit>();
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
            else if (!Permadeath)
            {
                roster.Add(Wound.Inflict(unit, content.Class(unit.ClassId)));
                wounded.Add(unit.Id);
            }
            else
            {
                fallen.Add(unit.Id);
                lost.Add(unit.Id);
                gone.Add(unit);
            }
        }

        var won = end.Outcome.Result == BattleResult.Won;
        var paid = "";
        if (won && quest.Pays is { } item && roster.FindIndex(u => u.Id == quest.MemberId) is var at and >= 0)
        {
            roster[at] = roster[at] with { Inventory = roster[at].Inventory.Add(new ItemStack(item, content.Weapon(item).Durability)) };
            paid = $"; {quest.MemberId} receives {content.ItemName(item)}";
        }

        if (won && quest.Wakes is { } heirloom && roster.FindIndex(u => u.Id == quest.MemberId) is var bearer and >= 0
            && roster[bearer].Inventory.Items.ToList().FindIndex(s => s.ItemId == heirloom) is var slot and >= 0)
        {
            var weapon = content.Weapon(heirloom);
            var before = roster[bearer].Inventory.Items[slot];
            var opened = Heirloom.OpenGate(weapon, before);
            roster[bearer] = roster[bearer] with { Inventory = roster[bearer].Inventory.Replace(slot, opened) };
            paid += opened.Stage > before.Stage
                ? $"; {content.ItemName(heirloom)} wakes in {content.Unit(quest.MemberId).Name}'s hands"
                : $"; {content.ItemName(heirloom)} will wake in {content.Unit(quest.MemberId).Name}'s hands";
        }

        if (won && quest.Names is { } named && roster.FindIndex(u => u.Id == quest.MemberId) is var holder and >= 0
            && roster[holder].Inventory.Items.ToList().FindIndex(s => s.ItemId == named) is var at2 and >= 0)
        {
            var stack = roster[holder].Inventory.Items[at2];
            roster[holder] = roster[holder] with { Inventory = roster[holder].Inventory.Replace(at2, stack with { Named = true }) };
            paid += $"; {content.ItemName(named)} is {Heirloom.Name(stack with { Named = true }, content)} now";
        }

        if (won && quest.Promotes is { } classId && roster.FindIndex(u => u.Id == quest.MemberId) is var promoted and >= 0)
        {
            roster[promoted] = Promote(roster[promoted], content.Class(classId));
            paid += $"; {quest.MemberId} becomes a {content.Class(classId).Name}";
        }

        if (won && content.Campaign.Drake is { } drakeRules && roster.FindIndex(u => u.Id == drakeRules.Member) is var rider and >= 0
            && roster[rider].Drake is { } drake)
        {
            roster[rider] = drakeRules.Grow(roster[rider], WonQuestIds.Append(questId).ToList());
            if (roster[rider].Drake!.Stage != drake.Stage)
            {
                paid += $"; {roster[rider].Name}'s drake is {Drake.Word(roster[rider].Drake!.Stage)} now";
            }
        }

        var common = won ? quest.Common : 0;
        var rare = won ? quest.Rare : 0;
        if (common + rare > 0)
        {
            paid += "; the stores take " + string.Join(" and ", new[] { (common, Material.Common), (rare, Material.Rare) }.Where(m => m.Item1 > 0).Select(m => $"{m.Item1} {Forge.Label(m.Item2)}"));
        }

        var oath = won && end.Map.Bond is not null && Kinsbane.Bearer(content) == quest.MemberId ? Oath.Of(end, quest.MemberId) : null;
        var record = this with
        {
            Roster = ValueList<Unit>.From(roster),
            Fallen = ValueList<string>.From(fallen),
            FellOn = FellOnAdd(lost, end.Map.Name),
            DrakeFlew = DrakeFlewAfter(gone),
            QuestsTried = QuestsTried.Add(questId),
            Duties = WithDuty(opening.UnitsOf(Side.Player).Select(u => u.Id).Where(id => !lost.Contains(id)), Duty.Quest),
            QuestsWon = won ? QuestsWon.Add(new QuestWon(questId, MapIndex)) : QuestsWon,
            CommonMaterial = CommonMaterial + common,
            RareMaterial = RareMaterial + rare,
            Wagon = won ? ValueList<string>.From(Wagon.Concat(end.Wagon)) : Wagon,
            Rapport = end.Rapport,
            KeziahOath = oath ?? KeziahOath,
        };
        var dead = lost.Count > 0 ? $"; fallen for good: {string.Join(", ", lost)}"
            : wounded.Count > 0 ? $"; fell and came back wounded: {string.Join(", ", wounded)}"
            : "; nobody fell";
        var line = won
            ? $"{quest.MemberId} wins {questId}{paid}{dead}"
            : lost.Contains(quest.MemberId)
                ? $"{quest.MemberId} falls on {questId}, which closes for good{dead}"
                : $"side map {questId} is lost: {Objective.Reason(end, content)}; it opens again after the next map{dead}";
        return new ScreenResult(record, line, true);
    }

    /// <summary>
    /// <paramref name="unit"/> in the hidden class <paramref name="target"/> (issue 691): the class
    /// changes with no seal and no check, and the class's mastery joins the unit's abilities at
    /// once, its points held full, so it is mastered on arrival. Level, EXP, stats, ranks and pack are its own.
    /// </summary>
    public static Unit Promote(Unit unit, UnitClass target)
    {
        var promoted = unit with { ClassId = target.Id };
        return target.Mastery is { } mastery && !promoted.Abilities.Contains(mastery)
            ? promoted with { Abilities = promoted.Abilities.Add(mastery), Mastery = promoted.Mastery.With(target.Id, target.MasteryPoints) }
            : promoted;
    }

    /// <summary>
    /// Benches <paramref name="unitId"/> from <paramref name="map"/>, the next map: its bare slot
    /// goes to the next recruit in roster order, or on the pick's join map to the pick (<see cref="SeatOrder"/>),
    /// and the line names who takes it (issue 1357). Anyone <see cref="Present"/> for it may be benched,
    /// so a joiner, the branch's pick or a side character met at this camp is benched like a member
    /// (issue 842) and still joins the roster after the map. Refused for the captain, the map's
    /// protected recruit, its <c>seen_far:</c> unit (issue 973), a recruit the map places by name, a unit not present, or one already benched.
    /// </summary>
    public ScreenResult Bench(string unitId, MapDefinition map, GameContent content)
    {
        if (Present(content).FirstOrDefault(u => u.Id == unitId) is not { } unit)
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

        if (map.SeenFar?.UnitId == unit.Id)
        {
            return ScreenResult.Refused(this, $"{map.Name} sees {unit.Name} from far off; {unit.Name} flies here");
        }

        if (map.DeploysAll)
        {
            return ScreenResult.Refused(this, $"the whole company fights on {map.Name}; nobody is benched");
        }

        if (map.Placements.OfType<PlayerPlacement>().FirstOrDefault(p => p.RecruitId == unit.Id) is { } named)
        {
            return ScreenResult.Refused(this, $"{map.Name} places {unit.Id} by name at {named.At}");
        }

        if (Benched.Contains(unit.Id))
        {
            return ScreenResult.Refused(this, $"{unit.Id} is already benched");
        }

        var benched = this with { Benched = Benched.Add(unit.Id) };
        return new ScreenResult(benched, $"{unit.Id} is benched from {map.Name}{SeatTaker(benched, map, content)}", true);
    }

    /// <summary>
    /// What <see cref="Bench"/> prints after the bench (issue 1357, round 458): who takes the seat it
    /// frees, by <see cref="Deployment"/> before and after, or that nobody does when the company is
    /// short; nothing when the map cannot be deployed to read it.
    /// </summary>
    private string SeatTaker(CampaignRecord benched, MapDefinition map, GameContent content)
    {
        try
        {
            var before = Deployment(map, content);
            var taker = benched.Deployment(map, content).FirstOrDefault(id => !before.Contains(id));
            return taker is null ? "; nobody takes the seat" : $"; {taker} takes the seat";
        }
        catch (ArgumentException)
        {
            return "";
        }
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
    /// The question a bare <c>march</c> asks before <paramref name="map"/> (issue 871), or null when it
    /// marches: on a <c>keziah_warning</c> map with the hungering weapon's bearer in the deployment and
    /// the question not yet answered on this map, Lotus's line (<see cref="Kinsbane.WarningLine"/>).
    /// <c>march sure</c> answers it (<see cref="ConfirmWarning"/>); benching the bearer removes it.
    /// </summary>
    public string? MarchWarning(MapDefinition map, GameContent content)
    {
        if (!map.KeziahWarning || WarningConfirmed == MapIndex || Kinsbane.Bearer(content) is not { } bearer
            || MarchRefusal(map, content) is not null || !Deployment(map, content).Contains(bearer))
        {
            return null;
        }

        return Kinsbane.WarningLine(Find(bearer)?.Name ?? bearer);
    }

    /// <summary>The record with this map's <see cref="MarchWarning"/> answered (issue 871): <c>march sure</c>.</summary>
    public CampaignRecord ConfirmWarning() => this with { WarningConfirmed = MapIndex };

    /// <summary>
    /// Why <c>march</c> cannot open <paramref name="map"/>, or null when it can (issue 795): the
    /// branch's pick not yet made (issue 633), else the reason <see cref="Begin"/> refuses the battle,
    /// so a march is refused as a camp line and never throws.
    /// </summary>
    public string? MarchRefusal(MapDefinition map, GameContent content)
    {
        if (!IsFinished(content) && NextMap(content).Branch is { Count: > 0 } branch && Pick is null)
        {
            return $"the seat is not filled; pick {string.Join(" or ", branch)} first";
        }

        try
        {
            Begin(map, content);
            return null;
        }
        catch (ArgumentException e)
        {
            return e.Message;
        }
    }

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
    /// This record with every keep work whose tile is off its edit's placement set dropped and the
    /// edit's price refunded into the purse (issue 1154, DECISIONS/0266): a save written before the
    /// menu moved still loads, and the player gets back what the moved work cost at today's price,
    /// since the record never stored the price paid. <see cref="KeepSettled"/> says what was dropped;
    /// a record with nothing off the menu is returned unchanged.
    /// </summary>
    public CampaignRecord SettleKeep(GameContent content)
    {
        var menu = content.Campaign.Keep;
        var kept = new List<KeepWork>();
        var dropped = new List<string>();
        var refund = 0;
        foreach (var work in Keep)
        {
            if (menu.Edit(work.EditId) is { } edit && !edit.At.Contains(work.At))
            {
                refund += edit.Price;
                dropped.Add($"{work} ({edit.Name} goes only on {string.Join(" ", edit.At)})");
            }
            else
            {
                kept.Add(work);
            }
        }

        if (dropped.Count == 0)
        {
            return this;
        }

        return this with
        {
            Keep = ValueList<KeepWork>.From(kept),
            Purse = Purse + refund,
            KeepSettled = $"The keep's menu moved since this save: dropped {string.Join("; ", dropped)}; {refund} refunded, the purse holds {Purse + refund}",
        };
    }

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

    /// <summary>
    /// Buys the room <paramref name="roomId"/> for the keep (issue 687, DESIGN section 13.20): open
    /// at every camp from map 1, unlike the edits, which wait for the raid. Refused for a campaign
    /// whose keep sells no rooms, a room it does not sell (named), a room already built its
    /// <see cref="KeepRoom.Max"/> times, a finished campaign, or a purse short of the price.
    /// </summary>
    public ScreenResult BuildRoom(string roomId, GameContent content)
    {
        var keep = content.Campaign.Keep;
        if (keep.Rooms.Count == 0)
        {
            return ScreenResult.Refused(this, "the keep has no rooms to build");
        }

        if (keep.Room(roomId) is not { } room)
        {
            return ScreenResult.Refused(this, $"the keep has no room '{roomId}'; it builds {string.Join(", ", keep.Rooms.Select(r => r.Id))}");
        }

        if (IsFinished(content))
        {
            return ScreenResult.Refused(this, "the campaign is finished");
        }

        if (room.After.Length > 0 && content.Campaign.Maps.ToList().FindIndex(m => m.MapId == room.After) is var after && after >= MapIndex)
        {
            return ScreenResult.Refused(this, $"{room.Name} opens once {room.After} is won");
        }

        if (room.Requires.Length > 0 && !Rooms.Contains(room.Requires))
        {
            return ScreenResult.Refused(this, $"{room.Name} needs the {keep.Room(room.Requires)?.Name ?? room.Requires} built first");
        }

        var built = Rooms.Count(id => id == room.Id);
        if (built >= room.Max)
        {
            return ScreenResult.Refused(this, $"{room.Name} is built {built} of {room.Max}");
        }

        if (Purse < room.Price)
        {
            return ScreenResult.Refused(this, $"{room.Name} costs {room.Price} and the purse holds {Purse}");
        }

        var bought = this with { Purse = Purse - room.Price, Rooms = Rooms.Add(room.Id) };
        var adds = room.Beds > 0 ? $"; beds: {bought.BedsTaken}/{bought.Beds(content)}" : room.Forge ? "; refine <unit> <slot> mt|hit works here" : "";
        if (room.Hires.Count > 0)
        {
            adds += $"; hire <id> takes {string.Join(", ", room.Hires)} for {keep.HirePrice} each";
        }

        return new ScreenResult(bought, $"{room.Name} built for {room.Price}, the purse holds {bought.Purse}{adds}", true);
    }

    /// <summary>
    /// The hires on the barracks' list now (issue 690), in content order: listed by a room that is
    /// built, and neither in the company nor fallen. Empty when no room that hires is built.
    /// </summary>
    public IReadOnlyList<KeepHire> HiresOffered(GameContent content)
    {
        var keep = content.Campaign.Keep;
        return keep.Hires
            .Where(h => keep.RoomHiring(h.Id) is { } room && Rooms.Contains(room.Id) && Find(h.Id) is null && !Fallen.Contains(h.Id))
            .ToList();
    }

    /// <summary>
    /// Why <paramref name="hireId"/> may not be hired now, or null when they may (issue 690): the
    /// keep must list the hire, the room that lists them must be built, they must be neither in
    /// the company nor fallen, the company must have room (<see cref="Room"/>: the cap of 12 or
    /// the beds, whichever binds, named as the camp names a turned-away arrival), and the purse
    /// must hold <see cref="KeepMenu.HirePrice"/>.
    /// </summary>
    public string? HireRefusal(string hireId, GameContent content)
    {
        var keep = content.Campaign.Keep;
        if (keep.Hires.Count == 0)
        {
            return "the keep has no barracks to hire from";
        }

        if (IsFinished(content))
        {
            return "the campaign is finished";
        }

        if (keep.Hire(hireId) is not { } hire)
        {
            return $"the barracks lists no '{hireId}'";
        }

        if (keep.RoomHiring(hire.Id) is { } room && !Rooms.Contains(room.Id))
        {
            return $"{hire.Id} is hired once the {room.Name} is built";
        }

        if (Find(hire.Id) is not null)
        {
            return $"{hire.Id} is already in the company";
        }

        if (Fallen.Contains(hire.Id))
        {
            return $"{hire.Id} has fallen";
        }

        if (Room(content) == 0)
        {
            return CompanyFull(content) ? $"company full ({CompanyCap}): {hire.Id} will not join" : $"no bed free: {hire.Id} will not join";
        }

        return Purse < keep.HirePrice ? $"a hire costs {keep.HirePrice} and the purse holds {Purse}" : null;
    }

    /// <summary>
    /// Hires <paramref name="hireId"/> at the barracks (issue 690): refused as
    /// <see cref="HireRefusal"/>, else the hire joins the end of the roster as
    /// <see cref="Barracks.Recruit"/> builds them at <see cref="Barracks.JoinLevel"/>, and the purse pays.
    /// </summary>
    public ScreenResult Hire(string hireId, GameContent content)
    {
        if (HireRefusal(hireId, content) is { } refusal)
        {
            return ScreenResult.Refused(this, refusal);
        }

        var keep = content.Campaign.Keep;
        var unit = Barracks.Recruit(keep.Hire(hireId)!, Barracks.JoinLevel(this), content);
        var hired = this with { Roster = Roster.Add(unit), Purse = Purse - keep.HirePrice };
        var beds = hired.Beds(content) is { } total ? $"; beds: {hired.BedsTaken}/{total}" : "";
        return new ScreenResult(hired, $"{unit.Id} joins as a {content.Class(unit.ClassId).Name} at L{unit.Level} for {keep.HirePrice}, the purse holds {hired.Purse}{beds}", true);
    }

    /// <summary>
    /// Refines the weapon in <paramref name="slot"/> (0-based) of <paramref name="unitId"/> one step
    /// at the forge (issue 647, DESIGN section 13.20): <paramref name="stat"/> <c>mt</c> adds
    /// <see cref="ForgeRules.Mt"/>, <c>hit</c> adds <see cref="ForgeRules.Hit"/>, for one material of
    /// the weapon's kind (<see cref="Forge.MaterialFor"/>) and <see cref="ForgeRules.Price"/> from
    /// the purse. Refused without a forge, for a unit or slot that is not there, an item, a weapon
    /// the forge never works (named), a glass weapon (issue 702), an heirloom short of its last stage (the smith's own line at
    /// rust), a weapon at its last step, a stat other than mt or hit, and a short store or purse.
    /// </summary>
    public ScreenResult Refine(string unitId, int slot, string stat, GameContent content)
    {
        var refined = RefineStep(unitId, slot, stat, content, content.Campaign.Forge.Price);
        return refined.Accepted ? refined with { Text = "Refine " + refined.Text } : refined;
    }

    /// <summary>
    /// One <see cref="Refine"/> step for <paramref name="price"/> gold: <see cref="ForgeRules.Price"/> at
    /// <c>refine</c>, none on the forge duty (<see cref="ForgeDuty"/>). The accepted text names the weapon
    /// and what the step took: <c>Iron Lance: Iron Lance +1, Acc 80, Power 8, for 1 common and 100; ...</c>.
    /// </summary>
    private ScreenResult RefineStep(string unitId, int slot, string stat, GameContent content, int price)
    {
        var rules = content.Campaign.Forge;
        if (!ForgeBuilt(content))
        {
            return ScreenResult.Refused(this, "the keep has no forge; build it first");
        }

        if (stat is not ("mt" or "hit"))
        {
            return ScreenResult.Refused(this, $"'{stat}' is not a step; Refine adds mt or hit, never weight");
        }

        if (Find(unitId) is not { } unit)
        {
            return ScreenResult.Refused(this, $"'{unitId}' is not on the roster");
        }

        if (slot < 0 || slot >= unit.Inventory.Count)
        {
            return ScreenResult.Refused(this, $"{unitId} has no item in slot {slot + 1}");
        }

        var stack = unit.Inventory.Items[slot];
        if (!content.Weapons.TryGetValue(stack.ItemId, out var weapon))
        {
            return ScreenResult.Refused(this, $"{content.Item(stack.ItemId).Name} is not a weapon; nothing to Refine");
        }

        var (material, refusal) = Forge.MaterialFor(weapon, content);
        if (material is not { } kind)
        {
            return ScreenResult.Refused(this, refusal!);
        }

        if (Heirloom.SmithRefuses(weapon, stack))
        {
            return ScreenResult.Refused(this, $"the smith: \"{Heirloom.SmithRefusal}\"");
        }

        if (weapon.Heirloom is { } ladder && stack.Stage < ladder.Turns.Count)
        {
            return ScreenResult.Refused(this, $"{weapon.Name} is not yet woken; the smith works it only then");
        }

        var max = Forge.MaxSteps(kind, rules);
        if (stack.Refines >= max)
        {
            return ScreenResult.Refused(this, $"{Forge.Name(weapon.Name, stack)} is Refined {stack.Refines} of {max}");
        }

        var held = kind == Material.Rare ? RareMaterial : CommonMaterial;
        var word = Forge.Label(kind);
        if (held < 1)
        {
            return ScreenResult.Refused(this, $"Refining {weapon.Name} takes 1 {word} and the stores hold none");
        }

        if (Purse < price)
        {
            return ScreenResult.Refused(this, $"Refining {weapon.Name} costs {price} and the purse holds {Purse}");
        }

        var refined = stat == "mt"
            ? stack with { RefineMt = stack.RefineMt + rules.Mt, Refines = stack.Refines + 1 }
            : stack with { RefineHit = stack.RefineHit + rules.Hit, Refines = stack.Refines + 1 };
        var after = Replace(unit with { Inventory = unit.Inventory.Replace(slot, refined) }) with
        {
            Purse = Purse - price,
            CommonMaterial = kind == Material.Common ? CommonMaterial - 1 : CommonMaterial,
            RareMaterial = kind == Material.Rare ? RareMaterial - 1 : RareMaterial,
        };
        var shaped = Forge.Shape(Heirloom.Shape(weapon, refined), refined);
        return new ScreenResult(
            after,
            $"{weapon.Name}: {Forge.Name(weapon.Name, refined)}, Acc {shaped.Hit}, Power {shaped.Mt}, for 1 {word} and {(price == 0 ? "no gold" : price)}; the purse holds {after.Purse}",
            true);
    }

    private CampaignRecord Replace(Unit unit) =>
        this with { Roster = ValueList<Unit>.From(Roster.Select(u => u.Id == unit.Id ? unit : u)) };
}
