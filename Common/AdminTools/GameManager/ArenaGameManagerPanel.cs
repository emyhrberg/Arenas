using Arenas.Common.Generation;
using Arenas.Common.Rounds;
using Arenas.Common.UI;
using Arenas.Core;
using Arenas.Core.Configs;
using Arenas.Core.Configs.ConfigElements;
using SubworldLibrary;
using System;
using System.Linq;
using Terraria.GameContent.UI.Elements;
using Terraria.Localization;
using Terraria.UI;

namespace Arenas.Common.AdminTools.GameManager;

internal sealed class ArenaGameManagerPanel : UIDraggablePanel
{
    private enum GeometryPage : byte { World, Boss, Spawns }

    private readonly UIList list;
    private readonly ArenaManagerPresetSelector preset;
    private readonly ArenaManagerSection arenaSection;
    private UILabeledSlider countdown, roundTime, votingTime;
    private ArenaGeometryConfig draft;
    private GeometryPage geometryPage;
    private bool selectionTouched;
    private int seenGeometryRevision;

    protected override float MinResizeW => 480f;
    protected override float MinResizeH => 560f;
    protected override float MaxResizeW => 620f;
    protected override float MaxResizeH => 900f;

    public ArenaGameManagerPanel() : base(Language.GetTextValue("Mods.Arenas.Tools.ArenaGameManagerPanel.Title"))
    {
        Width.Set(540, 0); Height.Set(700, 0); HAlign = .5f; Top.Set(90, 0); Content.SetPadding(6);

        UIScrollbar scrollbar = new() { Left = { Pixels = -20, Percent = 1 }, Width = { Pixels = 20 }, Height = { Percent = 1 } };
        list = new UIList { Width = { Pixels = -26, Percent = 1 }, Height = { Percent = 1 }, ListPadding = 10 };
        list.SetScrollbar(scrollbar); Content.Append(list); Content.Append(scrollbar);

        ArenaManagerSection match = new("Match", 112);
        match.Add(new ArenaManagerStatusRow(), 38);
        match.Add(new ArenaManagerPlayerRow(), 74);
        list.Add(match);

        ArenaManagerSection fight = new("Fight Preset", 202);
        preset = new(OnPresetChanged);
        preset.SetIndex(ArenaRoundSystem.CurrentPresetIndex);
        fight.Add(preset, 38);
        BossFightPreset selected = SelectedPreset;
        countdown = CreateTimingSlider("Freeze", 300, DefaultCountdownSeconds(), value => $"{value:0}s", RoundPhase.FreezeCountdown, ArenaGameManagerNetHandler.ActionType.SetCountdown);
        roundTime = CreateTimingSlider("Round", 3600, selected?.RoundDurationSeconds ?? 600, value => ArenaGameManagerText.Time((int)value), RoundPhase.Playing, ArenaGameManagerNetHandler.ActionType.SetRoundTime, 1);
        votingTime = CreateTimingSlider("Vote", 300, Config.VotingDurationSeconds, value => $"{value:0}s", RoundPhase.Voting, ArenaGameManagerNetHandler.ActionType.SetVotingTime);
        fight.Add(countdown, 92); fight.Add(roundTime, 128); fight.Add(votingTime, 164);
        list.Add(fight);

        arenaSection = new("Arena Settings", 436);
        list.Add(arenaSection);

        ArenaManagerSection controls = new("Round Controls", 268);
        AddControls(controls);
        list.Add(controls);

        LoadDraft();
        RefreshValues();
    }

    protected override void OnClosePanelLeftClick() => ModContent.GetInstance<ArenaGameManagerUISystem>().Hide();
    protected override void OnRefreshPanelLeftClick() { selectionTouched = false; preset.SetIndex(ArenaRoundSystem.CurrentPresetIndex); RefreshValues(); LoadDraft(); }

    public override void Update(GameTime gameTime)
    {
        if (!selectionTouched && preset.Index != ArenaRoundSystem.CurrentPresetIndex)
        {
            preset.SetIndex(ArenaRoundSystem.CurrentPresetIndex);
            RefreshPresetDefaults();
            LoadDraft();
        }
        if (seenGeometryRevision != ArenaGameManagerNetHandler.GeometryRevision)
        {
            seenGeometryRevision = ArenaGameManagerNetHandler.GeometryRevision;
            LoadDraft();
        }
        if (!countdown.IsHeld && ArenaRoundSystem.Phase == RoundPhase.FreezeCountdown) countdown.SetValue(SecondsLeft());
        if (!roundTime.IsHeld && ArenaRoundSystem.Phase == RoundPhase.Playing) roundTime.SetValue(SecondsLeft());
        if (!votingTime.IsHeld && ArenaRoundSystem.Phase == RoundPhase.Voting) votingTime.SetValue(SecondsLeft());
        bool available = !SubworldSystem.AnyActive() || InArena();
        countdown.Enabled = roundTime.Enabled = votingTime.Enabled = available;
        base.Update(gameTime);
    }

    private BossFightPreset SelectedPreset => ArenaRoundSystem.GetPresetOrDefault(preset.Index);
    private static ArenasConfig Config => ModContent.GetInstance<ArenasConfig>();
    private bool EditableArena => !ArenaRoundSystem.IsSandboxPreset(SelectedPreset);

    private void OnPresetChanged()
    {
        selectionTouched = true;
        RefreshPresetDefaults();
        LoadDraft();
    }

    private UILabeledSlider CreateTimingSlider(string label, int max, int value, Func<float, string> format, RoundPhase phase, ArenaGameManagerNetHandler.ActionType action, int min = 0)
    {
        UILabeledSlider slider = new(label, min, max, Math.Clamp(value, min, max), 1, format: format, icon: Ass.IconArenas);
        slider.OnRelease = next => { if (ArenaRoundSystem.Phase == phase) ArenaGameManagerNetHandler.Request(action, (int)next); };
        return slider;
    }

    private void AddControls(ArenaManagerSection section)
    {
        section.Add(new ArenaManagerButton(() => InArena() ? "Change Arena" : "Create & Enter Arena", Ass.IconArenas, PrepareArena, CanPrepareArena, PrepareArenaReason), 38);
        section.Add(new ArenaManagerButton(() => ArenaRoundSystem.Phase is RoundPhase.FreezeCountdown or RoundPhase.Playing ? "Restart Fight" : "Start Fight", Ass.IconStartGame, StartFight, CanStartFight, StartFightReason), 76);
        section.Add(new ArenaManagerButton(() => "Balance Teams", Ass.IconArenas, () => Request(ArenaGameManagerNetHandler.ActionType.BalanceTeams), CanBalance, () => "Shuffle Red and Blue"), 114);
        section.Add(new ArenaManagerButton(() => ArenaRoundSystem.IsTimerPaused ? "Resume Timer" : "Pause Timer", Ass.IconArenas, () => Request(ArenaGameManagerNetHandler.ActionType.TogglePause), () => InArena() && FreezeOrPlaying(ArenaRoundSystem.Phase), () => "Pause or resume the timer"), 152);
        section.Add(new ArenaManagerButton(AdvanceText, Ass.IconRefresh, () => Request(ArenaGameManagerNetHandler.ActionType.AdvancePhase), () => InArena() && ArenaRoundSystem.Phase is RoundPhase.FreezeCountdown or RoundPhase.Voting, AdvanceTooltip), 190);
        section.Add(new ArenaManagerButton(() => ArenaRoundSystem.Phase == RoundPhase.Generating ? "Changing Arena" : "End Round", Ass.IconEndGame, () => Request(ArenaGameManagerNetHandler.ActionType.EndRound), () => InArena() && ArenaRoundSystem.Phase is RoundPhase.Generating or RoundPhase.FreezeCountdown or RoundPhase.Playing, () => ArenaRoundSystem.Phase == RoundPhase.Generating ? "The next arena is loading" : "End the round and open voting", true), 228);
    }

    private void LoadDraft()
    {
        draft = ArenaGeneratorRegistry.ResolveGeometry(SelectedPreset);
        if (draft != null) draft.Enabled = true;
        BuildArenaSection();
    }

    private void BuildArenaSection()
    {
        arenaSection.RemoveAllChildren();
        if (!EditableArena || draft == null)
        {
            arenaSection.Add(new ArenaManagerButton(() => "Sandbox uses Arenas_v10.wld", Ass.IconArenas, () => { }, () => false, () => "Bundled Sandbox world"), 38);
            arenaSection.SetHeight(80);
            RecalculateList();
            return;
        }

        arenaSection.Add(new ArenaManagerTabs(["World", "Boss", "Spawns"], () => (int)geometryPage, index => { geometryPage = (GeometryPage)index; BuildArenaSection(); }), 38);
        float top = 82;
        switch (geometryPage)
        {
            case GeometryPage.World:
                AddGeometrySlider("World Width", 700, 1600, () => draft.WorldWidth, SetWorldWidth, ref top, 2, true);
                AddGeometrySlider("World Height", 500, 1000, () => draft.WorldHeight, SetWorldHeight, ref top, rebuild: true);
                AddGeometrySlider("Arena Left", 4, draft.WorldWidth - 100, () => draft.ArenaLeft, value => { draft.ArenaLeft = value; draft.ArenaRight = draft.WorldWidth - value; }, ref top);
                AddGeometrySlider("Arena Right", 100, draft.WorldWidth - 4, () => draft.ArenaRight, value => { draft.ArenaRight = value; draft.ArenaLeft = draft.WorldWidth - value; }, ref top);
                AddGeometrySlider("Arena Top", 4, draft.WorldHeight - 100, () => draft.ArenaTop, value => draft.ArenaTop = value, ref top);
                AddGeometrySlider("Arena Bottom", 100, draft.WorldHeight - 4, () => draft.ArenaBottom, value => draft.ArenaBottom = value, ref top);
                AddGeometrySlider("Outer Border", 1, 10, () => draft.OuterBorderThickness, value => draft.OuterBorderThickness = value, ref top);
                AddGeometrySlider("Team Line Width", 1, 10, () => draft.TeamBorderWidth, value => draft.TeamBorderWidth = value, ref top);
                break;
            case GeometryPage.Boss:
                AddGeometrySlider("Boss Area X", 4, draft.WorldWidth - 44, () => draft.BossAreaX, value => draft.BossAreaX = value, ref top);
                AddGeometrySlider("Boss Area Y", 4, draft.WorldHeight - 44, () => draft.BossAreaY, value => draft.BossAreaY = value, ref top);
                AddGeometrySlider("Boss Area Width", 40, Math.Min(1000, draft.WorldWidth - 8), () => draft.BossAreaWidth, value => draft.BossAreaWidth = value, ref top);
                AddGeometrySlider("Boss Area Height", 40, Math.Min(900, draft.WorldHeight - 8), () => draft.BossAreaHeight, value => draft.BossAreaHeight = value, ref top);
                AddGeometrySlider("Boss Spawn X", 4, draft.WorldWidth - 4, () => draft.BossSpawnX, value => draft.BossSpawnX = value, ref top);
                AddGeometrySlider("Boss Spawn Y", 4, draft.WorldHeight - 4, () => draft.BossSpawnY, value => draft.BossSpawnY = value, ref top);
                AddGeometrySlider("Blue Border X", 4, draft.WorldWidth - 4, () => draft.BlueBorderX, value => draft.BlueBorderX = value, ref top);
                AddGeometrySlider("Red Border X", 4, draft.WorldWidth - 4, () => draft.RedBorderX, value => draft.RedBorderX = value, ref top);
                break;
            default:
                AddGeometrySlider("Red Spawn X", 4, draft.WorldWidth - 4, () => draft.RedSpawnX, value => draft.RedSpawnX = value, ref top);
                AddGeometrySlider("Red Spawn Y", 4, draft.WorldHeight - 4, () => draft.RedSpawnY, value => draft.RedSpawnY = value, ref top);
                AddGeometrySlider("Blue Spawn X", 4, draft.WorldWidth - 4, () => draft.BlueSpawnX, value => draft.BlueSpawnX = value, ref top);
                AddGeometrySlider("Blue Spawn Y", 4, draft.WorldHeight - 4, () => draft.BlueSpawnY, value => draft.BlueSpawnY = value, ref top);
                AddGeometrySlider("Spawn Room Width", 10, 80, () => draft.SpawnRoomWidth, value => draft.SpawnRoomWidth = value, ref top);
                AddGeometrySlider("Spawn Room Height", 8, 50, () => draft.SpawnRoomHeight, value => draft.SpawnRoomHeight = value, ref top);
                arenaSection.Add(new ArenaManagerButton(() => $"Auto Team Ground: {OnOff(draft.AutoPlaceTeamSpawns)}", Ass.IconArenas, () => { draft.AutoPlaceTeamSpawns = !draft.AutoPlaceTeamSpawns; BuildArenaSection(); }, () => true, () => "Find team ground after terrain builds"), top); top += 38;
                arenaSection.Add(new ArenaManagerButton(() => $"Auto Boss Spot: {OnOff(draft.AutoPlaceBossSpawn)}", Ass.IconArenas, () => { draft.AutoPlaceBossSpawn = !draft.AutoPlaceBossSpawn; BuildArenaSection(); }, () => true, () => "Find a boss spot after terrain builds"), top); top += 38;
                break;
        }

        arenaSection.Add(new ArenaManagerButton(() => "Generator Defaults", Ass.IconRefresh, LoadDefaults, () => true, () => "Reset this preset to its arena defaults"), top + 4);
        arenaSection.Add(new ArenaManagerButton(() => "Save Preset Arena", Ass.IconArenas, SaveGeometry, CanSaveGeometry, SaveGeometryReason), top + 42);
        arenaSection.SetHeight(top + 82);
        RecalculateList();
    }

    private void AddGeometrySlider(string label, int min, int max, Func<int> get, Action<int> set, ref float top, int step = 1, bool rebuild = false)
    {
        int value = Math.Clamp(get(), min, Math.Max(min, max)); set(value);
        UILabeledSlider slider = new(label, min, Math.Max(min, max), value, step, next => set((int)next), icon: Ass.IconArenas, buttonStep: step);
        if (rebuild) slider.OnRelease = _ => BuildArenaSection();
        arenaSection.Add(slider, top); top += 34;
    }

    private void LoadDefaults() { draft = ArenaGeometryDefaults.Create(ArenaGeneratorRegistry.ResolveKind(SelectedPreset)); BuildArenaSection(); }
    private void SaveGeometry() => ArenaGameManagerNetHandler.RequestGeometry(preset.Index, draft);
    private bool CanSaveGeometry() { try { ArenaGeneratorRegistry.ValidateGeometry(SelectedPreset, draft); return true; } catch { return false; } }
    private string SaveGeometryReason() { try { ArenaGeneratorRegistry.ValidateGeometry(SelectedPreset, draft); return "Save this arena with the fight preset"; } catch (Exception exception) { return exception.Message; } }
    private static string OnOff(bool value) => value ? "On" : "Off";
    private void RecalculateList() { arenaSection.Recalculate(); list.Recalculate(); }

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
        draft.WorldHeight = value; draft.ArenaBottom = value - bottomMargin;
    }

    private void PrepareArena() { ArenaGameManagerNetHandler.Request(ArenaGameManagerNetHandler.ActionType.PrepareArena, preset.Index); selectionTouched = false; }
    private void StartFight() => ArenaGameManagerNetHandler.Request(ArenaGameManagerNetHandler.ActionType.StartFight, preset.Index, (int)countdown.Value, (int)roundTime.Value);
    private static void Request(ArenaGameManagerNetHandler.ActionType type) => ArenaGameManagerNetHandler.Request(type);
    private static int SecondsLeft() => Math.Max(0, (int)Math.Ceiling(ArenaRoundSystem.RemainingTicks / 60f));
    private static int DefaultCountdownSeconds() => Config.UseFreezeCountdown ? Config.FreezeCountdownSeconds : 0;
    private static bool InArena() => ArenaWorldSystem.Active;
    private static bool PlayersReady() => Main.player.Any(player => player?.active == true);
    private bool CanPrepareArena() => (!SubworldSystem.AnyActive() || InArena()) && !ArenaSubworldCoordinator.IsTransitioning && ArenaRoundSystem.GetValidPresets().Count > 0 && PlayersReady();
    private bool CanStartFight() => InArena() && ArenaWorldSystem.WorldReady && !ArenaSubworldCoordinator.IsTransitioning && PlayersReady()
        && preset.Index == ArenaRoundSystem.CurrentPresetIndex && !ArenaRoundSystem.IsSandboxPreset(SelectedPreset)
        && ArenaRoundSystem.Phase is RoundPhase.Ready or RoundPhase.FreezeCountdown or RoundPhase.Playing;
    private static bool CanBalance() => (!SubworldSystem.AnyActive() || InArena()) && (ArenaRoundSystem.Phase is RoundPhase.Idle or RoundPhase.Ready) && !ArenaSubworldCoordinator.IsTransitioning;
    private string PrepareArenaReason() => SubworldSystem.AnyActive() && !InArena() ? "Return to the main world" : ArenaSubworldCoordinator.IsTransitioning ? "Changing arena" : ArenaRoundSystem.GetValidPresets().Count == 0 ? "Add a fight preset" : !PlayersReady() ? "Waiting for players" : ArenaRoundSystem.IsSandboxPreset(SelectedPreset) ? "Create the Sandbox world" : "Create this arena and move everyone into it";
    private string StartFightReason() => !InArena() ? "Create and enter an arena first" : !ArenaWorldSystem.WorldReady ? "Arena is still loading" : preset.Index != ArenaRoundSystem.CurrentPresetIndex ? "Create this selected arena first" : ArenaRoundSystem.IsSandboxPreset(SelectedPreset) ? "Sandbox has no boss fight" : !PlayersReady() ? "Waiting for players" : "Start the countdown in the current arena";
    private static bool FreezeOrPlaying(RoundPhase phase) => phase is RoundPhase.FreezeCountdown or RoundPhase.Playing;
    private static string AdvanceText() => ArenaRoundSystem.Phase == RoundPhase.FreezeCountdown ? "Start Fight" : ArenaRoundSystem.Phase == RoundPhase.Voting ? "End Voting" : "Next Phase";
    private static string AdvanceTooltip() => ArenaRoundSystem.Phase == RoundPhase.FreezeCountdown ? "Skip the countdown" : "End voting now";

    private void RefreshValues()
    {
        countdown.SetValue(ArenaRoundSystem.Phase == RoundPhase.FreezeCountdown ? SecondsLeft() : DefaultCountdownSeconds());
        roundTime.SetValue(ArenaRoundSystem.Phase == RoundPhase.Playing ? SecondsLeft() : SelectedPreset?.RoundDurationSeconds ?? 600);
        votingTime.SetValue(ArenaRoundSystem.Phase == RoundPhase.Voting ? SecondsLeft() : Config.VotingDurationSeconds);
    }

    private void RefreshPresetDefaults()
    {
        if (SelectedPreset == null) return;
        if (ArenaRoundSystem.Phase != RoundPhase.FreezeCountdown) countdown.SetValue(DefaultCountdownSeconds());
        if (ArenaRoundSystem.Phase != RoundPhase.Playing) roundTime.SetValue(SelectedPreset.RoundDurationSeconds);
        if (ArenaRoundSystem.Phase != RoundPhase.Voting) votingTime.SetValue(Config.VotingDurationSeconds);
    }
}
