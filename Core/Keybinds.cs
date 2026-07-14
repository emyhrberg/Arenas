using Terraria;
using Terraria.ModLoader;

namespace Arenas.Core;

internal sealed class Keybinds : ModSystem
{
    public ModKeybind ArenasMenu { get; private set; }
    public ModKeybind Scoreboard { get; private set; }

    public override void Load()
    {
        if (!Main.dedServ) {
            ArenasMenu = KeybindLoader.RegisterKeybind(Mod, "ArenasMenu", "P");
            Scoreboard = KeybindLoader.RegisterKeybind(Mod, "Scoreboard", "Tab");
        }
    }

    public override void Unload() => ArenasMenu = Scoreboard = null;
}
