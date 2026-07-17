using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria.Enums;
using Terraria.GameContent;

namespace Arenas.Common.Rounds;

internal static class ArenaScoreboardDrawer
{
    private const int TeamHeaderHeight = 58, ColumnHeight = 36, RowHeight = 72, BossPanelHeight = 76, BossPanelGap = 4;
    private static Texture2D PanelBackground => Main.Assets.Request<Texture2D>("Images/UI/PanelBackground").Value;
    private static Texture2D PanelBorder => Main.Assets.Request<Texture2D>("Images/UI/PanelBorder").Value;

    public static int MeasureHeight(IReadOnlyList<RoundPlayerStats> players) => TeamHeaderHeight + ColumnHeight + Math.Max(1, Math.Max(players.Count(p => p.Team == Team.Red), players.Count(p => p.Team == Team.Blue))) * RowHeight;

    public static void Draw(Rectangle bounds, IReadOnlyList<RoundPlayerStats> players)
    {
        DrawBossPanel(bounds);
        int gap = 10, width = (bounds.Width - gap) / 2, height = bounds.Height;
        int rows = Math.Max(1, Math.Max(players.Count(p => p.Team == Team.Red), players.Count(p => p.Team == Team.Blue)));
        int rowHeight = Math.Min(RowHeight, Math.Max(26, (height - TeamHeaderHeight - ColumnHeight - 8) / rows));
        DrawTeam(new(bounds.X, bounds.Y, width, height), Team.Red, TeamPlayers(players, Team.Red), rowHeight);
        DrawTeam(new(bounds.Right - width, bounds.Y, width, height), Team.Blue, TeamPlayers(players, Team.Blue), rowHeight);
    }

    private static void DrawBossPanel(Rectangle scoreboard)
    {
        var presets = ArenaRoundSystem.GetValidPresets(); int index = ArenaRoundSystem.CurrentPresetIndex;
        if (index < 0 || index >= presets.Count) return;
        var preset = presets[index]; const string label = "Current boss"; string name = preset.Boss.DisplayName;
        int textWidth = (int)Math.Ceiling(Math.Max(FontAssets.MouseText.Value.MeasureString(label).X * .82f, FontAssets.MouseText.Value.MeasureString(name).X * 1.16f));
        int width = Math.Min(scoreboard.Width, 10 + 60 + 10 + textWidth + 10);
        Rectangle panel = new(scoreboard.Center.X - width / 2, scoreboard.Y - BossPanelGap - BossPanelHeight, width, BossPanelHeight);
        Utils.DrawSplicedPanel(Main.spriteBatch, PanelBackground, panel.X, panel.Y, panel.Width, panel.Height, 10, 10, 10, 10, new Color(34, 43, 96) * .96f);
        Utils.DrawSplicedPanel(Main.spriteBatch, PanelBorder, panel.X, panel.Y, panel.Width, panel.Height, 10, 10, 10, 10, new Color(236, 205, 89));
        Rectangle portrait = new(panel.X + 10, panel.Y + 8, 60, 60); int textX = portrait.Right + 10, available = panel.Right - textX - 10;
        Utils.DrawInvBG(Main.spriteBatch, portrait, new Color(52, 66, 140) * .95f);
        ArenaBossVoteDrawer.DrawBossHead(preset.Boss.Type, portrait);
        Text(label, new(textX, panel.Y + 12), new Color(255, 226, 118), .82f, available, 0f);
        Text(name, new(textX, panel.Y + 36), Color.White, 1.16f, available, 0f);
    }

    public static void DrawTeamPanel(SpriteBatch batch, Rectangle rect, Color teamColor, float opacity, float fill = .72f)
    {
        Utils.DrawSplicedPanel(batch, PanelBackground, rect.X, rect.Y, rect.Width, rect.Height, 10, 10, 10, 10, teamColor * (fill * opacity));
        Utils.DrawSplicedPanel(batch, PanelBorder, rect.X, rect.Y, rect.Width, rect.Height, 10, 10, 10, 10, Color.Black * opacity);
    }

    private static void DrawTeam(Rectangle panel, Team team, List<RoundPlayerStats> players, int rowHeight)
    {
        Color teamColor = Main.teamColor[(int)team];
        DrawTeamPanel(Main.spriteBatch, panel, teamColor, .98f, .22f);
        Rectangle header = new(panel.X, panel.Y, panel.Width, TeamHeaderHeight); DrawTeamPanel(Main.spriteBatch, header, teamColor, 1f, .78f);
        Text($"{team} team", new(header.X + 16, header.Y + 16), Color.White, 1.2f, header.Width / 2f, 0f);
        Text($"Boss damage  {players.Sum(p => p.BossDamage)}", new(header.Right - 16, header.Y + 17), Color.White, 1.02f, header.Width / 2f, 1f);

        int statsLeft = panel.X + Math.Max(112, (int)(panel.Width * .43f)), statsWidth = panel.Right - statsLeft;
        int killsX = statsLeft + (int)(statsWidth * .06f), deathsX = statsLeft + (int)(statsWidth * .21f), damageX = statsLeft + (int)(statsWidth * .44f);
        int bossX = statsLeft + (int)(statsWidth * .69f), pingX = statsLeft + (int)(statsWidth * .91f);
        int columnsY = header.Bottom;
        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(panel.X + 4, columnsY, panel.Width - 8, ColumnHeight), Color.Black * .34f);
        Text("Player", new(panel.X + 66, columnsY + 8), Color.White * .9f, .92f, Math.Max(30, statsLeft - panel.X - 72), 0f);
        Text("Kills", new(killsX, columnsY + 8), Color.White * .9f, .84f, Math.Max(22, statsWidth * .12f));
        Text("Deaths", new(deathsX, columnsY + 8), Color.White * .9f, .78f, Math.Max(22, statsWidth * .18f));
        Text("Damage", new(damageX, columnsY + 8), Color.White * .9f, .8f, Math.Max(46, statsWidth * .22f));
        Text("Boss", new(bossX, columnsY + 8), Color.White * .9f, .84f, Math.Max(38, statsWidth * .18f));
        Text("Ping", new(pingX, columnsY + 8), Color.White * .9f, .84f, Math.Max(32, statsWidth * .16f));

        if (players.Count == 0) { Text("No players", new(panel.Center.X, columnsY + ColumnHeight + 14), Color.White * .65f, .96f, panel.Width - 24); return; }
        for (int i = 0; i < players.Count; i++)
        {
            RoundPlayerStats stats = players[i];
            Rectangle row = new(panel.X + 5, columnsY + ColumnHeight + i * rowHeight, panel.Width - 10, rowHeight);
            bool local = stats.PlayerId == Main.myPlayer, hover = row.Contains(Main.MouseScreen.ToPoint());
            Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, row, local ? teamColor * .42f : hover ? Color.White * .14f : Color.Black * (i % 2 == 0 ? .22f : .12f));
            if (hover) { Main.LocalPlayer.mouseInterface = true; Main.instance.MouseText(stats.Name); }
            int headSize = Math.Min(52, rowHeight - 8);
            Rectangle head = new(row.X - 1, row.Center.Y - headSize / 2, headSize, headSize);
            Utils.DrawInvBG(Main.spriteBatch, head, teamColor * .8f);
            ArenaPlayerStatus status = ArenaPlayerStatusSystem.GetStatus(stats.PlayerId);
            Player player = stats.PlayerId < Main.maxPlayers ? Main.player[stats.PlayerId] : null;
            if (SteamAvatarCache.TryGetAvatar(stats.PlayerId, out Texture2D avatar))
                DrawSteamAvatar(avatar, head, status);
            else if (player != null)
                Main.MapPlayerRenderer.DrawPlayerHead(Main.Camera, player, head.Center.ToVector2() - new Vector2(4f, 0f), 1f, .6f * headSize / 26f, teamColor);
            Text(stats.Name, new(row.X + 68, row.Center.Y - 12), local ? new Color(255, 244, 170) : Color.White, 1.16f, Math.Max(30, statsLeft - row.X - 76), 0f);
            Text(stats.Kills.ToString(), new(killsX, row.Center.Y - 12), Color.White, 1.08f, Math.Max(22, statsWidth * .12f));
            Text(stats.Deaths.ToString(), new(deathsX, row.Center.Y - 12), Color.White, 1.08f, Math.Max(22, statsWidth * .18f));
            Text(Compact(stats.Damage), new(damageX, row.Center.Y - 12), Color.White, 1.02f, Math.Max(46, statsWidth * .22f));
            Text(stats.BossDamage.ToString(), new(bossX, row.Center.Y - 12), new Color(255, 228, 124), 1.02f, Math.Max(38, statsWidth * .18f));
            Text(PingText(status.PingMs), new(pingX, row.Center.Y - 12), PingColor(status.PingMs), .9f, Math.Max(32, statsWidth * .16f));
        }
    }

    private static void DrawSteamAvatar(Texture2D avatar, Rectangle frame, ArenaPlayerStatus status)
    {
        Rectangle portrait = new(frame.X + 4, frame.Y + 4, Math.Max(1, frame.Width - 8), Math.Max(1, frame.Height - 8));
        if (!status.Dead)
        {
            Main.spriteBatch.Draw(avatar, portrait, Color.White);
            return;
        }

        DrawGrayscale(avatar, portrait, Color.White * .62f);

        string seconds = Math.Max(0, (int)Math.Ceiling(status.RespawnTimer / 60f)).ToString();
        float scale = .95f;
        float height = FontAssets.MouseText.Value.MeasureString(seconds).Y * scale;
        Utils.DrawBorderString(Main.spriteBatch, seconds, new Vector2(portrait.Center.X, portrait.Center.Y - height / 2f), Color.White, scale, .5f);
    }

    private static void DrawGrayscale(Texture2D texture, Rectangle destination, Color color)
    {
        if (!EffectLoader.TryGetGrayscaleEffect(out Effect effect))
        {
            Main.spriteBatch.Draw(texture, destination, Color.Gray * (color.A / (float)byte.MaxValue));
            return;
        }

        effect.Parameters["Intensity"]?.SetValue(1f);
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer, effect, Matrix.Identity);
        Main.spriteBatch.Draw(texture, destination, color);
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer, null, Matrix.Identity);
    }

    private static string PingText(int pingMs) => pingMs < 0 ? "-" : $"{pingMs} ms";

    private static Color PingColor(int pingMs) => pingMs switch
    {
        < 0 => Color.Gray,
        <= 60 => new Color(126, 238, 126),
        <= 120 => new Color(255, 230, 112),
        <= 200 => new Color(255, 173, 92),
        _ => new Color(255, 112, 112)
    };

    private static List<RoundPlayerStats> TeamPlayers(IReadOnlyList<RoundPlayerStats> players, Team team) => players.Where(p => p.Team == team)
        .OrderByDescending(p => p.PlayerId == Main.myPlayer).ThenByDescending(p => p.BossDamage).ThenByDescending(p => p.Damage).ToList();

    private static string Compact(long value) => value >= 1_000_000 ? $"{value / 1_000_000f:0.#}m" : value >= 1_000 ? $"{value / 1_000f:0.#}k" : value.ToString();

    internal static void Text(string value, Vector2 position, Color color, float scale, float maxWidth, float anchor = .5f)
    {
        float width = FontAssets.MouseText.Value.MeasureString(value).X * scale;
        if (width > maxWidth) scale *= maxWidth / width;
        Utils.DrawBorderString(Main.spriteBatch, value, position, color, scale, anchor);
    }
}
