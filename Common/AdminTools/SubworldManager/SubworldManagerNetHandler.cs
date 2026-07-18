using Arenas.Core.Compat;
using SubworldLibrary;
using System;
using System.IO;
using Terraria.ID;

namespace Arenas.Common.AdminTools.SubworldManager;

internal static class SubworldManagerNetHandler
{
    internal enum Action : byte
    {
        SendAllToMainWorld = 0,
        SendAllToArenas = 1,
        SendPlayerToMainWorld = 2,
        SendPlayerToArenas = 3
    }

    internal static void Request(Action action, int targetPlayer = -1)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
        {
            Execute(action, targetPlayer);
            return;
        }

        if (Main.netMode != NetmodeID.MultiplayerClient)
            return;

        var packet = ModContent.GetInstance<Arenas>().GetPacket();
        packet.Write((byte)Arenas.ArenasPacketType.SubworldManager);
        packet.Write((byte)action);
        packet.Write((short)targetPlayer);
        packet.Send();
    }

    public static void HandlePacket(BinaryReader reader, int fromWho)
    {
        Action action = (Action)reader.ReadByte();
        int targetPlayer = reader.ReadInt16();

        if (Main.netMode != NetmodeID.Server)
            return;
        if (fromWho != 256 && !ErkySSCCompat.IsAdmin(fromWho, out string reason))
        {
            Log.Warn($"Rejected Subworld Manager action {action} from player {fromWho}: {reason}");
            return;
        }

        if (SubworldSystem.IsActive<ArenasSubworld>())
            RelayToMainServer(action, targetPlayer);
        else
            Execute(action, targetPlayer);
    }

    private static void Execute(Action action, int targetPlayer)
    {
        if (Main.netMode == NetmodeID.SinglePlayer && targetPlayer >= 0 && targetPlayer != Main.myPlayer)
            return;

        if (action is Action.SendAllToMainWorld or Action.SendPlayerToMainWorld)
        {
            ArenaSubworldCoordinator.MoveFromArenaToMain(targetPlayer);
            return;
        }

        ArenaSubworldCoordinator.MoveFromMainToExistingArena(targetPlayer);
    }

    private static void RelayToMainServer(Action action, int targetPlayer)
    {
        using MemoryStream stream = new();
        using (BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, true))
        {
            writer.Write((byte)Arenas.ArenasPacketType.SubworldManager);
            writer.Write((byte)action);
            writer.Write((short)targetPlayer);
        }
        Log.Debug($"[SubworldManager0] Relaying {action} to the main server target={targetPlayer}");
        SubworldSystem.SendToMainServer(ModContent.GetInstance<Arenas>(), stream.ToArray());
    }
}
