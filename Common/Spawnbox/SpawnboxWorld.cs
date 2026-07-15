using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Arenas.Core.Utilities;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.Enums;
using Terraria.ModLoader;
using Terraria.UI;

namespace Arenas.Common.Spawnbox;

[Autoload(Side = ModSide.Client)]
public sealed class SpawnBoxWorld : ModSystem
{
    private const float TileSize = 16f;
    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int index = layers.FindIndex(l => l.Name == "Vanilla: Interface Logic 1");
        if (index != -1)
            layers.Insert(index + 1, new SpawnBoxInterfaceLayer());
    }

    private sealed class SpawnBoxInterfaceLayer() : GameInterfaceLayer("Arenas: SpawnBox", InterfaceScaleType.Game)
    {
        protected override bool DrawSelf()
        {
            DrawSpawnBox(Main.spriteBatch);
            return true;
        }

        private static void DrawSpawnBox(SpriteBatch spriteBatch)
        {
            if (Main.dedServ || Main.gameMenu)
                return;

            SpawnBoxSystem box = ModContent.GetInstance<SpawnBoxSystem>();
            if (!box.Active) return;
            foreach (Team team in SpawnBoxSystem.Teams)
            {
                Rectangle area = box.GetTileArea(team), inner = GetScreenRect(area); int thickness = box.GetThickness(team) * (int)TileSize;
                if (inner.Width <= 0 || inner.Height <= 0 || thickness <= 0) continue;
                bool canCross = box.CanCross(team, Main.LocalPlayer);
                Color color = Main.teamColor[(int)(canCross ? Team.Green : Team.Red)]; float opacity = canCross ? .72f : .88f;
                if (EffectLoader.TryGetSpawnBoxBorderEffect(out Effect effect)) DrawShaderBorder(spriteBatch, inner, thickness, color, opacity, effect);
                else { DrawPixelBorder(spriteBatch, inner, thickness, color, opacity); DrawCollisionLine(spriteBatch, inner, thickness, color, opacity); }
            }
        }

        private static Rectangle GetScreenRect(Rectangle area)
        {
            Vector2 screenTopLeft = new(area.Left * TileSize, area.Top * TileSize);
            Vector2 screenBottomRight = new(area.Right * TileSize, area.Bottom * TileSize);

            screenTopLeft -= Main.screenPosition;
            screenBottomRight -= Main.screenPosition;

            Rectangle rect = new(
                (int)Math.Floor(screenTopLeft.X),
                (int)Math.Floor(screenTopLeft.Y),
                (int)Math.Round(screenBottomRight.X - screenTopLeft.X),
                (int)Math.Round(screenBottomRight.Y - screenTopLeft.Y));

            return rect;
        }

        private static void DrawShaderBorder(SpriteBatch sb, Rectangle inner, int thickness, Color color, float opacity, Effect effect)
        {
            Rectangle outer = inner;
            outer.Inflate(thickness, thickness);

            if (outer.Width <= 0 || outer.Height <= 0)
                return;

            effect.Parameters["globalTime"]?.SetValue((float)Main.timeForVisualEffects);
            effect.Parameters["borderColor"]?.SetValue(color.ToVector3());
            effect.Parameters["opacity"]?.SetValue(opacity);
            effect.Parameters["borderSize"]?.SetValue(new Vector2(thickness / (float)outer.Width, thickness / (float)outer.Height));
            float collisionEdge = Math.Min(1f, 1f / thickness);
            effect.Parameters["outerEdgeFade"]?.SetValue(collisionEdge);
            effect.Parameters["innerEdgeFade"]?.SetValue(1f);
            effect.Parameters["pulseStrength"]?.SetValue(0.12f);
            effect.Parameters["shimmerStrength"]?.SetValue(0.08f);
            effect.Parameters["shimmerScale"]?.SetValue(34f);
            effect.Parameters["shimmerSpeed"]?.SetValue(0.07f);

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            effect.CurrentTechnique.Passes[0].Apply();
            sb.Draw(TextureAssets.MagicPixel.Value, outer, Color.White);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            DrawCollisionLine(sb, inner, thickness, color, opacity);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);
        }

        private static void DrawPixelBorder(SpriteBatch sb, Rectangle inner, int thickness, Color color, float opacity)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value; int steps = Math.Min(thickness, 24);
            for (int i = 0; i < steps; i++)
            {
                int start = i * thickness / steps, end = (i + 1) * thickness / steps, band = end - start;
                float strength = MathF.Pow(start / (float)thickness, .35f);
                Rectangle outer = inner, innerBand = inner; outer.Inflate(end, end); innerBand.Inflate(start, start); Color tint = color * (opacity * strength);
                sb.Draw(pixel, new Rectangle(outer.X, outer.Y, outer.Width, band), tint);
                sb.Draw(pixel, new Rectangle(outer.X, innerBand.Bottom, outer.Width, band), tint);
                sb.Draw(pixel, new Rectangle(outer.X, innerBand.Y, band, innerBand.Height), tint);
                sb.Draw(pixel, new Rectangle(innerBand.Right, innerBand.Y, band, innerBand.Height), tint);
            }
        }

        private static void DrawCollisionLine(SpriteBatch sb, Rectangle inner, int thickness, Color color, float opacity)
        {
            Rectangle edge = inner; edge.Inflate(thickness, thickness);
            Color highlight = Color.Lerp(color, Color.White, .28f) * Math.Min(1f, opacity + .18f); Texture2D pixel = TextureAssets.MagicPixel.Value;
            sb.Draw(pixel, new Rectangle(edge.X, edge.Y, edge.Width, 1), highlight);
            sb.Draw(pixel, new Rectangle(edge.X, edge.Bottom - 1, edge.Width, 1), highlight);
            sb.Draw(pixel, new Rectangle(edge.X, edge.Y, 1, edge.Height), highlight);
            sb.Draw(pixel, new Rectangle(edge.Right - 1, edge.Y, 1, edge.Height), highlight);
        }
    }
}
