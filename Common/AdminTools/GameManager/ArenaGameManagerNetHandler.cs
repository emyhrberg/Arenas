using Arenas.Common.Rounds;
using Arenas.Core.Compat;
using System.IO;
using Terraria.ID;

namespace Arenas.Common.AdminTools.GameManager;

internal static class ArenaGameManagerNetHandler
{
    internal enum ActionType : byte
    {
        PrepareArena,
        StartFight,
        SetCountdown,
        SetRoundTime,
        SetVotingTime,
        TogglePause,
        AdvancePhase,
        EndRound,
        BalanceTeams
    }

    public static void Request(ActionType type, int first = 0, int second = 0, int third = 0)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
        {
            Execute(type, first, second, third);
            return;
        }

        if (Main.netMode != NetmodeID.MultiplayerClient)
            return;

        ModPacket packet = ModContent.GetInstance<Arenas>().GetPacket();
        packet.Write((byte)Arenas.ArenasPacketType.ArenaGameManager);
        packet.Write((byte)type);
        packet.Write(first);
        packet.Write(second);
        packet.Write(third);
        packet.Send();
    }

    public static void HandlePacket(BinaryReader reader, int fromWho)
    {
        ActionType type = (ActionType)reader.ReadByte();
        int first = reader.ReadInt32();
        int second = reader.ReadInt32();
        int third = reader.ReadInt32();
        if (Main.netMode != NetmodeID.Server || !Authorized(fromWho))
            return;

        Execute(type, first, second, third);
    }

    private static bool Authorized(int fromWho)
    {
        if (ErkySSCCompat.IsAdmin(fromWho, out string reason))
            return true;

        Log.Warn($"Rejected Arenas Game Manager action from player {fromWho}: {reason}");
        return false;
    }

    private static void Execute(ActionType type, int first, int second, int third)
    {
        switch (type)
        {
            case ActionType.PrepareArena: ArenaRoundSystem.AdminPrepareArena(first); break;
            case ActionType.StartFight: ArenaRoundSystem.AdminStartFight(first, second, third); break;
            case ActionType.SetCountdown: ArenaRoundSystem.AdminSetCountdown(first); break;
            case ActionType.SetRoundTime: ArenaRoundSystem.AdminSetRoundTime(first); break;
            case ActionType.SetVotingTime: ArenaRoundSystem.AdminSetVotingTime(first); break;
            case ActionType.TogglePause: ArenaRoundSystem.AdminTogglePause(); break;
            case ActionType.AdvancePhase: ArenaRoundSystem.AdminAdvancePhase(); break;
            case ActionType.EndRound: ArenaRoundSystem.AdminEndRound(); break;
            case ActionType.BalanceTeams: ArenaRoundSystem.AdminBalanceTeams(); break;
        }
    }
}
