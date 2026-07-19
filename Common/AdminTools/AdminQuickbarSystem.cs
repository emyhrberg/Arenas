using Arenas.Common.AdminTools.GameManager;
using Arenas.Common.AdminTools.SubworldManager;
using Arenas.Common.AdminTools.WorldGenManager;
using Arenas.Core;
using System;

namespace Arenas.Common.AdminTools;

[Autoload(Side = ModSide.Client)]
internal sealed class AdminQuickbarSystem : ModSystem
{
    private const string GameOwner = "Arenas.GameManager";
    private const string WorldOwner = "Arenas.SubworldManager";

    public override void PostSetupContent() => RegisterEntries();
    public override void OnWorldLoad() => RegisterEntries();

    public override void Unload()
    {
        if (!TryGetErkySSC(out Mod mod))
            return;
        Clear(mod, GameOwner);
        Clear(mod, WorldOwner);
    }

    private static void RegisterEntries()
    {
        if (!TryGetErkySSC(out Mod mod))
            return;

        // Arena Game Manager
        Add(mod, GameOwner, "arena_game_manager", "Arenas : Game Manager",
            "Balance teams and start arenas", 31,
            () => ModContent.GetInstance<ArenaGameManagerUISystem>().Toggle(),
            () => ModContent.GetInstance<ArenaGameManagerUISystem>().IsActive);

        // World Gen Manager
        Add(mod, GameOwner, "arena_world_gen_manager", "Arenas : World Gen Manager",
            "Generate and modify the world", 32,
            () => ModContent.GetInstance<WorldGenManagerUISystem>().Toggle(),
            () => ModContent.GetInstance<WorldGenManagerUISystem>().IsActive);

        // Subworld Manager
        //Add(mod, WorldOwner, "subworld_manager", "Arenas : Subworld Manager",
        //    "Move players between worlds", 30,
        //    () => ModContent.GetInstance<SubworldManagerUISystem>().Toggle(),
        //    () => ModContent.GetInstance<SubworldManagerUISystem>().IsActive);
    }

    private static void Add(Mod mod, string owner, string key, string title, string tooltip, int order, Action toggle, Func<bool> active)
    {
        try
        {
            mod.Call("RegisterAdminQuickbarEntry", owner, key, title, tooltip, Ass.IconArenas,
                toggle, new Func<string>(() => active() ? "Close" : "Open"), new Func<Color>(() => Color.White), true, order, "");
        }
        catch (Exception exception)
        {
            Log.Warn($"Failed to register {title}: {exception.Message}");
        }
    }

    private static void Clear(Mod mod, string owner)
    {
        try { mod.Call("ClearAdminQuickbarEntries", owner); }
        catch (Exception exception) { Log.Warn($"Failed to clear {owner}: {exception.Message}"); }
    }

    private static bool TryGetErkySSC(out Mod mod) =>
        ModLoader.TryGetMod("ErkySSC", out mod) || ModLoader.TryGetMod("ErkySsc", out mod);
}
