using System;
using System.Diagnostics;
using Terraria.ID;

namespace Arenas.Common.Generation;

/// <summary>
/// Reveals every map tile on the client after entering an arena. A preliminary pass
/// makes the map useful immediately; multiplayer performs a second authoritative pass
/// after the server has resent every tile section.
/// </summary>
[Autoload(Side = ModSide.Client)]
internal sealed class ArenaMapRevealSystem : ModSystem
{
    private enum RevealStage : byte { Idle, Waiting, Building, Complete }

    private const int PreliminaryDelayTicks = 15;
    private const int RequestRetryTicks = 180;
    private const int CompleteVerificationTicks = 120;
    private const int MaxTileUpdatesPerTick = 40_000;
    private const int MapBuildBudgetMilliseconds = 6;

    private static RevealStage stage;
    private static int generationId = -1;
    private static int waitTicks;
    private static int requestCooldown;
    private static int retryCount;
    private static int nextTileIndex;
    private static int revealedTiles;
    private static int verificationCooldown;
    private static bool preliminaryComplete;
    private static bool serverFinishedSending;
    private static bool buildingFinalPass;

    public override void OnWorldUnload() => Reset();

    public override void PostUpdateEverything()
    {
        if (stage == RevealStage.Idle || !ArenaWorldSystem.Active || Main.gameMenu ||
            Main.Map == null || !Main.mapEnabled)
            return;

        if (stage == RevealStage.Complete)
        {
            VerifyCompletedReveal();
            return;
        }

        if (stage == RevealStage.Building)
        {
            BuildMapBatch();
            return;
        }

        WaitForWorldAndSections();
    }

    internal static void Request(ArenaLayout layout, int nextGenerationId)
    {
        if (Main.dedServ || layout == null || !Main.mapEnabled)
            return;

        // Round state is synchronized repeatedly. Do not restart an in-progress
        // full-world scan every time the same generation's state packet arrives.
        if (stage != RevealStage.Idle && generationId == nextGenerationId)
            return;

        Reset();
        generationId = nextGenerationId;
        stage = RevealStage.Waiting;
        serverFinishedSending = Main.netMode != NetmodeID.MultiplayerClient;
        Log.Debug($"[MapReveal1] Queued direct full-map reveal. generation={generationId}, world={Main.maxTilesX}x{Main.maxTilesY}, netMode={Main.netMode}");
    }

    internal static void NotifySectionsComplete(int completedGenerationId, int width, int height)
    {
        if (completedGenerationId != generationId || width != Main.maxTilesX || height != Main.maxTilesY)
            return;

        serverFinishedSending = true;
        Log.Debug($"[MapReveal2] Full arena tile stream received. generation={generationId}, world={width}x{height}");
    }

    internal static void Cancel()
    {
        if (!Main.dedServ)
            Reset();
    }

    private static void WaitForWorldAndSections()
    {
        if (Main.Map.MaxWidth != Main.maxTilesX || Main.Map.MaxHeight != Main.maxTilesY)
            return;

        waitTicks++;
        if (Main.netMode == NetmodeID.MultiplayerClient && !serverFinishedSending && --requestCooldown <= 0)
        {
            requestCooldown = RequestRetryTicks;
            retryCount++;
            ArenaMapRevealNetHandler.RequestSections(generationId);
            Log.Debug($"[MapReveal3] Requested every arena tile section. generation={generationId}, attempt={retryCount}");
        }

        // Do not gate the reveal on SectionLoaded/SectionFramed. A compact world's
        // partial edge sections do not reliably satisfy both flags. The completion
        // packet is ordered after the tile packets and is the authoritative final gate.
        if (serverFinishedSending)
        {
            BeginMapBuild(finalPass: true);
            return;
        }

        if (!preliminaryComplete && waitTicks >= PreliminaryDelayTicks)
            BeginMapBuild(finalPass: false);
    }

    private static void BeginMapBuild(bool finalPass)
    {
        buildingFinalPass = finalPass;
        nextTileIndex = 0;
        revealedTiles = 0;
        stage = RevealStage.Building;

        // Main.clearMap causes vanilla's later DrawToMap work to erase the values we
        // just wrote. ErkySSC's working reveal only updates WorldMap then refreshes it.
        Main.clearMap = false;
        Main.loadMap = false;
        Main.loadMapLock = false;
        Log.Debug($"[MapReveal4] Starting {(finalPass ? "final" : "preliminary")} direct reveal of {Main.maxTilesX * Main.maxTilesY:N0} tiles");
    }

    private static void BuildMapBatch()
    {
        int total = Main.maxTilesX * Main.maxTilesY;
        int updated = 0;
        Stopwatch timer = Stopwatch.StartNew();
        while (nextTileIndex < total && updated < MaxTileUpdatesPerTick && timer.ElapsedMilliseconds < MapBuildBudgetMilliseconds)
        {
            int x = nextTileIndex % Main.maxTilesX;
            int y = nextTileIndex / Main.maxTilesX;
            Main.Map.Update(x, y, byte.MaxValue);
            if (Main.Map.IsRevealed(x, y))
                revealedTiles++;
            nextTileIndex++;
            updated++;
        }

        if (nextTileIndex < total)
            return;

        Main.clearMap = false;
        Main.loadMap = false;
        Main.loadMapLock = false;
        Main.loadMapLastX = 0;
        Main.mapReady = true;
        Main.refreshMap = true;

        Log.Debug($"[MapReveal5] Direct map reveal finished. final={buildingFinalPass}, revealed={revealedTiles}/{total}, generation={generationId}");
        if (buildingFinalPass)
        {
            stage = RevealStage.Complete;
            verificationCooldown = CompleteVerificationTicks;
            return;
        }

        preliminaryComplete = true;
        waitTicks = 0;
        stage = RevealStage.Waiting;
    }

    private static void VerifyCompletedReveal()
    {
        if (--verificationCooldown > 0)
            return;
        verificationCooldown = CompleteVerificationTicks;

        // A late map-file load from another mod can clear WorldMap after arena entry.
        // Sample points across the whole world and automatically rebuild if that occurs.
        for (int x = Math.Max(1, Main.maxTilesX / 8); x < Main.maxTilesX; x += Math.Max(1, Main.maxTilesX / 4))
            for (int y = Math.Max(1, Main.maxTilesY / 8); y < Main.maxTilesY; y += Math.Max(1, Main.maxTilesY / 4))
                if (!Main.Map.IsRevealed(Math.Min(x, Main.maxTilesX - 1), Math.Min(y, Main.maxTilesY - 1)))
                {
                    Log.Warn("[MapReveal6] Completed map data was cleared after reveal; rebuilding it now");
                    BeginMapBuild(finalPass: true);
                    return;
                }
    }

    private static void Reset()
    {
        stage = RevealStage.Idle;
        generationId = -1;
        waitTicks = 0;
        requestCooldown = 0;
        retryCount = 0;
        nextTileIndex = 0;
        revealedTiles = 0;
        verificationCooldown = 0;
        preliminaryComplete = false;
        serverFinishedSending = false;
        buildingFinalPass = false;
    }
}

internal static class ArenaMapReveal
{
    internal static void Request(ArenaLayout layout, int generationId) => ArenaMapRevealSystem.Request(layout, generationId);
    internal static void Cancel() => ArenaMapRevealSystem.Cancel();
}
