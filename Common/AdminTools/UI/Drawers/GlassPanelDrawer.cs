using System;
using Terraria.GameContent;

namespace Arenas.Common.AdminTools.UI.Drawers;

public readonly record struct GlassPanelStyle(Color Primary, Color Secondary, Color Border, float Opacity, float Gloss, float BorderStrength);

public static class GlassPanelDrawer
{
    private const int Slice = 10;

    private static Texture2D PanelBackground => Main.Assets.Request<Texture2D>("Images/UI/PanelBackground").Value;
    private static Texture2D PanelBorder => Main.Assets.Request<Texture2D>("Images/UI/PanelBorder").Value;

    public static readonly GlassPanelStyle Shell = new(new Color(18, 116, 82), new Color(72, 190, 146), new Color(54, 214, 154), 0.42f, 0.72f, 0.78f);
    public static readonly GlassPanelStyle Row = new(new Color(16, 112, 78), new Color(70, 180, 132), new Color(48, 195, 136), 0.34f, 0.48f, 0.62f);
    public static readonly GlassPanelStyle RowHover = new(new Color(24, 138, 96), new Color(86, 204, 150), new Color(70, 226, 158), 0.50f, 0.64f, 0.82f);
    public static readonly GlassPanelStyle RowSelected = new(new Color(30, 190, 116), new Color(142, 255, 184), new Color(210, 255, 176), 0.78f, 0.92f, 1.30f);
    public static readonly GlassPanelStyle PanelShell = new(new Color(12, 58, 70), new Color(52, 136, 130), new Color(70, 214, 170), 0.36f, 0.56f, 0.72f);
    public static readonly GlassPanelStyle PanelHeader = new(new Color(34, 72, 148), new Color(78, 178, 172), new Color(132, 240, 198), 0.52f, 0.84f, 0.96f);
    public static readonly GlassPanelStyle PanelBody = new(new Color(10, 36, 48), new Color(36, 104, 104), new Color(56, 184, 148), 0.34f, 0.46f, 0.64f);
    public static readonly GlassPanelStyle PanelInset = new(new Color(20, 50, 88), new Color(48, 130, 128), new Color(88, 204, 166), 0.42f, 0.58f, 0.76f);
    public static readonly GlassPanelStyle PanelButton = new(new Color(36, 76, 142), new Color(66, 158, 154), new Color(110, 220, 184), 0.44f, 0.58f, 0.70f);
    public static readonly GlassPanelStyle PanelButtonHover = new(new Color(52, 102, 182), new Color(94, 206, 184), new Color(154, 255, 210), 0.58f, 0.78f, 0.96f);
    public static readonly GlassPanelStyle ActionButton = new(new Color(20, 128, 88), new Color(82, 204, 150), new Color(116, 248, 184), 0.58f, 0.72f, 1.00f);
    public static readonly GlassPanelStyle ActionButtonHover = new(new Color(30, 158, 108), new Color(116, 238, 176), new Color(172, 255, 214), 0.72f, 0.90f, 1.18f);
    public static readonly GlassPanelStyle ActionButtonActive = new(new Color(52, 224, 122), new Color(164, 255, 184), new Color(238, 255, 178), 0.96f, 1.00f, 1.70f);

    public static void Draw(SpriteBatch spriteBatch, Rectangle rect, GlassPanelStyle style) => DrawPanel(spriteBatch, rect, style, drawShadow: true);

    public static void DrawSpliced(SpriteBatch spriteBatch, Rectangle rect, GlassPanelStyle style) => DrawPanel(spriteBatch, rect, style, drawShadow: false);

    private static void DrawPanel(SpriteBatch spriteBatch, Rectangle rect, GlassPanelStyle style, bool drawShadow)
    {
        if (rect.Width < Slice * 2 || rect.Height < Slice * 2)
            return;

        if (drawShadow)
            DrawShadow(spriteBatch, rect);

        DrawFallback(spriteBatch, rect, style);
        DrawBorder(spriteBatch, rect, style);
    }

    private static void DrawFallback(SpriteBatch spriteBatch, Rectangle rect, GlassPanelStyle style)
    {
        Color tint = Color.Lerp(style.Primary, style.Secondary, 0.38f) * (style.Opacity * 0.52f);

        Panel(spriteBatch, PanelBackground, rect, tint);
        Panel(spriteBatch, PanelBackground, Inset(rect, 2, 2, 4, Math.Max(8, rect.Height / 2)), tint * 0.24f);
        Panel(spriteBatch, PanelBackground, Inset(rect, 4, 3, 8, Math.Max(5, rect.Height / 4)), Color.White * (0.035f + style.Gloss * 0.03f));

        int shadeHeight = Math.Max(5, rect.Height / 4);
        Panel(spriteBatch, PanelBackground, new Rectangle(rect.X + 3, rect.Bottom - shadeHeight - 2, rect.Width - 6, shadeHeight), Color.Black * 0.08f);
    }

    private static void DrawBorder(SpriteBatch spriteBatch, Rectangle rect, GlassPanelStyle style)
    {
        Panel(spriteBatch, PanelBorder, rect, style.Border * (0.34f + style.BorderStrength * 0.18f));
        Panel(spriteBatch, PanelBorder, Inset(rect, 2, 2, 4, rect.Height - 4), Color.White * 0.07f);
        Panel(spriteBatch, PanelBackground, Inset(rect, 3, 3, 6, Math.Min(10, rect.Height / 3)), Color.White * 0.035f);
    }

    private static void DrawShadow(SpriteBatch spriteBatch, Rectangle rect)
    {
        Texture2D pixel = TextureAssets.MagicPixel.Value;

        spriteBatch.Draw(pixel, new Rectangle(rect.X + 5, rect.Y + 7, rect.Width, rect.Height), new Color(0, 58, 38) * 0.06f);
        spriteBatch.Draw(pixel, new Rectangle(rect.X + 2, rect.Y + 3, rect.Width + 5, rect.Height + 4), new Color(28, 176, 118) * 0.035f);
    }

    private static void Panel(SpriteBatch spriteBatch, Texture2D texture, Rectangle rect, Color color)
    {
        if (rect.Width <= Slice * 2 || rect.Height <= 0)
            return;

        Utils.DrawSplicedPanel(spriteBatch, texture, rect.X, rect.Y, rect.Width, rect.Height, Slice, Slice, Slice, Slice, color);
    }

    private static Rectangle Inset(Rectangle rect, int x, int y, int width, int height) => new(rect.X + x, rect.Y + y, rect.Width - width, height);
}
