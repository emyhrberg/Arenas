using Arenas.Core.Configs.ConfigElements;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent.Events;
using Terraria.ID;

namespace Arenas.Common.Generation;

internal enum ArenaGenerationStage : byte { Terrain, VanillaGeneration, Mirroring, Structures, Framing, Validating, Complete, Failed }

[Obsolete("Compact per-preset worlds are deprecated. ArenasSubworld now uses ArenaWorldGenerationSystem and one reusable 4200x1200 world.")]
internal sealed class ArenaGenerationJob
{
    private const int MutationBudget = 8000;
    private const double MillisecondBudget = 8d;
    private readonly IArenaGenerator generator;
    private readonly List<Point> structurePoints = [];
    private int cursor;
    private bool initialized;

    public ArenaLayout Layout { get; }
    public ArenaGenerationStage Stage { get; private set; } = ArenaGenerationStage.Terrain;
    public ArenaGenerationStage FailedStage { get; private set; }
    public Exception Error { get; private set; }
    public bool IsComplete => Stage == ArenaGenerationStage.Complete;
    public bool HasFailed => Stage == ArenaGenerationStage.Failed;
    public float Progress => Stage switch
    {
        ArenaGenerationStage.Terrain => cursor / (float)Math.Max(1, TerrainArea.Width * TerrainArea.Height) * .45f,
        ArenaGenerationStage.VanillaGeneration => .45f,
        ArenaGenerationStage.Mirroring => .50f + cursor / (float)Math.Max(1, LeftHalfArea.Width * LeftHalfArea.Height) * .18f,
        ArenaGenerationStage.Structures => .68f + cursor / (float)Math.Max(1, structurePoints.Count) * .12f,
        ArenaGenerationStage.Framing => .80f + cursor / (float)Math.Max(1, FramingArea.Width * FramingArea.Height) * .18f,
        ArenaGenerationStage.Validating => .98f,
        ArenaGenerationStage.Complete => 1f,
        _ => 0f
    };

    public ArenaGenerationJob(IArenaGenerator generator, ArenaGeometryConfig geometry, int seed)
    {
        this.generator = generator;
        Layout = generator.CreateLayout(geometry, seed);
        Layout.Validate(Main.maxTilesX, Main.maxTilesY);
        generator.Prepare(Layout);
    }

    public void Tick()
    {
        if (IsComplete || HasFailed) return;
        try
        {
            if (!initialized) { ClearEntities(); initialized = true; }
            Stopwatch watch = Stopwatch.StartNew();
            int mutations = 0;
            while (mutations < MutationBudget && watch.Elapsed.TotalMilliseconds < MillisecondBudget && !IsComplete)
            {
                switch (Stage)
                {
                    case ArenaGenerationStage.Terrain: mutations += GenerateNext(); break;
                    case ArenaGenerationStage.VanillaGeneration: mutations += GenerateVanilla(); break;
                    case ArenaGenerationStage.Mirroring: mutations += MirrorNext(); break;
                    case ArenaGenerationStage.Structures: mutations += StructureNext(); break;
                    case ArenaGenerationStage.Framing: mutations += FrameNext(); break;
                    case ArenaGenerationStage.Validating: mutations += ValidateNext(); break;
                }
            }
        }
        catch (Exception exception)
        {
            FailedStage = Stage;
            Error = exception;
            Stage = ArenaGenerationStage.Failed;
            Log.Error($"[WorldGenError] Arena generation failed during {FailedStage}: {exception}");
        }
    }

    private int GenerateNext()
    {
        Rectangle area = TerrainArea;
        int total = area.Width * area.Height;
        if (cursor >= total)
        {
            Advance(generator.UsesVanillaGeneration ? ArenaGenerationStage.VanillaGeneration : ArenaGenerationStage.Structures);
            return 0;
        }
        int x = area.Left + cursor % area.Width, y = area.Top + cursor / area.Width; cursor++;
        Apply(Main.tile[x, y], generator.GetTile(Layout, x, y));
        int mutations = 1;
        int mirror = Layout.MirrorRight - x;
        if (generator.MirrorGeneration && mirror >= 0 && mirror < Main.maxTilesX)
        {
            Tile mirroredTile = Main.tile[mirror, y];
            mirroredTile.CopyFrom(Main.tile[x, y]);
            mirroredTile.Slope = Mirror(mirroredTile.Slope);
            mutations++;
        }
        return mutations;
    }

    private int GenerateVanilla()
    {
        generator.GenerateVanilla(Layout);
        Advance(generator.MirrorGeneration ? ArenaGenerationStage.Mirroring : ArenaGenerationStage.Structures);
        return 1;
    }

    private int MirrorNext()
    {
        Rectangle area = LeftHalfArea;
        int total = area.Width * area.Height;
        if (cursor >= total) { Advance(ArenaGenerationStage.Structures); return 0; }
        int x = area.Left + cursor % area.Width, y = area.Top + cursor / area.Width; cursor++;
        int mirror = Layout.MirrorRight - x;
        Tile mirroredTile = Main.tile[mirror, y];
        mirroredTile.CopyFrom(Main.tile[x, y]);
        mirroredTile.Slope = Mirror(mirroredTile.Slope);
        return 2;
    }

    private int StructureNext()
    {
        int total = structurePoints.Count;
        if (cursor >= total)
        {
            generator.PlaceCombatStructures(Layout);
            Advance(ArenaGenerationStage.Framing);
            return 0;
        }
        Point p = structurePoints[cursor++];
        int x = p.X, y = p.Y;

        if (IsOuterFrame(p))
            Apply(Main.tile[x, y], ArenaTileData.Solid(TileID.LihzahrdBrick));
        else if ((generator.ClearBossArea && Layout.BossArea.Contains(p)) || Layout.RedSpawnClearance.Contains(p) || Layout.BlueSpawnClearance.Contains(p))
        {
            Tile tile = Main.tile[x, y];
            tile.ClearTile();
            tile.LiquidAmount = 0;
            tile.RedWire = tile.BlueWire = tile.GreenWire = tile.YellowWire = false;
            tile.HasActuator = tile.IsActuated = false;
        }
        return 1;
    }

    private void BuildStructurePoints()
    {
        Rectangle arena = Layout.ArenaArea;
        int outer = Layout.OuterBorderThickness;
        for (int x = arena.Left - outer; x < arena.Right + outer; x++)
            for (int layer = 1; layer <= outer; layer++)
            {
                structurePoints.Add(new Point(x, arena.Top - layer));
                structurePoints.Add(new Point(x, arena.Bottom + layer - 1));
            }
        for (int y = arena.Top; y < arena.Bottom; y++)
            for (int layer = 1; layer <= outer; layer++)
            {
                structurePoints.Add(new Point(arena.Left - layer, y));
                structurePoints.Add(new Point(arena.Right + layer - 1, y));
            }

        AddArea(Layout.RedSpawnClearance);
        AddArea(Layout.BlueSpawnClearance);
    }

    private void AddArea(Rectangle area)
    {
        for (int x = area.Left; x < area.Right; x++)
            for (int y = area.Top; y < area.Bottom; y++)
                structurePoints.Add(new Point(x, y));
    }

    private int FrameNext()
    {
        Rectangle area = FramingArea;
        int total = area.Width * area.Height;
        if (cursor >= total)
        {
            Advance(ArenaGenerationStage.Validating);
            return 0;
        }
        int x = area.Left + cursor % area.Width, y = area.Top + cursor / area.Width; cursor++;
        if (WorldGen.InWorld(x, y, 2))
        {
            Tile tile = Main.tile[x, y];
            bool frameTile = tile.HasTile && (Main.tileFrameImportant[tile.TileType] || HasTileEdge(x, y, tile.TileType));
            bool frameWall = tile.WallType != WallID.None && !tile.HasTile;
            if (!frameTile && !frameWall) return 0;
            if (frameTile) WorldGen.SquareTileFrame(x, y, true);
            if (frameWall) WorldGen.SquareWallFrame(x, y, true);
        }
        return 1;
    }

    private static bool HasTileEdge(int x, int y, ushort type) => !Main.tile[x - 1, y].HasTile || !Main.tile[x + 1, y].HasTile
        || !Main.tile[x, y - 1].HasTile || !Main.tile[x, y + 1].HasTile
        || Main.tile[x - 1, y].TileType != type || Main.tile[x + 1, y].TileType != type
        || Main.tile[x, y - 1].TileType != type || Main.tile[x, y + 1].TileType != type;

    private Rectangle FramingArea
    {
        get
        {
            Rectangle area = Layout.ArenaArea;
            area.Inflate(Layout.OuterBorderThickness, Layout.OuterBorderThickness);
            return area;
        }
    }

    private Rectangle LeftHalfArea => new(Layout.ArenaArea.Left, Layout.ArenaArea.Top,
        Layout.MirrorRightX - Layout.ArenaArea.Left, Layout.ArenaArea.Height);

    private Rectangle TerrainArea => generator.MirrorGeneration ? LeftHalfArea : Layout.ArenaArea;

    private int ValidateNext()
    {
        if (cursor++ > 0) return 0;
        foreach (Point point in structurePoints)
            if (IsOuterFrame(point) && (!Main.tile[point.X, point.Y].HasTile || Main.tile[point.X, point.Y].TileType != TileID.LihzahrdBrick))
                throw new InvalidOperationException($"Outer arena frame is incomplete at ({point.X},{point.Y}).");

        ValidateClearance(Layout.RedSpawnClearance, "red team");
        ValidateClearance(Layout.BlueSpawnClearance, "blue team");
        ValidateOpenSpawn(Layout.BossSpawn, "boss");
        Layout.Validate(Main.maxTilesX, Main.maxTilesY);
        if (generator.ValidateMirroring)
            ValidateMirrorSamples();
        if (Layout.Generator == ArenaGeneratorKind.PlanteraJungle)
        {
            int red = CountJungle(Layout.RedSpawn), blue = CountJungle(Layout.BlueSpawn);
            if (red < 140 || blue < 140)
                throw new InvalidOperationException($"Plantera arena jungle density is too low near a team spawn ({red}/{blue}).");
        }

        ArenaGenerationDiagnostics.ValidateOrThrow(Layout, generator);

        FinalizeWorld();
        Stage = ArenaGenerationStage.Complete;
        return 1;
    }

    private static bool TilesMirror(Tile left, Tile right) => left.HasTile == right.HasTile
        && (!left.HasTile || left.TileType == right.TileType)
        && left.WallType == right.WallType
        && left.LiquidAmount == right.LiquidAmount && left.LiquidType == right.LiquidType
        && left.RedWire == right.RedWire && left.BlueWire == right.BlueWire && left.GreenWire == right.GreenWire && left.YellowWire == right.YellowWire
        && left.HasActuator == right.HasActuator && left.IsActuated == right.IsActuated && left.IsHalfBlock == right.IsHalfBlock
        && right.Slope == Mirror(left.Slope);

    private void ValidateMirrorSamples()
    {
        for (int x = Layout.ArenaArea.Left; x < Layout.MirrorRightX; x += 23)
            for (int y = Layout.ArenaArea.Top; y < Layout.ArenaArea.Bottom; y += 23)
            {
                int mirror = Layout.MirrorRight - x;
                if (!TilesMirror(Main.tile[x, y], Main.tile[mirror, y]))
                    throw new InvalidOperationException($"Arena mirror validation failed at ({x},{y}) and ({mirror},{y}): left=[{DescribeTile(Main.tile[x, y])}], right=[{DescribeTile(Main.tile[mirror, y])}].");
            }
    }

    private static string DescribeTile(Tile tile) => $"tile={tile.HasTile}:{tile.TileType} wall={tile.WallType} liquid={tile.LiquidAmount}:{tile.LiquidType} slope={tile.Slope} half={tile.IsHalfBlock} wire={tile.RedWire}/{tile.BlueWire}/{tile.GreenWire}/{tile.YellowWire} actuator={tile.HasActuator}/{tile.IsActuated}";

    private static int CountJungle(Point spawn)
    {
        int count = 0;
        for (int x = Math.Max(1, spawn.X - 85); x <= Math.Min(Main.maxTilesX - 2, spawn.X + 85); x++)
            for (int y = Math.Max(1, spawn.Y - 65); y <= Math.Min(Main.maxTilesY - 2, spawn.Y + 65); y++)
                if (Main.tile[x, y].HasTile && Main.tile[x, y].TileType is TileID.JungleGrass or TileID.Hive or TileID.LihzahrdBrick) count++;
        return count;
    }

    private static void ValidateClearance(Rectangle area, string name)
    {
        for (int x = area.Left; x < area.Right; x++)
            for (int y = area.Top; y < area.Bottom; y++)
                if (Main.tile[x, y].HasTile) throw new InvalidOperationException($"The {name} spawn clearance is obstructed at ({x},{y}).");
    }

    private static void ValidateOpenSpawn(Point spawn, string name)
    {
        for (int x = spawn.X - 1; x <= spawn.X + 1; x++)
            for (int y = spawn.Y - 4; y <= spawn.Y; y++)
                if (!WorldGen.InWorld(x, y, 1) || Main.tile[x, y].HasTile)
                    throw new InvalidOperationException($"The {name} spawn is obstructed at ({x},{y}).");
    }

    private void FinalizeWorld()
    {
        Main.worldSurface = generator.WorldSurface; Main.rockLayer = generator.RockLayer;
        Main.spawnTileX = Layout.RedSpawn.X; Main.spawnTileY = Layout.RedSpawn.Y;
        Main.raining = false; Main.slimeRain = false; Main.bloodMoon = false; Main.eclipse = false;
        Main.maxRaining = 0f; Main.windSpeedCurrent = Main.windSpeedTarget = 0f;
        Main.pumpkinMoon = false; Main.snowMoon = false; Main.fastForwardTimeToDawn = Main.fastForwardTimeToDusk = false;
        Sandstorm.Happening = false;
        if (BirthdayParty.PartyIsUp) BirthdayParty.ToggleManualParty();
        Main.invasionType = 0; Main.invasionSize = 0; Main.invasionSizeStart = 0;
        Liquid.ReInit();
    }

    private bool IsOuterFrame(Point point)
    {
        Rectangle outer = Layout.ArenaArea; outer.Inflate(Layout.OuterBorderThickness, Layout.OuterBorderThickness);
        return outer.Contains(point) && !Layout.ArenaArea.Contains(point);
    }

    private void Advance(ArenaGenerationStage stage)
    {
        Log.Debug($"[WorldGen2] Completed {Stage}; advancing to {stage}.");
        if (stage == ArenaGenerationStage.Structures && structurePoints.Count == 0)
            BuildStructurePoints();
        Stage = stage;
        cursor = 0;
    }
    private static void Apply(Tile tile, ArenaTileData data)
    {
        tile.ClearEverything(); tile.WallType = data.WallType;
        if (!data.HasTile) return;
        tile.HasTile = true; tile.TileType = data.TileType; tile.Slope = data.Slope;
    }

    private static SlopeType Mirror(SlopeType slope) => slope switch
    {
        SlopeType.SlopeDownLeft => SlopeType.SlopeDownRight,
        SlopeType.SlopeDownRight => SlopeType.SlopeDownLeft,
        SlopeType.SlopeUpLeft => SlopeType.SlopeUpRight,
        SlopeType.SlopeUpRight => SlopeType.SlopeUpLeft,
        _ => slope
    };

    private static void ClearEntities()
    {
        for (int i = 0; i < Main.maxNPCs; i++)
        {
            if (!Main.npc[i].active) continue;
            Main.npc[i].active = false;
            if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, number: i);
        }
        for (int i = 0; i < Main.maxProjectiles; i++)
        {
            Projectile projectile = Main.projectile[i];
            if (!projectile.active) continue;
            int identity = projectile.identity, owner = projectile.owner;
            projectile.active = false;
            if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.KillProjectile, number: identity, number2: owner);
        }
        for (int i = 0; i < Main.maxItems; i++)
        {
            if (!Main.item[i].active) continue;
            Main.item[i].TurnToAir();
            Main.item[i].active = false;
            if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncItem, number: i);
        }
        for (int i = 0; i < Main.maxChests; i++) Main.chest[i] = null;
        for (int i = 0; i < Main.sign.Length; i++) Main.sign[i] = null;
        TileEntity.ByID.Clear(); TileEntity.ByPosition.Clear();
    }

}
