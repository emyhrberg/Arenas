using SubworldLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Arenas.Common.AdminTools;

internal sealed class ArenasErkySSCAdminBridgeSystem : ModSystem, ICopyWorldData
{
    private const string AdminsKey = "!Arenas.ErkySSCAdmins";

    private static bool _triedInit;
    private static MethodInfo _getAdminList;
    private static MethodInfo _isAdmin;
    private static MethodInfo _getInstance;
    private static FieldInfo _adminNamesField;
    private static MethodInfo _refreshRuntimeStates;

    private static bool TryInit()
    {
        if (_triedInit)
            return _getAdminList != null;

        _triedInit = true;

        if (!ModLoader.TryGetMod("ErkySSC", out Mod mod))
            return false;

        Type sys = mod.Code.GetType("ErkySSC.Common.AdminPermissions.AdminPermissionSystem");
        if (sys == null)
            return false;

        _getAdminList = sys.GetMethod("GetAdminList", BindingFlags.Public | BindingFlags.Static);
        _isAdmin = sys.GetMethod("IsAdmin", BindingFlags.Public | BindingFlags.Static);
        _adminNamesField = sys.GetField("adminNames", BindingFlags.NonPublic | BindingFlags.Instance);
        _refreshRuntimeStates = sys.GetMethod("RefreshRuntimeAdminStates", BindingFlags.Public | BindingFlags.Static);
        _getInstance = typeof(ModContent)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "GetInstance" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0)
            ?.MakeGenericMethod(sys);

        return _getAdminList != null && _isAdmin != null && _adminNamesField != null
            && _refreshRuntimeStates != null && _getInstance != null;
    }

    internal static bool IsAdmin(Player player)
    {
        if (!TryInit()) return false;
        return _isAdmin.Invoke(null, [player]) is true;
    }

    // Runs on the main server before the subserver process spawns.
    public void CopyMainWorldData()
    {
        if (Main.netMode != NetmodeID.Server) return;
        if (!TryInit()) return;

        var list = (List<string>)_getAdminList.Invoke(null, null);
        SubworldSystem.CopyWorldData(AdminsKey, list.ToArray());
    }

    // Runs inside the subserver process before the subworld generates/loads.
    public void ReadCopiedMainWorldData()
    {
        if (Main.netMode != NetmodeID.Server) return;
        if (!SubworldSystem.AnyActive()) return;
        if (!TryInit()) return;

        string[] names = SubworldSystem.ReadCopiedWorldData<string[]>(AdminsKey) ?? [];

        object instance = _getInstance.Invoke(null, null);
        if (instance == null) return;

        var adminNames = (HashSet<string>)_adminNamesField.GetValue(instance);
        adminNames.Clear();
        foreach (string name in names)
            adminNames.Add(name);

        _refreshRuntimeStates.Invoke(null, [-1]);
    }
}
