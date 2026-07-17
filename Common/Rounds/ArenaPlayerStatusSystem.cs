using System;
using System.Collections.Generic;
using System.Diagnostics;
using Terraria.ID;

namespace Arenas.Common.Rounds;

internal readonly record struct ArenaPlayerStatus(int PingMs, string SteamId, bool Dead, int RespawnTimer);

internal sealed class ArenaPlayerStatusSystem : ModSystem
{
    private const int PingInterval = 60;
    private const int PendingPingLifetimeSeconds = 10;

    private static readonly Dictionary<int, ArenaPlayerStatus> statuses = [];
    private static readonly Dictionary<long, long> pendingPings = [];
    private static long nextPingId;
    private static int pingTimer;

    internal static IReadOnlyDictionary<int, ArenaPlayerStatus> Statuses => statuses;

    public override void OnWorldLoad() => Reset();
    public override void OnWorldUnload() => Reset();

    public override void PostUpdatePlayers()
    {
        if (Main.netMode == NetmodeID.Server)
        {
            if (Main.GameUpdateCount % 60 == 0)
                RemoveInactivePlayers();

            return;
        }

        UpdateLocalStatus();
        TickRemoteRespawnTimers();

        if (Main.netMode != NetmodeID.MultiplayerClient || Main.myPlayer < 0 || Main.myPlayer >= Main.maxPlayers || !Main.LocalPlayer.active || ++pingTimer < PingInterval)
            return;

        pingTimer = 0;
        RemoveExpiredPings();

        long pingId = ++nextPingId;
        pendingPings[pingId] = Stopwatch.GetTimestamp();
        ArenaPlayerStatusNetHandler.SendPingRequest(pingId);
    }

    internal static void ReceivePingResponse(long pingId)
    {
        if (!pendingPings.Remove(pingId, out long sentTimestamp))
            return;

        int pingMs = Math.Clamp(
            (int)Math.Round((Stopwatch.GetTimestamp() - sentTimestamp) * 1000d / Stopwatch.Frequency),
            0,
            60_000);

        Player player = Main.LocalPlayer;
        ArenaPlayerStatus status = new(pingMs, SteamAvatarCache.GetLocalSteamId(), player.dead, Math.Max(0, player.respawnTimer));
        statuses[Main.myPlayer] = status;
        ArenaPlayerStatusNetHandler.SendStatus(status);
    }

    internal static ArenaPlayerStatus GetStatus(int playerId)
    {
        bool hasStatus = statuses.TryGetValue(playerId, out ArenaPlayerStatus status);

        if (Main.netMode != NetmodeID.Server && playerId == Main.myPlayer && playerId >= 0 && playerId < Main.maxPlayers)
        {
            Player player = Main.player[playerId];
            int ping = Main.netMode == NetmodeID.SinglePlayer ? 0 : hasStatus ? status.PingMs : -1;
            string steamId = string.IsNullOrEmpty(status.SteamId) ? SteamAvatarCache.GetLocalSteamId() : status.SteamId;
            return new(ping, steamId, player.dead, Math.Max(0, player.respawnTimer));
        }

        if (hasStatus)
            return status;

        if (playerId >= 0 && playerId < Main.maxPlayers)
        {
            Player player = Main.player[playerId];
            return new(-1, "", player.dead, Math.Max(0, player.respawnTimer));
        }

        return new(-1, "", false, 0);
    }

    internal static void SetStatus(int playerId, ArenaPlayerStatus status)
    {
        if (playerId < 0 || playerId >= Main.maxPlayers)
            return;

        statuses[playerId] = Normalize(status);
    }

    internal static void RemoveStatus(int playerId) => statuses.Remove(playerId);

    internal static ArenaPlayerStatus Normalize(ArenaPlayerStatus status)
    {
        string steamId = status.SteamId?.Trim() ?? "";
        if (steamId.Length > 32 || steamId.Length > 0 && !ulong.TryParse(steamId, out _))
            steamId = "";

        return new(
            Math.Clamp(status.PingMs, -1, 60_000),
            steamId,
            status.Dead,
            Math.Clamp(status.RespawnTimer, 0, 60 * 60 * 60));
    }

    private static void UpdateLocalStatus()
    {
        if (Main.myPlayer < 0 || Main.myPlayer >= Main.maxPlayers)
            return;

        Player player = Main.LocalPlayer;
        bool hasStatus = statuses.TryGetValue(Main.myPlayer, out ArenaPlayerStatus current);
        statuses[Main.myPlayer] = new(
            Main.netMode == NetmodeID.SinglePlayer ? 0 : hasStatus ? current.PingMs : -1,
            string.IsNullOrEmpty(current.SteamId) ? SteamAvatarCache.GetLocalSteamId() : current.SteamId,
            player.dead,
            Math.Max(0, player.respawnTimer));
    }

    private static void TickRemoteRespawnTimers()
    {
        foreach (int playerId in new List<int>(statuses.Keys))
        {
            if (playerId == Main.myPlayer)
                continue;

            ArenaPlayerStatus status = statuses[playerId];
            if (status.Dead && status.RespawnTimer > 0)
                statuses[playerId] = status with { RespawnTimer = status.RespawnTimer - 1 };
        }
    }

    private static void RemoveInactivePlayers()
    {
        foreach (int playerId in new List<int>(statuses.Keys))
        {
            if (playerId >= 0 && playerId < Main.maxPlayers && Main.player[playerId].active)
                continue;

            statuses.Remove(playerId);
            ArenaPlayerStatusNetHandler.SendRemoveStatus(playerId);
        }
    }

    private static void RemoveExpiredPings()
    {
        long oldestAllowed = Stopwatch.GetTimestamp() - PendingPingLifetimeSeconds * Stopwatch.Frequency;
        foreach (long pingId in new List<long>(pendingPings.Keys))
        {
            if (pendingPings[pingId] < oldestAllowed)
                pendingPings.Remove(pingId);
        }
    }

    private static void Reset()
    {
        statuses.Clear();
        pendingPings.Clear();
        nextPingId = 0;
        pingTimer = PingInterval - 1;
    }
}
