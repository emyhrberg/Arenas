using Arenas.Common.Generation;
using Arenas.Common.Rounds;
using Arenas.Core.Configs.ConfigElements;
using Microsoft.Xna.Framework;
using SubworldLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria.GameContent.Generation;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace Arenas.Common;

/// <summary>
/// A disposable world created by Subworld Library for exactly one arena layout.
/// The main world is never used as a generation target.
/// </summary>
public sealed class ArenasSubworld : Subworld
{
    private static ArenaGenerationJob generationJob;
    private static IArenaGenerator selectedGenerator;
    private static ArenaGeometryConfig selectedGeometry;

    internal static ArenaLayout GeneratedLayout { get; private set; }

    public override int Width => RequestedGeometry().WorldWidth;
    public override int Height => RequestedGeometry().WorldHeight;
    public override bool ShouldSave => false;
    public override bool NoPlayerSaving => true;
    public override bool NormalUpdates => true;

    public override List<GenPass> Tasks =>
    [
        new PassLegacy("Select Arena Preset", SelectPreset, 0.05f),
        new PassLegacy("Generate Boss Arena", GenerateArena, 0.90f),
        new PassLegacy("Finalize Arena", FinalizeArena, 0.05f)
    ];

    private static void SelectPreset(GenerationProgress progress, GameConfiguration configuration)
    {
        progress.Message = "Selecting boss arena";
        GeneratedLayout = null;
        generationJob = null;
        selectedGenerator = null;
        selectedGeometry = null;

        ArenaSubworldRequest request = ArenaSubworldCoordinator.ActiveRequest;
        BossFightPreset preset = ArenaRoundSystem.GetPresetOrDefault(request.PresetIndex);
        if (preset == null || !ArenaGeneratorRegistry.TryResolve(preset, out IArenaGenerator generator))
            throw new InvalidOperationException($"Fight preset {request.PresetIndex} has no valid arena generator.");

        Log.Debug($"[WorldGen1] Running preset selection. preset={request.PresetIndex}, boss={preset.Boss.DisplayName}, generator={generator.Kind}, seed={request.Seed}.");
        selectedGenerator = generator;
        selectedGeometry = ArenaGeneratorRegistry.ResolveGeometry(preset);
        Log.Debug($"[WorldGen1] Geometry world={selectedGeometry.WorldWidth}x{selectedGeometry.WorldHeight} arena=({selectedGeometry.ArenaLeft},{selectedGeometry.ArenaTop})..({selectedGeometry.ArenaRight},{selectedGeometry.ArenaBottom}) boss=({selectedGeometry.BossAreaX},{selectedGeometry.BossAreaY},{selectedGeometry.BossAreaWidth},{selectedGeometry.BossAreaHeight}) borders={selectedGeometry.BlueBorderX}/{selectedGeometry.RedBorderX}");
        if (Main.maxTilesX != selectedGeometry.WorldWidth || Main.maxTilesY != selectedGeometry.WorldHeight)
            throw new InvalidOperationException($"Subworld Library created {Main.maxTilesX}x{Main.maxTilesY}, but preset '{ArenaRoundSystem.PresetName(preset)}' requested {selectedGeometry.WorldWidth}x{selectedGeometry.WorldHeight}");
        if (generator.Kind != ArenaGeneratorKind.SandboxWorld)
            generationJob = new ArenaGenerationJob(generator, selectedGeometry, request.Seed);
    }

    private static void GenerateArena(GenerationProgress progress, GameConfiguration configuration)
    {
        if (selectedGenerator?.Kind == ArenaGeneratorKind.SandboxWorld)
        {
            LoadSandboxWorld(progress);
            return;
        }

        if (generationJob == null)
            throw new InvalidOperationException("The arena generation job was not initialized.");

        Log.Debug($"[WorldGen2] Generating terrain and structures for {generationJob.Layout.Generator}.");
        progress.Message = $"Generating {generationJob.Layout.Generator} arena";
        while (!generationJob.IsComplete && !generationJob.HasFailed)
        {
            generationJob.Tick();
            progress.Set(generationJob.Progress);
        }

        if (generationJob.HasFailed)
            throw new InvalidOperationException($"Arena generation failed during {generationJob.FailedStage}.", generationJob.Error);

        GeneratedLayout = generationJob.Layout;
        Log.Debug($"[WorldGen2] Arena generation completed. generator={GeneratedLayout.Generator}, seed={GeneratedLayout.Seed}.");
    }

    private static void LoadSandboxWorld(GenerationProgress progress)
    {
        const string path = "Core/WorldFiles/Arenas_v10.wld";
        progress.Message = "Loading Sandbox world";
        Log.Debug($"[WorldGen2.Sandbox] Loading bundled world file '{path}' only.");

        byte[] bytes = ModContent.GetInstance<Arenas>().GetFileBytes(path);
        if (bytes == null || bytes.Length == 0)
            throw new InvalidOperationException($"Bundled Sandbox world file '{path}' is missing or empty.");

        using MemoryStream stream = new(bytes, writable: false);
        using BinaryReader reader = new(stream);
        WorldFile.LoadWorld_Version2(reader);

        ArenaSubworldRequest request = ArenaSubworldCoordinator.ActiveRequest;
        GeneratedLayout = selectedGenerator.CreateLayout(selectedGeometry ?? RequestedGeometry(), request.Seed);
        progress.Set(1f);
        Log.Debug($"[WorldGen2.Sandbox] Loaded {Main.maxTilesX}x{Main.maxTilesY} world. spawn=({Main.spawnTileX},{Main.spawnTileY}), bytes={bytes.Length}.");
    }

    private static void FinalizeArena(GenerationProgress progress, GameConfiguration configuration)
    {
        if (GeneratedLayout == null)
            throw new InvalidOperationException("Arena generation completed without a layout.");

        Log.Debug($"[WorldGen3] Finalizing arena spawn and world state. spawn={GeneratedLayout.RedSpawn}, bossSpawn={GeneratedLayout.BossSpawn}.");
        progress.Message = "Preparing the fight";
        if (GeneratedLayout.Generator != ArenaGeneratorKind.SandboxWorld)
        {
            Main.spawnTileX = GeneratedLayout.RedSpawn.X;
            Main.spawnTileY = GeneratedLayout.RedSpawn.Y;
        }
        progress.Set(1f);
        generationJob = null;
        selectedGenerator = null;
        selectedGeometry = null;
    }

    public override void OnEnter()
    {
        SubworldSystem.noReturn = true;
        SubworldSystem.hideUnderworld = true;
    }

    public override void OnLoad()
    {
        SubworldSystem.noReturn = true;
        SubworldSystem.hideUnderworld = true;

        Log.Debug($"[WorldGen4] Arenas subworld OnLoad. netMode={Main.netMode}, gameMenu={Main.gameMenu}, generatedLayout={GeneratedLayout != null}.");

        if (Main.netMode != NetmodeID.MultiplayerClient)
        {
            ArenaWorldSystem.InitializeSubworld(GeneratedLayout);
            ArenaSubworldCoordinator.QueueSubworldRoundStart();
        }

        // Multiplayer clients request the reveal after the authoritative round state
        // (layout + generation ID) arrives. Revealing here races the tile-section stream.
        if (Main.netMode == NetmodeID.SinglePlayer && GeneratedLayout != null)
            Main.QueueMainThreadAction(() => ArenaMapReveal.Request(GeneratedLayout, ArenaRoundSystem.GenerationId));
    }

    public override void OnUnload()
    {
        generationJob = null;
        selectedGenerator = null;
        selectedGeometry = null;
        GeneratedLayout = null;
    }

    private static ArenaGeometryConfig RequestedGeometry()
    {
        BossFightPreset preset = ArenaRoundSystem.GetPresetOrDefault(ArenaSubworldCoordinator.ActiveRequest.PresetIndex);
        ArenaGeometryConfig geometry = ArenaGeneratorRegistry.ResolveGeometry(preset);
        return geometry ?? ArenaGeometryDefaults.Create(ArenaGeneratorKind.KingSlimeSurface);
    }

    public override bool GetLight(Tile tile, int x, int y, ref Terraria.Utilities.FastRandom rand, ref Vector3 color)
    {
        color.X = Math.Max(color.X, 0.004f);
        color.Y = Math.Max(color.Y, 0.004f);
        color.Z = Math.Max(color.Z, 0.004f);
        return false;
    }
}
