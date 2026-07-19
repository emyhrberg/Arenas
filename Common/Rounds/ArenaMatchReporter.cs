using Arenas.Common.Interop;
using Arenas.Core.Configs.ConfigElements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Terraria;
using Terraria.Enums;
using Terraria.ID;

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

        if (Main.netMode == NetmodeID.MultiplayerClient || !startUtc.HasValue || !PvPHubApi.IsAvailable)
            return;

        MatchPayload payload = BuildPayload(startUtc.Value, DateTime.UtcNow, result, scoreboard, bossType, roundToken);
        if (payload.Players.Count == 0)
        {
            Log.Warn("Arenas match post skipped because no participants had authenticated PvPHub Steam IDs.");
            return;
        }

        string payloadJson = JsonSerializer.Serialize(payload, PvPHubApi.JsonOptions);
        _ = PostSafeAsync(payloadJson);
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
            if (player?.active != true || !PvPHubApi.TryGetSteamId(player, out ulong steamId))
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

    private static async Task PostSafeAsync(string payloadJson)
    {
        PvPHubApiResult result = await PvPHubApi.PostMatchAsync(payloadJson).ConfigureAwait(false);
        if (!result.Success)
        {
            Log.Error($"Arenas match post failed. Status={result.StatusCode}, Error={result.Error}, Request={result.RequestSummary}");
            return;
        }

        if (result.TryGetMatchId(out long matchId))
            Log.Info($"Arenas match posted successfully. MatchId={matchId}");
        else
            Log.Info("Arenas match posted successfully, but PvPHub returned no match id.");
    }

    private static uint Clamp(long value) => (uint)Math.Clamp(value, 0L, uint.MaxValue);

    private sealed record MatchPayload(
        DateTime Start,
        DateTime End,
        string GameMode,
        IDictionary<ulong, MatchPlayerPayload> Players,
        IDictionary<string, string> Metrics,
        List<MatchTeamPayload?> Teams);

    private readonly record struct MatchPlayerPayload(
        string Name,
        uint Team,
        uint Reward,
        int Kills,
        int Deaths,
        bool Winner,
        IDictionary<string, uint> Stats,
        IDictionary<string, IDictionary<int, uint>> ItemStats);

    private readonly record struct MatchTeamPayload(int Points, IList<short> Bosses);
}
