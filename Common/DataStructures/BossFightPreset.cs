using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace Arenas.Common.DataStructures;

internal sealed class BossFightPreset
{
    public NPCDefinition Boss = new();

    [DefaultValue(ArenaKind.WorldCenterSurface)]
    public ArenaKind ArenaKind = ArenaKind.WorldCenterSurface;

    [DefaultValue(500), Range(100, 4000)]
    public int ArenaWidthTiles = 500;

    [DefaultValue(500), Range(100, 4000)]
    public int ArenaHeightTiles = 500;

    [DefaultValue(500), Range(1, 500)]
    public int MaxHealth = 500;

    [DefaultValue(200), Range(0, 200)]
    public int MaxMana = 200;

    [DefaultValue(5), Range(0, 300)]
    public int GracePeriodSeconds = 5;

    public Loadout Loadout = new();
}
