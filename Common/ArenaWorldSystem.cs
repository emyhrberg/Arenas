namespace Arenas.Common;

/// <summary>Marks the loaded main world as the active Arenas game world.</summary>
internal sealed class ArenaWorldSystem : ModSystem
{
    public static bool Active => !Main.gameMenu && Main.ActiveWorldFileData != null;
}
