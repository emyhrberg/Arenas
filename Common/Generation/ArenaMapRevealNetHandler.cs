using System;
using System.Collections.Generic;
using System.IO;
using Arenas.Common.Rounds;
using Terraria.ID;

namespace Arenas.Common.Generation;

internal static class ArenaMapRevealNetHandler
{
    private enum Packet : byte { RequestSections, SectionsComplete }

    internal static void HandlePacket(BinaryReader reader, int fromWho)
    {
        switch ((Packet)reader.ReadByte())
        {
            case Packet.RequestSections when Main.netMode == NetmodeID.Server:
            {
                int generationId = reader.ReadInt32();
                Main.QueueMainThreadAction(() => ArenaMapSectionSyncSystem.Queue(fromWho, generationId));
                break;
            }
            case Packet.SectionsComplete when Main.netMode == NetmodeID.MultiplayerClient:
            {
                int generationId = reader.ReadInt32();
                int width = reader.ReadInt32();
                int height = reader.ReadInt32();
                Main.QueueMainThreadAction(() => ArenaMapRevealSystem.NotifySectionsComplete(generationId, width, height));
                break;
            }
        }
    }

    internal static void RequestSections(int generationId)
    {
        if (Main.netMode != NetmodeID.MultiplayerClient)
            return;
        Write(Packet.RequestSections, packet => packet.Write(generationId)).Send();
    }

    internal static void SendComplete(int toClient, int generationId)
    {
        if (Main.netMode != NetmodeID.Server)
            return;
        Write(Packet.SectionsComplete, packet =>
        {
            packet.Write(generationId);
            packet.Write(Main.maxTilesX);
            packet.Write(Main.maxTilesY);
        }).Send(toClient);
    }

    private static ModPacket Write(Packet type, Action<ModPacket> write)
    {
        ModPacket packet = ModContent.GetInstance<Arenas>().GetPacket();
        packet.Write((byte)Arenas.ArenasPacketType.MapReveal);
        packet.Write((byte)type);
        write(packet);
        return packet;
    }
}

/// <summary>Sends a compact arena world to requesting clients in bounded section batches.</summary>
internal sealed class ArenaMapSectionSyncSystem : ModSystem
{
    private const int SectionWidth = 200;
    private const int SectionHeight = 150;
    private const int SectionsPerClientPerTick = 2;
    private static readonly Dictionary<int, Transfer> transfers = [];

    private sealed class Transfer(int generationId)
    {
        public int GenerationId { get; } = generationId;
        public int NextSection { get; set; }
    }

    internal static void Queue(int client, int generationId)
    {
        if (Main.netMode != NetmodeID.Server || !ArenaWorldSystem.Active || !ArenaWorldSystem.WorldReady ||
            client < 0 || client >= Main.maxPlayers || Main.player[client]?.active != true ||
            generationId != ArenaRoundSystem.GenerationId)
            return;

        transfers[client] = new Transfer(generationId);
        Log.Debug($"[MapRevealServer1] Queued all {Main.maxSectionsX * Main.maxSectionsY} sections for client={client}, generation={generationId}");
    }

    public override void PostUpdateEverything()
    {
        if (Main.netMode != NetmodeID.Server || transfers.Count == 0)
            return;

        List<int> finished = [];
        foreach ((int client, Transfer transfer) in transfers)
        {
            if (!ArenaWorldSystem.Active || transfer.GenerationId != ArenaRoundSystem.GenerationId ||
                client < 0 || client >= Main.maxPlayers || Main.player[client]?.active != true)
            {
                finished.Add(client);
                continue;
            }

            int total = Main.maxSectionsX * Main.maxSectionsY;
            for (int sent = 0; sent < SectionsPerClientPerTick && transfer.NextSection < total; sent++, transfer.NextSection++)
                SendExactSection(client, transfer.NextSection);

            if (transfer.NextSection < total)
                continue;

            ArenaMapRevealNetHandler.SendComplete(client, transfer.GenerationId);
            Log.Debug($"[MapRevealServer2] Finished all tile sections for client={client}, generation={transfer.GenerationId}");
            finished.Add(client);
        }

        foreach (int client in finished)
            transfers.Remove(client);
    }

    public override void OnWorldUnload() => transfers.Clear();

    private static void SendExactSection(int client, int index)
    {
        int sectionX = index % Main.maxSectionsX;
        int sectionY = index / Main.maxSectionsX;
        int startX = sectionX * SectionWidth;
        int startY = sectionY * SectionHeight;
        int width = Math.Min(SectionWidth, Main.maxTilesX - startX);
        int height = Math.Min(SectionHeight, Main.maxTilesY - startY);
        if (width <= 0 || height <= 0)
            return;

        // NetMessage.SendSection always serializes 200x150 and silently catches its
        // out-of-range exception on partial sections. SendData accepts exact dimensions.
        Netplay.Clients[client].TileSections[sectionX, sectionY] = true;
        NetMessage.SendData(MessageID.TileSection, client, -1, null, startX, startY, width, height);
    }
}
