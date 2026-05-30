using Arenas.Common.UI;
using Terraria.ModLoader;

namespace Arenas.Common.Commands;

internal sealed class ArenasCommand : ModCommand
{
    public override string Command => "arenas";
    public override CommandType Type => CommandType.Chat;
    public override string Description => "Toggles the arenas UI.";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        ArenasUISystem.Toggle();
    }
}