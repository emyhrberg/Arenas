using SubworldLibrary;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Arenas.Common.AdminTools.SubworldManager;

internal sealed class SubworldAdminBridgeSystem : ModSystem, ICopyWorldData
{
    private const string AdminsKey = "!Arenas.ErkySSCAdmins";

    internal static bool IsAdmin(Player player)
    {
        if (player?.active != true || !TryGetErkySsc(out Mod erkySsc))
            return false;

        try { return erkySsc.Call("IsAdmin", player.whoAmI) is true; }
        catch { return false; }
    }

    // Runs on the main server before the subserver process spawns.
    public void CopyMainWorldData()
    {
        if (Main.netMode != NetmodeID.Server) return;
        if (!TryGetErkySsc(out Mod erkySsc)) return;

        try
        {
            if (erkySsc.Call("GetAdminNames") is string[] names)
                SubworldSystem.CopyWorldData(AdminsKey, names);
        }
        catch (System.Exception exception)
        {
            Log.Warn($"Failed to copy ErkySSC admins into the Arenas subworld: {exception.Message}");
        }
    }

    // Runs inside the subserver process before the subworld generates/loads.
    public void ReadCopiedMainWorldData()
    {
        if (Main.netMode != NetmodeID.Server) return;
        if (!SubworldSystem.AnyActive()) return;
        if (!TryGetErkySsc(out Mod erkySsc)) return;

        string[] names = SubworldSystem.ReadCopiedWorldData<string[]>(AdminsKey) ?? [];
        try { erkySsc.Call("ReplaceAdminNames", names); }
        catch (System.Exception exception)
        {
            Log.Warn($"Failed to restore ErkySSC admins inside the Arenas subworld: {exception.Message}");
        }
    }

    private static bool TryGetErkySsc(out Mod mod) =>
        ModLoader.TryGetMod("ErkySSC", out mod) || ModLoader.TryGetMod("ErkySsc", out mod);
}
