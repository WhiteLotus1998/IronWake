using System.IO.Compression;
using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Client;

/// <summary>
/// The generated art (issue 564): <c>docs/art/make_art.py</c> writes every token but the
/// captain's, every tile and every class and boss clip under <c>src/Ironwake.Godot/assets/art/</c>
/// at the art spec's names, and lists them in <c>generated.txt</c>. Every token and tile is the
/// spec's 2x frame and keeps LOOK.md's colours: a token is its side's values and nothing else, a
/// tile its terrain's colour with detail laid over it, so terrain never adds a hue. A clip is a
/// one-row sheet of the spec's frames with its sidecar, drawn in three neutral greys the client
/// tints with the side's colour, the feet on the pivot. An effect is a one-row sheet with its
/// sidecar, drawn opaque in LOOK.md's own values and never tinted, ember only where fire is.
/// </summary>
public class ArtGeneratedTests
{
    private const int Frame = 2 * ArtSpec.TokenFrame;

    /// <summary>The enemy disc's ink shadow, at the client's 0.55.</summary>
    private const int ShadowAlpha = 140;

    private static readonly Rgb Ink = LookPalette.Ui["ink"];

    private static string ArtDirectory() =>
        Path.GetFullPath(Path.Combine(Fixture.RealContentDirectory(), "..", "src", "Ironwake.Godot", "assets", "art"));

    private static List<string> Generated() =>
        File.ReadAllLines(Path.Combine(ArtDirectory(), "generated.txt"))
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .Select(l => Path.GetFileNameWithoutExtension(l))
            .ToList();

    /// <summary>The clip sheets' three greys: a base, its shade, and the dark of steel, hair and boots.</summary>
    private static readonly Rgba[] Greys = { new(0xD6, 0xD6, 0xD6, 255), new(0x9A, 0x9A, 0x9A, 255), new(0x4E, 0x4E, 0x4E, 255) };

    private static bool IsEffect(string name) => name.StartsWith("fx_", StringComparison.Ordinal);

    private static bool IsClip(string name) =>
        !name.StartsWith("token_", StringComparison.Ordinal) && !name.StartsWith("tile_", StringComparison.Ordinal) && !IsEffect(name);

    private static Rgba Value(Rgb c) => Opaque(c);

    /// <summary>
    /// The values each effect may use: white (<c>mark.struck</c>) for sparks and flashes, text for
    /// the slash arc and Radiance, frost (<c>mark.reach</c>) for Gust, Bolt and the heal, salt grey
    /// (<c>terrain.road</c>) for dust and smoke, and ember (<c>terrain.fire</c>) only where fire is.
    /// A new effect fails until it is given its values here.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, Rgba[]> EffectValues = new Dictionary<string, Rgba[]>
    {
        ["fx_hit_spark"] = new[] { Value(LookPalette.Marks["struck"]) },
        ["fx_slash_arc"] = new[] { Value(LookPalette.Ui["text"]) },
        ["fx_crit_flash"] = new[] { Value(LookPalette.Marks["struck"]), Value(LookPalette.Ui["text"]) },
        ["fx_heal"] = new[] { Value(LookPalette.Marks["reach"]), Value(LookPalette.Marks["struck"]) },
        ["fx_dust"] = new[] { Value(LookPalette.Terrain["road"]) },
        ["fx_embers"] = new[] { Value(LookPalette.Terrain["fire"]) },
        ["fx_spell_bolt"] = new[] { Value(LookPalette.Marks["reach"]), Value(LookPalette.Marks["struck"]) },
        ["fx_spell_cinder"] = new[] { Value(LookPalette.Terrain["fire"]), Value(LookPalette.Terrain["road"]) },
        ["fx_spell_gust"] = new[] { Value(LookPalette.Marks["reach"]), Value(LookPalette.Ui["text"]) },
        ["fx_spell_pell_commonplace"] = new[] { Value(LookPalette.Ui["text"]), Value(LookPalette.Marks["struck"]), Value(LookPalette.Terrain["road"]) },
        ["fx_spell_radiance"] = new[] { Value(LookPalette.Ui["text"]), Value(LookPalette.Marks["struck"]) },
    };

    /// <summary>The effects that are fire, the only ones ember may appear in.</summary>
    private static readonly IReadOnlySet<string> FireEffects = new HashSet<string> { "fx_embers", "fx_spell_cinder" };

    [Fact]
    public void TheGeneratorWritesEveryTokenButTheCaptainsEveryTileEveryClipAndEveryEffect()
    {
        var content = ContentLoader.Load(Fixture.RealContentDirectory());
        var expected = ArtSpec.Tokens(content).Where(n => n != "token_captain_player").Concat(ArtSpec.TokenVariants(content, ArtSpecTests.ShippedEnemies(content))).Concat(ArtSpec.Tiles(content))
            .Concat(ArtSpec.ClassClips(content)).Concat(ArtSpec.BossClips(content, ArtSpecTests.ShippedBosses(content)))
            .Concat(ArtSpec.EffectNames(content)).ToHashSet();

        Assert.Equal(expected.OrderBy(n => n, StringComparer.Ordinal), Generated().OrderBy(n => n, StringComparer.Ordinal));
        Assert.All(Generated(), name => Assert.True(File.Exists(Path.Combine(ArtDirectory(), name + ".png")), name));
    }

    [Fact]
    public void EveryGeneratedTokenAndTileIsTheSpecsFrameAtTwoX()
    {
        Assert.All(Generated().Where(n => !IsClip(n) && !IsEffect(n)), name =>
        {
            var image = Png.Read(Path.Combine(ArtDirectory(), name + ".png"));
            Assert.Equal((Frame, Frame), (image.Width, image.Height));
        });
    }

    [Fact]
    public void ATokenIsItsSidesValuesAndNothingElse()
    {
        Assert.All(Generated().Where(n => n.StartsWith("token_", StringComparison.Ordinal)), name =>
        {
            var image = Png.Read(Path.Combine(ArtDirectory(), name + ".png"));
            Assert.Null(TokenFault(image, player: name.EndsWith("_player", StringComparison.Ordinal)));
        });
    }

    [Fact]
    public void ATileIsItsTerrainWithDetailLaidOverIt()
    {
        Assert.All(Generated().Where(n => n.StartsWith("tile_", StringComparison.Ordinal)), name =>
        {
            var image = Png.Read(Path.Combine(ArtDirectory(), name + ".png"));
            Assert.Null(TileFault(image, name["tile_".Length..]));
        });
    }

    [Fact]
    public void AVariantTokenIsItsClassTokenWithTheTellAdded()
    {
        Assert.All(Generated().Where(n => n.StartsWith("token_", StringComparison.Ordinal) && n.Split('_').Length == 4), name =>
        {
            var variant = Png.Read(Path.Combine(ArtDirectory(), name + ".png"));
            var plain = Png.Read(Path.Combine(ArtDirectory(), string.Join('_', name.Split('_')[..3]) + ".png"));
            Assert.Null(TellFault(variant, plain));
        });
    }

    [Fact]
    public void AVariantWithNoTellOrAMissingStrokeFailsTheTellRule()
    {
        var plain = Png.Read(Path.Combine(ArtDirectory(), "token_reaver_enemy.png"));
        var variant = Png.Read(Path.Combine(ArtDirectory(), "token_reaver_enemy_double.png"));
        Assert.Null(TellFault(variant, plain));

        Assert.NotNull(TellFault(plain, plain));
        var bone = Opaque(LookPalette.EnemyBone);
        var (bx, by) = Enumerable.Range(0, plain.Width * plain.Height).Select(i => (i % plain.Width, i / plain.Width)).First(p => plain.At(p.Item1, p.Item2) == bone);
        Assert.NotNull(TellFault(variant.With(bx, by, Opaque(LookPalette.Enemy)), plain));
    }

    /// <summary>
    /// Why a variant token is not its class's token with the tell added, or null: every bone pixel
    /// of the plain token stays bone, and the variant adds at least 20 more (the tell reads at 1x).
    /// </summary>
    private static string? TellFault(Png variant, Png plain)
    {
        var bone = Opaque(LookPalette.EnemyBone);
        for (var y = 0; y < plain.Height; y++)
        {
            for (var x = 0; x < plain.Width; x++)
            {
                if (plain.At(x, y) == bone && variant.At(x, y) != bone)
                {
                    return $"pixel {x},{y} of the class's silhouette is missing";
                }
            }
        }

        var added = variant.Count(bone) - plain.Count(bone);
        return added >= 20 ? null : $"the tell adds {added} pixels, fewer than 20";
    }

    [Fact]
    public void ATokenWithAForeignColourFailsTheSidesRule()
    {
        var image = Png.Read(Path.Combine(ArtDirectory(), "token_cadet_enemy.png"));
        Assert.Null(TokenFault(image, player: false));

        Assert.NotNull(TokenFault(image.With(48, 40, new Rgba(200, 40, 40, 255)), player: false));
        Assert.NotNull(TokenFault(image, player: true));
        Assert.NotNull(TokenFault(image.With(48, 90, new Rgba(Ink.R, Ink.G, Ink.B, 255)), player: false));
    }

    [Fact]
    public void ATileThatAddsAHueFailsTheTerrainRule()
    {
        var image = Png.Read(Path.Combine(ArtDirectory(), "tile_forest.png"));
        Assert.Null(TileFault(image, "forest"));

        Assert.NotNull(TileFault(image.With(10, 10, new Rgba(200, 40, 40, 255)), "forest"));
        Assert.NotNull(TileFault(image.With(10, 10, new Rgba(0, 0, 0, 0)), "forest"));
        Assert.NotNull(TileFault(image, "plain"));
    }

    [Fact]
    public void AClipIsOneRowOfTheSpecsFramesWithItsSidecar()
    {
        Assert.All(Generated().Where(IsClip), name =>
        {
            var clip = ArtSpec.Clips.Where(c => name.EndsWith("_" + c.Name, StringComparison.Ordinal)).MaxBy(c => c.Name.Length)!;
            var image = Png.Read(Path.Combine(ArtDirectory(), name + ".png"));
            Assert.Equal((clip.Frames * ArtSpec.ClipFrame, ArtSpec.ClipFrame), (image.Width, image.Height));

            using var sidecar = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(ArtDirectory(), name + ".json")));
            var root = sidecar.RootElement;
            Assert.Equal(new[] { ArtSpec.ClipFrame, ArtSpec.ClipFrame }, root.GetProperty("frame").EnumerateArray().Select(e => e.GetInt32()));
            Assert.Equal(clip.Frames, root.GetProperty("frames").GetInt32());
            Assert.Equal(new[] { 128, 232 }, root.GetProperty("pivot").EnumerateArray().Select(e => e.GetInt32()));
            var contact = root.GetProperty("contact");
            Assert.Equal(clip.Contact, contact.ValueKind == System.Text.Json.JsonValueKind.Null ? null : contact.GetInt32());
        });
    }

    [Fact]
    public void AClipIsTheThreeGreysAndStandsOnThePivot()
    {
        Assert.All(Generated().Where(IsClip), name =>
        {
            var image = Png.Read(Path.Combine(ArtDirectory(), name + ".png"));
            Assert.Null(ClipFault(image));
        });
    }

    [Fact]
    public void AClipWithAHueOrOffThePivotFailsTheClipRule()
    {
        var image = Png.Read(Path.Combine(ArtDirectory(), "cadet_sword_idle.png"));
        Assert.Null(ClipFault(image));

        Assert.NotNull(ClipFault(image.With(128, 180, new Rgba(232, 163, 61, 255))));
        Assert.NotNull(ClipFault(image.With(128, 180, new Rgba(0xD6, 0xD6, 0xD6, 128))));
        Assert.NotNull(ClipFault(image.With(128, 250, new Rgba(0x4E, 0x4E, 0x4E, 255))));
        Assert.NotNull(ClipFault(image.Blank(0, ArtSpec.ClipFrame)));
    }

    private static bool IsLanceClip(string name) =>
        IsClip(name) && (name.Contains("_lance_", StringComparison.Ordinal) || name.Contains("_toll_spear_", StringComparison.Ordinal));

    private static readonly string[] Thrusts = { "strike", "strike_crit", "miss_recover" };

    [Fact]
    public void ALanceThrustPeaksOnItsContactFrameInsideItsFrame()
    {
        var thrusts = Generated().Where(IsLanceClip)
            .Select(name => (name, clip: ArtSpec.Clips.SingleOrDefault(c => Thrusts.Contains(c.Name) && name.EndsWith("_" + c.Name, StringComparison.Ordinal))))
            .Where(t => t.clip is not null).ToList();
        Assert.NotEmpty(thrusts);
        Assert.All(thrusts, t => Assert.Null(LanceFault(Png.Read(Path.Combine(ArtDirectory(), t.name + ".png")), t.clip!.Contact!.Value)));
    }

    [Fact]
    public void ALanceCutByItsFrameOrPeakingOffContactFailsTheLanceRule()
    {
        var image = Png.Read(Path.Combine(ArtDirectory(), "pikeman_lance_strike.png"));
        Assert.Null(LanceFault(image, 5));

        Assert.NotNull(LanceFault(image.With(5 * ArtSpec.ClipFrame + 252, 100, new Rgba(0x4E, 0x4E, 0x4E, 255)), 5));
        Assert.NotNull(LanceFault(image.Blank(5 * ArtSpec.ClipFrame, 6 * ArtSpec.ClipFrame), 5));
    }

    /// <summary>The margin a lance's tip keeps from its frame's right edge (round 170: the crop ate the reach).</summary>
    private const int LanceMargin = 8;

    /// <summary>Why a lance thrust breaks the rule, or null: every frame keeps the tip <see cref="LanceMargin"/> inside the frame, and the contact frame reaches furthest.</summary>
    private static string? LanceFault(Png image, int contact)
    {
        var frames = image.Width / ArtSpec.ClipFrame;
        var reach = new int[frames];
        for (var f = 0; f < frames; f++)
        {
            reach[f] = -1;
            for (var x = 0; x < ArtSpec.ClipFrame; x++)
            {
                for (var y = 0; y < image.Height; y++)
                {
                    if (image.At(f * ArtSpec.ClipFrame + x, y).A != 0)
                    {
                        reach[f] = x;
                        break;
                    }
                }
            }

            if (reach[f] >= ArtSpec.ClipFrame - LanceMargin)
            {
                return $"frame {f} reaches column {reach[f]}, inside the last {LanceMargin}";
            }
        }

        var widest = Array.IndexOf(reach, reach.Max());
        return reach[contact] == reach.Max() ? null : $"the tip is furthest on frame {widest}, not the contact frame {contact}";
    }

    [Fact]
    public void AnEffectIsOneRowOfItsFramesWithItsSidecar()
    {
        var content = ContentLoader.Load(Fixture.RealContentDirectory());
        var frames = ArtSpec.Effects(content).ToDictionary(e => $"fx_{e.Name}", e => e.Frames);
        Assert.All(Generated().Where(IsEffect), name =>
        {
            var image = Png.Read(Path.Combine(ArtDirectory(), name + ".png"));
            Assert.Equal((frames[name] * ArtSpec.ClipFrame, ArtSpec.ClipFrame), (image.Width, image.Height));

            using var sidecar = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(ArtDirectory(), name + ".json")));
            var root = sidecar.RootElement;
            Assert.Equal(new[] { ArtSpec.ClipFrame, ArtSpec.ClipFrame }, root.GetProperty("frame").EnumerateArray().Select(e => e.GetInt32()));
            Assert.Equal(frames[name], root.GetProperty("frames").GetInt32());
            Assert.Equal(new[] { ArtSpec.EffectPivot.X, ArtSpec.EffectPivot.Y }, root.GetProperty("pivot").EnumerateArray().Select(e => e.GetInt32()));
            Assert.Equal(System.Text.Json.JsonValueKind.Null, root.GetProperty("contact").ValueKind);
        });
    }

    [Fact]
    public void AnEffectIsItsOwnLookValuesAndEmberOnlyWhereFireIs()
    {
        Assert.All(Generated().Where(IsEffect), name =>
        {
            Assert.True(EffectValues.ContainsKey(name), $"{name} has no values in EffectValues");
            var image = Png.Read(Path.Combine(ArtDirectory(), name + ".png"));
            Assert.Null(EffectFault(image, name));
        });
        Assert.All(EffectValues.Where(e => !FireEffects.Contains(e.Key)), e => Assert.DoesNotContain(Value(LookPalette.Terrain["fire"]), e.Value));
    }

    [Fact]
    public void AnEffectWithAForeignValueEmberOrAnEmptyFrameFailsTheEffectRule()
    {
        var image = Png.Read(Path.Combine(ArtDirectory(), "fx_hit_spark.png"));
        Assert.Null(EffectFault(image, "fx_hit_spark"));

        var fire = LookPalette.Terrain["fire"];
        Assert.NotNull(EffectFault(image.With(128, 128, new Rgba(fire.R, fire.G, fire.B, 255)), "fx_hit_spark"));
        Assert.NotNull(EffectFault(image.With(128, 128, new Rgba(255, 255, 255, 128)), "fx_hit_spark"));
        Assert.NotNull(EffectFault(image.Blank(0, ArtSpec.ClipFrame), "fx_hit_spark"));
    }

    [Fact]
    public void TheSheetForLotusNamesEveryGeneratedFile()
    {
        var page = File.ReadAllText(Path.Combine(Fixture.RealContentDirectory(), "..", "docs", "art", "for-lotus.html"));
        Assert.Empty(UnnamedOnSheet(page, Generated()));
    }

    [Fact]
    public void ASheetThatMissesAGeneratedFileFailsTheSheetRule()
    {
        var page = File.ReadAllText(Path.Combine(Fixture.RealContentDirectory(), "..", "docs", "art", "for-lotus.html"));
        Assert.Equal(new[] { "token_newclass_enemy" }, UnnamedOnSheet(page, Generated().Append("token_newclass_enemy")));
        Assert.Equal(new[] { "fx_dust" }, UnnamedOnSheet(page.Replace("<code>fx_dust</code>", ""), Generated()));
    }

    /// <summary>
    /// The generated files the sheet for Lotus (<c>docs/art/for-lotus.html</c>, written by
    /// <c>docs/art/for_lotus.py</c>) does not name, so a new row cannot miss the sheet an artist works from.
    /// </summary>
    private static List<string> UnnamedOnSheet(string page, IEnumerable<string> generated) =>
        generated.Where(n => !page.Contains("<code>" + n + "</code>", StringComparison.Ordinal)).ToList();

    /// <summary>
    /// Why an effect sheet breaks the look, or null: every pixel clear or opaque in one of the
    /// effect's own values (ember only in the fire effects), and every frame drawing something.
    /// </summary>
    private static string? EffectFault(Png image, string name)
    {
        var allowed = EffectValues[name];
        var drawn = new bool[image.Width / ArtSpec.ClipFrame];
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var p = image.At(x, y);
                if (p.A == 0)
                {
                    continue;
                }

                if (!allowed.Contains(p))
                {
                    return $"pixel {x},{y} is {p}, not one of {name}'s values";
                }

                drawn[x / ArtSpec.ClipFrame] = true;
            }
        }

        var empty = Array.IndexOf(drawn, false);
        return empty < 0 ? null : $"frame {empty} draws nothing";
    }

    /// <summary>
    /// Why a clip sheet breaks the look, or null: every pixel clear or one of the three greys, so
    /// the client's tint carries the side; nothing below the pivot's row; and the first frame's
    /// lowest pixel within four rows of the pivot, so the figure stands where the client plants it.
    /// </summary>
    private static string? ClipFault(Png image)
    {
        var lowest = -1;
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var p = image.At(x, y);
                if (p.A == 0)
                {
                    continue;
                }

                if (!Greys.Contains(p))
                {
                    return $"pixel {x},{y} is {p}, not one of the three greys";
                }

                if (y >= 232)
                {
                    return $"pixel {x},{y} is below the pivot";
                }

                if (x < ArtSpec.ClipFrame)
                {
                    lowest = y;
                }
            }
        }

        return lowest >= 228 ? null : $"the first frame's feet end at row {lowest}, not on the pivot's";
    }

    /// <summary>
    /// Why a token breaks the look, or null: every pixel clear, one of its side's values, or (the
    /// enemy's) the ink shadow; the bottom 16 rows clear for the HP bar; the side's fill present.
    /// </summary>
    private static string? TokenFault(Png image, bool player)
    {
        var allowed = player
            ? new[] { Opaque(LookPalette.Player), Opaque(LookPalette.PlayerDeep), Opaque(Ink) }
            : new[] { Opaque(LookPalette.Enemy), Opaque(LookPalette.EnemyBone), new Rgba(Ink.R, Ink.G, Ink.B, ShadowAlpha) };
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var p = image.At(x, y);
                if (p.A == 0)
                {
                    continue;
                }

                if (y >= image.Height - 16)
                {
                    return $"pixel {x},{y} is inside the HP bar's rows";
                }

                if (!allowed.Contains(p))
                {
                    return $"pixel {x},{y} is {p}, not one of the side's values";
                }
            }
        }

        return image.Count(allowed[0]) > 0 ? null : "the side's fill is missing";
    }

    /// <summary>
    /// Why a tile breaks the look, or null: fire is ember hatch on clear ground; any other tile is
    /// opaque, each pixel its terrain's colour or that colour with ink, frost or iron laid over it
    /// at 20 to 65 percent (LOOK.md's detail and the wall's coursing), so no pixel adds a hue.
    /// </summary>
    private static string? TileFault(Png image, string terrain)
    {
        var baseColour = Opaque(LookPalette.Terrain[terrain]);
        var overlays = new[] { Ink, LookPalette.Marks["reach"], LookPalette.Terrain["mountain"] };
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var p = image.At(x, y);
                if (terrain == LookPalette.HatchedTerrain)
                {
                    if (p.A != 0 && p != baseColour)
                    {
                        return $"pixel {x},{y} is {p}, not ember or clear";
                    }

                    continue;
                }

                if (p.A != 255)
                {
                    return $"pixel {x},{y} is not opaque";
                }

                if (p != baseColour && !overlays.Any(o => LaidOver(LookPalette.Terrain[terrain], o, p)))
                {
                    return $"pixel {x},{y} is {p}, not {terrain} or a detail over it";
                }
            }
        }

        return null;
    }

    /// <summary>Whether <paramref name="p"/> is <paramref name="over"/> laid on <paramref name="under"/> at one alpha in 0.2 to 0.65, to a channel's rounding.</summary>
    private static bool LaidOver(Rgb under, Rgb over, Rgba p)
    {
        var channels = new[] { (under.R, over.R, p.R), (under.G, over.G, p.G), (under.B, over.B, p.B) };
        var (u, o, v) = channels.MaxBy(c => Math.Abs(c.Item2 - c.Item1));
        if (o == u)
        {
            return false;
        }

        var alpha = (double)(v - u) / (o - u);
        return alpha is >= 0.2 and <= 0.65
            && channels.All(c => Math.Abs(c.Item1 + (c.Item2 - c.Item1) * alpha - c.Item3) <= 1.5);
    }

    private static Rgba Opaque(Rgb c) => new(c.R, c.G, c.B, 255);

    private readonly record struct Rgba(byte R, byte G, byte B, byte A)
    {
        public override string ToString() => $"#{R:X2}{G:X2}{B:X2}/{A}";
    }

    /// <summary>A PNG read as 8-bit RGBA, enough of the format for the generator's files (every filter, no interlace).</summary>
    private sealed record Png(int Width, int Height, byte[] Pixels)
    {
        public Rgba At(int x, int y)
        {
            var i = 4 * (y * Width + x);
            return new Rgba(Pixels[i], Pixels[i + 1], Pixels[i + 2], Pixels[i + 3]);
        }

        public Png With(int x, int y, Rgba p)
        {
            var copy = (byte[])Pixels.Clone();
            var i = 4 * (y * Width + x);
            (copy[i], copy[i + 1], copy[i + 2], copy[i + 3]) = (p.R, p.G, p.B, p.A);
            return this with { Pixels = copy };
        }

        public Png Blank(int x0, int x1)
        {
            var copy = (byte[])Pixels.Clone();
            for (var y = 0; y < Height; y++)
            {
                Array.Clear(copy, 4 * (y * Width + x0), 4 * (x1 - x0));
            }

            return this with { Pixels = copy };
        }

        public int Count(Rgba p) =>
            Enumerable.Range(0, Width * Height).Count(i => At(i % Width, i / Width) == p);

        public static Png Read(string path)
        {
            var bytes = File.ReadAllBytes(path);
            Assert.True(bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }), $"{path} is not a PNG");
            int width = 0, height = 0;
            using var data = new MemoryStream();
            for (var at = 8; at < bytes.Length;)
            {
                var length = BigEndian(bytes, at);
                var kind = System.Text.Encoding.ASCII.GetString(bytes, at + 4, 4);
                if (kind == "IHDR")
                {
                    width = BigEndian(bytes, at + 8);
                    height = BigEndian(bytes, at + 12);
                    Assert.True(bytes[at + 16] == 8 && bytes[at + 17] == 6 && bytes[at + 20] == 0, $"{path} is not 8-bit RGBA without interlace");
                }
                else if (kind == "IDAT")
                {
                    data.Write(bytes, at + 8, length);
                }

                at += 12 + length;
            }

            data.Position = 0;
            using var inflate = new ZLibStream(data, CompressionMode.Decompress);
            using var raw = new MemoryStream();
            inflate.CopyTo(raw);
            return new Png(width, height, Unfilter(raw.ToArray(), width, height));
        }

        private static int BigEndian(byte[] b, int at) => (b[at] << 24) | (b[at + 1] << 16) | (b[at + 2] << 8) | b[at + 3];

        private static byte[] Unfilter(byte[] raw, int width, int height)
        {
            var stride = 4 * width;
            var pixels = new byte[stride * height];
            for (var y = 0; y < height; y++)
            {
                var filter = raw[y * (stride + 1)];
                for (var x = 0; x < stride; x++)
                {
                    var v = raw[y * (stride + 1) + 1 + x];
                    int a = x >= 4 ? pixels[y * stride + x - 4] : 0;
                    int b = y > 0 ? pixels[(y - 1) * stride + x] : 0;
                    int c = x >= 4 && y > 0 ? pixels[(y - 1) * stride + x - 4] : 0;
                    pixels[y * stride + x] = (byte)(filter switch
                    {
                        0 => v,
                        1 => v + a,
                        2 => v + b,
                        3 => v + ((a + b) >> 1),
                        4 => v + Paeth(a, b, c),
                        _ => throw new InvalidDataException($"PNG filter {filter}"),
                    });
                }
            }

            return pixels;
        }

        private static int Paeth(int a, int b, int c)
        {
            var p = a + b - c;
            int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
            return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
        }
    }
}
