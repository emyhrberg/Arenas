using Arenas.Core;
using System;

namespace Arenas.Common.AdminTools.SpawnBoxTool;

[Autoload(Side = ModSide.Client)]
internal sealed class SpawnBoxToolQuickbarSystem : ModSystem
{
    private const string Owner = "Arenas.SpawnBoxTool";

    public override void PostSetupContent() => RegisterEntry();
    public override void OnWorldLoad() => RegisterEntry();

    private static void RegisterEntry()
    {
        if (!TryGetErkySSC(out Mod mod)) return;
        try
        {
            mod.Call("RegisterAdminQuickbarEntry", Owner, "arenas_spawnbox", "Arenas Spawnbox Tool", "Change the Red and Green spawnboxes", Ass.IconArenas,
                new Action(() => ModContent.GetInstance<SpawnBoxToolUISystem>()?.Toggle()),
                new Func<string>(() => ModContent.GetInstance<SpawnBoxToolUISystem>()?.IsActive == true ? "Close" : "Open"),
                new Func<Color>(() => Color.White), true, 32, "");
        }
        catch (Exception e) { Log.Warn($"Failed to register Arenas Spawnbox Tool quickbar entry: {e}"); }
    }

    public override void Unload()
    {
        if (!TryGetErkySSC(out Mod mod)) return;
        try { mod.Call("ClearAdminQuickbarEntries", Owner); }
        catch (Exception e) { Log.Warn($"Failed to clear Arenas Spawnbox Tool quickbar entry: {e}"); }
    }

    private static bool TryGetErkySSC(out Mod mod) => ModLoader.TryGetMod("ErkySSC", out mod) || ModLoader.TryGetMod("ErkySsc", out mod);
}
