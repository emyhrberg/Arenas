using PvPFramework.Common.Spawnbox;
using System;

namespace Arenas.Common;

/// <summary>
/// Disables PvPFramework's single main-world spawn box in Arenas. The red and blue
/// spawn rooms in ArenaLayout are the only spawn boxes shown in the arena.
/// </summary>
internal sealed class ArenaSpawnBoxIntegration : ModSystem
{
    private static Func<bool> previousProvider;
    private static Func<bool> arenasProvider;

    public override void Load()
    {
        Func<bool> inheritedProvider = SpawnBoxSystem.EnabledProvider ?? EnabledByDefault;
        previousProvider = inheritedProvider;
        // Layout is included because the SWL active marker can lag behind world-state
        // synchronization briefly on a multiplayer client. Never draw a nested global
        // spawn box once an authoritative Arenas layout has been received.
        arenasProvider = () => inheritedProvider()
            && !ArenaWorldSystem.Active
            && ArenaWorldSystem.Layout == null;
        SpawnBoxSystem.EnabledProvider = arenasProvider;
    }

    public override void Unload()
    {
        if (ReferenceEquals(SpawnBoxSystem.EnabledProvider, arenasProvider))
            SpawnBoxSystem.EnabledProvider = previousProvider ?? EnabledByDefault;

        previousProvider = null;
        arenasProvider = null;
    }

    private static bool EnabledByDefault() => true;
}
