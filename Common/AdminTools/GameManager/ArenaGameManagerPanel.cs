using Arenas.Common.Rounds;
using Arenas.Common.UI;
using Arenas.Core;
using Arenas.Core.Configs;
using Arenas.Core.Configs.ConfigElements;
using System;
using System.Linq;
using Terraria.Enums;
using Terraria.Localization;
using Terraria.UI;

namespace Arenas.Common.AdminTools.GameManager;

internal sealed class ArenaGameManagerPanel : UIDraggablePanel
{
    private readonly ArenaManagerPresetSelector preset;
    private readonly UISliderElement countdown, roundTime, votingTime;
    private bool selectionTouched;

    protected override float MinResizeW => 400f;
    protected override float MinResizeH => 540f;
    protected override float MaxResizeW => 520f;
    protected override float MaxResizeH => 620f;

    public ArenaGameManagerPanel() : base(Language.GetTextValue("Mods.Arenas.Tools.ArenaGameManagerPanel.Title"))
    {
        Width.Set(420, 0); Height.Set(540, 0); HAlign = .5f; VAlign = 0f; Top.Set(170, 0); ContentPanel.SetPadding(6);
        BossFightPreset selected = ArenaRoundSystem.GetPresetOrDefault(ArenaRoundSystem.CurrentPresetIndex);
        Add(new ArenaManagerStatusRow(), 0); Add(new ArenaManagerPlayerRow(), 40);
        preset = new(() => { selectionTouched = true; RefreshPresetDefaults(); }); Add(preset, 80);
        countdown = Slider("Freeze", 300, DefaultCountdownSeconds(), value => $"{value:0}s", RoundPhase.FreezeCountdown, ArenaGameManagerNetHandler.ActionType.SetCountdown); Add(countdown, 136);
        roundTime = Slider("Round", 3600, selected?.RoundDurationSeconds ?? 600, value => ArenaGameManagerText.Time((int)value), RoundPhase.Playing, ArenaGameManagerNetHandler.ActionType.SetRoundTime); Add(roundTime, 174);
        votingTime = Slider("Vote", 300, TimingConfig.VotingDurationSeconds, value => $"{value:0}s", RoundPhase.Voting, ArenaGameManagerNetHandler.ActionType.SetVotingTime); Add(votingTime, 212);
        AddButtons(); RefreshValues();
    }

    protected override void OnClosePanelLeftClick() => ModContent.GetInstance<ArenaGameManagerUISystem>().Hide();
    protected override void OnRefreshPanelLeftClick() { selectionTouched = false; RefreshValues(); }

    public override void Update(GameTime gameTime)
    {
        if (!selectionTouched) preset.SetIndex(ArenaRoundSystem.CurrentPresetIndex);
        if (!countdown.IsHeld && ArenaRoundSystem.Phase == RoundPhase.FreezeCountdown) countdown.SetValue(SecondsLeft());
        if (!roundTime.IsHeld && ArenaRoundSystem.Phase == RoundPhase.Playing) roundTime.SetValue(SecondsLeft());
        if (!votingTime.IsHeld && ArenaRoundSystem.Phase == RoundPhase.Voting) votingTime.SetValue(SecondsLeft());
        bool active = InArena(); countdown.Enabled = roundTime.Enabled = votingTime.Enabled = active;
        base.Update(gameTime);
    }

    private UISliderElement Slider(string label, int max, int value, Func<float, string> format, RoundPhase phase, ArenaGameManagerNetHandler.ActionType action)
    {
        UISliderElement slider = new(label, 0, max, value, 1, format: format, icon: Ass.IconArenas);
        slider.OnRelease = next => { if (ArenaRoundSystem.Phase == phase) ArenaGameManagerNetHandler.Request(action, (int)next); };
        return slider;
    }

    private void AddButtons()
    {
        AddButton(new(() => ArenaRoundSystem.Phase == RoundPhase.Idle ? "Start Round" : "Restart Round", Ass.IconStartGame, StartRound, CanStart, StartReason), 252);
        AddButton(new(() => "Balance Teams", Ass.IconArenas, () => Request(ArenaGameManagerNetHandler.ActionType.BalanceTeams), CanBalance, () => "Randomly split every online player as evenly as possible between Red and Blue"), 290);
        AddButton(new(() => ArenaWorldSystem.IsClearing ? $"Clearing World ({ArenaWorldSystem.ClearingProgress:P0})" : "Clear World", Ass.IconRefresh, () => Request(ArenaGameManagerNetHandler.ActionType.ClearWorld), CanClear, () => "Stop the current game, erase the world, and create a small safe platform at the central spawn", true), 328);
        AddButton(new(() => ArenaRoundSystem.IsTimerPaused ? "Resume Timer" : "Pause Timer", Ass.IconArenas, () => Request(ArenaGameManagerNetHandler.ActionType.TogglePause), () => InArena() && ArenaRoundSystem.Phase is not (RoundPhase.Idle or RoundPhase.Generating), () => "Pause or resume the timer"), 366);
        AddButton(new(AdvanceText, Ass.IconRefresh, () => Request(ArenaGameManagerNetHandler.ActionType.AdvancePhase), () => InArena() && ArenaRoundSystem.Phase is RoundPhase.FreezeCountdown or RoundPhase.Voting, AdvanceTooltip), 404);
        AddButton(new(() => ArenaRoundSystem.Phase == RoundPhase.Generating ? "Abort Generation" : "End Round", Ass.IconEndGame, () => Request(ArenaGameManagerNetHandler.ActionType.EndRound), () => InArena() && ArenaRoundSystem.Phase is RoundPhase.Generating or RoundPhase.FreezeCountdown or RoundPhase.Playing, () => ArenaRoundSystem.Phase == RoundPhase.Generating ? "Abort generation and return to a clean central spawn" : "End the round and open voting", true), 442);
    }

    private void StartRound() { ArenaGameManagerNetHandler.Request(ArenaGameManagerNetHandler.ActionType.StartRound, preset.Index, (int)countdown.Value, (int)roundTime.Value); selectionTouched = false; }
    private static void Request(ArenaGameManagerNetHandler.ActionType type) => ArenaGameManagerNetHandler.Request(type);
    private static int SecondsLeft() => Math.Max(0, (int)Math.Ceiling(ArenaRoundSystem.RemainingTicks / 60f));
    private static ArenaTimingConfig TimingConfig => ModContent.GetInstance<ArenaTimingConfig>();
    private static int DefaultCountdownSeconds() => TimingConfig.UseFreezeCountdown ? TimingConfig.FreezeCountdownSeconds : 0;
    private static bool InArena() => ArenaWorldSystem.Active;
    private static bool TeamsReady() => Main.netMode == Terraria.ID.NetmodeID.SinglePlayer ? Main.LocalPlayer?.active == true : Main.player.Any(p => p?.active == true && (Team)p.team == Team.Red) && Main.player.Any(p => p?.active == true && (Team)p.team == Team.Blue);
    private bool CanStart() => InArena() && ArenaWorldSystem.WorldReady && !ArenaWorldSystem.IsClearing && ArenaRoundSystem.GetValidPresets().Count > 0 && TeamsReady();
    private static bool CanBalance() => InArena() && ArenaRoundSystem.Phase == RoundPhase.Idle && !ArenaWorldSystem.IsClearing;
    private static bool CanClear() => InArena() && !ArenaWorldSystem.IsClearing;
    private string StartReason() => !InArena() ? "Enter Arenas first" : ArenaWorldSystem.IsClearing ? "Wait for the world clear to finish" : ArenaRoundSystem.GetValidPresets().Count == 0 ? "Add a valid boss fight preset" : !TeamsReady() ? "Balance teams first; Red and Blue both need a player" : "Generate this arena, teleport both teams, and start the freeze countdown";
    private static string AdvanceText() => ArenaRoundSystem.Phase == RoundPhase.FreezeCountdown ? "Start Fight" : ArenaRoundSystem.Phase == RoundPhase.Voting ? "End Voting" : "Next Phase";
    private static string AdvanceTooltip() => ArenaRoundSystem.Phase == RoundPhase.FreezeCountdown ? "Skip the countdown" : "End voting now";

    private void RefreshValues()
    {
        preset.SetIndex(ArenaRoundSystem.CurrentPresetIndex);
        BossFightPreset selected = ArenaRoundSystem.GetPresetOrDefault(preset.Index);
        countdown.SetValue(ArenaRoundSystem.Phase == RoundPhase.FreezeCountdown ? SecondsLeft() : DefaultCountdownSeconds());
        roundTime.SetValue(ArenaRoundSystem.Phase == RoundPhase.Playing ? SecondsLeft() : selected?.RoundDurationSeconds ?? 600);
        votingTime.SetValue(ArenaRoundSystem.Phase == RoundPhase.Voting ? SecondsLeft() : TimingConfig.VotingDurationSeconds);
    }

    private void RefreshPresetDefaults()
    {
        BossFightPreset selected = ArenaRoundSystem.GetPresetOrDefault(preset.Index);
        if (selected == null) return;
        if (ArenaRoundSystem.Phase != RoundPhase.FreezeCountdown) countdown.SetValue(DefaultCountdownSeconds());
        if (ArenaRoundSystem.Phase != RoundPhase.Playing) roundTime.SetValue(selected.RoundDurationSeconds);
        if (ArenaRoundSystem.Phase != RoundPhase.Voting) votingTime.SetValue(TimingConfig.VotingDurationSeconds);
    }

    private void Add(UIElement element, float top) { element.Top.Set(top, 0); ContentPanel.Append(element); }
    private void AddButton(ArenaManagerButton button, float top) { button.Top.Set(top, 0); ContentPanel.Append(button); }
}
