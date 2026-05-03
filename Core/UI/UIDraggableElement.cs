using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.UI;

namespace Arenas.Core.UI;

public sealed class UIDraggableElement : UIElement
{
    private bool dragging;
    private Vector2 dragOffset;

    public void BeginDrag(UIMouseEvent evt)
    {
        dragging = true;
        dragOffset = evt.MousePosition - GetDimensions().Position();
    }

    public void EndDrag(UIMouseEvent evt)
    {
        dragging = false;
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (IsMouseHovering)
            Main.LocalPlayer.mouseInterface = true;

        if (!dragging || Parent == null)
            return;

        if (!Main.mouseLeft)
        {
            dragging = false;
            return;
        }

        var parent = Parent.GetDimensions();
        var dims = GetDimensions();

        Left.Pixels = Main.mouseX - dragOffset.X - parent.X;
        Top.Pixels = Main.mouseY - dragOffset.Y - parent.Y;
        Left.Pixels = Utils.Clamp(Left.Pixels, 0f, Math.Max(0f, parent.Width - dims.Width));
        Top.Pixels = Utils.Clamp(Top.Pixels, 0f, Math.Max(0f, parent.Height - dims.Height));
        Recalculate();
    }
}
