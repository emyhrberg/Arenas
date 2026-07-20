using System.IO;
using Terraria.ModLoader.IO;

namespace Arenas;

public sealed class Arenas : Mod
{
    internal enum ArenasPacketType : byte
    {
    }

    public override void HandlePacket(BinaryReader reader, int whoAmI)
    {
        var type = (ArenasPacketType)reader.ReadByte();

        switch (type)
        {
            //case ArenasPacketType.WorldGenManager:
                //Common.AdminTools.WorldGenManager.WorldGenManagerNetHandler.HandlePacket(reader, whoAmI);
                //break;
        }
    }
}
