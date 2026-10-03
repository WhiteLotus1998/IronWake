namespace Ironwake.Core;

/// <summary>
/// What happened when a command was applied. Events are the log: the CLI and every
/// renderer draw from these, never by diffing states (DESIGN.md section 2).
/// </summary>
public abstract record GameEvent;

public sealed record UnitMoved(string UnitId, Coord From, Coord To, ValueList<Coord> Path) : GameEvent;

/// <summary>A combat, strike by strike, with both sides' HP when it ended.</summary>
public sealed record CombatFought(
    string AttackerId, string TargetId, int Turn, Side Phase, ValueList<StrikeEvent> Strikes, int AttackerHpAfter, int TargetHpAfter) : GameEvent;

/// <summary>A unit's move was taken back (issue 676): it stood at <paramref name="From"/> and is back on <paramref name="To"/>, its start tile, unmoved.</summary>
public sealed record MoveUndone(string UnitId, Coord From, Coord To) : GameEvent;

public sealed record UnitDied(string UnitId, Side Side, Coord At) : GameEvent;

/// <summary>EXP earned from one combat (DESIGN.md section 6), with the total toward the next level after it.</summary>
public sealed record ExpGained(string UnitId, int Amount, int ExpAfter) : GameEvent;

/// <summary>A level gained: <see cref="Gains"/> holds 1 for each stat that rose.</summary>
public sealed record LeveledUp(string UnitId, int NewLevel, Stats Gains) : GameEvent;

/// <summary>A unit's rank points in a weapon type crossed a threshold (issue 67); points that cross none raise no event.</summary>
public sealed record RankRaised(string UnitId, WeaponType Type, WeaponRank Rank) : GameEvent;

/// <summary>A player unit mastered its class (issue 69): the class's mastery ability is now its own.</summary>
public sealed record MasteryEarned(string UnitId, string ClassId, string AbilityId) : GameEvent;

public sealed record UnitWaited(string UnitId, bool Braced = false) : GameEvent;

/// <summary>A fallen player unit's weapon stays on its tile on a <c>keepsakes: on</c> map (DESIGN.md 13.8).</summary>
public sealed record KeepsakeLeft(string FallenId, string ItemId, Coord At) : GameEvent;

/// <summary>A player unit recovered a fallen ally's weapon; it carries the fallen's name from now on (DESIGN.md 13.8).</summary>
public sealed record KeepsakeRecovered(string UnitId, string FallenId, string ItemId) : GameEvent;

/// <summary>
/// A player unit opened the chest at <paramref name="At"/> (issue 649): <paramref name="ItemIds"/>
/// went to its pack and <paramref name="Wagon"/>, what did not fit, to the wagon, each in file
/// order (issue 679).
/// </summary>
public sealed record ChestOpened(string UnitId, Coord At, ValueList<string> ItemIds, ValueList<string> Wagon) : GameEvent;

/// <summary>An enemy ended a move on a keepsake and took it; it carries the weapon until it dies (DESIGN.md 13.8, issue 295).</summary>
public sealed record KeepsakeTaken(string UnitId, string FallenId, string ItemId) : GameEvent;

/// <summary>
/// The battle ended with a keepsake nobody recovered (issue 295): lying at <paramref name="At"/>,
/// or carried off by the enemy <paramref name="CarrierId"/> when it is not null.
/// </summary>
public sealed record KeepsakeLost(string FallenId, string ItemId, Coord At, string? CarrierId) : GameEvent;

/// <summary>A player unit left the board through the exit it stood on (issue 269).</summary>
public sealed record UnitExited(string UnitId, Coord At) : GameEvent;

/// <summary>The captain's exit ended the battle with this player unit still on the board (issue 269); it counts as fallen.</summary>
public sealed record UnitLeftBehind(string UnitId, Coord At) : GameEvent;

/// <summary>A unit took its Canto (issue 71): <see cref="From"/> equal to <see cref="To"/> and an empty path is a Canto declined.</summary>
public sealed record Cantoed(string UnitId, Coord From, Coord To, ValueList<Coord> Path) : GameEvent;

/// <summary>A unit pushed another one tile, from <paramref name="From"/> to <paramref name="To"/> (DESIGN.md 13.12).</summary>
public sealed record Shoved(string UnitId, string TargetId, Coord From, Coord To) : GameEvent;

/// <summary>An enemy fell back to a healing tile instead of fighting (issue 33); it will not retreat again this battle.</summary>
public sealed record UnitRetreated(string UnitId, Coord From, Coord To) : GameEvent;

/// <summary>An enemy swore a grudge against the player unit that killed one of its group, on a <c>grudges: on</c> map (DESIGN.md 13.4, experiment).</summary>
/// <summary>A unit whose boss fell while it stood at or below half HP left the board (DESIGN.md 13.22, experiment).</summary>
public sealed record UnitBroke(string UnitId, Coord At, int Hp) : GameEvent;

/// <summary>The enemy bound to a boss by the map's <c>freed:</c> header left the board when he fell (issue 750): not a kill.</summary>
public sealed record UnitFreed(string UnitId, Coord At, int Hp) : GameEvent;

/// <summary>The messenger reached its road and left the board, not a kill (DESIGN.md 13.24, experiment); its events fire next.</summary>
public sealed record MessengerEscaped(string UnitId, Coord At) : GameEvent;

/// <summary>A front fell (issue 692): an enemy stands on one of its tiles. The map goes on; the front's <c>falls</c> events fire next.</summary>
public sealed record FrontFell(string Front) : GameEvent;

public sealed record GrudgeSworn(string UnitId, string AgainstId) : GameEvent;

/// <summary>
/// Two adjacent recruits gained rapport at the end of a player phase (issue 16): both rates,
/// and the pair's total after. <paramref name="OutOf"/> is the overwrite threshold when the
/// pair were rivals before the gain, so the line can print how far the cure has come, and
/// null for a pair of one region.
/// </summary>
public sealed record RapportGained(string A, string B, int Amount, int Total, int? OutOf = null) : GameEvent;

/// <summary>A rival pair's rapport reached the overwrite threshold; they are rivals no longer.</summary>
public sealed record RivalryEnded(string A, string B) : GameEvent;

/// <summary>
/// A support pair's rapport reached a new support tier at the end of a player phase (issue 77):
/// <paramref name="Tier"/> is the tier's name (C, B or A), the highest the pair's total now
/// reaches. One event per pair per phase, whatever the number of tiers crossed.
/// </summary>
public sealed record SupportReached(string A, string B, string Tier) : GameEvent;

public sealed record PhaseEnded(Side Side, int Turn) : GameEvent;

public sealed record PhaseBegan(Side Side, int Turn) : GameEvent;

/// <summary>Healing terrain at the start of the owner's phase (DESIGN.md section 4). Amount is what was actually gained.</summary>
public sealed record UnitHealed(string UnitId, int Amount, int HpAfter) : GameEvent;

/// <summary>Burning terrain at the start of the owner's phase (DESIGN.md 13.15, experiment). Amount is what was actually lost; fire never takes a unit below 1.</summary>
public sealed record UnitBurned(string UnitId, int Amount, int HpAfter) : GameEvent;

/// <summary>A drop's rock struck the unit standing under it (DESIGN.md 13.26, experiment). Amount is what was actually lost; the rock never takes a unit below 1.</summary>
public sealed record RockfallStruck(string UnitId, Coord At, int Amount, int HpAfter) : GameEvent;

/// <summary>At the start of its side's phase a unit pays an art that cost it this phase (issue 636): it begins moved and acted and can neither move nor act until its side's next phase.</summary>
public sealed record UnitRested(string UnitId) : GameEvent;

/// <summary>A windup weapon's attack raised a blow over <paramref name="At"/>, where <paramref name="TargetId"/> stood (DESIGN.md 13.16, experiment). No combat was fought.</summary>
public sealed record BlowRaised(string UnitId, string TargetId, Coord At) : GameEvent;

/// <summary>A raised blow landed at its wielder's phase start on the unit standing on its tile: a sure hit, no crit, no counter (DESIGN.md 13.16).</summary>
public sealed record BlowLanded(string UnitId, string TargetId, Coord At, int Damage, int TargetHpAfter) : GameEvent;

/// <summary>A raised blow fell on an empty tile at its wielder's phase start and harmed nobody (DESIGN.md 13.16).</summary>
public sealed record BlowFell(string UnitId, Coord At) : GameEvent;

/// <summary>A hit on the wielder broke its raised blow before it landed (DESIGN.md 13.16).</summary>
public sealed record BlowBroken(string UnitId, Coord At) : GameEvent;

/// <summary>
/// A unit took Watch (DESIGN.md 13.17) on its tile. <paramref name="PassedUpTargetId"/> and
/// <paramref name="PassedUpHit"/> name the best legal strike it gave up, displayed hit, when it had one (round 115).
/// <paramref name="Holds"/> is set on an <c>overwatch: hold</c> map (13.17b), where
/// <paramref name="HoldsInsteadOf"/> is the tile the move it gave up would have reached, null when no move was closer.
/// </summary>
public sealed record WatchTaken(string UnitId, Coord At, string? PassedUpTargetId = null, int? PassedUpHit = null, bool Holds = false, Coord? HoldsInsteadOf = null) : GameEvent;

/// <summary>A watch fired on the unit that ended a move in its ring, before that unit acts: one strike, no counter (DESIGN.md 13.17).</summary>
public sealed record WatchFired(string UnitId, string TargetId, Coord At, StrikeEvent Strike) : GameEvent;

/// <summary>
/// A watch held its shot at a unit that ended a move in its ring: Ottilie's ledger refused a shot
/// at <paramref name="Hit"/>, displayed, under 65 (DESIGN.md 13.18). The watch stays for the next arrival.
/// </summary>
public sealed record WatchHeld(string UnitId, string TargetId, Coord At, int Hit) : GameEvent;

/// <summary>A strike on a watching unit ended its watch unfired (DESIGN.md 13.17).</summary>
public sealed record WatchEnded(string UnitId) : GameEvent;

/// <summary>
/// A unit took Cover on an ally beside it (DESIGN.md 13.19): <paramref name="AllyLandsOn"/> is
/// where the ally lands if the swap fires, the coverer's tile. <paramref name="PassedUpTargetId"/>
/// and <paramref name="PassedUpHit"/> name the best legal strike it gave up, displayed hit, when it had one.
/// </summary>
public sealed record CoverTaken(string UnitId, string AllyId, Coord AllyLandsOn, string? PassedUpTargetId = null, int? PassedUpHit = null) : GameEvent;

/// <summary>
/// A cover fired (DESIGN.md 13.19): <paramref name="AttackerId"/>'s Attack aimed at
/// <paramref name="AllyId"/> swapped the two, the coverer now on <paramref name="At"/> and the ally
/// on <paramref name="AllyTo"/>. <paramref name="WouldHaveKilled"/> is whether the strike, every
/// hit landing and no crit, would have killed the ally where it stood; <paramref name="Counters"/>
/// is whether the coverer can answer from the ally's tile.
/// </summary>
public sealed record CoverFired(string UnitId, string AllyId, string AttackerId, Coord At, Coord AllyTo, bool WouldHaveKilled, bool Counters) : GameEvent;

public sealed record Recalled(int ToIndex, int ChargesLeft) : GameEvent;

/// <summary>An item or a healing spell was used (section 7's Item action); a <see cref="UnitHealed"/> for the target follows.</summary>
public sealed record ItemUsed(string UnitId, string ItemId, string TargetId, int UsesLeft) : GameEvent;

/// <summary>An attack named a weapon slot other than the equipped one; the weapon is at the front of the inventory from now on.</summary>
public sealed record WeaponEquipped(string UnitId, string ItemId) : GameEvent;

/// <summary>An attack declared a combat art with the weapon it strikes with (issue 68); <see cref="Cost"/> extra uses are spent whatever the rolls. Precedes the <see cref="CombatFought"/>.</summary>
public sealed record ArtDeclared(string UnitId, string ArtId, string ItemId, int Cost) : GameEvent;

/// <summary>A physical weapon reached zero uses on this strike; it stays in the inventory and fights at the section 5 fallback.</summary>
public sealed record WeaponBroke(string UnitId, string ItemId) : GameEvent;

/// <summary>A spell's last use this battle was spent; it refreshes at the next map.</summary>
public sealed record SpellSpent(string UnitId, string ItemId) : GameEvent;

/// <summary>Why a Guard group woke (DESIGN.md section 8), the loudest cause first.</summary>
public enum WakeCause
{
    /// <summary>A member of the group died, at any distance.</summary>
    Death,

    /// <summary>A combat involved a tile within the wake radius plus 2 of a member.</summary>
    Noise,

    /// <summary>A player unit stands within the wake radius of a member.</summary>
    Proximity,

    /// <summary>A group linked to it by the map's <c>wake_links:</c> header woke (issue 393).</summary>
    Call,
}

/// <summary>
/// A Guard group woke; its members behave as Aggressive from now on. <paramref name="Lamps"/>
/// names the members whose lamps a player-phase wake lit on a dusk map, row-major by tile
/// (issue 382), and is empty for every other wake.
/// </summary>
public sealed record GroupWoke(string Group, WakeCause Cause, ValueList<Lamp> Lamps = default, string? CalledBy = null) : GameEvent;

/// <summary>A member of a woken group whose lamp is lit, and where it stood when it woke (issue 382).</summary>
public sealed record Lamp(string UnitId, Coord At);

/// <summary>
/// A map event fired (issue 32). <paramref name="Blocked"/> is true when its tile was barred:
/// a spawn tile with a unit on it or terrain the template cannot stand on, or a terrain change
/// its occupant could not stand on. <paramref name="Terrain"/> is the terrain id that barred a
/// spawn when no unit held the tile (issue 655), and null otherwise. A blocked event is spent
/// all the same. Unless blocked, the action's own event follows.
/// </summary>
public sealed record MapEventFired(string Name, bool Blocked, string? Terrain = null) : GameEvent;

/// <summary>A map event changed a tile's terrain.</summary>
public sealed record TerrainChanged(Coord At, string TerrainId) : GameEvent;

/// <summary>A map event brought an enemy onto the board.</summary>
public sealed record UnitSpawned(string UnitId, Coord At, string Group, Behavior Behavior) : GameEvent;

/// <summary>A map event set a flag.</summary>
public sealed record FlagSet(string Flag) : GameEvent;

/// <summary>
/// At the start of its carrier's phase a hungering weapon that fed on nothing since the last one
/// drains its carrier (DESIGN.md 13.23, experiment). Amount is what was actually lost; the drain
/// never takes a unit below 1, and <paramref name="Starved"/> is set when it would have taken the
/// carrier to 1 or below, which puts the weapon in its starved form.
/// </summary>
public sealed record HungerDrained(string UnitId, string ItemId, int Amount, int HpAfter, bool Starved) : GameEvent;

/// <summary>
/// A hungering weapon fed on a kill (DESIGN.md 13.23, experiment): <paramref name="Fed"/> is its
/// count after the kill, <paramref name="Healed"/> what the carrier actually gained,
/// <paramref name="MtBonus"/> its Mt growth after the kill, and <paramref name="Woke"/> is set on the
/// kill that reaches the cap, after which it neither drains, starves nor heals.
/// </summary>
public sealed record HungerFed(string UnitId, string ItemId, int Fed, int Healed, int HpAfter, int MtBonus, bool Woke) : GameEvent;

/// <summary>A starved hungering weapon landed a hit that killed nothing (DESIGN.md 13.23, experiment): it leaves the starved form and its carrier heals what <paramref name="Healed"/> says.</summary>
public sealed record HungerEased(string UnitId, string ItemId, int Healed, int HpAfter) : GameEvent;

/// <summary>
/// An heirloom turned to its next stage after a combat it was fought with (issue 646):
/// <paramref name="Stage"/> is the stage reached, counted from 0, and <paramref name="StageId"/> its id.
/// </summary>
public sealed record HeirloomTurned(string UnitId, string ItemId, int Stage, string StageId) : GameEvent;

/// <summary>
/// A hit from a frozen-iron weapon chilled a unit that survived it (issue 702 slice 2, <see cref="Frost"/>):
/// Mov -1 until the end of <paramref name="Side"/>'s next phase, the struck unit's side. <paramref name="Next"/>
/// is true when it was struck on its own side's phase, so the chill outlasts the phase under way.
/// </summary>
public sealed record UnitChilled(string UnitId, string ByUnitId, Side Side, bool Next = false) : GameEvent;

/// <summary>
/// A hit from a holder of Opening left an enemy alive and open (issue 772, <see cref="Opening"/>): until the
/// phase ends, every strike by an ally of <paramref name="ByUnitId"/> reads its Def <paramref name="Def"/> and
/// its Res <paramref name="Res"/> lower, never below 0.
/// </summary>
public sealed record UnitOpened(string UnitId, string ByUnitId, int Def, int Res) : GameEvent;

/// <summary>
/// A bow's crit landed a flier that survived (issue 703, <see cref="Grounding"/>): it moves on foot
/// until <paramref name="Side"/>'s next phase ends, or the one after that when <paramref name="Next"/>
/// says its own phase is under way.
/// </summary>
public sealed record UnitGrounded(string UnitId, string ByUnitId, Side Side, bool Next = false) : GameEvent;

/// <summary>
/// The captain called an order (DESIGN.md 13.2, issue 85): the allies it acts on in id order, how
/// many allies stood in its radius and how many were alive, and the captain's exposure where he
/// stands (<see cref="Exposure.Of"/>, no crit), the numbers the spike's binding fraction and
/// exposure counter read.
/// </summary>
public sealed record OrderCalled(string CaptainId, OrderKind Kind, int Radius, ValueList<string> Reached, int InRadius, int Alive, int Exposure) : GameEvent;

/// <summary>An ally used the move a Fall back order owed it (issue 85); from equal to to and an empty path: declined.</summary>
public sealed record FellBack(string UnitId, Coord From, Coord To, ValueList<Coord> Path) : GameEvent;
