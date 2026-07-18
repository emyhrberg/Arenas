using Arenas.Core;
using Microsoft.Xna.Framework;
using System;

namespace Arenas.Common.AdminTools.SubworldManager;

[Autoload(Side = ModSide.Client)]
internal sealed class SubworldManagerErkySSCTool : ModSystem
{
    private const string Owner = "Arenas.SubworldManager";

    public override void PostSetupContent() => RegisterEntry();
    public override void OnWorldLoad() => RegisterEntry();

    private static void RegisterEntry()
    {
        if (!TryGetErkySSC(out Mod erkySSC)) return;

        try
        {
            erkySSC.Call(
                "RegisterAdminQuickbarEntry",
                Owner,
                "subworld_manager",
                "Arenas : Subworld Manager",
                "Move players between the main world and the Arenas subworld",
                Ass.IconArenas,
                new Action(() => ModContent.GetInstance<SubworldManagerUISystem>()?.Toggle()),
                new Func<string>(() => ModContent.GetInstance<SubworldManagerUISystem>()?.IsActive == true ? "Close" : "Open"),
                new Func<Color>(() => Color.White),
                true,
                30,
                "");
        }
        catch (Exception exception)
        {
            Log.Warn($"Failed to register the Subworld Manager quickbar entry: {exception}");
        }
    }

    public override void Unload()
    {
        if (!TryGetErkySSC(out Mod erkySSC)) return;

        try
        {
            erkySSC.Call("ClearAdminQuickbarEntries", Owner);
        }
        catch (Exception exception)
        {
            Log.Warn($"Failed to clear the Subworld Manager quickbar entry: {exception}");
        }
    }

    private static bool TryGetErkySSC(out Mod erkySSC) => ModLoader.TryGetMod("ErkySSC", out erkySSC)
        || ModLoader.TryGetMod("ErkySsc", out erkySSC);
}
