using Microsoft.Xna.Framework.Graphics;
using Steamworks;
using System;
using System.Collections.Generic;

namespace Arenas.Common.Rounds;

[Autoload(Side = ModSide.Client)]
internal sealed class SteamAvatarCache : ModSystem
{
    private const ulong AvatarRetryTicks = 120;
    private const ulong SteamIdRetryTicks = 300;

    private static readonly Dictionary<ulong, Texture2D> avatars = [];
    private static readonly Dictionary<ulong, ulong> nextAvatarAttempts = [];
    private static string localSteamId = "";
    private static ulong nextSteamIdAttempt;

    public override void OnWorldLoad() => Clear();
    public override void OnWorldUnload() => Clear();
    public override void Unload() => Clear();

    internal static string GetLocalSteamId()
    {
        if (Main.dedServ || !string.IsNullOrEmpty(localSteamId))
            return localSteamId;

        if (Main.GameUpdateCount < nextSteamIdAttempt)
            return "";

        nextSteamIdAttempt = Main.GameUpdateCount + SteamIdRetryTicks;

        try
        {
            ulong steamId = SteamUser.GetSteamID().m_SteamID;
            if (steamId > 0)
                localSteamId = steamId.ToString();
        }
        catch
        {
            // Steam is unavailable, so the scoreboard will retain its Terraria-head fallback.
        }

        return localSteamId;
    }

    internal static bool TryGetAvatar(int playerId, out Texture2D avatar)
    {
        avatar = null;
        string value = ArenaPlayerStatusSystem.GetStatus(playerId).SteamId;
        if (!ulong.TryParse(value, out ulong steamId) || steamId == 0)
            return false;

        if (avatars.TryGetValue(steamId, out avatar) && avatar is { IsDisposed: false })
            return true;

        if (nextAvatarAttempts.TryGetValue(steamId, out ulong nextAttempt) && Main.GameUpdateCount < nextAttempt)
            return false;

        nextAvatarAttempts[steamId] = Main.GameUpdateCount + AvatarRetryTicks;

        try
        {
            CSteamID id = new(steamId);
            SteamFriends.RequestUserInformation(id, false);
            int image = SteamFriends.GetMediumFriendAvatar(id);
            if (image <= 0 || !SteamUtils.GetImageSize(image, out uint width, out uint height) || width == 0 || height == 0 || width > 512 || height > 512)
                return false;

            byte[] rgba = new byte[checked((int)(width * height * 4))];
            if (!SteamUtils.GetImageRGBA(image, rgba, rgba.Length))
                return false;

            Color[] pixels = new Color[checked((int)(width * height))];
            for (int i = 0, source = 0; i < pixels.Length; i++, source += 4)
                pixels[i] = new Color(rgba[source], rgba[source + 1], rgba[source + 2], rgba[source + 3]);

            avatar = new Texture2D(Main.graphics.GraphicsDevice, (int)width, (int)height);
            avatar.SetData(pixels);
            avatars[steamId] = avatar;
            nextAvatarAttempts.Remove(steamId);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void Clear()
    {
        foreach (Texture2D avatar in avatars.Values)
            avatar?.Dispose();

        avatars.Clear();
        nextAvatarAttempts.Clear();
        localSteamId = "";
        nextSteamIdAttempt = 0;
    }
}
