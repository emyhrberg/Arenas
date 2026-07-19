using System;
using System.Collections.Generic;
using System.Linq;

namespace Arenas.Common.Generation;

/// <summary>
/// Fast opt-in contract audit. Complete pass testing is intentionally performed through
/// World Gen Manager, which exercises Terraria's real lifecycle in the isolated Arenas child.
/// Set ARENAS_WORLDGEN_SELFTEST=1 to validate the shared catalog/layout at mod load.
/// </summary>
internal sealed class ArenaGenerationSelfTestSystem : ModSystem
{
    public override void PostSetupContent()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("ARENAS_WORLDGEN_SELFTEST"), "1", StringComparison.Ordinal))
            return;

        Log.Info("[WorldGenSelfTest] START reusable-world contract audit.");
        string[] steps = ArenaWorldGenerationCatalog.Steps;
        if (steps.Length == 0 || steps[0] != "Reset" || steps[^1] != ArenaWorldGenerationCatalog.ValidateCombatRegion)
            throw new InvalidOperationException("The controlled generation catalog has invalid endpoints.");

        List<string> duplicates = steps.GroupBy(name => name).Where(group => group.Count() > 1).Select(group => group.Key).ToList();
        if (duplicates.Count > 0)
            throw new InvalidOperationException($"The controlled generation catalog contains duplicate names: {string.Join(", ", duplicates)}");

        ArenaLayout layout = ArenaWorldGenerationSystem.CreateFixedLayout(12345);
        layout.Validate(ArenasSubworld.FixedWidth, ArenasSubworld.FixedHeight);
        Log.Info($"[WorldGenSelfTest] PASS steps={steps.Length}, world={layout.WorldWidth}x{layout.WorldHeight}, arena={layout.ArenaArea}.");
    }
}
