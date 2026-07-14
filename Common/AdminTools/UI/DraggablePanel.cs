using Arenas.Common.AdminTools.UI.Drawers;
using Arenas.Core;
using System;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;

namespace Arenas.Common.AdminTools.UI;

public abstract class DraggablePanel : UIElement
{
    private readonly Func<string> hotkeyText;
    private readonly UIText hotkeyLabel;
    private string lastHotkeyText;
    private bool dragging;
    private Vector2 dragOffset;

    protected UIPanel TitlePanel;
    protected UIPanel ContentPanel;
    protected UIPanel RefreshPanel;
    protected UIPanel ClosePanel;
    protected ResizeButton ResizeButton;

    protected abstract void OnClosePanelLeftClick();
    protected virtual void OnRefreshPanelLeftClick() { }

    protected virtual float MinResizeWidth => 350f;
    protected virtual float MinResizeHeight => 210f;
    protected virtual float MaxResizeWidth => 1000f;
    protected virtual float MaxResizeHeight => 1000f;
    protected virtual bool CanResizeX => true;
    protected virtual bool CanResizeY => true;

    public DraggablePanel(string title, Func<string> hotkeyText = null)
    {
        this.hotkeyText = hotkeyText;

        Width.Set(350, 0f);
        Height.Set(460, 0f);
        VAlign = 0.7f;
        HAlign = 0.9f;
        SetPadding(0f);

        TitlePanel = new GlassUIPanel(GlassPanelDrawer.PanelHeader);
        TitlePanel.Height.Set(40f, 0f);
        TitlePanel.Width.Set(0f, 1f);
        TitlePanel.SetPadding(0f);

        UIText titleText = new(title, large: true, textScale: 0.7f)
        {
            HAlign = 0.5f,
            VAlign = 0.5f
        };
        TitlePanel.Append(titleText);

        ContentPanel = new GlassUIPanel(GlassPanelDrawer.PanelBody)
        {
            Top = new StyleDimension(40f, 0f),
            Width = new StyleDimension(0f, 1f),
            Height = new StyleDimension(420f, 0f),
            OverflowHidden = true
        };
        ContentPanel.SetPadding(0f);
        Append(ContentPanel);

        GlassUIPanel closePanel = new(GlassPanelDrawer.PanelButton);
        ClosePanel = closePanel;
        ClosePanel.Height = new StyleDimension(0f, 1f);
        ClosePanel.Width = new StyleDimension(40f, 0f);
        ClosePanel.HAlign = 1f;
        ClosePanel.VAlign = 0.5f;
        ClosePanel.OnLeftClick += (_, _) => OnClosePanelLeftClick();
        ClosePanel.OnMouseOver += (_, _) => closePanel.Style = GlassPanelDrawer.PanelButtonHover;
        ClosePanel.OnMouseOut += (_, _) => closePanel.Style = GlassPanelDrawer.PanelButton;
        ClosePanel.Append(new UIText("X", large: true, textScale: 0.55f)
        {
            HAlign = 0.5f,
            VAlign = 0.5f
        });
        TitlePanel.Append(ClosePanel);

        if (hotkeyText != null)
        {
            hotkeyLabel = new UIText("", textScale: 0.52f)
            {
                HAlign = 1f,
                VAlign = 0.5f,
                Left = { Pixels = -46f },
                TextColor = new Color(210, 226, 255) * 0.8f
            };
            TitlePanel.Append(hotkeyLabel);
            UpdateHotkeyText();
        }

        Append(TitlePanel);

        GlassUIPanel refreshPanel = new(GlassPanelDrawer.PanelButton);
        RefreshPanel = refreshPanel;
        RefreshPanel.Height = new StyleDimension(0f, 1f);
        RefreshPanel.Width = new StyleDimension(40f, 0f);
        RefreshPanel.VAlign = 0.5f;
        RefreshPanel.OnLeftClick += (_, _) => OnRefreshPanelLeftClick();
        RefreshPanel.OnMouseOver += (_, _) => refreshPanel.Style = GlassPanelDrawer.PanelButtonHover;
        RefreshPanel.OnMouseOut += (_, _) => refreshPanel.Style = GlassPanelDrawer.PanelButton;
        RefreshPanel.SetPadding(0f);

        if (Ass.IconRefresh?.Value != null)
        {
            RefreshPanel.Append(new UIImage(Ass.IconRefresh.Value)
            {
                HAlign = 0.5f,
                VAlign = 0.5f
            });
        }

        TitlePanel.Append(RefreshPanel);

        ResizeButton = Ass.IconResize != null ? new ResizeButton(Ass.IconResize) : new ResizeButton(TextureAssets.MagicPixel);
        ResizeButton.OnDragX += ResizeX;
        ResizeButton.OnDragY += ResizeY;
        Append(ResizeButton);
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        GlassPanelDrawer.Draw(spriteBatch, GetDimensions().ToRectangle(), GlassPanelDrawer.PanelShell);
    }

    public override void Recalculate()
    {
        base.Recalculate();

        TitlePanel?.Top.Set(0f, 0f);
        TitlePanel?.Width.Set(0f, 1f);
        TitlePanel?.Height.Set(40f, 0f);

        if (ContentPanel != null)
        {
            ContentPanel.Top.Set(40f, 0f);
            ContentPanel.Left.Set(0f, 0f);
            ContentPanel.Width.Set(0f, 1f);
            ContentPanel.Height.Set(Height.Pixels - 40f, 0f);
        }

        if (ResizeButton != null)
        {
            ResizeButton.HAlign = 1f;
            ResizeButton.VAlign = 1f;
            ResizeButton.Left.Set(-2f, 0f);
            ResizeButton.Top.Set(-2f, 0f);
            ResizeButton.Width.Set(20f, 0f);
            ResizeButton.Height.Set(20f, 0f);
        }
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (IsMouseHovering)
            Main.LocalPlayer.mouseInterface = true;

        UpdateHotkeyText();

        if (RefreshPanel?.IsMouseHovering == true)
            Main.instance.MouseText("Refresh");

        if (Parent == null)
            return;

        if (IsChromeHovering())
            return;

        if (dragging)
        {
            CalculatedStyle parent = Parent.GetDimensions();

            Left.Pixels = Main.mouseX - dragOffset.X - parent.X;
            Top.Pixels = Main.mouseY - dragOffset.Y - parent.Y;

            CalculatedStyle dims = GetDimensions();
            Left.Pixels = Utils.Clamp(Left.Pixels, 0f, Math.Max(0f, parent.Width - dims.Width));
            Top.Pixels = Utils.Clamp(Top.Pixels, 0f, Math.Max(0f, parent.Height - dims.Height));
            Recalculate();
        }
    }

    public override void LeftMouseDown(UIMouseEvent evt)
    {
        base.LeftMouseDown(evt);

        if (IsChromeHovering())
            return;

        UIElement parent = Parent;
        if (TitlePanel == null || !TitlePanel.ContainsPoint(evt.MousePosition) || parent == null)
            return;

        parent.RemoveChild(this);
        parent.Append(this);
        ConvertAlignmentToPixels();

        dragging = true;
        dragOffset = evt.MousePosition - GetDimensions().Position();
    }

    public override void LeftMouseUp(UIMouseEvent evt)
    {
        base.LeftMouseUp(evt);
        dragging = false;
    }

    private void ResizeX(float dx)
    {
        if (!CanResizeX || Parent == null)
            return;

        ConvertAlignmentToPixels();

        CalculatedStyle parent = Parent.GetDimensions();
        float maxWidth = Math.Min(MaxResizeWidth, parent.Width - Left.Pixels);
        float width = Utils.Clamp(Width.Pixels + dx, MinResizeWidth, maxWidth);
        Width.Set(width, 0f);
        Recalculate();
    }

    private void ResizeY(float dy)
    {
        if (!CanResizeY || Parent == null)
            return;

        ConvertAlignmentToPixels();

        CalculatedStyle parent = Parent.GetDimensions();
        float maxHeight = Math.Min(MaxResizeHeight, parent.Height - Top.Pixels);
        float height = Utils.Clamp(Height.Pixels + dy, MinResizeHeight, Math.Max(MinResizeHeight, maxHeight));
        Height.Set(height, 0f);
        Recalculate();
    }

    private void ConvertAlignmentToPixels()
    {
        if (Parent == null)
            return;

        if (HAlign == 0f && VAlign == 0f && Left.Percent == 0f && Top.Percent == 0f)
            return;

        CalculatedStyle parent = Parent.GetDimensions();
        CalculatedStyle dims = GetDimensions();

        HAlign = 0f;
        VAlign = 0f;
        Left.Percent = 0f;
        Top.Percent = 0f;
        Left.Pixels = dims.X - parent.X;
        Top.Pixels = dims.Y - parent.Y;
        Recalculate();
    }

    private void UpdateHotkeyText()
    {
        if (hotkeyLabel == null)
            return;

        string text = hotkeyText?.Invoke();
        if (string.IsNullOrWhiteSpace(text))
            text = "None";

        if (text == lastHotkeyText)
            return;

        lastHotkeyText = text;
        hotkeyLabel.SetText(text, 0.52f, false);
    }

    private bool IsChromeHovering()
    {
        return ClosePanel?.IsMouseHovering == true || RefreshPanel?.IsMouseHovering == true || ResizeButton?.IsMouseHovering == true;
    }
}

public class GlassUIPanel : UIPanel
{
    public GlassPanelStyle Style;
    private readonly bool drawShadow;

    public GlassUIPanel(GlassPanelStyle style, bool drawShadow = false)
    {
        Style = style;
        this.drawShadow = drawShadow;
        BackgroundColor = Color.Transparent;
        BorderColor = Color.Transparent;
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        Rectangle rect = GetDimensions().ToRectangle();

        if (drawShadow)
            GlassPanelDrawer.Draw(spriteBatch, rect, Style);
        else
            GlassPanelDrawer.DrawSpliced(spriteBatch, rect, Style);
    }
}
