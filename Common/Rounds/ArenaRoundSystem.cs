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

internal enum RoundPhase : byte { Idle, Generating, Ready, Sandbox, FreezeCountdown, Playing, Voting }
internal enum RoundResult : byte { None, BossDefeated, TimeExpired, BossDespawned, SpawnFailed, GenerationFailed, AdminEnded }
internal readonly record struct RoundPlayerStats(byte PlayerId, Team Team, string Name, int Kills, int Deaths, long Damage, long BossDamage);

internal sealed class ArenaRoundSystem : ModSystem
{
    private sealed class RoundParticipant(string characterKey, byte playerId, Team team, string name)
    {
        public string CharacterKey { get; } = characterKey;
        public byte PlayerId { get; set; } = playerId;
        public Team Team { get; set; } = team;
        public string Name { get; set; } = name;
        public RoundPlayerStats Snapshot { get; set; } = new(playerId, team, name, 0, 0, 0, 0);
    }

    public const int MaxPresets = 8;
    private static readonly List<RoundParticipant> participants = [];
    private static readonly List<RoundParticipant> generationCandidates = [];
    private static readonly List<RoundPlayerStats> scoreboard = [];
    private static readonly Dictionary<int, int> votes = [];
    private static readonly List<int> voteCounts = [];
    private static readonly List<List<byte>> voteVoters = [];
    private static bool inArena;
    private static int bossIndex = -1, bossType, nextRoundTicks;
    private static int bossActiveTicks, bossMissingTicks, bossRespawnAttempts;
    private static bool bossDefeatedSignal;
    private static int generationId;
    private static float remoteGenerationProgress;

    private const int BossStartupGraceTicks = 3 * 60;
    private const int BossDespawnConfirmTicks = 30;
    private const int MaxBossSpawnAttempts = 3;

    public static RoundPhase Phase { get; private set; }
    public static RoundResult Result { get; private set; }
    public static int RemainingTicks { get; private set; }
    public static int CurrentPresetIndex { get; private set; }
    public static string CurrentRoundToken { get; private set; } = "";
    public static int LocalVote { get; private set; } = -1;
    public static bool IsTimerPaused { get; private set; }
    public static int GenerationId => generationId;
    public static float GenerationProgress => remoteGenerationProgress;
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
            case RoundPhase.Ready: break;
            case RoundPhase.Sandbox: break;
            case RoundPhase.FreezeCountdown: TickFreeze(); break;
            case RoundPhase.Playing: TickPlaying(); break;
            case RoundPhase.Voting: TickVoting(); break;
        }

        if (Main.netMode == NetmodeID.Server && Main.GameUpdateCount % 60 == 0) ArenaRoundNetHandler.SendStateToAll();
    }

    public static List<BossFightPreset> GetValidPresets() => (Config.FightPresets ?? [])
        .Where(p => p != null && p.Loadout != null && p.MaxHealth > 0 && p.MaxMana >= 0
            && p.RoundDurationSeconds > 0 && (IsSandboxPreset(p) || p.Boss?.Type > 0)
            && ArenaGeneratorRegistry.TryResolve(p, out _))
        .Take(MaxPresets)
        .ToList();

    public static string PresetName(BossFightPreset preset) => !string.IsNullOrWhiteSpace(preset?.Name)
        ? preset.Name.Trim()
        : preset?.Boss?.DisplayName ?? "Boss";
    public static bool IsSandboxPreset(BossFightPreset preset) => preset?.ArenaGenerator == ArenaGeneratorKind.SandboxWorld;
    public static bool IsSandboxActive => ArenaWorldSystem.Active && Phase == RoundPhase.Sandbox
        && TryGetCurrentPreset(out BossFightPreset preset) && IsSandboxPreset(preset);
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
                : Phase is RoundPhase.Generating or RoundPhase.Ready;
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
        return layout?.TeamSpawn(team) ?? new Point(Math.Max(1, Main.maxTilesX / 2), Math.Max(1, Main.maxTilesY / 2));
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

    internal static void ApplyState(RoundPhase phase, RoundResult result, int ticks, int preset, int localVote, bool paused, int nextGenerationId, float progress, ArenaLayout layout, List<int> counts, List<List<byte>> voters, List<RoundPlayerStats> entries)
    {
        Phase = phase; Result = result; RemainingTicks = ticks; CurrentPresetIndex = preset; LocalVote = localVote;
        if (Main.netMode == NetmodeID.MultiplayerClient && phase != RoundPhase.Playing)
        {
            bossIndex = -1;
            bossType = 0;
        }
        IsTimerPaused = paused;
        generationId = nextGenerationId; remoteGenerationProgress = progress;
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
        {
            if (IsSandboxActive)
                Main.LocalPlayer.GetModPlayer<ArenaPlayer>().SandboxLoadoutPresetIndex = presetIndex;
            LoadoutService.Apply(Main.LocalPlayer, presets[presetIndex]);
        }
    }

    internal static void AdminPrepareArena(int presetIndex)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient) return;
        BeginGeneration(presetIndex);
    }

    internal static void AdminStartFight(int presetIndex, int countdownSeconds, int roundSeconds)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || !ArenaWorldSystem.Active || !ArenaWorldSystem.WorldReady
            || Phase is not (RoundPhase.Ready or RoundPhase.FreezeCountdown or RoundPhase.Playing)
            || presetIndex != CurrentPresetIndex)
            return;

        List<BossFightPreset> presets = GetValidPresets();
        if (presetIndex < 0 || presetIndex >= presets.Count || IsSandboxPreset(presets[presetIndex]))
            return;

        CaptureGenerationCandidates();
        int countdownTicks = Math.Clamp(countdownSeconds, 0, 300) * 60;
        int safeRoundSeconds = roundSeconds > 0 ? Math.Clamp(roundSeconds, 1, 3600) : Math.Max(1, presets[presetIndex].RoundDurationSeconds);
        StartFreeze(presetIndex, countdownTicks, safeRoundSeconds * 60);
    }

    internal static void AdminBalanceTeams()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || SubworldLibrary.SubworldSystem.AnyActive() && !ArenaWorldSystem.Active
            || Phase is not (RoundPhase.Idle or RoundPhase.Ready)) return;
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
        if (Phase == RoundPhase.Ready)
            RefreshPreparedTeams();
        ArenaRoundNetHandler.SendStateToAll();
    }

    internal static void AdminSetCountdown(int seconds) { if (Phase == RoundPhase.FreezeCountdown) SetRemaining(seconds, 300); }
    internal static void AdminSetRoundTime(int seconds) { if (Phase == RoundPhase.Playing) SetRemaining(seconds, 3600); }
    internal static void AdminSetVotingTime(int seconds) { if (Phase == RoundPhase.Voting) SetRemaining(seconds, 300); }

    internal static void AdminTogglePause()
    {
        if (Phase is RoundPhase.Idle or RoundPhase.Generating or RoundPhase.Ready) return;
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
        }
        else if (Phase is RoundPhase.FreezeCountdown or RoundPhase.Playing) EndRound(RoundResult.AdminEnded);
    }

    private static ArenasConfig Config => ModContent.GetInstance<ArenasConfig>();
    internal static void AssignBalancedTeams()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        List<Player> active = Main.player
            .Where(player => player?.active == true)
            .OrderBy(player => player.whoAmI)
            .ToList();
        if (active.Count == 0)
            return;

        int red = active.Count(player => (Team)player.team == Team.Red);
        int blue = active.Count(player => (Team)player.team == Team.Blue);
        foreach (Player player in active.Where(player => (Team)player.team is not (Team.Red or Team.Blue)))
        {
            Team team = red <= blue ? Team.Red : Team.Blue;
            SetPlayerTeam(player, team);
            if (team == Team.Red) red++; else blue++;
        }

        while (Math.Abs(red - blue) > 1)
        {
            Team larger = red > blue ? Team.Red : Team.Blue;
            Team smaller = larger == Team.Red ? Team.Blue : Team.Red;
            Player player = active.Last(p => (Team)p.team == larger);
            SetPlayerTeam(player, smaller);
            if (larger == Team.Red) { red--; blue++; } else { blue--; red++; }
        }
    }

    private static void SetPlayerTeam(Player player, Team team)
    {
        if ((Team)player.team == team)
            return;

        player.team = (int)team;
        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendData(MessageID.PlayerTeam, -1, -1, null, player.whoAmI, player.team);
    }

    private static bool HasPlayers()
    {
        AssignBalancedTeams();
        return Main.player.Any(player => player?.active == true);
    }

    private static void BeginGeneration(int presetIndex, int countdownTicks = -1, int playingTicks = -1)
    {
        List<BossFightPreset> presets = GetValidPresets();
        bool validIndex = presetIndex >= 0 && presetIndex < presets.Count;
        bool playersReady = HasPlayers();
        IArenaGenerator generator = null;
        bool hasGenerator = validIndex && ArenaGeneratorRegistry.TryResolve(presets[presetIndex], out generator);
        Log.Debug($"[ArenaFlow0] Begin generation requested preset={presetIndex} validPresets={presets.Count} playersReady={playersReady} generator={(hasGenerator ? generator.Kind.ToString() : "none")} netMode={Main.netMode}");
        if (!playersReady || !validIndex || !hasGenerator)
        {
            Log.Warn($"Arena start rejected: playersReady={playersReady}, validPreset={validIndex}, hasGenerator={hasGenerator}");
            SetIdle();
            return;
        }

        CleanupBoss();
        participants.Clear(); generationCandidates.Clear(); scoreboard.Clear(); CurrentRoundToken = "";
        CurrentPresetIndex = presetIndex; Phase = RoundPhase.Generating; Result = RoundResult.None; LocalVote = -1;
        IsTimerPaused = false; votes.Clear(); generationId++;
        RemainingTicks = 0;
        remoteGenerationProgress = 0f;
        if (!ArenaSubworldCoordinator.PrepareArena(presetIndex, countdownTicks, playingTicks, generationId))
        {
            SetIdle(RoundResult.GenerationFailed);
            return;
        }
        ArenaRoundNetHandler.SendStateToAll();
    }

    private static void TickGenerating()
    {
        HoldPlayersInLobby();
    }

    internal static void PrepareSubworldRound(int presetIndex, int nextGenerationId)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || ArenaWorldSystem.Layout == null)
            return;

        CleanupBoss();
        participants.Clear();
        generationCandidates.Clear();
        scoreboard.Clear();
        votes.Clear();
        CurrentRoundToken = "";
        CurrentPresetIndex = presetIndex;
        generationId = Math.Max(1, nextGenerationId);
        Phase = RoundPhase.Generating;
        Result = RoundResult.None;
        RemainingTicks = 0;
        LocalVote = -1;
        IsTimerPaused = false;
        remoteGenerationProgress = 1f;
        Log.Debug($"[ArenaFlow3] Round state prepared while the subworld waits for participants. preset={presetIndex}, generationId={generationId}.");
        ArenaRoundNetHandler.SendStateToAll();
    }

    internal static void MarkSubworldReady(int presetIndex, int nextGenerationId)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || !ArenaWorldSystem.Active || !ArenaWorldSystem.WorldReady)
            return;

        generationId = Math.Max(1, nextGenerationId);
        remoteGenerationProgress = 1f;
        CaptureGenerationCandidates();
        List<BossFightPreset> presets = GetValidPresets();
        bool sandbox = presetIndex >= 0 && presetIndex < presets.Count && IsSandboxPreset(presets[presetIndex]);
        Log.Debug($"[ArenaFlow6] Arena is ready with {generationCandidates.Count} player candidate(s). preset={presetIndex}.");
        if (sandbox)
        {
            StartSandbox(presetIndex);
            return;
        }

        CleanupBoss();
        CurrentPresetIndex = presetIndex;
        CurrentRoundToken = "";
        Phase = RoundPhase.Ready;
        Result = RoundResult.None;
        RemainingTicks = 0;
        LocalVote = -1;
        IsTimerPaused = false;
        participants.Clear();
        scoreboard.Clear();
        votes.Clear();
        ResizeVotes(presets.Count);
        foreach (RoundParticipant candidate in generationCandidates)
        {
            if (!IsCandidateConnected(candidate)) continue;
            participants.Add(candidate);
            Teleport(Main.player[candidate.PlayerId], TeamSpawn(candidate.Team));
        }
        Log.Debug($"[ArenaFlow7] Arena prepared for {participants.Count} player(s); waiting for the host to start the fight.");
        ArenaRoundNetHandler.SendStateToAll();
    }

    private static void CaptureGenerationCandidates()
    {
        generationCandidates.Clear();
        AssignBalancedTeams();
        generationCandidates.AddRange(Main.player
            .Where(player => player?.active == true)
            .Select(player => new RoundParticipant(player.GetModPlayer<ArenaRoundPlayer>().CharacterKeyOrFallback(),
                (byte)player.whoAmI, (Team)player.team, player.name)));
    }

    private static void RefreshPreparedTeams()
    {
        foreach (RoundParticipant participant in participants)
            if (participant.PlayerId < Main.maxPlayers && Main.player[participant.PlayerId]?.active == true)
            {
                participant.Team = (Team)Main.player[participant.PlayerId].team;
                Teleport(Main.player[participant.PlayerId], TeamSpawn(participant.Team));
            }
        foreach (RoundParticipant candidate in generationCandidates)
            if (candidate.PlayerId < Main.maxPlayers && Main.player[candidate.PlayerId]?.active == true)
                candidate.Team = (Team)Main.player[candidate.PlayerId].team;
    }

    private static void StartSandbox(int presetIndex)
    {
        CleanupBoss();
        CurrentPresetIndex = presetIndex;
        CurrentRoundToken = Guid.NewGuid().ToString("N");
        Phase = RoundPhase.Sandbox;
        Result = RoundResult.None;
        RemainingTicks = 0;
        LocalVote = -1;
        IsTimerPaused = false;
        participants.Clear();
        scoreboard.Clear();

        Point spawn = ArenaWorldSystem.Layout?.RedSpawn ?? new Point(Main.spawnTileX, Main.spawnTileY);
        foreach (RoundParticipant candidate in generationCandidates)
        {
            if (!IsCandidateConnected(candidate)) continue;
            participants.Add(candidate);
            Teleport(Main.player[candidate.PlayerId], spawn);
        }

        Log.Debug($"[ArenaFlow7.Sandbox] Sandbox ready with {participants.Count} player(s). No loadout, countdown, boundaries, or boss were applied.");
        ArenaRoundNetHandler.SendStateToAll();
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

    private static void HoldPlayersInLobby()
    {
        Rectangle lobbyTiles = ArenaWorldSystem.Layout?.StagingLobby
            ?? new Rectangle(Math.Max(1, Main.maxTilesX / 2 - 10), Math.Max(1, Main.maxTilesY / 2 - 10), 20, 20);
        Rectangle lobby = new(lobbyTiles.X * 16, lobbyTiles.Y * 16, lobbyTiles.Width * 16, lobbyTiles.Height * 16);
        foreach (Player player in Main.player.Where(p => p?.active == true))
        {
            player.AddBuff(BuffID.Frozen, 2); player.immune = true; player.immuneTime = 2; player.noFallDmg = true; player.velocity = Vector2.Zero;
            if (!lobby.Contains(player.Center.ToPoint())) Teleport(player, lobbyTiles.Center);
        }
    }

    private static void StartFreeze(int presetIndex, int countdownTicks = -1, int playingTicks = -1)
    {
        List<BossFightPreset> presets = GetValidPresets();
        if (ArenaWorldSystem.Layout == null || presetIndex < 0 || presetIndex >= presets.Count) { SetIdle(); return; }
        BossFightPreset preset = presets[presetIndex];

        CleanupBoss();
        CurrentRoundToken = Guid.NewGuid().ToString("N");
        int defaultCountdownTicks = Config.UseFreezeCountdown ? Math.Max(0, Config.FreezeCountdownSeconds) * 60 : 0;
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

        Log.Debug($"[ArenaFlow7] Applied preset loadout and teleported {participants.Count} participant(s); freezeTicks={RemainingTicks}.");

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
        if (Phase != RoundPhase.FreezeCountdown)
            return;
        List<BossFightPreset> presets = GetValidPresets();
        if (CurrentPresetIndex < 0 || CurrentPresetIndex >= presets.Count) { SetIdle(); return; }
        BossFightPreset preset = presets[CurrentPresetIndex];
        if (!participants.Any(IsCandidateConnected))
        {
            Log.Warn("Fight start rejected because no prepared players are connected");
            Phase = RoundPhase.Ready;
            RemainingTicks = 0;
            ArenaRoundNetHandler.SendStateToAll();
            return;
        }

        ApplyFightTime(preset.Time);
        bossType = preset.Boss.Type;
        bossActiveTicks = bossMissingTicks = bossRespawnAttempts = 0;
        bossDefeatedSignal = false;
        if (!SpawnRoundBoss("initial")) { EndRound(RoundResult.SpawnFailed); return; }
        Phase = RoundPhase.Playing;
        RemainingTicks = Math.Max(60, nextRoundTicks);
        IsTimerPaused = false;
        ArenaRoundNetHandler.SendStateToAll();
    }

    private static bool SpawnRoundBoss(string reason)
    {
        Point spawn = ResolveBossSpawn();
        int target = FindBossTarget();
        if (target < 0)
            return false;

        int index = NPC.NewNPC(new EntitySource_Misc("ArenasRound"), spawn.X * 16 + 8, spawn.Y * 16, bossType);
        if (index < 0 || index >= Main.maxNPCs || Main.npc[index]?.active != true || Main.npc[index].type != bossType)
            return false;

        bossIndex = index;
        bossRespawnAttempts++;
        bossMissingTicks = 0;
        NPC boss = Main.npc[bossIndex];
        boss.target = target;
        boss.timeLeft = Math.Max(boss.timeLeft, 3600);
        boss.netAlways = true;
        boss.netUpdate = true;
        ConstrainBosses();
        Log.Debug($"[ArenaFlow8] Spawned boss type={bossType}, npcIndex={bossIndex}, target={target}, tilePosition={spawn}, reason={reason}, attempt={bossRespawnAttempts}");
        return true;
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
        bossActiveTicks++;
        if (TryGetCurrentPreset(out BossFightPreset preset)) MaintainFightTime(preset.Time);

        if (bossDefeatedSignal)
        {
            EndRound(RoundResult.BossDefeated);
            return;
        }

        if (!TryResolveRoundBoss(out NPC boss))
        {
            bossMissingTicks++;
            bool startup = bossActiveTicks < BossStartupGraceTicks;
            if (startup && bossMissingTicks >= 2 && bossRespawnAttempts < MaxBossSpawnAttempts && SpawnRoundBoss("startup recovery"))
                return;
            if (startup || bossMissingTicks < BossDespawnConfirmTicks)
                return;

            Log.Warn($"Round boss remained missing for {bossMissingTicks} ticks. type={bossType}, lastIndex={bossIndex}, attempts={bossRespawnAttempts}");
            EndRound(RoundResult.BossDespawned);
            return;
        }

        bossMissingTicks = 0;
        if (boss.life <= 0) { EndRound(RoundResult.BossDefeated); return; }
        MaintainBoss(boss);
        ConstrainBosses();
        if (!IsTimerPaused && RemainingTicks > 0)
            RemainingTicks--;
        if (!IsTimerPaused && RemainingTicks <= 0)
            EndRound(RoundResult.TimeExpired);
    }

    private static bool TryResolveRoundBoss(out NPC boss)
    {
        if (bossIndex >= 0 && bossIndex < Main.maxNPCs && Main.npc[bossIndex]?.active == true && Main.npc[bossIndex].type == bossType)
        {
            boss = Main.npc[bossIndex];
            return true;
        }

        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC candidate = Main.npc[i];
            if (candidate?.active != true || candidate.type != bossType || !candidate.boss)
                continue;
            bossIndex = i;
            boss = candidate;
            Log.Debug($"[ArenaFlow8] Reassociated round boss type={bossType} with npcIndex={bossIndex}");
            return true;
        }

        boss = null;
        return false;
    }

    private static void MaintainBoss(NPC boss)
    {
        int target = FindBossTarget();
        if (target >= 0 && (boss.target < 0 || boss.target >= Main.maxPlayers || Main.player[boss.target]?.active != true || Main.player[boss.target].dead))
        {
            boss.target = target;
            boss.netUpdate = true;
        }
        boss.timeLeft = Math.Max(boss.timeLeft, 3600);
    }

    private static int FindBossTarget()
    {
        foreach (RoundParticipant participant in participants)
            if (participant.PlayerId < Main.maxPlayers && Main.player[participant.PlayerId]?.active == true && !Main.player[participant.PlayerId].dead)
                return participant.PlayerId;
        foreach (RoundParticipant participant in participants)
            if (participant.PlayerId < Main.maxPlayers && Main.player[participant.PlayerId]?.active == true)
                return participant.PlayerId;
        return -1;
    }

    internal static bool IsPrimaryRoundBoss(NPC npc)
    {
        if (npc == null || Phase != RoundPhase.Playing)
            return false;
        if (bossIndex >= 0)
            return npc.whoAmI == bossIndex;

        // The server owns bossIndex, but that private runtime index is not part of the
        // round-state packet. Associate the configured primary when its NPC sync reaches
        // a multiplayer client so client-side vanilla AI cannot locally despawn Golem.
        if (Main.netMode == NetmodeID.MultiplayerClient && npc.active && npc.boss &&
            TryGetCurrentPreset(out BossFightPreset preset) && npc.type == preset.Boss.Type)
        {
            bossIndex = npc.whoAmI;
            bossType = npc.type;
            return true;
        }
        return false;
    }

    internal static bool IsRoundBoss(NPC npc)
    {
        if (npc == null || Phase != RoundPhase.Playing)
            return false;
        if (IsPrimaryRoundBoss(npc))
            return true;
        if (bossIndex < 0)
            return false;
        if (npc.realLife == bossIndex)
            return true;
        return bossType == NPCID.Golem && NPC.golemBoss == bossIndex && IsGolemPartType(npc.type);
    }

    internal static void NotifyBossKilled(NPC npc)
    {
        // Linked parts dying is normal for multipart bosses. The round ends only
        // when the configured primary NPC is actually killed.
        if (IsPrimaryRoundBoss(npc) || Phase == RoundPhase.Playing && npc?.boss == true && npc.type == bossType)
            bossDefeatedSignal = true;
    }

    private static bool IsGolemPartType(int type) =>
        type is NPCID.GolemHead or NPCID.GolemHeadFree or NPCID.GolemFistLeft or NPCID.GolemFistRight;

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
        int life = bossIndex >= 0 && bossIndex < Main.maxNPCs && Main.npc[bossIndex] != null ? Main.npc[bossIndex].life : -1;
        bool active = bossIndex >= 0 && bossIndex < Main.maxNPCs && Main.npc[bossIndex]?.active == true;
        Log.Debug($"[ArenaFlow9] Ending fight. result={result}, bossType={bossType}, bossIndex={bossIndex}, active={active}, life={life}, remainingTicks={RemainingTicks}, missingTicks={bossMissingTicks}, spawnAttempts={bossRespawnAttempts}");
        Result = result;
        scoreboard.Clear(); scoreboard.AddRange(LiveStats());
        CleanupBoss(); votes.Clear(); ResizeVotes(GetValidPresets().Count);
        int votingSeconds = Math.Max(1, Config.VotingDurationSeconds);
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
        if (!HasPlayers()) { SetIdle(); return; }
        int best = voteCounts.Count == 0 ? 0 : voteCounts.Max();
        List<int> choices = Enumerable.Range(0, presets.Count).Where(i => best == 0 || voteCounts[i] == best).ToList();
        BeginGeneration(choices[Main.rand.Next(choices.Count)]);
    }

    private static void SetIdle(RoundResult result = RoundResult.None)
    {
        int defaultTicks = DefaultRoundTicks();
        CleanupBoss(); Phase = RoundPhase.Idle; Result = result; RemainingTicks = defaultTicks; CurrentPresetIndex = 0; LocalVote = -1;
        CurrentRoundToken = "";
        IsTimerPaused = false; nextRoundTicks = defaultTicks;
        remoteGenerationProgress = 0f;
        votes.Clear(); voteCounts.Clear(); voteVoters.Clear(); participants.Clear(); generationCandidates.Clear(); scoreboard.Clear();
        ArenaRoundNetHandler.SendStateToAll();
    }

    private static void Reset(bool cleanup)
    {
        if (cleanup && Main.netMode != NetmodeID.MultiplayerClient) CleanupBoss();
        ArenaRoundNetHandler.ResetClientState();
        Phase = RoundPhase.Idle; Result = RoundResult.None; RemainingTicks = DefaultRoundTicks(); CurrentPresetIndex = 0; LocalVote = -1; bossIndex = -1; bossType = 0;
        CurrentRoundToken = "";
        IsTimerPaused = false; nextRoundTicks = RemainingTicks;
        remoteGenerationProgress = 0f; generationId = 0;
        votes.Clear(); voteCounts.Clear(); voteVoters.Clear(); participants.Clear(); generationCandidates.Clear(); scoreboard.Clear();
    }

    private static void CleanupBoss()
    {
        int roundBossIndex = bossIndex;
        int roundBossType = bossType;
        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC npc = Main.npc[i];
            bool owned = roundBossIndex >= 0 && (i == roundBossIndex || npc?.realLife == roundBossIndex)
                || roundBossType > 0 && npc?.boss == true && npc.type == roundBossType
                || roundBossType == NPCID.Golem && roundBossIndex >= 0 && NPC.golemBoss == roundBossIndex
                    && npc != null && IsGolemPartType(npc.type);
            if (npc?.active != true || !owned)
                continue;
            npc.active = false;
            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncNPC, number: i);
        }
        bossIndex = -1; bossType = 0;
        bossActiveTicks = bossMissingTicks = bossRespawnAttempts = 0;
        bossDefeatedSignal = false;
    }

    private static int DefaultRoundTicks() => Math.Max(1, GetPresetOrDefault(0)?.RoundDurationSeconds ?? 600) * 60;

    private static Point ResolveBossSpawn() => ArenaWorldSystem.Layout?.BossSpawn ?? new Point(Math.Max(1, Main.maxTilesX / 2), Math.Max(1, Main.maxTilesY / 2));

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
            if (!npc.active || !IsRoundBoss(npc)) continue;
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
