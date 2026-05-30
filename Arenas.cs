namespace Arenas;

public class Arenas : Mod
{
public enum ArenasPacketType
{
    //Admin = 0
}

//public override void HandlePacket(BinaryReader reader, int whoAmI)
//{
//    var type = (ArenasPacketType)reader.ReadByte();

//    switch (type)
//    {
//        case ArenasPacketType.Admin:
//            ArenasAdminNetHandler.HandlePacket(reader, whoAmI);
//            break;
//    }
//}
}
