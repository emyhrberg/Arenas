using Arenas.Common.DataStructures;
using Terraria.ID;
using Terraria.ModLoader.Config;

namespace Arenas._Deprecated.LoadoutSelector;

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
