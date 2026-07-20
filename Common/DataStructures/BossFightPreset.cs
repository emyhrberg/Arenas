using PvPFramework.Core.Configs.ConfigElements;
using System.ComponentModel;
using Terraria.ID;
using Terraria.ModLoader.Config;

namespace Arenas.Common.DataStructures;

internal sealed class BossFightPreset
{
    [ConfigIcon(ItemID.SuspiciousLookingEye)]
    public NPCDefinition Boss = new();

    [ConfigIcon(ItemID.WoodPlatform), DefaultValue(ArenaKind.WorldCenterSurface)]
    public ArenaKind ArenaKind = ArenaKind.WorldCenterSurface;

    [ConfigIcon(nameof(Ass.IconResize)), DefaultValue(500), Range(100, 4000)]
    public int ArenaWidthTiles = 500;

    [ConfigIcon(nameof(Ass.IconResize)), DefaultValue(500), Range(100, 4000)]
    public int ArenaHeightTiles = 500;

    [ConfigIcon(ItemID.LifeCrystal), DefaultValue(500), Range(1, 500)]
    public int MaxHealth = 500;

    [ConfigIcon(ItemID.ManaCrystal), DefaultValue(200), Range(0, 200)]
    public int MaxMana = 200;

    [ConfigIcon(nameof(PvPFramework.Core.Utilities.Ass.Attack))]
    public Loadout Loadout = new();
}
