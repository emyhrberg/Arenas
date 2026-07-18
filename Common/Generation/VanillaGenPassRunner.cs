using System;
using System.Diagnostics;
using Terraria.Utilities;
using Terraria.WorldBuilding;

namespace Arenas.Common.Generation;

/// <summary>Runs the GenPass instances registered by the installed Terraria build with vanilla configuration and RNG reset semantics.</summary>
internal static class VanillaGenPassRunner
{
    private const string ConfigurationPath = "Terraria.GameContent.WorldBuilding.Configuration.json";
    private static readonly Lazy<WorldGenConfiguration> VanillaConfiguration = new(() => WorldGenConfiguration.FromEmbeddedPath(ConfigurationPath));

    public static WorldGenConfiguration Configuration => VanillaConfiguration.Value;

    public static void Run(string scope, string name, int seed)
    {
        try
        {
            EnsurePasses();
            if (!WorldGen.VanillaGenPasses.TryGetValue(name, out GenPass pass))
                throw new InvalidOperationException($"The installed Terraria build did not register GenPass '{name}'");

            WorldGen._genRand = new UnifiedRandom(seed);
            Main.rand = new UnifiedRandom(seed);
            pass.Reset();
            Stopwatch timer = Stopwatch.StartNew();
            Log.Debug($"[{scope}] START vanilla GenPass '{name}' seed={seed} size={Main.maxTilesX}x{Main.maxTilesY}");
            pass.Apply(new GenerationProgress(), Configuration.GetPassConfiguration(name));
            timer.Stop();
            Log.Debug($"[{scope}] END vanilla GenPass '{name}' elapsed={timer.Elapsed.TotalMilliseconds:F1}ms surface={Main.worldSurface:F1} rock={Main.rockLayer:F1} water={GenVars.waterLine} lava={GenVars.lavaLine}");
        }
        catch (Exception exception)
        {
            string state = $"scope={scope}, pass={name}, seed={seed}, size={Main.maxTilesX}x{Main.maxTilesY}, surface={Main.worldSurface:F1}, rock={Main.rockLayer:F1}, water={GenVars.waterLine}, lava={GenVars.lavaLine}";
            Log.Debug($"[WorldGenFailure] {state}: {exception.GetType().Name}: {exception.Message}");
            throw new InvalidOperationException($"Vanilla world generation failed ({state}). Inspect the inner exception and the preceding WorldGen chat/log entries", exception);
        }
    }

    private static void EnsurePasses()
    {
        if (WorldGen.VanillaGenPasses.Count == 0)
            WorldGen.AddGenPasses();
        if (WorldGen.VanillaGenPasses.Count == 0)
            throw new InvalidOperationException("Terraria registered zero vanilla world-generation passes");
    }
}
