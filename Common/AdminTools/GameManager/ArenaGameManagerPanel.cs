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
    private readonly ArenaManagerPresetSelector preset;
    private UILabeledSlider countdown, roundTime, votingTime;
    private bool selectionTouched;

    protected override float MinResizeW => 480f;
    protected override float MinResizeH => 500f;
    protected override float MaxResizeW => 620f;
    protected override float MaxResizeH => 760f;

    public ArenaGameManagerPanel() : base(Language.GetTextValue("Mods.Arenas.Tools.ArenaGameManagerPanel.Title"))
    {
        Width.Set(540, 0);
        Height.Set(620, 0);
        HAlign = .5f;
        Top.Set(90, 0);
        Content.SetPadding(6);

        UIScrollbar scrollbar = new() { Left = { Pixels = -20, Percent = 1 }, Width = { Pixels = 20 }, Height = { Percent = 1 } };
        UIList list = new() { Width = { Pixels = -26, Percent = 1 }, Height = { Percent = 1 }, ListPadding = 10 };
        list.SetScrollbar(scrollbar);
        Content.Append(list);
        Content.Append(scrollbar);

        ArenaManagerSection match = new("Match", 112);
        match.Add(new ArenaManagerStatusRow(), 38);
        match.Add(new ArenaManagerPlayerRow(), 74);
        list.Add(match);

        ArenaManagerSection fight = new("Boss Fight", 202);
        preset = new(OnPresetChanged);
        preset.SetIndex(ArenaRoundSystem.CurrentPresetIndex);
        fight.Add(preset, 38);
        BossFightPreset selected = SelectedPreset;
        countdown = CreateTimingSlider("Freeze", 300, DefaultCountdownSeconds(), value => $"{value:0}s", RoundPhase.FreezeCountdown, ArenaGameManagerNetHandler.ActionType.SetCountdown);
        roundTime = CreateTimingSlider("Round", 3600, selected?.RoundDurationSeconds ?? 600, value => ArenaGameManagerText.Time((int)value), RoundPhase.Playing, ArenaGameManagerNetHandler.ActionType.SetRoundTime, 1);
        votingTime = CreateTimingSlider("Vote", 300, Config.VotingDurationSeconds, value => $"{value:0}s", RoundPhase.Voting, ArenaGameManagerNetHandler.ActionType.SetVotingTime);
        fight.Add(countdown, 92);
        fight.Add(roundTime, 128);
        fight.Add(votingTime, 164);
        list.Add(fight);

        ArenaManagerSection controls = new("Round Controls", 268);
        AddControls(controls);
        list.Add(controls);

        RefreshValues();
    }

    protected override void OnClosePanelLeftClick() => ModContent.GetInstance<ArenaGameManagerUISystem>().Hide();

    protected override void OnRefreshPanelLeftClick()
    {
        selectionTouched = false;
        preset.SetIndex(ArenaRoundSystem.CurrentPresetIndex);
        RefreshValues();
    }

    public override void Update(GameTime gameTime)
    {
        if (!selectionTouched && preset.Index != ArenaRoundSystem.CurrentPresetIndex)
        {
            preset.SetIndex(ArenaRoundSystem.CurrentPresetIndex);
            RefreshPresetDefaults();
        }

        if (!countdown.IsHeld && ArenaRoundSystem.Phase == RoundPhase.FreezeCountdown)
            countdown.SetValue(SecondsLeft());
        if (!roundTime.IsHeld && ArenaRoundSystem.Phase == RoundPhase.Playing)
            roundTime.SetValue(SecondsLeft());
        if (!votingTime.IsHeld && ArenaRoundSystem.Phase == RoundPhase.Voting)
            votingTime.SetValue(SecondsLeft());

        bool available = !SubworldSystem.AnyActive() || InArena();
        countdown.Enabled = roundTime.Enabled = votingTime.Enabled = available;
        base.Update(gameTime);
    }

    private BossFightPreset SelectedPreset => ArenaRoundSystem.GetPresetOrDefault(preset.Index);
    private static ServerConfig Config => ModContent.GetInstance<ServerConfig>();

    private void OnPresetChanged()
    {
        selectionTouched = true;
        RefreshPresetDefaults();
    }

    private UILabeledSlider CreateTimingSlider(string label, int max, int value, Func<float, string> format, RoundPhase phase, ArenaGameManagerNetHandler.ActionType action, int min = 0)
    {
        UILabeledSlider slider = new(label, min, max, Math.Clamp(value, min, max), 1, format: format, icon: Ass.IconArenas);
        slider.OnRelease = next =>
        {
            if (ArenaRoundSystem.Phase == phase)
                ArenaGameManagerNetHandler.Request(action, (int)next);
        };
        return slider;
    }

    private void AddControls(ArenaManagerSection section)
    {
        section.Add(new ArenaManagerButton(() => InArena() ? "Prepare Preset" : "Enter Arenas", Ass.IconArenas, PrepareArena, CanPrepareArena, PrepareArenaReason), 38);
        section.Add(new ArenaManagerButton(() => ArenaRoundSystem.Phase is RoundPhase.FreezeCountdown or RoundPhase.Playing ? "Restart Fight" : "Start Fight", Ass.IconStartGame, StartFight, CanStartFight, StartFightReason), 76);
        section.Add(new ArenaManagerButton(() => "Balance Teams", Ass.IconArenas, () => Request(ArenaGameManagerNetHandler.ActionType.BalanceTeams), CanBalance, () => "Shuffle Red and Blue"), 114);
        section.Add(new ArenaManagerButton(() => ArenaRoundSystem.IsTimerPaused ? "Resume Timer" : "Pause Timer", Ass.IconArenas, () => Request(ArenaGameManagerNetHandler.ActionType.TogglePause), () => InArena() && FreezeOrPlaying(ArenaRoundSystem.Phase), () => "Pause or resume the timer"), 152);
        section.Add(new ArenaManagerButton(AdvanceText, Ass.IconRefresh, () => Request(ArenaGameManagerNetHandler.ActionType.AdvancePhase), () => InArena() && ArenaRoundSystem.Phase is RoundPhase.FreezeCountdown or RoundPhase.Voting, AdvanceTooltip), 190);
        section.Add(new ArenaManagerButton(() => ArenaRoundSystem.Phase == RoundPhase.Generating ? "Changing Arena" : "End Round", Ass.IconEndGame, () => Request(ArenaGameManagerNetHandler.ActionType.EndRound), () => InArena() && ArenaRoundSystem.Phase is RoundPhase.Generating or RoundPhase.FreezeCountdown or RoundPhase.Playing, () => ArenaRoundSystem.Phase == RoundPhase.Generating ? "The next arena is loading" : "End the round and open voting", true), 228);
    }

    private void PrepareArena()
    {
        ArenaGameManagerNetHandler.Request(ArenaGameManagerNetHandler.ActionType.PrepareArena, preset.Index);
        selectionTouched = false;
    }

    private void StartFight() => ArenaGameManagerNetHandler.Request(ArenaGameManagerNetHandler.ActionType.StartFight, preset.Index, (int)countdown.Value, (int)roundTime.Value);
    private static void Request(ArenaGameManagerNetHandler.ActionType type) => ArenaGameManagerNetHandler.Request(type);
    private static int SecondsLeft() => Math.Max(0, (int)Math.Ceiling(ArenaRoundSystem.RemainingTicks / 60f));
    private static int DefaultCountdownSeconds() => Config.UseFreezeCountdown ? Config.FreezeCountdownSeconds : 0;
    private static bool InArena() => ArenaWorldSystem.Active;
    private static bool PlayersReady() => Main.player.Any(player => player?.active == true);
    private bool CanPrepareArena() => (!SubworldSystem.AnyActive() || InArena()) && !ArenaSubworldCoordinator.IsTransitioning && ArenaRoundSystem.GetValidPresets().Count > 0 && PlayersReady();
    private bool CanStartFight() => InArena() && ArenaWorldSystem.MatchReady && !ArenaSubworldCoordinator.IsTransitioning && PlayersReady()
        && preset.Index == ArenaRoundSystem.CurrentPresetIndex && !ArenaRoundSystem.IsSandboxPreset(SelectedPreset)
        && ArenaRoundSystem.Phase is RoundPhase.Ready or RoundPhase.FreezeCountdown or RoundPhase.Playing;
    private static bool CanBalance() => (!SubworldSystem.AnyActive() || InArena())
        && ArenaRoundSystem.Phase is RoundPhase.Idle or RoundPhase.Ready
        && !ArenaSubworldCoordinator.IsTransitioning;
    private string PrepareArenaReason() => SubworldSystem.AnyActive() && !InArena() ? "Return to the main world" : ArenaSubworldCoordinator.IsTransitioning ? "Arenas is restarting" : ArenaRoundSystem.GetValidPresets().Count == 0 ? "Add a boss fight" : !PlayersReady() ? "Waiting for players" : InArena() ? "Prepare this preset in the existing world (no regeneration)" : "Enter the persistent Arenas world; generate it once if needed";
    private string StartFightReason() => !InArena() ? "Enter Arenas first" : !ArenaWorldSystem.MatchReady ? "A complete Arenas world is required" : preset.Index != ArenaRoundSystem.CurrentPresetIndex ? "Prepare this selected preset first" : ArenaRoundSystem.IsSandboxPreset(SelectedPreset) ? "Sandbox has no boss fight" : !PlayersReady() ? "Waiting for players" : "Start the countdown in the current arena";
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
        if (SelectedPreset == null)
            return;
        if (ArenaRoundSystem.Phase != RoundPhase.FreezeCountdown)
            countdown.SetValue(DefaultCountdownSeconds());
        if (ArenaRoundSystem.Phase != RoundPhase.Playing)
            roundTime.SetValue(SelectedPreset.RoundDurationSeconds);
        if (ArenaRoundSystem.Phase != RoundPhase.Voting)
            votingTime.SetValue(Config.VotingDurationSeconds);
    }
}
