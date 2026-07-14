using System;
using System.Collections.Generic;
using System.IO;
using Terraria.Enums;
using Terraria.ID;

namespace Arenas.Common.Rounds;

internal static class ArenaRoundNetHandler
{
    private enum Packet : byte { SyncState, CastVote, ApplyKit }

    public static void HandlePacket(BinaryReader reader, int fromWho)
    {
        switch ((Packet)reader.ReadByte())
        {
            case Packet.SyncState when Main.netMode == NetmodeID.MultiplayerClient: ReadState(reader); break;
            case Packet.CastVote when Main.netMode == NetmodeID.Server: ArenaRoundSystem.CastVote(fromWho, reader.ReadByte()); break;
            case Packet.ApplyKit when Main.netMode == NetmodeID.MultiplayerClient: ArenaRoundSystem.ApplyKit(reader.ReadByte()); break;
        }
    }

    public static void SendVote(int index) => Send(Packet.CastVote, p => p.Write((byte)index));
    public static void SendApplyKit(int playerId, int presetIndex) => Send(Packet.ApplyKit, p => p.Write((byte)presetIndex), playerId);

    public static void SendStateToAll()
    {
        if (Main.netMode != NetmodeID.Server) return;
        for (int i = 0; i < Main.maxPlayers; i++) if (Main.player[i]?.active == true) SendState(i);
    }

    public static void SendState(int playerId)
    {
        if (Main.netMode != NetmodeID.Server || playerId < 0 || playerId >= Main.maxPlayers) return;
        Send(Packet.SyncState, writer =>
        {
            IReadOnlyList<RoundPlayerStats> scoreboard = ArenaRoundSystem.Scoreboard;
            writer.Write((byte)ArenaRoundSystem.Phase); writer.Write((byte)ArenaRoundSystem.Result); writer.Write(ArenaRoundSystem.RemainingTicks);
            writer.Write((byte)ArenaRoundSystem.CurrentPresetIndex); writer.Write((sbyte)ArenaRoundSystem.VoteFor(playerId));
            writer.Write(ArenaRoundSystem.IsTimerPaused); writer.Write(ArenaRoundSystem.IsAutoStartHeld); writer.Write(ArenaRoundSystem.BossLife); writer.Write(ArenaRoundSystem.BossLifeMax);
            writer.Write((byte)ArenaRoundSystem.VoteCounts.Count);
            for (int i = 0; i < ArenaRoundSystem.VoteCounts.Count; i++)
            {
                IReadOnlyList<byte> voters = ArenaRoundSystem.VotersFor(i);
                writer.Write((byte)voters.Count); foreach (byte voter in voters) writer.Write(voter);
            }
            writer.Write((byte)scoreboard.Count);
            foreach (RoundPlayerStats entry in scoreboard)
            {
                writer.Write(entry.PlayerId); writer.Write((byte)entry.Team); writer.Write(entry.Name ?? "");
                writer.Write(entry.Kills); writer.Write(entry.Deaths); writer.Write(entry.Damage); writer.Write(entry.BossDamage);
            }
        }, playerId);
    }

    private static void ReadState(BinaryReader reader)
    {
        RoundPhase phase = (RoundPhase)reader.ReadByte(); RoundResult result = (RoundResult)reader.ReadByte(); int ticks = reader.ReadInt32(); int preset = reader.ReadByte(); int localVote = reader.ReadSByte();
        bool paused = reader.ReadBoolean(), autoStartHeld = reader.ReadBoolean(); int life = reader.ReadInt32(), lifeMax = reader.ReadInt32();
        List<int> counts = []; List<List<byte>> voters = [];
        for (int i = reader.ReadByte(); i > 0; i--) { List<byte> group = []; for (int n = reader.ReadByte(); n > 0; n--) group.Add(reader.ReadByte()); voters.Add(group); counts.Add(group.Count); }
        List<RoundPlayerStats> scoreboard = [];
        for (int i = reader.ReadByte(); i > 0; i--) scoreboard.Add(new RoundPlayerStats(reader.ReadByte(), (Team)reader.ReadByte(), reader.ReadString(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt64(), reader.ReadInt64()));
        ArenaRoundSystem.ApplyState(phase, result, ticks, preset, localVote, paused, autoStartHeld, life, lifeMax, counts, voters, scoreboard);
    }

    private static void Send(Packet type, Action<ModPacket> write, int toClient = -1)
    {
        ModPacket packet = ModContent.GetInstance<Arenas>().GetPacket();
        packet.Write((byte)Arenas.ArenasPacketType.ArenaRound); packet.Write((byte)type); write(packet); packet.Send(toClient);
    }
}
