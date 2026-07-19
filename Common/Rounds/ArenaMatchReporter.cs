using Arenas.Core.Configs.ConfigElements;
using PvPHub.Common.Authentication;
using PvPHub.Common.MainMenu.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terraria;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using CompletedMatchPayload = PvPHub.Common.MainMenu.API.MatchHistory.MatchApi.CompletedMatchPayload;
using MatchApi = PvPHub.Common.MainMenu.API.MatchHistory.MatchApi;
using MatchPayload = PvPHub.Common.MainMenu.API.MatchHistory.MatchApi.MatchPayload;
using MatchPlayerPayload = PvPHub.Common.MainMenu.API.MatchHistory.MatchApi.MatchPlayerPayload;
using MatchTeamPayload = PvPHub.Common.MainMenu.API.MatchHistory.MatchApi.MatchTeamPayload;

namespace Arenas.Common.Rounds;

/// <summary>Builds and posts one PvPHub match for each completed boss-fight round.</summary>
internal static class ArenaMatchReporter
{
    private const string GameMode = "arenas";
    private static DateTime? matchStartUtc;

    public static void BeginMatch()
    {
        if (Main.netMode != NetmodeID.MultiplayerClient)
            matchStartUtc = DateTime.UtcNow;
    }

    public static void Reset() => matchStartUtc = null;

    public static uint CalculateReward(RoundResult result)
    {
        if (result != RoundResult.BossDefeated || !ArenaRoundSystem.TryGetCurrentPreset(out BossFightPreset preset))
            return 0;

        return (uint)Math.Max(0, preset.VictoryGemReward);
    }

    public static void EndMatch(
        RoundResult result,
        IReadOnlyList<RoundPlayerStats> scoreboard,
        int bossType,
        string roundToken)
    {
        DateTime? startUtc = matchStartUtc;
        matchStartUtc = null;

        if (Main.netMode == NetmodeID.MultiplayerClient || !startUtc.HasValue)
            return;

        MatchPayload payload = BuildPayload(startUtc.Value, DateTime.UtcNow, result, scoreboard, bossType, roundToken);
        if (payload.Players.Count == 0)
        {
            Log.Warn("Arenas match post skipped because no participants had authenticated PvPHub Steam IDs.");
            return;
        }

        _ = PostSafeAsync(payload);
    }

    private static MatchPayload BuildPayload(
        DateTime startUtc,
        DateTime endUtc,
        RoundResult result,
        IReadOnlyList<RoundPlayerStats> scoreboard,
        int bossType,
        string roundToken)
    {
        uint reward = CalculateReward(result);
        Dictionary<ulong, MatchPlayerPayload> players = [];

        foreach (RoundPlayerStats stats in scoreboard)
        {
            if (stats.PlayerId >= Main.maxPlayers)
                continue;

            Player player = Main.player[stats.PlayerId];
            if (player?.active != true || !TryGetSteamId(player, out ulong steamId))
                continue;

            players[steamId] = new MatchPlayerPayload(
                stats.Name,
                (uint)stats.Team,
                reward,
                stats.Kills,
                stats.Deaths,
                result == RoundResult.BossDefeated,
                new Dictionary<string, uint>
                {
                    ["damage_dealt"] = Clamp(stats.Damage),
                    ["boss_damage_dealt"] = Clamp(stats.BossDamage)
                },
                new Dictionary<string, IDictionary<int, uint>>());
        }

        string bossName = ArenaRoundSystem.TryGetCurrentPreset(out BossFightPreset preset)
            ? ArenaRoundSystem.PresetName(preset)
            : bossType.ToString();
        Dictionary<string, string> metrics = new()
        {
            ["result"] = result.ToString(),
            ["boss_type"] = bossType.ToString(),
            ["boss_name"] = bossName,
            ["round_token"] = roundToken ?? ""
        };

        return new MatchPayload(
            DateTime.SpecifyKind(startUtc, DateTimeKind.Utc),
            DateTime.SpecifyKind(endUtc, DateTimeKind.Utc),
            GameMode,
            players,
            metrics,
            BuildTeams(scoreboard, result, bossType));
    }

    private static List<MatchTeamPayload?> BuildTeams(
        IReadOnlyList<RoundPlayerStats> scoreboard,
        RoundResult result,
        int bossType)
    {
        List<MatchTeamPayload?> teams = [null, null, null, null, null, null, null];

        foreach (IGrouping<Team, RoundPlayerStats> team in scoreboard
                     .Where(player => player.Team != Team.None)
                     .GroupBy(player => player.Team))
        {
            int teamIndex = (int)team.Key;
            while (teams.Count <= teamIndex)
                teams.Add(null);

            List<short> bosses = [];
            if (result == RoundResult.BossDefeated && bossType is > 0 and <= short.MaxValue)
                bosses.Add((short)bossType);

            long bossDamage = team.Sum(player => player.BossDamage);
            teams[teamIndex] = new MatchTeamPayload((int)Math.Clamp(bossDamage, 0L, int.MaxValue), bosses);
        }

        return teams;
    }

    private static async Task PostSafeAsync(MatchPayload payload)
    {
        ApiResult<CompletedMatchPayload> result = await MatchApi.PostOfficialMatchAsync(payload).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            Log.Error($"Arenas match post failed. Status={(int)result.Status}, Error={result.ErrorMessage}, Request={result.RequestSummary}");
            return;
        }

        long matchId = result.Data?.Id ?? 0;
        if (matchId > 0)
            Log.Info($"Arenas match posted successfully. MatchId={matchId}");
        else
            Log.Info("Arenas match posted successfully, but PvPHub returned no match id.");
    }

    private static bool TryGetSteamId(Player player, out ulong steamId)
    {
        steamId = 0;
        if (player?.active != true || Main.netMode != NetmodeID.Server || player.whoAmI is < 0 or >= Main.maxPlayers)
            return false;

        try
        {
            ulong? identity = ModContent.GetInstance<SteamAuthentication>()
                .GetAuthenticatedIdentity((byte)player.whoAmI);
            if (identity is not ulong id || id == 0 || id > long.MaxValue)
                return false;

            steamId = id;
            return true;
        }
        catch (Exception ex)
        {
            Log.Warn($"PvPHub Steam ID lookup failed for {player.name}: {ex.Message}");
            return false;
        }
    }

    private static uint Clamp(long value) => (uint)Math.Clamp(value, 0L, uint.MaxValue);
}
