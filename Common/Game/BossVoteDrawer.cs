using Arenas.Common.AdminTools.UI;
using Arenas.Common.DataStructures;
using Arenas.Core;
using Arenas.Core.Compat;
using Arenas.Core.Configs;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria.GameContent;
using Terraria.ID;

namespace Arenas.Common.Game;

/// <summary>Draws the intermission boss vote panel above the end screen.</summary>
internal static class BossVoteDrawer
{
    private const int DesignWidth = 600, HeaderHeight = 106, RowHeight = 56, RowGap = 4, BottomPadding = 10;
    private static readonly Color PanelFill = new(45, 61, 132), PanelEdge = new(70, 89, 165), RowFill = new(30, 43, 98), RowHover = new(68, 86, 158);
    private static readonly Color DarkEdge = new(6, 12, 38), Yellow = new(246, 216, 72), HoverEdge = new(244, 209, 74), Selected = new(104, 222, 72);
    private static Texture2D PanelBackground => Main.Assets.Request<Texture2D>("Images/UI/PanelBackground").Value;
    private static Texture2D PanelBorder => Main.Assets.Request<Texture2D>("Images/UI/PanelBorder").Value;

    public static void Draw(int top = 140)
    {
        List<(int PresetIndex, BossFightPreset Preset)> presets = BossVoteSystem.VotablePresets();
        if (presets.Count == 0) return;
        BossVoteSystem voteSystem = ModContent.GetInstance<BossVoteSystem>();
        RoundManager manager = ModContent.GetInstance<RoundManager>();

        int designHeight = HeaderHeight + presets.Count * RowHeight + Math.Max(0, presets.Count - 1) * RowGap + BottomPadding;
        float scale = Math.Min(1f, Math.Min((Main.screenWidth - 12f) / DesignWidth, (Main.screenHeight - top - 2f) / designHeight));
        int S(float value) => Math.Max(1, (int)MathF.Round(value * scale));

        int panelWidth = S(DesignWidth);
        Rectangle panel = new((Main.screenWidth - panelWidth) / 2, top, panelWidth, S(designHeight));
        DrawPanel(panel, PanelFill, PanelEdge, S(9));

        int seconds = Math.Max(0, (int)Math.Ceiling(manager.RemainingTicks / 60f));
        Text($"Vote ends in {seconds}s", new Vector2(panel.Center.X, panel.Y - S(32) + 4), Color.White, .92f * scale, S(300));
        Utils.DrawBorderStringBig(Main.spriteBatch, "Boss Vote", new Vector2(panel.Center.X, panel.Y + S(16)), Yellow, .76f * scale, .5f, 0f);
        Text("Choose the next boss — majority wins!", new Vector2(panel.Center.X, panel.Y + S(54)), Color.White, .88f * scale, panel.Width - S(32));

        Rectangle track = new(panel.X + S(18), panel.Y + S(80), panel.Width - S(36), S(16));
        int votingSeconds = Math.Max(1, ModContent.GetInstance<ServerConfig>().VotingDurationSeconds);
        float progress = Math.Clamp(manager.RemainingTicks / (votingSeconds * 60f), 0f, 1f);
        DrawProgressBar(track, progress);

        Point mouse = new(Main.mouseX, Main.mouseY);
        if (panel.Contains(mouse)) Main.LocalPlayer.mouseInterface = true;
        int localVote = voteSystem.LocalVote;
        for (int i = 0; i < presets.Count; i++)
        {
            Rectangle row = new(panel.X + S(16), panel.Y + S(HeaderHeight + i * (RowHeight + RowGap)), panel.Width - S(32), S(RowHeight));
            bool hover = row.Contains(mouse), selected = localVote == i;
            DrawPanel(row, hover && !selected ? RowHover : RowFill, selected ? Selected : hover ? HoverEdge : DarkEdge, S(7));
            if (hover)
            {
                Main.LocalPlayer.mouseInterface = true;
                if (Main.mouseLeft && Main.mouseLeftRelease) BossVoteSystem.RequestVote(i);
            }

            Rectangle icon = new(row.X + S(10), row.Y + S(6), S(44), S(44));
            DrawPanel(icon, new Color(34, 49, 111), DarkEdge, S(6));
            DrawBossHead(presets[i].Preset.Boss?.Type ?? 0, icon);
            DrawVoteState(icon, selected, hover, scale);

            Color nameColor = selected ? new Color(206, 255, 142) : hover ? Yellow : Color.White;
            Text(PresetName(presets[i].Preset), new Vector2(row.X + S(66), row.Y + S(15)), nameColor, .9f * scale, S(250), 0f);

            Rectangle counter = new(row.Right - S(50), row.Y + S(8), S(42), S(40));
            DrawPanel(counter, new Color(6, 11, 35), DarkEdge, S(6));
            int count = voteSystem.VoteCount(i);
            Text(count.ToString(), new Vector2(counter.Center.X, counter.Y + S(9)), count > 0 ? Yellow : Color.Gray, .9f * scale, counter.Width - S(8));
            DrawVoters(row, counter, voteSystem.VotersFor(i), mouse, scale, S);
        }
    }

    private static string PresetName(BossFightPreset preset) => preset?.Boss?.DisplayName ?? "Boss";

    private static void DrawVoters(Rectangle row, Rectangle counter, IReadOnlyList<byte> voters, Point mouse, float scale, Func<float, int> s)
    {
        if (voters.Count == 0) return;
        int size = s(30), right = counter.X - s(7) - size, minimum = row.X + s(285);
        float step = voters.Count == 1 ? 0f : Math.Min(s(34), Math.Max(0, right - minimum) / (float)(voters.Count - 1));
        float start = right - step * (voters.Count - 1);
        for (int i = 0; i < voters.Count; i++)
        {
            int id = voters[i];
            if (id < 0 || id >= Main.maxPlayers) continue;
            Player player = Main.player[id];
            if (player?.active != true) continue;
            Rectangle tile = new((int)MathF.Round(start + step * i) - 8, row.Y + s(13), size, size);
            Color team = player.team > 0 && player.team < Main.teamColor.Length ? Main.teamColor[player.team] : Color.Gray;
            DrawPanel(tile, Color.Lerp(player.shirtColor, team, .25f), DarkEdge, s(6));
            ErkySSCCompat.DrawUnfilteredPlayerHead(player, tile.Center.ToVector2() - new Vector2(4f, 0f), 1f, .5f * scale, team);
            if (tile.Contains(mouse)) { Main.LocalPlayer.mouseInterface = true; Main.instance.MouseText(player.name); }
        }
    }

    private static void DrawProgressBar(Rectangle track, float progress)
    {
        UISlider.DrawBar(Main.spriteBatch, Ass.Slider.Value, track, new Color(5, 10, 35));
        UISlider.DrawBar(Main.spriteBatch, Ass.SliderHighlight.Value, track, DarkEdge);
        Rectangle inner = track; inner.Inflate(-4, -4);
        int width = (int)MathF.Round(inner.Width * progress); if (width <= 0) return;
        Rectangle fill = new(inner.X, inner.Y, Math.Min(inner.Width, Math.Max(12, width)), inner.Height);
        UISlider.DrawBar(Main.spriteBatch, Ass.Slider.Value, fill, Yellow);
        UISlider.DrawBar(Main.spriteBatch, Ass.SliderHighlight.Value, fill, new Color(255, 239, 145));
    }

    internal static void DrawBossHead(int type, Rectangle box, float opacity = 1f)
    {
        int head = type >= 0 && type < NPCID.Sets.BossHeadTextures.Length
            ? NPCID.Sets.BossHeadTextures[type]
            : -1;
        if (head >= 0 && head < TextureAssets.NpcHeadBoss.Length)
        {
            Texture2D texture = TextureAssets.NpcHeadBoss[head].Value;
            float scale = Math.Min((box.Width - 8f) / texture.Width, (box.Height - 8f) / texture.Height);
            Main.spriteBatch.Draw(texture, box.Center.ToVector2(), null, Color.White * opacity, 0f,
                texture.Size() / 2f, scale, SpriteEffects.None, 0f);
            return;
        }

        if (type <= 0 || type >= TextureAssets.Npc.Length)
            return;

        Main.instance.LoadNPC(type);
        Texture2D npc = TextureAssets.Npc[type].Value;
        Rectangle source = new(0, 0, npc.Width, npc.Height / Math.Max(1, Main.npcFrameCount[type]));
        float fallbackScale = Math.Min((box.Width - 8f) / source.Width, (box.Height - 8f) / source.Height);
        Main.spriteBatch.Draw(npc, box.Center.ToVector2(), source, Color.White * opacity, 0f,
            source.Size() / 2f, fallbackScale, SpriteEffects.None, 0f);
    }

    private static void DrawVoteState(Rectangle icon, bool selected, bool hover, float scale)
    {
        Texture2D texture = (selected ? hover ? Ass.IconCheckOnHover : Ass.IconCheckOn : hover ? Ass.IconCheckOffHover : Ass.IconCheckOff).Value;
        float drawScale = Math.Max(.75f, scale);
        Main.spriteBatch.Draw(texture, new Vector2(icon.Right - texture.Width * drawScale, icon.Bottom - texture.Height * drawScale), null, Color.White, 0f, Vector2.Zero, drawScale, SpriteEffects.None, 0f);
    }

    private static void DrawPanel(Rectangle rectangle, Color fill, Color edge, int corner)
    {
        Utils.DrawSplicedPanel(Main.spriteBatch, PanelBackground, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, corner, corner, corner, corner, fill);
        Utils.DrawSplicedPanel(Main.spriteBatch, PanelBorder, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, corner, corner, corner, corner, edge);
    }

    internal static void Text(string value, Vector2 position, Color color, float scale, float maxWidth, float anchor = .5f)
    {
        float width = FontAssets.MouseText.Value.MeasureString(value).X * scale;
        if (width > maxWidth) scale *= maxWidth / width;
        Utils.DrawBorderString(Main.spriteBatch, value, position, color, scale, anchor);
    }
}
