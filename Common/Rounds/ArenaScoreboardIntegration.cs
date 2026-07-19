using Microsoft.Xna.Framework;
using PvPFramework.Common.Scoreboard;
using System.Linq;
using Terraria.Enums;

namespace Arenas.Common.Rounds;

internal sealed class ArenaScoreboardIntegration : ModSystem
{
    private static readonly Color Gold = new(255, 228, 124);

    public override void Load()
    {
        ScoreboardColumnRegistry.Register(Mod, new ScoreboardColumn(
            "Arenas.BossDamage",
            "Boss damage",
            .23f,
            "Boss",
            entry => Compact(BossDamage(entry.PlayerId)),
            _ => Gold,
            team => Compact(ArenaRoundSystem.Scoreboard.Where(entry => entry.Team == team).Sum(entry => entry.BossDamage)),
            Gold));
    }

    public override void Unload() => ScoreboardColumnRegistry.Unregister(Mod);

    private static long BossDamage(byte playerId)
    {
        foreach (RoundPlayerStats entry in ArenaRoundSystem.Scoreboard)
            if (entry.PlayerId == playerId)
                return entry.BossDamage;
        return 0;
    }

    private static string Compact(long value) => value >= 1_000_000
        ? $"{value / 1_000_000f:0.#}m"
        : value >= 1_000 ? $"{value / 1_000f:0.#}k" : value.ToString();
}
