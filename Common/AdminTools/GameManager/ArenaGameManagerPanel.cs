using Arenas.Common.Rounds;
using Arenas.Common.UI;
using Arenas.Core;
using Arenas.Core.Configs;
using SubworldLibrary;
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
    protected override float MinResizeH => 500f;
    protected override float MaxResizeW => 520f;
    protected override float MaxResizeH => 620f;

    public ArenaGameManagerPanel() : base(Language.GetTextValue("Mods.Arenas.Tools.ArenaGameManagerPanel.Title"))
    {
        Width.Set(420, 0); Height.Set(500, 0); HAlign = .5f; VAlign = 0f; Top.Set(200, 0); ContentPanel.SetPadding(6);
        ArenasConfig config = ModContent.GetInstance<ArenasConfig>();
        Add(new ArenaManagerStatusRow(), 0); Add(new ArenaManagerPlayerRow(), 40);
        preset = new(() => selectionTouched = true); Add(preset, 80);
        countdown = Slider("Freeze", 300, config.FreezeCountdownSeconds, value => $"{value:0}s", RoundPhase.FreezeCountdown, ArenaGameManagerNetHandler.ActionType.SetCountdown); Add(countdown, 136);
        roundTime = Slider("Round", 3600, config.RoundDurationSeconds, value => ArenaGameManagerText.Time((int)value), RoundPhase.Playing, ArenaGameManagerNetHandler.ActionType.SetRoundTime); Add(roundTime, 174);
        votingTime = Slider("Vote", 300, config.VotingDurationSeconds, value => $"{value:0}s", RoundPhase.Voting, ArenaGameManagerNetHandler.ActionType.SetVotingTime); Add(votingTime, 212);
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
        AddButton(new(() => ArenaRoundSystem.IsTimerPaused ? "Resume Timer" : "Pause Timer", Ass.IconArenas, () => Request(ArenaGameManagerNetHandler.ActionType.TogglePause), () => InArena() && ArenaRoundSystem.Phase != RoundPhase.Idle, () => "Pause or resume the timer"), 290);
        AddButton(new(AdvanceText, Ass.IconRefresh, () => Request(ArenaGameManagerNetHandler.ActionType.AdvancePhase), () => InArena() && ArenaRoundSystem.Phase is RoundPhase.FreezeCountdown or RoundPhase.Voting, AdvanceTooltip), 328);
        AddButton(new(() => "End Round", Ass.IconEndGame, () => Request(ArenaGameManagerNetHandler.ActionType.EndRound), () => InArena() && ArenaRoundSystem.Phase is RoundPhase.FreezeCountdown or RoundPhase.Playing, () => "End the round and open voting", true), 366);
        AddButton(new(() => ArenaRoundSystem.IsAutoStartHeld ? "Turn On Auto Start" : "Stop Auto Start", Ass.IconArenas, ToggleIdleHold, InArena, () => ArenaRoundSystem.IsAutoStartHeld ? "Let rounds start on their own" : "Stop the game and stay idle", true), 404);
    }

    private void StartRound() { ArenaGameManagerNetHandler.Request(ArenaGameManagerNetHandler.ActionType.StartRound, preset.Index, (int)countdown.Value, (int)roundTime.Value); selectionTouched = false; }
    private void ToggleIdleHold() => ArenaGameManagerNetHandler.Request(ArenaGameManagerNetHandler.ActionType.SetIdleHold, ArenaRoundSystem.IsAutoStartHeld ? 0 : 1);
    private static void Request(ArenaGameManagerNetHandler.ActionType type) => ArenaGameManagerNetHandler.Request(type);
    private static int SecondsLeft() => Math.Max(0, (int)Math.Ceiling(ArenaRoundSystem.RemainingTicks / 60f));
    private static bool InArena() => SubworldSystem.IsActive<ArenasSubworld>();
    private static bool TeamsReady() => Main.netMode == Terraria.ID.NetmodeID.SinglePlayer ? Main.LocalPlayer?.active == true : Main.player.Any(p => p?.active == true && (Team)p.team == Team.Red) && Main.player.Any(p => p?.active == true && (Team)p.team == Team.Blue);
    private bool CanStart() => InArena() && ArenaRoundSystem.GetValidPresets().Count > 0 && TeamsReady();
    private string StartReason() => !InArena() ? "Enter Arenas first" : ArenaRoundSystem.GetValidPresets().Count == 0 ? "Add a valid boss fight preset" : !TeamsReady() ? "Red and Blue both need a player" : "Start this boss fight";
    private static string AdvanceText() => ArenaRoundSystem.Phase == RoundPhase.FreezeCountdown ? "Start Fight" : ArenaRoundSystem.Phase == RoundPhase.Voting ? "End Voting" : "Next Phase";
    private static string AdvanceTooltip() => ArenaRoundSystem.Phase == RoundPhase.FreezeCountdown ? "Skip the countdown" : "End voting now";

    private void RefreshValues()
    {
        ArenasConfig config = ModContent.GetInstance<ArenasConfig>(); preset.SetIndex(ArenaRoundSystem.CurrentPresetIndex);
        countdown.SetValue(ArenaRoundSystem.Phase == RoundPhase.FreezeCountdown ? SecondsLeft() : config.FreezeCountdownSeconds);
        roundTime.SetValue(ArenaRoundSystem.Phase == RoundPhase.Playing ? SecondsLeft() : config.RoundDurationSeconds);
        votingTime.SetValue(ArenaRoundSystem.Phase == RoundPhase.Voting ? SecondsLeft() : config.VotingDurationSeconds);
    }

    private void Add(UIElement element, float top) { element.Top.Set(top, 0); ContentPanel.Append(element); }
    private void AddButton(ArenaManagerButton button, float top) { button.Top.Set(top, 0); ContentPanel.Append(button); }
}
