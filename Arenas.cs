using System;
using System.IO;

namespace Arenas;

public class Arenas : Mod
{
public enum ArenasPacketType
{
    Admin = 0
}

    public override void HandlePacket(BinaryReader reader, int whoAmI)
    {
        var type = (ArenasPacketType)reader.ReadByte();

        switch (type)
        {
            case ArenasPacketType.Admin:
                Common.AdminTools.ArenasAdminNetHandler.HandlePacket(reader, whoAmI);
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
