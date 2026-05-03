using DragonLens.Core.Systems;
using SubworldLibrary;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Arenas.Core.AdminTools.Tools.ArenasTool;

internal static class ArenasAdminNetHandler
{
    public enum ArenasAdminPacketType : byte
    {
        SendAllToMainWorld = 0,
        SendAllToArenas = 1,
        SendPlayerToMainWorld = 2,
        SendPlayerToArenas = 3
    }

    public static void Request(ArenasAdminPacketType type, int targetPlayer = -1)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
        {
            Execute(type, targetPlayer);
            return;
        }

        if (Main.netMode != NetmodeID.MultiplayerClient)
            return;

        var packet = ModContent.GetInstance<global::Arenas.Arenas>().GetPacket();
        packet.Write((byte)global::Arenas.Arenas.ArenasPacketType.Admin);
        packet.Write((byte)type);
        packet.Write((short)targetPlayer);
        packet.Send();
    }

    public static void HandlePacket(BinaryReader reader, int fromWho)
    {
        var type = (ArenasAdminPacketType)reader.ReadByte();
        int targetPlayer = reader.ReadInt16();

        if (Main.netMode == NetmodeID.Server)
        {
            if (!IsAuthorizedAdmin(fromWho))
            {
                string name = fromWho >= 0 && fromWho < Main.maxPlayers ? Main.player[fromWho]?.name : "unknown";
                DebugLog.Warn($"Rejected Arenas admin packet '{type}' from {name} ({fromWho}).");
                return;
            }

            var packet = ModContent.GetInstance<global::Arenas.Arenas>().GetPacket();
            packet.Write((byte)global::Arenas.Arenas.ArenasPacketType.Admin);
            packet.Write((byte)type);
            packet.Write((short)targetPlayer);
            packet.Send(toClient: -1, ignoreClient: -1);
            return;
        }

        if (Main.netMode == NetmodeID.MultiplayerClient)
            Execute(type, targetPlayer);
    }

    private static bool IsAuthorizedAdmin(int fromWho)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
            return true;

        if (fromWho < 0 || fromWho >= Main.maxPlayers)
            return false;

        Player player = Main.player[fromWho];
        if (player == null || !player.active)
            return false;

        return ModLoader.HasMod("DragonLens") && IsDragonLensAdmin(player);
    }

    [JITWhenModsEnabled("DragonLens")]
    private static bool IsDragonLensAdmin(Player player)
    {
        return PermissionHandler.CanUseTools(player);
    }

    private static void Execute(ArenasAdminPacketType type, int targetPlayer)
    {
        if (targetPlayer >= 0 && targetPlayer != Main.myPlayer)
            return;

        Main.QueueMainThreadAction(() =>
        {
            if (type == ArenasAdminPacketType.SendAllToArenas || type == ArenasAdminPacketType.SendPlayerToArenas)
            {
                if (!SubworldSystem.AnyActive())
                    SubworldSystem.Enter<ArenasSubworld>();
                return;
            }

            if (type == ArenasAdminPacketType.SendAllToMainWorld || type == ArenasAdminPacketType.SendPlayerToMainWorld)
            {
                if (SubworldSystem.AnyActive())
                    SubworldSystem.Exit();
            }
        });
    }
}
