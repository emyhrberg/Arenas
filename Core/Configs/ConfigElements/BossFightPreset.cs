using System.ComponentModel;
using Terraria.ID;
using Terraria.ModLoader.Config;

namespace Arenas.Core.Configs.ConfigElements;

public enum FightTime { Unchanged, Day, Night }

public enum ArenaGeneratorKind { Auto, KingSlimeSurface, EyeSurface, PlanteraJungle, GolemTemple, SandboxWorld }

public sealed class BossFightPreset
{
    [ConfigIcon(ItemID.SuspiciousLookingEye)]
    public NPCDefinition Boss { get; set; } = new();

    [ConfigIcon(ItemID.DirtBlock), DefaultValue(ArenaGeneratorKind.Auto)]
    public ArenaGeneratorKind ArenaGenerator { get; set; } = ArenaGeneratorKind.Auto;

    [ConfigIcon(ItemID.LifeCrystal), DefaultValue(500), Range(1, 10000)]
    public int MaxHealth { get; set; } = 500;

    [ConfigIcon(ItemID.ManaCrystal), DefaultValue(200), Range(0, 1000)]
    public int MaxMana { get; set; } = 200;

    [ConfigIcon(ItemID.Stopwatch), DefaultValue(600), Range(1, 3600)]
    public int RoundDurationSeconds { get; set; } = 600;

    [ConfigIcon("IconGem"), DefaultValue(25), Range(0, 10000)]
    public int VictoryGemReward { get; set; } = 25;

    [ConfigIcon(ItemID.GoldWatch), DefaultValue(FightTime.Unchanged)]
    public FightTime Time { get; set; } = FightTime.Unchanged;

    [ConfigIcon(ItemID.GoldChest)]
    public Loadout Loadout { get; set; } = new();
}
