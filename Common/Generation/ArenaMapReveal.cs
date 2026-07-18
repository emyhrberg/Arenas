using System;
using System.Diagnostics;
using Terraria.ID;

namespace Arenas.Common.Generation;

/// <summary>
/// Reveals an arena world only after its complete tile stream has arrived. Map data is
/// rebuilt in bounded batches, then rendered through Main.DrawToMap on the graphics thread.
/// </summary>
[Autoload(Side = ModSide.Client)]
internal sealed class ArenaMapRevealSystem : ModSystem
{
    private enum RevealStage : byte { Idle, WaitingForSections, BuildingMap, RenderingMap, VerifyingMap, Complete }

    private const int RequestRetryTicks = 180;
    private const int MaxTileUpdatesPerTick = 20_000;
    private const int MapBuildBudgetMilliseconds = 5;
    private const int SectionWidth = 200;
    private const int SectionHeight = 150;

    private static RevealStage stage;
    private static int generationId = -1;
    private static int requestCooldown;
    private static int retryCount;
    private static int lastReadySectionCount = -1;
    private static bool serverFinishedSending;
    private static int nextTileIndex;
    private static int renderPassesRemaining;
    private static int verificationChangedTiles;
    private static int verificationHiddenTiles;

    public override void Load()
    {
        On_Main.DrawToMap += DrawToMap;
        On_Main.DrawToMap_Section += DrawToMapSection;
    }

    public override void OnWorldUnload() => Reset();

    public override void PostUpdateEverything()
    {
        if (stage == RevealStage.Idle || stage == RevealStage.Complete)
            return;

        if (!ArenaWorldSystem.Active || Main.gameMenu || Main.Map == null || Main.sectionManager == null)
            return;

        switch (stage)
        {
            case RevealStage.WaitingForSections:
                WaitForSections();
                break;
            case RevealStage.BuildingMap:
                BuildMapBatch();
                break;
            case RevealStage.RenderingMap:
                // DoDraw calls DrawToMap while loadMap is set. The hook below expands
                // its normal viewport-sized range to the whole compact arena world.
                Main.loadMap = true;
                Main.loadMapLock = true;
                Main.mapReady = false;
                break;
            case RevealStage.VerifyingMap:
                VerifyMapBatch();
                break;
        }
    }

    internal static void Request(ArenaLayout nextLayout, int nextGenerationId)
    {
        if (Main.dedServ || nextLayout == null || !Main.mapEnabled)
            return;

        if (stage != RevealStage.Idle && generationId == nextGenerationId &&
            Main.Map?.MaxWidth == Main.maxTilesX && Main.Map.MaxHeight == Main.maxTilesY)
            return;

        Reset();
        generationId = nextGenerationId;
        stage = RevealStage.WaitingForSections;
        requestCooldown = 0;
        serverFinishedSending = Main.netMode != NetmodeID.MultiplayerClient;
        Log.Debug($"[MapReveal1] Queued full map reveal. generation={generationId}, world={Main.maxTilesX}x{Main.maxTilesY}, netMode={Main.netMode}");
    }

    internal static void NotifySectionsComplete(int completedGenerationId, int width, int height)
    {
        if (stage != RevealStage.WaitingForSections || completedGenerationId != generationId ||
            width != Main.maxTilesX || height != Main.maxTilesY)
            return;

        serverFinishedSending = true;
        requestCooldown = RequestRetryTicks;
        Log.Debug($"[MapReveal2] Server finished sending every tile section. generation={generationId}, sections={Main.maxSectionsX}x{Main.maxSectionsY}");
    }

    internal static void Cancel()
    {
        if (!Main.dedServ)
            Reset();
    }

    private static void WaitForSections()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            int ready = CountReadySections();
            if (ready != lastReadySectionCount)
            {
                lastReadySectionCount = ready;
                if (retryCount > 0)
                    requestCooldown = RequestRetryTicks;
                Log.Debug($"[MapReveal3] Tile sections loaded and framed: {ready}/{Main.maxSectionsX * Main.maxSectionsY}");
            }

            if (!serverFinishedSending || ready != Main.maxSectionsX * Main.maxSectionsY)
            {
                if (--requestCooldown <= 0)
                {
                    retryCount++;
                    serverFinishedSending = false;
                    requestCooldown = RequestRetryTicks;
                    ArenaMapRevealNetHandler.RequestSections(generationId);
                    Log.Debug($"[MapReveal4] Requesting every arena tile section. generation={generationId}, attempt={retryCount}");
                }
                return;
            }
        }

        BeginMapBuild();
    }

    private static int CountReadySections()
    {
        int ready = 0;
        for (int x = 0; x < Main.maxSectionsX; x++)
            for (int y = 0; y < Main.maxSectionsY; y++)
                if (Main.sectionManager.SectionLoaded(x, y) && Main.sectionManager.SectionFramed(x, y))
                    ready++;
        return ready;
    }

    private static void BeginMapBuild()
    {
        if (Main.Map.MaxWidth != Main.maxTilesX || Main.Map.MaxHeight != Main.maxTilesY)
        {
            Log.Warn($"Map reveal delayed because WorldMap is {Main.Map.MaxWidth}x{Main.Map.MaxHeight}, expected {Main.maxTilesX}x{Main.maxTilesY}");
            return;
        }

        // Arena subworlds are disposable. Loading a character .map file here can restore
        // stale data from a previous arena (and ErkySSC can redirect that file), so derive
        // every map pixel directly from the received tiles. Updating every coordinate also
        // overwrites all stale WorldMap entries without a second full-world clear pass.
        Main.clearMap = true;
        Main.refreshMap = false;
        Main.updateMap = false;
        Main.loadMap = false;
        Main.loadMapLock = true;
        Main.loadMapLastX = 0;
        Main.mapReady = false;
        nextTileIndex = 0;
        stage = RevealStage.BuildingMap;
        Log.Debug($"[MapReveal5] Rebuilding all {Main.maxTilesX * Main.maxTilesY:N0} map tiles from synchronized world tiles");
    }

    private static void BuildMapBatch()
    {
        int total = Main.maxTilesX * Main.maxTilesY;
        int changed = 0;
        Stopwatch timer = Stopwatch.StartNew();
        while (nextTileIndex < total && changed < MaxTileUpdatesPerTick && timer.ElapsedMilliseconds < MapBuildBudgetMilliseconds)
        {
            int x = nextTileIndex % Main.maxTilesX;
            int y = nextTileIndex / Main.maxTilesX;
            Main.Map.Update(x, y, byte.MaxValue);
            nextTileIndex++;
            changed++;
        }

        if (nextTileIndex < total)
            return;

        int perPass = Math.Max(1, Main.maxMapUpdates - 1);
        renderPassesRemaining = Math.Max(2, (total + perPass - 1) / perPass + 1);
        stage = RevealStage.RenderingMap;
        Main.loadMap = true;
        Main.loadMapLock = true;
        Log.Debug($"[MapReveal6] Map tile data is complete; rendering the full map in {renderPassesRemaining} passes");
    }

    private static void DrawToMap(On_Main.orig_DrawToMap orig, Main self)
    {
        if (stage != RevealStage.RenderingMap || !ArenaWorldSystem.Active)
        {
            orig(self);
            return;
        }

        int oldMinX = Main.mapMinX, oldMaxX = Main.mapMaxX;
        int oldMinY = Main.mapMinY, oldMaxY = Main.mapMaxY;
        Main.mapMinX = 0;
        Main.mapMinY = 0;
        Main.mapMaxX = Main.maxTilesX;
        Main.mapMaxY = Main.maxTilesY;
        orig(self);
        Main.mapMinX = oldMinX;
        Main.mapMaxX = oldMaxX;
        Main.mapMinY = oldMinY;
        Main.mapMaxY = oldMaxY;

        if (--renderPassesRemaining > 0)
        {
            Main.loadMap = true;
            Main.loadMapLock = true;
            Main.mapReady = false;
            return;
        }

        Main.loadMap = false;
        Main.loadMapLastX = 0;
        nextTileIndex = 0;
        verificationChangedTiles = 0;
        verificationHiddenTiles = 0;
        stage = RevealStage.VerifyingMap;
        Main.mapReady = false;
    }

    private static void VerifyMapBatch()
    {
        int total = Main.maxTilesX * Main.maxTilesY;
        int checkedTiles = 0;
        Stopwatch timer = Stopwatch.StartNew();
        while (nextTileIndex < total && checkedTiles < MaxTileUpdatesPerTick && timer.ElapsedMilliseconds < MapBuildBudgetMilliseconds)
        {
            int x = nextTileIndex % Main.maxTilesX;
            int y = nextTileIndex / Main.maxTilesX;
            if (!Main.Map.IsRevealed(x, y))
            {
                Main.Map.Update(x, y, byte.MaxValue);
                verificationHiddenTiles++;
            }
            if (Main.Map[x, y].IsChanged)
                verificationChangedTiles++;
            nextTileIndex++;
            checkedTiles++;
        }

        if (nextTileIndex < total)
            return;

        if (verificationHiddenTiles > 0 || verificationChangedTiles > 0)
        {
            int outstanding = Math.Max(verificationHiddenTiles, verificationChangedTiles);
            int perPass = Math.Max(1, Main.maxMapUpdates - 1);
            renderPassesRemaining = Math.Max(2, (outstanding + perPass - 1) / perPass + 1);
            stage = RevealStage.RenderingMap;
            Main.loadMap = true;
            Main.loadMapLock = true;
            Log.Debug($"[MapReveal7] Verification found hidden={verificationHiddenTiles}, undrawn={verificationChangedTiles}; rendering again");
            return;
        }

        Main.loadMap = false;
        Main.loadMapLock = false;
        Main.loadMapLastX = 0;
        Main.updateMap = false;
        Main.mapReady = true;
        stage = RevealStage.Complete;
        Log.Debug($"[MapReveal8] Verified full client map reveal. generation={generationId}, world={Main.maxTilesX}x{Main.maxTilesY}");
    }

    private static void DrawToMapSection(On_Main.orig_DrawToMap_Section orig, Main self, int sectionX, int sectionY)
    {
        // Vanilla always reads 200x150 here. Compact subworlds such as 850x600 have
        // a partial rightmost section, so invoking vanilla for it reads beyond WorldMap.
        bool partial = (sectionX + 1) * SectionWidth > Main.maxTilesX ||
                       (sectionY + 1) * SectionHeight > Main.maxTilesY;
        if (ArenaWorldSystem.Active && partial)
            return;
        orig(self, sectionX, sectionY);
    }

    private static void Reset()
    {
        stage = RevealStage.Idle;
        generationId = -1;
        requestCooldown = 0;
        retryCount = 0;
        lastReadySectionCount = -1;
        serverFinishedSending = false;
        nextTileIndex = 0;
        renderPassesRemaining = 0;
        verificationChangedTiles = 0;
        verificationHiddenTiles = 0;
    }
}

internal static class ArenaMapReveal
{
    internal static void Request(ArenaLayout layout, int generationId) => ArenaMapRevealSystem.Request(layout, generationId);
    internal static void Cancel() => ArenaMapRevealSystem.Cancel();
}
