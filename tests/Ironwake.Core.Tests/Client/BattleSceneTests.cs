using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Client;

/// <summary>
/// The battle scene (issue 535, slice 1): the pacing rule, the scene's timeline read from a
/// combat, its clip and effect names at the art spec's names, and the level-up card. The board is
/// the Tollgate's seed 113 turn 4, where the bandit leader and the rider both strike Teodor.
/// </summary>
public class BattleSceneTests
{
    private static readonly GameContent Content = ContentLoader.Load(Fixture.RealContentDirectory());

    private static ClientSession TurnFour()
    {
        var map = MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "maps", "the_tollgate.map"), Content);
        var client = new ClientSession(Content, BattleState.From(map, Content, Content.Cast, 113));
        var script = File.ReadAllText(Path.Combine(Fixture.RealContentDirectory(), "..", "docs", "screenshots", "the_tollgate-113-enemy.script"));
        Ironwake.Client.Script.Apply(client, script);
        return client;
    }

    private static void StepUntil(ClientSession client, string prefix)
    {
        while (client.Step())
        {
            if (client.Playing!.Line.StartsWith(prefix, StringComparison.Ordinal))
            {
                return;
            }
        }

        Assert.Fail($"no enemy line starts with '{prefix}'");
    }

    private static StrikeEvent Strike(string attacker, string target, bool hit, bool crit = false, int damage = 3, int hpAfter = 10) =>
        new(0, attacker, target, hit, crit, hit ? damage : 0, hpAfter);

    private static CombatFought Combat(BattleUnit attacker, BattleUnit target, params StrikeEvent[] strikes) =>
        new(attacker.Id, target.Id, 1, attacker.Side, ValueList<StrikeEvent>.From(strikes), strikes.LastOrDefault(s => s.TargetId == attacker.Id)?.TargetHpAfter ?? attacker.Hp,
            strikes.LastOrDefault(s => s.TargetId == target.Id)?.TargetHpAfter ?? target.Hp);

    [Fact]
    public void KeyMomentsLeaveAPlainEnemyStrikeOnTheMap()
    {
        var state = TurnFour().State;
        var enemy = state.Units.First(u => u.Side == Side.Enemy && !u.IsBoss);
        var player = state.Units.First(u => u.Side == Side.Player);
        var plain = Combat(enemy, player, Strike(enemy.Id, player.Id, hit: true), Strike(player.Id, enemy.Id, hit: false));

        Assert.False(Scenes.Plays(SceneSetting.KeyMoments, plain, state, new GameEvent[] { plain }));
        Assert.True(Scenes.Plays(SceneSetting.All, plain, state, new GameEvent[] { plain }));
    }

    [Fact]
    public void KeyMomentsPlayAPlayerStrikeACritAKillABossStrikeAndALevel()
    {
        var state = TurnFour().State;
        var enemy = state.Units.First(u => u.Side == Side.Enemy && !u.IsBoss);
        var boss = state.Units.First(u => u.IsBoss);
        var player = state.Units.First(u => u.Side == Side.Player);
        bool Key(CombatFought c, params GameEvent[] more) => Scenes.Plays(SceneSetting.KeyMoments, c, state, more.Prepend(c));

        Assert.True(Key(Combat(player, enemy, Strike(player.Id, enemy.Id, hit: false))));
        Assert.True(Key(Combat(enemy, player, Strike(enemy.Id, player.Id, hit: true, crit: true))));
        Assert.True(Key(Combat(enemy, player, Strike(enemy.Id, player.Id, hit: true, hpAfter: 0))));
        Assert.True(Key(Combat(enemy, player, Strike(enemy.Id, player.Id, hit: false), Strike(player.Id, enemy.Id, hit: true, hpAfter: 0))));
        Assert.True(Key(Combat(boss, player, Strike(boss.Id, player.Id, hit: false))));
        Assert.True(Key(Combat(enemy, player, Strike(enemy.Id, player.Id, hit: false)), new LeveledUp(player.Id, 3, Stats.Zero)));
    }

    [Fact]
    public void MapOnlyPlaysNoSceneEvenForAKill()
    {
        var state = TurnFour().State;
        var enemy = state.Units.First(u => u.Side == Side.Enemy && !u.IsBoss);
        var player = state.Units.First(u => u.Side == Side.Player);
        var kill = Combat(player, enemy, Strike(player.Id, enemy.Id, hit: true, crit: true, hpAfter: 0));

        Assert.False(Scenes.Plays(SceneSetting.MapOnly, kill, state, new GameEvent[] { kill }));
    }

    [Fact]
    public void TheSettingCyclesKeyMomentsAllMapOnlyAndTheFooterNamesIt()
    {
        Assert.Equal(SceneSetting.KeyMoments, default(SceneSetting));
        Assert.Equal(SceneSetting.All, Scenes.Next(SceneSetting.KeyMoments));
        Assert.Equal(SceneSetting.MapOnly, Scenes.Next(SceneSetting.All));
        Assert.Equal(SceneSetting.KeyMoments, Scenes.Next(SceneSetting.MapOnly));
        Assert.Equal("scenes: key moments", Scenes.Label(SceneSetting.KeyMoments));
        Assert.Equal("scenes: map only", Scenes.Label(SceneSetting.MapOnly));
    }

    [Fact]
    public void ABossStrikeCarriesASceneWhoseContactsAreTheBoardsNumberTimes()
    {
        var client = TurnFour();
        client.Submit(new EndPhase());
        StepUntil(client, "Bandit Leader attacks Teodor");

        var beat = Assert.Single(client.Beats);
        var scene = Assert.IsType<BattleScene>(beat.Scene);
        Assert.Equal(beat.Pops.Count, scene.Strikes.Count);
        Assert.Equal(scene.Strikes.Select(s => s.Contact), Rhythm.PopTimes(beat));
        Assert.Equal(beat.Pops.Select(p => (p.TargetId, p.Text, p.Kind)), scene.Strikes.Select(s => (s.TargetId, s.Text, s.Kind)));
        Assert.True(Rhythm.Length(beat) >= scene.Length);
        Assert.False(scene.Attacker.Left);
        Assert.True(scene.Defender.Left);
        Assert.StartsWith("boss_bandit_leader_", scene.Attacker.Prefixes[0], StringComparison.Ordinal);
        Assert.Equal("pikeman_lance", scene.Defender.Prefixes.Single());
    }

    [Fact]
    public void EveryClipASceneNamesIsOnTheArtSpecsList()
    {
        var client = TurnFour();
        client.SceneSetting = SceneSetting.All;
        client.Submit(new EndPhase());
        var scenes = new List<BattleScene>();
        while (client.Step())
        {
            scenes.AddRange(client.Beats.Select(b => b.Scene).OfType<BattleScene>());
        }

        Assert.NotEmpty(scenes);
        var classClips = ArtSpec.ClassClips(Content).ToHashSet();
        var effects = ArtSpec.Effects(Content).Select(e => e.Name).ToHashSet();
        foreach (var scene in scenes)
        {
            foreach (var side in new[] { scene.Attacker, scene.Defender })
            {
                Assert.All(ArtSpec.Clips, clip => Assert.Contains(side.ClipFiles(clip.Name)[^1], classClips));
            }

            Assert.All(scene.Strikes.SelectMany(s => s.Effects), e => Assert.Contains(e, effects));
        }
    }

    [Fact]
    public void AStrikesContactIsItsClipsContactFrameAtTwelveASecondAndHitStopHoldsThere()
    {
        var client = TurnFour();
        client.Submit(new EndPhase());
        StepUntil(client, "Rider attacks Teodor");
        var scene = Assert.Single(client.Beats).Scene!;

        var advance = ArtSpec.Clips.First(c => c.Name == "advance").Frames / BattleScene.Fps;
        Assert.Equal(advance, scene.Strikes[0].Start, 5);
        foreach (var s in scene.Strikes)
        {
            var clip = ArtSpec.Clips.First(c => c.Name == s.AttackerClip);
            var stop = s.Kind switch { PopKind.Miss => 0, PopKind.Crit => BattleScene.CritStop, _ => BattleScene.HitStop };
            Assert.Equal(s.Start + clip.Contact!.Value / BattleScene.Fps, s.Contact, 5);
            Assert.Equal(s.Start + clip.Frames / BattleScene.Fps + stop, s.End, 5);
        }

        for (var i = 1; i < scene.Strikes.Count; i++)
        {
            Assert.Equal(scene.Strikes[i - 1].End + BattleScene.Between, scene.Strikes[i].Start, 5);
        }

        Assert.True(scene.Length > scene.Strikes[^1].End);
    }

    [Fact]
    public void TheKillingBlowStrikesTheFallClipAndTheSceneHoldsTheFall()
    {
        var client = TurnFour();
        client.Submit(new EndPhase());
        StepUntil(client, "Rider attacks Teodor");
        var scene = Assert.Single(client.Beats).Scene!;

        Assert.Equal("teodor", scene.FallenId);
        var last = scene.Strikes.Last(s => s.TargetId == "teodor" && s.Kind != PopKind.Miss);
        Assert.Equal("fall", last.TargetClip);
        Assert.Equal(0, last.TargetHpAfter);
        var fall = ArtSpec.Clips.First(c => c.Name == "fall").Frames / BattleScene.Fps;
        Assert.True(scene.Length >= scene.FallStart + fall + BattleScene.Tail - 0.0001f);
    }

    [Fact]
    public void ADodgeRaisesDustAndAHitTheSparkTheArcForABladeOrTheSpellsOwnBurst()
    {
        var sword = Content.Weapons["iron_sword"];
        var bow = Content.Weapons["iron_bow"];
        var cinder = Content.Weapons["cinder"];
        var hit = Strike("a", "b", hit: true);
        var crit = Strike("a", "b", hit: true, crit: true);

        Assert.Equal(new[] { "dust" }, BattleScene.EffectsOf(Strike("a", "b", hit: false), sword));
        Assert.Equal(new[] { "hit_spark", "slash_arc" }, BattleScene.EffectsOf(hit, sword));
        Assert.Equal(new[] { "hit_spark" }, BattleScene.EffectsOf(hit, bow));
        Assert.Equal(new[] { "spell_cinder" }, BattleScene.EffectsOf(hit, cinder));
        Assert.Equal(new[] { "hit_spark", "slash_arc", "crit_flash" }, BattleScene.EffectsOf(crit, sword));
        Assert.Equal(new[] { "hit_spark" }, BattleScene.EffectsOf(hit, null));
    }

    [Fact]
    public void ShakeScalesWithDamageOverMaxHpAndACritIsLouder()
    {
        var state = TurnFour().State;
        var enemy = state.Units.First(u => u.Side == Side.Enemy && !u.IsBoss);
        var player = state.Units.First(u => u.Side == Side.Player);
        var max = player.MaxHp(Content);
        var small = BattleScene.Of(Combat(enemy, player, Strike(enemy.Id, player.Id, hit: true, damage: 2)), state, state, Content)!;
        var big = BattleScene.Of(Combat(enemy, player, Strike(enemy.Id, player.Id, hit: true, damage: 6)), state, state, Content)!;
        var critical = BattleScene.Of(Combat(enemy, player, Strike(enemy.Id, player.Id, hit: true, crit: true, damage: 6)), state, state, Content)!;
        var miss = BattleScene.Of(Combat(enemy, player, Strike(enemy.Id, player.Id, hit: false)), state, state, Content)!;

        Assert.Equal(2f / max, small.Strikes[0].Shake, 5);
        Assert.True(big.Strikes[0].Shake > small.Strikes[0].Shake);
        Assert.True(critical.Strikes[0].Shake > big.Strikes[0].Shake);
        Assert.Equal(0, miss.Strikes[0].Shake);
        Assert.Equal("dodge", miss.Strikes[0].TargetClip);
        Assert.Null(miss.FallenId);
    }

    [Fact]
    public void AnEnemysTemplateIsItsIdLessTheCounter()
    {
        Assert.Equal("bandit_leader", BattleScene.Template("bandit_leader-1"));
        Assert.Equal("brigand", BattleScene.Template("brigand-12"));
        Assert.Equal("captain", BattleScene.Template("captain"));
        Assert.Equal("odd-", BattleScene.Template("odd-"));
    }

    [Fact]
    public void TwoLevelsFromOneCombatShowOnceWithBothLevelsGains()
    {
        var state = TurnFour().State;
        var events = new GameEvent[]
        {
            new LeveledUp("wren", 3, Stats.Zero with { Hp = 1, Spd = 1 }),
            new LeveledUp("wren", 4, Stats.Zero with { Spd = 1, Def = 1 }),
        };

        var card = LevelUpCard.Of(events, state, Content)!;

        Assert.Equal(2, card.FromLevel);
        Assert.Equal(4, card.ToLevel);
        Assert.Equal(new[] { (Stat.Hp, 1), (Stat.Spd, 2), (Stat.Def, 1) }, card.Rose);
        Assert.Null(card.Flat);
        Assert.Equal(new[] { "Wren reaches level 4", "HP +1", "Speed +2", "Defence +1" }, card.Lines());
    }

    [Fact]
    public void ALevelWhereNothingRoseGetsTheUnitsDryLineNeverABlank()
    {
        var state = TurnFour().State;

        var card = LevelUpCard.Of(new GameEvent[] { new LeveledUp("teodor", 5, Stats.Zero) }, state, Content)!;

        Assert.Empty(card.Rose);
        Assert.Equal(LevelUpCard.FlatLines["teodor"], card.Flat);
        Assert.Equal(new[] { "Teodor reaches level 5", LevelUpCard.FlatLines["teodor"] }, card.Lines());
        Assert.All(Content.Cast, unit => Assert.True(LevelUpCard.FlatLines.ContainsKey(unit.Id), unit.Id));
        Assert.Null(LevelUpCard.Of(Array.Empty<GameEvent>(), state, Content));
    }

    [Fact]
    public void ABeatThatLevelsHoldsItsCardAfterTheScene()
    {
        var client = TurnFour();
        client.Submit(new EndPhase());
        StepUntil(client, "Rider attacks Teodor");
        var beat = Assert.Single(client.Beats);
        var card = new LevelUpCard("wren", "Wren", 2, 3, new[] { (Stat.Hp, 1) }, null);

        Assert.Null(beat.LevelUp);
        Assert.Equal(beat.Scene!.Length, Rhythm.Length(beat), 5);
        var levelled = beat with { LevelUp = card };
        Assert.Equal(beat.Scene.Length, levelled.LevelUpAt, 5);
        Assert.Equal(beat.Scene.Length + LevelUpCard.Hold, Rhythm.Length(levelled), 5);
        var onMap = levelled with { Scene = null };
        Assert.Equal(Rhythm.StrikeLength(onMap) + LevelUpCard.Hold, Rhythm.Length(onMap), 5);
    }

    private static BattleState Tollgate(Func<string, string>? edit = null)
    {
        var text = File.ReadAllText(Path.Combine(Fixture.RealContentDirectory(), "maps", "the_tollgate.map"));
        var path = Path.Combine(Path.GetTempPath(), $"ironwake-scene-{Guid.NewGuid():N}.map");
        File.WriteAllText(path, edit is null ? text : edit(text));
        try
        {
            return BattleState.From(MapFiles.Load(path, Content), Content, Content.Cast, 113);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DuskDarkensTheSceneTurnByTurnAndDaylightNever()
    {
        var map = MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "maps", "brackwater_cut.map"), Content);
        var start = Assert.IsType<int>(map.Dusk);

        Assert.Equal(SceneBackdrop.DuskFirst, SceneBackdrop.DarkOf(map, 1), 3);
        Assert.True(SceneBackdrop.DarkOf(map, 2) > SceneBackdrop.DarkOf(map, 1));
        Assert.Equal(SceneBackdrop.DuskLast, SceneBackdrop.DarkOf(map, start), 3);
        Assert.Equal(SceneBackdrop.DuskLast, SceneBackdrop.DarkOf(map, start + 4), 3);
        Assert.Equal(0f, SceneBackdrop.DarkOf(Tollgate().Map, 9));
    }

    [Fact]
    public void FogLiesOnForestAndWaterOnly()
    {
        var state = Tollgate();
        var archer = state.Units.First(u => u.Id == "archer-2");
        var captain = state.Find("captain")!;
        Assert.Equal("forest", state.Map.TerrainIdAt(archer.At));
        Assert.Equal("plain", state.Map.TerrainIdAt(captain.At));

        var backdrop = SceneBackdrop.Of(state, captain, archer);

        Assert.Equal(SceneBackdrop.GroundFog["forest"], backdrop.Right.Fog);
        Assert.Equal(0f, backdrop.Left.Fog);
    }

    [Fact]
    public void EmbersRiseOnlyWhereFireBurnsWithinReachAndTheSameEveryRun()
    {
        var state = Tollgate();
        var archer = state.Units.First(u => u.Id == "archer-2");
        var captain = state.Find("captain")!;
        Assert.Empty(SceneBackdrop.Of(state, captain, archer).Right.Embers);

        var burning = state with { Map = state.Map.WithTerrain(new Coord(archer.At.X + 1, archer.At.Y), "fire") };
        var near = SceneBackdrop.Of(burning, captain, archer);
        var far = state with { Map = state.Map.WithTerrain(new Coord(archer.At.X, archer.At.Y - SceneBackdrop.EmberReach - 1), "fire") };

        Assert.Equal(SceneBackdrop.EmbersPerFire, near.Right.Embers.Count);
        Assert.Empty(near.Left.Embers);
        Assert.Empty(SceneBackdrop.Of(far, captain, archer).Right.Embers);
        Assert.Equal(near.Right.Embers, SceneBackdrop.Of(burning, captain, archer).Right.Embers);
        Assert.All(near.Right.Embers, e => Assert.InRange(e.X, 0f, 1f));
    }

    [Fact]
    public void LampsHangOnlyOverAnEnemyWhoseGroupIsLit()
    {
        var state = Tollgate();
        var archer = state.Units.First(u => u.Id == "archer-2");
        var captain = state.Find("captain")!;
        Assert.False(SceneBackdrop.Of(state, captain, archer).Right.Lamps);

        var lit = SceneBackdrop.Of(state.Light(archer.Group!), captain, archer);

        Assert.True(lit.Right.Lamps);
        Assert.False(lit.Left.Lamps);
    }

    [Fact]
    public void ASceneCarriesTheBackdropOfTheBoardItWasFoughtOn()
    {
        var client = TurnFour();
        client.Submit(new EndPhase());
        StepUntil(client, "Bandit Leader attacks Teodor");

        var scene = Assert.IsType<BattleScene>(Assert.Single(client.Beats).Scene);

        Assert.Equal(scene.Defender.Terrain, scene.Backdrop.Left.Terrain);
        Assert.Equal(scene.Attacker.Terrain, scene.Backdrop.Right.Terrain);
        Assert.Equal(0f, scene.Backdrop.Dark);
    }

    [Fact]
    public void AHealThatLevelsItsHealerShowsTheCardAlone()
    {
        var state = Tollgate(text => text.Replace("P recruit:pell 6,10", "P recruit:maud 6,10", StringComparison.Ordinal));
        var maud = state.Find("maud")!;
        var wren = state.Find("wren")!;
        var slot = maud.Unit.Inventory.Items.ToList().FindIndex(s => s.ItemId == "salve");
        state = state.WithUnit(maud with { Unit = maud.Unit with { Exp = 99 } }).WithUnit(wren with { Hp = 5 });
        var client = new ClientSession(Content, state);
        client.Submit(new Move("maud", new Coord(5, 10)));

        Assert.True(client.Submit(new UseItem("maud", slot, "wren")));

        var beat = Assert.Single(client.Beats, b => b.LevelUp is not null);
        Assert.False(beat.IsStrike);
        Assert.Null(beat.Scene);
        Assert.Equal("maud", beat.LevelUp!.UnitId);
        Assert.Equal(0f, beat.LevelUpAt);
        Assert.Equal(LevelUpCard.Hold, Rhythm.Length(beat));
    }
}
