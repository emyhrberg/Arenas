using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria.Audio;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.UI;

namespace PvPArenas.Common.AdminTools.UI;

internal sealed class UILabeledSlider : UIElement
{
    private readonly string label;
    private readonly float step, buttonStep;
    private readonly Func<float, string> format;
    private readonly Action<float> changed;
    private readonly Asset<Texture2D> icon;
    private readonly UIText text;
    private float value = float.NaN;

    public UISlider Slider { get; }
    public float Min { get; }
    public float Max { get; }
    public float Value => value;
    public bool IsHeld => Slider.IsHeld;
    public Action<float> OnRelease { get; set; }

    public bool Enabled
    {
        get => Slider.Enabled;
        set { Slider.Enabled = value; text.TextColor = value ? Color.Gray : Color.DimGray; }
    }

    public UILabeledSlider(string label, float min, float max, float defaultValue, float step = .01f, Action<float> onValueChanged = null, Func<float, string> format = null, Asset<Texture2D> icon = null, float buttonStep = 0f)
    {
        this.label = label; Min = min; Max = max; this.step = Math.Max(step, .0001f); this.buttonStep = buttonStep > 0f ? buttonStep : this.step; changed = onValueChanged; this.format = format; this.icon = icon ?? Ass.IconArenas;
        Width.Set(0, 1f); Height.Set(32, 0);

        text = new("", .8f) { Left = { Pixels = 30 }, VAlign = .5f, TextOriginX = 0f, TextOriginY = .5f, TextColor = Color.Gray };
        text.Width.Set(-44, .52f); Append(text);

        Slider = new() { Left = { Percent = .59f }, Width = { Percent = .32f }, VAlign = .5f };
        Slider.OnDrag += ratio => Apply(Min + ratio * (Max - Min), true);
        Slider.OnRelease += ratio => { Apply(Min + ratio * (Max - Min), true); OnRelease?.Invoke(value); };
        Append(new UIPlusMinusButton("-", () => Step(-1), () => Enabled && CanStep(-1)) { Left = { Percent = .53f }, VAlign = .5f });
        Append(Slider);
        Append(new UIPlusMinusButton("+", () => Step(1), () => Enabled && CanStep(1)) { Left = { Percent = 1f, Pixels = -22 }, VAlign = .5f });
        SetValue(defaultValue);
    }

    public void SetValue(float next) => Apply(next, false);

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        if (IsMouseHovering) Main.LocalPlayer.mouseInterface = true;
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        Texture2D texture = icon?.Value ?? Ass.IconArenas?.Value;
        if (texture == null) return;
        Rectangle rect = GetDimensions().ToRectangle();
        float scale = Math.Min(22f / texture.Width, 22f / texture.Height);
        spriteBatch.Draw(texture, new Vector2(rect.X + 2, rect.Center.Y - texture.Height * scale * .5f), null, Enabled ? Color.White : Color.Gray, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private void Apply(float raw, bool notify)
    {
        float next = MathHelper.Clamp((float)Math.Round((raw - Min) / step) * step + Min, Min, Max);
        Slider.Ratio = Max <= Min ? 0f : (next - Min) / (Max - Min);
        if (Math.Abs(value - next) <= float.Epsilon) return;
        value = next; text.SetText($"{label}: {(format?.Invoke(value) ?? value.ToString("0.##"))}");
        if (notify) changed?.Invoke(value);
    }

    private void Step(int direction)
    {
        float previous = value;
        Apply(value + buttonStep * direction, true);
        if (Math.Abs(previous - value) <= float.Epsilon) return;
        SoundEngine.PlaySound(SoundID.MenuTick);
        OnRelease?.Invoke(value);
    }

    private bool CanStep(int direction) => direction < 0 ? value > Min + .0001f : value < Max - .0001f;
}
