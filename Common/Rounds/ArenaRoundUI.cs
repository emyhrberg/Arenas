using Arenas.Core;
using Arenas.Core.Configs;
using Arenas.Common.EndScreen;
using SubworldLibrary;
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
    public static bool ScoreboardVisible { get; private set; }

    public override void OnWorldLoad() => ScoreboardVisible = false;
    public override void OnWorldUnload() => ScoreboardVisible = false;
    internal static void SetScoreboardVisible(bool visible) => ScoreboardVisible = visible;

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int index = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
        if (index >= 0) layers.Insert(index, new LegacyGameInterfaceLayer("Arenas: Round UI", Draw, InterfaceScaleType.None));
    }

    private static bool Draw()
    {
        if (!SubworldSystem.IsActive<ArenasSubworld>()) return true;
        if (ArenaRoundSystem.Phase == RoundPhase.Voting)
        {
            if (!ModContent.GetInstance<EndScreenSystem>().IsVisible) ArenaBossVoteDrawer.Draw(260);
        }
        else DrawTimer();
        if (ArenaRoundSystem.Phase == RoundPhase.FreezeCountdown) DrawCountdown();
        if (ScoreboardVisible) DrawScoreboard();
        return true;
    }

    private static void DrawTimer()
    {
        int ticks = ArenaRoundSystem.Phase == RoundPhase.Idle ? ModContent.GetInstance<ArenasConfig>().RoundDurationSeconds * 60 : ArenaRoundSystem.RemainingTicks;
        int seconds = Math.Max(0, (int)Math.Ceiling(ticks / 60f));
        var presets = ArenaRoundSystem.GetValidPresets(); int index = ArenaRoundSystem.CurrentPresetIndex;
        string presetName = index >= 0 && index < presets.Count ? ArenaRoundSystem.PresetName(presets[index]) : "No active fight";
        int bossType = index >= 0 && index < presets.Count ? presets[index].Boss.Type : 0;
        const int timerMinWidth = 172, timerHeight = 52, damageWidth = 88, damageHeight = 30, iconSize = 44; const float nameScale = .72f;
        int desiredWidth = (int)Math.Ceiling(FontAssets.MouseText.Value.MeasureString(presetName).X * nameScale) + iconSize + 24;
        int timerWidth = Math.Min(Math.Max(timerMinWidth, desiredWidth), Math.Max(1, Main.screenWidth - damageWidth * 2 - 16));
        Rectangle timer = new(Main.screenWidth / 2 - timerWidth / 2, 0, timerWidth, timerHeight);
        Rectangle red = new(timer.X - damageWidth, timer.Y, damageWidth, damageHeight), blue = new(timer.Right, timer.Y, damageWidth, damageHeight);
        Rectangle bounds = Rectangle.Union(red, blue); bounds.Inflate(16, 16);
        scorelineOpacity = MathHelper.Lerp(scorelineOpacity, bounds.Contains(Main.MouseScreen.ToPoint()) ? .25f : 1f, 1f / 16f);

        Utils.DrawInvBG(Main.spriteBatch, timer, Color.White * .9f * scorelineOpacity);
        Utils.DrawInvBG(Main.spriteBatch, red, Main.teamColor[(int)Terraria.Enums.Team.Red] * .7f * scorelineOpacity);
        Utils.DrawInvBG(Main.spriteBatch, blue, Main.teamColor[(int)Terraria.Enums.Team.Blue] * .7f * scorelineOpacity);
        DrawDamage(TeamBossDamage(Terraria.Enums.Team.Red).ToString(), red);
        Rectangle bossIcon = new(timer.X + 4, timer.Center.Y - iconSize / 2, iconSize, iconSize);
        ArenaBossVoteDrawer.DrawBossHead(bossType, bossIcon, scorelineOpacity);
        int lineHeight = (int)Math.Ceiling(FontAssets.MouseText.Value.MeasureString(presetName).Y * nameScale), nameY = timer.Y + 5, timerY = timer.Y + 1 + lineHeight;
        ArenaScoreboardDrawer.Text(presetName, new Vector2((bossIcon.Right + timer.Right) / 2f, nameY), Color.White * scorelineOpacity, nameScale, timer.Right - bossIcon.Right - 8);
        ArenaScoreboardDrawer.Text($"{seconds / 60:00}:{seconds % 60:00}", new Vector2(timer.Center.X, timerY), Color.White * scorelineOpacity, 1f, timer.Width - 16);
        DrawDamage(TeamBossDamage(Terraria.Enums.Team.Blue).ToString(), blue);

        if (timer.Contains(Main.MouseScreen.ToPoint())) { Main.LocalPlayer.mouseInterface = true; Main.instance.MouseText("Time left"); }
        else if (red.Contains(Main.MouseScreen.ToPoint())) { Main.LocalPlayer.mouseInterface = true; Main.instance.MouseText("Red team boss damage"); }
        else if (blue.Contains(Main.MouseScreen.ToPoint())) { Main.LocalPlayer.mouseInterface = true; Main.instance.MouseText("Blue team boss damage"); }
    }

    private static void DrawCountdown()
    {
        string value = Math.Max(1, (int)Math.Ceiling(ArenaRoundSystem.RemainingTicks / 60f)).ToString();
        Utils.DrawBorderStringBig(Main.spriteBatch, value, new Vector2(Main.screenWidth / 2, Main.screenHeight / 2), Color.White, 1.5f, .5f, .5f);
    }

    private static void DrawScoreboard()
    {
        IReadOnlyList<RoundPlayerStats> players = ScoreboardPlayers();
        int top = 200 + (ArenaRoundSystem.Phase == RoundPhase.Voting && ModContent.GetInstance<EndScreenSystem>().IsVisible ? 40 : 0);
        int width = Math.Min(1280, Main.screenWidth - 40), height = Math.Min(ArenaScoreboardDrawer.MeasureHeight(players), Math.Max(1, Main.screenHeight - top - 20));
        ArenaScoreboardDrawer.Draw(new Rectangle((Main.screenWidth - width) / 2, top, width, height), players);
    }

    private static long TeamBossDamage(Terraria.Enums.Team team) => ArenaRoundSystem.Scoreboard.Where(p => p.Team == team).Sum(p => p.BossDamage);

    private static void DrawDamage(string text, Rectangle panel)
    {
        ArenaScoreboardDrawer.Text(text, new(panel.Center.X, panel.Center.Y - 9), Color.White * scorelineOpacity, 1.05f, panel.Width - 8);
    }

    private static IReadOnlyList<RoundPlayerStats> ScoreboardPlayers()
    {
        List<RoundPlayerStats> players = ArenaRoundSystem.Scoreboard.ToList();
        foreach (Player player in Main.player.Where(p => p?.active == true && (Team)p.team is Team.Red or Team.Blue))
        {
            if (players.Any(p => p.PlayerId == player.whoAmI)) continue;
            ArenaRoundPlayer stats = player.GetModPlayer<ArenaRoundPlayer>();
            players.Add(new((byte)player.whoAmI, (Team)player.team, player.name, stats.Kills, stats.Deaths, stats.Damage, stats.BossDamage));
        }
        return players;
    }
}
