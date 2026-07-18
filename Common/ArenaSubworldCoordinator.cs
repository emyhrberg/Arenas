using Arenas.Common.Rounds;
using SubworldLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader.IO;

namespace Arenas.Common;

internal readonly record struct ArenaSubworldRequest(int PresetIndex, int Seed, int CountdownTicks, int PlayingTicks, int GenerationId, int ExpectedPlayers)
{
    public static ArenaSubworldRequest Default => new(0, Environment.TickCount, -1, -1, 1, 1);
}

/// <summary>
/// Coordinates disposable arena subworlds. In multiplayer this code runs on the main server and
/// passes the request into the freshly launched subserver through Subworld Library's copied data.
/// </summary>
internal sealed class ArenaSubworldCoordinator : ModSystem, ICopyWorldData
{
    private const string RequestKey = "!Arenas.RoundRequest";
    private const int ReturnTimeoutTicks = 5 * 60;
    private const int RestartDelayTicks = 15;

    private enum TransitionState : byte { None, WaitingForMainWorld, RestartDelay }

    private static readonly HashSet<int> roster = [];
    private static readonly Dictionary<int, Team> rosterTeams = [];
    private static ArenaSubworldRequest pendingRequest = ArenaSubworldRequest.Default;
    private static ArenaSubworldRequest activeRequest = ArenaSubworldRequest.Default;
    private static TransitionState transitionState;
    private static int transitionTicks;
    private static bool bootstrapPending;
    private static int bootstrapTicks;
    private static int lastReadyPlayers = -1;
    private static bool arenaSubserverRunning;
    private static string sscWorldScope = "";

    internal static ArenaSubworldRequest ActiveRequest => activeRequest;
    internal static bool IsTransitioning => transitionState != TransitionState.None;
    internal static string SscWorldScope => sscWorldScope;

    public override void OnWorldUnload()
    {
        // Static transition data intentionally survives a normal Subworld Library world swap.
    }

    public override void PostUpdateEverything()
    {
        if (SubworldSystem.IsActive<ArenasSubworld>())
        {
            TickSubworldBootstrap();
            return;
        }

        if (Main.netMode == NetmodeID.MultiplayerClient || SubworldSystem.AnyActive() || transitionState == TransitionState.None)
            return;

        transitionTicks++;
        if (transitionState == TransitionState.WaitingForMainWorld)
        {
            ApplyRosterTeams();
            bool everyoneReturned = roster.All(id => id < 0 || id >= Main.maxPlayers || Main.player[id]?.active == true);
            if (!everyoneReturned && transitionTicks < ReturnTimeoutTicks)
                return;

            int index = SubworldSystem.GetIndex<ArenasSubworld>();
            if (index >= 0)
                SubworldSystem.StopSubserver(index);
            arenaSubserverRunning = false;
            Log.Debug("[ArenaFlow6] Stopped the previous arena subserver before creating the next round.");
            transitionState = TransitionState.RestartDelay;
            transitionTicks = 0;
            return;
        }

        if (transitionTicks < RestartDelayTicks)
            return;

        transitionState = TransitionState.None;
        MoveRosterIntoArena();
    }

    internal static bool StartRound(int presetIndex, int countdownTicks, int playingTicks, int generationId)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return false;

        if (SubworldSystem.IsActive<ArenasSubworld>())
        {
            int expected = Math.Max(1, CountReadyPlayers(presetIndex));
            CaptureTeamsFromActivePlayers();
            pendingRequest = new ArenaSubworldRequest(presetIndex, Main.rand.Next(), countdownTicks, playingTicks, Math.Max(1, generationId), expected);
            Log.Debug($"[ArenaFlow1] Requested a fresh arena from inside the current subworld. preset={presetIndex}, expectedPlayers={expected}.");
            if (Main.netMode == NetmodeID.SinglePlayer)
            {
                transitionState = TransitionState.RestartDelay;
                transitionTicks = 0;
                SubworldSystem.Exit();
                return true;
            }

            SendRestartRequestToMainServer(pendingRequest);
            return true;
        }

        if (SubworldSystem.AnyActive())
            return false;

        CaptureRoster(presetIndex);
        pendingRequest = new ArenaSubworldRequest(presetIndex, Main.rand.Next(), countdownTicks, playingTicks, Math.Max(1, generationId), Math.Max(1, roster.Count));
        Log.Debug($"[ArenaFlow1] Launching an arena subworld. preset={presetIndex}, seed={pendingRequest.Seed}, expectedPlayers={pendingRequest.ExpectedPlayers}.");
        if (Main.netMode == NetmodeID.SinglePlayer)
        {
            activeRequest = pendingRequest;
            return SubworldSystem.Enter<ArenasSubworld>();
        }

        MoveRosterIntoArena();
        return true;
    }

    internal static void HandlePacket(BinaryReader reader, int fromWho)
    {
        ArenaSubworldRequest request = new(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
        int teamCount = Math.Clamp(reader.ReadInt32(), 0, Main.maxPlayers);
        Dictionary<int, Team> transferredTeams = [];
        for (int i = 0; i < teamCount; i++)
        {
            int playerId = reader.ReadByte();
            Team team = (Team)reader.ReadByte();
            if (playerId >= 0 && playerId < Main.maxPlayers && team is Team.Red or Team.Blue)
                transferredTeams[playerId] = team;
        }
        if (Main.netMode != NetmodeID.Server || SubworldSystem.AnyActive() || fromWho != 256)
            return;

        roster.Clear();
        rosterTeams.Clear();
        foreach ((int playerId, Team team) in transferredTeams)
        {
            roster.Add(playerId);
            rosterTeams[playerId] = team;
        }
        pendingRequest = request with { Seed = Main.rand.Next(), ExpectedPlayers = Math.Max(1, roster.Count) };
        Log.Debug($"[ArenaFlow5] Main server received the next-round request. preset={pendingRequest.PresetIndex}, expectedPlayers={pendingRequest.ExpectedPlayers}.");
        transitionState = TransitionState.WaitingForMainWorld;
        transitionTicks = 0;
        foreach (int playerId in roster)
            SubworldSystem.MovePlayerToMainWorld(playerId);
    }

    private static void SendRestartRequestToMainServer(ArenaSubworldRequest request)
    {
        using MemoryStream stream = new();
        using (BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, true))
        {
            writer.Write((byte)Arenas.ArenasPacketType.ArenaSubworld);
            writer.Write(request.PresetIndex);
            writer.Write(request.Seed);
            writer.Write(request.CountdownTicks);
            writer.Write(request.PlayingTicks);
            writer.Write(request.GenerationId);
            writer.Write(request.ExpectedPlayers);
            writer.Write(rosterTeams.Count);
            foreach ((int playerId, Team team) in rosterTeams)
            {
                writer.Write((byte)playerId);
                writer.Write((byte)team);
            }
        }
        SubworldSystem.SendToMainServer(ModContent.GetInstance<Arenas>(), stream.ToArray());
    }

    private static void CaptureRoster(int presetIndex)
    {
        roster.Clear();
        rosterTeams.Clear();
        sscWorldScope = Main.ActiveWorldFileData?.Name ?? Main.worldName ?? "";
        ArenaRoundSystem.AssignBalancedTeams();
        foreach (Player player in Main.player.Where(p => p?.active == true))
        {
            roster.Add(player.whoAmI);
            rosterTeams[player.whoAmI] = (Team)player.team;
        }
        if (Main.netMode == NetmodeID.SinglePlayer && Main.LocalPlayer?.active == true)
            roster.Add(Main.myPlayer);
    }

    private static void MoveRosterIntoArena()
    {
        ApplyRosterTeams();
        Log.Debug($"[ArenaFlow2] Moving {roster.Count} rostered player(s) into the Arenas subworld.");
        arenaSubserverRunning = true;
        if (Main.netMode == NetmodeID.SinglePlayer)
        {
            activeRequest = pendingRequest;
            bool entered = SubworldSystem.Enter<ArenasSubworld>();
            Log.Debug($"[ArenaFlow2] Single-player arena entry requested. accepted={entered}.");
            return;
        }

        foreach (int playerId in roster.Where(id => id >= 0 && id < Main.maxPlayers && Main.player[id]?.active == true).ToArray())
            SubworldSystem.MovePlayerToSubworld<ArenasSubworld>(playerId);
    }

    internal static void MoveFromMainToExistingArena(int targetPlayer)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
        {
            Log.Warn("Subworld Manager cannot create an arena. Start a fight with Arenas : Game Manager.");
            return;
        }
        if (Main.netMode != NetmodeID.Server || SubworldSystem.AnyActive())
            return;
        if (!arenaSubserverRunning)
        {
            Log.Warn("Subworld Manager refused to enter Arenas because no Game Manager arena is running.");
            return;
        }

        IEnumerable<int> targets = targetPlayer >= 0
            ? [targetPlayer]
            : Enumerable.Range(0, Main.maxPlayers).Where(id => Main.player[id]?.active == true);
        foreach (int playerId in targets.Where(id => id >= 0 && id < Main.maxPlayers && Main.player[id]?.active == true).ToArray())
        {
            ArenaRoundSystem.AssignBalancedTeams();
            roster.Add(playerId);
            Team team = (Team)Main.player[playerId].team;
            if (team is Team.Red or Team.Blue)
                rosterTeams[playerId] = team;
            Log.Debug($"[SubworldManager1] Moving player {playerId} into the existing arena subserver.");
            SubworldSystem.MovePlayerToSubworld<ArenasSubworld>(playerId);
        }
    }

    internal static void MoveFromArenaToMain(int targetPlayer)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
        {
            if (SubworldSystem.IsActive<ArenasSubworld>()) SubworldSystem.Exit();
            return;
        }
        if (Main.netMode != NetmodeID.Server || SubworldSystem.AnyActive())
            return;

        IEnumerable<int> targets = targetPlayer >= 0 ? [targetPlayer] : roster;
        foreach (int playerId in targets.Where(id => id >= 0 && id < Main.maxPlayers).ToArray())
        {
            Log.Debug($"[SubworldManager2] Moving player {playerId} back to the main world.");
            SubworldSystem.MovePlayerToMainWorld(playerId);
        }
    }

    internal static void QueueSubworldRoundStart()
    {
        bootstrapPending = true;
        bootstrapTicks = 0;
        lastReadyPlayers = -1;
        ArenaRoundSystem.PrepareSubworldRound(activeRequest.PresetIndex, activeRequest.GenerationId);
        Log.Debug($"[ArenaFlow3] Arena layout is loaded; waiting for players before round bootstrap. preset={activeRequest.PresetIndex}, expectedPlayers={activeRequest.ExpectedPlayers}.");
    }

    private static void TickSubworldBootstrap()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || !bootstrapPending || !ArenaWorldSystem.Active || !ArenaWorldSystem.WorldReady)
            return;

        bootstrapTicks++;
        int readyPlayers = CountReadyPlayers(activeRequest.PresetIndex);
        int expectedPlayers = Math.Max(1, activeRequest.ExpectedPlayers);
        if (readyPlayers != lastReadyPlayers)
        {
            lastReadyPlayers = readyPlayers;
            Log.Debug($"[ArenaFlow4] Arena player readiness changed: {readyPlayers}/{expectedPlayers} player(s) ready.");
        }

        if (readyPlayers <= 0)
            return;
        if (readyPlayers < expectedPlayers && bootstrapTicks < ReturnTimeoutTicks)
            return;
        if (readyPlayers < expectedPlayers)
            Log.Warn($"Arena bootstrap timed out waiting for players; starting with {readyPlayers}/{expectedPlayers} ready.");

        bootstrapPending = false;
        Log.Debug($"[ArenaFlow5] Starting round bootstrap. preset={activeRequest.PresetIndex}, readyPlayers={readyPlayers}.");
        ArenaRoundSystem.StartSubworldRound(activeRequest.PresetIndex, activeRequest.CountdownTicks, activeRequest.PlayingTicks, activeRequest.GenerationId);
    }

    private static int CountReadyPlayers(int presetIndex)
    {
        ApplyRosterTeams();
        ArenaRoundSystem.AssignBalancedTeams();
        CaptureTeamsFromActivePlayers();
        return Main.player.Count(player => player?.active == true);
    }

    private static void CaptureTeamsFromActivePlayers()
    {
        foreach (Player player in Main.player.Where(player => player?.active == true))
        {
            Team team = (Team)player.team;
            if (team is Team.Red or Team.Blue)
            {
                roster.Add(player.whoAmI);
                rosterTeams[player.whoAmI] = team;
            }
        }
    }

    private static void ApplyRosterTeams()
    {
        foreach ((int playerId, Team team) in rosterTeams)
        {
            if (playerId < 0 || playerId >= Main.maxPlayers || Main.player[playerId]?.active != true)
                continue;
            Player player = Main.player[playerId];
            if ((Team)player.team == team)
                continue;
            player.team = (int)team;
            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.PlayerTeam, -1, -1, null, playerId, player.team);
        }
    }

    public void CopyMainWorldData()
    {
        TagCompound request = new()
        {
            ["preset"] = pendingRequest.PresetIndex,
            ["seed"] = pendingRequest.Seed,
            ["countdown"] = pendingRequest.CountdownTicks,
            ["playing"] = pendingRequest.PlayingTicks,
            ["generation"] = pendingRequest.GenerationId,
            ["expectedPlayers"] = pendingRequest.ExpectedPlayers,
            ["teamIds"] = rosterTeams.Keys.ToList(),
            ["teams"] = rosterTeams.Values.Select(team => (int)team).ToList(),
            ["sscWorldScope"] = sscWorldScope
        };
        Log.Debug($"[WorldGen0] Copying round request to Subworld Library. preset={pendingRequest.PresetIndex}, seed={pendingRequest.Seed}, expectedPlayers={pendingRequest.ExpectedPlayers}.");
        SubworldSystem.CopyWorldData(RequestKey, request);
    }

    public void ReadCopiedMainWorldData()
    {
        TagCompound request = SubworldSystem.ReadCopiedWorldData<TagCompound>(RequestKey);
        activeRequest = request == null
            ? pendingRequest
            : new ArenaSubworldRequest(request.GetInt("preset"), request.GetInt("seed"), request.GetInt("countdown"), request.GetInt("playing"), request.GetInt("generation"), Math.Max(1, request.GetInt("expectedPlayers")));
        roster.Clear();
        rosterTeams.Clear();
        if (request != null)
        {
            IList<int> ids = request.GetList<int>("teamIds");
            IList<int> teams = request.GetList<int>("teams");
            for (int i = 0; i < Math.Min(ids.Count, teams.Count); i++)
            {
                Team team = (Team)teams[i];
                if (ids[i] is >= 0 and < Main.maxPlayers && team is Team.Red or Team.Blue)
                {
                    roster.Add(ids[i]);
                    rosterTeams[ids[i]] = team;
                }
            }
            sscWorldScope = request.GetString("sscWorldScope");
        }
        Log.Debug($"[WorldGen0] Read round request inside the subworld. preset={activeRequest.PresetIndex}, seed={activeRequest.Seed}, expectedPlayers={activeRequest.ExpectedPlayers}.");
    }
}
