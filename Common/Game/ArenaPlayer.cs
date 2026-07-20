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
    private bool roundPrepared;
    private bool arenaSpawnActive;

    internal long BossDamage { get; private set; }

    public override void Load()
    {
        On_Player.TrySwitchingLoadout += OnTrySwitchingLoadout;
        On_Player.Spawn += OnPlayerSpawn;
    }

    public override void Unload()
    {
        On_Player.TrySwitchingLoadout -= OnTrySwitchingLoadout;
        On_Player.Spawn -= OnPlayerSpawn;
    }

    public override void OnEnterWorld()
    {
        roundPrepared = false;
        arenaSpawnActive = false;
        RoundManager.RoundPhase phase = ModContent.GetInstance<RoundManager>().CurrentPhase;
        if (phase is RoundManager.RoundPhase.WaitingForPlayers
            or RoundManager.RoundPhase.VotingOrEndScreen)
        {
            Player.SpawnX = -1;
            Player.SpawnY = -1;
        }
        TeamBalancer.AssignJoiningPlayer(Player);
    }

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

        if (manager.CurrentPhase is RoundManager.RoundPhase.WaitingForPlayers
            or RoundManager.RoundPhase.VotingOrEndScreen)
        {
            roundPrepared = false;
            ResetArenaSpawn();
            if (HasCarriedItems(Player))
                ClearCarriedItems(Player, sync: Main.netMode == NetmodeID.Server);
            if (Player.whoAmI == Main.myPlayer && !Main.mouseItem.IsAir)
                Main.mouseItem.TurnToAir();
        }

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
        {
            if (manager.CurrentLayout != null)
                SetArenaSpawn(manager.CurrentLayout.PlayerSpawn((Team)Player.team));

            if (Main.netMode != NetmodeID.MultiplayerClient && !roundPrepared
                && manager.CurrentLayout != null
                && manager.TryGetSelectedPreset(out BossFightPreset preset))
                Prepare(Player, preset, manager.CurrentLayout);

            Player.hostile = true;
            KeepInsideArena(manager.CurrentLayout);
        }
    }

    public override void OnRespawn()
    {
        RoundManager manager = ModContent.GetInstance<RoundManager>();
        if ((Team)Player.team is not (Team.Red or Team.Blue)
            || manager.CurrentPhase is not (RoundManager.RoundPhase.FreezeCountdown or RoundManager.RoundPhase.Playing)
            || manager.CurrentLayout == null || !manager.TryGetSelectedPreset(out BossFightPreset preset))
            return;

        ApplyLoadout(Player, preset);
        SetArenaSpawn(manager.CurrentLayout.PlayerSpawn((Team)Player.team));
        roundPrepared = true;
    }

    internal static void Prepare(Player player, BossFightPreset preset, ArenaLayout layout)
    {
        if (player?.active != true || preset == null || layout == null)
            return;

        if (player.dead)
            player.Spawn(PlayerSpawnContext.ReviveFromDeath);

        ArenaPlayer arenaPlayer = player.GetModPlayer<ArenaPlayer>();
        arenaPlayer.SetArenaSpawn(layout.PlayerSpawn((Team)player.team));
        arenaPlayer.ResetBossDamage();
        ScoreboardService.ResetPlayer(player);
        ApplyLoadout(player, preset);
        Teleport(player, layout.PlayerSpawn((Team)player.team));
        arenaPlayer.roundPrepared = true;
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
            if (player?.active != true)
                continue;

            ArenaPlayer arenaPlayer = player.GetModPlayer<ArenaPlayer>();
            arenaPlayer.roundPrepared = false;
            arenaPlayer.ResetArenaSpawn();
            ClearCarriedItems(player, sync: Main.netMode == NetmodeID.Server);
            if (player.hostile && (Team)player.team is Team.Red or Team.Blue)
            {
                player.hostile = false;
                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.TogglePVP, -1, -1, null, player.whoAmI);
            }
        }
    }

    private static void OnTrySwitchingLoadout(On_Player.orig_TrySwitchingLoadout orig,
        Player player, int loadoutIndex)
    {
        RoundManager.RoundPhase phase = ModContent.GetInstance<RoundManager>().CurrentPhase;
        if (phase is RoundManager.RoundPhase.Generating or RoundManager.RoundPhase.FreezeCountdown
            or RoundManager.RoundPhase.Playing)
            return;

        orig(player, loadoutIndex);
    }

    private static void OnPlayerSpawn(On_Player.orig_Spawn orig, Player player,
        PlayerSpawnContext context)
    {
        orig(player, context);

        RoundManager manager = ModContent.GetInstance<RoundManager>();
        Team team = (Team)player.team;
        if (team is not (Team.Red or Team.Blue)
            || manager.CurrentPhase is not (RoundManager.RoundPhase.Generating
                or RoundManager.RoundPhase.FreezeCountdown or RoundManager.RoundPhase.Playing)
            || manager.CurrentLayout == null)
            return;

        Point spawn = manager.CurrentLayout.PlayerSpawn(team);
        player.GetModPlayer<ArenaPlayer>().SetArenaSpawn(spawn);
        Teleport(player, spawn);
    }

    private static bool HasCarriedItems(Player player)
    {
        if (HasItems(player.inventory) || HasItems(player.armor) || HasItems(player.dye)
            || HasItems(player.miscEquips) || HasItems(player.miscDyes) || !player.trashItem.IsAir)
            return true;

        if (player.Loadouts != null)
            foreach (var loadout in player.Loadouts)
                if (loadout != null && (HasItems(loadout.Armor) || HasItems(loadout.Dye)))
                    return true;

        return false;
    }

    private static bool HasItems(Item[] items)
    {
        if (items == null)
            return false;
        foreach (Item item in items)
            if (item?.IsAir == false)
                return true;
        return false;
    }

    private static void ClearCarriedItems(Player player, bool sync)
    {
        Clear(player.inventory);
        Clear(player.armor);
        Clear(player.dye);
        Clear(player.miscEquips);
        Clear(player.miscDyes);
        player.trashItem.TurnToAir();
        player.selectedItem = 0;
        player.itemAnimation = 0;
        player.itemTime = 0;

        if (player.Loadouts != null)
            foreach (var loadout in player.Loadouts)
            {
                if (loadout == null)
                    continue;
                Clear(loadout.Armor);
                Clear(loadout.Dye);
            }

        if (sync)
            SyncEquipment(player);
    }

    private static void Clear(Item[] items)
    {
        if (items == null)
            return;
        foreach (Item item in items)
            item?.TurnToAir();
    }

    private static void SyncEquipment(Player player)
    {
        if (Main.netMode != NetmodeID.Server)
            return;

        SyncItems(player, player.inventory, PlayerItemSlotID.Inventory0);
        SyncItems(player, player.armor, PlayerItemSlotID.Armor0);
        SyncItems(player, player.dye, PlayerItemSlotID.Dye0);
        SyncItems(player, player.miscEquips, PlayerItemSlotID.Misc0);
        SyncItems(player, player.miscDyes, PlayerItemSlotID.MiscDye0);

        if (player.Loadouts != null && player.Loadouts.Length >= 3)
        {
            if (player.Loadouts[0] != null)
            {
                SyncItems(player, player.Loadouts[0].Armor, PlayerItemSlotID.Loadout1_Armor_0);
                SyncItems(player, player.Loadouts[0].Dye, PlayerItemSlotID.Loadout1_Dye_0);
            }
            if (player.Loadouts[1] != null)
            {
                SyncItems(player, player.Loadouts[1].Armor, PlayerItemSlotID.Loadout2_Armor_0);
                SyncItems(player, player.Loadouts[1].Dye, PlayerItemSlotID.Loadout2_Dye_0);
            }
            if (player.Loadouts[2] != null)
            {
                SyncItems(player, player.Loadouts[2].Armor, PlayerItemSlotID.Loadout3_Armor_0);
                SyncItems(player, player.Loadouts[2].Dye, PlayerItemSlotID.Loadout3_Dye_0);
            }
        }
    }

    private static void SyncItems(Player player, Item[] items, int firstSlot)
    {
        if (items == null)
            return;
        for (int i = 0; i < items.Length; i++)
            NetMessage.SendData(MessageID.SyncEquipment, number: player.whoAmI,
                number2: firstSlot + i, number3: items[i]?.prefix ?? 0);
    }

    internal void AddBossDamage(uint damage) =>
        BossDamage = BossDamage > long.MaxValue - damage ? long.MaxValue : BossDamage + damage;

    private void KeepInsideArena(ArenaLayout layout)
    {
        if (layout == null || Player.dead || Player.ghost)
            return;

        Rectangle area = ArenaSpawnBoxes.TileToWorld(layout.ArenaBounds);
        float maxX = Math.Max(area.Left, area.Right - Player.width);
        float maxY = Math.Max(area.Top, area.Bottom - Player.height);
        Vector2 position = new(
            MathHelper.Clamp(Player.position.X, area.Left, maxX),
            MathHelper.Clamp(Player.position.Y, area.Top, maxY));
        if (position == Player.position)
            return;

        if (position.X != Player.position.X)
            Player.velocity.X = 0f;
        if (position.Y != Player.position.Y)
            Player.velocity.Y = 0f;
        Player.position = position;

        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendData(MessageID.PlayerControls, -1, -1, null, Player.whoAmI);
    }

    private void ResetBossDamage() => BossDamage = 0;

    private void SetArenaSpawn(Point spawn)
    {
        arenaSpawnActive = true;
        Player.SpawnX = spawn.X;
        Player.SpawnY = spawn.Y;
    }

    private void ResetArenaSpawn()
    {
        if (!arenaSpawnActive)
            return;

        arenaSpawnActive = false;
        Player.SpawnX = -1;
        Player.SpawnY = -1;
    }

    private static void ApplyLoadout(Player player, BossFightPreset preset)
    {
        ClearCarriedItems(player, sync: false);

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

        SyncEquipment(player);
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
