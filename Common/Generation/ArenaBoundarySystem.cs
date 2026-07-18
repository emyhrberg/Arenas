using Arenas.Common.Rounds;
using Arenas.Core.Utilities;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Map;
using Terraria.UI;

namespace Arenas.Common.Generation;

internal sealed class ArenaBoundaryPlayer : ModPlayer
{
    public override void PostUpdate()
    {
        if (!ArenaWorldSystem.Active || ArenaWorldSystem.Layout == null || ArenaRoundSystem.Phase is not (RoundPhase.FreezeCountdown or RoundPhase.Playing))
            return;

        ArenaLayout layout = ArenaWorldSystem.Layout;
        if (!ArenaRoundSystem.TryGetParticipantTeam(Player.whoAmI, out Team team))
        {
            Player.immune = true;
            Player.immuneTime = 2;
            Player.noFallDmg = true;
            if (ClampTo(layout.StagingLobby)) SyncCorrection();
            return;
        }
        Rectangle arena = ToWorld(layout.ArenaArea);
        float minX = arena.Left;
        float maxX = arena.Right - Player.width;
        float minY = arena.Top;
        float maxY = arena.Bottom - Player.height;

        if (team == Team.Red)
            maxX = Math.Min(maxX, layout.BossArea.Right * 16f - Player.width);
        else if (team == Team.Blue)
            minX = Math.Max(minX, layout.BossArea.Left * 16f);
        else
            return;

        Vector2 clamped = new(MathHelper.Clamp(Player.position.X, minX, maxX), MathHelper.Clamp(Player.position.Y, minY, maxY));
        bool corrected = clamped != Player.position;
        if (clamped.X != Player.position.X) Player.velocity.X = 0f;
        if (clamped.Y != Player.position.Y) Player.velocity.Y = 0f;
        Player.position = clamped;
        if (corrected) SyncCorrection();
    }

    private bool ClampTo(Rectangle tiles)
    {
        Rectangle area = ToWorld(tiles);
        Vector2 clamped = new(MathHelper.Clamp(Player.position.X, area.Left, area.Right - Player.width),
            MathHelper.Clamp(Player.position.Y, area.Top, area.Bottom - Player.height));
        bool corrected = clamped != Player.position;
        if (corrected) Player.velocity = Vector2.Zero;
        Player.position = clamped;
        return corrected;
    }

    private void SyncCorrection()
    {
        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendData(MessageID.PlayerControls, -1, -1, null, Player.whoAmI);
    }

    private static Rectangle ToWorld(Rectangle tiles) => new(tiles.X * 16, tiles.Y * 16, tiles.Width * 16, tiles.Height * 16);
}

[Autoload(Side = ModSide.Client)]
internal sealed class ArenaBoundaryDrawSystem : ModSystem
{
    internal const int BoundaryWidthTiles = 3;

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int index = layers.FindIndex(layer => layer.Name == "Vanilla: Interface Logic 1");
        if (index >= 0) layers.Insert(index + 1, new BoundaryLayer());
    }

    private sealed class BoundaryLayer() : GameInterfaceLayer("Arenas: Team Boundary", InterfaceScaleType.Game)
    {
        protected override bool DrawSelf()
        {
            ArenaLayout layout = ArenaWorldSystem.Layout;
            if (layout == null || ArenaRoundSystem.Phase is not (RoundPhase.FreezeCountdown or RoundPhase.Playing)
                || !TryGetLocalBarrier(layout, out int tileX, out bool redOnLeft))
                return true;

            int boundaryWidth = BoundaryWidthTiles * 16;
            int screenX = (int)MathF.Round(tileX * 16f - Main.screenPosition.X) - boundaryWidth / 2;
            int screenY = (int)MathF.Round(layout.ArenaArea.Top * 16f - Main.screenPosition.Y);
            int height = layout.ArenaArea.Height * 16;
            float pulse = .86f + MathF.Sin((float)Main.timeForVisualEffects * .045f) * .08f;
            DrawWorldGradient(Main.spriteBatch, new Rectangle(screenX, screenY, boundaryWidth, height), redOnLeft, pulse);
            return true;
        }
    }

    internal static bool TryGetLocalBarrier(ArenaLayout layout, out int tileX, out bool redOnLeft)
    {
        if (ArenaRoundSystem.TryGetParticipantTeam(Main.myPlayer, out Team team) && team == Team.Red)
        {
            tileX = layout.BossArea.Right;
            redOnLeft = true;
            return true;
        }
        if (team == Team.Blue)
        {
            tileX = layout.BossArea.Left;
            redOnLeft = false;
            return true;
        }
        tileX = 0;
        redOnLeft = true;
        return false;
    }

    private static void SetEffect(Effect effect, bool redOnLeft, float opacity)
    {
        effect.Parameters["globalTime"]?.SetValue((float)Main.timeForVisualEffects);
        effect.Parameters["borderColor"]?.SetValue(new Color(255, 35, 45).ToVector3());
        effect.Parameters["passableColor"]?.SetValue(new Color(35, 235, 95).ToVector3());
        effect.Parameters["opacity"]?.SetValue(opacity);
        effect.Parameters["flipGradient"]?.SetValue(redOnLeft ? 0f : 1f);
        effect.Parameters["pulseStrength"]?.SetValue(.10f);
        effect.Parameters["shimmerStrength"]?.SetValue(.08f);
        effect.Parameters["shimmerScale"]?.SetValue(16f);
        effect.Parameters["shimmerSpeed"]?.SetValue(.06f);
    }

    private static void DrawWorldGradient(SpriteBatch batch, Rectangle area, bool redOnLeft, float opacity)
    {
        if (area.Width <= 0 || area.Height <= 0) return;
        Effect effect = EffectLoader.TryGetSpawnBoxBorderEffect(out Effect loaded) ? loaded : null;
        if (effect != null) SetEffect(effect, redOnLeft, opacity);
        batch.End();
        batch.Begin(effect == null ? SpriteSortMode.Deferred : SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, Main.Rasterizer, effect, Main.GameViewMatrix.TransformationMatrix);
        if (effect != null) effect.CurrentTechnique.Passes[0].Apply();
        if (effect != null) batch.Draw(TextureAssets.MagicPixel.Value, area, Color.White);
        else DrawPixelGradient(batch, area, redOnLeft, opacity);
        batch.End();
        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
            DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);
    }

    internal static void DrawPixelGradient(SpriteBatch batch, Rectangle area, bool redOnLeft, float opacity)
    {
        if (area.Width <= 0 || area.Height <= 0) return;
        Texture2D pixel = TextureAssets.MagicPixel.Value;
        for (int x = 0; x < area.Width; x++)
        {
            float amount = area.Width == 1 ? .5f : x / (float)(area.Width - 1);
            if (!redOnLeft) amount = 1f - amount;
            Color color = Color.Lerp(new Color(255, 35, 45), new Color(35, 235, 95), amount) * opacity;
            batch.Draw(pixel, new Rectangle(area.X + x, area.Y, 1, area.Height), color);
        }
    }
}

[Autoload(Side = ModSide.Client)]
internal sealed class ArenaBoundaryMapLayer : ModMapLayer
{
    public override void Draw(ref MapOverlayDrawContext context, ref string text)
    {
        ArenaLayout layout = ArenaWorldSystem.Layout;
        if (layout == null || ArenaRoundSystem.Phase is not (RoundPhase.FreezeCountdown or RoundPhase.Playing)
            || !ArenaBoundaryDrawSystem.TryGetLocalBarrier(layout, out int tileX, out bool redOnLeft))
            return;

        float widthTiles = ArenaBoundaryDrawSystem.BoundaryWidthTiles;
        Vector2 topLeft = (new Vector2(tileX - widthTiles / 2f, layout.ArenaArea.Top) - context.MapPosition) * context.MapScale + context.MapOffset;
        int width = Math.Max(1, (int)MathF.Round(context.MapScale * widthTiles));
        Rectangle line = new((int)MathF.Round(topLeft.X), (int)MathF.Round(topLeft.Y), width,
            Math.Max(1, (int)MathF.Round(layout.ArenaArea.Height * context.MapScale)));
        if (!Main.mapFullscreen && Main.mapStyle == 1)
        {
            line = Rectangle.Intersect(line, new Rectangle(Main.miniMapX, Main.miniMapY, Main.miniMapWidth, Main.miniMapHeight));
            if (line.Width <= 0 || line.Height <= 0) return;
        }
        ArenaBoundaryDrawSystem.DrawPixelGradient(Main.spriteBatch, line, redOnLeft, .9f);
    }
}
