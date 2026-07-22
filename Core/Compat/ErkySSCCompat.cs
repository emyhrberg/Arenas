using Microsoft.Xna.Framework;

namespace PvPArenas.Core.Compat;

internal static class ErkySSCCompat
{
    private const string BeginHeadBypassCall = "BeginMapPlayerHeadVisibilityBypass";
    private const string EndHeadBypassCall = "EndMapPlayerHeadVisibilityBypass";

    internal static void DrawUnfilteredPlayerHead(Player player, Vector2 position, float alpha, float scale, Color borderColor)
    {
        if (player?.active != true)
            return;

        if (!TryGetMod(out Mod erkySSC))
        {
            Main.MapPlayerRenderer.DrawPlayerHead(Main.Camera, player, position, alpha, scale, borderColor);
            return;
        }

        bool bypassed = false;
        try
        {
            bypassed = erkySSC.Call(BeginHeadBypassCall) is true;
            if (bypassed)
                Main.MapPlayerRenderer.DrawPlayerHead(Main.Camera, player, position, alpha, scale, borderColor);
            else
                Main.PlayerRenderer.DrawPlayerHead(Main.Camera, player, position, alpha, scale, borderColor);
        }
        catch
        {
            Main.PlayerRenderer.DrawPlayerHead(Main.Camera, player, position, alpha, scale, borderColor);
        }
        finally
        {
            if (bypassed)
                try { erkySSC.Call(EndHeadBypassCall); } catch { }
        }
    }

    private static bool TryGetMod(out Mod mod) =>
        ModLoader.TryGetMod("ErkySSC", out mod) || ModLoader.TryGetMod("ErkySsc", out mod);
    internal static bool IsAdmin(int playerId, out string reason) =>
        playerId >= 0 && playerId < Main.maxPlayers
            ? IsPlayerAdmin(Main.player[playerId], out reason)
            : Fail("Invalid player", out reason);

    internal static bool IsPlayerAdmin(Player player, out string reason)
    {
        if (player == null || !player.active)
            return Fail("Invalid player", out reason);

        if (!TryGetMod(out Mod erkySSCMod))
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
