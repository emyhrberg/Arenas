using PvPFramework.Common.Spawnbox;
using System;

namespace PvPArenas.Common.Game;

/// <summary>Hides PvPFramework's center-spawn box while Arenas owns the two team spawn boxes.</summary>
internal sealed class ArenaSpawnBoxIntegration : ModSystem
{
    private static Func<bool> previousProvider;
    private static Func<bool> arenasProvider;

    public override void Load()
    {
        Func<bool> inheritedProvider = SpawnBoxSystem.EnabledProvider ?? EnabledByDefault;
        previousProvider = inheritedProvider;
        arenasProvider = () => inheritedProvider()
            && ModContent.GetInstance<RoundManager>().CurrentPhase
                is not (RoundManager.RoundPhase.FreezeCountdown or RoundManager.RoundPhase.Playing);
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
