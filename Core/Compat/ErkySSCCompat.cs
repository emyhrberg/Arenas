namespace Arenas.Core.Compat;

internal static class ErkySSCCompat
{
    internal static bool IsAdmin(int playerId, out string reason) =>
        playerId >= 0 && playerId < Main.maxPlayers
            ? IsPlayerAdmin(Main.player[playerId], out reason)
            : Fail("Invalid player", out reason);

    internal static bool IsPlayerAdmin(Player player, out string reason)
    {
        if (player == null || !player.active)
            return Fail("Invalid player", out reason);

        if (!ModLoader.TryGetMod("ErkySSC", out Mod erkySSCMod))
            return Fail("ErkySSC is not loaded", out reason);

        try
        {
            bool admin = erkySSCMod.Call("IsAdmin", player.whoAmI) is true;
            reason = admin ? "" : "ErkySSC admin required";
            return admin;
        }
        catch (System.Exception exception)
        {
            return Fail($"ErkySSC admin check failed: {exception.Message}", out reason);
        }
    }

    private static bool Fail(string message, out string reason) { reason = message; return false; }
}
