using System.IO;
using Arenas.Common.Rounds;
using Terraria;
using Terraria.ModLoader.IO;

namespace Arenas;

public sealed class Arenas : Mod
{
    private const string ImportSscStatsCall = "ErkySSC.ImportStats";
    private const string ExportSscStatsCall = "ErkySSC.ExportStats";
    private const string SscStorageScopeCall = "ErkySSC.StorageScope";

    internal enum ArenasPacketType : byte
    {
        ArenaRound,
        TeamBoss,
        ArenaGameManager,
        ArenaSubworld,
        //SubworldManager,
        Sandbox,
        MapReveal,
        WorldGenManager
    }

    public override void HandlePacket(BinaryReader reader, int whoAmI)
    {
        var type = (ArenasPacketType)reader.ReadByte();

        switch (type)
        {
            case ArenasPacketType.ArenaRound:
                Common.Rounds.ArenaRoundNetHandler.HandlePacket(reader, whoAmI);
                break;
            case ArenasPacketType.ArenaGameManager:
                Common.AdminTools.GameManager.ArenaGameManagerNetHandler.HandlePacket(reader, whoAmI);
                break;
            case ArenasPacketType.ArenaSubworld:
                Common.ArenaSubworldCoordinator.HandlePacket(reader, whoAmI);
                break;
            //case ArenasPacketType.SubworldManager:
            //    Common.AdminTools.SubworldManager.SubworldManagerNetHandler.HandlePacket(reader, whoAmI);
            //    break;
            case ArenasPacketType.Sandbox:
                Common.Sandbox.SandboxNetHandler.HandlePacket(reader, whoAmI);
                break;
            case ArenasPacketType.MapReveal:
                Common.Generation.ArenaMapRevealNetHandler.HandlePacket(reader, whoAmI);
                break;
            case ArenasPacketType.WorldGenManager:
                Common.AdminTools.WorldGenManager.WorldGenManagerNetHandler.HandlePacket(reader, whoAmI);
                break;
        }
    }

    public override object Call(params object[] args)
    {
        if (args.Length >= 1 && args[0] is string simpleOperation && simpleOperation == SscStorageScopeCall)
            return Common.ArenaSubworldCoordinator.SscWorldScope;

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
