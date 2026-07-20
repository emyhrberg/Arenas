using Arenas.Common.DataStructures;
using PvPFramework.Common.Scoreboard;
using System;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader.Config;

namespace Arenas.Common.Game;

/// <summary>Applies round loadouts, spawn rules, freezing, and arena bounds.</summary>
internal sealed class ArenaPlayer : ModPlayer
{
    internal long BossDamage { get; private set; }

    public override void OnEnterWorld() => TeamBalancer.AssignJoiningPlayer(Player);

    public override void SetControls()
    {
        RoundManager manager = ModContent.GetInstance<RoundManager>();
        if (manager.CurrentPhase is not (RoundManager.RoundPhase.Generating or RoundManager.RoundPhase.FreezeCountdown))
            return;

        Player.controlLeft = Player.controlRight = Player.controlUp = Player.controlDown = false;
        Player.controlJump = Player.controlMount = Player.controlHook = false;
        Player.controlUseItem = Player.controlUseTile = Player.controlThrow = false;
    }

    public override void PostUpdate()
    {
        RoundManager manager = ModContent.GetInstance<RoundManager>();
        if (manager.CurrentPhase is RoundManager.RoundPhase.Generating or RoundManager.RoundPhase.FreezeCountdown)
        {
            Player.AddBuff(BuffID.Frozen, 2);
            Player.immune = true;
            Player.immuneTime = Math.Max(Player.immuneTime, 2);
            Player.immuneNoBlink = true;
            Player.velocity = Vector2.Zero;
        }

        if ((manager.CurrentPhase is RoundManager.RoundPhase.FreezeCountdown or RoundManager.RoundPhase.Playing)
            && ((Team)Player.team is Team.Red or Team.Blue))
            Player.hostile = true;
    }

    public override void OnRespawn()
    {
        RoundManager manager = ModContent.GetInstance<RoundManager>();
        if ((Team)Player.team is not (Team.Red or Team.Blue)
            || manager.CurrentPhase is not (RoundManager.RoundPhase.FreezeCountdown or RoundManager.RoundPhase.Playing)
            || manager.CurrentLayout == null || !manager.TryGetSelectedPreset(out BossFightPreset preset))
            return;

        ApplyLoadout(Player, preset);
        Teleport(Player, manager.CurrentLayout.PlayerSpawn((Team)Player.team));
    }

    internal static void Prepare(Player player, BossFightPreset preset, ArenaLayout layout)
    {
        if (player?.active != true || preset == null || layout == null)
            return;

        if (player.dead)
            player.Spawn(PlayerSpawnContext.ReviveFromDeath);

        player.GetModPlayer<ArenaPlayer>().ResetBossDamage();
        ScoreboardService.ResetPlayer(player);
        ApplyLoadout(player, preset);
        Teleport(player, layout.PlayerSpawn((Team)player.team));
        player.hostile = true;

        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendData(MessageID.TogglePVP, -1, -1, null, player.whoAmI);
    }

    internal static void ReleaseAll()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        foreach (Player player in Main.player)
        {
            if (player?.active != true || !player.hostile
                || (Team)player.team is not (Team.Red or Team.Blue))
                continue;

            player.hostile = false;
            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.TogglePVP, -1, -1, null, player.whoAmI);
        }
    }

    internal void AddBossDamage(uint damage) =>
        BossDamage = BossDamage > long.MaxValue - damage ? long.MaxValue : BossDamage + damage;

    private void ResetBossDamage() => BossDamage = 0;

    private static void ApplyLoadout(Player player, BossFightPreset preset)
    {
        foreach (Item item in player.inventory) item.TurnToAir();
        foreach (Item item in player.armor) item.TurnToAir();
        foreach (Item item in player.miscEquips) item.TurnToAir();

        Loadout loadout = preset.Loadout ?? new Loadout();
        ItemDefinition[] equipped =
        [
            loadout.Armor?.Head,
            loadout.Armor?.Body,
            loadout.Armor?.Legs,
            loadout.Accessories?.Accessory1,
            loadout.Accessories?.Accessory2,
            loadout.Accessories?.Accessory3,
            loadout.Accessories?.Accessory4,
            loadout.Accessories?.Accessory5
        ];

        for (int i = 0; i < equipped.Length && i < player.armor.Length; i++)
            player.armor[i].SetDefaults(equipped[i]?.Type ?? ItemID.None);

        for (int i = 0; i < (loadout.Inventory?.Count ?? 0) && i < player.inventory.Length; i++)
        {
            LoadoutItem entry = loadout.Inventory[i];
            if (entry == null)
                continue;
            player.inventory[i].SetDefaults(entry?.Item?.Type ?? ItemID.None);
            if (!player.inventory[i].IsAir)
                player.inventory[i].stack = Math.Max(1, entry.Stack);
        }

        if (player.miscEquips.Length > 4)
            player.miscEquips[4].SetDefaults(loadout.Equipment?.GrapplingHook?.Type ?? ItemID.None);
        if (player.miscEquips.Length > 3)
            player.miscEquips[3].SetDefaults(loadout.Equipment?.Mount?.Type ?? ItemID.None);

        player.dead = false;
        player.ghost = false;
        player.respawnTimer = 0;
        player.statLifeMax = player.statLifeMax2 = Math.Max(1, preset.MaxHealth);
        player.statManaMax = player.statManaMax2 = Math.Max(0, preset.MaxMana);
        player.statLife = player.statLifeMax;
        player.statMana = player.statManaMax;

        if (Main.netMode != NetmodeID.Server)
            return;

        int equipmentSlots = player.inventory.Length + player.armor.Length + player.dye.Length
            + player.miscEquips.Length + player.miscDyes.Length;
        for (int slot = 0; slot < equipmentSlots; slot++)
            NetMessage.SendData(MessageID.SyncEquipment, number: player.whoAmI, number2: slot);
        NetMessage.SendData(MessageID.PlayerLifeMana, number: player.whoAmI);
        NetMessage.SendData(MessageID.PlayerMana, number: player.whoAmI);
    }

    private static void Teleport(Player player, Point tile)
    {
        Vector2 position = new(tile.X * 16f + 8f - player.width / 2f, tile.Y * 16f - player.height);
        player.Teleport(position, TeleportationStyleID.RodOfDiscord);
        player.velocity = Vector2.Zero;

        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendData(MessageID.TeleportEntity, -1, -1, null, 0, player.whoAmI,
                position.X, position.Y, TeleportationStyleID.RodOfDiscord);
    }
}
