using Arenas.Core.Configs;
using Arenas.Core.Configs.ConfigElements;
using Arenas.Common.Generation;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;

namespace Arenas.Common.Rounds;

public enum RoundPhase : byte { Idle, Generating, FreezeCountdown, Playing, Voting }
public enum RoundResult : byte { None, BossDefeated, TimeExpired, BossDespawned, SpawnFailed, GenerationFailed, AdminEnded }
public readonly record struct RoundPlayerStats(byte PlayerId, Team Team, string Name, int Kills, int Deaths, long Damage, long BossDamage);

internal sealed class ArenaRoundSystem : ModSystem
{
    private sealed class RoundParticipant(string characterKey, byte playerId, Team team, string name)
    {
        public string CharacterKey { get; } = characterKey;
        public byte PlayerId { get; set; } = playerId;
        public Team Team { get; } = team;
        public string Name { get; set; } = name;
        public RoundPlayerStats Snapshot { get; set; } = new(playerId, team, name, 0, 0, 0, 0);
    }

    public const int MaxPresets = 7;
    private static readonly List<RoundParticipant> participants = [];
    private static readonly List<RoundParticipant> generationCandidates = [];
    private static readonly List<RoundPlayerStats> scoreboard = [];
    private static readonly Dictionary<int, int> votes = [];
    private static readonly List<int> voteCounts = [];
    private static readonly List<List<byte>> voteVoters = [];
    private static bool inArena;
    private static int bossIndex = -1, bossType, bossLife, bossLifeMax, nextRoundTicks;
    private static ArenaGenerationJob generationJob;
    private static int generationId, pendingCountdownTicks, pendingPlayingTicks;
    private static bool generationSynced, stopAfterGeneration, generationIsEmergency;
    private static float remoteGenerationProgress;

    public static RoundPhase Phase { get; private set; }
    public static RoundResult Result { get; private set; }
    public static int RemainingTicks { get; private set; }
    public static int CurrentPresetIndex { get; private set; }
    public static string CurrentRoundToken { get; private set; } = "";
    public static int LocalVote { get; private set; } = -1;
    public static bool IsTimerPaused { get; private set; }
    public static int BossLife => bossLife;
    public static int BossLifeMax => bossLifeMax;
    public static int GenerationId => generationId;
    public static float GenerationProgress => generationJob?.Progress ?? remoteGenerationProgress;
    public static IReadOnlyList<int> VoteCounts => voteCounts;
    public static IReadOnlyList<RoundPlayerStats> Scoreboard => Main.netMode == NetmodeID.MultiplayerClient || Phase == RoundPhase.Voting ? scoreboard : LiveStats();
    public static IReadOnlyList<byte> VotersFor(int preset) => preset >= 0 && preset < voteVoters.Count ? voteVoters[preset] : [];

    public override void OnWorldLoad() => Reset(false);
    public override void OnWorldUnload() => Reset(true);

    public override void PostUpdateEverything()
    {
        bool active = ArenaWorldSystem.Active;
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
            if (!ArenaWorldSystem.WorldReady || Phase is RoundPhase.Generating or RoundPhase.FreezeCountdown)
                Main.LocalPlayer.AddBuff(BuffID.Frozen, 2);
            if (!IsTimerPaused && Phase != RoundPhase.Idle && RemainingTicks > 0) RemainingTicks--;
            return;
        }

        if (!ArenaWorldSystem.WorldReady && Phase != RoundPhase.Generating)
        {
            HoldPlayersInLobby();
            return;
        }

        switch (Phase)
        {
            case RoundPhase.Idle: break;
            case RoundPhase.Generating: TickGenerating(); break;
            case RoundPhase.FreezeCountdown: TickFreeze(); break;
            case RoundPhase.Playing: TickPlaying(); break;
            case RoundPhase.Voting: TickVoting(); break;
        }

        if (Main.netMode == NetmodeID.Server && Main.GameUpdateCount % 60 == 0) ArenaRoundNetHandler.SendStateToAll();
    }

    public static List<BossFightPreset> GetValidPresets() => (Config.FightPresets ?? [])
        .Where(p => p?.Boss?.Type > 0 && p.Loadout != null && p.MaxHealth > 0 && p.MaxMana >= 0
            && p.RoundDurationSeconds > 0 && ArenaGeneratorRegistry.TryResolve(p, out _))
        .Take(MaxPresets)
        .ToList();

    public static string PresetName(BossFightPreset preset) => preset?.Boss?.DisplayName ?? "Boss";
    public static bool IsParticipant(int playerId)
    {
        if (playerId < 0 || playerId >= Main.maxPlayers)
            return false;

        ArenaRoundPlayer stats = Main.player[playerId]?.GetModPlayer<ArenaRoundPlayer>();
        return stats != null && participants.Any(p => p.PlayerId == playerId && p.CharacterKey == stats.CharacterKeyOrFallback());
    }

    internal static bool TryGetParticipantTeam(int playerId, out Team team)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            foreach (RoundPlayerStats entry in scoreboard)
                if (entry.PlayerId == playerId)
                {
                    team = entry.Team;
                    return true;
                }
        }
        RoundParticipant participant = participants.FirstOrDefault(p => p.PlayerId == playerId);
        team = participant?.Team ?? Team.None;
        return participant != null;
    }

    internal static bool ReassociateParticipant(Player player, string characterKey)
    {
        if (player == null || string.IsNullOrEmpty(characterKey) || Phase == RoundPhase.Idle)
            return false;

        RoundParticipant participant = participants.FirstOrDefault(p => p.CharacterKey == characterKey);
        if (participant == null)
        {
            RoundParticipant candidate = generationCandidates.FirstOrDefault(p => p.CharacterKey == characterKey);
            if (candidate == null) return false;
            candidate.PlayerId = (byte)player.whoAmI;
            candidate.Name = player.name;
            player.team = (int)candidate.Team;
            return Phase is RoundPhase.FreezeCountdown or RoundPhase.Playing
                ? ActivateLateParticipant(candidate, player)
                : Phase == RoundPhase.Generating;
        }

        participant.PlayerId = (byte)player.whoAmI;
        participant.Name = player.name;
        player.team = (int)participant.Team;
        return true;
    }
    public static bool TryGetCurrentPreset(out BossFightPreset preset)
    {
        List<BossFightPreset> presets = GetValidPresets();
        if (CurrentPresetIndex >= 0 && CurrentPresetIndex < presets.Count)
        {
            preset = presets[CurrentPresetIndex];
            return true;
        }

        preset = null;
        return false;
    }

    internal static BossFightPreset GetPresetOrDefault(int index)
    {
        List<BossFightPreset> presets = GetValidPresets();
        if (index >= 0 && index < presets.Count)
            return presets[index];
        return presets.Count > 0 ? presets[0] : null;
    }

    internal static Point TeamSpawn(Team team)
    {
        ArenaLayout layout = ArenaWorldSystem.Layout;
        return layout?.TeamSpawn(team) ?? ArenaGeneratorRegistry.StagingLobby.Center;
    }

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

    internal static void ApplyState(RoundPhase phase, RoundResult result, int ticks, int preset, int localVote, bool paused, int life, int lifeMax, bool clearing, float clearingProgress, int nextGenerationId, float progress, ArenaLayout layout, List<int> counts, List<List<byte>> voters, List<RoundPlayerStats> entries)
    {
        Phase = phase; Result = result; RemainingTicks = ticks; CurrentPresetIndex = preset; LocalVote = localVote;
        IsTimerPaused = paused; bossLife = life; bossLifeMax = lifeMax;
        generationId = nextGenerationId; generationSynced = progress >= 1f; remoteGenerationProgress = progress;
        ArenaWorldSystem.ApplyNetworkClearing(clearing, clearingProgress);
        if (layout != null) ArenaWorldSystem.ApplyNetworkLayout(layout);
        else if (phase == RoundPhase.Generating) ArenaWorldSystem.ApplyNetworkLayout(null);
        voteCounts.Clear(); voteCounts.AddRange(counts);
        voteVoters.Clear(); voteVoters.AddRange(voters);
        scoreboard.Clear(); scoreboard.AddRange(entries);
    }

    internal static void ApplyKit(int presetIndex)
    {
        List<BossFightPreset> presets = GetValidPresets();
        if (presetIndex >= 0 && presetIndex < presets.Count)
            LoadoutService.Apply(Main.LocalPlayer, presets[presetIndex]);
    }

    internal static void AdminStartRound(int presetIndex, int countdownSeconds, int roundSeconds)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || !ArenaWorldSystem.Active || !ArenaWorldSystem.WorldReady || ArenaWorldSystem.IsClearing) return;
        BeginGeneration(presetIndex, Math.Clamp(countdownSeconds, 0, 300) * 60, Math.Clamp(roundSeconds, 0, 3600) * 60);
    }

    internal static void AdminClearWorld()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || !ArenaWorldSystem.Active || ArenaWorldSystem.IsClearing) return;
        SetIdle();
        ArenaWorldSystem.BeginClearWorld();
    }

    internal static void AdminBalanceTeams()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || !ArenaWorldSystem.Active || Phase != RoundPhase.Idle) return;
        List<Player> players = Main.player.Where(player => player?.active == true).ToList();
        for (int i = players.Count - 1; i > 0; i--)
        {
            int swap = Main.rand.Next(i + 1);
            (players[i], players[swap]) = (players[swap], players[i]);
        }

        int redTarget = (players.Count + 1) / 2;
        for (int i = 0; i < players.Count; i++)
        {
            Player player = players[i];
            player.team = i < redTarget ? (int)Team.Red : (int)Team.Blue;
            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.PlayerTeam, -1, -1, null, player.whoAmI, player.team);
        }
        ArenaRoundNetHandler.SendStateToAll();
    }

    internal static void AdminSetCountdown(int seconds) { if (Phase == RoundPhase.FreezeCountdown) SetRemaining(seconds, 300); }
    internal static void AdminSetRoundTime(int seconds) { if (Phase == RoundPhase.Playing) SetRemaining(seconds, 3600); }
    internal static void AdminSetVotingTime(int seconds) { if (Phase == RoundPhase.Voting) SetRemaining(seconds, 300); }

    internal static void AdminTogglePause()
    {
        if (Phase is RoundPhase.Idle or RoundPhase.Generating) return;
        IsTimerPaused = !IsTimerPaused; ArenaRoundNetHandler.SendStateToAll();
    }

    internal static void AdminAdvancePhase()
    {
        if (Phase == RoundPhase.FreezeCountdown) StartPlaying();
        else if (Phase == RoundPhase.Voting) ResolveVoting();
    }

    internal static void AdminEndRound()
    {
        if (Phase == RoundPhase.Generating)
        {
            SetIdle(RoundResult.AdminEnded);
            ArenaWorldSystem.CancelGeneration();
            OnWorldClearCompleted();
        }
        else if (Phase is RoundPhase.FreezeCountdown or RoundPhase.Playing) EndRound(RoundResult.AdminEnded);
    }

    internal static void OnWorldClearCompleted()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient) return;
        Point spawn = ArenaGeneratorRegistry.WorldSpawn;
        foreach (Player player in Main.player.Where(player => player?.active == true))
        {
            player.immune = true;
            player.immuneTime = 120;
            player.noFallDmg = true;
            player.velocity = Vector2.Zero;
            Teleport(player, spawn);
        }
        ArenaRoundNetHandler.SendStateToAll();
    }

    private static ArenasConfig Config => ModContent.GetInstance<ArenasConfig>();
    private static ArenaTimingConfig TimingConfig => ModContent.GetInstance<ArenaTimingConfig>();
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

    private static void BeginGeneration(int presetIndex, int countdownTicks = -1, int playingTicks = -1)
    {
        List<BossFightPreset> presets = GetValidPresets();
        if (!TeamsReady() || presetIndex < 0 || presetIndex >= presets.Count || !ArenaGeneratorRegistry.TryResolve(presets[presetIndex], out IArenaGenerator generator))
        {
            SetIdle();
            return;
        }

        CleanupBoss();
        generationCandidates.Clear();
        generationCandidates.AddRange(Main.player
            .Where(p => IsTeam(p, Team.Red) || IsTeam(p, Team.Blue))
            .Select(p => new RoundParticipant(p.GetModPlayer<ArenaRoundPlayer>().CharacterKeyOrFallback(), (byte)p.whoAmI, (Team)p.team, p.name)));
        participants.Clear(); scoreboard.Clear(); CurrentRoundToken = "";
        ArenaWorldSystem.BeginGeneration();
        CurrentPresetIndex = presetIndex; Phase = RoundPhase.Generating; Result = RoundResult.None; LocalVote = -1;
        IsTimerPaused = false; votes.Clear(); generationSynced = false; stopAfterGeneration = generationIsEmergency = false;
        pendingCountdownTicks = countdownTicks; pendingPlayingTicks = playingTicks; generationId++;
        int seed = Main.rand.Next();
        try
        {
            generationJob = new ArenaGenerationJob(generator, seed);
        }
        catch (Exception exception)
        {
            Log.Warn($"Arena generation failed. generator={generator.Kind}, seed={seed}, stage=Initializing: {exception}");
            if (!TryStartEmergencyGeneration())
            {
                SetIdle(RoundResult.GenerationFailed);
                return;
            }
        }
        remoteGenerationProgress = 0f;
        Log.Info($"Starting arena generation. generator={generationJob.Layout.Generator}, seed={generationJob.Layout.Seed}, preset={presetIndex}");
        RemainingTicks = 0;

        foreach (Player player in Main.player.Where(p => IsTeam(p, Team.Red) || IsTeam(p, Team.Blue)))
            Teleport(player, ArenaGeneratorRegistry.StagingLobby.Center);

        ArenaRoundNetHandler.SendStateToAll();
    }

    private static void TickGenerating()
    {
        HoldPlayersInLobby();
        if (!generationSynced)
        {
            generationJob?.Tick();
            if (generationJob?.HasFailed == true)
            {
                Log.Warn($"Arena generation failed. generator={generationJob.Layout.Generator}, seed={generationJob.Layout.Seed}, stage={generationJob.FailedStage}: {generationJob.Error}");
                if (generationIsEmergency)
                {
                    SetIdle(RoundResult.GenerationFailed); return;
                }
                if (!TryStartEmergencyGeneration()) SetIdle(RoundResult.GenerationFailed);
                else ArenaRoundNetHandler.SendStateToAll();
                return;
            }

            if (generationJob?.IsComplete != true) return;
            ArenaWorldSystem.CompleteGeneration(generationJob.Layout);
            generationSynced = true; remoteGenerationProgress = 1f;
            if (!Main.dedServ) ArenaMapReveal.Reveal(generationJob.Layout);
            SyncGeneratedWorld();
            ArenaRoundNetHandler.SendStateToAll();
            FinishGeneration();
            return;
        }
    }

    private static void FinishGeneration()
    {
        if (stopAfterGeneration) { SetIdle(RoundResult.GenerationFailed); return; }
        generationJob = null;
        StartFreeze(CurrentPresetIndex, pendingCountdownTicks, pendingPlayingTicks);
    }

    private static bool IsCandidateConnected(RoundParticipant candidate)
    {
        int playerId = candidate.PlayerId;
        if (playerId < 0 || playerId >= Main.maxPlayers || Main.player[playerId]?.active != true) return false;
        return Main.player[playerId].GetModPlayer<ArenaRoundPlayer>().CharacterKeyOrFallback() == candidate.CharacterKey;
    }

    private static bool ActivateLateParticipant(RoundParticipant candidate, Player player)
    {
        if (candidate == null || player?.active != true || Phase is not (RoundPhase.FreezeCountdown or RoundPhase.Playing)) return false;
        RoundParticipant existing = participants.FirstOrDefault(p => p.CharacterKey == candidate.CharacterKey);
        if (existing != null)
        {
            existing.PlayerId = (byte)player.whoAmI;
            existing.Name = player.name;
            player.team = (int)existing.Team;
            return true;
        }
        if (!TryGetCurrentPreset(out BossFightPreset preset)) return false;

        candidate.PlayerId = (byte)player.whoAmI;
        candidate.Name = player.name;
        player.team = (int)candidate.Team;
        player.GetModPlayer<ArenaRoundPlayer>().ResetStats();
        participants.Add(candidate);
        LoadoutService.Apply(player, preset);
        Teleport(player, TeamSpawn(candidate.Team));
        if (Main.netMode == NetmodeID.Server) ArenaRoundNetHandler.SendApplyKit(player.whoAmI, CurrentPresetIndex);
        ArenaRoundNetHandler.SendStateToAll();
        return true;
    }

    private static void SyncGeneratedWorld()
    {
        if (Main.netMode != NetmodeID.Server) return;
        IEnumerable<int> targets = Enumerable.Range(0, Main.maxPlayers).Where(i => Main.player[i]?.active == true);
        int sectionsX = Netplay.GetSectionX(Main.maxTilesX - 1) + 1, sectionsY = Netplay.GetSectionY(Main.maxTilesY - 1) + 1;
        foreach (int client in targets)
            for (int sectionX = 0; sectionX < sectionsX; sectionX++)
                for (int sectionY = 0; sectionY < sectionsY; sectionY++)
                    NetMessage.SendSection(client, sectionX, sectionY);
        NetMessage.SendData(MessageID.WorldData);
    }

    private static void HoldPlayersInLobby()
    {
        Rectangle lobby = new(ArenaGeneratorRegistry.StagingLobby.X * 16, ArenaGeneratorRegistry.StagingLobby.Y * 16,
            ArenaGeneratorRegistry.StagingLobby.Width * 16, ArenaGeneratorRegistry.StagingLobby.Height * 16);
        foreach (Player player in Main.player.Where(p => p?.active == true))
        {
            player.AddBuff(BuffID.Frozen, 2); player.immune = true; player.immuneTime = 2; player.noFallDmg = true; player.velocity = Vector2.Zero;
            if (!lobby.Contains(player.Center.ToPoint())) Teleport(player, ArenaGeneratorRegistry.StagingLobby.Center);
        }
    }

    private static void StartFreeze(int presetIndex, int countdownTicks = -1, int playingTicks = -1)
    {
        List<BossFightPreset> presets = GetValidPresets();
        if (ArenaWorldSystem.Layout == null || presetIndex < 0 || presetIndex >= presets.Count) { SetIdle(); return; }
        BossFightPreset preset = presets[presetIndex];

        CleanupBoss();
        CurrentRoundToken = Guid.NewGuid().ToString("N");
        int defaultCountdownTicks = TimingConfig.UseFreezeCountdown ? Math.Max(0, TimingConfig.FreezeCountdownSeconds) * 60 : 0;
        Phase = RoundPhase.FreezeCountdown; Result = RoundResult.None; RemainingTicks = countdownTicks >= 0 ? countdownTicks : defaultCountdownTicks; CurrentPresetIndex = presetIndex; LocalVote = -1;
        nextRoundTicks = playingTicks >= 0 ? playingTicks : preset.RoundDurationSeconds * 60; IsTimerPaused = false;
        votes.Clear(); scoreboard.Clear(); participants.Clear();
        foreach (RoundParticipant candidate in generationCandidates)
        {
            if (!IsCandidateConnected(candidate)) continue;
            Player candidatePlayer = Main.player[candidate.PlayerId];
            candidatePlayer.team = (int)candidate.Team;
            candidatePlayer.GetModPlayer<ArenaRoundPlayer>().ResetStats();
            candidate.Name = candidatePlayer.name;
            participants.Add(candidate);
        }

        foreach (RoundParticipant entry in participants)
        {
            Player player = Main.player[entry.PlayerId];
            player.GetModPlayer<ArenaRoundPlayer>().ResetStats();
            LoadoutService.Apply(player, preset);
            Teleport(player, TeamSpawn(entry.Team));
            if (Main.netMode == NetmodeID.Server) ArenaRoundNetHandler.SendApplyKit(entry.PlayerId, presetIndex);
        }

        ResizeVotes(presets.Count);
        if (RemainingTicks <= 0)
        {
            StartPlaying();
            return;
        }

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
        BossFightPreset preset = presets[CurrentPresetIndex];
        ApplyFightTime(preset.Time);
        Point spawn = ResolveBossSpawn();
        bossType = preset.Boss.Type;
        bossIndex = NPC.NewNPC(new EntitySource_Misc("ArenasRound"), spawn.X * 16 + 8, spawn.Y * 16, bossType);
        if (bossIndex < 0 || bossIndex >= Main.maxNPCs || !Main.npc[bossIndex].active) { EndRound(RoundResult.SpawnFailed); return; }
        ConstrainBosses();
        Main.npc[bossIndex].netUpdate = true;
        Phase = RoundPhase.Playing; RemainingTicks = Math.Max(0, nextRoundTicks); IsTimerPaused = false;
        bossLife = Main.npc[bossIndex].life; bossLifeMax = Main.npc[bossIndex].lifeMax;
        ArenaRoundNetHandler.SendStateToAll();
    }

    private static void ApplyFightTime(FightTime time)
    {
        if (time == FightTime.Unchanged)
            return;

        Main.dayTime = time == FightTime.Day;
        Main.time = 0d;

        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendData(MessageID.WorldData);
    }

    private static void TickPlaying()
    {
        if (TryGetCurrentPreset(out BossFightPreset preset)) MaintainFightTime(preset.Time);
        if (bossIndex < 0 || bossIndex >= Main.maxNPCs) { EndRound(RoundResult.BossDespawned); return; }
        NPC boss = Main.npc[bossIndex];
        if (boss.life <= 0) { EndRound(RoundResult.BossDefeated); return; }
        if (!boss.active || boss.type != bossType) { EndRound(RoundResult.BossDespawned); return; }
        ConstrainBosses();
        bossLife = boss.life; bossLifeMax = boss.lifeMax;
        if (!IsTimerPaused && --RemainingTicks <= 0) EndRound(RoundResult.TimeExpired);
    }

    private static void MaintainFightTime(FightTime time)
    {
        if (time == FightTime.Unchanged) return;
        bool shouldBeDay = time == FightTime.Day;
        if (Main.dayTime == shouldBeDay) return;
        Main.dayTime = shouldBeDay;
        Main.time = 0d;
        if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.WorldData);
    }

    private static void EndRound(RoundResult result)
    {
        Result = result;
        scoreboard.Clear(); scoreboard.AddRange(LiveStats());
        CleanupBoss(); votes.Clear(); ResizeVotes(GetValidPresets().Count);
        int votingSeconds = Math.Max(1, TimingConfig.VotingDurationSeconds);
        Phase = RoundPhase.Voting; RemainingTicks = votingSeconds * 60; LocalVote = -1; IsTimerPaused = false;
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
        BeginGeneration(choices[Main.rand.Next(choices.Count)]);
    }

    private static bool TryStartEmergencyGeneration()
    {
        int seed = Main.rand.Next();
        try
        {
            generationJob = new ArenaGenerationJob(ArenaGeneratorRegistry.Emergency, seed);
            ArenaWorldSystem.BeginGeneration();
            generationSynced = false;
            generationIsEmergency = stopAfterGeneration = true; Result = RoundResult.GenerationFailed;
            Log.Warn($"Building emergency flat arena. generator={generationJob.Layout.Generator}, seed={seed}; another round must be started manually.");
            return true;
        }
        catch (Exception exception)
        {
            Log.Error($"Emergency arena generation failed. generator={ArenaGeneratorRegistry.Emergency.Kind}, seed={seed}, stage=Initializing: {exception}");
            generationJob = null;
            return false;
        }
    }

    private static void SetIdle(RoundResult result = RoundResult.None)
    {
        int defaultTicks = DefaultRoundTicks();
        CleanupBoss(); Phase = RoundPhase.Idle; Result = result; RemainingTicks = defaultTicks; CurrentPresetIndex = 0; LocalVote = -1;
        CurrentRoundToken = "";
        IsTimerPaused = false; nextRoundTicks = defaultTicks;
        generationJob = null; generationSynced = false; remoteGenerationProgress = 0f; stopAfterGeneration = generationIsEmergency = false;
        votes.Clear(); voteCounts.Clear(); voteVoters.Clear(); participants.Clear(); generationCandidates.Clear(); scoreboard.Clear();
        ArenaRoundNetHandler.SendStateToAll();
    }

    private static void Reset(bool cleanup)
    {
        if (cleanup && Main.netMode != NetmodeID.MultiplayerClient) CleanupBoss();
        ArenaRoundNetHandler.ResetClientState();
        Phase = RoundPhase.Idle; Result = RoundResult.None; RemainingTicks = DefaultRoundTicks(); CurrentPresetIndex = 0; LocalVote = -1; bossIndex = -1; bossType = bossLife = bossLifeMax = 0;
        CurrentRoundToken = "";
        IsTimerPaused = false; nextRoundTicks = RemainingTicks;
        generationJob = null; generationSynced = false; remoteGenerationProgress = 0f; generationId = 0;
        pendingCountdownTicks = pendingPlayingTicks = 0; stopAfterGeneration = generationIsEmergency = false;
        votes.Clear(); voteCounts.Clear(); voteVoters.Clear(); participants.Clear(); generationCandidates.Clear(); scoreboard.Clear();
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

    private static int DefaultRoundTicks() => Math.Max(1, GetPresetOrDefault(0)?.RoundDurationSeconds ?? 600) * 60;

    private static Point ResolveBossSpawn() => ArenaWorldSystem.Layout?.BossSpawn ?? ArenaGeneratorRegistry.ArenaArea.Center;

    private static void Teleport(Player player, Point tile)
    {
        Vector2 position = new(tile.X * 16, tile.Y * 16 - player.height);
        player.Teleport(position, TeleportationStyleID.RodOfDiscord);
        if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.TeleportEntity, number: 0, number2: player.whoAmI, number3: position.X, number4: position.Y, number5: TeleportationStyleID.RodOfDiscord);
    }

    private static void ConstrainBosses()
    {
        if (ArenaWorldSystem.Layout == null) return;
        Rectangle tiles = ArenaWorldSystem.Layout.BossArea;
        Rectangle area = new(tiles.X * 16, tiles.Y * 16, tiles.Width * 16, tiles.Height * 16);
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
    private static List<RoundPlayerStats> LiveStats()
    {
        List<RoundPlayerStats> result = [];

        foreach (RoundParticipant participant in participants)
        {
            Player player = Main.player[participant.PlayerId];
            ArenaRoundPlayer stats = player?.GetModPlayer<ArenaRoundPlayer>();

            if (player?.active == true && stats != null && stats.CharacterKeyOrFallback() == participant.CharacterKey)
            {
                participant.Name = player.name;
                participant.Snapshot = new RoundPlayerStats(
                    participant.PlayerId,
                    participant.Team,
                    participant.Name,
                    stats.Kills,
                    stats.Deaths,
                    stats.Damage,
                    stats.BossDamage);
            }

            result.Add(participant.Snapshot with
            {
                PlayerId = participant.PlayerId,
                Team = participant.Team,
                Name = participant.Name
            });
        }

        return result;
    }
}
