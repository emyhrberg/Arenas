using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace Arenas.Core.Configs.ConfigElements;

public sealed class TilePoint
{
    [DefaultValue(-1)] public int X { get; set; } = -1;
    [DefaultValue(-1)] public int Y { get; set; } = -1;
}

public sealed class BossFightPreset
{
    public string Name { get; set; } = "";
    public NPCDefinition Boss { get; set; } = new();
    public string LoadoutName { get; set; } = "";
}
