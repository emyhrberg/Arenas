using System.Collections.Generic;
using System.IO;
using Terraria.ID;

namespace Arenas.Common.Rounds;

internal static class ArenaPlayerStatusNetHandler
{
    private enum Packet : byte
    {
        PingRequest,
        PingResponse,
        StatusUpdate,
        SyncStatus,
        FullSync,
        RemoveStatus
    }

    public static void HandlePacket(BinaryReader reader, int sender)
    {
        switch ((Packet)reader.ReadByte())
        {
            case Packet.PingRequest when Main.netMode == NetmodeID.Server:
                ReceivePingRequest(reader, sender);
                break;
            case Packet.PingResponse when Main.netMode == NetmodeID.MultiplayerClient:
                ArenaPlayerStatusSystem.ReceivePingResponse(reader.ReadInt64());
                break;
            case Packet.StatusUpdate when Main.netMode == NetmodeID.Server:
                ReceiveStatus(reader, sender);
                break;
            case Packet.SyncStatus when Main.netMode == NetmodeID.MultiplayerClient:
                ArenaPlayerStatusSystem.SetStatus(reader.ReadByte(), ReadStatus(reader));
                break;
            case Packet.FullSync when Main.netMode == NetmodeID.MultiplayerClient:
                ReceiveFullSync(reader);
                break;
            case Packet.RemoveStatus when Main.netMode == NetmodeID.MultiplayerClient:
                ArenaPlayerStatusSystem.RemoveStatus(reader.ReadByte());
                break;
        }
    }

    public static void SendPingRequest(long pingId)
    {
        if (Main.netMode != NetmodeID.MultiplayerClient)
            return;

        ModPacket packet = GetPacket(Packet.PingRequest);
        packet.Write(pingId);
        packet.Send();
    }

    public static void SendStatus(ArenaPlayerStatus status)
    {
        if (Main.netMode != NetmodeID.MultiplayerClient)
            return;

        ModPacket packet = GetPacket(Packet.StatusUpdate);
        WriteStatus(packet, status);
        packet.Send();
    }

    public static void SendRemoveStatus(int playerId)
    {
        if (Main.netMode != NetmodeID.Server || playerId < 0 || playerId >= Main.maxPlayers)
            return;

        ModPacket packet = GetPacket(Packet.RemoveStatus);
        packet.Write((byte)playerId);
        packet.Send();
    }

    private static void ReceivePingRequest(BinaryReader reader, int sender)
    {
        long pingId = reader.ReadInt64();
        if (sender < 0 || sender >= Main.maxPlayers || !Main.player[sender].active)
            return;

        ModPacket response = GetPacket(Packet.PingResponse);
        response.Write(pingId);
        response.Send(sender);

        if (!ArenaPlayerStatusSystem.Statuses.ContainsKey(sender))
            SendFullSync(sender);
    }

    private static void ReceiveStatus(BinaryReader reader, int sender)
    {
        ArenaPlayerStatus status = ArenaPlayerStatusSystem.Normalize(ReadStatus(reader));
        if (sender < 0 || sender >= Main.maxPlayers || !Main.player[sender].active)
            return;

        ArenaPlayerStatusSystem.SetStatus(sender, status);

        ModPacket packet = GetPacket(Packet.SyncStatus);
        packet.Write((byte)sender);
        WriteStatus(packet, status);
        packet.Send();
    }

    private static void SendFullSync(int toClient)
    {
        if (Main.netMode != NetmodeID.Server)
            return;

        IReadOnlyDictionary<int, ArenaPlayerStatus> statuses = ArenaPlayerStatusSystem.Statuses;
        ModPacket packet = GetPacket(Packet.FullSync);
        packet.Write((byte)statuses.Count);

        foreach ((int playerId, ArenaPlayerStatus status) in statuses)
        {
            packet.Write((byte)playerId);
            WriteStatus(packet, status);
        }

        packet.Send(toClient);
    }

    private static void ReceiveFullSync(BinaryReader reader)
    {
        int count = reader.ReadByte();
        for (int i = 0; i < count; i++)
            ArenaPlayerStatusSystem.SetStatus(reader.ReadByte(), ReadStatus(reader));
    }

    private static ArenaPlayerStatus ReadStatus(BinaryReader reader) => new(
        reader.ReadInt32(),
        reader.ReadString(),
        reader.ReadBoolean(),
        reader.ReadInt32());

    private static void WriteStatus(BinaryWriter writer, ArenaPlayerStatus status)
    {
        status = ArenaPlayerStatusSystem.Normalize(status);
        writer.Write(status.PingMs);
        writer.Write(status.SteamId ?? "");
        writer.Write(status.Dead);
        writer.Write(status.RespawnTimer);
    }

    private static ModPacket GetPacket(Packet type)
    {
        ModPacket packet = ModContent.GetInstance<Arenas>().GetPacket();
        packet.Write((byte)Arenas.ArenasPacketType.PlayerStatus);
        packet.Write((byte)type);
        return packet;
    }
}
