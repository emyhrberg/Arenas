using Arenas.Core.Configs.ConfigElements;
using System;
using Terraria.GameContent.Biomes;
using Terraria.ID;
using Terraria.Utilities;
using Terraria.WorldBuilding;

namespace Arenas.Common.Generation;

/// <summary>Builds a compact but biome-complete surface world from Terraria's own generation passes and structure methods.</summary>
internal static class VanillaSurfaceWorldGenerator
{
    public static (double WorldSurface, double RockLayer) Generate(ArenaLayout layout, int seed, ArenaGeneratorKind kind)
    {
        UnifiedRandom previousRandom = WorldGen.genRand;
        bool previousGenerating = WorldGen.gen, previousNoActions = WorldGen.noTileActions;
        bool previousDontStarve = WorldGen.dontStarveWorldGen, previousRemix = WorldGen.remixWorldGen;
        bool previousDrunk = WorldGen.drunkWorldGen, previousGood = WorldGen.getGoodWorldGen;
        bool previousNotBees = WorldGen.notTheBees, previousTenth = WorldGen.tenthAnniversaryWorldGen;
        bool previousMainDrunk = Main.drunkWorld, previousMainGood = Main.getGoodWorld, previousMainRemix = Main.remixWorld;
        bool previousMainTenth = Main.tenthAnniversaryWorld, previousMainNotBees = Main.notTheBeesWorld, previousMainDontStarve = Main.dontStarveWorld;
        double previousSurface = Main.worldSurface, previousRock = Main.rockLayer;
        WorldGenConfiguration previousConfiguration = GenVars.configuration;
        double generatedSurface = 150, generatedRock = 300;

        try
        {
            WorldGen.gen = WorldGen.noTileActions = true;
            WorldGen.dontStarveWorldGen = WorldGen.remixWorldGen = WorldGen.drunkWorldGen = false;
            WorldGen.getGoodWorldGen = WorldGen.notTheBees = WorldGen.tenthAnniversaryWorldGen = false;
            Main.drunkWorld = Main.getGoodWorld = Main.remixWorld = false;
            Main.tenthAnniversaryWorld = Main.notTheBeesWorld = Main.dontStarveWorld = false;
            GenVars.configuration = VanillaGenPassRunner.Configuration;
            GenVars.structures = new StructureMap();
            GenVars.dungeonSide = (seed & 1) == 0 ? 1 : -1;
            int coastEnd = Math.Clamp(Main.maxTilesX * 13 / 100, 80, 180);
            GenVars.leftBeachEnd = coastEnd;
            GenVars.rightBeachStart = Main.maxTilesX - coastEnd;
            GenVars.smallHolesBeachAvoidance = 20;
            GenVars.surfaceCavesBeachAvoidance = 20;
            GenVars.surfaceCavesBeachAvoidance2 = 20;
            GenVars.lakesBeachAvoidance = 40;
            GenVars.oceanWaterStartRandomMin = 60;
            GenVars.oceanWaterStartRandomMax = 80;
            GenVars.oceanWaterForcedJungleLength = 70;
            GenVars.PyrX = new int[100];
            GenVars.PyrY = new int[100];
            GenVars.numPyr = 0;
            GenVars.numTunnels = GenVars.numMCaves = GenVars.numLakes = 0;
            GenVars.skipDesertTileCheck = false;

            Run("Terrain", seed);
            generatedSurface = Main.worldSurface;
            generatedRock = Main.rockLayer;
            ConfigureCompactBiomes(seed, coastEnd);
            ArenaGenerationDiagnostics.LogSnapshot("Surface terrain", layout);

            Run("Ocean Sand", seed);
            Run("Dirt Wall Backgrounds", seed);
            Run("Rocks In Dirt", seed);
            Run("Dirt In Rocks", seed);
            Run("Clay", seed);
            Run("Small Holes", seed);
            Run("Dirt Layer Caves", seed);
            Run("Rock Layer Caves", seed);
            Run("Surface Caves", seed);
            ArenaGenerationDiagnostics.LogSnapshot("Surface caves", layout);

            RunCompactIceBiome(seed);
            Run("Grass", seed);
            RunCompactDesert(seed);
            PlaceVanillaFloatingIslands(layout, seed);
            Run("Beaches", seed);
            ArenaGenerationDiagnostics.LogSnapshot("Surface biomes", layout);

            Run("Clean Up Dirt", seed);
            Run("Settle Liquids", seed);
            Run("Smooth World", seed);
            Run("Ice", seed);
            ResolveCombatAnchors(layout, kind);

            Run("Sunflowers", seed);
            Run("Planting Trees", seed);
            Run("Herbs", seed);
            Run("Weeds", seed);
            // Vanilla Flowers assumes normal-world padding while scanning its 30x30 patches and can
            // index outside a compact Tilemap. It is purely decorative; every terrain/biome pass stays exact.
            Log.Debug("[WorldGen2.Surface] SKIP Flowers: vanilla patch scan is not compact-world safe");
            Run("Mushrooms", seed);
            ArenaGenerationDiagnostics.LogSnapshot("Finished surface world", layout);
            return (generatedSurface, generatedRock);
        }
        finally
        {
            Main.worldSurface = previousSurface;
            Main.rockLayer = previousRock;
            GenVars.configuration = previousConfiguration;
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
            WorldGen.noTileActions = previousNoActions;
            WorldGen.gen = previousGenerating;
            WorldGen._genRand = previousRandom;
        }
    }

    private static void ConfigureCompactBiomes(int seed, int coastEnd)
    {
        bool desertOnLeft = GenVars.dungeonSide > 0;
        // The vanilla ice pass drifts both edges toward the dungeon side for hundreds of rows.
        // Normal worlds have thousands of tiles of side padding; compact worlds need the same band
        // nearer the center so vanilla's unguarded j/k loops remain inside the Tilemap.
        int snowWidth = Math.Clamp(Main.maxTilesX * 11 / 100, 80, 180);
        int snowCenter = (int)(Main.maxTilesX * (desertOnLeft ? .647 : .353));
        GenVars.snowOriginLeft = snowCenter - snowWidth / 2;
        GenVars.snowOriginRight = snowCenter + snowWidth / 2;
        GenVars.snowMinX = new int[Main.maxTilesY];
        GenVars.snowMaxX = new int[Main.maxTilesY];
        GenVars.snowTop = 0;
        GenVars.snowBottom = 0;
        Log.Debug($"[WorldGen2.Surface] Compact biome plan seed={seed}: desert={(desertOnLeft ? "left" : "right")}, snow={GenVars.snowOriginLeft}..{GenVars.snowOriginRight}, coasts=0..{coastEnd}/{Main.maxTilesX - coastEnd}..{Main.maxTilesX}");
    }

    /// <summary>
    /// Terraria's Generate Ice Biome pass copied for a compact Tilemap. The tile conversion and random
    /// walk are vanilla; only the X/Y iteration is clipped because the original assumes thousands of
    /// tiles of horizontal padding and performs no bounds checks.
    /// </summary>
    private static void RunCompactIceBiome(int seed)
    {
        UnifiedRandom random = new(seed);
        WorldGen._genRand = random;
        Main.rand = new UnifiedRandom(seed);
        GenVars.snowTop = (int)Main.worldSurface;
        int solidIceStart = GenVars.lavaLine - random.Next(160, 200);
        int bottom = Math.Min(Main.maxTilesY - 1, GenVars.lavaLine);
        int left = GenVars.snowOriginLeft, right = GenVars.snowOriginRight, fringeDepth = 10;
        int clippedColumns = 0, minEdge = left, maxEdge = right;
        Log.Debug($"[WorldGen2.Surface] START compact-safe vanilla GenPass 'Generate Ice Biome' seed={seed}");

        for (int y = 0; y <= bottom - 140; y++)
        {
            left += random.Next(-4, 4);
            right += random.Next(-3, 5);
            if (y > 0)
            {
                left = (left + GenVars.snowMinX[y - 1]) / 2;
                right = (right + GenVars.snowMaxX[y - 1]) / 2;
            }
            if (GenVars.dungeonSide > 0)
            {
                if (random.Next(4) == 0)
                    left++;
                if (random.Next(4) == 0)
                    right++;
            }
            else
            {
                if (random.Next(4) == 0)
                    left--;
                if (random.Next(4) == 0)
                    right--;
            }

            GenVars.snowMinX[y] = left;
            GenVars.snowMaxX[y] = right;
            minEdge = Math.Min(minEdge, left);
            maxEdge = Math.Max(maxEdge, right);
            int firstX = Math.Max(1, left), lastX = Math.Min(Main.maxTilesX - 1, right);
            clippedColumns += Math.Max(0, firstX - left) + Math.Max(0, right - lastX);
            for (int x = firstX; x < lastX; x++)
            {
                if (y < solidIceStart)
                {
                    ConvertToIce(x, y);
                    continue;
                }

                fringeDepth += random.Next(-3, 4);
                if (random.Next(3) == 0)
                {
                    fringeDepth += random.Next(-4, 5);
                    if (random.Next(3) == 0)
                        fringeDepth += random.Next(-6, 7);
                }
                fringeDepth = fringeDepth < 0 ? random.Next(3) : fringeDepth > 50 ? 50 - random.Next(3) : fringeDepth;
                for (int fringeY = y; fringeY < Math.Min(Main.maxTilesY, y + fringeDepth); fringeY++)
                    ConvertToIce(x, fringeY);
            }
            GenVars.snowBottom = Math.Max(GenVars.snowBottom, y);
        }

        Log.Debug($"[WorldGen2.Surface] END compact-safe vanilla GenPass 'Generate Ice Biome' rawEdges={minEdge}..{maxEdge} clippedColumns={clippedColumns}");
    }

    private static void RunCompactDesert(int seed)
    {
        UnifiedRandom random = new(seed);
        WorldGen._genRand = random;
        Main.rand = new UnifiedRandom(seed);
        Main.tileSolid[TileID.RollingCactus] = false;
        GenVars.skipDesertTileCheck = false;
        int side = GenVars.dungeonSide, halfWidth = Main.maxTilesX / 2;
        int offset = random.Next(halfWidth) / 8 + halfWidth / 8;
        int x = halfWidth + offset * -side;
        int attemptsOnSide = 0, sideFlips = 0, attempts = 0;
        DesertBiome desert = GenVars.configuration.CreateBiome<DesertBiome>();
        // The vanilla Chambers entrance selects center +/-40, but a compact world's scaled
        // SurfaceMap is only about 64 tiles wide. Keep the complete biome and omit only that unsafe entrance.
        desert.ChanceOfEntrance = 0;
        Log.Debug($"[WorldGen2.Surface] START compact-safe vanilla DesertBiome seed={seed} initialX={x} side={side}");
        while (!desert.Place(new Point(x, (int)GenVars.worldSurfaceHigh + 25), GenVars.structures))
        {
            offset = random.Next(halfWidth) / 2 + halfWidth / 8 + random.Next(attemptsOnSide / 12);
            x = halfWidth + offset * -side;
            attemptsOnSide++;
            attempts++;
            if (attemptsOnSide > Main.maxTilesX / 4)
            {
                side *= -1;
                attemptsOnSide = 0;
                sideFlips++;
                if (sideFlips >= 2)
                    GenVars.skipDesertTileCheck = true;
            }
            if (attempts > 50_000)
                throw new InvalidOperationException($"Vanilla DesertBiome rejected 50000 compact placements; lastX={x}, side={side}, flips={sideFlips}, surfaceHigh={GenVars.worldSurfaceHigh:F1}");
        }
        Log.Debug($"[WorldGen2.Surface] END compact-safe vanilla DesertBiome x={x} attempts={attempts} area={GenVars.UndergroundDesertLocation}");
    }

    private static void ConvertToIce(int x, int y)
    {
        Tile tile = Main.tile[x, y];
        if (tile.WallType == WallID.DirtUnsafe)
            tile.WallType = WallID.SnowWallUnsafe;
        switch (tile.TileType)
        {
            case TileID.Dirt:
            case TileID.Grass:
            case TileID.CorruptGrass:
            case TileID.JungleVines:
            case TileID.Sand:
                tile.TileType = TileID.SnowBlock;
                break;
            case TileID.Stone:
                tile.TileType = TileID.IceBlock;
                break;
        }
    }

    private static void PlaceVanillaFloatingIslands(ArenaLayout layout, int seed)
    {
        WorldGen._genRand = new UnifiedRandom(seed ^ 0x49534C44);
        int y = Math.Clamp((int)GenVars.worldSurfaceLow - 25, layout.ArenaArea.Top + 18, layout.ArenaArea.Top + 37);
        int leftX = layout.ArenaArea.Left + layout.ArenaArea.Width * 30 / 100;
        int rightX = layout.ArenaArea.Left + layout.ArenaArea.Width * 70 / 100;
        Log.Debug($"[WorldGen2.Surface] START vanilla FloatingIsland structures at ({leftX},{y}) and ({rightX},{y + 5})");
        WorldGen.FloatingIsland(leftX, y);
        WorldGen.FloatingIsland(rightX, y + 5);
        Log.Debug("[WorldGen2.Surface] END vanilla FloatingIsland structures");
    }

    private static void ResolveCombatAnchors(ArenaLayout layout, ArenaGeneratorKind kind)
    {
        int redGround = -1, blueGround = -1, bossGround = -1;
        if (layout.AutoPlaceTeamSpawns)
        {
            redGround = FindSurfaceGround(layout, layout.RedSpawn.X);
            blueGround = FindSurfaceGround(layout, layout.BlueSpawn.X);
            layout.RedSpawn = new Point(layout.RedSpawn.X, redGround - 1);
            layout.BlueSpawn = new Point(layout.BlueSpawn.X, blueGround - 1);
            layout.RedSpawnClearance = SpawnRoom(layout.RedSpawn, new Point(layout.RedSpawnClearance.Width, layout.RedSpawnClearance.Height));
            layout.BlueSpawnClearance = SpawnRoom(layout.BlueSpawn, new Point(layout.BlueSpawnClearance.Width, layout.BlueSpawnClearance.Height));
        }
        if (layout.AutoPlaceBossSpawn)
        {
            bossGround = FindSurfaceGround(layout, layout.BossSpawn.X);
            int verticalOffset = kind == ArenaGeneratorKind.EyeSurface ? 65 : 35;
            layout.BossSpawn = new Point(layout.BossSpawn.X, Math.Clamp(bossGround - verticalOffset, layout.BossArea.Top + 8, layout.BossArea.Bottom - 8));
        }
        Log.Debug($"[WorldGen2.Surface] Resolved anchors red={layout.RedSpawn} blue={layout.BlueSpawn} boss={layout.BossSpawn} ground={redGround}/{blueGround}/{bossGround} autoTeams={layout.AutoPlaceTeamSpawns} autoBoss={layout.AutoPlaceBossSpawn}");
    }

    private static Rectangle SpawnRoom(Point spawn, Point size) => ArenaGeneratorRegistry.SpawnRoom(spawn, size.X, size.Y);

    private static int FindSurfaceGround(ArenaLayout layout, int x)
    {
        if (x <= layout.ArenaArea.Left || x >= layout.ArenaArea.Right)
            throw new InvalidOperationException($"Automatic surface spawn X {x} lies outside arena {layout.ArenaArea}");
        for (int y = layout.ArenaArea.Top + 5; y < Math.Min(Main.maxTilesY - 20, layout.ArenaArea.Bottom - 5); y++)
        {
            Tile tile = Main.tile[x, y];
            if (!tile.HasTile || !Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType])
                continue;
            bool open = true;
            for (int above = 1; above <= 4; above++)
                if (Main.tile[x, y - above].HasTile)
                {
                    open = false;
                    break;
                }
            if (open)
                return y;
        }
        throw new InvalidOperationException($"No valid surface ground was generated at x={x}; Terrain/Grass/biome passes did not produce a usable combat surface");
    }

    private static void Run(string name, int seed) => VanillaGenPassRunner.Run("WorldGen2.Surface", name, seed);
}
