using Arenas.Core;
using Arenas.Core.Configs;
using SubworldLibrary;
using System;
using System.Collections.Generic;
using Terraria.UI;

namespace Arenas.Common.Rounds;

[Autoload(Side = ModSide.Client)]
internal sealed class ArenaRoundUI : ModSystem
{
    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int index = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
        if (index >= 0) layers.Insert(index, new LegacyGameInterfaceLayer("Arenas: Round UI", Draw, InterfaceScaleType.None));
    }

    private static bool Draw()
    {
        if (!SubworldSystem.IsActive<ArenasSubworld>()) return true;
        if (ArenaRoundSystem.Phase == RoundPhase.Voting) ArenaBossVoteDrawer.Draw(); else DrawTimer();
        if (ArenaRoundSystem.Phase == RoundPhase.FreezeCountdown) DrawCountdown();
        if (ArenaRoundSystem.Phase == RoundPhase.Playing && ModContent.GetInstance<Keybinds>().Scoreboard.Current) DrawScoreboard();
        return true;
    }

    private static void DrawTimer()
    {
        int ticks = ArenaRoundSystem.Phase == RoundPhase.Idle ? ModContent.GetInstance<ArenasConfig>().RoundDurationSeconds * 60 : ArenaRoundSystem.RemainingTicks;
        int seconds = Math.Max(0, (int)Math.Ceiling(ticks / 60f));
        Rectangle box = new(Main.screenWidth / 2 - 50, 0, 100, 40);
        Utils.DrawInvBG(Main.spriteBatch, box, Color.Black * .7f);
        ArenaScoreboardDrawer.Text($"{seconds / 60:00}:{seconds % 60:00}", new Vector2(box.Center.X, 10), Color.White, 1f, 90);
    }

    private static void DrawCountdown()
    {
        string value = Math.Max(1, (int)Math.Ceiling(ArenaRoundSystem.RemainingTicks / 60f)).ToString();
        Utils.DrawBorderStringBig(Main.spriteBatch, value, new Vector2(Main.screenWidth / 2, Main.screenHeight / 2), Color.White, 1.5f, .5f, .5f);
    }

    private static void DrawScoreboard()
    {
        int width = Math.Min(920, Main.screenWidth - 32), height = ArenaScoreboardDrawer.MeasureHeight(ArenaRoundSystem.Scoreboard);
        ArenaScoreboardDrawer.Draw(new Rectangle((Main.screenWidth - width) / 2, 52, width, height), ArenaRoundSystem.Scoreboard);
    }
}
