using System;
using Arenas.Core.Configs.ConfigElements;
using Terraria.ID;
using Terraria.ModLoader.Config;

namespace Arenas.Common.Rounds;

internal static class LoadoutService
{
    public static void Apply(Player player, BossFightPreset preset)
    {
        Loadout loadout = preset.Loadout ?? new Loadout();
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
        player.dead = player.ghost = false;
        player.respawnTimer = 0;
        player.statLife = player.statLifeMax = player.statLifeMax2 = Math.Max(1, preset.MaxHealth);
        player.statMana = player.statManaMax = player.statManaMax2 = Math.Max(0, preset.MaxMana);

        if (Main.netMode == NetmodeID.Server)
        {
            for (int slot = 0, count = player.inventory.Length + player.armor.Length + player.dye.Length + player.miscEquips.Length + player.miscDyes.Length; slot < count; slot++)
                NetMessage.SendData(MessageID.SyncEquipment, number: player.whoAmI, number2: slot);
            NetMessage.SendData(MessageID.PlayerLifeMana, number: player.whoAmI);
        }
    }
}
