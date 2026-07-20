using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria.GameContent;
using Terraria.ID;

namespace Arenas.Common.Game;

/// <summary>
/// Milestone 2 only needs the shared boss-head renderer. Interactive voting is M5.
/// </summary>
internal static class BossVoteDrawer
{
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
}
