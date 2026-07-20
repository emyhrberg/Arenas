using Arenas.Core.Configs;
using System;
using System.Collections.Generic;
using Terraria.GameContent;
using Terraria.Enums;
using Terraria.UI;

namespace Arenas.Common.Game;

[Autoload(Side = ModSide.Client)]
internal sealed class ScorelineUISystem : ModSystem
{
    private static float opacity = 1f;

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
        if (index >= 0)
            layers.Insert(index, new LegacyGameInterfaceLayer(
                "Arenas: Phase Scoreline", Draw, InterfaceScaleType.UI));
    }

    private static bool Draw()
    {
        if (Main.gameMenu || !ModContent.GetInstance<ClientConfig>().ShowTopScoreboard)
            return true;

        RoundManager manager = ModContent.GetInstance<RoundManager>();
        const int width = 250;
        const int height = 52;
        Rectangle panel = new(Main.screenWidth / 2 - width / 2, 0, width, height);
        bool hovered = panel.Contains(Main.MouseScreen.ToPoint());
        opacity = MathHelper.Lerp(opacity, hovered ? .3f : 1f, 1f / 12f);
        Utils.DrawInvBG(Main.spriteBatch, panel, Color.White * .9f * opacity);

        Rectangle icon = new(panel.X + 8, panel.Y + 2, 48, 48);
        BossVoteDrawer.DrawBossHead(manager.SelectedBossType, icon, opacity);
        DrawText(Status(manager), new Vector2(panel.X + 153, panel.Y + 16),
            Color.White * opacity, 1.05f, panel.Width - 70);

        if (hovered)
        {
            Main.LocalPlayer.mouseInterface = true;
            Main.instance.MouseText("Arenas round status");
        }

        return true;
    }

    private static string Status(RoundManager manager)
    {
        if (manager.CurrentPhase == RoundManager.RoundPhase.VotingOrEndScreen
            && (Team)Main.LocalPlayer.team is not (Team.Red or Team.Blue))
            return "Choose Red or Blue";

        return manager.CurrentPhase switch
        {
            RoundManager.RoundPhase.WaitingForPlayers => "Waiting for players",
            RoundManager.RoundPhase.VotingOrEndScreen => $"Next round {FormatTime(manager.RemainingTicks)}",
            RoundManager.RoundPhase.Generating => "Preparing",
            RoundManager.RoundPhase.FreezeCountdown => Math.Max(1,
                (int)Math.Ceiling(manager.RemainingTicks / 60f)).ToString(),
            RoundManager.RoundPhase.Playing => FormatTime(manager.RemainingTicks),
            _ => "Arenas"
        };
    }

    private static string FormatTime(int ticks)
    {
        int seconds = Math.Max(0, (int)Math.Ceiling(ticks / 60f));
        return $"{seconds / 60}:{seconds % 60:00}";
    }

    private static void DrawText(string value, Vector2 position, Color color, float scale, float maxWidth)
    {
        float width = FontAssets.MouseText.Value.MeasureString(value).X * scale;
        if (width > maxWidth)
            scale *= maxWidth / width;
        Utils.DrawBorderString(Main.spriteBatch, value, position, color, scale, .5f);
    }
}
