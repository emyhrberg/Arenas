using Arenas.Core;
using ReLogic.Content;
using System;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;

namespace Arenas.Common.AdminTools.UI;

public class ResizeButton : UIImageButton
{
    private Vector2 lastMouse;
    private bool draggingResize;

    public event Action<float> OnDragX;
    public event Action<float> OnDragY;

    public ResizeButton(Asset<Texture2D> texture) : base(texture)
    {
        HAlign = 1f;
        VAlign = 1f;
        Left.Set(-2f, 0f);
        Top.Set(-2f, 0f);
        Width.Set(20f, 0f);
        Height.Set(20f, 0f);
    }

    public override void LeftMouseDown(UIMouseEvent evt)
    {
        base.LeftMouseDown(evt);

        draggingResize = true;
        lastMouse = Main.MouseScreen;
    }

    public override void LeftMouseUp(UIMouseEvent evt)
    {
        base.LeftMouseUp(evt);
        draggingResize = false;
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (!draggingResize)
            return;

        if (!Main.mouseLeft)
        {
            draggingResize = false;
            return;
        }

        Vector2 mouse = Main.MouseScreen;
        Vector2 delta = mouse - lastMouse;
        lastMouse = mouse;

        if (delta.X != 0f)
            OnDragX?.Invoke(delta.X);

        if (delta.Y != 0f)
            OnDragY?.Invoke(delta.Y);
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        spriteBatch.Draw(Ass.IconResize.Value, GetDimensions().ToRectangle(), Color.White);

        if (IsMouseHovering)
            Main.instance.MouseText("Resize");
    }
}
