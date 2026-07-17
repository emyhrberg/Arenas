using Arenas.Common.Generation;
using Arenas.Common.Rounds;
using System;
using System.IO;
using Terraria.ID;

namespace Arenas.Common;

/// <summary>Owns the Game Manager controlled empty world and its current generated layout.</summary>
internal sealed class ArenaWorldSystem : ModSystem
{
    private static ArenaWorldClearJob clearJob;
    private static bool remoteClearing;
    private static float remoteClearingProgress;
    private static int worldRevision;

    public static ArenaLayout Layout { get; private set; }
    public static bool Active => !Main.gameMenu && Main.ActiveWorldFileData != null;
    public static bool WorldReady { get; private set; }
    public static bool IsClearing => clearJob != null || remoteClearing;
    public static float ClearingProgress => clearJob?.Progress ?? (remoteClearing ? remoteClearingProgress : WorldReady ? 1f : 0f);

    public override void ClearWorld()
    {
        clearJob = null;
        remoteClearing = false;
        remoteClearingProgress = 0f;
        worldRevision = 0;
        WorldReady = false;
        Layout = null;
    }

    public override void OnWorldLoad()
    {
        Layout = null;
        clearJob = null;
        remoteClearing = false;
        remoteClearingProgress = 0f;
        worldRevision = 0;
        WorldReady = true;
        if (Main.netMode != NetmodeID.MultiplayerClient)
            ArenaWorldClearJob.PrepareWorldSpawn();
    }

    public override void OnWorldUnload()
    {
        clearJob = null;
        remoteClearing = false;
        remoteClearingProgress = 0f;
        worldRevision = 0;
        WorldReady = false;
        Layout = null;
    }

    public override void NetSend(BinaryWriter writer)
    {
        writer.Write(WorldReady);
        writer.Write(worldRevision);
        writer.Write(Layout != null);
        Layout?.Write(writer);
    }

    public override void NetReceive(BinaryReader reader)
    {
        WorldReady = reader.ReadBoolean();
        int revision = reader.ReadInt32();
        bool worldChanged = revision > 0 && revision != worldRevision;
        worldRevision = revision;
        Layout = reader.ReadBoolean() ? ArenaLayout.Read(reader) : null;
        if (worldChanged && WorldReady && Layout == null && !Main.dedServ)
        {
            Main.QueueMainThreadAction(() =>
            {
                Main.Map.Clear();
                Main.sectionManager.SetAllFramedSectionsAsNeedingRefresh();
            });
        }
    }

    public override void PostUpdateWorld()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || clearJob == null) return;
        clearJob.Tick();
        if (clearJob.Error != null)
        {
            Log.Error($"Failed to clear the loaded world for Arenas: {clearJob.Error}");
            clearJob = null;
            WorldReady = false;
            return;
        }
        if (!clearJob.IsComplete) return;

        clearJob = null;
        remoteClearing = false;
        remoteClearingProgress = 0f;
        Layout = null;
        WorldReady = true;
        worldRevision++;
        if (!Main.dedServ)
        {
            Main.Map.Clear();
            Main.sectionManager.SetAllFramedSectionsAsNeedingRefresh();
        }
        SyncEmptyWorld();
        ArenaRoundSystem.OnWorldClearCompleted();
        Log.Info("The Arenas Game Manager cleared the world and prepared the central spawn platform.");
    }

    internal static bool BeginClearWorld()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || clearJob != null)
            return false;

        Layout = null;
        WorldReady = false;
        clearJob = new ArenaWorldClearJob();
        ArenaRoundNetHandler.SendStateToAll();
        return true;
    }

    internal static void BeginGeneration()
    {
        clearJob = null;
        WorldReady = false;
        Layout = null;
    }

    internal static void CompleteGeneration(ArenaLayout layout)
    {
        Layout = layout;
        WorldReady = layout != null;
    }

    internal static void ApplyNetworkLayout(ArenaLayout layout)
    {
        Layout = layout;
        WorldReady = layout != null;
    }

    internal static void ApplyNetworkClearing(bool clearing, float progress)
    {
        remoteClearing = clearing;
        remoteClearingProgress = Math.Clamp(progress, 0f, 1f);
        if (clearing)
        {
            WorldReady = false;
            Layout = null;
        }
    }

    private static void SyncEmptyWorld()
    {
        if (Main.netMode != NetmodeID.Server) return;
        Netplay.ResetSections();
        int sectionsX = Netplay.GetSectionX(Main.maxTilesX - 1) + 1;
        int sectionsY = Netplay.GetSectionY(Main.maxTilesY - 1) + 1;
        for (int client = 0; client < Main.maxPlayers; client++)
        {
            if (Main.player[client]?.active != true) continue;
            for (int sectionX = 0; sectionX < sectionsX; sectionX++)
                for (int sectionY = 0; sectionY < sectionsY; sectionY++)
                    NetMessage.SendSection(client, sectionX, sectionY);
        }
        NetMessage.SendData(MessageID.WorldData);
        ArenaRoundNetHandler.SendStateToAll();
    }
}

internal sealed class ArenaSpawnSuppression : GlobalNPC
{
    public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns)
    {
        if (!ArenaWorldSystem.Active) return;
        spawnRate = int.MaxValue;
        maxSpawns = 0;
    }
}
