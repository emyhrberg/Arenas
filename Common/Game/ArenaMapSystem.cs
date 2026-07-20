using System;

namespace Arenas.Common.Game;

/// <summary>Locally reveals the world map for active rounds and clears it between rounds.</summary>
[Autoload(Side = ModSide.Client)]
internal sealed class ArenaMapSystem : ModSystem
{
    private const int UpdatesPerTick = 100_000;

    private bool revealStarted;
    private bool hiddenApplied;
    private int nextTile;

    public override void PostUpdateEverything()
    {
        if (Main.gameMenu || Main.Map == null || !Main.mapEnabled)
            return;

        RoundManager.RoundPhase phase = ModContent.GetInstance<RoundManager>().CurrentPhase;
        if (phase is RoundManager.RoundPhase.FreezeCountdown or RoundManager.RoundPhase.Playing)
        {
            hiddenApplied = false;
            if (!revealStarted)
            {
                revealStarted = true;
                nextTile = 0;
                Main.clearMap = false;
            }

            RevealBatch();
            return;
        }

        revealStarted = false;
        nextTile = 0;
        if (phase is not (RoundManager.RoundPhase.WaitingForPlayers
                or RoundManager.RoundPhase.VotingOrEndScreen) || hiddenApplied)
            return;

        Main.Map.Clear();
        Main.refreshMap = true;
        hiddenApplied = true;
    }

    public override void OnWorldUnload()
    {
        revealStarted = false;
        hiddenApplied = false;
        nextTile = 0;
    }

    private void RevealBatch()
    {
        if (Main.Map.MaxWidth != Main.maxTilesX || Main.Map.MaxHeight != Main.maxTilesY)
            return;

        int total = Main.maxTilesX * Main.maxTilesY;
        if (nextTile >= total)
            return;

        int end = Math.Min(total, nextTile + UpdatesPerTick);
        while (nextTile < end)
        {
            int x = nextTile % Main.maxTilesX;
            int y = nextTile / Main.maxTilesX;
            Main.Map.Update(x, y, byte.MaxValue);
            nextTile++;
        }

        Main.mapReady = true;
        Main.refreshMap = true;
    }
}
