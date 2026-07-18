using System;
using Terraria.Utilities;
using Terraria.WorldBuilding;

namespace Arenas.Common.Generation;

/// <summary>
/// Adapts Terraria's registered vanilla passes to a compact, all-Jungle arena. The pass bodies are the
/// implementations from the installed Terraria/tModLoader build; only their global inputs are scoped here.
/// </summary>
internal static class VanillaJungleGenerator
{
    public static (double WorldSurface, double RockLayer) GenerateLeftHalf(ArenaLayout layout, int seed, bool carveBossPocket)
    {
        int sourceWidth = layout.MirrorRightX;
        UnifiedRandom previousRandom = WorldGen.genRand;
        bool previousGenerating = WorldGen.gen;
        int previousWidth = Main.maxTilesX;
        double previousSurface = Main.worldSurface, previousRockLayer = Main.rockLayer;
        double previousGenSurface = GenVars.worldSurface, previousSurfaceLow = GenVars.worldSurfaceLow, previousSurfaceHigh = GenVars.worldSurfaceHigh;
        double previousGenRock = GenVars.rockLayer, previousRockLow = GenVars.rockLayerLow, previousRockHigh = GenVars.rockLayerHigh;
        int previousJungleOrigin = GenVars.jungleOriginX, previousLeftBeach = GenVars.leftBeachEnd, previousRightBeach = GenVars.rightBeachStart;
        int previousDungeonSide = GenVars.dungeonSide, previousWaterLine = GenVars.waterLine, previousLavaLine = GenVars.lavaLine;
        int previousSmallHoleAvoidance = GenVars.smallHolesBeachAvoidance, previousSurfaceCaveAvoidance = GenVars.surfaceCavesBeachAvoidance;
        int previousSurfaceCaveAvoidance2 = GenVars.surfaceCavesBeachAvoidance2;
        bool previousDontStarve = WorldGen.dontStarveWorldGen, previousRemix = WorldGen.remixWorldGen;
        bool previousDrunk = WorldGen.drunkWorldGen, previousGood = WorldGen.getGoodWorldGen;
        bool previousNotBees = WorldGen.notTheBees, previousTenth = WorldGen.tenthAnniversaryWorldGen;
        bool previousMainDrunk = Main.drunkWorld, previousMainGood = Main.getGoodWorld, previousMainRemix = Main.remixWorld;
        bool previousMainTenth = Main.tenthAnniversaryWorld, previousMainNotBees = Main.notTheBeesWorld, previousMainDontStarve = Main.dontStarveWorld;
        WorldGenConfiguration previousConfiguration = GenVars.configuration;

        try
        {
            WorldGen._genRand = new UnifiedRandom(seed);
            WorldGen.gen = true;
            WorldGen.dontStarveWorldGen = WorldGen.remixWorldGen = WorldGen.drunkWorldGen = false;
            WorldGen.getGoodWorldGen = WorldGen.notTheBees = WorldGen.tenthAnniversaryWorldGen = false;
            Main.drunkWorld = Main.getGoodWorld = Main.remixWorld = false;
            Main.tenthAnniversaryWorld = Main.notTheBeesWorld = Main.dontStarveWorld = false;
            GenVars.configuration = VanillaGenPassRunner.Configuration;
            Main.maxTilesX = sourceWidth;
            double surface = Math.Clamp(layout.WorldHeight * 11d / 60d, 80d, layout.WorldHeight - 350d);
            double rock = Math.Clamp(layout.WorldHeight * 17d / 60d, surface + 40d, layout.WorldHeight - 250d);
            Main.worldSurface = GenVars.worldSurface = surface;
            GenVars.worldSurfaceLow = surface - 10;
            GenVars.worldSurfaceHigh = surface + 10;
            Main.rockLayer = GenVars.rockLayer = rock;
            GenVars.rockLayerLow = rock - 15;
            GenVars.rockLayerHigh = rock + 15;
            GenVars.jungleOriginX = sourceWidth * 53 / 100;
            GenVars.leftBeachEnd = GenVars.smallHolesBeachAvoidance = GenVars.surfaceCavesBeachAvoidance = GenVars.surfaceCavesBeachAvoidance2 = 20;
            GenVars.rightBeachStart = sourceWidth - 1;
            GenVars.dungeonSide = (seed & 1) == 0 ? 1 : -1;
            GenVars.waterLine = layout.WorldHeight * 8 / 15;
            GenVars.lavaLine = layout.WorldHeight * 11 / 15;
            GenVars.structures = new StructureMap();
            GenVars.numLarva = 0;

            Run("Small Holes", seed);
            Run("Dirt Layer Caves", seed);
            Run("Rock Layer Caves", seed);
            Run("Surface Caves", seed);
            ArenaGenerationDiagnostics.LogSnapshot("Jungle caves", layout);
            Run("Jungle", seed);
            Run("Mud Caves To Grass", seed);
            Run("Wet Jungle", seed);
            Run("Hives", seed);
            Run("Settle Liquids", seed);
            Run("Smooth World", seed);
            Run("Muds Walls In Jungle", seed);
            Run("Jungle Plants", seed);
            Run("Vines", seed);
            Run("Planting Trees", seed);
            RemoveUnmirrorableObjects(layout);
            ArenaGenerationDiagnostics.LogSnapshot("Finished Jungle", layout);

            if (carveBossPocket)
            {
                Log.Debug($"[WorldGen2.Jungle] Carving a small organic boss pocket at {layout.BossSpawn}");
                int sourceX = layout.BossSpawn.X < sourceWidth ? layout.BossSpawn.X : layout.MirrorRight - layout.BossSpawn.X;
                sourceX = Math.Clamp(sourceX, 12, sourceWidth - 1);
                WorldGen.TileRunner(Math.Min(sourceWidth - 12, sourceX), layout.BossSpawn.Y, 22, 12, -1);
                ClearBossPocket(sourceX, layout.BossSpawn.Y, sourceWidth);
            }
            return (surface, rock);
        }
        finally
        {
            Main.maxTilesX = previousWidth;
            Main.worldSurface = previousSurface;
            Main.rockLayer = previousRockLayer;
            GenVars.worldSurface = previousGenSurface;
            GenVars.worldSurfaceLow = previousSurfaceLow;
            GenVars.worldSurfaceHigh = previousSurfaceHigh;
            GenVars.rockLayer = previousGenRock;
            GenVars.rockLayerLow = previousRockLow;
            GenVars.rockLayerHigh = previousRockHigh;
            GenVars.jungleOriginX = previousJungleOrigin;
            GenVars.leftBeachEnd = previousLeftBeach;
            GenVars.rightBeachStart = previousRightBeach;
            GenVars.dungeonSide = previousDungeonSide;
            GenVars.waterLine = previousWaterLine;
            GenVars.lavaLine = previousLavaLine;
            GenVars.smallHolesBeachAvoidance = previousSmallHoleAvoidance;
            GenVars.surfaceCavesBeachAvoidance = previousSurfaceCaveAvoidance;
            GenVars.surfaceCavesBeachAvoidance2 = previousSurfaceCaveAvoidance2;
            WorldGen.dontStarveWorldGen = previousDontStarve;
            WorldGen.remixWorldGen = previousRemix;
            WorldGen.drunkWorldGen = previousDrunk;
            WorldGen.getGoodWorldGen = previousGood;
            WorldGen.notTheBees = previousNotBees;
            WorldGen.tenthAnniversaryWorldGen = previousTenth;
            Main.drunkWorld = previousMainDrunk;
            Main.getGoodWorld = previousMainGood;
            Main.remixWorld = previousMainRemix;
            Main.tenthAnniversaryWorld = previousMainTenth;
            Main.notTheBeesWorld = previousMainNotBees;
            Main.dontStarveWorld = previousMainDontStarve;
            GenVars.configuration = previousConfiguration;
            WorldGen.gen = previousGenerating;
            WorldGen._genRand = previousRandom;
        }
    }

    private static void RemoveUnmirrorableObjects(ArenaLayout layout)
    {
        int removed = 0;
        int right = Math.Min(layout.MirrorRightX, layout.ArenaArea.Right);
        for (int x = layout.ArenaArea.Left; x < right; x++)
            for (int y = layout.ArenaArea.Top; y < layout.ArenaArea.Bottom; y++)
            {
                Tile tile = Main.tile[x, y];
                if (!tile.HasTile || !Main.tileFrameImportant[tile.TileType])
                    continue;
                tile.ClearTile();
                removed++;
            }
        Log.Chat($"[WorldGen2.Jungle] Removed {removed} frame-important object tiles before mirroring; terrain, Hive blocks, plants, vines, walls and liquids remain vanilla-generated");
    }

    private static void ClearBossPocket(int centerX, int centerY, int sourceWidth)
    {
        const int radius = 12;
        for (int x = Math.Max(1, centerX - radius); x < Math.Min(sourceWidth, centerX + radius + 1); x++)
            for (int y = centerY - radius; y <= centerY + radius; y++)
            {
                double dx = x - centerX, dy = y - centerY;
                if (dx * dx + dy * dy > radius * radius)
                    continue;
                Tile tile = Main.tile[x, y];
                tile.ClearTile();
                tile.LiquidAmount = 0;
            }
        Log.Chat($"[WorldGen2.Jungle] Guaranteed compact boss pocket radius={radius} at source=({centerX},{centerY})");
    }

    private static void Run(string name, int seed) => VanillaGenPassRunner.Run("WorldGen2.Jungle", name, seed);
}
