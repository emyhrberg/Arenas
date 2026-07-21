using System.Collections.Generic;
using Terraria.ID;
using Terraria.ModLoader.Config;
using static Arenas.Core.Configs.ServerConfig;

namespace Arenas.Common.DataStructures;

internal static class FightPresets
{
    public static List<BossFightPreset> CreateFightPresets() =>
    [
        new()
        {
            Boss = new NPCDefinition(NPCID.KingSlime),
            ArenaKind = ArenaKind.WorldCenterSurface,
            ArenaWidthTiles = 400,
            ArenaHeightTiles = 200,
            MaxHealth = 200,
            MaxMana = 100,
            Loadout = CreatePreBoss()
        },
        new()
        {
            Boss = new NPCDefinition(NPCID.EyeofCthulhu),
            ArenaKind = ArenaKind.WorldCenterSurface,
            ArenaWidthTiles = 400,
            ArenaHeightTiles = 200,
            MaxHealth = 240,
            MaxMana = 100,
            Loadout = CreatePreBoss()
        },
        new()
        {
            Boss = new NPCDefinition(NPCID.Plantera),
            ArenaKind = ArenaKind.UndergroundJungle,
            ArenaWidthTiles = 400,
            ArenaHeightTiles = 200,
            MaxHealth = 400,
            MaxMana = 180,
            Loadout = CreatePostMech()
        },
        new()
        {
            Boss = new NPCDefinition(NPCID.Golem),
            ArenaKind = ArenaKind.JungleTemple,
            ArenaWidthTiles = 400,
            ArenaHeightTiles = 200,
            MaxHealth = 500,
            MaxMana = 200,
            Loadout = CreatePostPlantera()
        }
    ];

    #region EJ + Progression loadouts

    // Progression-stage loadouts. Gear per stage follows speedrunner metas and the
    // community class-setup consensus for 1.4.4: each loadout only contains items
    // obtainable BEFORE the bosses of its stage (so it is fair for fighting them).

    // No bosses defeated. Grenades + Minishark/boomstick, Enchanted Sword/Starfury,
    // Demon Scythe and the Magiluminescence movement meta.
    private static Loadout CreatePreBoss() => new()
    {
        Armor = new()
        {
            Head = Def(ItemID.PlatinumHelmet),
            Body = Def(ItemID.PlatinumChainmail),
            Legs = Def(ItemID.PlatinumGreaves)
        },
        Accessories = new()
        {
            Accessory1 = Def(ItemID.HermesBoots),
            Accessory2 = Def(ItemID.CloudinaBottle),
            Accessory3 = Def(ItemID.LuckyHorseshoe),
            Accessory4 = Def(ItemID.SharkToothNecklace),
            Accessory5 = Def(ItemID.Magiluminescence)
        },
        Equipment = new() { GrapplingHook = Def(ItemID.DiamondHook) },
        Inventory =
        [
            Item(ItemID.EnchantedSword),
            Item(ItemID.Starfury),
            Item(ItemID.PlatinumBroadsword),
            Item(ItemID.Grenade, 9999),
            Item(ItemID.Minishark),
            Item(ItemID.Boomstick),
            Item(ItemID.MusketBall, 9999),
            Item(ItemID.PlatinumBow),
            Item(ItemID.WoodenArrow, 9999),
            Item(ItemID.JestersArrow, 9999),
            Item(ItemID.DemonScythe),
            Item(ItemID.DiamondStaff),
            Item(ItemID.SpaceGun),
            Item(ItemID.FlinxStaff),
            Item(ItemID.BlandWhip),
            Item(ItemID.PlatinumPickaxe),
            Item(ItemID.HealingPotion, 9999),
            Item(ItemID.IronskinPotion, 999),
            Item(ItemID.RegenerationPotion, 999),
            Item(ItemID.SwiftnessPotion, 999),
            Item(ItemID.ArcheryPotion, 999),
            Item(ItemID.CookedFish, 999),
            Item(ItemID.Bomb, 999),
            Item(ItemID.MeteorHelmet),
            Item(ItemID.MeteorSuit),
            Item(ItemID.MeteorLeggings),
            Item(ItemID.BandofRegeneration),
            Item(ItemID.Binoculars),
            Item(ItemID.HoneyBucket),
            Item(ItemID.Campfire, 999),
            Item(ItemID.HeartLantern, 999),
            Item(ItemID.Torch, 9999),
            Item(ItemID.Glowstick, 9999),
            Item(ItemID.StoneBlock, 9999),
            Item(ItemID.Wood, 9999),
            Item(ItemID.WoodPlatform, 9999)
        ]
    };

    // All mechs down, tuned for Plantera. Hallowed armor (Holy Protection dodge)
    // with the 1.4.4 post-mech Terra Blade, Megashark/Shotbow + ichor, Optic
    // Staff + Durendal, helmet swaps for every class.
    private static Loadout CreatePostMech() => new()
    {
        Armor = new()
        {
            Head = Def(ItemID.HallowedMask),
            Body = Def(ItemID.HallowedPlateMail),
            Legs = Def(ItemID.HallowedGreaves)
        },
        Accessories = new()
        {
            Accessory1 = Def(ItemID.FrozenWings),
            Accessory2 = Def(ItemID.TerrasparkBoots),
            Accessory3 = Def(ItemID.AvengerEmblem),
            Accessory4 = Def(ItemID.AnkhShield),
            Accessory5 = Def(ItemID.FireGauntlet)
        },
        Equipment = new()
        {
            GrapplingHook = Def(ItemID.DualHook),
            Mount = Def(ItemID.QueenSlimeMountSaddle)
        },
        Inventory =
        [
            Item(ItemID.TerraBlade),
            Item(ItemID.DeathSickle),
            Item(ItemID.LightDisc),
            Item(ItemID.Megashark),
            Item(ItemID.ChlorophyteShotbow),
            Item(ItemID.IchorBullet, 9999),
            Item(ItemID.CrystalBullet, 9999),
            Item(ItemID.HolyArrow, 9999),
            Item(ItemID.IchorArrow, 9999),
            Item(ItemID.DaedalusStormbow),
            Item(ItemID.RainbowRod),
            Item(ItemID.GoldenShower),
            Item(ItemID.MagicalHarp),
            Item(ItemID.NimbusRod),
            Item(ItemID.OpticStaff),
            Item(ItemID.SanguineStaff),
            Item(ItemID.SwordWhip),
            Item(ItemID.FireWhip),
            Item(ItemID.HallowedHelmet),
            Item(ItemID.HallowedHeadgear),
            Item(ItemID.HallowedHood),
            Item(ItemID.PickaxeAxe),
            Item(ItemID.GreaterHealingPotion, 9999),
            Item(ItemID.GreaterManaPotion, 9999),
            Item(ItemID.IronskinPotion, 999),
            Item(ItemID.RegenerationPotion, 999),
            Item(ItemID.SwiftnessPotion, 999),
            Item(ItemID.EndurancePotion, 999),
            Item(ItemID.WrathPotion, 999),
            Item(ItemID.RagePotion, 999),
            Item(ItemID.ArcheryPotion, 999),
            Item(ItemID.MagicPowerPotion, 999),
            Item(ItemID.SummoningPotion, 999),
            Item(ItemID.LifeforcePotion, 999),
            Item(ItemID.FlaskofIchor, 999),
            Item(ItemID.PumpkinPie, 999),
            Item(ItemID.StarVeil),
            Item(ItemID.MagicQuiver),
            Item(ItemID.CharmofMyths),
            Item(ItemID.Binoculars),
            Item(ItemID.IceRod),
            Item(ItemID.HoneyBucket),
            Item(ItemID.Campfire, 999),
            Item(ItemID.HeartLantern, 999),
            Item(ItemID.Torch, 9999),
            Item(ItemID.Glowstick, 9999),
            Item(ItemID.StoneBlock, 9999),
            Item(ItemID.Wood, 9999),
            Item(ItemID.WoodPlatform, 9999)
        ]
    };

    // Post-Plantera, tuned for Golem / Duke / Empress / events. Shroomite +
    // Tsunami/ichor, Vampire Knives sustain, Razorblade Typhoon, Dark Harvest /
    // Morning Star whips, Spectre set swap.
    private static Loadout CreatePostPlantera() => new()
    {
        Armor = new()
        {
            Head = Def(ItemID.ShroomiteHeadgear),
            Body = Def(ItemID.ShroomiteBreastplate),
            Legs = Def(ItemID.ShroomiteLeggings)
        },
        Accessories = new()
        {
            Accessory1 = Def(ItemID.Hoverboard),
            Accessory2 = Def(ItemID.MasterNinjaGear),
            Accessory3 = Def(ItemID.RangerEmblem),
            Accessory4 = Def(ItemID.StalkersQuiver),
            Accessory5 = Def(ItemID.AnkhShield)
        },
        Equipment = new()
        {
            GrapplingHook = Def(ItemID.DualHook),
            Mount = Def(ItemID.QueenSlimeMountSaddle)
        },
        Inventory =
        [
            Item(ItemID.Tsunami),
            Item(ItemID.IchorArrow, 9999),
            Item(ItemID.HolyArrow, 9999),
            Item(ItemID.SniperRifle),
            Item(ItemID.TacticalShotgun),
            Item(ItemID.VenusMagnum),
            Item(ItemID.ChlorophyteBullet, 9999),
            Item(ItemID.IchorBullet, 9999),
            Item(ItemID.TerraBlade),
            Item(ItemID.VampireKnives),
            Item(ItemID.PaladinsHammer),
            Item(ItemID.NorthPole),
            Item(ItemID.RazorbladeTyphoon),
            Item(ItemID.SpectreStaff),
            Item(ItemID.DeadlySphereStaff),
            Item(ItemID.ScytheWhip),
            Item(ItemID.MaceWhip),
            Item(ItemID.SpectreHood),
            Item(ItemID.SpectreMask),
            Item(ItemID.SpectreRobe),
            Item(ItemID.SpectrePants),
            Item(ItemID.ShroomiteDiggingClaw),
            Item(ItemID.GreaterHealingPotion, 9999),
            Item(ItemID.GreaterManaPotion, 9999),
            Item(ItemID.IronskinPotion, 999),
            Item(ItemID.RegenerationPotion, 999),
            Item(ItemID.SwiftnessPotion, 999),
            Item(ItemID.EndurancePotion, 999),
            Item(ItemID.WrathPotion, 999),
            Item(ItemID.RagePotion, 999),
            Item(ItemID.ArcheryPotion, 999),
            Item(ItemID.MagicPowerPotion, 999),
            Item(ItemID.SummoningPotion, 999),
            Item(ItemID.LifeforcePotion, 999),
            Item(ItemID.FlaskofIchor, 999),
            Item(ItemID.PumpkinPie, 999),
            Item(ItemID.AvengerEmblem),
            Item(ItemID.PaladinsShield),
            Item(ItemID.CelestialEmblem),
            Item(ItemID.Binoculars),
            Item(ItemID.IceRod),
            Item(ItemID.HoneyBucket),
            Item(ItemID.Campfire, 999),
            Item(ItemID.HeartLantern, 999),
            Item(ItemID.Torch, 9999),
            Item(ItemID.Glowstick, 9999),
            Item(ItemID.StoneBlock, 9999),
            Item(ItemID.Wood, 9999),
            Item(ItemID.WoodPlatform, 9999)
        ]
    };
    #endregion

    #region EJ Plantera class loadouts
    internal enum ArenaClass : byte { None, Melee, Ranger, Mage, Summoner }

    internal static class PostMechKits
    {
        internal static bool Supports(BossFightPreset preset) => preset?.Boss?.Type == NPCID.Plantera;

        internal static Loadout Create(ArenaClass arenaClass) => arenaClass switch
        {
            ArenaClass.Melee => CreateMelee(),
            ArenaClass.Ranger => CreateRanger(),
            ArenaClass.Mage => CreateMage(),
            ArenaClass.Summoner => CreateSummoner(),
            _ => null
        };

        internal static int HeadItem(ArenaClass arenaClass) => arenaClass switch
        {
            ArenaClass.Melee => ItemID.ChlorophyteMask,
            ArenaClass.Ranger => ItemID.ChlorophyteHelmet,
            ArenaClass.Mage => ItemID.ChlorophyteHeadgear,
            ArenaClass.Summoner => ItemID.ObsidianHelm,
            _ => ItemID.None
        };

        private static Loadout CreateMelee() => CreateBase(
            ItemID.ChlorophyteMask, ItemID.ChlorophytePlateMail, ItemID.ChlorophyteGreaves,
            ItemID.FireGauntlet, ItemID.WarriorEmblem,
            [
                Item(ItemID.ShadowFlameKnife),
            Item(ItemID.ChlorophytePartisan),
            Item(ItemID.TrueExcalibur),
            Item(ItemID.LightDisc),
            Item(ItemID.ChainGuillotines),
            Item(ItemID.ChlorophyteClaymore),
            Item(ItemID.BouncingShield),
            Item(ItemID.Ale, 9999)
            ]);

        private static Loadout CreateRanger() => CreateBase(
            ItemID.ChlorophyteHelmet, ItemID.ChlorophytePlateMail, ItemID.ChlorophyteGreaves,
            ItemID.RangerEmblem, ItemID.MagicQuiver,
            [
                Item(ItemID.HolyArrow, 9999),
            Item(ItemID.CursedArrow, 9999),
            Item(ItemID.HellfireArrow, 9999),
            Item(ItemID.Gel, 9999),
            Item(ItemID.CrystalDart, 9999),
            Item(ItemID.CursedDart, 9999),
            Item(ItemID.HighVelocityBullet, 9999),
            Item(ItemID.ChlorophyteShotbow),
            Item(ItemID.PulseBow),
            Item(ItemID.OnyxBlaster),
            Item(ItemID.Flamethrower),
            Item(ItemID.DaedalusStormbow),
            Item(ItemID.DartRifle)
            ]);

        private static Loadout CreateMage() => CreateBase(
            ItemID.ChlorophyteHeadgear, ItemID.ChlorophytePlateMail, ItemID.ChlorophyteGreaves,
            ItemID.SorcererEmblem, ItemID.CelestialEmblem,
            [
                Item(ItemID.ClingerStaff),
            Item(ItemID.NimbusRod),
            Item(ItemID.MeteorStaff),
            Item(ItemID.VenomStaff),
            Item(ItemID.CursedFlames),
            Item(ItemID.UnholyTrident),
            Item(ItemID.RainbowRod),
            Item(ItemID.CrystalVileShard),
            Item(ItemID.ZapinatorOrange),
            Item(ItemID.CrystalBall),
            Item(ItemID.MagicCuffs)
            ]);

        private static Loadout CreateSummoner() => CreateBase(
            ItemID.ObsidianHelm, ItemID.ObsidianShirt, ItemID.ObsidianPants,
            ItemID.BerserkerGlove, ItemID.SummonerEmblem,
            [
                Item(ItemID.Smolstar),
            Item(ItemID.SpiderStaff),
            Item(ItemID.QueenSpiderStaff),
            Item(ItemID.CoolWhip),
            Item(ItemID.SwordWhip),
            Item(ItemID.FireWhip),
            Item(ItemID.ThornWhip),
            Item(ItemID.BoneWhip),
            Item(ItemID.BewitchingTable),
            Item(ItemID.Ale, 9999)
            ]);

        private static Loadout CreateBase(int head, int body, int legs, int classAccessory1, int classAccessory2, LoadoutItem[] classItems)
        {
            Loadout loadout = new()
            {
                Armor = new()
                {
                    Head = Def(head),
                    Body = Def(body),
                    Legs = Def(legs)
                },
                Accessories = new()
                {
                    Accessory1 = Def(ItemID.FairyWings),
                    Accessory2 = Def(ItemID.WormScarf),
                    Accessory3 = Def(ItemID.EoCShield),
                    Accessory4 = Def(classAccessory1),
                    Accessory5 = Def(classAccessory2)
                },
                Equipment = new()
                {
                    GrapplingHook = Def(ItemID.DualHook),
                    Mount = Def(ItemID.SlimySaddle)
                },
                Inventory =
                [
                    .. classItems,
                Item(ItemID.Binoculars),
                Item(ItemID.Teacup, 9999),
                Item(ItemID.IceRod),
                Item(ItemID.ChlorophyteDrill),
                Item(ItemID.HoneyBucket),
                Item(ItemID.Bomb, 500),
                Item(ItemID.GreaterHealingPotion, 9999),
                Item(ItemID.AegisFruit),
                Item(ItemID.Campfire, 5),
                Item(ItemID.HeartLantern, 5),
                Item(ItemID.Wood, 500),
                Item(ItemID.Bed, 5),
                Item(ItemID.PortalGun),
                Item(ItemID.CharmofMyths),
                Item(ItemID.StarVeil),
                Item(ItemID.VolatileGelatin),
                Item(ItemID.EncumberingStone)
                ]
            };
            // Vital Crystal is a newer Terraria item than the 1.4.4 API targeted by
            // this mod. Include it automatically once the runtime exposes it.
            if (ItemID.Search.TryGetId("VitalCrystal", out int vitalCrystal))
                loadout.Inventory.Add(Item(vitalCrystal));
            return loadout;
        }

        private static ItemDefinition Def(int type) => new(type);
        private static LoadoutItem Item(int type, int stack = 1) => new() { Item = Def(type), Stack = stack };
    }
    #endregion

    #region Deprecated generated loadouts

    // End of pre-hardmode, tuned for Wall of Flesh. Evolves "molten warrior":
    // Night's Edge (1.4.4 buff), Star Cannon / Phoenix Blaster / Hellwing Bow,
    // Beenades, Obsidian whip-summoner set, dynamite bridge tools.
    private static Loadout CreatePreHardmode() => new()
    {
        Armor = new()
        {
            Head = Def(ItemID.MoltenHelmet),
            Body = Def(ItemID.MoltenBreastplate),
            Legs = Def(ItemID.MoltenGreaves)
        },
        Accessories = new()
        {
            Accessory1 = Def(ItemID.TerrasparkBoots),
            Accessory2 = Def(ItemID.EoCShield),
            Accessory3 = Def(ItemID.BundleofBalloons),
            Accessory4 = Def(ItemID.ObsidianShield),
            Accessory5 = Def(ItemID.FeralClaws)
        },
        Equipment = new()
        {
            GrapplingHook = Def(ItemID.DiamondHook),
            Mount = Def(ItemID.SlimySaddle)
        },
        Inventory =
        [
            Item(ItemID.NightsEdge),
            Item(ItemID.Sunfury),
            Item(ItemID.DarkLance),
            Item(ItemID.Cascade),
            Item(ItemID.PhoenixBlaster),
            Item(ItemID.MeteorShot, 9999),
            Item(ItemID.StarCannon),
            Item(ItemID.FallenStar, 9999),
            Item(ItemID.HellwingBow),
            Item(ItemID.MoltenFury),
            Item(ItemID.HellfireArrow, 9999),
            Item(ItemID.QuadBarrelShotgun),
            Item(ItemID.Beenade, 9999),
            Item(ItemID.DemonScythe),
            Item(ItemID.WaterBolt),
            Item(ItemID.Flamelash),
            Item(ItemID.ImpStaff),
            Item(ItemID.BoneWhip),
            Item(ItemID.ThornWhip),
            Item(ItemID.ObsidianHelm),
            Item(ItemID.ObsidianShirt),
            Item(ItemID.ObsidianPants),
            Item(ItemID.MoltenPickaxe),
            Item(ItemID.HealingPotion, 9999),
            Item(ItemID.IronskinPotion, 999),
            Item(ItemID.RegenerationPotion, 999),
            Item(ItemID.SwiftnessPotion, 999),
            Item(ItemID.EndurancePotion, 999),
            Item(ItemID.ArcheryPotion, 999),
            Item(ItemID.ObsidianSkinPotion, 999),
            Item(ItemID.Ale, 999),
            Item(ItemID.Bomb, 999),
            Item(ItemID.Dynamite, 999),
            Item(ItemID.LavaBucket),
            Item(ItemID.HoneyBucket),
            Item(ItemID.WormScarf),
            Item(ItemID.Binoculars),
            Item(ItemID.Campfire, 999),
            Item(ItemID.HeartLantern, 999),
            Item(ItemID.Torch, 9999),
            Item(ItemID.Glowstick, 9999),
            Item(ItemID.StoneBlock, 9999),
            Item(ItemID.Wood, 9999),
            Item(ItemID.WoodPlatform, 9999)
        ]
    };

    // Early hardmode, tuned for the mechanical bosses. Daedalus Stormbow + Holy
    // Arrows (the Destroyer melter), Onyx Blaster, Fetid Baghnakhs, Blade Staff /
    // Sanguine Staff, Frost armor with a Spider set swap.
    private static Loadout CreateMechanical() => new()
    {
        Armor = new()
        {
            Head = Def(ItemID.FrostHelmet),
            Body = Def(ItemID.FrostBreastplate),
            Legs = Def(ItemID.FrostLeggings)
        },
        Accessories = new()
        {
            Accessory1 = Def(ItemID.FrozenWings),
            Accessory2 = Def(ItemID.TerrasparkBoots),
            Accessory3 = Def(ItemID.MagicQuiver),
            Accessory4 = Def(ItemID.RangerEmblem),
            Accessory5 = Def(ItemID.StarVeil)
        },
        Equipment = new()
        {
            GrapplingHook = Def(ItemID.QueenSlimeHook),
            Mount = Def(ItemID.QueenSlimeMountSaddle)
        },
        Inventory =
        [
            Item(ItemID.DaedalusStormbow),
            Item(ItemID.HolyArrow, 9999),
            Item(ItemID.IchorArrow, 9999),
            Item(ItemID.OnyxBlaster),
            Item(ItemID.CrystalBullet, 9999),
            Item(ItemID.IchorBullet, 9999),
            Item(ItemID.Uzi),
            Item(ItemID.ShadowFlameKnife),
            Item(ItemID.FetidBaghnakhs),
            Item(ItemID.Amarok),
            Item(ItemID.GoldenShower),
            Item(ItemID.CrystalSerpent),
            Item(ItemID.SkyFracture),
            Item(ItemID.MeteorStaff),
            Item(ItemID.NimbusRod),
            Item(ItemID.Smolstar),
            Item(ItemID.SanguineStaff),
            Item(ItemID.FireWhip),
            Item(ItemID.CoolWhip),
            Item(ItemID.SpiderMask),
            Item(ItemID.SpiderBreastplate),
            Item(ItemID.SpiderGreaves),
            Item(ItemID.TitaniumPickaxe),
            Item(ItemID.GreaterHealingPotion, 9999),
            Item(ItemID.GreaterManaPotion, 9999),
            Item(ItemID.IronskinPotion, 999),
            Item(ItemID.RegenerationPotion, 999),
            Item(ItemID.SwiftnessPotion, 999),
            Item(ItemID.EndurancePotion, 999),
            Item(ItemID.WrathPotion, 999),
            Item(ItemID.RagePotion, 999),
            Item(ItemID.ArcheryPotion, 999),
            Item(ItemID.MagicPowerPotion, 999),
            Item(ItemID.SummoningPotion, 999),
            Item(ItemID.LifeforcePotion, 999),
            Item(ItemID.PumpkinPie, 999),
            Item(ItemID.CharmofMyths),
            Item(ItemID.WormScarf),
            Item(ItemID.Binoculars),
            Item(ItemID.IceRod),
            Item(ItemID.HoneyBucket),
            Item(ItemID.Campfire, 999),
            Item(ItemID.HeartLantern, 999),
            Item(ItemID.Torch, 9999),
            Item(ItemID.Glowstick, 9999),
            Item(ItemID.StoneBlock, 9999),
            Item(ItemID.Wood, 9999),
            Item(ItemID.WoodPlatform, 9999)
        ]
    };

    // Post-Moon Lord. Zenith, SDMG + luminite, Phantasm + ichor (the speedrun
    // Moon Lord killer), Last Prism, Terraprisma + Kaleidoscope, Vortex/Nebula
    // set swaps, Celestial Starboard.
    private static Loadout CreateEndgame() => new()
    {
        Armor = new()
        {
            Head = Def(ItemID.SolarFlareHelmet),
            Body = Def(ItemID.SolarFlareBreastplate),
            Legs = Def(ItemID.SolarFlareLeggings)
        },
        Accessories = new()
        {
            Accessory1 = Def(ItemID.LongRainbowTrailWings),
            Accessory2 = Def(ItemID.MasterNinjaGear),
            Accessory3 = Def(ItemID.CelestialShell),
            Accessory4 = Def(ItemID.AnkhShield),
            Accessory5 = Def(ItemID.FireGauntlet)
        },
        Equipment = new()
        {
            GrapplingHook = Def(ItemID.LunarHook),
            Mount = Def(ItemID.CosmicCarKey)
        },
        Inventory =
        [
            Item(ItemID.Zenith),
            Item(ItemID.StarWrath),
            Item(ItemID.Terrarian),
            Item(ItemID.SolarEruption),
            Item(ItemID.SDMG),
            Item(ItemID.MoonlordBullet, 9999),
            Item(ItemID.ChlorophyteBullet, 9999),
            Item(ItemID.Phantasm),
            Item(ItemID.MoonlordArrow, 9999),
            Item(ItemID.IchorArrow, 9999),
            Item(ItemID.LastPrism),
            Item(ItemID.LunarFlareBook),
            Item(ItemID.SparkleGuitar),
            Item(ItemID.EmpressBlade),
            Item(ItemID.StardustDragonStaff),
            Item(ItemID.MoonlordTurretStaff),
            Item(ItemID.RainbowWhip),
            Item(ItemID.VortexHelmet),
            Item(ItemID.VortexBreastplate),
            Item(ItemID.VortexLeggings),
            Item(ItemID.NebulaHelmet),
            Item(ItemID.NebulaBreastplate),
            Item(ItemID.NebulaLeggings),
            Item(ItemID.SolarFlarePickaxe),
            Item(ItemID.DrillContainmentUnit),
            Item(ItemID.SuperHealingPotion, 9999),
            Item(ItemID.SuperManaPotion, 9999),
            Item(ItemID.IronskinPotion, 999),
            Item(ItemID.RegenerationPotion, 999),
            Item(ItemID.SwiftnessPotion, 999),
            Item(ItemID.EndurancePotion, 999),
            Item(ItemID.WrathPotion, 999),
            Item(ItemID.RagePotion, 999),
            Item(ItemID.ArcheryPotion, 999),
            Item(ItemID.MagicPowerPotion, 999),
            Item(ItemID.SummoningPotion, 999),
            Item(ItemID.LifeforcePotion, 999),
            Item(ItemID.FlaskofIchor, 999),
            Item(ItemID.GoldenDelight, 999),
            Item(ItemID.DestroyerEmblem),
            Item(ItemID.CelestialStone),
            Item(ItemID.EmpressFlightBooster),
            Item(ItemID.LunarCraftingStation),
            Item(ItemID.Binoculars),
            Item(ItemID.HoneyBucket),
            Item(ItemID.Campfire, 999),
            Item(ItemID.HeartLantern, 999),
            Item(ItemID.Torch, 9999),
            Item(ItemID.StoneBlock, 9999),
            Item(ItemID.WoodPlatform, 9999)
        ]
    };
    #endregion


    #region Deprecated (PvPAdventure 4 loadouts)

    private static Loadout CreateMagician() => new()
    {
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

    #endregion

    #region Helpers
    private static ItemDefinition Def(int type) => new(type);
    private static LoadoutItem Item(int type, int stack = 1) => new() { Item = Def(type), Stack = stack };
    #endregion
}
