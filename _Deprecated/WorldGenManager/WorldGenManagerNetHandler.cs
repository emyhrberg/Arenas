//using Arenas.Common.Generation;
//using Arenas.Core.Compat;
//using System;
//using System.IO;
//using Terraria.ID;

//namespace Arenas.Common.AdminTools.WorldGenManager;

//internal enum WorldGenManagerPhase : byte { Idle, Evacuating, Starting, Generating, Ready, Failed }

//internal static class WorldGenManagerNetHandler
//{
//    private enum Packet : byte { Generate, RequestState, SyncState, TogglePreview }

//    internal static WorldGenManagerPhase Phase { get; private set; }
//    internal static int RequestId { get; private set; }
//    internal static int CompletedStep { get; private set; } = -1;
//    internal static bool MatchReady { get; private set; }
//    internal static bool ServerAvailable { get; private set; }
//    internal static string Status { get; private set; } = "No controlled generation has been requested.";
//    internal static bool Busy => Phase is WorldGenManagerPhase.Evacuating or WorldGenManagerPhase.Starting or WorldGenManagerPhase.Generating;

//    internal static void RequestClear() => Request(ArenaGenerationMode.ClearOnly, -1);
//    internal static void RequestThroughStep(int index) => Request(ArenaGenerationMode.ThroughStep, index);
//    internal static void RequestCompleteWorld() => Request(ArenaGenerationMode.Full, ArenaWorldGenerationCatalog.Steps.Length - 1);

//    internal static void RequestPreviewTransition()
//    {
//        if (Busy)
//            return;
//        if (Main.netMode == NetmodeID.SinglePlayer)
//        {
//            if (ArenaWorldSystem.Active)
//                ArenaSubworldCoordinator.MoveFromArenaToMain(Main.myPlayer);
//            else
//                Main.NewText("Preview transfer requires multiplayer; use Enter Arenas for single-player generation.", Color.Orange);
//            return;
//        }
//        if (Main.netMode != NetmodeID.MultiplayerClient)
//            return;
//        NewPacket(Packet.TogglePreview).Send();
//    }

//    internal static void RequestState()
//    {
//        if (Main.netMode != NetmodeID.MultiplayerClient)
//            return;
//        ModPacket packet = NewPacket(Packet.RequestState);
//        packet.Send();
//    }

//    private static void Request(ArenaGenerationMode mode, int index)
//    {
//        if (Busy)
//            return;
//        if (mode == ArenaGenerationMode.ThroughStep && !ArenaWorldGenerationCatalog.IsValidIndex(index))
//            return;

//        if (Main.netMode == NetmodeID.SinglePlayer)
//        {
//            Execute(mode, index, Main.rand.Next());
//            return;
//        }
//        if (Main.netMode != NetmodeID.MultiplayerClient)
//            return;

//        Phase = WorldGenManagerPhase.Evacuating;
//        Status = "Request sent; all Arenas players will be returned to Main.";
//        ModPacket packet = NewPacket(Packet.Generate);
//        packet.Write((byte)mode);
//        packet.Write(index);
//        packet.Write(Main.rand.Next());
//        packet.Send();
//    }

//    internal static void HandlePacket(BinaryReader reader, int fromWho)
//    {
//        switch ((Packet)reader.ReadByte())
//        {
//            case Packet.Generate when Main.netMode == NetmodeID.Server:
//            {
//                ArenaGenerationMode mode = (ArenaGenerationMode)reader.ReadByte();
//                int index = reader.ReadInt32();
//                int seed = reader.ReadInt32();
//                if (Authorized(fromWho))
//                    Execute(mode, index, seed);
//                break;
//            }
//            case Packet.RequestState when Main.netMode == NetmodeID.Server:
//                SendState(fromWho);
//                break;
//            case Packet.SyncState when Main.netMode == NetmodeID.MultiplayerClient:
//                ReadState(reader);
//                break;
//            case Packet.TogglePreview when Main.netMode == NetmodeID.Server:
//                if (!Authorized(fromWho))
//                    break;
//                if (ArenaWorldSystem.Active)
//                    ArenaSubworldCoordinator.MoveFromArenaToMain(fromWho);
//                else
//                    ArenaSubworldCoordinator.MoveFromMainToExistingArena(fromWho);
//                break;
//        }
//    }

//    private static bool Authorized(int playerId)
//    {
//        if (ErkySSCCompat.IsAdmin(playerId, out string reason))
//            return true;
//        Log.Warn($"Rejected World Gen Manager action from player {playerId}: {reason}");
//        return false;
//    }

//    private static void Execute(ArenaGenerationMode mode, int index, int seed)
//    {
//        string target = mode == ArenaGenerationMode.ThroughStep && ArenaWorldGenerationCatalog.IsValidIndex(index)
//            ? ArenaWorldGenerationCatalog.Steps[index]
//            : "";
//        if (mode is < ArenaGenerationMode.Full or > ArenaGenerationMode.ClearOnly ||
//            mode == ArenaGenerationMode.ThroughStep && string.IsNullOrEmpty(target))
//        {
//            Log.Warn($"Rejected invalid controlled generation request. mode={mode}, index={index}.");
//            return;
//        }

//        if (!ArenaSubworldCoordinator.RequestWorldGeneration(mode, target, seed))
//        {
//            ServerFailed("Arenas is already transitioning, or this generation request is invalid.");
//            return;
//        }
//        Phase = WorldGenManagerPhase.Evacuating;
//        CompletedStep = -1;
//        MatchReady = false;
//        ServerAvailable = false;
//        Status = mode switch
//        {
//            ArenaGenerationMode.ClearOnly => "Evacuating players; the Arenas child will restart as an empty world.",
//            ArenaGenerationMode.Full => "Evacuating players; the complete Arenas world will be regenerated.",
//            _ => $"Evacuating players; the Arenas child will rebuild through '{target}'."
//        };
//        SendStateToAll();
//    }

//    internal static void ServerStarted(ArenaSubworldRequest request)
//    {
//        RequestId = request.WorldRequestId;
//        CompletedStep = -1;
//        MatchReady = false;
//        ServerAvailable = false;
//        Phase = WorldGenManagerPhase.Starting;
//        Status = request.GenerationMode switch
//        {
//            ArenaGenerationMode.ClearOnly => "Restarting the Arenas child and clearing every tile.",
//            ArenaGenerationMode.Full => "Restarting the Arenas child for complete world generation.",
//            _ => $"Restarting the Arenas child to rebuild through '{request.TargetStep}'."
//        };
//        SendStateToAll();
//    }

//    internal static void ServerGenerating(ArenaSubworldRequest request)
//    {
//        RequestId = request.WorldRequestId;
//        Phase = WorldGenManagerPhase.Generating;
//        ServerAvailable = false;
//        Status = request.GenerationMode switch
//        {
//            ArenaGenerationMode.ClearOnly => "The Arenas child is clearing tiles and world entities.",
//            ArenaGenerationMode.Full => "The Arenas child is running Terraria's complete world-generation pipeline.",
//            _ => $"The Arenas child is running prerequisite passes through '{request.TargetStep}'."
//        };
//        SendStateToAll();
//    }

//    internal static void ServerCompleted(ArenaSubworldRequest request, int completedStep, bool matchReady)
//    {
//        RequestId = request.WorldRequestId;
//        CompletedStep = completedStep;
//        MatchReady = matchReady;
//        ServerAvailable = true;
//        Phase = WorldGenManagerPhase.Ready;
//        Status = request.GenerationMode switch
//        {
//            ArenaGenerationMode.ClearOnly => "Arenas is empty: all tiles and world entities were cleared.",
//            ArenaGenerationMode.Full => "Complete vanilla generation and Arenas combat-region passes finished. The world is match-ready.",
//            _ => $"Arenas was rebuilt from Reset through '{request.TargetStep}'. Players remain in Main."
//        };
//        SendStateToAll();
//    }

//    internal static void ServerFailed(string error)
//    {
//        Phase = WorldGenManagerPhase.Failed;
//        MatchReady = false;
//        ServerAvailable = false;
//        Status = error ?? "Controlled Arenas generation failed.";
//        Log.Error(Status);
//        SendStateToAll();
//    }

//    private static void SendStateToAll()
//    {
//        if (Main.netMode != NetmodeID.Server)
//            return;
//        for (int i = 0; i < Main.maxPlayers; i++)
//            if (Main.player[i]?.active == true)
//                SendState(i);
//    }

//    internal static void SendState(int toClient)
//    {
//        if (Main.netMode != NetmodeID.Server || toClient < 0 || toClient >= Main.maxPlayers)
//            return;
//        ModPacket packet = NewPacket(Packet.SyncState);
//        packet.Write((byte)Phase);
//        packet.Write(RequestId);
//        packet.Write(CompletedStep);
//        packet.Write(MatchReady);
//        packet.Write(ServerAvailable);
//        packet.Write(Status ?? "");
//        packet.Send(toClient);
//    }

//    private static void ReadState(BinaryReader reader)
//    {
//        Phase = (WorldGenManagerPhase)reader.ReadByte();
//        RequestId = reader.ReadInt32();
//        CompletedStep = reader.ReadInt32();
//        MatchReady = reader.ReadBoolean();
//        ServerAvailable = reader.ReadBoolean();
//        Status = reader.ReadString();
//    }

//    private static ModPacket NewPacket(Packet type)
//    {
//        ModPacket packet = ModContent.GetInstance<Arenas>().GetPacket();
//        packet.Write((byte)Arenas.ArenasPacketType.WorldGenManager);
//        packet.Write((byte)type);
//        return packet;
//    }
//}

//internal sealed class WorldGenManagerStateSystem : ModSystem
//{
//    public override void PostUpdateEverything()
//    {
//        if (Main.netMode != NetmodeID.Server || Main.GameUpdateCount % 120 != 0)
//            return;
//        for (int i = 0; i < Main.maxPlayers; i++)
//            if (Main.player[i]?.active == true)
//                WorldGenManagerNetHandler.SendState(i);
//    }
//}
