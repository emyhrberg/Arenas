using Arenas.Common.Rounds;
using Arenas.Common.Generation;
using Arenas.Core.Compat;
using Arenas.Core.Configs;
using Arenas.Core.Configs.ConfigElements;
using System;
using System.IO;
using Terraria.ID;
using Terraria.ModLoader.Config;

namespace Arenas.Common.AdminTools.GameManager;

internal static class ArenaGameManagerNetHandler
{
    internal enum ActionType : byte { StartRound, SetCountdown, SetRoundTime, SetVotingTime, TogglePause, AdvancePhase, EndRound, ClearWorld, BalanceTeams, SaveGeometry, SyncGeometry }
    internal static int GeometryRevision { get; private set; }

    public static void Request(ActionType type, int first = 0, int second = 0, int third = 0)
    {
        if (Main.netMode == NetmodeID.SinglePlayer) { Execute(type, first, second, third); return; }
        if (Main.netMode != NetmodeID.MultiplayerClient) return;
        ModPacket packet = ModContent.GetInstance<Arenas>().GetPacket();
        packet.Write((byte)Arenas.ArenasPacketType.ArenaGameManager); packet.Write((byte)type);
        packet.Write(first); packet.Write(second); packet.Write(third); packet.Send();
    }

    public static void RequestGeometry(int presetIndex, ArenaGeometryConfig geometry)
    {
        if (geometry == null) return;
        if (Main.netMode == NetmodeID.SinglePlayer)
        {
            SaveGeometry(presetIndex, geometry);
            return;
        }
        if (Main.netMode != NetmodeID.MultiplayerClient) return;
        ModPacket packet = ModContent.GetInstance<Arenas>().GetPacket();
        packet.Write((byte)Arenas.ArenasPacketType.ArenaGameManager);
        packet.Write((byte)ActionType.SaveGeometry);
        packet.Write(presetIndex);
        geometry.Write(packet);
        packet.Send();
    }

    public static void HandlePacket(BinaryReader reader, int fromWho)
    {
        ActionType type = (ActionType)reader.ReadByte();
        if (type == ActionType.SyncGeometry)
        {
            int presetIndex = reader.ReadInt32();
            ArenaGeometryConfig geometry = ArenaGeometryConfig.Read(reader);
            if (Main.netMode == NetmodeID.MultiplayerClient)
                ApplyGeometry(presetIndex, geometry, save: false);
            return;
        }
        if (type == ActionType.SaveGeometry)
        {
            int presetIndex = reader.ReadInt32();
            ArenaGeometryConfig geometry = ArenaGeometryConfig.Read(reader);
            if (Main.netMode != NetmodeID.Server || !Authorized(fromWho)) return;
            SaveGeometry(presetIndex, geometry);
            return;
        }

        int first = reader.ReadInt32(), second = reader.ReadInt32(), third = reader.ReadInt32();
        if (Main.netMode != NetmodeID.Server || !Authorized(fromWho)) return;
        Execute(type, first, second, third);
    }

    private static bool Authorized(int fromWho)
    {
        if (fromWho < 0 || fromWho >= Main.maxPlayers) return false;
        try
        {
            if (!ErkySSCCompat.IsPlayerAdmin(Main.player[fromWho], out string reason))
            {
                Log.Warn($"Rejected Arenas Game Manager action from player {fromWho}: {reason}");
                return false;
            }
        }
        catch (Exception e) { Log.Warn($"Rejected Arenas Game Manager action because the admin check failed: {e.Message}"); return false; }
        return true;
    }

    private static void SaveGeometry(int presetIndex, ArenaGeometryConfig geometry)
    {
        try
        {
            BossFightPreset preset = ArenaRoundSystem.GetPresetOrDefault(presetIndex);
            geometry = geometry.Clone(); geometry.Enabled = true;
            ArenaGeneratorRegistry.ValidateGeometry(preset, geometry);
            ApplyGeometry(presetIndex, geometry, save: true);
            if (Main.netMode == NetmodeID.Server) SendGeometry(presetIndex, geometry);
            Log.Debug($"[ArenaGeometry] Saved preset={presetIndex} world={geometry.WorldWidth}x{geometry.WorldHeight} arena={geometry.ArenaLeft},{geometry.ArenaTop}..{geometry.ArenaRight},{geometry.ArenaBottom}");
        }
        catch (Exception exception)
        {
            Log.Warn($"Arena geometry was not saved: {exception.Message}");
            if (Main.netMode == NetmodeID.SinglePlayer) Main.NewText($"Arena settings not saved: {exception.Message}", Color.OrangeRed);
        }
    }

    private static void ApplyGeometry(int presetIndex, ArenaGeometryConfig geometry, bool save)
    {
        BossFightPreset preset = ArenaRoundSystem.GetPresetOrDefault(presetIndex);
        if (preset == null || ArenaRoundSystem.IsSandboxPreset(preset)) return;
        preset.Arena = geometry.Clone();
        if (save)
        {
            ArenasConfig config = ModContent.GetInstance<ArenasConfig>();
            ConfigManager.Save(config);
            config.OnChanged();
        }
        GeometryRevision++;
        if (Main.netMode != NetmodeID.Server) Main.NewText("Arena settings saved for the next fight", Color.LightGreen);
    }

    private static void SendGeometry(int presetIndex, ArenaGeometryConfig geometry)
    {
        ModPacket packet = ModContent.GetInstance<Arenas>().GetPacket();
        packet.Write((byte)Arenas.ArenasPacketType.ArenaGameManager);
        packet.Write((byte)ActionType.SyncGeometry);
        packet.Write(presetIndex);
        geometry.Write(packet);
        packet.Send();
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
