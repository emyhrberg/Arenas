using Arenas.Common.DataStructures;
using Arenas.Common.Generation;
using Arenas.Core.Configs;
using PvPFramework.Common.EndScreen;
using System;
using System.IO;
using System.Linq;
using Terraria.Enums;
using Terraria.GameContent.Creative;
using Terraria.GameContent.NetModules;
using Terraria.ID;
using Terraria.Net;

namespace Arenas.Common.Game;

/// <summary>Server-authoritative Arenas round loop.</summary>
internal sealed class RoundManager : ModSystem
{
    private const int TicksPerSecond = 60;

    internal enum RoundPhase : byte
    {
        WaitingForPlayers,
        VotingOrEndScreen,
        Generating,
        FreezeCountdown,
        Playing
    }

    internal enum AdminAction : byte
    {
        StartRound,
        EndRound,
        StartVoting,
        EndVoting,
        SetIdle,
        AutoBalanceTeams
    }

    private enum RoundEndReason : byte
    {
        BossDefeated,
        TimeExpired,
        BossDespawned,
        SpawnFailed,
        ArenaUnavailable,
        NoPlayers,
        AdminEnded
    }

    private RoundPhase currentPhase = RoundPhase.WaitingForPlayers;
    private int remainingTicks;
    private bool timerPaused;
    private bool idleHeld;
    private int selectedPresetIndex = -1;
    private ArenaLayout currentLayout;
    private Team pendingWinningTeam;
    private int pendingWinningPlayer = -1;

    internal RoundPhase CurrentPhase => currentPhase;
    internal int RemainingTicks => remainingTicks;
    internal bool IsTimerPaused => timerPaused;
    internal bool IsIdleHeld => idleHeld;
    internal int SelectedPresetIndex => selectedPresetIndex;
    internal ArenaLayout CurrentLayout => currentLayout;

    internal int SelectedBossType => TryGetSelectedPreset(out BossFightPreset preset)
        ? preset.Boss.Type
        : NPCID.None;

    public override void PostUpdateEverything()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            TickClientTimer();
            return;
        }

        if (!Main.player.Any(player => player?.active == true))
        {
            if (currentPhase != RoundPhase.WaitingForPlayers)
                FinishRound(RoundEndReason.NoPlayers);
            return;
        }

        TeamBalancer.AssignUnassignedPlayers();

        if (currentPhase == RoundPhase.WaitingForPlayers)
        {
            if (!idleHeld)
                StartIntermission();
            return;
        }

        if (currentPhase == RoundPhase.Playing)
        {
            if (pendingWinningTeam != Team.None)
            {
                Team winner = pendingWinningTeam;
                int player = pendingWinningPlayer;
                pendingWinningTeam = Team.None;
                pendingWinningPlayer = -1;
                FinishRound(RoundEndReason.BossDefeated, winner, player);
                return;
            }

            if (ModContent.GetInstance<BossManager>().Update() == BossState.Missing)
            {
                FinishRound(RoundEndReason.BossDespawned);
                return;
            }
        }

        if (timerPaused || remainingTicks <= 0)
            return;

        remainingTicks--;
        if (remainingTicks > 0)
            return;

        switch (currentPhase)
        {
            case RoundPhase.VotingOrEndScreen:
                PrepareRound();
                break;
            case RoundPhase.FreezeCountdown:
                StartPlaying();
                break;
            case RoundPhase.Playing:
                FinishRound(RoundEndReason.TimeExpired);
                break;
        }
    }

    internal bool TryGetSelectedPreset(out BossFightPreset preset)
    {
        var presets = ModContent.GetInstance<ServerConfig>().FightPresets;
        if (presets != null && selectedPresetIndex >= 0 && selectedPresetIndex < presets.Count)
        {
            preset = presets[selectedPresetIndex];
            return BossVoteSystem.IsVotable(preset);
        }

        preset = null;
        return false;
    }

    internal void NotifyBossDefeated(Player player, Team team)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || currentPhase != RoundPhase.Playing
            || team is not (Team.Red or Team.Blue) || pendingWinningTeam != Team.None)
            return;

        pendingWinningTeam = team;
        pendingWinningPlayer = player?.whoAmI ?? -1;
        Log.Info($"[M2-BossVictory] team={team}, player={pendingWinningPlayer}.");
    }

    internal void SetRemainingSeconds(int seconds)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || !IsTimedPhase(currentPhase))
            return;

        remainingTicks = SecondsToTicks(seconds);
        SyncState();
    }

    internal void ToggleTimerPaused()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || !IsTimedPhase(currentPhase))
            return;

        timerPaused = !timerPaused;
        SyncState();
    }

    internal static void RequestAdminAction(AdminAction action)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            ModPacket packet = ModContent.GetInstance<Arenas>().GetPacket();
            packet.Write((byte)Arenas.PacketType.AdminRoundAction);
            packet.Write((byte)action);
            packet.Send();
            return;
        }

        ModContent.GetInstance<RoundManager>().ExecuteAdminAction(action, Main.myPlayer);
    }

    internal void ExecuteAdminAction(AdminAction action, int playerId)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        Log.Info($"[M2-Admin] player={playerId}, action={action}, phase={currentPhase}.");
        switch (action)
        {
            case AdminAction.StartRound:
                idleHeld = false;
                if (currentPhase == RoundPhase.FreezeCountdown)
                    StartPlaying();
                else if (currentPhase is RoundPhase.WaitingForPlayers or RoundPhase.VotingOrEndScreen)
                    PrepareRound();
                break;

            case AdminAction.EndRound:
                if (currentPhase == RoundPhase.Playing)
                    FinishRound(RoundEndReason.AdminEnded);
                break;

            case AdminAction.StartVoting:
                idleHeld = false;
                if (currentPhase == RoundPhase.Playing)
                    FinishRound(RoundEndReason.AdminEnded);
                else if (currentPhase is not (RoundPhase.VotingOrEndScreen or RoundPhase.Generating))
                {
                    ModContent.GetInstance<BossManager>().Cleanup();
                    ArenaPlayer.ReleaseAll();
                    StartIntermission();
                }
                break;

            case AdminAction.EndVoting:
                if (currentPhase == RoundPhase.VotingOrEndScreen)
                    PrepareRound();
                break;

            case AdminAction.SetIdle:
                if (currentPhase == RoundPhase.Generating)
                    break;
                ModContent.GetInstance<BossManager>().Cleanup();
                ArenaPlayer.ReleaseAll();
                EndScreenService.Hide();
                pendingWinningTeam = Team.None;
                pendingWinningPlayer = -1;
                idleHeld = true;
                selectedPresetIndex = -1;
                currentLayout = null;
                SetPhase(RoundPhase.WaitingForPlayers, 0);
                break;

            case AdminAction.AutoBalanceTeams:
                if (currentPhase is RoundPhase.WaitingForPlayers or RoundPhase.VotingOrEndScreen)
                    TeamBalancer.AutoBalanceTeams();
                break;
        }
    }

    private void StartIntermission()
    {
        TeamBalancer.AssignUnassignedPlayers();
        if (!TeamBalancer.AllActivePlayersAssigned())
        {
            SetPhase(RoundPhase.WaitingForPlayers, 0);
            return;
        }

        ModContent.GetInstance<BossVoteSystem>().Reset();
        selectedPresetIndex = FindFirstPlayablePreset();
        int seconds = Math.Max(1, ModContent.GetInstance<ServerConfig>().VotingDurationSeconds);
        SetPhase(RoundPhase.VotingOrEndScreen, SecondsToTicks(seconds));
    }

    private void PrepareRound()
    {
        EndScreenService.Hide();

        int votedPreset = ModContent.GetInstance<BossVoteSystem>().ResolveWinner();
        if (votedPreset >= 0)
            selectedPresetIndex = votedPreset;

        if (!TryGetSelectedPreset(out BossFightPreset preset))
        {
            Log.Warn("[M2-Prepare] No playable boss preset is configured; retrying after the intermission.");
            StartIntermission();
            return;
        }

        Player[] participants = Main.player
            .Where(player => player?.active == true && (Team)player.team is Team.Red or Team.Blue)
            .ToArray();
        if (participants.Length == 0)
        {
            Log.Warn("[M2-Prepare] No active Red or Blue players were available after automatic assignment.");
            StartIntermission();
            return;
        }

        SetPhase(RoundPhase.Generating, 0);
        if (!ArenaGeneration.TryResolve(preset, out ArenaLayout layout, out string failure))
        {
            Log.Warn($"[M2-Prepare] Existing-terrain arena resolution failed: {failure}");
            FinishRound(RoundEndReason.ArenaUnavailable);
            return;
        }

        currentLayout = layout;
        foreach (Player player in participants)
            ArenaPlayer.Prepare(player, preset, currentLayout);

        int countdownSeconds = Math.Max(0, ModContent.GetInstance<ServerConfig>().FreezeCountdownSeconds);
        if (countdownSeconds == 0)
        {
            StartPlaying();
            return;
        }

        SetPhase(RoundPhase.FreezeCountdown, SecondsToTicks(countdownSeconds));
    }

    private void StartPlaying()
    {
        if (!TryGetSelectedPreset(out BossFightPreset preset) || currentLayout == null
            || !ModContent.GetInstance<BossManager>().TrySpawn(preset, currentLayout))
        {
            FinishRound(RoundEndReason.SpawnFailed);
            return;
        }

        int seconds = Math.Max(1, ModContent.GetInstance<ServerConfig>().RoundDurationSeconds);
        SetPhase(RoundPhase.Playing, SecondsToTicks(seconds));
    }

    private void FinishRound(RoundEndReason reason, Team winningTeam = Team.None, int winningPlayer = -1)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        ModContent.GetInstance<BossManager>().Cleanup();
        ArenaPlayer.ReleaseAll();
        pendingWinningTeam = Team.None;
        pendingWinningPlayer = -1;
        Log.Info($"[M2-RoundEnd] reason={reason}, winner={winningTeam}, player={winningPlayer}.");

        if (reason == RoundEndReason.NoPlayers)
        {
            EndScreenService.Hide();
            selectedPresetIndex = -1;
            currentLayout = null;
            SetPhase(RoundPhase.WaitingForPlayers, 0);
            return;
        }

        if (reason is RoundEndReason.BossDefeated or RoundEndReason.TimeExpired
            or RoundEndReason.BossDespawned or RoundEndReason.AdminEnded)
            ArenaEndScreen.Present(winningTeam, winningPlayer);

        StartIntermission();
    }

    private int FindFirstPlayablePreset()
    {
        var presets = ModContent.GetInstance<ServerConfig>().FightPresets;
        if (presets == null)
            return -1;

        for (int i = 0; i < presets.Count; i++)
            if (BossVoteSystem.IsVotable(presets[i]))
                return i;
        return -1;
    }

    private void SetPhase(RoundPhase newPhase, int durationTicks)
    {
        RoundPhase oldPhase = currentPhase;
        currentPhase = newPhase;
        remainingTicks = Math.Max(0, durationTicks);
        timerPaused = false;

        int players = Main.player.Count(player => player?.active == true);
        Log.Info($"[M2-Phase] {oldPhase} -> {newPhase}; ticks={remainingTicks}, preset={selectedPresetIndex}, players={players}.");
        UpdateFreezeTime(newPhase != RoundPhase.Playing);
        SyncState();
    }

    private void TickClientTimer()
    {
        if (!timerPaused && IsTimedPhase(currentPhase) && remainingTicks > 0)
            remainingTicks--;
    }

    private static bool IsTimedPhase(RoundPhase phase) =>
        phase is RoundPhase.VotingOrEndScreen or RoundPhase.FreezeCountdown or RoundPhase.Playing;

    private static int SecondsToTicks(int seconds) => Math.Max(0, seconds) * TicksPerSecond;

    private static void UpdateFreezeTime(bool frozen)
    {
        CreativePowers.FreezeTime freezeTime = CreativePowerManager.Instance.GetPower<CreativePowers.FreezeTime>();
        freezeTime.SetPowerInfo(frozen);

        if (Main.netMode == NetmodeID.Server)
        {
            NetPacket packet = NetCreativePowersModule.PreparePacket(freezeTime.PowerId, 1);
            packet.Writer.Write(freezeTime.Enabled);
            NetManager.Instance.Broadcast(packet);
        }
    }

    private static void SyncState()
    {
        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendData(MessageID.WorldData);
    }

    public override void OnWorldLoad()
    {
        currentPhase = RoundPhase.WaitingForPlayers;
        remainingTicks = 0;
        timerPaused = false;
        idleHeld = false;
        selectedPresetIndex = -1;
        currentLayout = null;
        pendingWinningTeam = Team.None;
        pendingWinningPlayer = -1;

        if (Main.netMode != NetmodeID.MultiplayerClient)
            UpdateFreezeTime(true);
    }

    public override void ClearWorld()
    {
        ModContent.GetInstance<BossManager>().Cleanup();
        currentPhase = RoundPhase.WaitingForPlayers;
        remainingTicks = 0;
        timerPaused = false;
        idleHeld = false;
        selectedPresetIndex = -1;
        currentLayout = null;
    }

    public override void NetSend(BinaryWriter writer)
    {
        writer.Write((byte)currentPhase);
        writer.Write(remainingTicks);
        writer.Write(timerPaused);
        writer.Write(idleHeld);
        writer.Write(selectedPresetIndex);
        writer.Write(currentLayout != null);
        currentLayout?.Write(writer);
    }

    public override void NetReceive(BinaryReader reader)
    {
        currentPhase = (RoundPhase)reader.ReadByte();
        remainingTicks = Math.Max(0, reader.ReadInt32());
        timerPaused = reader.ReadBoolean();
        idleHeld = reader.ReadBoolean();
        selectedPresetIndex = reader.ReadInt32();
        currentLayout = reader.ReadBoolean() ? ArenaLayout.Read(reader) : null;
    }
}
