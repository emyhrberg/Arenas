using Terraria.Enums;

namespace Arenas.Common;

internal static class ArenaGeometry
{
    public static readonly Point RedSpawn = new(107, 189);
    public static readonly Point BlueSpawn = new(688, 189);
    public static readonly Rectangle BossTileArea = new(167, 458, 205, 63);
    public static Rectangle BossWorldArea => new(BossTileArea.X * 16, BossTileArea.Y * 16, BossTileArea.Width * 16, BossTileArea.Height * 16);
    public static Point TeamSpawn(Team team) => team is Team.Blue or Team.Green ? BlueSpawn : RedSpawn;
}
