using Arenas.Core.Configs.ConfigElements;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Terraria.ID;

namespace Arenas.Common.Generation;

/// <summary>
/// Opt-in headless sampler. Set ARENAS_WORLDGEN_SELFTEST=1 before launching tModLoader to generate and audit
/// every procedural arena against scratch Tilemaps without touching a saved world.
/// </summary>
internal sealed class ArenaGenerationSelfTestSystem : ModSystem
{
    public override void PostSetupContent()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("ARENAS_WORLDGEN_SELFTEST"), "1", StringComparison.Ordinal))
            return;

        int samples = int.TryParse(Environment.GetEnvironmentVariable("ARENAS_WORLDGEN_SELFTEST_SAMPLES"), out int configured)
            ? Math.Clamp(configured, 1, 5)
            : 1;
        RunSamples(samples);
    }

    private static void RunSamples(int samples)
    {
        string dumpDirectory = Environment.GetEnvironmentVariable("ARENAS_WORLDGEN_SELFTEST_DUMP");
        Tilemap previousTiles = Main.tile;
        int previousWidth = Main.maxTilesX, previousHeight = Main.maxTilesY;
        int previousSpawnX = Main.spawnTileX, previousSpawnY = Main.spawnTileY;
        double previousSurface = Main.worldSurface, previousRock = Main.rockLayer;
        ArenaGeneratorKind[] kinds =
        [
            ArenaGeneratorKind.KingSlimeSurface,
            ArenaGeneratorKind.EyeSurface,
            ArenaGeneratorKind.PlanteraJungle,
            ArenaGeneratorKind.GolemTemple
        ];

        Log.Debug($"[WorldGenSelfTest] START kinds={kinds.Length} samplesPerKind={samples}");
        try
        {
            if (!string.IsNullOrWhiteSpace(dumpDirectory))
            {
                Directory.CreateDirectory(dumpDirectory);
                Log.Debug($"[WorldGenSelfTest] Writing diagnostic map samples to {dumpDirectory}");
            }
            foreach (ArenaGeneratorKind kind in kinds)
                for (int sample = 0; sample < samples; sample++)
                {
                    int seed = unchecked(0x41C64E6D * (sample + 1) ^ ((int)kind * 0x9E3779B));
                    RunSample(kind, seed, ArenaGeometryDefaults.Create(kind), dumpDirectory, "default");
                }

            if (string.Equals(Environment.GetEnvironmentVariable("ARENAS_WORLDGEN_SELFTEST_CUSTOM"), "1", StringComparison.Ordinal))
                foreach (ArenaGeneratorKind kind in kinds)
                    RunSample(kind, unchecked(0x5F3759DF ^ (int)kind * 7919), CreateResizedGeometry(kind), dumpDirectory, "resized");
            Log.Debug("[WorldGenSelfTest] ALL SAMPLES PASSED");
        }
        catch (Exception exception)
        {
            Log.Debug($"[WorldGenSelfTest/FAIL] {exception}");
            throw;
        }
        finally
        {
            Main.tile = previousTiles;
            Main.maxTilesX = previousWidth;
            Main.maxTilesY = previousHeight;
            Main.spawnTileX = previousSpawnX;
            Main.spawnTileY = previousSpawnY;
            Main.worldSurface = previousSurface;
            Main.rockLayer = previousRock;
        }
    }

    private static void RunSample(ArenaGeneratorKind kind, int seed, ArenaGeometryConfig geometry, string dumpDirectory, string variant)
    {
        geometry = RoundTrip(geometry);
        Main.maxTilesX = geometry.WorldWidth;
        Main.maxTilesY = geometry.WorldHeight;
        Main.tile = new Tilemap((ushort)geometry.WorldWidth, (ushort)geometry.WorldHeight);
        Stopwatch timer = Stopwatch.StartNew();
        ArenaGenerationJob job = new(ArenaGeneratorRegistry.GetForSelfTest(kind), geometry, seed);
        int ticks = 0;
        while (!job.IsComplete && !job.HasFailed && ticks++ < 20_000 && timer.Elapsed < TimeSpan.FromMinutes(2))
            job.Tick();

        if (job.HasFailed)
            throw new InvalidOperationException($"Self-test failed for {kind} {variant} seed={seed} during {job.FailedStage}", job.Error);
        if (!job.IsComplete)
            throw new TimeoutException($"Self-test timed out for {kind} {variant} seed={seed}; ticks={ticks}, elapsed={timer.Elapsed}");
        timer.Stop();
        if (!string.IsNullOrWhiteSpace(dumpDirectory))
            WriteMapDump(dumpDirectory, kind, seed, job.Layout);
        Log.Debug($"[WorldGenSelfTest/PASS] kind={kind} variant={variant} world={geometry.WorldWidth}x{geometry.WorldHeight} seed={seed} elapsed={timer.Elapsed.TotalSeconds:F2}s ticks={ticks} boss={job.Layout.BossSpawn} red={job.Layout.RedSpawn} blue={job.Layout.BlueSpawn}");
    }

    private static ArenaGeometryConfig RoundTrip(ArenaGeometryConfig geometry)
    {
        using MemoryStream stream = new();
        using (BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true)) geometry.Write(writer);
        stream.Position = 0;
        using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: true);
        ArenaGeometryConfig copy = ArenaGeometryConfig.Read(reader);
        foreach (System.Reflection.PropertyInfo property in typeof(ArenaGeometryConfig).GetProperties())
            if (!Equals(property.GetValue(geometry), property.GetValue(copy)))
                throw new InvalidOperationException($"Arena geometry packet round-trip changed {property.Name}: {property.GetValue(geometry)} -> {property.GetValue(copy)}");
        return copy;
    }

    private static ArenaGeometryConfig CreateResizedGeometry(ArenaGeneratorKind kind)
    {
        ArenaGeometryConfig geometry = ArenaGeometryDefaults.Create(kind);
        geometry.WorldWidth = 900; geometry.WorldHeight = 650;
        geometry.ArenaLeft = 30; geometry.ArenaRight = 870; geometry.ArenaTop = 50; geometry.ArenaBottom = 620;
        geometry.BossAreaX += 25; geometry.BossAreaY += 20;
        geometry.BlueBorderX += 25; geometry.RedBorderX += 25;
        geometry.RedSpawnX += 25; geometry.RedSpawnY += 20;
        geometry.BlueSpawnX += 25; geometry.BlueSpawnY += 20;
        geometry.BossSpawnX += 25; geometry.BossSpawnY += 20;
        geometry.AutoPlaceBossSpawn = false;
        return geometry;
    }

    private static void WriteMapDump(string directory, ArenaGeneratorKind kind, int seed, ArenaLayout layout)
    {
        string path = Path.Combine(directory, $"{kind}-{seed}.ppm");
        using FileStream stream = File.Create(path);
        byte[] header = Encoding.ASCII.GetBytes($"P6\n{Main.maxTilesX} {Main.maxTilesY}\n255\n");
        stream.Write(header);
        for (int y = 0; y < Main.maxTilesY; y++)
            for (int x = 0; x < Main.maxTilesX; x++)
            {
                (byte r, byte g, byte b) = MapColor(Main.tile[x, y], y, layout, x);
                stream.WriteByte(r);
                stream.WriteByte(g);
                stream.WriteByte(b);
            }
        Log.Debug($"[WorldGenSelfTest] WROTE map sample {path}");
    }

    private static (byte R, byte G, byte B) MapColor(Tile tile, int y, ArenaLayout layout, int x)
    {
        if (Math.Abs(x - layout.BossSpawn.X) <= 2 && Math.Abs(y - layout.BossSpawn.Y) <= 2) return (255, 225, 50);
        if (Math.Abs(x - layout.RedSpawn.X) <= 2 && Math.Abs(y - layout.RedSpawn.Y) <= 2) return (255, 45, 45);
        if (Math.Abs(x - layout.BlueSpawn.X) <= 2 && Math.Abs(y - layout.BlueSpawn.Y) <= 2) return (45, 125, 255);
        if (tile.HasTile)
        {
            ushort type = tile.TileType;
            if (type == TileID.LihzahrdBrick) return (173, 70, 24);
            if (type == TileID.JungleGrass) return (35, 151, 52);
            if (type == TileID.Mud) return (91, 65, 51);
            if (type == TileID.Hive) return (223, 158, 43);
            if (type == TileID.Grass) return (59, 151, 64);
            if (type == TileID.Dirt) return (126, 89, 57);
            if (type == TileID.Stone) return (112, 112, 122);
            if (type is TileID.SnowBlock or TileID.Cloud) return (226, 235, 242);
            if (type is TileID.IceBlock or TileID.BreakableIce) return (95, 169, 224);
            if (TileID.Sets.Conversion.Sand[type]) return (218, 188, 108);
            if (TileID.Sets.IsATreeTrunk[type]) return (112, 76, 40);
            return (125, 118, 112);
        }
        if (tile.LiquidAmount > 0)
            return tile.LiquidType switch { 1 => (241, 82, 30), 2 => (226, 167, 36), 3 => (182, 90, 224), _ => (31, 95, 205) };
        if (tile.WallType == WallID.LihzahrdBrickUnsafe) return (82, 35, 18);
        if (tile.WallType == WallID.JungleUnsafe) return (28, 65, 35);
        return y < Main.worldSurface ? ((byte)67, (byte)128, (byte)180) : ((byte)10, (byte)16, (byte)23);
    }
}
