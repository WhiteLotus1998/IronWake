using Ironwake.Core;

namespace Ironwake.Sim;

/// <summary>
/// The Sim read Lotus asked for on Obsidian Armor (issue 1403 slice 3, DECISIONS/0361): the full finale company on a
/// <c>deploy: all</c> board, Pell holding a fixture shell tome (<see cref="Tome"/>, +20 Def against the first hit, three
/// phases, range 1, one use; her class given earth, as the fixture tests give it), played by the heuristic once per
/// arm: no tome, the tome held and never laid, then the shell on the most exposed, on the drake rider alone, on a healer alone, and on the ally of the
/// highest Def alone (<see cref="ShellAim"/>). It reports, per arm, the wins, how many games laid the shell, and how the
/// shells ended: broken by a jab (the breaking hit's plain damage on the bare wearer under a quarter of its max HP), by a
/// real blow, by a hit no forecast names (a watch shot, a line strike, an area cast), the wearer killed through it, or
/// fallen unbroken. Data only; no shell tome ships, and the number is Lotus's to sign.
/// </summary>
public static class ShellRun
{
    /// <summary>The fixture tome's id.</summary>
    public const string TomeId = "sim_obsidian_armor";

    /// <summary>The caster the read hands the tome to.</summary>
    public const string CasterId = "pell";

    /// <summary>The fixture shell: Lotus's numbers (+20 Def, no Mov cost, three phases, the caster or an adjacent ally), once a map.</summary>
    public static readonly ArmorSpell Tome = new(20, 0, 3) { Shell = true, Range = 1 };

    /// <summary>One arm's reading.</summary>
    public sealed record Reading(string Arm, int Games, int Wins, int Laid, int Jabs, int Blows, int Unforecast, int KilledThrough, int Fell, IReadOnlyList<int> BareDamage)
    {
        public string Line()
        {
            var sorted = BareDamage.OrderBy(d => d).ToList();
            var median = sorted.Count == 0 ? "-" : sorted[(sorted.Count - 1) / 2].ToString();
            return $"shell {Arm}: wins {Wins}/{Games}, laid in {Laid}; broken by a jab {Jabs}, by a blow {Blows}, unforecast {Unforecast}; killed through it {KilledThrough}; fell unbroken {Fell}; breaking hit's bare damage median {median}";
        }
    }

    /// <summary><paramref name="fielded"/> with the fixture tome and Pell's class given earth; the tome goes in her last slot, the last stack dropped when her pack is full.</summary>
    public static GameContent Armed(GameContent fielded)
    {
        var cinder = fielded.Weapon("cinder");
        var tome = cinder with { Id = TomeId, Name = "Obsidian Armor", School = MagicSchool.Earth, Rider = RiderKind.Armor, Armor = Tome, Ignites = false, Durability = 1, Price = null };
        var cast = fielded.Cast.Select(u =>
        {
            if (u.Id != CasterId)
            {
                return u;
            }

            var items = u.Inventory.Items.ToList();
            if (items.Count >= Inventory.Capacity)
            {
                items.RemoveAt(items.Count - 1);
            }

            items.Add(new ItemStack(TomeId, 1));
            return u with { Inventory = new Inventory(ValueList<ItemStack>.From(items)) };
        });
        var casterClass = fielded.Cast.First(u => u.Id == CasterId).ClassId;
        var schools = fielded.Class(casterClass).Schools;
        return fielded with
        {
            Weapons = fielded.Weapons.SetItem(TomeId, tome),
            Classes = schools.Contains(MagicSchool.Earth) ? fielded.Classes : fielded.Classes.SetItem(casterClass, fielded.Class(casterClass) with { Schools = ValueList<MagicSchool>.From(schools.Append(MagicSchool.Earth)) }),
            Cast = ValueList<Unit>.From(cast),
        };
    }

    /// <summary>Plays every arm <paramref name="seeds"/> times on <paramref name="map"/> with the full company at <paramref name="level"/>.</summary>
    public static IReadOnlyList<Reading> Measure(GameContent content, MapDefinition map, int level, int seeds)
    {
        var fielded = FinaleRun.Fielded(content, FinaleCompany.Full, level);
        var armed = Armed(fielded);
        var readings = new List<Reading> { Arm("none", fielded, map, seeds, ShellAim.Exposed) };
        foreach (var aim in new[] { ShellAim.Never, ShellAim.Exposed, ShellAim.Drake, ShellAim.Healer, ShellAim.Front })
        {
            readings.Add(Arm(aim.ToString().ToLowerInvariant(), armed, map, seeds, aim));
        }

        return readings;
    }

    private static Reading Arm(string name, GameContent content, MapDefinition map, int seeds, ShellAim aim)
    {
        int wins = 0, laid = 0, jabs = 0, blows = 0, unforecast = 0, through = 0, fell = 0;
        var bare = new List<int>();
        for (var seed = 1; seed <= seeds; seed++)
        {
            var player = new HeuristicPlayer { Shell = aim };
            var state = BattleState.From(map, content, content.Cast, (ulong)seed);
            var cast = false;
            while (!state.Outcome.IsOver)
            {
                IReadOnlyList<Command> commands = state.Phase == Side.Player ? player.Next(state, content) : EnemyAi.PlanWithAnvils(state, content).Commands;
                foreach (var command in commands)
                {
                    var before = state;
                    var result = Resolver.Apply(state, content, command);
                    if (!result.Accepted)
                    {
                        throw new InvalidOperationException($"{command} was rejected: {result.Rejection!.Message}");
                    }

                    foreach (var e in result.Events)
                    {
                        switch (e)
                        {
                            case ArmorDonned { Shell: true }:
                                cast = true;
                                break;
                            case ArmorShattered shattered:
                                if (Bare(before, content, command, shattered) is { } damage)
                                {
                                    bare.Add(damage.Plain);
                                    if (damage.Plain * 4 < damage.MaxHp)
                                    {
                                        jabs++;
                                    }
                                    else
                                    {
                                        blows++;
                                    }
                                }
                                else
                                {
                                    unforecast++;
                                }

                                break;
                            case ArmorFell fallen when before.Find(fallen.UnitId) is { Armor.Shell: true }:
                                fell++;
                                break;
                            case UnitDied died when before.Find(died.UnitId) is { Armor.Shell: true }:
                                through++;
                                break;
                        }
                    }

                    state = result.Next;
                    if (state.Outcome.IsOver || result.Events.OfType<WatchFired>().Any(f => f.Strike.TargetHpAfter == 0))
                    {
                        break;
                    }
                }
            }

            wins += state.Outcome.Result == BattleResult.Won ? 1 : 0;
            laid += cast ? 1 : 0;
        }

        return new Reading(name, seeds, wins, laid, jabs, blows, unforecast, through, fell, bare);
    }

    /// <summary>
    /// The breaking hit's plain damage on the bare wearer and the wearer's max HP, read from the forecast of
    /// <paramref name="command"/> on the board before it, when it is an attack between the striker and the wearer; null
    /// for a break no forecast names.
    /// </summary>
    private static (int Plain, int MaxHp)? Bare(BattleState before, GameContent content, Command command, ArmorShattered shattered)
    {
        if (command is not Attack attack || before.Find(attack.UnitId) is not { } attacker || before.Find(attack.TargetId) is not { } target
            || Queries.Forecast(before, content, attacker, target, attack.Slot, attack.Art) is not { } forecast)
        {
            return null;
        }

        var (side, wearer) = (shattered.ByUnitId, shattered.UnitId) == (attacker.Id, target.Id) ? (forecast.Attacker, target)
            : (shattered.ByUnitId, shattered.UnitId) == (target.Id, attacker.Id) ? (forecast.Defender, attacker)
            : (null, null);
        return side is null ? null : (side.Damage, wearer!.MaxHp(content));
    }
}
