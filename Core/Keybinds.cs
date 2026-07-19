using Terraria;
using Terraria.ModLoader;

namespace Arenas.Core;

internal sealed class Keybinds : ModSystem
{
    public ModKeybind SandboxMenu { get; private set; }

    public override void Load()
    {
        if (!Main.dedServ)
        {
            SandboxMenu = KeybindLoader.RegisterKeybind(Mod, "SandboxMenu", "P");
        }
    }

    public override void Unload() => SandboxMenu = null;
}
