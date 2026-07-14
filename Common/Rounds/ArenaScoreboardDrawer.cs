using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria.Enums;
using Terraria.GameContent;

namespace Arenas.Common.Rounds;

internal static class ArenaScoreboardDrawer
{
    private const int HeaderHeight = 44, RowHeight = 46;
    private static Texture2D PanelBackground => Main.Assets.Request<Texture2D>("Images/UI/PanelBackground").Value;
    private static Texture2D PanelBorder => Main.Assets.Request<Texture2D>("Images/UI/PanelBorder").Value;

    public static int MeasureHeight(IReadOnlyList<RoundPlayerStats> players) => HeaderHeight + Math.Max(1, Math.Max(players.Count(p => p.Team == Team.Red), players.Count(p => p.Team == Team.Blue))) * RowHeight + 8;

    public static void Draw(Rectangle bounds, IReadOnlyList<RoundPlayerStats> players)
    {
        int gap = 10, width = (bounds.Width - gap) / 2;
        DrawTeam(new Rectangle(bounds.X, bounds.Y, width, bounds.Height), Team.Red, players.Where(p => p.Team == Team.Red).ToList());
        DrawTeam(new Rectangle(bounds.Right - width, bounds.Y, width, bounds.Height), Team.Blue, players.Where(p => p.Team == Team.Blue).ToList());
    }

    public static void DrawTeamPanel(SpriteBatch spriteBatch, Rectangle rect, Color teamColor, float opacity, float fill = .72f)
    {
        Utils.DrawSplicedPanel(spriteBatch, PanelBackground, rect.X, rect.Y, rect.Width, rect.Height, 10, 10, 10, 10, teamColor * (fill * opacity));
        Utils.DrawSplicedPanel(spriteBatch, PanelBorder, rect.X, rect.Y, rect.Width, rect.Height, 10, 10, 10, 10, Color.Black * opacity);
    }

    private static void DrawTeam(Rectangle panel, Team team, List<RoundPlayerStats> players)
    {
        DrawTeamPanel(Main.spriteBatch, panel, Main.teamColor[(int)team], .95f);
        int killsX = panel.Right - 116, deathsX = panel.Right - 78, damageX = panel.Right - 35;
        Text($"{team.ToString().ToUpper()} TEAM", new Vector2(panel.X + 12, panel.Y + 12), Color.White, .82f, Math.Max(20, killsX - panel.X - 20), 0);
        Text("K", new Vector2(killsX, panel.Y + 12), Color.White, .75f, 30);
        Text("D", new Vector2(deathsX, panel.Y + 12), Color.White, .75f, 30);
        Text("DMG", new Vector2(damageX, panel.Y + 12), Color.White, .68f, 58);

        if (players.Count == 0) Text("No players", new Vector2(panel.Center.X, panel.Y + HeaderHeight + 13), Color.White * .7f, .8f, panel.Width - 24);
        for (int i = 0; i < players.Count; i++)
        {
            RoundPlayerStats stats = players[i];
            Rectangle row = new(panel.X + 5, panel.Y + HeaderHeight + i * RowHeight, panel.Width - 10, RowHeight);
            Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, row, Color.Black * (i % 2 == 0 ? .18f : .1f));
            if (stats.PlayerId < Main.maxPlayers && Main.player[stats.PlayerId] is Player player)
                Main.MapPlayerRenderer.DrawPlayerHead(Main.Camera, player, new Vector2(row.X + 22, row.Center.Y), 1f, .72f, Main.teamColor[(int)team]);
            Text(stats.Name, new Vector2(row.X + 44, row.Y + 13), Color.White, .78f, Math.Max(20, killsX - row.X - 50), 0);
            Text(stats.Kills.ToString(), new Vector2(killsX, row.Y + 13), Color.White, .78f, 30);
            Text(stats.Deaths.ToString(), new Vector2(deathsX, row.Y + 13), Color.White, .78f, 30);
            Text(Compact(stats.Damage), new Vector2(damageX, row.Y + 13), Color.White, .73f, 62);
        }
    }

    private static string Compact(long value) => value >= 1_000_000 ? $"{value / 1_000_000f:0.#}m" : value >= 1_000 ? $"{value / 1_000f:0.#}k" : value.ToString();

    internal static void Text(string value, Vector2 position, Color color, float scale, float maxWidth, float anchor = .5f)
    {
        float width = FontAssets.MouseText.Value.MeasureString(value).X * scale;
        if (width > maxWidth) scale *= maxWidth / width;
        Utils.DrawBorderString(Main.spriteBatch, value, position, color, scale, anchor, 0);
    }
}
