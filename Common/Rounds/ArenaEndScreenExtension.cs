using PvPFramework.Common.EndScreen;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria.Enums;

namespace Arenas.Common.Rounds;

internal static class ArenaEndScreenExtension
{
    public static EndScreenSummary CreateSummary(RoundResult result, Team winningTeam, IReadOnlyList<RoundPlayerStats> players)
    {
        EndScreenSummary summary = new();

        foreach (IGrouping<Team, RoundPlayerStats> team in players.Where(player => player.Team != Team.None)
                     .GroupBy(player => player.Team).OrderBy(team => team.Key))
        {
            uint reward = ArenaMatchReporter.CalculateReward(result, team.Key, winningTeam);
            long bossDamage = team.Sum(player => player.BossDamage);
            summary.Scores.Add(new TeamScoreEntry(team.Key, (int)Math.Clamp(bossDamage, 0L, int.MaxValue)));
            summary.Results[team.Key] = Result(result, team.Key, winningTeam);

            List<EndScreenPlayerStats> teamPlayers = team.OrderByDescending(player => player.BossDamage)
                .ThenByDescending(player => player.Damage).ThenByDescending(player => player.Kills)
                .Select(CreatePlayer).ToList();

            if (teamPlayers.Count > 0)
                teamPlayers[0] = teamPlayers[0] with
                {
                    RoleTitle = "Boss Breaker",
                    RoleValue = $"{Short(teamPlayers[0].BossDamageDealt)} boss dmg"
                };

            summary.Players.AddRange(teamPlayers);

            foreach (RoundPlayerStats player in team)
                summary.PlayerRewards[player.PlayerId] = reward;
        }

        return summary;
    }

    private static EndScreenPlayerStats CreatePlayer(RoundPlayerStats player) => new(
        player.PlayerId, player.Team, player.Name, player.Kills, player.Deaths,
        Clamp(player.Damage), 0, 0, 0, 0, 0, 0, Clamp(player.BossDamage), 0, 0, 0,
        "Arena Fighter", "Ready for the next round");

    private static EndScreenResult Result(RoundResult result, Team team, Team winningTeam) => result switch
    {
        RoundResult.BossDefeated when team == winningTeam => EndScreenResult.Victory,
        RoundResult.BossDefeated => EndScreenResult.Defeat,
        RoundResult.AdminEnded => EndScreenResult.Tie,
        _ => EndScreenResult.Defeat
    };

    private static uint Clamp(long value) => (uint)Math.Clamp(value, 0L, uint.MaxValue);
    private static string Short(uint value) => value >= 1000 ? $"{value / 1000f:0.0}k" : value.ToString();
}
