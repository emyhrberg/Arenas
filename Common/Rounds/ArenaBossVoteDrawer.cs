using Arenas.Core.Configs;
using Arenas.Core.Configs.ConfigElements;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria.GameContent;
using Terraria.ID;

namespace Arenas.Common.Rounds;

internal static class ArenaBossVoteDrawer
{
    private const int DesignWidth = 720, HeaderHeight = 130, RowHeight = 66, RowGap = 6, BottomPadding = 16;
    private static readonly Color PanelFill = new(45, 61, 132), PanelEdge = new(70, 89, 165), RowFill = new(30, 43, 98), RowHover = new(68, 86, 158);
    private static readonly Color DarkEdge = new(6, 12, 38), Yellow = new(246, 216, 72), HoverEdge = new(244, 209, 74), Selected = new(104, 222, 72);
    private static Texture2D PanelBackground => Main.Assets.Request<Texture2D>("Images/UI/PanelBackground").Value;
    private static Texture2D PanelBorder => Main.Assets.Request<Texture2D>("Images/UI/PanelBorder").Value;

    public static void Draw()
    {
        List<BossFightPreset> presets = ArenaRoundSystem.GetValidPresets();
        if (presets.Count == 0) return;

        int designHeight = HeaderHeight + presets.Count * RowHeight + Math.Max(0, presets.Count - 1) * RowGap + BottomPadding;
        float scale = Math.Min(1f, Math.Min((Main.screenWidth - 12f) / DesignWidth, (Main.screenHeight - 2f) / (designHeight + 60f)));
        int S(float value) => Math.Max(1, (int)MathF.Round(value * scale));

        Rectangle panel = new((Main.screenWidth - S(DesignWidth)) / 2, S(60), S(DesignWidth), S(designHeight));
        DrawPanel(panel, PanelFill, PanelEdge, S(10));

        int seconds = Math.Max(0, (int)Math.Ceiling(ArenaRoundSystem.RemainingTicks / 60f));
        ArenaScoreboardDrawer.Text($"Vote ends in {seconds}s", new Vector2(panel.Center.X, S(20)), Color.White, 1.05f * scale, S(360));
        Utils.DrawBorderStringBig(Main.spriteBatch, "Boss Vote", new Vector2(panel.Center.X, panel.Y + S(20)), Yellow, .88f * scale, .5f, 0f);
        ArenaScoreboardDrawer.Text("Choose the next boss \u2014 majority wins!", new Vector2(panel.Center.X, panel.Y + S(72)), Color.White, 1.02f * scale, panel.Width - S(40));

        Rectangle track = new(panel.X + S(24), panel.Y + S(104), panel.Width - S(48), S(22));
        DrawPanel(track, new Color(5, 10, 35), DarkEdge, S(9));
        float progress = Math.Clamp(ArenaRoundSystem.RemainingTicks / (Math.Max(1, ModContent.GetInstance<ArenasConfig>().VotingDurationSeconds) * 60f), 0f, 1f);
        int fillWidth = (int)((track.Width - S(4)) * progress);
        if (fillWidth > 0) DrawPanel(new Rectangle(track.X + S(2), track.Y + S(2), fillWidth, track.Height - S(4)), Yellow, new Color(199, 166, 51), S(7));

        Point mouse = new(Main.mouseX, Main.mouseY);
        if (panel.Contains(mouse)) Main.LocalPlayer.mouseInterface = true;
        for (int i = 0; i < presets.Count; i++)
        {
            Rectangle row = new(panel.X + S(22), panel.Y + S(HeaderHeight + i * (RowHeight + RowGap)), panel.Width - S(22), S(RowHeight));
            bool hover = row.Contains(mouse), selected = ArenaRoundSystem.LocalVote == i;
            DrawPanel(row, hover && !selected ? RowHover : RowFill, selected ? Selected : hover ? HoverEdge : DarkEdge, S(9));
            if (hover)
            {
                Main.LocalPlayer.mouseInterface = true;
                if (Main.mouseLeft && Main.mouseLeftRelease) ArenaRoundSystem.RequestVote(i);
            }

            Rectangle icon = new(row.X + S(14), row.Y + S(8), S(52), S(48));
            DrawPanel(icon, new Color(34, 49, 111), DarkEdge, S(7));
            DrawBossHead(presets[i].Boss?.Type ?? 0, icon);

            Color nameColor = selected ? new Color(206, 255, 142) : hover ? Yellow : Color.White;
            ArenaScoreboardDrawer.Text(ArenaRoundSystem.PresetName(presets[i]), new Vector2(row.X + S(82), row.Y + S(18)), nameColor, 1.05f * scale, S(300), 0f);

            Rectangle counter = new(row.Right - S(62), row.Y + S(11), S(54), S(42));
            DrawPanel(counter, new Color(6, 11, 35), DarkEdge, S(7));
            int count = i < ArenaRoundSystem.VoteCounts.Count ? ArenaRoundSystem.VoteCounts[i] : 0;
            ArenaScoreboardDrawer.Text(count.ToString(), new Vector2(counter.Center.X, counter.Y + S(10)), count > 0 ? Yellow : Color.Gray, 1.05f * scale, counter.Width - S(8));
            DrawVoters(row, counter, ArenaRoundSystem.VotersFor(i), mouse, scale, S);
        }
    }

    private static void DrawVoters(Rectangle row, Rectangle counter, IReadOnlyList<byte> voters, Point mouse, float scale, Func<float, int> s)
    {
        if (voters.Count == 0) return;
        int size = s(36), right = counter.X - s(8) - size, minimum = row.X + s(360);
        float step = voters.Count == 1 ? 0f : Math.Min(s(40), Math.Max(0, right - minimum) / (float)(voters.Count - 1));
        float start = right - step * (voters.Count - 1);
        for (int i = 0; i < voters.Count; i++)
        {
            int id = voters[i];
            if (id < 0 || id >= Main.maxPlayers) continue;
            Player player = Main.player[id];
            Rectangle tile = new((int)MathF.Round(start + step * i), row.Y + s(14), size, size);
            Color team = player.team > 0 && player.team < Main.teamColor.Length ? Main.teamColor[player.team] : Color.Gray;
            DrawPanel(tile, Color.Lerp(player.shirtColor, team, .25f), DarkEdge, s(6));
            Main.MapPlayerRenderer.DrawPlayerHead(Main.Camera, player, tile.Center.ToVector2(), 1f, .58f * scale, team);
            if (tile.Contains(mouse)) { Main.LocalPlayer.mouseInterface = true; Main.instance.MouseText(player.name); }
        }
    }

    internal static void DrawBossHead(int type, Rectangle box)
    {
        int head = type >= 0 && type < NPCID.Sets.BossHeadTextures.Length ? NPCID.Sets.BossHeadTextures[type] : -1;
        if (head >= 0 && head < TextureAssets.NpcHeadBoss.Length)
        {
            Texture2D texture = TextureAssets.NpcHeadBoss[head].Value;
            float scale = Math.Min((box.Width - 8f) / texture.Width, (box.Height - 8f) / texture.Height);
            Main.spriteBatch.Draw(texture, box.Center.ToVector2(), null, Color.White, 0f, texture.Size() / 2f, scale, SpriteEffects.None, 0f);
            return;
        }
        if (type <= 0 || type >= TextureAssets.Npc.Length) return;
        Main.instance.LoadNPC(type);
        Texture2D npc = TextureAssets.Npc[type].Value;
        Rectangle source = new(0, 0, npc.Width, npc.Height / Math.Max(1, Main.npcFrameCount[type]));
        float fallbackScale = Math.Min((box.Width - 8f) / source.Width, (box.Height - 8f) / source.Height);
        Main.spriteBatch.Draw(npc, box.Center.ToVector2(), source, Color.White, 0f, source.Size() / 2f, fallbackScale, SpriteEffects.None, 0f);
    }

    private static void DrawPanel(Rectangle rectangle, Color fill, Color edge, int corner)
    {
        Utils.DrawSplicedPanel(Main.spriteBatch, PanelBackground, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, corner, corner, corner, corner, fill);
        Utils.DrawSplicedPanel(Main.spriteBatch, PanelBorder, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, corner, corner, corner, corner, edge);
    }
}
