using Arenas.Common.AdminTools.GameManager;
using Arenas.Common.Spawnbox;
using Arenas.Common.UI;
using Arenas.Core;
using System;
using Terraria.Audio;
using Terraria.Enums;
using Terraria.ID;
using Terraria.Localization;
using Terraria.UI;

namespace Arenas.Common.AdminTools.SpawnBoxTool;

internal sealed class SpawnBoxPanel : UIDraggablePanel
{
    private readonly SpawnBoxTeamTabs tabs;
    private readonly UISliderElement width, height, thickness, xOffset, yOffset;
    private Team team = Team.Red;

    protected override float MinResizeW => 400f;
    protected override float MinResizeH => 350f;
    protected override bool ShowResizeButton => false;

    public SpawnBoxPanel() : base(Language.GetTextValue("Mods.Arenas.Tools.SpawnBoxToolPanel.Title"))
    {
        Width.Set(420, 0); Height.Set(350, 0); HAlign = .5f; VAlign = 0f; Top.Set(200, 0); ContentPanel.SetPadding(6);
        tabs = new(ChangeTeam); Add(tabs, 0); Add(new SpawnBoxStatusRow(() => team), 40);
        width = Slider("Width", SpawnBoxSettings.MinSize, SpawnBoxSettings.MaxSize); Add(width, 80);
        height = Slider("Height", SpawnBoxSettings.MinSize, SpawnBoxSettings.MaxSize); Add(height, 116);
        thickness = Slider("Border", SpawnBoxSettings.MinThickness, SpawnBoxSettings.MaxThickness); Add(thickness, 152);
        xOffset = Slider("X Offset", SpawnBoxSettings.MinOffset, SpawnBoxSettings.MaxOffset); Add(xOffset, 188);
        yOffset = Slider("Y Offset", SpawnBoxSettings.MinOffset, SpawnBoxSettings.MaxOffset); Add(yOffset, 224);
        Add(new ArenaManagerButton(() => $"Reset {Name} Spawnbox", Ass.IconRefresh, Reset, () => true, () => $"Restore the default {Name.ToLowerInvariant()} team spawnbox size and position."), 264);
        RefreshValues();
    }

    protected override void OnClosePanelLeftClick() => ModContent.GetInstance<SpawnBoxToolUISystem>().Hide();
    protected override void OnRefreshPanelLeftClick() => RefreshValues();

    private string Name => team == Team.Red ? "Red" : "Green";
    private UISliderElement Slider(string label, int min, int max)
    {
        UISliderElement slider = new(label, min, max, min, 1, format: v => $"{v:0} tiles", icon: Ass.IconArenas, buttonStep: 1);
        slider.OnRelease = _ => Commit();
        return slider;
    }

    private void ChangeTeam(Team next) { team = next; tabs.Selected = next; RefreshValues(); }
    private void RefreshValues() => SetValues(ModContent.GetInstance<SpawnBoxSystem>().GetSettings(team));
    private void SetValues(SpawnBoxSettings s)
    {
        width.SetValue(s.Width); height.SetValue(s.Height); thickness.SetValue(s.Thickness); xOffset.SetValue(s.XOffset); yOffset.SetValue(s.YOffset);
    }
    private void Commit() => SpawnBoxNetHandler.SendSet(team, new((int)width.Value, (int)height.Value, (int)xOffset.Value, (int)yOffset.Value, (int)thickness.Value));
    private void Reset() { SetValues(SpawnBoxSystem.DefaultSettings); Commit(); }
    private void Add(UIElement element, float top) { element.Top.Set(top, 0); ContentPanel.Append(element); }
}

internal sealed class SpawnBoxStatusRow : UIElement
{
    private readonly Func<Team> selectedTeam;

    public SpawnBoxStatusRow(Func<Team> selectedTeam) { this.selectedTeam = selectedTeam; Width.Set(0, 1f); Height.Set(34, 0); }

    protected override void DrawSelf(SpriteBatch batch)
    {
        Rectangle rect = GetDimensions().ToRectangle(); Team team = selectedTeam(); SpawnBoxSystem box = ModContent.GetInstance<SpawnBoxSystem>();
        Rectangle area = box.GetTileArea(team); string name = team == Team.Red ? "Red" : "Green";
        ArenaGameManagerText.Panel(batch, rect, new Color(20, 20, 60) * .9f, Color.Black);
        ArenaGameManagerText.Icon(batch, Ass.IconArenas, new(rect.X + 8, rect.Y + 7, 20, 20), Color.White);
        string status = box.Active ? $"Status: {name} spawnbox | {area.Width} x {area.Height} tiles | X {area.Center.X}, Y {area.Center.Y}" : $"Status: {name} spawnbox | Outside Arenas";
        ArenaGameManagerText.Draw(batch, status, new(rect.X + 36, rect.Y + 9), Main.teamColor[(int)team], .68f, rect.Width - 46);
    }
}

internal sealed class SpawnBoxTeamTabs : UIElement
{
    private readonly Action<Team> changed;
    public Team Selected { get; set; } = Team.Red;

    public SpawnBoxTeamTabs(Action<Team> changed) { this.changed = changed; Width.Set(0, 1f); Height.Set(34, 0); }

    public override void LeftClick(UIMouseEvent evt)
    {
        base.LeftClick(evt); Rectangle rect = GetDimensions().ToRectangle(); Team next = Button(rect, Team.Red).Contains(evt.MousePosition.ToPoint()) ? Team.Red : Team.Green;
        if (next == Selected) return; Selected = next; SoundEngine.PlaySound(SoundID.MenuTick); changed(next);
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime); if (!IsMouseHovering) return; Main.LocalPlayer.mouseInterface = true;
        Team team = Button(GetDimensions().ToRectangle(), Team.Red).Contains(Main.MouseScreen.ToPoint()) ? Team.Red : Team.Green;
        Main.instance.MouseText($"Edit {(team == Team.Red ? "Red" : "Green")} team spawnbox");
    }

    protected override void DrawSelf(SpriteBatch batch)
    {
        Rectangle rect = GetDimensions().ToRectangle(); Draw(batch, Button(rect, Team.Red), Team.Red); Draw(batch, Button(rect, Team.Green), Team.Green);
    }

    private void Draw(SpriteBatch batch, Rectangle rect, Team team)
    {
        bool selected = team == Selected, hover = rect.Contains(Main.MouseScreen.ToPoint()); Color color = Main.teamColor[(int)team];
        ArenaGameManagerText.Panel(batch, rect, selected ? color * .55f : new Color(20, 20, 60) * .9f, hover ? Color.Yellow : selected ? color : Color.Black);
        ArenaGameManagerText.Draw(batch, $"{(team == Team.Red ? "Red" : "Green")} Team Spawnbox", rect.Center.ToVector2() + new Vector2(0, -8), Color.White, .7f, rect.Width - 12, .5f);
    }

    private static Rectangle Button(Rectangle rect, Team team)
    {
        int width = (rect.Width - 6) / 2;
        return team == Team.Red ? new(rect.X, rect.Y, width, rect.Height) : new(rect.X + width + 6, rect.Y, rect.Width - width - 6, rect.Height);
    }
}
