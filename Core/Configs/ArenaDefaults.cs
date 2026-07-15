using Arenas.Core.Configs.ConfigElements;
using System.Collections.Generic;
using Terraria.ID;
using Terraria.ModLoader.Config;

namespace Arenas.Core.Configs;

internal static class ArenaDefaults
{
    public static List<BossFightPreset> CreateFightPresets() =>
    [
        new() { Name = "Plantera", Boss = new NPCDefinition(NPCID.Plantera), LoadoutName = "Magician" },
        new() { Name = "Golem", Boss = new NPCDefinition(NPCID.Golem), LoadoutName = "The Warrior" }
    ];

    public static List<Loadout> CreateLoadouts() => [CreateMagician(), CreateWarrior(), CreateMoltenWarrior(), CreateRanger()];

    private static Loadout CreateMagician() => new()
    {
        Name = "Magician",
        Armor = new()
        {
            Head = Def(ItemID.ChlorophyteHeadgear),
            Body = Def(ItemID.AdamantiteBreastplate),
            Legs = Def(ItemID.AncientBattleArmorPants)
        },
        Accessories = new()
        {
            Accessory1 = Def(ItemID.GhostWings),
            Accessory2 = Def(ItemID.Tabi),
            Accessory3 = Def(ItemID.SorcererEmblem),
            Accessory4 = Def(ItemID.CelestialEmblem),
            Accessory5 = Def(ItemID.WormScarf)
        },
        Equipment = new()
        {
            GrapplingHook = Def(ItemID.DualHook),
            Mount = Def(ItemID.QueenSlimeMountSaddle)
        },
        Inventory =
        [
            Item(ItemID.StoneBlock, 9999),
            Item(ItemID.SpectrePickaxe),
            Item(ItemID.Binoculars),
            Item(ItemID.IceRod),
            Item(ItemID.HoneyBucket),
            Item(ItemID.StaffofEarth),
            Item(ItemID.RainbowRod),
            Item(ItemID.MeteorStaff),
            Item(ItemID.Torch, 9999),
            Item(ItemID.Glowstick, 9999),
            Item(ItemID.LunarCraftingStation),
            Item(ItemID.GreaterHealingPotion, 1999),
            Item(ItemID.LaserMachinegun),
            Item(ItemID.NettleBurst),
            Item(ItemID.FairyQueenMagicItem),
            Item(ItemID.BubbleGun),
            Item(ItemID.ChargedBlasterCannon),
            Item(ItemID.GreaterManaPotion, 9999),
            Item(ItemID.WoodPlatform, 9999),
            Item(ItemID.Wood, 9999),
            Item(ItemID.SpectreHood),
            Item(ItemID.SpectreRobe),
            Item(ItemID.SpectrePants),
            Item(ItemID.SpectreMask),
            Item(ItemID.ShadowbeamStaff),
            Item(ItemID.HeatRay),
            Item(ItemID.QueenSlimeMountSaddle),
            Item(ItemID.ChlorophytePlateMail),
            Item(ItemID.ChlorophyteGreaves),
            Item(ItemID.CrystalBall),
            Item(ItemID.Teacup, 9999),
            Item(ItemID.HeartLantern, 1999)
        ]
    };

    private static Loadout CreateWarrior() => new()
    {
        Name = "The Warrior",
        Armor = new()
        {
            Head = Def(ItemID.ChlorophyteMask),
            Body = Def(ItemID.ChlorophytePlateMail),
            Legs = Def(ItemID.ChlorophyteGreaves)
        },
        Accessories = new()
        {
            Accessory1 = Def(ItemID.BeetleWings),
            Accessory2 = Def(ItemID.Tabi),
            Accessory3 = Def(ItemID.CelestialStone),
            Accessory4 = Def(ItemID.FrozenTurtleShell),
            Accessory5 = Def(ItemID.PaladinsShield)
        },
        Equipment = new()
        {
            GrapplingHook = Def(ItemID.DualHook),
            Mount = Def(ItemID.QueenSlimeMountSaddle)
        },
        Inventory =
        [
            Item(ItemID.PaladinsHammer),
            Item(ItemID.StoneBlock, 9999),
            Item(ItemID.PickaxeAxe),
            Item(ItemID.Binoculars),
            Item(ItemID.Wood, 9999),
            Item(ItemID.IceRod),
            Item(ItemID.HoneyBucket),
            Item(ItemID.Kraken),
            Item(ItemID.ShadowJoustingLance),
            Item(ItemID.LightDisc),
            Item(ItemID.SniperRifle),
            Item(ItemID.ShadowbeamStaff),
            Item(ItemID.HighVelocityBullet, 9999),
            Item(ItemID.GreaterHealingPotion, 8888),
            Item(ItemID.Torch, 1888),
            Item(ItemID.Glowstick, 1999),
            Item(ItemID.MoneyTrough),
            Item(ItemID.HeartLantern, 1999),
            Item(ItemID.ChlorophytePartisan),
            Item(ItemID.FireGauntlet),
            Item(ItemID.Ale, 9999),
            Item(ItemID.QueenSlimeMountSaddle),
            Item(ItemID.Teacup, 1999),
            Item(ItemID.BeetleHelmet),
            Item(ItemID.BeetleScaleMail),
            Item(ItemID.BeetleLeggings),
            Item(ItemID.GolemFist)
        ]
    };

    private static Loadout CreateMoltenWarrior() => new()
    {
        Name = "molten warrior",
        Armor = new()
        {
            Head = Def(ItemID.MoltenHelmet),
            Body = Def(ItemID.MoltenBreastplate),
            Legs = Def(ItemID.MoltenGreaves)
        },
        Accessories = new()
        {
            Accessory1 = Def(ItemID.HermesBoots),
            Accessory2 = Def(ItemID.CloudinaBottle),
            Accessory3 = Def(ItemID.EoCShield),
            Accessory4 = Def(ItemID.BandofRegeneration),
            Accessory5 = Def(ItemID.FeralClaws)
        },
        Equipment = new() { GrapplingHook = Def(ItemID.EmeraldHook) },
        Inventory =
        [
            Item(ItemID.StoneBlock, 9999),
            Item(ItemID.Binoculars),
            Item(ItemID.Wood, 9999),
            Item(ItemID.HoneyBucket),
            Item(ItemID.LavaBucket),
            Item(ItemID.FieryGreatsword),
            Item(ItemID.Valor),
            Item(ItemID.MoltenFury),
            Item(ItemID.HellfireArrow, 9999),
            Item(ItemID.HealingPotion, 9999),
            Item(ItemID.Torch, 9999),
            Item(ItemID.Glowstick),
            Item(ItemID.Bomb, 9999),
            Item(ItemID.Ale, 9999),
            Item(ItemID.MoltenPickaxe),
            Item(ItemID.DarkLance),
            Item(ItemID.Sunfury)
        ]
    };

    private static Loadout CreateRanger() => new()
    {
        Name = "ranger",
        Armor = new()
        {
            Head = Def(ItemID.ChlorophyteHelmet),
            Body = Def(ItemID.ChlorophytePlateMail),
            Legs = Def(ItemID.ChlorophyteGreaves)
        },
        Accessories = new()
        {
            Accessory1 = Def(ItemID.RangerEmblem),
            Accessory2 = Def(ItemID.GhostWings),
            Accessory3 = Def(ItemID.Tabi),
            Accessory4 = Def(ItemID.SniperScope),
            Accessory5 = Def(ItemID.WormScarf)
        },
        Equipment = new()
        {
            GrapplingHook = Def(ItemID.WormHook),
            Mount = Def(ItemID.QueenSlimeMountSaddle)
        },
        Inventory =
        [
            Item(ItemID.StoneBlock, 9999),
            Item(ItemID.PickaxeAxe),
            Item(ItemID.Binoculars),
            Item(ItemID.Wood, 9999),
            Item(ItemID.IceRod),
            Item(ItemID.HoneyBucket),
            Item(ItemID.SniperRifle),
            Item(ItemID.HighVelocityBullet, 9999),
            Item(ItemID.GreaterHealingPotion, 9999),
            Item(ItemID.Torch, 9999),
            Item(ItemID.Glowstick, 9999),
            Item(ItemID.MoneyTrough),
            Item(ItemID.HeartLantern, 9999),
            Item(ItemID.StalkersQuiver),
            Item(ItemID.PaladinsShield),
            Item(ItemID.Stynger),
            Item(ItemID.StyngerBolt, 9999),
            Item(ItemID.Xenopopper),
            Item(ItemID.ElectrosphereLauncher),
            Item(ItemID.RocketLauncher),
            Item(ItemID.MiniNukeII, 9999),
            Item(ItemID.Tsunami),
            Item(ItemID.VenomArrow, 9999),
            Item(ItemID.VenusMagnum),
            Item(ItemID.FairyQueenRangedItem),
            Item(ItemID.WoodenArrow, 9999),
            Item(ItemID.TacticalShotgun),
            Item(ItemID.Sunflower),
            Item(ItemID.AvengerEmblem),
            Item(ItemID.ShroomiteHeadgear),
            Item(ItemID.ShroomiteMask),
            Item(ItemID.ShroomiteHelmet),
            Item(ItemID.ShroomiteBreastplate),
            Item(ItemID.ShroomiteLeggings),
            Item(ItemID.QueenSlimeMountSaddle),
            Item(ItemID.Teacup, 1999)
        ]
    };

    private static ItemDefinition Def(int type) => new(type);
    private static LoadoutItem Item(int type, int stack = 1) => new() { Item = Def(type), Stack = stack };
}
