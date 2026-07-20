using Arenas.Common.DataStructures;
using System;
using Terraria.ID;

namespace Arenas.Common.Generation;

/// <summary>Places the solid Lihzahrd Brick frame around a resolved arena during the Generating phase.</summary>
internal static class ArenaBorder
{
    internal const int Thickness = 3;

    internal static void Place(ArenaLayout layout)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || layout == null)
            return;

        Rectangle bounds = layout.ArenaBounds;
        Rectangle outer = bounds;
        outer.Inflate(Thickness, Thickness);

        for (int x = outer.Left; x < outer.Right; x++)
        {
            for (int y = outer.Top; y < outer.Bottom; y++)
            {
                if (bounds.Contains(x, y) || !WorldGen.InWorld(x, y, 5))
                    continue;

                Tile tile = Main.tile[x, y];
                tile.ClearTile();
                tile.HasTile = true;
                tile.TileType = TileID.LihzahrdBrick;
                tile.LiquidAmount = 0;
            }
        }

        Frame(outer, bounds);
        Sync(outer);
        Log.Info($"Placed the {Thickness}-tile Lihzahrd Brick arena border around {bounds}.");
    }

    private static void Frame(Rectangle outer, Rectangle bounds)
    {
        Rectangle framing = outer;
        framing.Inflate(1, 1);
        Rectangle skip = bounds;
        skip.Inflate(-1, -1);

        for (int x = framing.Left; x < framing.Right; x++)
            for (int y = framing.Top; y < framing.Bottom; y++)
                if (!skip.Contains(x, y) && WorldGen.InWorld(x, y, 5))
                    WorldGen.TileFrame(x, y, resetFrame: true);
    }

    private static void Sync(Rectangle outer)
    {
        if (Main.netMode != NetmodeID.Server)
            return;

        // The four ring strips, inflated by one tile so re-framed neighbors sync too.
        SendStrip(outer.Left - 1, outer.Top - 1, outer.Width + 2, Thickness + 2);
        SendStrip(outer.Left - 1, outer.Bottom - Thickness - 1, outer.Width + 2, Thickness + 2);
        SendStrip(outer.Left - 1, outer.Top - 1, Thickness + 2, outer.Height + 2);
        SendStrip(outer.Right - Thickness - 1, outer.Top - 1, Thickness + 2, outer.Height + 2);
    }

    private static void SendStrip(int x, int y, int width, int height)
    {
        const int ChunkSize = 50;
        int left = Math.Clamp(x, 0, Main.maxTilesX - 1);
        int top = Math.Clamp(y, 0, Main.maxTilesY - 1);
        int right = Math.Clamp(x + width, 0, Main.maxTilesX);
        int bottom = Math.Clamp(y + height, 0, Main.maxTilesY);

        for (int chunkX = left; chunkX < right; chunkX += ChunkSize)
            for (int chunkY = top; chunkY < bottom; chunkY += ChunkSize)
                NetMessage.SendTileSquare(-1, chunkX, chunkY,
                    Math.Min(ChunkSize, right - chunkX), Math.Min(ChunkSize, bottom - chunkY));
    }
}
