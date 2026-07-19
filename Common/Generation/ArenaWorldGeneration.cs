using Arenas.Core.Configs.ConfigElements;
using Arenas.Common.Rounds;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria.DataStructures;
using Terraria.GameContent.Generation;
using Terraria.ID;
using Terraria.IO;
using Terraria.WorldBuilding;

namespace Arenas.Common.Generation;

internal enum ArenaGenerationMode : byte
{
    Full,
    ThroughStep,
    ClearOnly
}

/// <summary>
/// The stable generation-step contract shared by the server, subserver, and admin UI.
/// A selected step means "clear and rebuild through this step". Vanilla passes are not
/// safe independent operations: most consume state and structures produced by earlier passes.
/// </summary>
internal static class ArenaWorldGenerationCatalog
{
    internal const string ReserveCombatRegion = "Arenas: Reserve Combat Region";
    internal const string BuildCombatRegion = "Arenas: Build Combat Region";
    internal const string ValidateCombatRegion = "Arenas: Validate Combat Region";

    internal static readonly string[] Steps =
    [
        "Reset",
        "Terrain",
        ReserveCombatRegion,
        "Dunes",
        "Ocean Sand",
        "Sand Patches",
        "Tunnels",
        "Mount Caves",
        "Dirt Wall Backgrounds",
        "Rocks In Dirt",
        "Dirt In Rocks",
        "Clay",
        "Small Holes",
        "Dirt Layer Caves",
        "Rock Layer Caves",
        "Surface Caves",
        "Wavy Caves",
        "Generate Ice Biome",
        "Grass",
        "Jungle",
        "Mud Caves To Grass",
        "Full Desert",
        "Floating Islands",
        "Mushroom Patches",
        "Marble",
        "Granite",
        "Dirt To Mud",
        "Silt",
        "Shinies",
        "Webs",
        "Underworld",
        "Corruption",
        "Lakes",
        "Dungeon",
        "Slush",
        "Mountain Caves",
        "Beaches",
        "Gems",
        "Gravitating Sand",
        "Create Ocean Caves",
        "Shimmer",
        "Clean Up Dirt",
        "Pyramids",
        "Dirt Rock Wall Runner",
        "Living Trees",
        "Wood Tree Walls",
        "Altars",
        "Wet Jungle",
        "Jungle Temple",
        "Hives",
        "Jungle Chests",
        "Settle Liquids",
        "Remove Water From Sand",
        "Oasis",
        "Shell Piles",
        "Smooth World",
        "Waterfalls",
        "Ice",
        "Wall Variety",
        "Life Crystals",
        "Statues",
        "Buried Chests",
        "Surface Chests",
        "Jungle Chests Placement",
        "Water Chests",
        "Spider Caves",
        "Gem Caves",
        "Moss",
        "Temple",
        "Cave Walls",
        "Jungle Trees",
        "Floating Island Houses",
        "Quick Cleanup",
        "Pots",
        "Hellforge",
        "Spreading Grass",
        "Surface Ore and Stone",
        "Place Fallen Log",
        "Traps",
        "Piles",
        "Spawn Point",
        "Grass Wall",
        "Guide",
        "Sunflowers",
        "Planting Trees",
        "Herbs",
        "Dye Plants",
        "Webs And Honey",
        "Weeds",
        "Glowing Mushrooms and Jungle Plants",
        "Jungle Plants",
        "Vines",
        "Flowers",
        "Mushrooms",
        "Gems In Ice Biome",
        "Random Gems",
        "Moss Grass",
        "Muds Walls In Jungle",
        "Larva",
        "Settle Liquids Again",
        "Cactus, Palm Trees, & Coral",
        "Tile Cleanup",
        "Lihzahrd Altars",
        "Micro Biomes",
        "Water Plants",
        "Stalac",
        "Remove Broken Traps",
        BuildCombatRegion,
        "Final Cleanup",
        ValidateCombatRegion
    ];

    internal static int IndexOf(string name) => Array.IndexOf(Steps, name);
    internal static bool IsValidIndex(int index) => index >= 0 && index < Steps.Length;
}

/// <summary>
/// Owns the complete Terraria generation invocation. This deliberately uses
/// WorldGen.GenerateWorld instead of calling GenPass.Apply directly so that vanilla and
/// mod lifecycle hooks, GenVars, structure maps, seeded RNG, and secret-seed setup all run.
/// </summary>
internal sealed class ArenaWorldGenerationSystem : ModSystem
{
    internal static bool IsGeneratingArena { get; private set; }
    internal static ArenaGenerationMode Mode { get; private set; }
    internal static string TargetStep { get; private set; } = "";
    internal static ArenaLayout GeneratedLayout { get; private set; }

    internal static void Generate(int seed, ArenaGenerationMode mode, string targetStep, GenerationProgress outerProgress)
    {
        if (mode == ArenaGenerationMode.ClearOnly)
        {
            // Subworld Library clears before Tasks, and this explicit clear makes the
            // operation's contract independent of that implementation detail.
            WorldGen.clearWorld();
            GeneratedLayout = null;
            outerProgress.Message = "Arenas world cleared";
            outerProgress.Set(1f);
            Log.Chat($"[WorldGen] Cleared the {Main.maxTilesX}x{Main.maxTilesY} Arenas world; no generation passes were run.");
            return;
        }

        if (mode == ArenaGenerationMode.ThroughStep && ArenaWorldGenerationCatalog.IndexOf(targetStep) < 0)
            throw new ArgumentOutOfRangeException(nameof(targetStep), targetStep, "Unknown Arenas generation step");

        GenerationProgress previousProgress = WorldGenerator.CurrentGenerationProgress;
        bool previousGeneratingWorld = WorldGen.generatingWorld;
        bool previousGen = WorldGen.gen;
        bool previousNoTileActions = WorldGen.noTileActions;
        GeneratedLayout = null;
        Mode = mode;
        TargetStep = targetStep ?? "";
        IsGeneratingArena = true;
        outerProgress.Message = mode == ArenaGenerationMode.Full
            ? "Generating complete vanilla Arenas world"
            : $"Generating through {TargetStep}";

        Log.Chat($"[WorldGen] Starting {(mode == ArenaGenerationMode.Full ? "complete generation" : $"generation through '{TargetStep}'")} with seed {seed}.");
        try
        {
            WorldGen.GenerateWorld(seed, new GenerationProgress());
            outerProgress.Set(1f);
            Log.Chat($"[WorldGen] Finished {(mode == ArenaGenerationMode.Full ? "all passes" : $"through '{TargetStep}'")}.");
        }
        catch (Exception exception)
        {
            Log.Error($"Controlled Arenas generation failed. mode={mode}, target='{TargetStep}', seed={seed}: {exception}");
            throw;
        }
        finally
        {
            IsGeneratingArena = false;
            TargetStep = "";
            WorldGenerator.CurrentGenerationProgress = previousProgress;
            WorldGen.generatingWorld = previousGeneratingWorld;
            WorldGen.gen = previousGen;
            WorldGen.noTileActions = previousNoTileActions;
        }
    }

    public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight)
    {
        if (!IsGeneratingArena)
            return;

        InsertAfter(tasks, "Terrain", new PassLegacy(ArenaWorldGenerationCatalog.ReserveCombatRegion, ReserveRegion, 0.01f));
        InsertBefore(tasks, "Final Cleanup", new PassLegacy(ArenaWorldGenerationCatalog.BuildCombatRegion, BuildRegion, 0.08f));
        InsertAfter(tasks, "Final Cleanup", new PassLegacy(ArenaWorldGenerationCatalog.ValidateCombatRegion, ValidateRegion, 0.01f));

        if (Mode == ArenaGenerationMode.ThroughStep)
        {
            int targetIndex = tasks.FindIndex(pass => pass.Name == TargetStep);
            if (targetIndex < 0)
            {
                string available = string.Join(", ", tasks.Select(pass => pass.Name));
                throw new InvalidOperationException($"World generation pass '{TargetStep}' was not registered. Available passes: {available}");
            }

            int removed = tasks.Count - targetIndex - 1;
            if (removed > 0)
                tasks.RemoveRange(targetIndex + 1, removed);
            Log.Info($"Trimmed the world generator to {tasks.Count} pass(es), ending at '{TargetStep}'.");
        }

        totalWeight = tasks.Sum(pass => pass.Weight);
        Log.Info($"Prepared {tasks.Count} deterministic pass(es): {string.Join(" -> ", tasks.Select(pass => pass.Name))}");
    }

    private static void InsertAfter(List<GenPass> tasks, string anchor, GenPass pass)
    {
        int index = tasks.FindIndex(candidate => candidate.Name == anchor);
        if (index < 0)
            throw new InvalidOperationException($"Cannot insert '{pass.Name}': vanilla anchor '{anchor}' was not found.");
        tasks.Insert(index + 1, pass);
    }

    private static void InsertBefore(List<GenPass> tasks, string anchor, GenPass pass)
    {
        int index = tasks.FindIndex(candidate => candidate.Name == anchor);
        if (index < 0)
            throw new InvalidOperationException($"Cannot insert '{pass.Name}': vanilla anchor '{anchor}' was not found.");
        tasks.Insert(index, pass);
    }

    private static void ReserveRegion(GenerationProgress progress, GameConfiguration configuration)
    {
        // Arena placement depends on terrain which does not exist yet at this point:
        // Plantera follows the generated underground Jungle and Golem follows the
        // generated Temple. Do not reserve or flatten either biome.
        progress.Message = "Waiting for the natural arena biome";
        GeneratedLayout = null;
        Log.Info($"Deferred Arenas placement until natural terrain is complete. preset={ArenaSubworldCoordinator.ActiveRequest.PresetIndex}.");
    }

    private static void BuildRegion(GenerationProgress progress, GameConfiguration configuration)
    {
        progress.Message = "Adding the Arenas perimeter";
        GeneratedLayout = CreatePresetLayout(ArenasSubworld.ActiveSeed);
        ArenaLayout layout = GeneratedLayout;
        Rectangle work = PerimeterEnvelope(layout);
        RemoveWorldObjects(layout, work);

        for (int x = work.Left; x < work.Right; x++)
        {
            progress.Set((x - work.Left) / (float)Math.Max(1, work.Width));
            for (int y = work.Top; y < work.Bottom; y++)
            {
                if (!layout.IsProtectedTile(x, y))
                    continue;

                Tile tile = Main.tile[x, y];
                ushort naturalWall = tile.WallType;
                tile.ClearEverything();
                tile.WallType = naturalWall;
                if (IsBorderTile(layout, x, y))
                {
                    tile.HasTile = true;
                    tile.TileType = TileID.LihzahrdBrick;
                }
            }
        }

        Main.spawnTileX = layout.RedSpawn.X;
        Main.spawnTileY = layout.RedSpawn.Y;
        progress.Set(1f);
        Log.Chat($"[WorldGen] Added a 3-tile Lihzahrd perimeter with 3-tile clearances around natural {layout.Generator} terrain at {layout.ArenaArea}; red={layout.RedSpawn}, blue={layout.BlueSpawn}, boss={layout.BossSpawn}.");
    }

    private static void ValidateRegion(GenerationProgress progress, GameConfiguration configuration)
    {
        progress.Message = "Validating the Arenas combat region";
        GeneratedLayout ??= CreatePresetLayout(ArenasSubworld.ActiveSeed);
        GeneratedLayout.Validate(Main.maxTilesX, Main.maxTilesY);
        Rectangle frame = PerimeterEnvelope(GeneratedLayout);
        for (int x = frame.Left; x < frame.Right; x++)
            for (int y = frame.Top; y < frame.Bottom; y++)
            {
                if (!GeneratedLayout.IsProtectedTile(x, y))
                    continue;

                Tile tile = Main.tile[x, y];
                bool border = IsBorderTile(GeneratedLayout, x, y);
                if (border && (!tile.HasTile || tile.TileType != TileID.LihzahrdBrick))
                    throw new InvalidOperationException($"Arena border is missing at ({x},{y}).");
                if (!border && tile.HasTile)
                    throw new InvalidOperationException($"Arena clearance contains tile {tile.TileType} at ({x},{y}).");
            }

        WorldGen.RangeFrame(frame.Left, frame.Top, frame.Right, frame.Bottom);
        Main.spawnTileX = GeneratedLayout.RedSpawn.X;
        Main.spawnTileY = GeneratedLayout.RedSpawn.Y;
        progress.Set(1f);
        Log.Info($"Validated natural {GeneratedLayout.Generator} arena and framed perimeter {frame} in {Main.maxTilesX}x{Main.maxTilesY}.");
    }

    private static ArenaLayout CreatePresetLayout(int seed)
    {
        BossFightPreset preset = ArenaRoundSystem.GetPresetOrDefault(ArenaSubworldCoordinator.ActiveRequest.PresetIndex);
        ArenaGeneratorKind kind = ResolveGeneratorKind(preset);
        if (kind == ArenaGeneratorKind.SandboxWorld)
            kind = ArenaGeneratorKind.Auto;

        Rectangle arena = kind switch
        {
            ArenaGeneratorKind.PlanteraJungle => FindPlanteraArena(),
            ArenaGeneratorKind.GolemTemple => FindGolemArena(),
            ArenaGeneratorKind.KingSlimeSurface or ArenaGeneratorKind.EyeSurface => FindSurfaceArena(),
            _ => CreateCenteredRectangle(Main.spawnTileX, (int)Main.worldSurface + 120, 1360, 500)
        };

        return CreateLayout(kind, seed, arena);
    }

    private static ArenaGeneratorKind ResolveGeneratorKind(BossFightPreset preset)
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

    private static ArenaLayout CreateLayout(ArenaGeneratorKind kind, int seed, Rectangle arena)
    {
        int bossWidth = Math.Clamp(arena.Width * 3 / 10, 120, Math.Min(400, arena.Width - 240));
        Rectangle boss = new(arena.Center.X - bossWidth / 2, arena.Top, bossWidth, arena.Height);
        int sideWidth = boss.Left - arena.Left;
        int spawnWidth = Math.Clamp(sideWidth - 24, 20, 100);
        int spawnTop = arena.Bottom - ArenaLayout.BorderClearanceThickness;
        Rectangle redRoom = new(arena.Left + 12, spawnTop, spawnWidth, ArenaLayout.BorderClearanceThickness);
        Rectangle blueRoom = new(arena.Right - 12 - spawnWidth, spawnTop, spawnWidth, ArenaLayout.BorderClearanceThickness);
        Point preferredBoss = kind == ArenaGeneratorKind.GolemTemple
            && boss.Contains(GenVars.lAltarX, GenVars.lAltarY)
            ? new Point(GenVars.lAltarX, GenVars.lAltarY)
            : boss.Center;
        Point bossSpawn = FindOpenBossSpawn(boss, preferredBoss);

        return new ArenaLayout
        {
            Generator = kind,
            Seed = seed,
            WorldWidth = Main.maxTilesX,
            WorldHeight = Main.maxTilesY,
            ArenaArea = arena,
            BossArea = boss,
            RedSpawnClearance = redRoom,
            BlueSpawnClearance = blueRoom,
            RedSpawn = new Point(redRoom.Center.X, redRoom.Bottom - 1),
            BlueSpawn = new Point(blueRoom.Center.X, blueRoom.Bottom - 1),
            BossSpawn = bossSpawn,
            StagingLobby = redRoom,
            OuterBorderThickness = 3,
            TeamBorderWidth = 3,
            BlueBorderX = boss.Left,
            RedBorderX = boss.Right,
            AutoPlaceTeamSpawns = false,
            AutoPlaceBossSpawn = false
        };
    }

    private static Rectangle FindPlanteraArena()
    {
        int scanTop = Math.Clamp((int)Main.worldSurface + 60, 80, Main.maxTilesY - 260);
        int scanBottom = Math.Max(scanTop + 1, Main.maxTilesY - 180);
        long sumX = 0, sumY = 0, count = 0;
        for (int x = 80; x < Main.maxTilesX - 80; x += 3)
            for (int y = scanTop; y < scanBottom; y += 3)
                if (IsUndergroundJungle(Main.tile[x, y]))
                {
                    sumX += x;
                    sumY += y;
                    count++;
                }

        int fallbackX = GenVars.jungleOriginX is > 0 and < ArenasSubworld.FixedWidth
            ? GenVars.jungleOriginX
            : Main.maxTilesX / 2;
        int centerX = count > 0 ? (int)(sumX / count) : fallbackX;
        int centerY = count > 0 ? (int)(sumY / count) : Math.Clamp((int)Main.rockLayer + 220, scanTop, scanBottom);
        Log.Info($"Located underground Jungle from {count} sampled natural tiles at center=({centerX},{centerY}), origin={GenVars.jungleOriginX}.");
        return CreateCenteredRectangle(centerX, centerY, 1360, 600);
    }

    private static Rectangle FindGolemArena()
    {
        Rectangle temple;
        if (GenVars.tLeft > 0 && GenVars.tRight > GenVars.tLeft && GenVars.tTop > 0 && GenVars.tBottom > GenVars.tTop)
        {
            temple = new Rectangle(GenVars.tLeft, GenVars.tTop, GenVars.tRight - GenVars.tLeft + 1, GenVars.tBottom - GenVars.tTop + 1);
        }
        else if (!TryFindTempleTiles(out temple))
        {
            Log.Warn("Vanilla Temple bounds were unavailable; centering the Golem arena on the underground Jungle instead.");
            return FindPlanteraArena();
        }

        int width = Math.Clamp(temple.Width + 60, 600, 1360);
        int height = Math.Clamp(temple.Height + 60, 400, 600);
        Log.Info($"Located vanilla Temple at {temple}; centering Golem arena around it.");
        return CreateCenteredRectangle(temple.Center.X, temple.Center.Y, width, height);
    }

    private static Rectangle FindSurfaceArena() =>
        CreateCenteredRectangle(Main.spawnTileX, (int)Main.worldSurface + 120, 1360, 500);

    private static Rectangle CreateCenteredRectangle(int centerX, int centerY, int width, int height)
    {
        int margin = 3 + ArenaLayout.BorderClearanceThickness + 5;
        width = Math.Clamp(width, 400, Main.maxTilesX - margin * 2);
        height = Math.Clamp(height, 200, Main.maxTilesY - margin * 2);
        int left = Math.Clamp(centerX - width / 2, margin, Main.maxTilesX - margin - width);
        int top = Math.Clamp(centerY - height / 2, margin, Main.maxTilesY - margin - height);
        return new Rectangle(left, top, width, height);
    }

    private static bool IsUndergroundJungle(Tile tile) =>
        tile.WallType == WallID.JungleUnsafe
        || tile.HasTile && tile.TileType is TileID.JungleGrass or TileID.Hive;

    private static bool TryFindTempleTiles(out Rectangle temple)
    {
        int left = Main.maxTilesX, top = Main.maxTilesY, right = -1, bottom = -1;
        for (int x = 40; x < Main.maxTilesX - 40; x += 2)
            for (int y = 80; y < Main.maxTilesY - 80; y += 2)
            {
                Tile tile = Main.tile[x, y];
                if ((!tile.HasTile || tile.TileType != TileID.LihzahrdBrick)
                    && tile.WallType != WallID.LihzahrdBrickUnsafe)
                    continue;
                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }

        if (right <= left || bottom <= top)
        {
            temple = Rectangle.Empty;
            return false;
        }

        temple = new Rectangle(left, top, right - left + 1, bottom - top + 1);
        return true;
    }

    private static Point FindOpenBossSpawn(Rectangle area, Point preferred)
    {
        Point best = preferred;
        int bestDistance = int.MaxValue;
        for (int x = area.Left + 5; x < area.Right - 5; x += 2)
            for (int y = area.Top + 6; y < area.Bottom - 6; y += 2)
            {
                if (!IsOpenBossSpace(x, y))
                    continue;
                int distance = Math.Abs(x - preferred.X) + Math.Abs(y - preferred.Y);
                if (distance >= bestDistance)
                    continue;
                best = new Point(x, y);
                bestDistance = distance;
            }

        if (bestDistance == int.MaxValue)
            Log.Warn($"No naturally open boss pocket was found in {area}; using requested point {preferred} without altering terrain.");
        return best;
    }

    private static bool IsOpenBossSpace(int centerX, int centerY)
    {
        for (int x = centerX - 3; x <= centerX + 3; x++)
            for (int y = centerY - 4; y <= centerY + 4; y++)
            {
                Tile tile = Main.tile[x, y];
                if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
                    return false;
            }
        return true;
    }

    private static Rectangle PerimeterEnvelope(ArenaLayout layout)
    {
        Rectangle area = layout.ArenaArea;
        int distance = layout.OuterBorderThickness + ArenaLayout.BorderClearanceThickness;
        area.Inflate(distance, distance);
        return area;
    }

    private static bool IsBorderTile(ArenaLayout layout, int x, int y)
    {
        Rectangle borderOuter = layout.ArenaArea;
        borderOuter.Inflate(layout.OuterBorderThickness, layout.OuterBorderThickness);
        return borderOuter.Contains(x, y) && !layout.ArenaArea.Contains(x, y);
    }

    private static void RemoveWorldObjects(ArenaLayout layout, Rectangle area)
    {
        for (int i = 0; i < Main.maxChests; i++)
            if (Main.chest[i] is Chest chest && TouchesPerimeter(layout, new Rectangle(chest.x, chest.y, 2, 2)))
                Main.chest[i] = null;

        for (int i = 0; i < Main.sign.Length; i++)
            if (Main.sign[i] is Sign sign && TouchesPerimeter(layout, new Rectangle(sign.x, sign.y, 2, 2)))
                Main.sign[i] = null;

        foreach ((Point16 position, TileEntity entity) in TileEntity.ByPosition.ToArray())
        {
            if (!area.Contains(position.X, position.Y) || !layout.IsProtectedTile(position.X, position.Y))
                continue;
            TileEntity.ByPosition.Remove(position);
            TileEntity.ByID.Remove(entity.ID);
        }
    }

    private static bool TouchesPerimeter(ArenaLayout layout, Rectangle footprint)
    {
        for (int x = footprint.Left; x < footprint.Right; x++)
            for (int y = footprint.Top; y < footprint.Bottom; y++)
                if (layout.IsProtectedTile(x, y))
                    return true;
        return false;
    }
}
