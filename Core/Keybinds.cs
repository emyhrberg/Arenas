using Terraria;
using Terraria.ModLoader;

namespace Arenas.Core;

internal sealed class Keybinds : ModSystem
{
    public ModKeybind Scoreboard { get; private set; }
    public ModKeybind SandboxMenu { get; private set; }

    public override void Load()
    {
        if (!Main.dedServ)
        {
            Scoreboard = KeybindLoader.RegisterKeybind(Mod, "Scoreboard", "Y");
            SandboxMenu = KeybindLoader.RegisterKeybind(Mod, "SandboxMenu", "P");
        }
    }

    public override void Unload() => Scoreboard = SandboxMenu = null;
}
