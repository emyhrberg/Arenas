using Arenas.Common.Rounds;
using Arenas.Core.Utilities;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
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
            maxX = Math.Min(maxX, layout.RedBorderX * 16f - Player.width);
        else if (team == Team.Blue)
            minX = Math.Max(minX, layout.BlueBorderX * 16f);
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
            if (layout == null || ArenaRoundSystem.Phase is not (RoundPhase.Ready or RoundPhase.FreezeCountdown or RoundPhase.Playing)
                || !TryGetLocalTeam(out _))
                return true;

            int boundaryWidth = layout.TeamBorderWidth * 16;
            int screenY = (int)MathF.Round(layout.ArenaArea.Top * 16f - Main.screenPosition.Y);
            int height = layout.ArenaArea.Height * 16;
            float pulse = .86f + MathF.Sin((float)Main.timeForVisualEffects * .045f) * .08f;
            Rectangle blueLine = BoundaryRectangle(layout.BlueBorderX, boundaryWidth, screenY, height);
            Rectangle redLine = BoundaryRectangle(layout.RedBorderX, boundaryWidth, screenY, height);
            Rectangle boss = TileToScreen(layout.BossArea);
            Rectangle bossLeft = new(boss.Left - boundaryWidth / 2, boss.Top, boundaryWidth, boss.Height);
            Rectangle bossRight = new(boss.Right - boundaryWidth / 2, boss.Top, boundaryWidth, boss.Height);
            Rectangle bossTop = new(boss.Left - boundaryWidth / 2, boss.Top - boundaryWidth / 2, boss.Width + boundaryWidth, boundaryWidth);
            Rectangle bossBottom = new(boss.Left - boundaryWidth / 2, boss.Bottom - boundaryWidth / 2, boss.Width + boundaryWidth, boundaryWidth);
            DrawWorldGradients(Main.spriteBatch, blueLine, redLine, bossLeft, bossRight, bossTop, bossBottom,
                layout.BossArea.Left != layout.BlueBorderX, layout.BossArea.Right != layout.RedBorderX, pulse);
            return true;
        }

        private static Rectangle BoundaryRectangle(int tileX, int width, int screenY, int height) =>
            new((int)MathF.Round(tileX * 16f - Main.screenPosition.X) - width / 2, screenY, width, height);

        private static Rectangle TileToScreen(Rectangle tiles) => new(
            (int)MathF.Round(tiles.X * 16f - Main.screenPosition.X),
            (int)MathF.Round(tiles.Y * 16f - Main.screenPosition.Y),
            tiles.Width * 16,
            tiles.Height * 16);
    }

    internal static bool TryGetLocalTeam(out Team team)
    {
        if (!ArenaRoundSystem.TryGetParticipantTeam(Main.myPlayer, out team))
            return false;
        return team is Team.Red or Team.Blue;
    }

    private static void SetEffect(Effect effect, bool redOnLeft, float opacity, Color blocked, Color passable)
    {
        effect.Parameters["globalTime"]?.SetValue((float)Main.timeForVisualEffects);
        effect.Parameters["borderColor"]?.SetValue(blocked.ToVector3());
        effect.Parameters["passableColor"]?.SetValue(passable.ToVector3());
        effect.Parameters["opacity"]?.SetValue(opacity);
        effect.Parameters["flipGradient"]?.SetValue(redOnLeft ? 0f : 1f);
        effect.Parameters["pulseStrength"]?.SetValue(.10f);
        effect.Parameters["shimmerStrength"]?.SetValue(.08f);
        effect.Parameters["shimmerScale"]?.SetValue(16f);
        effect.Parameters["shimmerSpeed"]?.SetValue(.06f);
    }

    private static void DrawWorldGradients(SpriteBatch batch, Rectangle blueLine, Rectangle redLine,
        Rectangle bossLeft, Rectangle bossRight, Rectangle bossTop, Rectangle bossBottom,
        bool drawBossLeft, bool drawBossRight, float opacity)
    {
        if (blueLine.Width <= 0 || blueLine.Height <= 0 || redLine.Width <= 0 || redLine.Height <= 0) return;
        Color blocked = new(255, 35, 45), passable = new(35, 235, 95);
        Effect effect = EffectLoader.TryGetSpawnBoxBorderEffect(out Effect loaded) ? loaded : null;
        batch.End();
        batch.Begin(effect == null ? SpriteSortMode.Deferred : SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, Main.Rasterizer, effect, Main.GameViewMatrix.TransformationMatrix);
        if (effect != null)
        {
            // Blue's left boundary blocks movement from the right; Red's right
            // boundary blocks movement from the left. The green sides are passable.
            DrawShaderGradient(batch, effect, bossTop, redOnLeft: false, opacity, passable, passable);
            DrawShaderGradient(batch, effect, bossBottom, redOnLeft: false, opacity, passable, passable);
            DrawShaderGradient(batch, effect, blueLine, redOnLeft: false, opacity, blocked, passable);
            DrawShaderGradient(batch, effect, redLine, redOnLeft: true, opacity, blocked, passable);
            if (drawBossLeft) DrawShaderGradient(batch, effect, bossLeft, redOnLeft: false, opacity, blocked, passable);
            if (drawBossRight) DrawShaderGradient(batch, effect, bossRight, redOnLeft: true, opacity, blocked, passable);
        }
        else
        {
            DrawSolid(batch, bossTop, passable * opacity);
            DrawSolid(batch, bossBottom, passable * opacity);
            DrawPixelGradient(batch, blueLine, redOnLeft: false, opacity);
            DrawPixelGradient(batch, redLine, redOnLeft: true, opacity);
            if (drawBossLeft) DrawPixelGradient(batch, bossLeft, redOnLeft: false, opacity);
            if (drawBossRight) DrawPixelGradient(batch, bossRight, redOnLeft: true, opacity);
        }
        batch.End();
        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
            DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);
    }

    private static void DrawShaderGradient(SpriteBatch batch, Effect effect, Rectangle area, bool redOnLeft, float opacity, Color blocked, Color passable)
    {
        SetEffect(effect, redOnLeft, opacity, blocked, passable);
        effect.CurrentTechnique.Passes[0].Apply();
        batch.Draw(TextureAssets.MagicPixel.Value, area, Color.White);
    }

    private static void DrawSolid(SpriteBatch batch, Rectangle area, Color color)
    {
        if (area.Width > 0 && area.Height > 0)
            batch.Draw(TextureAssets.MagicPixel.Value, area, color);
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
        if (layout == null || ArenaRoundSystem.Phase is not (RoundPhase.Ready or RoundPhase.FreezeCountdown or RoundPhase.Playing)
            || !ArenaBoundaryDrawSystem.TryGetLocalTeam(out _))
            return;

        DrawBossBorder(ref context, layout);
        DrawLine(ref context, layout, layout.BlueBorderX, redOnLeft: false);
        DrawLine(ref context, layout, layout.RedBorderX, redOnLeft: true);
        DrawSpawnRoom(ref context, layout.RedSpawnClearance, Team.Red, Language.GetTextValue("Mods.Arenas.Map.RedTeamSpawn"), ref text);
        DrawSpawnRoom(ref context, layout.BlueSpawnClearance, Team.Blue, Language.GetTextValue("Mods.Arenas.Map.BlueTeamSpawn"), ref text);
    }

    private static void DrawLine(ref MapOverlayDrawContext context, ArenaLayout layout, int tileX, bool redOnLeft)
    {
        float widthTiles = layout.TeamBorderWidth;
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

    private static void DrawBossBorder(ref MapOverlayDrawContext context, ArenaLayout layout)
    {
        Rectangle boss = ToMapRectangle(ref context, layout.BossArea);
        int thickness = Math.Max(2, (int)MathF.Round(context.MapScale * layout.TeamBorderWidth));
        Color passable = new(35, 235, 95);

        DrawMapSolid(new Rectangle(boss.Left - thickness / 2, boss.Top - thickness / 2, boss.Width + thickness, thickness), passable * .9f);
        DrawMapSolid(new Rectangle(boss.Left - thickness / 2, boss.Bottom - thickness / 2, boss.Width + thickness, thickness), passable * .9f);

        // Redraw directional sides last so blocked/passable coloring remains clear at corners.
        if (layout.BossArea.Left != layout.BlueBorderX)
            DrawDirectionalMapEdge(new Rectangle(boss.Left - thickness / 2, boss.Top, thickness, boss.Height), redOnLeft: false);
        if (layout.BossArea.Right != layout.RedBorderX)
            DrawDirectionalMapEdge(new Rectangle(boss.Right - thickness / 2, boss.Top, thickness, boss.Height), redOnLeft: true);
    }

    private static void DrawSpawnRoom(ref MapOverlayDrawContext context, Rectangle tiles, Team team, string label, ref string hoverText)
    {
        Rectangle room = ToMapRectangle(ref context, tiles);
        if (room.Width <= 0 || room.Height <= 0)
            return;

        Rectangle visibleRoom = room;
        Rectangle? mapClip = context.ClippingRectangle;
        if (mapClip is Rectangle clip)
        {
            visibleRoom = Rectangle.Intersect(room, clip);
            if (visibleRoom.Width <= 0 || visibleRoom.Height <= 0)
                return;
        }

        Color teamColor = Main.teamColor[(int)team];
        int thickness = Math.Clamp((int)MathF.Round(context.MapScale * 2f), 2, 8);
        DrawTeamEdge(new Rectangle(room.Left, room.Top, room.Width, thickness), teamColor, vertical: false, reverse: false);
        DrawTeamEdge(new Rectangle(room.Left, room.Bottom - thickness, room.Width, thickness), teamColor, vertical: false, reverse: true);
        DrawTeamEdge(new Rectangle(room.Left, room.Top + thickness, thickness, Math.Max(0, room.Height - thickness * 2)), teamColor, vertical: true, reverse: false);
        DrawTeamEdge(new Rectangle(room.Right - thickness, room.Top + thickness, thickness, Math.Max(0, room.Height - thickness * 2)), teamColor, vertical: true, reverse: true);

        Point mouse = Main.MouseScreen.ToPoint();
        if (visibleRoom.Contains(mouse))
            hoverText = label;

        if (!Main.mapFullscreen && room.Width < 48)
            return;

        Vector2 textSize = FontAssets.DeathText.Value.MeasureString(label);
        float widthScale = (room.Width + 100f) / Math.Max(1f, textSize.X);
        float scale = Math.Clamp(widthScale, .28f, .58f);
        Vector2 position = room.Center.ToVector2();

        // MapOverlayDrawContext clips icons itself, but this label is a SpriteFont draw and
        // bypasses that path. Keep its complete drawn bounds inside the minimap instead of
        // restarting vanilla's SpriteBatch with a scissor state in the middle of its map pass.
        if (mapClip is Rectangle labelClip)
        {
            const float outlinePadding = 4f;
            float availableWidth = Math.Max(0f, labelClip.Width - outlinePadding * 2f);
            float availableHeight = Math.Max(0f, labelClip.Height - outlinePadding * 2f);
            scale = Math.Min(scale, Math.Min(availableWidth / Math.Max(1f, textSize.X),
                availableHeight / Math.Max(1f, textSize.Y)));
            if (scale < .12f)
                return;

            Vector2 halfSize = textSize * scale * .5f;
            position.X = MathHelper.Clamp(position.X, labelClip.Left + outlinePadding + halfSize.X,
                labelClip.Right - outlinePadding - halfSize.X);
            position.Y = MathHelper.Clamp(position.Y, labelClip.Top + outlinePadding + halfSize.Y,
                labelClip.Bottom - outlinePadding - halfSize.Y);
        }

        Utils.DrawBorderStringBig(Main.spriteBatch, label, position, teamColor, scale, .5f, .5f);
    }

    private static Rectangle ToMapRectangle(ref MapOverlayDrawContext context, Rectangle tiles)
    {
        Vector2 topLeft = (tiles.TopLeft() - context.MapPosition) * context.MapScale + context.MapOffset;
        return new Rectangle((int)MathF.Round(topLeft.X), (int)MathF.Round(topLeft.Y),
            Math.Max(1, (int)MathF.Round(tiles.Width * context.MapScale)),
            Math.Max(1, (int)MathF.Round(tiles.Height * context.MapScale)));
    }

    private static void DrawDirectionalMapEdge(Rectangle area, bool redOnLeft)
    {
        if (!TryClipToMap(ref area)) return;
        ArenaBoundaryDrawSystem.DrawPixelGradient(Main.spriteBatch, area, redOnLeft, .9f);
    }

    private static void DrawMapSolid(Rectangle area, Color color)
    {
        if (!TryClipToMap(ref area)) return;
        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, area, color);
    }

    private static void DrawTeamEdge(Rectangle area, Color teamColor, bool vertical, bool reverse)
    {
        if (area.Width <= 0 || area.Height <= 0 || !TryClipToMap(ref area)) return;
        Texture2D pixel = TextureAssets.MagicPixel.Value;
        int depth = vertical ? area.Width : area.Height;
        for (int i = 0; i < depth; i++)
        {
            float amount = depth == 1 ? 1f : i / (float)(depth - 1);
            if (reverse) amount = 1f - amount;
            Color color = Color.Lerp(Color.Lerp(teamColor, Color.White, .3f), teamColor * .42f, amount);
            Rectangle strip = vertical
                ? new Rectangle(area.X + i, area.Y, 1, area.Height)
                : new Rectangle(area.X, area.Y + i, area.Width, 1);
            Main.spriteBatch.Draw(pixel, strip, color);
        }
    }

    private static bool TryClipToMap(ref Rectangle area)
    {
        if (!Main.mapFullscreen && Main.mapStyle == 1)
            area = Rectangle.Intersect(area, new Rectangle(Main.miniMapX, Main.miniMapY, Main.miniMapWidth, Main.miniMapHeight));
        return area.Width > 0 && area.Height > 0;
    }
}
