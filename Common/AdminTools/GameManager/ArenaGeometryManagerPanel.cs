using Arenas.Common.Generation;
using Arenas.Common.Rounds;
using Arenas.Common.UI;
using Arenas.Core;
using Arenas.Core.Configs.ConfigElements;
using System;
using Terraria.Localization;
using Terraria.UI;

namespace Arenas.Common.AdminTools.GameManager;

internal sealed class ArenaGeometryManagerPanel : UIDraggablePanel
{
    private enum Page : byte { World, Boss, Spawns }

    private readonly ArenaManagerPresetSelector preset;
    private ArenaGeometryConfig draft;
    private Page page;
    private int seenRevision;

    protected override float MinResizeW => 400f;
    protected override float MinResizeH => 520f;
    protected override float MaxResizeW => 540f;
    protected override float MaxResizeH => 660f;

    public ArenaGeometryManagerPanel() : base(Language.GetTextValue("Mods.Arenas.Tools.ArenaGeometryManagerPanel.Title"))
    {
        Width.Set(420, 0); Height.Set(520, 0); HAlign = .5f; Top.Set(170, 0); Left.Set(430, 0); ContentPanel.SetPadding(6);
        preset = new(OnPresetChanged);
        preset.SetIndex(ArenaRoundSystem.CurrentPresetIndex);
        LoadDraft();
    }

    protected override void OnClosePanelLeftClick() => ModContent.GetInstance<ArenaGameManagerUISystem>().Hide();
    protected override void OnRefreshPanelLeftClick() => LoadDraft();

    public override void Update(GameTime gameTime)
    {
        if (seenRevision != ArenaGameManagerNetHandler.GeometryRevision)
        {
            seenRevision = ArenaGameManagerNetHandler.GeometryRevision;
            LoadDraft();
        }
        base.Update(gameTime);
    }

    private void OnPresetChanged() => LoadDraft();

    private void LoadDraft()
    {
        BossFightPreset selected = ArenaRoundSystem.GetPresetOrDefault(preset.Index);
        draft = ArenaGeneratorRegistry.ResolveGeometry(selected);
        if (draft != null) draft.Enabled = true;
        BuildPage();
    }

    private void BuildPage()
    {
        ContentPanel.RemoveAllChildren();
        Add(preset, 0);
        AddButton(new(() => $"Page: {page}", Ass.IconRefresh, NextPage, () => Editable, () => "World size, boss area, or team spawns"), 54);
        if (!Editable || draft == null)
        {
            AddButton(new(() => "Sandbox uses Arenas_v10.wld", Ass.IconArenas, () => { }, () => false, () => "Bundled Sandbox world"), 104);
            return;
        }

        float top = 96;
        switch (page)
        {
            case Page.World:
                AddSlider("World Width", 700, 1600, () => draft.WorldWidth, SetWorldWidth, ref top, 2, rebuildOnRelease: true);
                AddSlider("World Height", 500, 1000, () => draft.WorldHeight, SetWorldHeight, ref top, rebuildOnRelease: true);
                AddSlider("Arena Left", 4, draft.WorldWidth - 100, () => draft.ArenaLeft, value => { draft.ArenaLeft = value; draft.ArenaRight = draft.WorldWidth - value; }, ref top);
                AddSlider("Arena Right", 100, draft.WorldWidth - 4, () => draft.ArenaRight, value => { draft.ArenaRight = value; draft.ArenaLeft = draft.WorldWidth - value; }, ref top);
                AddSlider("Arena Top", 4, draft.WorldHeight - 100, () => draft.ArenaTop, value => draft.ArenaTop = value, ref top);
                AddSlider("Arena Bottom", 100, draft.WorldHeight - 4, () => draft.ArenaBottom, value => draft.ArenaBottom = value, ref top);
                AddSlider("Outer Border", 1, 10, () => draft.OuterBorderThickness, value => draft.OuterBorderThickness = value, ref top);
                AddSlider("Team Line Width", 1, 10, () => draft.TeamBorderWidth, value => draft.TeamBorderWidth = value, ref top);
                break;
            case Page.Boss:
                AddSlider("Boss Area X", 4, draft.WorldWidth - 44, () => draft.BossAreaX, value => draft.BossAreaX = value, ref top);
                AddSlider("Boss Area Y", 4, draft.WorldHeight - 44, () => draft.BossAreaY, value => draft.BossAreaY = value, ref top);
                AddSlider("Boss Area Width", 40, Math.Min(1000, draft.WorldWidth - 8), () => draft.BossAreaWidth, value => draft.BossAreaWidth = value, ref top);
                AddSlider("Boss Area Height", 40, Math.Min(900, draft.WorldHeight - 8), () => draft.BossAreaHeight, value => draft.BossAreaHeight = value, ref top);
                AddSlider("Boss Spawn X", 4, draft.WorldWidth - 4, () => draft.BossSpawnX, value => draft.BossSpawnX = value, ref top);
                AddSlider("Boss Spawn Y", 4, draft.WorldHeight - 4, () => draft.BossSpawnY, value => draft.BossSpawnY = value, ref top);
                AddSlider("Blue Border X", 4, draft.WorldWidth - 4, () => draft.BlueBorderX, value => draft.BlueBorderX = value, ref top);
                AddSlider("Red Border X", 4, draft.WorldWidth - 4, () => draft.RedBorderX, value => draft.RedBorderX = value, ref top);
                break;
            default:
                AddSlider("Red Spawn X", 4, draft.WorldWidth - 4, () => draft.RedSpawnX, value => draft.RedSpawnX = value, ref top);
                AddSlider("Red Spawn Y", 4, draft.WorldHeight - 4, () => draft.RedSpawnY, value => draft.RedSpawnY = value, ref top);
                AddSlider("Blue Spawn X", 4, draft.WorldWidth - 4, () => draft.BlueSpawnX, value => draft.BlueSpawnX = value, ref top);
                AddSlider("Blue Spawn Y", 4, draft.WorldHeight - 4, () => draft.BlueSpawnY, value => draft.BlueSpawnY = value, ref top);
                AddSlider("Spawn Room Width", 10, 80, () => draft.SpawnRoomWidth, value => draft.SpawnRoomWidth = value, ref top);
                AddSlider("Spawn Room Height", 8, 50, () => draft.SpawnRoomHeight, value => draft.SpawnRoomHeight = value, ref top);
                AddButton(new(() => $"Auto Team Ground: {OnOff(draft.AutoPlaceTeamSpawns)}", Ass.IconArenas, () => { draft.AutoPlaceTeamSpawns = !draft.AutoPlaceTeamSpawns; BuildPage(); }, () => true, () => "Find team ground after terrain builds"), top); top += 38;
                AddButton(new(() => $"Auto Boss Spot: {OnOff(draft.AutoPlaceBossSpawn)}", Ass.IconArenas, () => { draft.AutoPlaceBossSpawn = !draft.AutoPlaceBossSpawn; BuildPage(); }, () => true, () => "Find a boss spot after terrain builds"), top); top += 38;
                break;
        }

        AddButton(new(() => "Generator Defaults", Ass.IconRefresh, LoadDefaults, () => true, () => "Reset this draft to its boss arena defaults"), top + 4);
        AddButton(new(() => "Save for Next Arena", Ass.IconArenas, Save, CanSave, SaveReason), top + 42);
    }

    private bool Editable => !ArenaRoundSystem.IsSandboxPreset(ArenaRoundSystem.GetPresetOrDefault(preset.Index));
    private static string OnOff(bool value) => value ? "On" : "Off";
    private void NextPage() { page = (Page)(((int)page + 1) % 3); BuildPage(); }
    private void LoadDefaults()
    {
        BossFightPreset selected = ArenaRoundSystem.GetPresetOrDefault(preset.Index);
        draft = ArenaGeometryDefaults.Create(ArenaGeneratorRegistry.ResolveKind(selected));
        BuildPage();
    }
    private void Save() => ArenaGameManagerNetHandler.RequestGeometry(preset.Index, draft);
    private bool CanSave() { try { ArenaGeneratorRegistry.ValidateGeometry(ArenaRoundSystem.GetPresetOrDefault(preset.Index), draft); return true; } catch { return false; } }
    private string SaveReason()
    {
        try { ArenaGeneratorRegistry.ValidateGeometry(ArenaRoundSystem.GetPresetOrDefault(preset.Index), draft); return "Save these settings for the next arena"; }
        catch (Exception exception) { return exception.Message; }
    }

    private void SetWorldWidth(int value)
    {
        value &= ~1;
        int shift = (value - draft.WorldWidth) / 2;
        draft.WorldWidth = value; draft.ArenaRight = value - draft.ArenaLeft;
        draft.BossAreaX += shift; draft.BlueBorderX += shift; draft.RedBorderX += shift;
        draft.RedSpawnX += shift; draft.BlueSpawnX += shift; draft.BossSpawnX += shift;
    }

    private void SetWorldHeight(int value)
    {
        int bottomMargin = draft.WorldHeight - draft.ArenaBottom;
        draft.WorldHeight = value;
        draft.ArenaBottom = value - bottomMargin;
    }

    private void AddSlider(string label, int min, int max, Func<int> get, Action<int> set, ref float top, int step = 1, bool rebuildOnRelease = false)
    {
        int value = Math.Clamp(get(), min, Math.Max(min, max)); set(value);
        UISliderElement slider = new(label, min, Math.Max(min, max), value, step, next => set((int)next), icon: Ass.IconArenas, buttonStep: step);
        if (rebuildOnRelease) slider.OnRelease = _ => BuildPage();
        Add(slider, top); top += 34;
    }

    private void Add(UIElement element, float top) { element.Top.Set(top, 0); ContentPanel.Append(element); }
    private void AddButton(ArenaManagerButton button, float top) { button.Top.Set(top, 0); ContentPanel.Append(button); }
}
