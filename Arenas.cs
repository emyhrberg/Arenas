using Arenas.Common.AdminTools.Tools.ArenasTool;
using System.IO;
using Terraria.ModLoader;

namespace Arenas
{
	// Please read https://github.com/tModLoader/tModLoader/wiki/Basic-tModLoader-Modding-Guide#mod-skeleton-contents for more information about the various files in a mod.
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
                    ArenasAdminNetHandler.HandlePacket(reader, whoAmI);
                    break;
            }
        }
	}
}
