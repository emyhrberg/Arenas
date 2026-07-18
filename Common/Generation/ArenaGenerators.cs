using Arenas.Core.Configs.ConfigElements;
using System;
using System.Collections.Generic;
using Terraria.Enums;
using Terraria.ID;
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
    ArenaLayout CreateLayout(ArenaGeometryConfig geometry, int seed);
    void Prepare(ArenaLayout layout);
    ArenaTileData GetTile(ArenaLayout layout, int x, int y);
    double WorldSurface { get; }
    double RockLayer { get; }
    bool UsesVanillaGeneration => false;
    bool ClearBossArea => true;
    bool MirrorGeneration => true;
    bool ValidateMirroring => true;
    void GenerateVanilla(ArenaLayout layout) { }
    void PlaceCombatStructures(ArenaLayout layout) { }
}

internal static class ArenaGeneratorRegistry
{
    private static readonly Dictionary<ArenaGeneratorKind, IArenaGenerator> Generators = new()
    {
        [ArenaGeneratorKind.KingSlimeSurface] = new SurfaceArenaGenerator(ArenaGeneratorKind.KingSlimeSurface),
        [ArenaGeneratorKind.EyeSurface] = new SurfaceArenaGenerator(ArenaGeneratorKind.EyeSurface),
        [ArenaGeneratorKind.PlanteraJungle] = new PlanteraArenaGenerator(),
        [ArenaGeneratorKind.GolemTemple] = new GolemArenaGenerator(),
        [ArenaGeneratorKind.SandboxWorld] = new SandboxWorldArenaGenerator()
    };

    public static bool TryResolve(BossFightPreset preset, out IArenaGenerator generator)
    {
        ArenaGeneratorKind kind = ResolveKind(preset);
        return Generators.TryGetValue(kind, out generator);
    }

    public static IArenaGenerator Emergency { get; } = new EmergencyFlatArenaGenerator();

    public static ArenaGeometryConfig ResolveGeometry(BossFightPreset preset) => ArenaGeometryDefaults.Resolve(preset, ResolveKind(preset));

    public static void ValidateGeometry(BossFightPreset preset, ArenaGeometryConfig geometry)
    {
        if (preset == null || geometry == null || !TryResolve(preset, out IArenaGenerator generator))
            throw new InvalidOperationException("The fight preset has no arena generator");
        if (generator.Kind == ArenaGeneratorKind.SandboxWorld)
            throw new InvalidOperationException("Sandbox geometry comes from Arenas_v10.wld");
        generator.CreateLayout(geometry, 0).Validate(geometry.WorldWidth, geometry.WorldHeight);
    }

    internal static IArenaGenerator GetForSelfTest(ArenaGeneratorKind kind) => Generators.TryGetValue(kind, out IArenaGenerator generator)
        ? generator
        : throw new ArgumentOutOfRangeException(nameof(kind), kind, "No arena generator is registered");

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

    internal static ArenaLayout Layout(ArenaGeneratorKind kind, ArenaGeometryConfig geometry, int seed)
    {
        Rectangle arena = new(geometry.ArenaLeft, geometry.ArenaTop, geometry.ArenaRight - geometry.ArenaLeft, geometry.ArenaBottom - geometry.ArenaTop);
        Rectangle boss = new(geometry.BossAreaX, geometry.BossAreaY, geometry.BossAreaWidth, geometry.BossAreaHeight);
        Point red = new(geometry.RedSpawnX, geometry.RedSpawnY), blue = new(geometry.BlueSpawnX, geometry.BlueSpawnY), bossSpawn = new(geometry.BossSpawnX, geometry.BossSpawnY);
        Rectangle redClearance = SpawnRoom(red, geometry.SpawnRoomWidth, geometry.SpawnRoomHeight);
        Rectangle blueClearance = SpawnRoom(blue, geometry.SpawnRoomWidth, geometry.SpawnRoomHeight);
        return new ArenaLayout
        {
            Generator = kind, Seed = seed, WorldWidth = geometry.WorldWidth, WorldHeight = geometry.WorldHeight,
            ArenaArea = arena, BossArea = boss,
            RedSpawnClearance = redClearance, BlueSpawnClearance = blueClearance, RedSpawn = red, BlueSpawn = blue, BossSpawn = bossSpawn,
            StagingLobby = redClearance, OuterBorderThickness = geometry.OuterBorderThickness, TeamBorderWidth = geometry.TeamBorderWidth,
            BlueBorderX = geometry.BlueBorderX, RedBorderX = geometry.RedBorderX,
            AutoPlaceTeamSpawns = geometry.AutoPlaceTeamSpawns, AutoPlaceBossSpawn = geometry.AutoPlaceBossSpawn
        };
    }

    internal static Rectangle SpawnRoom(Point spawn, int width, int height) => new(spawn.X - width / 2, spawn.Y - height + 1, width, height);
}

internal sealed class SandboxWorldArenaGenerator : IArenaGenerator
{
    public ArenaGeneratorKind Kind => ArenaGeneratorKind.SandboxWorld;
    public double WorldSurface => Main.worldSurface;
    public double RockLayer => Main.rockLayer;

    public ArenaLayout CreateLayout(ArenaGeometryConfig geometry, int seed)
    {
        int spawnX = Math.Clamp(Main.spawnTileX, 12, Math.Max(12, Main.maxTilesX - 13));
        int spawnY = Math.Clamp(Main.spawnTileY, 12, Math.Max(12, Main.maxTilesY - 13));
        Rectangle arena = new(3, 3, Math.Max(1, Main.maxTilesX - 6), Math.Max(1, Main.maxTilesY - 6));
        Rectangle spawnRoom = new(spawnX - 10, spawnY - 10, 20, 20);
        return new ArenaLayout
        {
            Generator = Kind,
            Seed = seed,
            WorldWidth = Main.maxTilesX,
            WorldHeight = Main.maxTilesY,
            ArenaArea = arena,
            BossArea = new Rectangle(spawnX, spawnY, 1, 1),
            RedSpawnClearance = spawnRoom,
            BlueSpawnClearance = spawnRoom,
            RedSpawn = new Point(spawnX, spawnY),
            BlueSpawn = new Point(spawnX, spawnY),
            BossSpawn = new Point(spawnX, spawnY),
            StagingLobby = spawnRoom,
            OuterBorderThickness = 0,
            TeamBorderWidth = 1,
            BlueBorderX = arena.Left,
            RedBorderX = arena.Right,
            AutoPlaceTeamSpawns = false,
            AutoPlaceBossSpawn = false
        };
    }

    public void Prepare(ArenaLayout layout) { }
    public ArenaTileData GetTile(ArenaLayout layout, int x, int y) => ArenaTileData.Air();
}

internal sealed class EmergencyFlatArenaGenerator : IArenaGenerator
{
    public ArenaGeneratorKind Kind => ArenaGeneratorKind.KingSlimeSurface;
    public double WorldSurface => 520;
    public double RockLayer => 550;

    public ArenaLayout CreateLayout(ArenaGeometryConfig geometry, int seed) => ArenaGeneratorRegistry.Layout(Kind, geometry, seed);

    public void Prepare(ArenaLayout layout) { }

    public ArenaTileData GetTile(ArenaLayout layout, int x, int y)
    {
        if (!layout.ArenaArea.Contains(x, y) || layout.BossArea.Contains(x, y) || layout.RedSpawnClearance.Contains(x, y))
            return ArenaTileData.Air();
        int platformY = layout.ArenaArea.Top + layout.ArenaArea.Height * 71 / 100;
        if (y == platformY && x >= layout.ArenaArea.Left + layout.ArenaArea.Width * 18 / 100 && x <= layout.ArenaArea.Left + layout.ArenaArea.Width * 35 / 100)
            return ArenaTileData.Solid(TileID.Platforms);
        int floorY = layout.ArenaArea.Bottom - Math.Max(12, layout.ArenaArea.Height / 8);
        if (y < floorY)
            return ArenaTileData.Air();
        return ArenaTileData.Solid(y == floorY ? TileID.Grass : TileID.Dirt);
    }
}

internal sealed class SurfaceArenaGenerator(ArenaGeneratorKind kind) : IArenaGenerator
{
    private double worldSurface = 150;
    private double rockLayer = 300;
    public ArenaGeneratorKind Kind => kind;
    public double WorldSurface => worldSurface;
    public double RockLayer => rockLayer;
    public bool UsesVanillaGeneration => true;
    public bool ClearBossArea => false;
    public bool MirrorGeneration => false;
    public bool ValidateMirroring => false;

    public ArenaLayout CreateLayout(ArenaGeometryConfig geometry, int seed) => ArenaGeneratorRegistry.Layout(kind, geometry, seed);

    public void Prepare(ArenaLayout layout) { }

    public ArenaTileData GetTile(ArenaLayout layout, int x, int y) => ArenaTileData.Air();

    public void GenerateVanilla(ArenaLayout layout)
    {
        Log.Debug($"[WorldGen2.Surface] Starting compact vanilla world generation for {kind} seed={layout.Seed}");
        (worldSurface, rockLayer) = VanillaSurfaceWorldGenerator.Generate(layout, layout.Seed, kind);
    }

    public void PlaceCombatStructures(ArenaLayout layout)
    {
        PlaceGrassFloor(layout.RedSpawnClearance);
        PlaceGrassFloor(layout.BlueSpawnClearance);
        if (!layout.AutoPlaceBossSpawn)
            ClearBossAnchor(layout.BossSpawn);
    }

    private static void PlaceGrassFloor(Rectangle room)
    {
        for (int x = room.Left; x < room.Right; x++)
        {
            Tile tile = Main.tile[x, room.Bottom];
            tile.ClearTile();
            tile.HasTile = true;
            tile.TileType = TileID.Grass;
            tile.Slope = SlopeType.Solid;
        }
    }

    private static void ClearBossAnchor(Point spawn)
    {
        for (int x = spawn.X - 2; x <= spawn.X + 2; x++)
            for (int y = spawn.Y - 6; y <= spawn.Y + 1; y++)
            {
                Tile tile = Main.tile[x, y];
                tile.ClearTile();
                tile.LiquidAmount = 0;
            }
    }
}

internal sealed class PlanteraArenaGenerator : IArenaGenerator
{
    private double worldSurface = 110, rockLayer = 170;
    public ArenaGeneratorKind Kind => ArenaGeneratorKind.PlanteraJungle;
    public double WorldSurface => worldSurface;
    public double RockLayer => rockLayer;
    public bool UsesVanillaGeneration => true;
    public bool ClearBossArea => false;

    public ArenaLayout CreateLayout(ArenaGeometryConfig geometry, int seed) => ArenaGeneratorRegistry.Layout(Kind, geometry, seed);

    public void Prepare(ArenaLayout layout) { }

    public ArenaTileData GetTile(ArenaLayout layout, int x, int y)
    {
        if (!layout.ArenaArea.Contains(x, y)) return ArenaTileData.Air();
        return ArenaTileData.Solid(TileID.Mud, WallID.JungleUnsafe);
    }

    public void GenerateVanilla(ArenaLayout layout)
    {
        Log.Debug($"[WorldGen2.Plantera] Building a dense underground Jungle with Terraria's registered GenPasses. seed={layout.Seed}");
        (worldSurface, rockLayer) = VanillaJungleGenerator.GenerateLeftHalf(layout, layout.Seed, carveBossPocket: true);
    }

    public void PlaceCombatStructures(ArenaLayout layout)
    {
        PlaceMudSpawnFloor(layout.RedSpawnClearance);
        PlaceMudSpawnFloor(layout.BlueSpawnClearance);
    }

    private static void PlaceMudSpawnFloor(Rectangle room)
    {
        for (int x = room.Left; x < room.Right; x++)
        {
            Tile tile = Main.tile[x, room.Bottom];
            tile.ClearTile();
            tile.HasTile = true;
            tile.TileType = TileID.JungleGrass;
            tile.Slope = SlopeType.Solid;
        }
    }
}

internal sealed class GolemArenaGenerator : IArenaGenerator
{
    private double worldSurface = 110, rockLayer = 170;
    public ArenaGeneratorKind Kind => ArenaGeneratorKind.GolemTemple;
    public double WorldSurface => worldSurface;
    public double RockLayer => rockLayer;
    public bool UsesVanillaGeneration => true;
    public bool ClearBossArea => false;
    public bool ValidateMirroring => false;

    public ArenaLayout CreateLayout(ArenaGeometryConfig geometry, int seed) => ArenaGeneratorRegistry.Layout(Kind, geometry, seed);
    public void Prepare(ArenaLayout layout) { }

    public ArenaTileData GetTile(ArenaLayout layout, int x, int y)
    {
        if (!layout.ArenaArea.Contains(x, y)) return ArenaTileData.Air();
        return ArenaTileData.Solid(TileID.Mud, WallID.JungleUnsafe);
    }

    public void GenerateVanilla(ArenaLayout layout)
    {
        Log.Debug($"[WorldGen2.Golem] Building the underground Jungle surrounding the Temple. seed={layout.Seed}");
        (worldSurface, rockLayer) = VanillaJungleGenerator.GenerateLeftHalf(layout, layout.Seed ^ 0x476F6C65, carveBossPocket: false);
    }

    public void PlaceCombatStructures(ArenaLayout layout)
    {
        VanillaTempleGenerator.Generate(layout, layout.Seed ^ 0x54656D70);
    }
}
