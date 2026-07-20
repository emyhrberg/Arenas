using Arenas.Common.DataStructures;
using System;
using Terraria.ID;

namespace Arenas.Common.Generation;

/// <summary>
/// Resolves a Milestone-2 arena from existing terrain. It deliberately does not
/// create, clear, or synchronize tiles; custom world generation belongs to M3.
/// </summary>
internal static class ArenaGeneration
{
    private const int WorldMargin = 20;
    private const int SpawnInset = 24;

    internal static bool TryResolve(BossFightPreset preset, out ArenaLayout layout, out string failure)
    {
        layout = null;
        failure = "";

        if (preset == null)
        {
            failure = "The King Slime preset is missing.";
            return false;
        }

        if (preset.Boss?.Type != NPCID.KingSlime || preset.ArenaKind != ArenaKind.WorldCenterSurface)
        {
            failure = "Milestone 2 supports only King Slime in a world-center surface arena.";
            return false;
        }

        int width = Math.Clamp(preset.ArenaWidthTiles, 100, Main.maxTilesX - WorldMargin * 2);
        int height = Math.Clamp(preset.ArenaHeightTiles, 100, Main.maxTilesY - WorldMargin * 2);
        int centerX = Main.maxTilesX / 2;

        if (!TryFindGround(centerX, WorldMargin, Main.maxTilesY - WorldMargin, out int centerGround))
        {
            failure = "No ground was found near the center of the world.";
            return false;
        }

        int left = Math.Clamp(centerX - width / 2, WorldMargin, Main.maxTilesX - WorldMargin - width);
        int top = Math.Clamp(centerGround - height * 3 / 4, WorldMargin, Main.maxTilesY - WorldMargin - height);
        Rectangle bounds = new(left, top, width, height);
        int blueX = bounds.Left + Math.Min(SpawnInset, Math.Max(1, bounds.Width / 8));
        int redX = bounds.Right - Math.Min(SpawnInset, Math.Max(1, bounds.Width / 8));

        if (!TryFindGround(blueX, bounds.Top, bounds.Bottom, out int blueY)
            || !TryFindGround(redX, bounds.Top, bounds.Bottom, out int redY))
        {
            failure = "Grounded Red and Blue spawn positions could not be resolved inside the arena.";
            return false;
        }

        layout = new ArenaLayout(bounds, new Point(blueX, blueY), new Point(redX, redY));
        Log.Info($"Resolved existing-terrain King Slime arena at {bounds}; blue={layout.BlueSpawn}, red={layout.RedSpawn}.");
        return true;
    }

    private static bool TryFindGround(int x, int startY, int endY, out int groundY)
    {
        int start = Math.Clamp(startY, WorldMargin, Main.maxTilesY - WorldMargin);
        int end = Math.Clamp(endY, start + 1, Main.maxTilesY - WorldMargin);
        for (int y = start; y < end; y++)
        {
            if (!WorldGen.SolidTile(x, y) || WorldGen.SolidTile(x, y - 1)
                || WorldGen.SolidTile(x, y - 2) || WorldGen.SolidTile(x, y - 3))
                continue;

            groundY = y;
            return true;
        }

        groundY = 0;
        return false;
    }
}
