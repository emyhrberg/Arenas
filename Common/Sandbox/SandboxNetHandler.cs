using Arenas.Common.Rounds;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria.DataStructures;
using Terraria.ID;

namespace Arenas.Common.Sandbox;

internal static class SandboxNetHandler
{
    private enum ActionType : byte { EquipPreset, SpawnItem }

    internal static void RequestLoadout(int presetIndex)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
        {
            EquipPreset(Main.myPlayer, presetIndex);
            return;
        }

        Send(ActionType.EquipPreset, presetIndex, 0);
    }

    internal static void RequestItem(int itemType, int stack)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
        {
            SpawnItem(Main.myPlayer, itemType, stack);
            return;
        }

        Send(ActionType.SpawnItem, itemType, stack);
    }

    internal static void HandlePacket(BinaryReader reader, int fromWho)
    {
        ActionType action = (ActionType)reader.ReadByte();
        int first = reader.ReadInt32();
        int second = reader.ReadInt32();
        if (Main.netMode != NetmodeID.Server || fromWho < 0 || fromWho >= Main.maxPlayers)
            return;

        if (!ArenaRoundSystem.IsSandboxActive || Main.player[fromWho]?.active != true)
        {
            Log.Warn($"Rejected sandbox action {action} from player {fromWho}: Sandbox is not active.");
            return;
        }

        if (action == ActionType.EquipPreset)
            EquipPreset(fromWho, first);
        else if (action == ActionType.SpawnItem)
            SpawnItem(fromWho, first, second);
    }

    private static void EquipPreset(int playerId, int presetIndex)
    {
        if (!ArenaRoundSystem.IsSandboxActive || playerId < 0 || playerId >= Main.maxPlayers)
            return;

        List<Core.Configs.ConfigElements.BossFightPreset> presets = ArenaRoundSystem.GetValidPresets();
        if (presetIndex < 0 || presetIndex >= presets.Count || presets[presetIndex]?.Loadout == null)
            return;

        Player player = Main.player[playerId];
        if (player?.active != true)
            return;

        player.GetModPlayer<ArenaPlayer>().SandboxLoadoutPresetIndex = presetIndex;
        LoadoutService.Apply(player, presets[presetIndex]);
        if (Main.netMode == NetmodeID.Server)
            ArenaRoundNetHandler.SendApplyKit(playerId, presetIndex);
        Log.Debug($"[SandboxUI1] Equipped player {playerId} with preset {presetIndex} ({ArenaRoundSystem.PresetName(presets[presetIndex])}).");
    }

    private static void SpawnItem(int playerId, int itemType, int requestedStack)
    {
        if (!ArenaRoundSystem.IsSandboxActive || playerId < 0 || playerId >= Main.maxPlayers
            || itemType <= ItemID.None || !ContentSamples.ItemsByType.TryGetValue(itemType, out Item sample))
            return;

        Player player = Main.player[playerId];
        if (player?.active != true)
            return;

        int stack = Math.Clamp(requestedStack, 1, Math.Max(1, sample.maxStack));
        player.QuickSpawnItem(new EntitySource_Misc("ArenasSandboxItemSpawner"), itemType, stack);
        Log.Debug($"[SandboxUI2] Spawned item type={itemType}, stack={stack} for player {playerId}.");
    }

    private static void Send(ActionType action, int first, int second)
    {
        if (Main.netMode != NetmodeID.MultiplayerClient || !ArenaRoundSystem.IsSandboxActive)
            return;

        ModPacket packet = ModContent.GetInstance<Arenas>().GetPacket();
        packet.Write((byte)Arenas.ArenasPacketType.Sandbox);
        packet.Write((byte)action);
        packet.Write(first);
        packet.Write(second);
        packet.Send();
    }
}
