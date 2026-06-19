using Arenas.Core;
using Microsoft.Xna.Framework;
using System;
using Terraria.ModLoader;

namespace Arenas.Common.AdminTools.ArenasManager;

[Autoload(Side = ModSide.Client)]
internal sealed class ArenasManagerErkySSCQuickbarSystem : ModSystem
{
    private const string Owner = "Arenas.ArenasManager";
    private const string EntryId = "arenas_manager";

    public override void PostSetupContent() => RegisterEntry();

    public override void OnWorldLoad() => RegisterEntry();

    private static void RegisterEntry()
    {
        if (!TryGetErkySSC(out Mod erkySSC))
            return;

        try
        {
            erkySSC.Call(
                "RegisterAdminQuickbarEntry",
                Owner,
                EntryId,
                "Arenas Manager",
                "Open the Arenas manager.",
                Ass.IconArenas,
                new Action(ToggleArenasManager),
                new Func<string>(ActionText),
                new Func<Color>(() => Color.White),
                true,
                30,
                ""
            );
        }
        catch (Exception e)
        {
            Log.Warn($"Failed to register ErkySSC admin quickbar entry: {e}");
        }
    }

    public override void Unload()
    {
        if (!TryGetErkySSC(out Mod erkySSC))
            return;

        try
        {
            erkySSC.Call("ClearAdminQuickbarEntries", Owner);
        }
        catch (Exception e)
        {
            Log.Warn($"Failed to clear ErkySSC admin quickbar entries: {e}");
        }
    }

    private static void ToggleArenasManager()
    {
        ModContent.GetInstance<ArenasManagerUISystem>()?.ToggleActive();
    }

    private static string ActionText()
    {
        ArenasManagerUISystem ui = ModContent.GetInstance<ArenasManagerUISystem>();
        return ui?.IsActive() == true ? "Close" : "Open";
    }

    private static bool TryGetErkySSC(out Mod erkySSC)
    {
        return ModLoader.TryGetMod("ErkySSC", out erkySSC)
            || ModLoader.TryGetMod("ErkySsc", out erkySSC);
    }
}
