using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using Arenas.Core;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;

namespace Arenas.Common.Rounds;

internal static class ArenaScoreboardDrawer
{
    private readonly record struct StatColumn(string Tooltip, float Weight, Func<RoundPlayerStats, ArenaPlayerStatus, string> Value, Func<RoundPlayerStats, ArenaPlayerStatus, Color> Color, Action<Rectangle> Icon = null);

    private const int HeaderHeight = 54, IconRowHeight = 34, RowHeight = 64, BossPanelHeight = 76, BossPanelGap = 4, PanelGap = 10, SidePadding = 14;
    private static readonly Color Gold = new(255, 228, 124);

    private static readonly StatColumn[] Columns =
    [
        new("Kills", .17f, (stats, _) => stats.Kills.ToString(), (_, _) => Color.White, cell => DrawIcon(Ass.Attack.Value, cell)),
        new("Deaths", .17f, (stats, _) => stats.Deaths.ToString(), (_, _) => Color.White, DrawSlainIcon),
        new("Damage dealt", .23f, (stats, _) => Compact(stats.Damage), (_, _) => Color.White, cell => DrawIcon(Ass.Knock.Value, cell)),
        new("Boss damage", .23f, (stats, _) => Compact(stats.BossDamage), (_, _) => Gold, DrawBossColumnIcon),
        new("Ping", .2f, (_, status) => PingText(status.PingMs), (_, status) => PingColor(status.PingMs), cell => DrawIcon(Ass.Ping.Value, cell, 32f))
    ];

    private static Texture2D PanelBackground => Main.Assets.Request<Texture2D>("Images/UI/PanelBackground").Value;
    private static Texture2D PanelBorder => Main.Assets.Request<Texture2D>("Images/UI/PanelBorder").Value;

    public static int MeasureHeight(IReadOnlyList<RoundPlayerStats> players)
        => HeaderHeight + IconRowHeight + MaxTeamSize(players) * RowHeight + SidePadding;

    public static void Draw(Rectangle bounds, IReadOnlyList<RoundPlayerStats> players)
    {
        DrawBossPanel(bounds);
        int width = (bounds.Width - PanelGap) / 2;
        int rowHeight = Math.Min(RowHeight, Math.Max(30, (bounds.Height - HeaderHeight - IconRowHeight - SidePadding) / MaxTeamSize(players)));
        DrawTeam(new(bounds.X, bounds.Y, width, bounds.Height), Team.Red, TeamPlayers(players, Team.Red), rowHeight);
        DrawTeam(new(bounds.Right - width, bounds.Y, width, bounds.Height), Team.Blue, TeamPlayers(players, Team.Blue), rowHeight);
    }

    private static int MaxTeamSize(IReadOnlyList<RoundPlayerStats> players)
        => Math.Max(1, Math.Max(players.Count(p => p.Team == Team.Red), players.Count(p => p.Team == Team.Blue)));

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

    private static void DrawTeam(Rectangle panel, Team team, List<RoundPlayerStats> players, int rowHeight)
    {
        Color teamColor = Main.teamColor[(int)team];
        Utils.DrawSplicedPanel(Main.spriteBatch, PanelBackground, panel.X, panel.Y, panel.Width, panel.Height, 10, 10, 10, 10, teamColor * .62f);
        Utils.DrawSplicedPanel(Main.spriteBatch, PanelBorder, panel.X, panel.Y, panel.Width, panel.Height, 10, 10, 10, 10, Color.Lerp(teamColor, Color.Black, .5f));

        int headSize = Math.Min(50, rowHeight - 8);
        int statsLeft = panel.X + Math.Max(120, (int)(panel.Width * .45f));
        int statsWidth = panel.Right - SidePadding - statsLeft;

        Texture2D pvpIcons = TextureAssets.Pvp[1].Value;
        Rectangle teamIcon = pvpIcons.Frame(6, 1, (int)team); teamIcon.Width -= 2;
        Main.spriteBatch.Draw(pvpIcons, new Vector2(panel.X + SidePadding + headSize / 2f, panel.Y + HeaderHeight / 2f), teamIcon, Color.White, 0f, teamIcon.Size() / 2f, 2f, SpriteEffects.None, 0f);
        int teamTextX = panel.X + SidePadding + headSize + 10;
        Text($"{team} Team", new(teamTextX, panel.Y + 11), Color.White, 1.38f, Math.Max(40, statsLeft - teamTextX - 8), 0f);

        Rectangle total = new(panel.Right - 18 - 150, panel.Y + 10, 150, 32);
        Text(Compact(players.Sum(p => p.BossDamage)), new(panel.Right - 18, panel.Y + 15), Gold, 1.22f, total.Width, 1f);
        Tooltip(total, "Team boss damage");

        int rowsTop = panel.Y + HeaderHeight + IconRowHeight;
        if (players.Count == 0)
        {
            Text("No players", new(panel.Center.X, rowsTop + 14), Color.White * .55f, 1.02f, panel.Width - 40);
            return;
        }

        float iconX = statsLeft;
        foreach (StatColumn column in Columns)
        {
            float width = statsWidth * column.Weight;
            Rectangle cell = new((int)iconX, panel.Y + HeaderHeight, (int)width, IconRowHeight);
            column.Icon?.Invoke(cell);
            Tooltip(cell, column.Tooltip);
            iconX += width;
        }

        for (int i = 0; i < players.Count; i++)
        {
            Rectangle row = new(panel.X + SidePadding, rowsTop + i * rowHeight, panel.Width - SidePadding * 2, rowHeight);
            DrawPlayer(row, team, players[i], ArenaPlayerStatusSystem.GetStatus(players[i].PlayerId), statsLeft, statsWidth);
        }
    }

    private static void DrawIcon(Texture2D texture, Rectangle cell, float maxPixels = 26f, Color? color = null)
    {
        float scale = Math.Min(1.15f, maxPixels / Math.Max(texture.Width, texture.Height));
        Main.spriteBatch.Draw(texture, cell.Center.ToVector2(), null, color ?? Color.White, 0f, texture.Size() / 2f, scale, SpriteEffects.None, 0f);
    }

    private static void DrawSlainIcon(Rectangle cell)
    {
        Main.instance.LoadItem(ItemID.Tombstone);
        DrawIcon(TextureAssets.Item[ItemID.Tombstone].Value, cell);
    }

    private static void DrawBossColumnIcon(Rectangle cell)
    {
        var presets = ArenaRoundSystem.GetValidPresets(); int index = ArenaRoundSystem.CurrentPresetIndex;
        if (index >= 0 && index < presets.Count)
            ArenaBossVoteDrawer.DrawBossHead(presets[index].Boss.Type, new Rectangle(cell.Center.X - 17, cell.Center.Y - 17, 34, 34));
        else
            DrawIcon(Ass.Attack.Value, cell, 26f, Gold);
    }

    private static void DrawPlayer(Rectangle row, Team team, RoundPlayerStats stats, ArenaPlayerStatus status, int statsLeft, int statsWidth)
    {
        Color teamColor = Main.teamColor[(int)team];
        int headSize = Math.Min(50, row.Height - 8);
        Rectangle head = new(row.X, row.Center.Y - headSize / 2, headSize, headSize);
        Utils.DrawInvBG(Main.spriteBatch, head, teamColor * .8f);
        Player player = stats.PlayerId < Main.maxPlayers ? Main.player[stats.PlayerId] : null;
        if (SteamAvatarCache.TryGetAvatar(stats.PlayerId, out Texture2D avatar))
            DrawSteamAvatar(avatar, head, status);
        else if (player != null)
            Main.MapPlayerRenderer.DrawPlayerHead(Main.Camera, player, head.Center.ToVector2() - new Vector2(4f, 0f), 1f, .6f * headSize / 26f, teamColor);

        bool local = stats.PlayerId == Main.myPlayer;
        Color nameColor = local ? new Color(255, 244, 170) : status.Dead ? Color.White * .6f : Color.White;
        Rectangle name = new(head.Right + 10, row.Y, Math.Max(30, statsLeft - head.Right - 18), row.Height);
        Text(stats.Name, new(name.X, row.Center.Y - 13), nameColor, 1.16f, name.Width, 0f);
        Tooltip(name, stats.Name);

        float x = statsLeft;
        foreach (StatColumn column in Columns)
        {
            float width = statsWidth * column.Weight;
            Text(column.Value(stats, status), new(x + width / 2f, row.Center.Y - 13), column.Color(stats, status), 1.1f, width - 6);
            Tooltip(new((int)x, row.Y, (int)width, row.Height), column.Tooltip);
            x += width;
        }
    }

    private static void Tooltip(Rectangle area, string text)
    {
        if (!area.Contains(Main.MouseScreen.ToPoint())) return;
        Main.LocalPlayer.mouseInterface = true;
        Main.instance.MouseText(text);
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
        .OrderByDescending(p => p.BossDamage).ThenByDescending(p => p.Damage).ThenByDescending(p => p.Kills).ToList();

    private static string Compact(long value) => value >= 1_000_000 ? $"{value / 1_000_000f:0.#}m" : value >= 1_000 ? $"{value / 1_000f:0.#}k" : value.ToString();

    internal static void Text(string value, Vector2 position, Color color, float scale, float maxWidth, float anchor = .5f)
    {
        float width = FontAssets.MouseText.Value.MeasureString(value).X * scale;
        if (width > maxWidth) scale *= maxWidth / width;
        Utils.DrawBorderString(Main.spriteBatch, value, position, color, scale, anchor);
    }
}
