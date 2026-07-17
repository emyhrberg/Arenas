using Arenas.Core.Configs.ConfigElements;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent.Events;
using Terraria.ID;

namespace Arenas.Common.Generation;

internal enum ArenaGenerationStage : byte { Clearing, Terrain, Structures, Framing, Validating, Complete, Failed }

internal sealed class ArenaGenerationJob
{
    private const int MutationBudget = 8000;
    private const double MillisecondBudget = 8d;
    private readonly IArenaGenerator generator;
    private readonly List<Point> structurePoints = [];
    private int cursor;
    private int redJungleTiles, blueJungleTiles;
    private bool initialized;

    public ArenaLayout Layout { get; }
    public ArenaGenerationStage Stage { get; private set; } = ArenaGenerationStage.Clearing;
    public ArenaGenerationStage FailedStage { get; private set; }
    public Exception Error { get; private set; }
    public bool IsComplete => Stage == ArenaGenerationStage.Complete;
    public bool HasFailed => Stage == ArenaGenerationStage.Failed;
    public float Progress => Stage switch
    {
        ArenaGenerationStage.Clearing => cursor / (float)Math.Max(1, Main.maxTilesX * Main.maxTilesY) * .4f,
        ArenaGenerationStage.Terrain => .4f + cursor / (float)Math.Max(1, 425 * Main.maxTilesY) * .35f,
        ArenaGenerationStage.Structures => .75f + cursor / (float)Math.Max(1, structurePoints.Count) * .1f,
        ArenaGenerationStage.Framing => .85f + cursor / (float)Math.Max(1, FramingArea.Width * FramingArea.Height) * .1f,
        ArenaGenerationStage.Validating => .95f + cursor / (float)Math.Max(1, 425 * Main.maxTilesY) * .05f,
        ArenaGenerationStage.Complete => 1f,
        _ => 0f
    };

    public ArenaGenerationJob(IArenaGenerator generator, int seed)
    {
        this.generator = generator;
        Layout = generator.CreateLayout(seed);
        Layout.Validate(Main.maxTilesX, Main.maxTilesY);
        generator.Prepare(Layout);
        BuildStructurePoints();
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
                    case ArenaGenerationStage.Clearing: mutations += ClearNext(); break;
                    case ArenaGenerationStage.Terrain: mutations += GenerateNext(); break;
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
        }
    }

    private int ClearNext()
    {
        int total = Main.maxTilesX * Main.maxTilesY;
        if (cursor >= total) { Advance(ArenaGenerationStage.Terrain); return 0; }
        int x = cursor % Main.maxTilesX, y = cursor / Main.maxTilesX; cursor++;
        Main.tile[x, y].ClearEverything();
        return 1;
    }

    private int GenerateNext()
    {
        int total = 425 * Main.maxTilesY;
        if (cursor >= total) { Advance(ArenaGenerationStage.Structures); return 0; }
        int x = cursor % 425, y = cursor / 425; cursor++;
        if (x < Layout.ArenaArea.Left || y < Layout.ArenaArea.Top || y >= Layout.ArenaArea.Bottom) return 1;
        Apply(Main.tile[x, y], generator.GetTile(Layout, x, y));
        int mirror = ArenaLayout.MirrorRight - x;
        if (mirror >= 0 && mirror < Main.maxTilesX)
        {
            Tile mirroredTile = Main.tile[mirror, y];
            mirroredTile.CopyFrom(Main.tile[x, y]);
            mirroredTile.Slope = Mirror(mirroredTile.Slope);
        }
        return 2;
    }

    private int StructureNext()
    {
        int total = structurePoints.Count;
        if (cursor >= total) { Advance(ArenaGenerationStage.Framing); return 0; }
        Point p = structurePoints[cursor++];
        int x = p.X, y = p.Y;

        if (IsOuterFrame(p))
            Apply(Main.tile[x, y], ArenaTileData.Solid(TileID.LihzahrdBrick));
        else if (IsBossFrame(p))
            Apply(Main.tile[x, y], ArenaTileData.Solid(TileID.Dirt));
        else if (Layout.RedSpawnClearance.Contains(p) || Layout.BlueSpawnClearance.Contains(p))
            Main.tile[x, y].ClearTile();
        return 1;
    }

    private void BuildStructurePoints()
    {
        Rectangle arena = Layout.ArenaArea;
        int outer = ArenaGeneratorRegistry.OuterBorderThickness;
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

        Rectangle boss = Layout.BossArea;
        for (int x = boss.Left - 1; x <= boss.Right; x++)
        {
            structurePoints.Add(new Point(x, boss.Top - 1));
            structurePoints.Add(new Point(x, boss.Bottom));
        }
        for (int y = boss.Top; y < boss.Bottom; y++)
        {
            Point left = new(boss.Left - 1, y), right = new(boss.Right, y);
            if (!Layout.LeftBossEntrance.Contains(left)) structurePoints.Add(left);
            if (!Layout.RightBossEntrance.Contains(right)) structurePoints.Add(right);
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
            if (!tile.HasTile && tile.WallType == WallID.None) return 0;
            WorldGen.SquareTileFrame(x, y, true);
            WorldGen.SquareWallFrame(x, y, true);
        }
        return 1;
    }

    private Rectangle FramingArea
    {
        get
        {
            Rectangle area = Layout.ArenaArea;
            area.Inflate(ArenaGeneratorRegistry.OuterBorderThickness, ArenaGeneratorRegistry.OuterBorderThickness);
            return area;
        }
    }

    private int ValidateNext()
    {
        int total = 425 * Main.maxTilesY;
        if (cursor >= total)
        {
            ValidateOpenSpawn(Layout.RedSpawn, "red team");
            ValidateOpenSpawn(Layout.BlueSpawn, "blue team");
            ValidateOpenSpawn(Layout.BossSpawn, "boss");
            if (Layout.Generator == ArenaGeneratorKind.PlanteraJungle && (redJungleTiles < 140 || blueJungleTiles < 140))
                throw new InvalidOperationException($"Plantera arena jungle density is too low near a team spawn ({redJungleTiles}/{blueJungleTiles}).");
            FinalizeWorld();
            Stage = ArenaGenerationStage.Complete;
            return 0;
        }

        int x = cursor % 425, y = cursor / 425; cursor++;
        int mirror = ArenaLayout.MirrorRight - x;
        if (mirror < 0 || mirror >= Main.maxTilesX) throw new InvalidOperationException("The fixed mirror axis is outside the loaded world.");
        Tile left = Main.tile[x, y], right = Main.tile[mirror, y];
        if (!TilesMirror(left, right)) throw new InvalidOperationException($"Arena mirror validation failed at ({x},{y}) and ({mirror},{y}).");
        ValidateStructuralTile(x, y, left);
        ValidateStructuralTile(mirror, y, right);

        if (Layout.Generator == ArenaGeneratorKind.PlanteraJungle)
        {
            if (left.HasTile && left.TileType == TileID.JungleGrass && Near(Layout.RedSpawn, x, y)) redJungleTiles++;
            if (right.HasTile && right.TileType == TileID.JungleGrass && Near(Layout.BlueSpawn, mirror, y)) blueJungleTiles++;
        }
        return 0;
    }

    private void ValidateStructuralTile(int x, int y, Tile tile)
    {
        Point point = new(x, y);
        if (IsOuterFrame(point) && (!tile.HasTile || tile.TileType != TileID.LihzahrdBrick))
            throw new InvalidOperationException($"Outer arena frame is incomplete at ({x},{y}).");
        if (IsBossFrame(point) && (!tile.HasTile || tile.TileType != TileID.Dirt))
            throw new InvalidOperationException($"Boss-area frame is incomplete at ({x},{y}).");
        if (Layout.RedSpawnClearance.Contains(point) || Layout.BlueSpawnClearance.Contains(point))
        {
            if (tile.HasTile) throw new InvalidOperationException($"Team spawn room is obstructed at ({x},{y}).");
        }
    }

    private static bool TilesMirror(Tile left, Tile right) => left.HasTile == right.HasTile
        && (!left.HasTile || left.TileType == right.TileType)
        && left.WallType == right.WallType
        && left.LiquidAmount == right.LiquidAmount && left.LiquidType == right.LiquidType
        && left.RedWire == right.RedWire && left.BlueWire == right.BlueWire && left.GreenWire == right.GreenWire && left.YellowWire == right.YellowWire
        && left.HasActuator == right.HasActuator && left.IsActuated == right.IsActuated && left.IsHalfBlock == right.IsHalfBlock
        && right.Slope == Mirror(left.Slope);

    private static bool Near(Point point, int x, int y) => Math.Abs(x - point.X) <= 85 && Math.Abs(y - point.Y) <= 65;

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
        Main.spawnTileX = ArenaGeneratorRegistry.WorldSpawn.X; Main.spawnTileY = ArenaGeneratorRegistry.WorldSpawn.Y;
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
        Rectangle outer = Layout.ArenaArea; outer.Inflate(ArenaGeneratorRegistry.OuterBorderThickness, ArenaGeneratorRegistry.OuterBorderThickness);
        return outer.Contains(point) && !Layout.ArenaArea.Contains(point);
    }

    private bool IsBossFrame(Point point)
    {
        Rectangle outer = Layout.BossArea; outer.Inflate(1, 1);
        return outer.Contains(point) && !Layout.BossArea.Contains(point)
            && !Layout.LeftBossEntrance.Contains(point) && !Layout.RightBossEntrance.Contains(point);
    }

    private void Advance(ArenaGenerationStage stage) { Stage = stage; cursor = 0; }
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
