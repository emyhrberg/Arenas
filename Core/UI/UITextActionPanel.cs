using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;

namespace Arenas.Core.UI;

public sealed class UITextActionPanel : UITextPanel<string>
{
    private readonly Action action;
    private readonly Texture2D icon;

    public UITextActionPanel(string text, Action action, float height, float textScale = 0.8f, bool large = false, Texture2D icon = null)
        : base(text, textScale, large)
    {
        this.action = action;
        this.icon = icon;

        Height.Set(height, 0f);
        Width.Set(0f, 1f);
        SetPadding(6f);
        BackgroundColor = new Color(63, 82, 151) * 0.8f;
        BorderColor = Color.Black;
    }

    public override void MouseOver(Terraria.UI.UIMouseEvent evt)
    {
        base.MouseOver(evt);
        BorderColor = Color.Yellow;
    }

    public override void MouseOut(Terraria.UI.UIMouseEvent evt)
    {
        base.MouseOut(evt);
        BorderColor = Color.Black;
    }

    public override void LeftClick(Terraria.UI.UIMouseEvent evt)
    {
        base.LeftClick(evt);
        SoundEngine.PlaySound(SoundID.MenuTick);
        action?.Invoke();
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        base.DrawSelf(spriteBatch);

        if (icon == null)
            return;

        var dims = GetDimensions();
        const float size = 24f;
        var rect = new Rectangle((int)dims.X + 10, (int)(dims.Y + (dims.Height - size) * 0.5f), (int)size, (int)size);
        spriteBatch.Draw(icon, rect, Color.White);
    }
}
