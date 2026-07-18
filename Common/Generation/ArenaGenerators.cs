using Arenas.Core.Configs.ConfigElements;
using Newtonsoft.Json.Linq;
using ReLogic.Utilities;
using System;
using System.Collections.Generic;
using Terraria.GameContent.Biomes;
using Terraria.Enums;
using Terraria.ID;
using Terraria.IO;
using Terraria.Utilities;
using Terraria.WorldBuilding;

namespace Arenas.Common.Generation;

internal readonly record struct ArenaTileData(bool HasTile, ushort TileType, ushort WallType = 0, SlopeType Slope = SlopeType.Solid)
{
    public static ArenaTileData Air(ushort wall = 0) => new(false, 0, wall);
    public static ArenaTileData Solid(ushort tile, ushort wall = 0, SlopeType slope = SlopeType.Solid) => new(true, tile, wall, slope);
}

internal interface IArenaGenerator
{
    ArenaGeneratorKind Kind { get; }
    ArenaLayout CreateLayout(int seed);
    void Prepare(ArenaLayout layout);
    ArenaTileData GetTile(ArenaLayout layout, int x, int y);
    double WorldSurface { get; }
    double RockLayer { get; }
    bool UsesVanillaGeneration => false;
    void GenerateVanilla(ArenaLayout layout) { }
    void PlaceCombatStructures(ArenaLayout layout) { }
}

internal static class ArenaGeneratorRegistry
{
    public const int OuterBorderThickness = 3;
    public static readonly Point WorldSpawn = new(425, 300);
    public static readonly Rectangle StagingLobby = new(390, 270, 70, 60);
    public static readonly Rectangle ArenaArea = new(28, 48, 794, 524);

    private static readonly Dictionary<ArenaGeneratorKind, IArenaGenerator> Generators = new()
    {
        [ArenaGeneratorKind.KingSlimeSurface] = new SurfaceArenaGenerator(ArenaGeneratorKind.KingSlimeSurface),
        [ArenaGeneratorKind.EyeSurface] = new SurfaceArenaGenerator(ArenaGeneratorKind.EyeSurface),
        [ArenaGeneratorKind.PlanteraJungle] = new PlanteraArenaGenerator(),
        [ArenaGeneratorKind.GolemTemple] = new GolemArenaGenerator()
    };

    public static bool TryResolve(BossFightPreset preset, out IArenaGenerator generator)
    {
        ArenaGeneratorKind kind = ResolveKind(preset);
        return Generators.TryGetValue(kind, out generator);
    }

    public static IArenaGenerator Emergency { get; } = new EmergencyFlatArenaGenerator();

    public static ArenaGeneratorKind ResolveKind(BossFightPreset preset)
    {
        if (preset == null)
            return ArenaGeneratorKind.Auto;
        if (preset.ArenaGenerator != ArenaGeneratorKind.Auto)
            return preset.ArenaGenerator;

        return preset.Boss?.Type switch
        {
            NPCID.KingSlime => ArenaGeneratorKind.KingSlimeSurface,
            NPCID.EyeofCthulhu => ArenaGeneratorKind.EyeSurface,
            NPCID.Plantera => ArenaGeneratorKind.PlanteraJungle,
            NPCID.Golem => ArenaGeneratorKind.GolemTemple,
            _ => ArenaGeneratorKind.Auto
        };
    }

    internal static ArenaLayout Layout(ArenaGeneratorKind kind, int seed, Rectangle boss, Rectangle redClearance, Rectangle blueClearance, Point red, Point blue, Point bossSpawn)
    {
        return new ArenaLayout
        {
            Generator = kind, Seed = seed, ArenaArea = ArenaArea, BossArea = boss,
            RedSpawnClearance = redClearance, BlueSpawnClearance = blueClearance, RedSpawn = red, BlueSpawn = blue, BossSpawn = bossSpawn,
            StagingLobby = StagingLobby
        };
    }
}

internal sealed class EmergencyFlatArenaGenerator : IArenaGenerator
{
    public ArenaGeneratorKind Kind => ArenaGeneratorKind.KingSlimeSurface;
    public double WorldSurface => 520;
    public double RockLayer => 550;

    public ArenaLayout CreateLayout(int seed) => ArenaGeneratorRegistry.Layout(Kind, seed,
        new Rectangle(325, 240, 200, 260),
        new Rectangle(118, 452, 65, 48), new Rectangle(667, 452, 65, 48),
        new Point(150, 499), new Point(699, 499), new Point(425, 450));

    public void Prepare(ArenaLayout layout) { }

    public ArenaTileData GetTile(ArenaLayout layout, int x, int y)
    {
        if (!layout.ArenaArea.Contains(x, y) || layout.BossArea.Contains(x, y) || layout.RedSpawnClearance.Contains(x, y))
            return ArenaTileData.Air();
        if (y == 420 && x is >= 175 and <= 300)
            return ArenaTileData.Solid(TileID.Platforms);
        if (y < 500)
            return ArenaTileData.Air();
        return ArenaTileData.Solid(y == 500 ? TileID.Grass : TileID.Dirt);
    }
}

internal sealed class SurfaceArenaGenerator(ArenaGeneratorKind kind) : IArenaGenerator
{
    private readonly int[] heights = new int[425];
    private readonly int[] platformShifts = new int[3];
    public ArenaGeneratorKind Kind => kind;
    public double WorldSurface => 520;
    public double RockLayer => 550;
    public bool UsesVanillaGeneration => true;

    public ArenaLayout CreateLayout(int seed) => kind == ArenaGeneratorKind.EyeSurface
        ? ArenaGeneratorRegistry.Layout(kind, seed, new Rectangle(285, 140, 280, 360), new Rectangle(118, 452, 65, 48), new Rectangle(667, 452, 65, 48), new Point(150, 499), new Point(699, 499), new Point(425, 270))
        : ArenaGeneratorRegistry.Layout(kind, seed, new Rectangle(325, 240, 200, 260), new Rectangle(118, 452, 65, 48), new Rectangle(667, 452, 65, 48), new Point(150, 499), new Point(699, 499), new Point(425, 450));

    public void Prepare(ArenaLayout layout)
    {
        int[] vanillaProfile = VanillaArenaPasses.CreateSurfaceProfile(layout.Seed, kind == ArenaGeneratorKind.EyeSurface);
        Array.Copy(vanillaProfile, heights, heights.Length);
        Random random = new(layout.Seed ^ 0x5A17C9);
        for (int i = 0; i < platformShifts.Length; i++) platformShifts[i] = random.Next(-20, 21);
    }

    public ArenaTileData GetTile(ArenaLayout layout, int x, int y)
    {
        if (!layout.ArenaArea.Contains(x, y)) return ArenaTileData.Air();
        if (layout.BossArea.Contains(x, y) || layout.RedSpawnClearance.Contains(x, y)) return ArenaTileData.Air();

        if (IsPlatform(x, y)) return ArenaTileData.Solid(TileID.Platforms);
        int surface = heights[Math.Clamp(x, 0, heights.Length - 1)];
        if (y < surface) return ArenaTileData.Air();
        return ArenaTileData.Solid(y < 545 ? TileID.Dirt : TileID.Stone);
    }

    public void GenerateVanilla(ArenaLayout layout)
    {
        UnifiedRandom previousRandom = WorldGen.genRand;
        bool previousGenerating = WorldGen.gen;
        double previousSurface = Main.worldSurface, previousRockLayer = Main.rockLayer;
        double previousGenSurface = GenVars.worldSurface, previousGenSurfaceLow = GenVars.worldSurfaceLow;
        double previousGenSurfaceHigh = GenVars.worldSurfaceHigh, previousGenRock = GenVars.rockLayer;
        WorldGen._genRand = new UnifiedRandom(layout.Seed ^ 0x314159);
        WorldGen.gen = true;
        try
        {
            Main.worldSurface = GenVars.worldSurface = 520;
            GenVars.worldSurfaceLow = 488; GenVars.worldSurfaceHigh = 506;
            Main.rockLayer = GenVars.rockLayer = 550;
            VanillaArenaPasses.SmoothWorld(layout.ArenaArea);
            VanillaArenaPasses.SpreadingGrass(layout.ArenaArea);
            ClearLeftArea(layout.BossArea);
        }
        finally
        {
            Main.worldSurface = previousSurface; Main.rockLayer = previousRockLayer;
            GenVars.worldSurface = previousGenSurface; GenVars.worldSurfaceLow = previousGenSurfaceLow;
            GenVars.worldSurfaceHigh = previousGenSurfaceHigh; GenVars.rockLayer = previousGenRock;
            WorldGen.gen = previousGenerating;
            WorldGen._genRand = previousRandom;
        }
    }

    private bool IsPlatform(int x, int y)
    {
        if (kind == ArenaGeneratorKind.KingSlimeSurface)
            return y == 420 && x is >= 175 and <= 300;
        return (y == 410 && x >= 80 + platformShifts[0] && x <= 250 + platformShifts[0])
            || (y == 330 && x >= 190 + platformShifts[1] && x <= 365 + platformShifts[1])
            || (y == 250 && x >= 80 + platformShifts[2] && x <= 230 + platformShifts[2]);
    }

    private static void ClearLeftArea(Rectangle area)
    {
        for (int x = area.Left; x < Math.Min(425, area.Right); x++)
            for (int y = area.Top; y < area.Bottom; y++)
            {
                Tile tile = Main.tile[x, y];
                tile.ClearTile();
                tile.LiquidAmount = 0;
            }
    }
}

internal sealed class PlanteraArenaGenerator : IArenaGenerator
{
    public ArenaGeneratorKind Kind => ArenaGeneratorKind.PlanteraJungle;
    public double WorldSurface => 110;
    public double RockLayer => 170;
    public bool UsesVanillaGeneration => true;

    public ArenaLayout CreateLayout(int seed) => ArenaGeneratorRegistry.Layout(Kind, seed, new Rectangle(285, 150, 280, 320), new Rectangle(118, 282, 65, 48), new Rectangle(667, 282, 65, 48), new Point(150, 329), new Point(699, 329), new Point(425, 300));

    public void Prepare(ArenaLayout layout) { }

    public ArenaTileData GetTile(ArenaLayout layout, int x, int y)
    {
        if (!layout.ArenaArea.Contains(x, y)) return ArenaTileData.Air();
        if (y < 120) return ArenaTileData.Air();
        return ArenaTileData.Solid(y < 170 ? TileID.Dirt : TileID.Stone);
    }

    public void GenerateVanilla(ArenaLayout layout)
    {
        UnifiedRandom previousRandom = WorldGen.genRand;
        bool previousGenerating = WorldGen.gen;
        WorldGen._genRand = new UnifiedRandom(layout.Seed);
        WorldGen.gen = true;
        double previousSurface = Main.worldSurface, previousRockLayer = Main.rockLayer;
        int previousJungleOrigin = GenVars.jungleOriginX, previousLeftBeach = GenVars.leftBeachEnd;
        int previousRightBeach = GenVars.rightBeachStart, previousDungeonSide = GenVars.dungeonSide;
        int previousWaterLine = GenVars.waterLine, previousLavaLine = GenVars.lavaLine;
        double previousGenSurface = GenVars.worldSurface, previousGenSurfaceLow = GenVars.worldSurfaceLow;
        double previousGenSurfaceHigh = GenVars.worldSurfaceHigh, previousGenRock = GenVars.rockLayer;
        double previousGenRockLow = GenVars.rockLayerLow, previousGenRockHigh = GenVars.rockLayerHigh;
        int previousWorldWidth = Main.maxTilesX;
        try
        {
            Main.worldSurface = GenVars.worldSurface = 110;
            GenVars.worldSurfaceLow = 100; GenVars.worldSurfaceHigh = 120;
            Main.rockLayer = GenVars.rockLayer = 170;
            GenVars.rockLayerLow = 155; GenVars.rockLayerHigh = 185;
            GenVars.jungleOriginX = 225;
            GenVars.leftBeachEnd = 20;
            GenVars.rightBeachStart = 424;
            GenVars.dungeonSide = (layout.Seed & 1) == 0 ? 1 : -1;
            GenVars.waterLine = 320;
            GenVars.lavaLine = 400;

            // Run Terraria's actual Jungle generation pass as a 425-tile source world. This makes every
            // internal vanilla search/range stay on the generated half before it is mirrored.
            Main.maxTilesX = 425;
            new JunglePass().Apply(new GenerationProgress(), new GameConfiguration(new JObject()));
            Main.maxTilesX = previousWorldWidth;

            VanillaArenaPasses.MudCavesToGrass(layout.ArenaArea);
            VanillaArenaPasses.SmoothWorld(layout.ArenaArea);
            VanillaArenaPasses.SpreadingGrass(layout.ArenaArea);
            VanillaArenaPasses.MudWallsInJungle(layout.ArenaArea);
            ClearLeftLiquids(layout.ArenaArea);

            ClearLeftArea(layout.RedSpawnClearance);
            ClearLeftArea(layout.BossArea);
        }
        finally
        {
            Main.maxTilesX = previousWorldWidth;
            Main.worldSurface = previousSurface; Main.rockLayer = previousRockLayer;
            GenVars.jungleOriginX = previousJungleOrigin; GenVars.leftBeachEnd = previousLeftBeach;
            GenVars.rightBeachStart = previousRightBeach; GenVars.dungeonSide = previousDungeonSide;
            GenVars.waterLine = previousWaterLine; GenVars.lavaLine = previousLavaLine;
            GenVars.worldSurface = previousGenSurface; GenVars.worldSurfaceLow = previousGenSurfaceLow;
            GenVars.worldSurfaceHigh = previousGenSurfaceHigh; GenVars.rockLayer = previousGenRock;
            GenVars.rockLayerLow = previousGenRockLow; GenVars.rockLayerHigh = previousGenRockHigh;
            WorldGen.gen = previousGenerating;
            WorldGen._genRand = previousRandom;
        }
    }

    public void PlaceCombatStructures(ArenaLayout layout)
    {
        PlaceMirroredPlatform(92, 255, 395);
        PlaceMirroredPlatform(190, 360, 330);
        PlaceMirroredPlatform(305, 400, 245);
    }

    private static void ClearLeftArea(Rectangle area)
    {
        for (int x = area.Left; x < Math.Min(425, area.Right); x++)
            for (int y = area.Top; y < area.Bottom; y++)
            {
                Main.tile[x, y].ClearTile();
                Main.tile[x, y].LiquidAmount = 0;
            }
    }

    private static void ClearLeftLiquids(Rectangle area)
    {
        for (int x = area.Left; x < Math.Min(425, area.Right); x++)
            for (int y = area.Top; y < area.Bottom; y++)
                Main.tile[x, y].LiquidAmount = 0;
    }

    private static void PlaceMirroredPlatform(int startX, int endX, int y)
    {
        for (int x = startX; x <= endX; x++)
        {
            SetPlatform(x, y);
            SetPlatform(ArenaLayout.MirrorRight - x, y);
        }
    }

    private static void SetPlatform(int x, int y)
    {
        Tile tile = Main.tile[x, y];
        tile.ClearTile();
        tile.HasTile = true;
        tile.TileType = TileID.Platforms;
    }
}

internal sealed class GolemArenaGenerator : IArenaGenerator
{
    public ArenaGeneratorKind Kind => ArenaGeneratorKind.GolemTemple;
    public double WorldSurface => 110;
    public double RockLayer => 170;
    public bool UsesVanillaGeneration => true;

    public ArenaLayout CreateLayout(int seed) => ArenaGeneratorRegistry.Layout(Kind, seed, new Rectangle(305, 270, 240, 230), new Rectangle(118, 452, 65, 48), new Rectangle(667, 452, 65, 48), new Point(150, 499), new Point(699, 499), new Point(425, 440));
    public void Prepare(ArenaLayout layout) { }

    public ArenaTileData GetTile(ArenaLayout layout, int x, int y)
    {
        if (!layout.ArenaArea.Contains(x, y)) return ArenaTileData.Air();
        return ArenaTileData.Solid(TileID.LihzahrdBrick, WallID.LihzahrdBrickUnsafe);
    }

    public void GenerateVanilla(ArenaLayout layout)
    {
        UnifiedRandom previousRandom = WorldGen.genRand;
        bool previousGenerating = WorldGen.gen;
        WorldGen._genRand = new UnifiedRandom(layout.Seed);
        WorldGen.gen = true;
        try
        {
            WorldGen.makeTemple(205, 155);
            Vector2D path = new(layout.RedSpawn.X, layout.RedSpawn.Y - 10);
            int destinationX = layout.BossArea.Left + 28;
            int destinationY = layout.BossArea.Center.Y;
            for (int guard = 0; guard < 80 && ((int)path.X != destinationX || (int)path.Y != destinationY); guard++)
                path = WorldGen.templePather(path, destinationX, destinationY);

            VanillaArenaPasses.SmoothWorld(layout.ArenaArea);

            for (int x = layout.ArenaArea.Left; x < 425; x++)
                for (int y = layout.ArenaArea.Top; y < layout.ArenaArea.Bottom; y++)
                {
                    Tile tile = Main.tile[x, y];
                    if (tile.HasTile && tile.TileType is TileID.LihzahrdAltar or TileID.WoodenSpikes or TileID.ClosedDoor or TileID.OpenDoor)
                        tile.ClearTile();
                    tile.LiquidAmount = 0;
                    tile.RedWire = tile.BlueWire = tile.GreenWire = tile.YellowWire = false;
                    tile.HasActuator = tile.IsActuated = false;
                }
            ClearLeftArea(layout.BossArea);
        }
        finally
        {
            WorldGen.gen = previousGenerating;
            WorldGen._genRand = previousRandom;
        }
    }

    public void PlaceCombatStructures(ArenaLayout layout)
    {
        int y = layout.BossArea.Bottom - 1;
        for (int x = layout.BossArea.Left; x < layout.BossArea.Right; x++)
        {
            Tile tile = Main.tile[x, y];
            tile.ClearTile();
            tile.HasTile = true;
            tile.TileType = TileID.LihzahrdBrick;
            tile.WallType = WallID.LihzahrdBrickUnsafe;
        }
    }

    private static void ClearLeftArea(Rectangle area)
    {
        for (int x = area.Left; x < Math.Min(425, area.Right); x++)
            for (int y = area.Top; y < area.Bottom; y++)
            {
                Tile tile = Main.tile[x, y];
                tile.ClearTile();
                tile.LiquidAmount = 0;
                tile.RedWire = tile.BlueWire = tile.GreenWire = tile.YellowWire = false;
                tile.HasActuator = tile.IsActuated = false;
            }
    }
}
