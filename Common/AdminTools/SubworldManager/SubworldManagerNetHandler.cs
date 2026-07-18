using Arenas.Core.Compat;
using SubworldLibrary;
using System;
using System.IO;
using Terraria.ID;

namespace Arenas.Common.AdminTools.SubworldManager;

internal static class SubworldManagerNetHandler
{
    public enum SubworldManagerPacketType : byte
    {
        SendAllToMainWorld = 0,
        SendAllToArenas = 1,
        SendPlayerToMainWorld = 2,
        SendPlayerToArenas = 3
    }

    public static void Request(SubworldManagerPacketType type, int targetPlayer = -1)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
        {
            ExecuteLocally(type, targetPlayer);
            return;
        }

        if (Main.netMode != NetmodeID.MultiplayerClient)
            return;

        var packet = ModContent.GetInstance<Arenas>().GetPacket();
        packet.Write((byte)Arenas.ArenasPacketType.SubworldManager);
        packet.Write((byte)type);
        packet.Write((short)targetPlayer);
        packet.Send();
    }

    public static void HandlePacket(BinaryReader reader, int fromWho)
    {
        var type = (SubworldManagerPacketType)reader.ReadByte();
        int targetPlayer = reader.ReadInt16();

        if (Main.netMode == NetmodeID.Server)
        {
            if (fromWho != 256 && !ErkySSCCompat.IsPlayerAdmin(Main.player[fromWho], out string reason))
            {
                string name = fromWho >= 0 && fromWho < Main.maxPlayers ? Main.player[fromWho]?.name : "unknown";
                Log.Warn($"Rejected Subworld admin packet '{type}' from {name} ({fromWho}).");
                Log.Warn($"Reason: {reason}");
                return;
            }

            if (SubworldSystem.IsActive<ArenasSubworld>())
                RelayToMainServer(type, targetPlayer);
            else
                ExecuteOnMainServer(type, targetPlayer);
            return;
        }
    }

    private static void ExecuteLocally(SubworldManagerPacketType type, int targetPlayer)
    {
        if (targetPlayer >= 0 && targetPlayer != Main.myPlayer)
            return;

        if (type is SubworldManagerPacketType.SendAllToMainWorld or SubworldManagerPacketType.SendPlayerToMainWorld)
        {
            ArenaSubworldCoordinator.MoveFromArenaToMain(targetPlayer);
            return;
        }

        ArenaSubworldCoordinator.MoveFromMainToExistingArena(targetPlayer);
    }

    private static void ExecuteOnMainServer(SubworldManagerPacketType type, int targetPlayer)
    {
        if (type is SubworldManagerPacketType.SendAllToMainWorld or SubworldManagerPacketType.SendPlayerToMainWorld)
            ArenaSubworldCoordinator.MoveFromArenaToMain(targetPlayer);
        else
            ArenaSubworldCoordinator.MoveFromMainToExistingArena(targetPlayer);
    }

    private static void RelayToMainServer(SubworldManagerPacketType type, int targetPlayer)
    {
        using MemoryStream stream = new();
        using (BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, true))
        {
            writer.Write((byte)Arenas.ArenasPacketType.SubworldManager);
            writer.Write((byte)type);
            writer.Write((short)targetPlayer);
        }
        Log.Debug($"[SubworldManager0] Relaying {type} to the main server. target={targetPlayer}.");
        SubworldSystem.SendToMainServer(ModContent.GetInstance<Arenas>(), stream.ToArray());
    }
}
