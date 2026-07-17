using Arenas.Core.Configs.ConfigElements;
using System;
using System.Collections.Generic;
using Terraria.Enums;
using Terraria.ID;

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
}

internal static class ArenaGeneratorRegistry
{
    public const int OuterBorderThickness = 3;
    public const int BossBorderThickness = 1;
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

    internal static ArenaLayout Layout(ArenaGeneratorKind kind, int seed, Rectangle boss, Rectangle redClearance, Rectangle blueClearance, Point red, Point blue, Point bossSpawn, int entranceTop)
    {
        return new ArenaLayout
        {
            Generator = kind, Seed = seed, ArenaArea = ArenaArea, BossArea = boss,
            RedSpawnClearance = redClearance, BlueSpawnClearance = blueClearance, RedSpawn = red, BlueSpawn = blue, BossSpawn = bossSpawn,
            StagingLobby = StagingLobby,
            LeftBossEntrance = new Rectangle(boss.Left - 1, entranceTop, 1, 40),
            RightBossEntrance = new Rectangle(boss.Right, entranceTop, 1, 40)
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
        new Point(150, 499), new Point(699, 499), new Point(425, 450), 460);

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

    public ArenaLayout CreateLayout(int seed) => kind == ArenaGeneratorKind.EyeSurface
        ? ArenaGeneratorRegistry.Layout(kind, seed, new Rectangle(285, 140, 280, 360), new Rectangle(118, 452, 65, 48), new Rectangle(667, 452, 65, 48), new Point(150, 499), new Point(699, 499), new Point(425, 270), 460)
        : ArenaGeneratorRegistry.Layout(kind, seed, new Rectangle(325, 240, 200, 260), new Rectangle(118, 452, 65, 48), new Rectangle(667, 452, 65, 48), new Point(150, 499), new Point(699, 499), new Point(425, 450), 460);

    public void Prepare(ArenaLayout layout)
    {
        Random random = new(layout.Seed);
        int height = 500;
        for (int x = 0; x < heights.Length; x++)
        {
            if (kind == ArenaGeneratorKind.KingSlimeSurface && x % 7 == 0)
                height = Math.Clamp(height + random.Next(-2, 3), 488, 506);
            heights[x] = kind == ArenaGeneratorKind.EyeSurface ? 500 : height;
        }
        for (int i = 0; i < platformShifts.Length; i++) platformShifts[i] = random.Next(-20, 21);
    }

    public ArenaTileData GetTile(ArenaLayout layout, int x, int y)
    {
        if (!layout.ArenaArea.Contains(x, y)) return ArenaTileData.Air();
        if (layout.BossArea.Contains(x, y) || layout.RedSpawnClearance.Contains(x, y)) return ArenaTileData.Air();

        if (IsPlatform(x, y)) return ArenaTileData.Solid(TileID.Platforms);
        int surface = heights[Math.Clamp(x, 0, heights.Length - 1)];
        if (y < surface) return ArenaTileData.Air();
        return ArenaTileData.Solid(y == surface ? TileID.Grass : TileID.Dirt);
    }

    private bool IsPlatform(int x, int y)
    {
        if (kind == ArenaGeneratorKind.KingSlimeSurface)
            return y == 420 && x is >= 175 and <= 300;
        return (y == 410 && x >= 80 + platformShifts[0] && x <= 250 + platformShifts[0])
            || (y == 330 && x >= 190 + platformShifts[1] && x <= 365 + platformShifts[1])
            || (y == 250 && x >= 80 + platformShifts[2] && x <= 230 + platformShifts[2]);
    }
}

internal sealed class PlanteraArenaGenerator : IArenaGenerator
{
    private readonly List<(Point Center, int RadiusX, int RadiusY)> caves = [];
    public ArenaGeneratorKind Kind => ArenaGeneratorKind.PlanteraJungle;
    public double WorldSurface => 110;
    public double RockLayer => 170;

    public ArenaLayout CreateLayout(int seed) => ArenaGeneratorRegistry.Layout(Kind, seed, new Rectangle(285, 150, 280, 320), new Rectangle(118, 282, 65, 48), new Rectangle(667, 282, 65, 48), new Point(150, 329), new Point(699, 329), new Point(425, 300), 290);

    public void Prepare(ArenaLayout layout)
    {
        caves.Clear();
        Random random = new(layout.Seed);
        for (int i = 0; i < 18; i++)
            caves.Add((new Point(random.Next(55, 280), random.Next(85, 540)), random.Next(22, 55), random.Next(16, 38)));
    }

    public ArenaTileData GetTile(ArenaLayout layout, int x, int y)
    {
        if (!layout.ArenaArea.Contains(x, y)) return ArenaTileData.Air();
        if (layout.BossArea.Contains(x, y) && ((y == 245 && x is >= 320 and <= 390) || (y == 355 && x is >= 300 and <= 370)))
            return ArenaTileData.Solid(TileID.JungleGrass, WallID.JungleUnsafe);
        Rectangle spawnAnchor = layout.RedSpawnClearance;
        spawnAnchor.Inflate(8, 3);
        if (spawnAnchor.Contains(x, y) && !layout.RedSpawnClearance.Contains(x, y)
            && (y < layout.RedSpawnClearance.Top || y >= layout.RedSpawnClearance.Bottom))
            return ArenaTileData.Solid(TileID.JungleGrass, WallID.JungleUnsafe);
        bool carved = IsCarved(layout, x, y);
        if (carved)
        {
            if ((y == 330 || y == 395) && x is >= 90 and <= 400 && !layout.RedSpawnClearance.Contains(x, y))
                return ArenaTileData.Solid(TileID.Platforms, WallID.JungleUnsafe);
            return ArenaTileData.Air(WallID.JungleUnsafe);
        }

        bool exposed = IsCarved(layout, x - 1, y) || IsCarved(layout, x + 1, y) || IsCarved(layout, x, y - 1) || IsCarved(layout, x, y + 1);
        return ArenaTileData.Solid(exposed ? TileID.JungleGrass : TileID.Mud, WallID.JungleUnsafe);
    }

    private bool IsCarved(ArenaLayout layout, int x, int y)
    {
        if (layout.BossArea.Contains(x, y) || layout.RedSpawnClearance.Contains(x, y)) return true;
        if (x is >= 180 and <= 325 && y is >= 290 and < 330) return true;
        foreach ((Point center, int rx, int ry) in caves)
        {
            double dx = (x - center.X) / (double)rx, dy = (y - center.Y) / (double)ry;
            if (dx * dx + dy * dy <= 1d) return true;
        }
        return false;
    }
}

internal sealed class GolemArenaGenerator : IArenaGenerator
{
    private readonly List<Rectangle> randomRooms = [];
    public ArenaGeneratorKind Kind => ArenaGeneratorKind.GolemTemple;
    public double WorldSurface => 110;
    public double RockLayer => 170;

    public ArenaLayout CreateLayout(int seed) => ArenaGeneratorRegistry.Layout(Kind, seed, new Rectangle(305, 270, 240, 230), new Rectangle(118, 452, 65, 48), new Rectangle(667, 452, 65, 48), new Point(150, 499), new Point(699, 499), new Point(425, 440), 460);
    public void Prepare(ArenaLayout layout)
    {
        randomRooms.Clear();
        Random random = new(layout.Seed);
        for (int i = 0; i < 5; i++)
            randomRooms.Add(new Rectangle(random.Next(60, 230), random.Next(70, 420), random.Next(45, 90), random.Next(28, 55)));
    }

    public ArenaTileData GetTile(ArenaLayout layout, int x, int y)
    {
        if (!layout.ArenaArea.Contains(x, y)) return ArenaTileData.Air();
        bool room = layout.BossArea.Contains(x, y) || layout.RedSpawnClearance.Contains(x, y)
            || (x is >= 180 and <= 325 && y is >= 452 and < 500)
            || (x is >= 70 and <= 280 && y is >= 330 and < 390)
            || (x is >= 90 and <= 250 && y is >= 190 and < 245)
            || randomRooms.Exists(room => room.Contains(x, y));

        if (room)
        {
            if ((y == 390 && x is >= 85 and <= 280) || (y == 245 && x is >= 110 and <= 250))
                return ArenaTileData.Solid(TileID.Platforms, WallID.LihzahrdBrickUnsafe);
            return ArenaTileData.Air(WallID.LihzahrdBrickUnsafe);
        }
        return ArenaTileData.Solid(TileID.LihzahrdBrick, WallID.LihzahrdBrickUnsafe);
    }
}
