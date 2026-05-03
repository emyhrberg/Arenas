using Terraria;
using Terraria.ModLoader;

namespace Arenas.Core;

internal sealed class Keybinds : ModSystem
{
    public ModKeybind ArenasMenu { get; private set; }

    public override void Load()
    {
        if (!Main.dedServ)
            ArenasMenu = KeybindLoader.RegisterKeybind(Mod, "ArenasMenu", "P");
    }

    public override void Unload()
    {
        ArenasMenu = null;
    }
}
