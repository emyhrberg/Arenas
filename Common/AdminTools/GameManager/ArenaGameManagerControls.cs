using Arenas.Common.Rounds;
using Arenas.Core;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using SubworldLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria.Audio;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.UI;

namespace Arenas.Common.AdminTools.GameManager;

internal static class ArenaGameManagerText
{
    private static Texture2D PanelBackground => Main.Assets.Request<Texture2D>("Images/UI/PanelBackground").Value;
    private static Texture2D PanelBorder => Main.Assets.Request<Texture2D>("Images/UI/PanelBorder").Value;

    public static void Draw(SpriteBatch batch, string text, Vector2 position, Color color, float scale, float width, float anchor = 0f)
    {
        float measured = FontAssets.MouseText.Value.MeasureString(text).X * scale;
        if (measured > width) scale *= width / measured;
        Utils.DrawBorderString(batch, text, position, color, scale, anchor);
    }

    public static void Icon(SpriteBatch batch, Asset<Texture2D> asset, Rectangle box, Color color)
    {
        Texture2D texture = (asset ?? Ass.IconArenas)?.Value; if (texture == null) return;
        float scale = Math.Min(box.Width / (float)texture.Width, box.Height / (float)texture.Height);
        batch.Draw(texture, box.Center.ToVector2(), null, color, 0f, texture.Size() * .5f, scale, SpriteEffects.None, 0f);
    }

    public static string Time(int seconds) => $"{Math.Max(0, seconds) / 60:00}:{Math.Max(0, seconds) % 60:00}";
    public static string Phase(RoundPhase phase) => phase == RoundPhase.FreezeCountdown ? "Freeze Countdown" : phase.ToString();
    public static string Result(RoundResult result) => result switch { RoundResult.BossDefeated => "Boss Defeated", RoundResult.TimeExpired => "Time Expired", RoundResult.BossDespawned => "Boss Despawned", RoundResult.SpawnFailed => "Spawn Failed", RoundResult.AdminEnded => "Ended by Admin", _ => "" };

    public static void Panel(SpriteBatch batch, Rectangle rect, Color background, Color border)
    {
        Utils.DrawSplicedPanel(batch, PanelBackground, rect.X, rect.Y, rect.Width, rect.Height, 10, 10, 10, 10, background);
        Utils.DrawSplicedPanel(batch, PanelBorder, rect.X, rect.Y, rect.Width, rect.Height, 10, 10, 10, 10, border);
    }
}

internal sealed class ArenaManagerStatusRow : UIElement
{
    public ArenaManagerStatusRow() { Width.Set(0, 1f); Height.Set(34, 0); }

    protected override void DrawSelf(SpriteBatch batch)
    {
        Rectangle rect = GetDimensions().ToRectangle(); ArenaGameManagerText.Panel(batch, rect, new Color(20, 20, 60) * .9f, Color.Black);
        bool active = SubworldSystem.IsActive<ArenasSubworld>();
        string status = !active ? "Outside Arenas" : ArenaRoundSystem.IsAutoStartHeld ? "Idle - automatic start held" : ArenaRoundSystem.IsTimerPaused ? $"{ArenaGameManagerText.Phase(ArenaRoundSystem.Phase)} - paused" : ArenaRoundSystem.Phase == RoundPhase.Voting && ArenaRoundSystem.Result != RoundResult.None ? $"Voting - {ArenaGameManagerText.Result(ArenaRoundSystem.Result)}" : ArenaGameManagerText.Phase(ArenaRoundSystem.Phase);
        Color color = !active ? Color.Gray : ArenaRoundSystem.IsTimerPaused ? Color.Yellow : ArenaRoundSystem.Phase == RoundPhase.Playing ? Color.LimeGreen : Color.White;
        ArenaGameManagerText.Icon(batch, Ass.IconArenas, new(rect.X + 8, rect.Y + 7, 20, 20), Color.White);
        ArenaGameManagerText.Draw(batch, $"Status: {status}", new(rect.X + 36, rect.Y + 9), color, .72f, rect.Width - 126);
        ArenaGameManagerText.Draw(batch, $"Time: {ArenaGameManagerText.Time((int)Math.Ceiling(ArenaRoundSystem.RemainingTicks / 60f))}", new(rect.Right - 10, rect.Y + 9), Color.White, .68f, 84, 1f);
    }
}

internal sealed class ArenaManagerPlayerRow : UIElement
{
    public ArenaManagerPlayerRow() { Width.Set(0, 1f); Height.Set(34, 0); }

    protected override void DrawSelf(SpriteBatch batch)
    {
        Rectangle rect = GetDimensions().ToRectangle(); ArenaGameManagerText.Panel(batch, rect, new Color(10, 10, 10) * .65f, Color.Black);
        int red = Main.player.Count(p => p?.active == true && (Team)p.team == Team.Red), blue = Main.player.Count(p => p?.active == true && (Team)p.team == Team.Blue);
        ArenaGameManagerText.Icon(batch, Ass.IconArenas, new(rect.X + 8, rect.Y + 7, 20, 20), Color.White);
        ArenaGameManagerText.Draw(batch, $"Player Count: {red + blue}", new(rect.X + 36, rect.Y + 9), new Color(174, 216, 226), .66f, 112);
        ArenaGameManagerText.Draw(batch, $"Red: {red}", new(rect.X + 156, rect.Y + 9), new Color(255, 120, 126), .68f, 64);
        ArenaGameManagerText.Draw(batch, $"Blue: {blue}", new(rect.X + 226, rect.Y + 9), new Color(116, 188, 255), .68f, 70);
    }
}

internal sealed class ArenaManagerPresetSelector : UIElement
{
    private int index; private readonly Action changed;
    public int Index => index;
    public ArenaManagerPresetSelector(Action changed) { this.changed = changed; Width.Set(0, 1f); Height.Set(48, 0); }
    public void SetIndex(int value) { int count = ArenaRoundSystem.GetValidPresets().Count; index = count == 0 ? 0 : Math.Clamp(value, 0, count - 1); }

    public override void LeftClick(UIMouseEvent evt)
    {
        base.LeftClick(evt); List<Core.Configs.ConfigElements.BossFightPreset> presets = ArenaRoundSystem.GetValidPresets(); if (presets.Count == 0) return;
        Rectangle rect = GetDimensions().ToRectangle(); Point mouse = evt.MousePosition.ToPoint();
        if (Previous(rect).Contains(mouse)) index = (index - 1 + presets.Count) % presets.Count;
        else if (Next(rect).Contains(mouse)) index = (index + 1) % presets.Count; else return;
        SoundEngine.PlaySound(SoundID.MenuTick); changed?.Invoke();
    }

    public override void Update(GameTime time)
    {
        base.Update(time); if (!IsMouseHovering) return; Main.LocalPlayer.mouseInterface = true;
        Rectangle rect = GetDimensions().ToRectangle(); Point mouse = Main.MouseScreen.ToPoint();
        if (Previous(rect).Contains(mouse)) Main.instance.MouseText("Previous boss preset"); else if (Next(rect).Contains(mouse)) Main.instance.MouseText("Next boss preset");
    }

    protected override void DrawSelf(SpriteBatch batch)
    {
        Rectangle rect = GetDimensions().ToRectangle(); ArenaGameManagerText.Panel(batch, rect, new Color(20, 20, 60) * .9f, Color.Black); DrawArrow(batch, Previous(rect), "<"); DrawArrow(batch, Next(rect), ">");
        List<Core.Configs.ConfigElements.BossFightPreset> presets = ArenaRoundSystem.GetValidPresets();
        if (presets.Count == 0) { ArenaGameManagerText.Draw(batch, "No valid fight presets", rect.Center.ToVector2() + new Vector2(0, -8), Color.Gray, .76f, rect.Width - 100, .5f); return; }
        SetIndex(index); var preset = presets[index]; ArenaBossVoteDrawer.DrawBossHead(preset.Boss?.Type ?? 0, new(rect.X + 48, rect.Y + 4, 40, 40));
        string label = index == ArenaRoundSystem.CurrentPresetIndex ? "Current boss" : "Selected boss";
        ArenaGameManagerText.Draw(batch, label, new(rect.X + 94, rect.Y + 6), new Color(174, 216, 226), .55f, rect.Width - 145);
        ArenaGameManagerText.Draw(batch, ArenaRoundSystem.PresetName(preset), new(rect.X + 94, rect.Y + 23), Color.White, .78f, rect.Width - 145);
    }

    private static Rectangle Previous(Rectangle r) => new(r.X + 6, r.Y + 7, 32, 34);
    private static Rectangle Next(Rectangle r) => new(r.Right - 38, r.Y + 7, 32, 34);
    private static void DrawArrow(SpriteBatch batch, Rectangle rect, string text)
    {
        bool hover = rect.Contains(Main.MouseScreen.ToPoint());
        ArenaGameManagerText.Panel(batch, rect, hover ? new Color(73, 94, 171) : new Color(63, 82, 151), hover ? Color.Yellow : Color.Black);
        ArenaGameManagerText.Draw(batch, text, rect.Center.ToVector2() + new Vector2(0, -9), Color.White, .82f, rect.Width, .5f);
    }
}

internal sealed class ArenaManagerButton : UIElement
{
    private readonly Func<string> text, tooltip; private readonly Func<bool> enabled; private readonly Action action; private readonly Asset<Texture2D> icon; private readonly bool danger;
    public ArenaManagerButton(Func<string> text, Asset<Texture2D> icon, Action action, Func<bool> enabled, Func<string> tooltip, bool danger = false)
    { this.text = text; this.icon = icon ?? Ass.IconArenas; this.action = action; this.enabled = enabled; this.tooltip = tooltip; this.danger = danger; Height.Set(34, 0); Width.Set(-24, 1f); }
    public override void LeftClick(UIMouseEvent evt) { base.LeftClick(evt); if (enabled()) { SoundEngine.PlaySound(SoundID.MenuTick); action(); } }
    public override void Update(GameTime time) { base.Update(time); if (!IsMouseHovering) return; Main.LocalPlayer.mouseInterface = true; string tip = tooltip?.Invoke(); if (!string.IsNullOrWhiteSpace(tip)) Main.instance.MouseText(tip); }

    protected override void DrawSelf(SpriteBatch batch)
    {
        Rectangle rect = GetDimensions().ToRectangle(); bool active = enabled(), hover = active && IsMouseHovering;
        Color background = !active ? new Color(45, 45, 55) * .7f : danger ? hover ? new Color(170, 45, 60) : new Color(120, 35, 45) : hover ? new Color(73, 94, 171) : new Color(63, 82, 151);
        ArenaGameManagerText.Panel(batch, rect, background, hover ? Color.Yellow : Color.Black);
        ArenaGameManagerText.Icon(batch, icon, new(rect.X + 9, rect.Y + 7, 20, 20), active ? Color.White : Color.Gray);
        ArenaGameManagerText.Draw(batch, text(), new(rect.Center.X + 8, rect.Y + 9), active ? Color.White : Color.Gray, .72f, rect.Width - 58, .5f);
    }
}
