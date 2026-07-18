using System;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.ModLoader;

namespace Arenas.Core.Utilities;

public class EffectLoader : ModSystem
{
    private const string GrayscalePath = "Arenas/Assets/Effects/Grayscale";

    private static readonly Lazy<Effect> GrayscaleEffect = new(LoadGrayscaleEffect);

    public static bool TryGetGrayscaleEffect(out Effect effect)
    {
        try
        {
            effect = GrayscaleEffect.Value;
            return effect != null;
        }
        catch (Exception e)
        {
            Log.Warn($"Failed to load grayscale effect '{GrayscalePath}': {e.Message}");
            effect = null;
            return false;
        }
    }

    private static Effect LoadGrayscaleEffect()
    {
        try
        {
            return ModContent.Request<Effect>(GrayscalePath, AssetRequestMode.ImmediateLoad).Value;
        }
        catch (Exception e)
        {
            Log.Warn($"Failed to load grayscale effect '{GrayscalePath}': {e.Message}");
            return null;
        }
    }

    private const string LiquidGlassPath = "Arenas/Assets/Effects/LiquidGlass";
    private const string SpawnBoxBorderPath = "Arenas/Assets/Effects/SpawnBoxBorder";

    private static Effect liquidGlassEffect;
    private static Effect spawnBoxBorderEffect;
    private static bool spawnBoxBorderLoadAttempted;

    public static bool TryGetLiquidGlassEffect(out Effect effect)
    {
        try
        {
            liquidGlassEffect ??= ModContent.Request<Effect>(LiquidGlassPath, AssetRequestMode.ImmediateLoad).Value;
            effect = liquidGlassEffect;
            return effect != null;
        }
        catch (Exception e)
        {
            Log.Warn($"Failed to load liquid glass effect '{LiquidGlassPath}': {e.Message}");
            effect = null;
            return false;
        }
    }

    public static bool TryGetSpawnBoxBorderEffect(out Effect effect)
    {
        if (spawnBoxBorderEffect != null)
        {
            effect = spawnBoxBorderEffect;
            return true;
        }
        if (spawnBoxBorderLoadAttempted)
        {
            effect = null;
            return false;
        }

        spawnBoxBorderLoadAttempted = true;
        try
        {
            // This is first called by the interface draw layer, where FNA graphics access is main-thread safe.
            spawnBoxBorderEffect = ModContent.Request<Effect>(SpawnBoxBorderPath, AssetRequestMode.ImmediateLoad).Value;
            effect = spawnBoxBorderEffect;
            return effect != null;
        }
        catch (Exception e)
        {
            Log.Warn($"Failed to load spawnbox border effect '{SpawnBoxBorderPath}': {e.Message}");
            effect = null;
            return false;
        }
    }

    public override void Unload()
    {
        liquidGlassEffect = null;
        spawnBoxBorderEffect = null;
        spawnBoxBorderLoadAttempted = false;
    }
}
