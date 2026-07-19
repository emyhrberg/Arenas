using Arenas.Core;
using Arenas.Core.Configs;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.UI;

namespace Arenas.Common.Rounds;

[Autoload(Side = ModSide.Client)]
internal sealed class ArenaRoundUI : ModSystem
{
    private static float scorelineOpacity = 1f;

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int index = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
        if (index >= 0) layers.Insert(index, new LegacyGameInterfaceLayer("Arenas: Round UI", Draw, InterfaceScaleType.None));
    }

    private static bool Draw()
    {
        if (!ArenaWorldSystem.Active)
        {
            if (ModContent.GetInstance<ClientConfig>().ShowTopScoreboard)
                DrawTopScoreboard(true);
            return true;
        }
        if (ArenaRoundSystem.Phase == RoundPhase.Generating)
            DrawGeneration();
        else if (ArenaRoundSystem.Phase == RoundPhase.Voting)
        {
            ArenaBossVoteDrawer.Draw(260);
        }
        if (ModContent.GetInstance<ClientConfig>().ShowTopScoreboard)
            DrawTopScoreboard(false);
        if (ArenaRoundSystem.Phase == RoundPhase.FreezeCountdown) DrawCountdown();
        return true;
    }

    private static void DrawGeneration()
    {
        float progress = Math.Clamp(ArenaRoundSystem.GenerationProgress, 0f, 1f);
        Rectangle panel = new(Main.screenWidth / 2 - 210, Main.screenHeight / 2 - 56, 420, 112);
        Rectangle bar = new(panel.X + 28, panel.Bottom - 38, panel.Width - 56, 16);
        Utils.DrawInvBG(Main.spriteBatch, panel, new Color(25, 38, 86) * .94f);
        Utils.DrawInvBG(Main.spriteBatch, bar, Color.Black * .8f);
        Rectangle fill = new(bar.X + 3, bar.Y + 3, (int)((bar.Width - 6) * progress), bar.Height - 6);
        if (fill.Width > 0) Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, fill, new Color(90, 190, 255));
        Utils.DrawBorderStringBig(Main.spriteBatch, "Generating Arena", new Vector2(panel.Center.X, panel.Y + 28), Color.White, .65f, .5f, .5f);
        Utils.DrawBorderString(Main.spriteBatch, $"{progress:P0}", new Vector2(panel.Center.X, panel.Y + 57), Color.White, .85f, .5f, .5f);
    }

    private static void DrawTopScoreboard(bool mainWorld)
    {
        var presets = ArenaRoundSystem.GetValidPresets(); int index = ArenaRoundSystem.CurrentPresetIndex;
        int bossType = index >= 0 && index < presets.Count ? presets[index].Boss?.Type ?? 0 : 0;
        int timerWidth = mainWorld ? 240 : 184;
        const int timerHeight = 52, damageWidth = 88, damageHeight = 30, iconSize = 50;
        Rectangle timer = new(Main.screenWidth / 2 - timerWidth / 2, 0, timerWidth, timerHeight);
        Rectangle red = new(timer.X - damageWidth, timer.Y, damageWidth, damageHeight), blue = new(timer.Right, timer.Y, damageWidth, damageHeight);
        Rectangle bounds = mainWorld ? timer : Rectangle.Union(red, blue); bounds.Inflate(16, 16);
        scorelineOpacity = MathHelper.Lerp(scorelineOpacity, bounds.Contains(Main.MouseScreen.ToPoint()) ? .25f : 1f, 1f / 16f);

        Utils.DrawInvBG(Main.spriteBatch, timer, Color.White * .9f * scorelineOpacity);
        if (!mainWorld)
        {
            Utils.DrawInvBG(Main.spriteBatch, red, Main.teamColor[(int)Terraria.Enums.Team.Red] * .7f * scorelineOpacity);
            Utils.DrawInvBG(Main.spriteBatch, blue, Main.teamColor[(int)Terraria.Enums.Team.Blue] * .7f * scorelineOpacity);
            DrawDamage(TeamBossDamage(Terraria.Enums.Team.Red).ToString(), red);
        }
        Rectangle bossIcon = new(timer.Center.X - iconSize / 2, timer.Center.Y - iconSize / 2, iconSize, iconSize);
        if (!mainWorld && bossType > 0)
            ArenaBossVoteDrawer.DrawBossHead(bossType, bossIcon, .34f * scorelineOpacity);
        Text(TopStatus(), new Vector2(timer.Center.X, timer.Center.Y - 11),
            Color.White * scorelineOpacity, 1.15f, timer.Width - 18);
        if (!mainWorld)
            DrawDamage(TeamBossDamage(Terraria.Enums.Team.Blue).ToString(), blue);

        if (timer.Contains(Main.MouseScreen.ToPoint())) { Main.LocalPlayer.mouseInterface = true; Main.instance.MouseText("Round status"); }
        else if (!mainWorld && red.Contains(Main.MouseScreen.ToPoint())) { Main.LocalPlayer.mouseInterface = true; Main.instance.MouseText("Red team boss damage"); }
        else if (!mainWorld && blue.Contains(Main.MouseScreen.ToPoint())) { Main.LocalPlayer.mouseInterface = true; Main.instance.MouseText("Blue team boss damage"); }
    }

    private static string TopStatus()
    {
        if (!ArenaWorldSystem.Active) return "Waiting for arenas";
        return ArenaRoundSystem.Phase switch
        {
            RoundPhase.Idle => "Waiting for host",
            RoundPhase.Generating => "Generating",
            RoundPhase.Ready => "Waiting for host",
            RoundPhase.Sandbox => "Sandbox",
            RoundPhase.Voting => "Voting",
            RoundPhase.FreezeCountdown => Math.Max(1, (int)Math.Ceiling(ArenaRoundSystem.RemainingTicks / 60f)).ToString(),
            RoundPhase.Playing => FormatTime(ArenaRoundSystem.RemainingTicks),
            _ => "Waiting for host"
        };
    }

    private static string FormatTime(int ticks)
    {
        int seconds = Math.Max(0, (int)Math.Ceiling(ticks / 60f));
        return $"{seconds / 60}:{seconds % 60:00}";
    }

    private static void DrawCountdown()
    {
        string value = Math.Max(1, (int)Math.Ceiling(ArenaRoundSystem.RemainingTicks / 60f)).ToString();
        Utils.DrawBorderStringBig(Main.spriteBatch, value, new Vector2(Main.screenWidth / 2, Main.screenHeight / 2), Color.White, 1.5f, .5f, .5f);
    }

    private static long TeamBossDamage(Terraria.Enums.Team team) => ArenaRoundSystem.Scoreboard.Where(p => p.Team == team).Sum(p => p.BossDamage);

    private static void DrawDamage(string text, Rectangle panel)
    {
        Text(text, new(panel.Center.X, panel.Center.Y - 9), Color.White * scorelineOpacity, 1.05f, panel.Width - 8);
    }

    private static void Text(string value, Vector2 position, Color color, float scale, float maxWidth, float anchor = .5f)
    {
        float width = FontAssets.MouseText.Value.MeasureString(value).X * scale;
        if (width > maxWidth) scale *= maxWidth / width;
        Utils.DrawBorderString(Main.spriteBatch, value, position, color, scale, anchor);
    }
}
