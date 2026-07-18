using System;
using Terraria.Enums;
using Terraria.GameContent.Biomes;
using Terraria.ID;
using Terraria.Utilities;
using Terraria.WorldBuilding;

namespace Arenas.Common.Generation;

/// <summary>
/// Bounded ports of anonymous vanilla world-generation passes. Constants, ordering, random rolls, and
/// tile predicates match the installed Terraria implementation; only the iteration bounds are narrowed.
/// </summary>
internal static class VanillaArenaPasses
{
    public static int[] CreateSurfaceProfile(int seed, bool flat)
    {
        UnifiedRandom random = new(seed);
        int[] heights = new int[ArenaLayout.MirrorRight / 2 + 1];
        double vanillaSurface = Main.maxTilesY * .3d;
        vanillaSurface *= random.Next(90, 110) * .005d;
        double vanillaRockLayer = vanillaSurface + Main.maxTilesY * .2d;
        vanillaRockLayer *= random.Next(90, 110) * .01d;
        double surface = 500d;
        TerrainPass.TerrainFeatureType feature = TerrainPass.TerrainFeatureType.Plateau;
        int featureLength = 0;

        for (int x = 0; x < heights.Length; x++)
        {
            if (!flat)
            {
                if (featureLength <= 0)
                {
                    feature = (TerrainPass.TerrainFeatureType)random.Next(0, 5);
                    featureLength = random.Next(5, 40);
                    if (feature == TerrainPass.TerrainFeatureType.Plateau)
                        featureLength *= (int)(random.Next(5, 30) * .2d);
                }
                featureLength--;

                if (x > Main.maxTilesX * .45d && x < Main.maxTilesX * .55d
                    && feature is TerrainPass.TerrainFeatureType.Mountain or TerrainPass.TerrainFeatureType.Valley)
                    feature = (TerrainPass.TerrainFeatureType)random.Next(3);
                if (x > Main.maxTilesX * .48d && x < Main.maxTilesX * .52d)
                    feature = TerrainPass.TerrainFeatureType.Plateau;

                surface += SurfaceOffset(feature, random);
                surface = Math.Clamp(surface, 488d, 506d);
            }
            while (random.Next(0, 3) == 0)
                vanillaRockLayer += random.Next(-2, 3);
            heights[x] = flat ? 500 : (int)surface;
        }
        return heights;
    }

    // TerrainPass.GenerateWorldSurfaceOffset, using an explicit seeded random so Prepare has no global side effects.
    private static double SurfaceOffset(TerrainPass.TerrainFeatureType feature, UnifiedRandom random)
    {
        double offset = 0d;
        if ((WorldGen.drunkWorldGen || WorldGen.getGoodWorldGen || WorldGen.remixWorldGen) && random.Next(2) == 0)
        {
            switch (feature)
            {
                case TerrainPass.TerrainFeatureType.Plateau:
                    while (random.Next(0, 6) == 0) offset += random.Next(-1, 2);
                    break;
                case TerrainPass.TerrainFeatureType.Hill:
                    while (random.Next(0, 3) == 0) offset -= 1d;
                    while (random.Next(0, 10) == 0) offset += 1d;
                    break;
                case TerrainPass.TerrainFeatureType.Dale:
                    while (random.Next(0, 3) == 0) offset += 1d;
                    while (random.Next(0, 10) == 0) offset -= 1d;
                    break;
                case TerrainPass.TerrainFeatureType.Mountain:
                    while (random.Next(0, 3) != 0) offset -= 1d;
                    while (random.Next(0, 6) == 0) offset += 1d;
                    break;
                case TerrainPass.TerrainFeatureType.Valley:
                    while (random.Next(0, 3) != 0) offset += 1d;
                    while (random.Next(0, 5) == 0) offset -= 1d;
                    break;
            }
            return offset;
        }

        switch (feature)
        {
            case TerrainPass.TerrainFeatureType.Plateau:
                while (random.Next(0, 7) == 0) offset += random.Next(-1, 2);
                break;
            case TerrainPass.TerrainFeatureType.Hill:
                while (random.Next(0, 4) == 0) offset -= 1d;
                while (random.Next(0, 10) == 0) offset += 1d;
                break;
            case TerrainPass.TerrainFeatureType.Dale:
                while (random.Next(0, 4) == 0) offset += 1d;
                while (random.Next(0, 10) == 0) offset -= 1d;
                break;
            case TerrainPass.TerrainFeatureType.Mountain:
                while (random.Next(0, 2) == 0) offset -= 1d;
                while (random.Next(0, 6) == 0) offset += 1d;
                break;
            case TerrainPass.TerrainFeatureType.Valley:
                while (random.Next(0, 2) == 0) offset += 1d;
                while (random.Next(0, 5) == 0) offset -= 1d;
                break;
        }
        return offset;
    }

    public static void SpreadingGrass(Rectangle area)
    {
        int left = Math.Max(10, area.Left + 1), right = Math.Min(424, area.Right - 1);
        for (int x = Math.Max(50, left); x <= right; x++)
            for (int y = Math.Max(50, area.Top); y <= Math.Min(area.Bottom - 1, (int)Main.worldSurface); y++)
            {
                Tile tile = Main.tile[x, y];
                if (!tile.HasTile) continue;
                ushort type = tile.TileType;
                if (type == TileID.JungleGrass)
                {
                    for (int adjacentX = x - 1; adjacentX <= x + 1; adjacentX++)
                        for (int adjacentY = y - 1; adjacentY <= y + 1; adjacentY++)
                            if (Main.tile[adjacentX, adjacentY].HasTile && Main.tile[adjacentX, adjacentY].TileType == TileID.Dirt)
                                Main.tile[adjacentX, adjacentY].TileType = !Main.tile[adjacentX, adjacentY - 1].HasTile
                                    ? TileID.JungleGrass : TileID.Mud;
                }
                else if (type == TileID.Stone || type == TileID.ClayBlock || TileID.Sets.Ore[type])
                {
                    const int radius = 3;
                    bool exposed = false;
                    ushort conversion = TileID.Dirt;
                    for (int scanX = x - radius; scanX <= x + radius; scanX++)
                        for (int scanY = y - radius; scanY <= y + radius; scanY++)
                        {
                            Tile nearby = Main.tile[scanX, scanY];
                            if (nearby.HasTile)
                            {
                                if (nearby.TileType == TileID.Sand || conversion == TileID.Sand)
                                    conversion = TileID.Sand;
                                else if (nearby.TileType is TileID.Mud or TileID.JungleGrass or TileID.SnowBlock
                                    or TileID.IceBlock or TileID.CorruptGrass or TileID.CrimsonGrass)
                                    conversion = nearby.TileType;
                            }
                            else if (scanY < y && nearby.WallType == WallID.None)
                                exposed = true;
                        }
                    if (!exposed) continue;
                    if (conversion is TileID.CorruptGrass or TileID.CrimsonGrass && Main.tile[x, y - 1].HasTile)
                        conversion = TileID.Dirt;
                    else if (conversion is TileID.Mud or TileID.JungleGrass && x >= GenVars.jungleMinX && x <= GenVars.jungleMaxX)
                        conversion = Main.tile[x, y - 1].HasTile ? TileID.Mud : TileID.JungleGrass;
                    tile.TileType = conversion;
                }
            }

        for (int x = left; x <= right; x++)
        {
            bool exposed = true;
            for (int y = Math.Max(1, area.Top); y < Math.Min(Main.maxTilesY - 1, area.Bottom); y++)
            {
                Tile tile = Main.tile[x, y];
                if (tile.HasTile)
                {
                    if (exposed && tile.TileType == TileID.Dirt)
                    {
                        try
                        {
                            WorldGen.grassSpread = 0;
                            WorldGen.SpreadGrass(x, y);
                        }
                        catch
                        {
                            WorldGen.grassSpread = 0;
                            WorldGen.SpreadGrass(x, y, TileID.Dirt, TileID.Grass, false);
                        }
                    }
                    if (y > GenVars.worldSurfaceHigh) break;
                    exposed = false;
                }
                else if (tile.WallType == WallID.None)
                    exposed = true;
            }
        }
    }

    public static void MudCavesToGrass(Rectangle area)
    {
        int left = Math.Max(1, area.Left), right = Math.Min(424, area.Right - 1);
        int top = Math.Max(1, area.Top), bottom = Math.Min(Main.maxTilesY - 1, area.Bottom);
        for (int x = left; x <= right; x++)
            for (int y = top; y < bottom; y++)
                if (Main.tile[x, y].HasTile && Main.tile[x, y].TileType == TileID.Mud)
                {
                    WorldGen.grassSpread = 0;
                    WorldGen.SpreadGrass(x, y, TileID.Mud, TileID.JungleGrass);
                }

        WorldGen.SmallConsecutivesFound = 0;
        WorldGen.SmallConsecutivesEliminated = 0;
        for (int x = Math.Max(10, left); x <= right; x++)
            WorldGen.ScanTileColumnAndRemoveClumps(x);
    }

    public static void WetJungle(Rectangle area)
    {
        int startY = Math.Max(area.Top, (int)GenVars.worldSurfaceLow);
        int endY = Math.Min(area.Bottom, (int)Main.worldSurface - 1);
        if (endY <= startY) return;
        for (int x = area.Left; x <= Math.Min(424, area.Right - 1); x++)
            for (int y = startY; y < endY; y++)
                if (Main.tile[x, y].HasTile)
                {
                    if (Main.tile[x, y].TileType == TileID.JungleGrass)
                    {
                        Main.tile[x, y - 1].LiquidAmount = byte.MaxValue;
                        Main.tile[x, y - 2].LiquidAmount = byte.MaxValue;
                    }
                    break;
                }
    }

    public static void MudWallsInJungle(Rectangle area)
    {
        int first = -1, last = -1;
        int scanBottom = Math.Min(area.Bottom, (int)(Main.worldSurface + 20d));
        for (int x = Math.Max(5, area.Left); x <= Math.Min(424, area.Right - 1) && first < 0; x++)
            for (int y = Math.Max(0, area.Top); y < scanBottom; y++)
                if (Main.tile[x, y].HasTile && Main.tile[x, y].TileType == TileID.JungleGrass)
                {
                    first = x;
                    break;
                }
        for (int x = Math.Min(424, area.Right - 1); x >= Math.Max(5, area.Left) && last < 0; x--)
            for (int y = Math.Max(0, area.Top); y < scanBottom; y++)
                if (Main.tile[x, y].HasTile && Main.tile[x, y].TileType == TileID.JungleGrass)
                {
                    last = x;
                    break;
                }
        if (first < 0 || last < first) return;

        GenVars.jungleMinX = first;
        GenVars.jungleMaxX = last;
        for (int x = first; x <= last; x++)
            for (int y = Math.Max(0, area.Top); y < scanBottom; y++)
                if (((x >= first + 2 && x <= last - 2) || WorldGen.genRand.Next(2) != 0)
                    && ((x >= first + 3 && x <= last - 3) || WorldGen.genRand.Next(3) != 0)
                    && Main.tile[x, y].WallType is WallID.DirtUnsafe or WallID.MudUnsafe)
                    Main.tile[x, y].WallType = WallID.JungleUnsafe;
    }

    public static void SmoothWorld(Rectangle area)
    {
        int left = Math.Max(20, area.Left + 1), right = Math.Min(424, area.Right - 2);
        int top = Math.Max(20, area.Top + 2), bottom = Math.Min(Main.maxTilesY - 20, area.Bottom - 2);
        bool crackedSolid = Main.tileSolid[GenVars.crackedType];
        bool tile137Solid = Main.tileSolid[TileID.Traps], tile190Solid = Main.tileSolid[190], tile192Solid = Main.tileSolid[192];
        Main.tileSolid[GenVars.crackedType] = true;
        try
        {
            for (int x = left; x <= right; x++)
                for (int y = top; y < bottom; y++)
                    SmoothFirstPass(x, y);

            for (int x = left; x <= right; x++)
                for (int y = top; y < bottom; y++)
                {
                    Tile tile = Main.tile[x, y];
                    if (WorldGen.genRand.Next(2) == 0 && !Main.tile[x, y - 1].HasTile && !Excluded(tile.TileType)
                        && tile.TileType is not (75 or 76) && WorldGen.SolidTile(x, y)
                        && Main.tile[x - 1, y].TileType != TileID.Traps && Main.tile[x + 1, y].TileType != TileID.Traps)
                    {
                        if (WorldGen.SolidTile(x, y + 1) && WorldGen.SolidTile(x + 1, y) && !Main.tile[x - 1, y].HasTile)
                            WorldGen.SlopeTile(x, y, 2);
                        if (WorldGen.SolidTile(x, y + 1) && WorldGen.SolidTile(x - 1, y) && !Main.tile[x + 1, y].HasTile)
                            WorldGen.SlopeTile(x, y, 1);
                    }
                    if (tile.Slope == SlopeType.SlopeDownRight && !WorldGen.SolidTile(x - 1, y))
                    {
                        WorldGen.SlopeTile(x, y);
                        WorldGen.PoundTile(x, y);
                    }
                    if (tile.Slope == SlopeType.SlopeDownLeft && !WorldGen.SolidTile(x + 1, y))
                    {
                        WorldGen.SlopeTile(x, y);
                        WorldGen.PoundTile(x, y);
                    }
                }
        }
        finally
        {
            Main.tileSolid[TileID.Traps] = tile137Solid;
            Main.tileSolid[190] = tile190Solid;
            Main.tileSolid[192] = tile192Solid;
            Main.tileSolid[GenVars.crackedType] = crackedSolid;
        }
    }

    private static void SmoothFirstPass(int x, int y)
    {
        Tile tile = Main.tile[x, y];
        if (Excluded(tile.TileType)) return;
        if (!Main.tile[x, y - 1].HasTile && Main.tile[x - 1, y].TileType != TileID.Switches && Main.tile[x + 1, y].TileType != TileID.Switches)
        {
            if (WorldGen.SolidTile(x, y) && TileID.Sets.CanBeClearedDuringGeneration[tile.TileType])
            {
                if (!Main.tile[x - 1, y].IsHalfBlock && !Main.tile[x + 1, y].IsHalfBlock
                    && Main.tile[x - 1, y].Slope == SlopeType.Solid && Main.tile[x + 1, y].Slope == SlopeType.Solid)
                {
                    if (WorldGen.SolidTile(x, y + 1))
                    {
                        if (!WorldGen.SolidTile(x - 1, y) && !Main.tile[x - 1, y + 1].IsHalfBlock && WorldGen.SolidTile(x - 1, y + 1)
                            && WorldGen.SolidTile(x + 1, y) && !Main.tile[x + 1, y - 1].HasTile)
                            RandomSlopeOrHalf(x, y, 2);
                        else if (!WorldGen.SolidTile(x + 1, y) && !Main.tile[x + 1, y + 1].IsHalfBlock && WorldGen.SolidTile(x + 1, y + 1)
                            && WorldGen.SolidTile(x - 1, y) && !Main.tile[x - 1, y - 1].HasTile)
                            RandomSlopeOrHalf(x, y, 1);
                        else if (WorldGen.SolidTile(x + 1, y + 1) && WorldGen.SolidTile(x - 1, y + 1)
                            && !Main.tile[x + 1, y].HasTile && !Main.tile[x - 1, y].HasTile)
                            WorldGen.PoundTile(x, y);

                        if (WorldGen.SolidTile(x, y))
                        {
                            if (WorldGen.SolidTile(x - 1, y) && WorldGen.SolidTile(x + 1, y + 2) && !Main.tile[x + 1, y].HasTile
                                && !Main.tile[x + 1, y + 1].HasTile && !Main.tile[x - 1, y - 1].HasTile)
                                WorldGen.KillTile(x, y);
                            else if (WorldGen.SolidTile(x + 1, y) && WorldGen.SolidTile(x - 1, y + 2) && !Main.tile[x - 1, y].HasTile
                                && !Main.tile[x - 1, y + 1].HasTile && !Main.tile[x + 1, y - 1].HasTile)
                                WorldGen.KillTile(x, y);
                            else if (!Main.tile[x - 1, y + 1].HasTile && !Main.tile[x - 1, y].HasTile && WorldGen.SolidTile(x + 1, y) && WorldGen.SolidTile(x, y + 2))
                                KillHalfOrSlope(x, y, 2);
                            else if (!Main.tile[x + 1, y + 1].HasTile && !Main.tile[x + 1, y].HasTile && WorldGen.SolidTile(x - 1, y) && WorldGen.SolidTile(x, y + 2))
                                KillHalfOrSlope(x, y, 1);
                        }
                    }
                    if (WorldGen.SolidTile(x, y) && !Main.tile[x - 1, y].HasTile && !Main.tile[x + 1, y].HasTile)
                        WorldGen.KillTile(x, y);
                }
            }
            else if (!tile.HasTile && Main.tile[x, y + 1].TileType is not (151 or 274))
            {
                if (Main.tile[x + 1, y].TileType is not (190 or 48 or 232) && WorldGen.SolidTile(x - 1, y + 1)
                    && WorldGen.SolidTile(x + 1, y) && !Main.tile[x - 1, y].HasTile && !Main.tile[x + 1, y - 1].HasTile)
                    PlaceAndShape(x, y, x + 1, 2);
                if (Main.tile[x - 1, y].TileType is not (190 or 48 or 232) && WorldGen.SolidTile(x + 1, y + 1)
                    && WorldGen.SolidTile(x - 1, y) && !Main.tile[x + 1, y].HasTile && !Main.tile[x - 1, y - 1].HasTile)
                    PlaceAndShape(x, y, x - 1, 1);
            }
        }
        else if (!Main.tile[x, y + 1].HasTile && WorldGen.genRand.Next(2) == 0 && WorldGen.SolidTile(x, y)
            && !Main.tile[x - 1, y].IsHalfBlock && !Main.tile[x + 1, y].IsHalfBlock
            && Main.tile[x - 1, y].Slope == SlopeType.Solid && Main.tile[x + 1, y].Slope == SlopeType.Solid && WorldGen.SolidTile(x, y - 1))
        {
            if (WorldGen.SolidTile(x - 1, y) && !WorldGen.SolidTile(x + 1, y) && WorldGen.SolidTile(x - 1, y - 1))
                WorldGen.SlopeTile(x, y, 3);
            else if (WorldGen.SolidTile(x + 1, y) && !WorldGen.SolidTile(x - 1, y) && WorldGen.SolidTile(x + 1, y - 1))
                WorldGen.SlopeTile(x, y, 4);
        }
        if (TileID.Sets.Conversion.Sand[tile.TileType]) Tile.SmoothSlope(x, y, false);
    }

    private static void RandomSlopeOrHalf(int x, int y, int slope)
    {
        if (WorldGen.genRand.Next(2) == 0) WorldGen.SlopeTile(x, y, slope);
        else WorldGen.PoundTile(x, y);
    }

    private static void KillHalfOrSlope(int x, int y, int slope)
    {
        if (WorldGen.genRand.Next(5) == 0) WorldGen.KillTile(x, y);
        else if (WorldGen.genRand.Next(5) == 0) WorldGen.PoundTile(x, y);
        else WorldGen.SlopeTile(x, y, slope);
    }

    private static void PlaceAndShape(int x, int y, int sideX, int slope)
    {
        ushort type = Main.tile[sideX, y].TileType == TileID.ShellPile ? Main.tile[sideX, y].TileType : Main.tile[x, y + 1].TileType;
        WorldGen.PlaceTile(x, y, type);
        RandomSlopeOrHalf(x, y, slope);
    }

    private static bool Excluded(ushort type) => type is 48 or 137 or 232 or 191 or 151 or 274;
}
