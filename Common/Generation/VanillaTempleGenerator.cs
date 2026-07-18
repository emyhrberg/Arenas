using System;
using Terraria.Enums;
using Terraria.ID;
using Terraria.Utilities;
using Terraria.WorldBuilding;

namespace Arenas.Common.Generation;

internal static class VanillaTempleGenerator
{
    // makeTemple scales its room count only from maxTilesX. Temporarily using Terraria's small-world width
    // retains vanilla room density while the actual writes remain in the configured compact Tilemap.
    private const int VanillaSmallWorldWidth = 4200;

    public static void Generate(ArenaLayout layout, int seed)
    {
        int previousWidth = Main.maxTilesX;
        UnifiedRandom previousRandom = WorldGen.genRand;
        bool previousGenerating = WorldGen.gen;
        bool previousDrunk = WorldGen.drunkWorldGen, previousGood = WorldGen.getGoodWorldGen, previousRemix = WorldGen.remixWorldGen;
        WorldGenConfiguration previousConfiguration = GenVars.configuration;
        try
        {
            WorldGen._genRand = new UnifiedRandom(seed);
            WorldGen.gen = true;
            WorldGen.drunkWorldGen = WorldGen.getGoodWorldGen = WorldGen.remixWorldGen = false;
            GenVars.configuration = VanillaGenPassRunner.Configuration;
            Main.maxTilesX = VanillaSmallWorldWidth;
            int templeX = layout.BossArea.Center.X;
            int templeY = Math.Clamp(layout.BossArea.Top - 70, layout.ArenaArea.Top + 50, layout.ArenaArea.Bottom - 260);
            Log.Debug($"[WorldGen2.Temple] Running the full vanilla WorldGen.makeTemple at ({templeX},{templeY}) with small-world room scaling. seed={seed}");
            WorldGen.makeTemple(templeX, templeY);
            Log.Debug($"[WorldGen2.Temple] Vanilla Temple complete. rooms={GenVars.tRooms}, bounds=({GenVars.tLeft},{GenVars.tTop})-({GenVars.tRight},{GenVars.tBottom})");
            VanillaGenPassRunner.Run("WorldGen2.Temple", "Temple", seed);
            VanillaGenPassRunner.Run("WorldGen2.Temple", "Lihzahrd Altars", seed);
        }
        finally
        {
            Main.maxTilesX = previousWidth;
            WorldGen.drunkWorldGen = previousDrunk;
            WorldGen.getGoodWorldGen = previousGood;
            WorldGen.remixWorldGen = previousRemix;
            GenVars.configuration = previousConfiguration;
            WorldGen.gen = previousGenerating;
            WorldGen._genRand = previousRandom;
        }

        ClearSpawnRoom(layout.RedSpawnClearance);
        ClearSpawnRoom(layout.BlueSpawnClearance);
        EnsureBossAnchor(layout);
        ArenaGenerationDiagnostics.LogSnapshot("Finished Temple", layout);
    }

    private static void EnsureBossAnchor(ArenaLayout layout)
    {
        if (!layout.AutoPlaceBossSpawn)
        {
            Rectangle configuredRoom = CenteredRoom(layout.BossSpawn, 16, 12, layout.BossArea);
            Log.Debug($"[WorldGen2.Temple] Preserving configured Golem spawn {layout.BossSpawn} in chamber {configuredRoom}");
            ClearSpawnRoom(configuredRoom, templeWalls: true);
            PlaceTempleFloor(configuredRoom);
            return;
        }

        Point altar = new(GenVars.lAltarX, GenVars.lAltarY);
        Point best = Point.Zero;
        int bestDistance = int.MaxValue;
        for (int x = layout.BossArea.Left + 6; x < layout.BossArea.Right - 6; x++)
            for (int y = layout.BossArea.Top + 12; y < layout.BossArea.Bottom - 2; y++)
                if (HasTempleRoomAt(x, y))
                {
                    int distance = Math.Abs(x - altar.X) * 2 + Math.Abs(y - altar.Y);
                    if (distance >= bestDistance)
                        continue;
                    best = new Point(x, y);
                    bestDistance = distance;
                }

        if (best != Point.Zero)
        {
            layout.BossSpawn = best;
            Log.Debug($"[WorldGen2.Temple] Using vanilla Temple room near altar={altar} for Golem at {layout.BossSpawn}");
            return;
        }

        Rectangle room = CenteredRoom(layout.BossArea.Center, Math.Min(32, layout.BossArea.Width - 4), Math.Min(24, layout.BossArea.Height - 4), layout.BossArea);
        Log.Debug($"[WorldGen2.Temple] No suitable vanilla room intersected BossArea; carving a compact fallback Temple chamber {room}");
        ClearSpawnRoom(room, templeWalls: true);
        PlaceTempleFloor(room);
        layout.BossSpawn = new Point(room.Center.X, room.Bottom - 1);
    }

    private static Rectangle CenteredRoom(Point center, int width, int height, Rectangle bounds)
    {
        width = Math.Clamp(width, 4, bounds.Width - 2);
        height = Math.Clamp(height, 6, bounds.Height - 2);
        int x = Math.Clamp(center.X - width / 2, bounds.Left + 1, bounds.Right - width - 1);
        int y = Math.Clamp(center.Y - height + 1, bounds.Top + 1, bounds.Bottom - height - 1);
        return new Rectangle(x, y, width, height);
    }

    private static void PlaceTempleFloor(Rectangle room)
    {
        int floor = room.Bottom;
        for (int x = room.Left; x < room.Right; x++)
        {
            Tile tile = Main.tile[x, floor];
            tile.ClearEverything();
            tile.HasTile = true;
            tile.TileType = TileID.LihzahrdBrick;
            tile.WallType = WallID.LihzahrdBrickUnsafe;
        }
    }

    private static bool HasTempleRoomAt(int x, int feetY)
    {
        if (!WorldGen.InWorld(x, feetY, 12)
            || Main.tile[x, feetY].WallType != WallID.LihzahrdBrickUnsafe
            || !Main.tile[x, feetY + 1].HasTile
            || Main.tile[x, feetY + 1].TileType != TileID.LihzahrdBrick)
            return false;
        for (int scanX = x - 5; scanX <= x + 5; scanX++)
            for (int scanY = feetY - 11; scanY <= feetY; scanY++)
                if (Main.tile[scanX, scanY].HasTile)
                    return false;
        return true;
    }

    private static void ClearSpawnRoom(Rectangle room, bool templeWalls = false)
    {
        for (int x = room.Left; x < room.Right; x++)
            for (int y = room.Top; y < room.Bottom; y++)
            {
                Tile tile = Main.tile[x, y];
                tile.ClearTile();
                tile.LiquidAmount = 0;
                tile.RedWire = tile.BlueWire = tile.GreenWire = tile.YellowWire = false;
                tile.HasActuator = tile.IsActuated = false;
                if (templeWalls)
                    tile.WallType = WallID.LihzahrdBrickUnsafe;
                else if (tile.WallType == WallID.None)
                    tile.WallType = WallID.JungleUnsafe;
            }

        for (int x = room.Left; x < room.Right; x++)
        {
            Tile floor = Main.tile[x, room.Bottom];
            floor.ClearTile();
            floor.HasTile = true;
            floor.TileType = templeWalls ? TileID.LihzahrdBrick : TileID.JungleGrass;
            floor.Slope = SlopeType.Solid;
        }
    }
}
