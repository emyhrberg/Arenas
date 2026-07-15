using Arenas.Core.Compat;
using System.IO;
using Terraria;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;

namespace Arenas.Common.Spawnbox;

public static class SpawnBoxNetHandler
{
    private enum PacketType : byte
    {
        Set,
        Sync
    }

    public static void HandlePacket(BinaryReader reader, int whoAmI)
    {
        PacketType type = (PacketType)reader.ReadByte();
        Team team = (Team)reader.ReadByte();
        SpawnBoxSettings settings = SpawnBoxSettings.Read(reader);
        if (team is not (Team.Red or Team.Green)) return;

        if (type == PacketType.Sync)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                ModContent.GetInstance<SpawnBoxSystem>().ReceiveSync(team, settings);

            return;
        }

        if (Main.netMode != NetmodeID.Server || whoAmI < 0 || whoAmI >= Main.maxPlayers || !ErkySSCCompat.IsPlayerAdmin(Main.player[whoAmI], out _))
            return;

        ModContent.GetInstance<SpawnBoxSystem>().SetFromTool(team, settings, sync: true);
    }

    public static void SendSet(Team team, SpawnBoxSettings settings)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
        {
            ModContent.GetInstance<SpawnBoxSystem>().SetFromTool(team, settings);
            return;
        }

        if (Main.netMode != NetmodeID.MultiplayerClient)
            return;

        ModContent.GetInstance<SpawnBoxSystem>().ReceiveSync(team, settings);
        Write(PacketType.Set, team, settings).Send();
    }

    internal static void SendSync(Team team, SpawnBoxSettings settings) => Write(PacketType.Sync, team, settings).Send();

    private static ModPacket Write(PacketType type, Team team, SpawnBoxSettings settings)
    {
        ModPacket packet = ModContent.GetInstance<global::Arenas.Arenas>().GetPacket();
        packet.Write((byte)global::Arenas.Arenas.ArenasPacketType.SpawnBox);
        packet.Write((byte)type);
        packet.Write((byte)team);
        settings.Clamped().Write(packet);
        return packet;
    }
}
