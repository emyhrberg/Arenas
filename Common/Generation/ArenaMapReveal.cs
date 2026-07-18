using System;

namespace Arenas.Common.Generation;

internal static class ArenaMapReveal
{
    public static void Reveal(ArenaLayout layout)
    {
        if (Main.dedServ || layout == null || !Main.mapEnabled) return;

        Rectangle area = layout.ArenaArea;
        area.Inflate(ArenaGeneratorRegistry.OuterBorderThickness, ArenaGeneratorRegistry.OuterBorderThickness);
        int left = Math.Max(0, area.Left), top = Math.Max(0, area.Top);
        int right = Math.Min(Main.maxTilesX, area.Right), bottom = Math.Min(Main.maxTilesY, area.Bottom);
        for (int x = left; x < right; x++)
            for (int y = top; y < bottom; y++)
                Main.Map.Update(x, y, byte.MaxValue);

        Main.sectionManager.SetAllFramedSectionsAsNeedingRefresh();
        Main.refreshMap = true;
    }
}
