using Arenas.Common.Rounds;
using Arenas.Core.Compat;
using System;
using System.IO;
using Terraria.ID;

namespace Arenas.Common.AdminTools.GameManager;

internal static class ArenaGameManagerNetHandler
{
    internal enum ActionType : byte { StartRound, SetCountdown, SetRoundTime, SetVotingTime, TogglePause, AdvancePhase, EndRound, ClearWorld, BalanceTeams }

    public static void Request(ActionType type, int first = 0, int second = 0, int third = 0)
    {
        if (Main.netMode == NetmodeID.SinglePlayer) { Execute(type, first, second, third); return; }
        if (Main.netMode != NetmodeID.MultiplayerClient) return;
        ModPacket packet = ModContent.GetInstance<Arenas>().GetPacket();
        packet.Write((byte)Arenas.ArenasPacketType.ArenaGameManager); packet.Write((byte)type);
        packet.Write(first); packet.Write(second); packet.Write(third); packet.Send();
    }

    public static void HandlePacket(BinaryReader reader, int fromWho)
    {
        ActionType type = (ActionType)reader.ReadByte(); int first = reader.ReadInt32(), second = reader.ReadInt32(), third = reader.ReadInt32();
        if (Main.netMode != NetmodeID.Server || fromWho < 0 || fromWho >= Main.maxPlayers) return;
        try
        {
            if (!ErkySSCCompat.IsPlayerAdmin(Main.player[fromWho], out string reason))
            {
                Log.Warn($"Rejected Arenas Game Manager action '{type}' from player {fromWho}: {reason}");
                return;
            }
        }
        catch (Exception e) { Log.Warn($"Rejected Arenas Game Manager action '{type}' because the admin check failed: {e.Message}"); return; }
        Execute(type, first, second, third);
    }

    private static void Execute(ActionType type, int first, int second, int third)
    {
        switch (type)
        {
            case ActionType.StartRound: ArenaRoundSystem.AdminStartRound(first, second, third); break;
            case ActionType.SetCountdown: ArenaRoundSystem.AdminSetCountdown(first); break;
            case ActionType.SetRoundTime: ArenaRoundSystem.AdminSetRoundTime(first); break;
            case ActionType.SetVotingTime: ArenaRoundSystem.AdminSetVotingTime(first); break;
            case ActionType.TogglePause: ArenaRoundSystem.AdminTogglePause(); break;
            case ActionType.AdvancePhase: ArenaRoundSystem.AdminAdvancePhase(); break;
            case ActionType.EndRound: ArenaRoundSystem.AdminEndRound(); break;
            case ActionType.ClearWorld: ArenaRoundSystem.AdminClearWorld(); break;
            case ActionType.BalanceTeams: ArenaRoundSystem.AdminBalanceTeams(); break;
        }
    }
}
