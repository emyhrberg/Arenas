using System.IO;
using Arenas.Common.Rounds;
using Terraria;
using Terraria.ModLoader.IO;

namespace Arenas;

public class Arenas : Mod
{
    private const string ImportSscStatsCall = "ErkySSC.ImportStats";
    private const string ExportSscStatsCall = "ErkySSC.ExportStats";

    public enum ArenasPacketType
    {
        ArenaRound,
        TeamBoss,
        ArenaGameManager,
        EndScreen,
        SpawnBox,
        PlayerStatus
    }

    public override void HandlePacket(BinaryReader reader, int whoAmI)
    {
        var type = (ArenasPacketType)reader.ReadByte();

        switch (type)
        {
            case ArenasPacketType.ArenaRound:
                Common.Rounds.ArenaRoundNetHandler.HandlePacket(reader, whoAmI);
                break;
            case ArenasPacketType.TeamBoss:
                Common.TeamBoss.TeamBossNetHandler.HandlePacket(reader, whoAmI);
                break;
            case ArenasPacketType.ArenaGameManager:
                Common.AdminTools.GameManager.ArenaGameManagerNetHandler.HandlePacket(reader, whoAmI);
                break;
            case ArenasPacketType.EndScreen:
                Common.EndScreen.EndScreenNetHandler.HandlePacket(reader, whoAmI);
                break;
            case ArenasPacketType.SpawnBox:
                Common.Spawnbox.SpawnBoxNetHandler.HandlePacket(reader, whoAmI);
                break;
            case ArenasPacketType.PlayerStatus:
                ArenaPlayerStatusNetHandler.HandlePacket(reader, whoAmI);
                break;
        }
    }

    public override object Call(params object[] args)
    {
        if (args.Length < 4 || args[0] is not string operation ||
            args[1] is not int whoAmI || args[2] is not string characterKey ||
            args[3] is not TagCompound root || whoAmI is < 0 or >= Main.maxPlayers)
            return false;

        Player player = Main.player[whoAmI];
        if (player == null)
            return false;

        return operation switch
        {
            ImportSscStatsCall => ArenaRoundPlayer.ImportSscStats(player, characterKey, root),
            ExportSscStatsCall => ArenaRoundPlayer.ExportSscStats(player, characterKey, root),
            _ => false
        };
    }
}
