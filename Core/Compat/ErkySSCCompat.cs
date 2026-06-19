namespace Arenas.Core.Compat;

public static class ErkySSCCompat
{
    public static bool IsPlayerAdmin(Player player, out string reason)
    {
        reason = string.Empty;

        if (player == null || !player.active)
        {
            reason = "Player is invalid or inactive.";
            return false;
        }

        if (!ModLoader.TryGetMod("ErkySSC", out Mod erkySSCMod))
        {
            reason = "ErkySSC is not loaded.";
            return false;
        }

        object result = erkySSCMod.Call("IsAdmin", player.whoAmI);

        if (result is bool isAdmin)
        {
            if (!isAdmin)
                reason = "Player is not an admin in ErkySSC.";

            return isAdmin;
        }

        reason = "Unexpected response from ErkySSC mod call.";
        return false;
    }
}