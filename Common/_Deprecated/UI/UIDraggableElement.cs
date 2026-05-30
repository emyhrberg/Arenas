//using Microsoft.Xna.Framework;
//using System;
//using Terraria;
//using Terraria.UI;

//namespace Arenas.Common.UI;

//public sealed class UIDraggableElement : UIElement
//{
//    private bool dragging;
//    private Vector2 dragOffset;

//    public void BeginDrag(UIMouseEvent evt)
//    {
//        if (Parent == null)
//            return;

//        ConvertLayoutToAbsolutePixels();
//        dragging = true;
//        dragOffset = Main.MouseScreen - GetDimensions().Position();
//    }

//    public void EndDrag(UIMouseEvent evt)
//    {
//        dragging = false;
//    }

//    public override void Update(GameTime gameTime)
//    {
//        base.Update(gameTime);

//        if (IsMouseHovering)
//            Main.LocalPlayer.mouseInterface = true;

//        if (!dragging || Parent == null)
//            return;

//        if (!Main.mouseLeft)
//        {
//            dragging = false;
//            return;
//        }

//        var parent = Parent.GetDimensions();
//        var dims = GetDimensions();

//        Vector2 mouse = Main.MouseScreen;
//        Left.Pixels = mouse.X - dragOffset.X - parent.X;
//        Top.Pixels = mouse.Y - dragOffset.Y - parent.Y;
//        Left.Pixels = Utils.Clamp(Left.Pixels, 0f, Math.Max(0f, parent.Width - dims.Width));
//        Top.Pixels = Utils.Clamp(Top.Pixels, 0f, Math.Max(0f, parent.Height - dims.Height));
//        Recalculate();
//    }

//    private void ConvertLayoutToAbsolutePixels()
//    {
//        var parent = Parent.GetDimensions();
//        var dims = GetDimensions();

//        HAlign = 0f;
//        VAlign = 0f;
//        Left.Percent = 0f;
//        Top.Percent = 0f;
//        Left.Pixels = dims.X - parent.X;
//        Top.Pixels = dims.Y - parent.Y;

//        Recalculate();
//    }
//}
