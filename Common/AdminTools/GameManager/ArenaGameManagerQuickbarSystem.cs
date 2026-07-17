using Arenas.Core;
using Microsoft.Xna.Framework;
using System;

namespace Arenas.Common.AdminTools.GameManager;

[Autoload(Side = ModSide.Client)]
internal sealed class ArenaGameManagerQuickbarSystem : ModSystem
{
    private const string Owner = "Arenas.GameManager";

    public override void PostSetupContent() => RegisterEntry();
    public override void OnWorldLoad() => RegisterEntry();

    private static void RegisterEntry()
    {
        if (!TryGetErkySSC(out Mod mod)) return;
        try
        {
            mod.Call("RegisterAdminQuickbarEntry", Owner, "arena_game_manager", "Arenas Game Manager", "Clear the world, balance teams, and manage rounds", Ass.IconStartGame,
                new Action(() => ModContent.GetInstance<ArenaGameManagerUISystem>()?.Toggle()),
                new Func<string>(() => ModContent.GetInstance<ArenaGameManagerUISystem>()?.IsActive == true ? "Close" : "Open"),
                new Func<Color>(() => Color.White), true, 31, "");
        }
        catch (Exception e) { Log.Warn($"Failed to register Arenas Game Manager quickbar entry: {e}"); }
    }

    public override void Unload()
    {
        if (!TryGetErkySSC(out Mod mod)) return;
        try { mod.Call("ClearAdminQuickbarEntries", Owner); }
        catch (Exception e) { Log.Warn($"Failed to clear Arenas Game Manager quickbar entry: {e}"); }
    }

    private static bool TryGetErkySSC(out Mod mod) => ModLoader.TryGetMod("ErkySSC", out mod) || ModLoader.TryGetMod("ErkySsc", out mod);
}
