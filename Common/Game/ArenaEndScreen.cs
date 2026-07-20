using PvPFramework.Common.EndScreen;
using PvPFramework.Common.Scoreboard;
using System;
using System.Linq;
using Terraria.Enums;

namespace Arenas.Common.Game;

internal static class ArenaEndScreen
{
    internal static void Present(Team winningTeam, int winningPlayerId)
    {
        EndScreenSummary summary = new();
        Team[] teams = Main.player.Where(player => player?.active == true).Select(player => (Team)player.team)
            .Where(team => team is Team.Red or Team.Blue)
            .Distinct()
            .ToArray();

        foreach (Team team in teams)
        {
            summary.Scores.Add(new TeamScoreEntry(team, team == winningTeam ? 1 : 0));
            summary.Results[team] = winningTeam == Team.None
                ? EndScreenResult.Tie
                : team == winningTeam ? EndScreenResult.Victory : EndScreenResult.Defeat;
        }

        foreach (Player player in Main.player.Where(player => player?.active == true
            && (Team)player.team is Team.Red or Team.Blue))
        {
            ScoreboardEntry stats = ScoreboardService.GetPlayerStats(player);
            summary.Players.Add(new EndScreenPlayerStats(
                stats.PlayerId, stats.Team, stats.Name, stats.Kills, stats.Deaths,
                Clamp(stats.Damage), 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                player.whoAmI == winningPlayerId ? "Boss Finisher" : "Arena Fighter",
                player.whoAmI == winningPlayerId ? "Dealt the killing blow" : "Fought for the team"));
        }

        EndScreenService.Present(summary);
    }

    private static uint Clamp(long value) => (uint)Math.Clamp(value, 0L, uint.MaxValue);
}
