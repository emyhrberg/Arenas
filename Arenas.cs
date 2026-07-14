using System;
using System.IO;

namespace Arenas;

public class Arenas : Mod
{
    public enum ArenasPacketType
    {
        ArenasManager,
        ArenaRound,
        TeamBoss
    }

    public override void HandlePacket(BinaryReader reader, int whoAmI)
    {
        var type = (ArenasPacketType)reader.ReadByte();

        switch (type)
        {
            case ArenasPacketType.ArenasManager:
                Common.AdminTools.ArenasManager.ArenasManagerNetHandler.HandlePacket(reader, whoAmI);
                break;
            case ArenasPacketType.ArenaRound:
                Common.Rounds.ArenaRoundNetHandler.HandlePacket(reader, whoAmI);
                break;
            case ArenasPacketType.TeamBoss:
                Common.TeamBoss.TeamBossNetHandler.HandlePacket(reader, whoAmI);
                break;
        }
    }

    public override object Call(params object[] args)
    {
        if (args.Length > 0
            && args[0] is string command
            && string.Equals(command, "OwnsRespawnTimer", StringComparison.Ordinal))
        {
            return Common.RespawnTimer.RespawnTimerPlayer.OwnsRespawnTimer;
        }

        return base.Call(args);
    }
}
