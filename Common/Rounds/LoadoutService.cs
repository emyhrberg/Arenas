using System;
using Arenas.Core.Configs.ConfigElements;
using Terraria.ID;
using Terraria.ModLoader.Config;

namespace Arenas.Common.Rounds;

internal static class LoadoutService
{
    public static void Apply(Player player, BossFightPreset preset)
        => Apply(player, preset?.Loadout ?? new Loadout(), preset?.MaxHealth ?? 100, preset?.MaxMana ?? 20, true, true);

    public static void Apply(Player player, Loadout loadout, int maxHealth, int maxMana, bool revive, bool fillVitals)
    {
        bool wasDead = player.dead;
        bool wasGhost = player.ghost;
        int respawnTimer = player.respawnTimer;
        int currentLife = player.statLife;
        int currentMana = player.statMana;
        foreach (Item item in player.inventory) item.TurnToAir();
        foreach (Item item in player.armor) item.TurnToAir();
        foreach (Item item in player.miscEquips) item.TurnToAir();

        LoadoutArmor armor = loadout.Armor ?? new();
        LoadoutAccessories accessories = loadout.Accessories ?? new();
        LoadoutEquipment equipment = loadout.Equipment ?? new();
        ItemDefinition[] equipped = [armor.Head, armor.Body, armor.Legs, accessories.Accessory1, accessories.Accessory2, accessories.Accessory3, accessories.Accessory4, accessories.Accessory5];
        for (int i = 0; i < equipped.Length && i < player.armor.Length; i++)
            player.armor[i].SetDefaults(equipped[i]?.Type ?? ItemID.None);

        for (int i = 0; i < (loadout.Inventory?.Count ?? 0) && i < player.inventory.Length; i++)
        {
            LoadoutItem entry = loadout.Inventory[i];
            player.inventory[i].SetDefaults(entry?.Item?.Type ?? ItemID.None);
            if (!player.inventory[i].IsAir) player.inventory[i].stack = entry.Stack;
        }

        player.miscEquips[4].SetDefaults(equipment.GrapplingHook?.Type ?? ItemID.None);
        player.miscEquips[3].SetDefaults(equipment.Mount?.Type ?? ItemID.None);
        player.statLifeMax = player.statLifeMax2 = Math.Max(1, maxHealth);
        player.statManaMax = player.statManaMax2 = Math.Max(0, maxMana);
        if (revive)
        {
            player.dead = player.ghost = false;
            player.respawnTimer = 0;
            player.statLife = fillVitals ? player.statLifeMax : Math.Clamp(currentLife, 1, player.statLifeMax);
            player.statMana = fillVitals ? player.statManaMax : Math.Clamp(currentMana, 0, player.statManaMax);
        }
        else
        {
            player.dead = wasDead;
            player.ghost = wasGhost;
            player.respawnTimer = respawnTimer;
            player.statLife = currentLife;
            player.statMana = currentMana;
        }

        if (Main.netMode == NetmodeID.Server)
        {
            for (int slot = 0, count = player.inventory.Length + player.armor.Length + player.dye.Length + player.miscEquips.Length + player.miscDyes.Length; slot < count; slot++)
                NetMessage.SendData(MessageID.SyncEquipment, number: player.whoAmI, number2: slot);
            NetMessage.SendData(MessageID.PlayerLifeMana, number: player.whoAmI);
        }
    }
}
