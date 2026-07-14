using Arenas.Core.Configs;
using Arenas.Core.Configs.ConfigElements;
using SubworldLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;

namespace Arenas.Common.Rounds;

public enum RoundPhase : byte { Idle, Playing, Voting, FreezeCountdown }
public enum RoundResult : byte { None, BossDefeated, TimeExpired, BossDespawned, SpawnFailed, AdminEnded }
public readonly record struct RoundPlayerStats(byte PlayerId, Team Team, string Name, int Kills, int Deaths, long Damage, long BossDamage);

internal sealed class ArenaRoundSystem : ModSystem
{
    public const int MaxPresets = 7;
    private static readonly List<RoundPlayerStats> participants = [], scoreboard = [];
    private static readonly Dictionary<int, int> votes = [];
    private static readonly List<int> voteCounts = [];
    private static readonly List<List<byte>> voteVoters = [];
    private static bool inArena;
    private static int bossIndex = -1, bossType, bossLife, bossLifeMax, nextRoundTicks;

    public static RoundPhase Phase { get; private set; }
    public static RoundResult Result { get; private set; }
    public static int RemainingTicks { get; private set; }
    public static int CurrentPresetIndex { get; private set; }
    public static int LocalVote { get; private set; } = -1;
    public static bool IsTimerPaused { get; private set; }
    public static bool IsAutoStartHeld { get; private set; }
    public static int BossLife => bossLife;
    public static int BossLifeMax => bossLifeMax;
    public static IReadOnlyList<int> VoteCounts => voteCounts;
    public static IReadOnlyList<RoundPlayerStats> Scoreboard => Main.netMode == NetmodeID.MultiplayerClient || Phase == RoundPhase.Voting ? scoreboard : LiveStats();
    public static IReadOnlyList<byte> VotersFor(int preset) => preset >= 0 && preset < voteVoters.Count ? voteVoters[preset] : [];

    public override void OnWorldLoad() => Reset(false);
    public override void OnWorldUnload() => Reset(true);

    public override void PostUpdateEverything()
    {
        bool active = SubworldSystem.IsActive<ArenasSubworld>();
        if (!active)
        {
            if (inArena) Reset(true);
            inArena = false;
            return;
        }

        if (!inArena) Reset(false);
        inArena = true;
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            if (Phase == RoundPhase.FreezeCountdown) Main.LocalPlayer.AddBuff(BuffID.Frozen, 2);
            if (!IsTimerPaused && Phase != RoundPhase.Idle && RemainingTicks > 0) RemainingTicks--;
            return;
        }

        switch (Phase)
        {
            case RoundPhase.Idle:
                int requiredPresets = Main.netMode == NetmodeID.SinglePlayer ? 1 : 2;
                if (!IsAutoStartHeld && GetValidPresets().Count >= requiredPresets && TeamsReady()) StartFreeze(0);
                break;
            case RoundPhase.FreezeCountdown: TickFreeze(); break;
            case RoundPhase.Playing: TickPlaying(); break;
            case RoundPhase.Voting: TickVoting(); break;
        }

        if (Main.netMode == NetmodeID.Server && Main.GameUpdateCount % 60 == 0) ArenaRoundNetHandler.SendStateToAll();
    }

    public static List<BossFightPreset> GetValidPresets() => (Config.FightPresets ?? [])
        .Where(p => p?.Boss?.Type > 0 && FindLoadout(p.LoadoutName) != null).Take(MaxPresets).ToList();

    public static string PresetName(BossFightPreset preset) => string.IsNullOrWhiteSpace(preset.Name) ? preset.Boss?.DisplayName ?? "Boss" : preset.Name;
    public static bool IsParticipant(int playerId) => participants.Any(p => p.PlayerId == playerId);

    public static void RequestVote(int index)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient) ArenaRoundNetHandler.SendVote(index);
        else CastVote(Main.myPlayer, index);
    }

    internal static void CastVote(int playerId, int index)
    {
        if (Phase != RoundPhase.Voting || !IsParticipant(playerId) || index < 0 || index >= GetValidPresets().Count) return;
        if (playerId < 0 || playerId >= Main.maxPlayers || Main.player[playerId]?.active != true) return;
        votes[playerId] = index;
        CountVotes();
        LocalVote = Main.netMode == NetmodeID.SinglePlayer ? index : LocalVote;
        ArenaRoundNetHandler.SendStateToAll();
    }

    internal static int VoteFor(int playerId) => votes.TryGetValue(playerId, out int vote) ? vote : -1;

    internal static void ApplyState(RoundPhase phase, RoundResult result, int ticks, int preset, int localVote, bool paused, bool autoStartHeld, int life, int lifeMax, List<int> counts, List<List<byte>> voters, List<RoundPlayerStats> entries)
    {
        Phase = phase; Result = result; RemainingTicks = ticks; CurrentPresetIndex = preset; LocalVote = localVote;
        IsTimerPaused = paused; IsAutoStartHeld = autoStartHeld; bossLife = life; bossLifeMax = lifeMax;
        voteCounts.Clear(); voteCounts.AddRange(counts);
        voteVoters.Clear(); voteVoters.AddRange(voters);
        scoreboard.Clear(); scoreboard.AddRange(entries);
    }

    internal static void ApplyKit(int presetIndex)
    {
        List<BossFightPreset> presets = GetValidPresets();
        if (presetIndex >= 0 && presetIndex < presets.Count && FindLoadout(presets[presetIndex].LoadoutName) is Loadout loadout)
            LoadoutService.Apply(Main.LocalPlayer, loadout);
    }

    internal static void AdminStartRound(int presetIndex, int countdownSeconds, int roundSeconds)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || !SubworldSystem.IsActive<ArenasSubworld>()) return;
        StartFreeze(presetIndex, Math.Clamp(countdownSeconds, 0, 300) * 60, Math.Clamp(roundSeconds, 0, 3600) * 60);
    }

    internal static void AdminSetCountdown(int seconds) { if (Phase == RoundPhase.FreezeCountdown) SetRemaining(seconds, 300); }
    internal static void AdminSetRoundTime(int seconds) { if (Phase == RoundPhase.Playing) SetRemaining(seconds, 3600); }
    internal static void AdminSetVotingTime(int seconds) { if (Phase == RoundPhase.Voting) SetRemaining(seconds, 300); }

    internal static void AdminTogglePause()
    {
        if (Phase == RoundPhase.Idle) return;
        IsTimerPaused = !IsTimerPaused; ArenaRoundNetHandler.SendStateToAll();
    }

    internal static void AdminAdvancePhase()
    {
        if (Phase == RoundPhase.FreezeCountdown) StartPlaying();
        else if (Phase == RoundPhase.Voting) ResolveVoting();
    }

    internal static void AdminEndRound() { if (Phase is RoundPhase.FreezeCountdown or RoundPhase.Playing) EndRound(RoundResult.AdminEnded); }

    internal static void AdminSetIdleHold(bool hold)
    {
        if (hold) SetIdle(true);
        else { IsAutoStartHeld = false; ArenaRoundNetHandler.SendStateToAll(); }
    }

    private static ArenasConfig Config => ModContent.GetInstance<ArenasConfig>();
    private static Loadout FindLoadout(string name) => string.IsNullOrWhiteSpace(name) ? null : (Config.ArenaLoadouts ?? []).FirstOrDefault(l => string.Equals(l?.Name, name, StringComparison.OrdinalIgnoreCase));
    private static bool IsTeam(Player p, Team team) => p?.active == true && (Team)p.team == team;
    private static bool TeamsReady()
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
        {
            Player player = Main.LocalPlayer;
            if (player?.active != true) return false;
            if ((Team)player.team != Team.Red && (Team)player.team != Team.Blue) player.team = (int)Team.Red;
            return true;
        }

        return Main.player.Any(p => IsTeam(p, Team.Red)) && Main.player.Any(p => IsTeam(p, Team.Blue));
    }

    private static void StartFreeze(int presetIndex, int countdownTicks = -1, int playingTicks = -1)
    {
        List<BossFightPreset> presets = GetValidPresets();
        if (!TeamsReady() || presetIndex < 0 || presetIndex >= presets.Count) { SetIdle(); return; }

        CleanupBoss();
        Phase = RoundPhase.FreezeCountdown; Result = RoundResult.None; RemainingTicks = countdownTicks >= 0 ? countdownTicks : Config.FreezeCountdownSeconds * 60; CurrentPresetIndex = presetIndex; LocalVote = -1;
        nextRoundTicks = playingTicks >= 0 ? playingTicks : Config.RoundDurationSeconds * 60; IsTimerPaused = IsAutoStartHeld = false;
        votes.Clear(); scoreboard.Clear(); participants.Clear();
        participants.AddRange(Main.player.Where(p => IsTeam(p, Team.Red) || IsTeam(p, Team.Blue)).Select(p => new RoundPlayerStats((byte)p.whoAmI, (Team)p.team, p.name, 0, 0, 0, 0)));

        Loadout loadout = FindLoadout(presets[presetIndex].LoadoutName);
        foreach (RoundPlayerStats entry in participants)
        {
            Player player = Main.player[entry.PlayerId];
            player.GetModPlayer<ArenaRoundPlayer>().ResetStats();
            LoadoutService.Apply(player, loadout);
            Teleport(player, ArenaGeometry.TeamSpawn(entry.Team));
            if (Main.netMode == NetmodeID.Server) ArenaRoundNetHandler.SendApplyKit(entry.PlayerId, presetIndex);
        }

        ResizeVotes(presets.Count);
        ArenaRoundNetHandler.SendStateToAll();
    }

    private static void TickFreeze()
    {
        foreach (Player player in Main.player.Where(p => p?.active == true)) player.AddBuff(BuffID.Frozen, 2);
        if (!IsTimerPaused && --RemainingTicks <= 0) StartPlaying();
    }

    private static void StartPlaying()
    {
        List<BossFightPreset> presets = GetValidPresets();
        if (CurrentPresetIndex < 0 || CurrentPresetIndex >= presets.Count) { SetIdle(); return; }
        TilePoint spawn = ResolveBossSpawn();
        bossType = presets[CurrentPresetIndex].Boss.Type;
        bossIndex = NPC.NewNPC(new EntitySource_Misc("ArenasRound"), spawn.X * 16 + 8, spawn.Y * 16, bossType);
        if (bossIndex < 0 || bossIndex >= Main.maxNPCs || !Main.npc[bossIndex].active) { EndRound(RoundResult.SpawnFailed); return; }
        ConstrainBosses();
        Main.npc[bossIndex].netUpdate = true;
        Phase = RoundPhase.Playing; RemainingTicks = Math.Max(0, nextRoundTicks); IsTimerPaused = false;
        bossLife = Main.npc[bossIndex].life; bossLifeMax = Main.npc[bossIndex].lifeMax;
        ArenaRoundNetHandler.SendStateToAll();
    }

    private static void TickPlaying()
    {
        if (bossIndex < 0 || bossIndex >= Main.maxNPCs) { EndRound(RoundResult.BossDespawned); return; }
        NPC boss = Main.npc[bossIndex];
        if (boss.life <= 0) { EndRound(RoundResult.BossDefeated); return; }
        if (!boss.active || boss.type != bossType) { EndRound(RoundResult.BossDespawned); return; }
        ConstrainBosses();
        bossLife = boss.life; bossLifeMax = boss.lifeMax;
        if (!IsTimerPaused && --RemainingTicks <= 0) EndRound(RoundResult.TimeExpired);
    }

    private static void EndRound(RoundResult result)
    {
        Result = result;
        scoreboard.Clear(); scoreboard.AddRange(LiveStats());
        CleanupBoss(); votes.Clear(); ResizeVotes(GetValidPresets().Count);
        Phase = RoundPhase.Voting; RemainingTicks = Config.VotingDurationSeconds * 60; LocalVote = -1; IsTimerPaused = false;
        EndScreen.EndScreenSystem.SendMatchEndSnapshots();
        ArenaRoundNetHandler.SendStateToAll();
    }

    private static void TickVoting()
    {
        List<BossFightPreset> presets = GetValidPresets();
        if (presets.Count == 0) { SetIdle(); return; }
        if (IsTimerPaused || --RemainingTicks > 0) return;
        ResolveVoting();
    }

    private static void ResolveVoting()
    {
        List<BossFightPreset> presets = GetValidPresets();
        if (presets.Count == 0) { SetIdle(); return; }
        if (!TeamsReady()) { SetIdle(); return; }
        int best = voteCounts.Count == 0 ? 0 : voteCounts.Max();
        List<int> choices = Enumerable.Range(0, presets.Count).Where(i => best == 0 || voteCounts[i] == best).ToList();
        StartFreeze(choices[Main.rand.Next(choices.Count)]);
    }

    private static void SetIdle(bool hold = false)
    {
        CleanupBoss(); Phase = RoundPhase.Idle; Result = RoundResult.None; RemainingTicks = Config.RoundDurationSeconds * 60; CurrentPresetIndex = 0; LocalVote = -1;
        IsTimerPaused = false; IsAutoStartHeld = hold; nextRoundTicks = Config.RoundDurationSeconds * 60;
        votes.Clear(); voteCounts.Clear(); voteVoters.Clear(); participants.Clear(); scoreboard.Clear();
        ArenaRoundNetHandler.SendStateToAll();
    }

    private static void Reset(bool cleanup)
    {
        if (cleanup && Main.netMode != NetmodeID.MultiplayerClient) CleanupBoss();
        Phase = RoundPhase.Idle; Result = RoundResult.None; RemainingTicks = Config?.RoundDurationSeconds * 60 ?? 36000; CurrentPresetIndex = 0; LocalVote = -1; bossIndex = -1; bossType = bossLife = bossLifeMax = 0;
        IsTimerPaused = IsAutoStartHeld = false; nextRoundTicks = RemainingTicks;
        votes.Clear(); voteCounts.Clear(); voteVoters.Clear(); participants.Clear(); scoreboard.Clear();
    }

    private static void CleanupBoss()
    {
        if (bossIndex >= 0 && bossIndex < Main.maxNPCs && Main.npc[bossIndex].active)
        {
            Main.npc[bossIndex].active = false;
            if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, number: bossIndex);
        }
        bossIndex = -1; bossType = bossLife = bossLifeMax = 0;
    }

    private static TilePoint ResolveBossSpawn() => Config.BossSpawn?.X >= 0 && Config.BossSpawn.Y >= 0
        ? Config.BossSpawn : new TilePoint { X = ArenaGeometry.BossTileArea.Center.X, Y = ArenaGeometry.BossTileArea.Center.Y };

    private static void Teleport(Player player, Point tile)
    {
        Vector2 position = new(tile.X * 16, tile.Y * 16 - player.height);
        player.Teleport(position, TeleportationStyleID.RodOfDiscord);
        if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.TeleportEntity, number: 0, number2: player.whoAmI, number3: position.X, number4: position.Y, number5: TeleportationStyleID.RodOfDiscord);
    }

    private static void ConstrainBosses()
    {
        Rectangle area = ArenaGeometry.BossWorldArea;
        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC npc = Main.npc[i];
            if (!npc.active || (!npc.boss && npc.whoAmI != bossIndex && npc.realLife != bossIndex)) continue;
            float minX = area.Left, minY = area.Top, maxX = Math.Max(minX, area.Right - npc.width), maxY = Math.Max(minY, area.Bottom - npc.height);
            Vector2 next = new(MathHelper.Clamp(npc.position.X, minX, maxX), MathHelper.Clamp(npc.position.Y, minY, maxY));
            if (next == npc.position) continue;
            if (next.X != npc.position.X) npc.velocity.X = 0;
            if (next.Y != npc.position.Y) npc.velocity.Y = 0;
            npc.position = next; npc.netUpdate = true;
        }
    }

    private static void ResizeVotes(int count) { voteCounts.Clear(); voteVoters.Clear(); for (int i = 0; i < count; i++) { voteCounts.Add(0); voteVoters.Add([]); } }
    private static void SetRemaining(int seconds, int maxSeconds) { RemainingTicks = Math.Clamp(seconds, 0, maxSeconds) * 60; ArenaRoundNetHandler.SendStateToAll(); }
    private static void CountVotes()
    {
        ResizeVotes(GetValidPresets().Count);
        foreach ((int player, int vote) in votes) if (vote >= 0 && vote < voteCounts.Count) { voteCounts[vote]++; voteVoters[vote].Add((byte)player); }
    }
    private static List<RoundPlayerStats> LiveStats() => participants.Select(p =>
    {
        ArenaRoundPlayer stats = Main.player[p.PlayerId].GetModPlayer<ArenaRoundPlayer>();
        return p with { Kills = stats.Kills, Deaths = stats.Deaths, Damage = stats.Damage, BossDamage = stats.BossDamage };
    }).ToList();
}
