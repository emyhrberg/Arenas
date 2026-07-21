using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace Arenas.Common.DataStructures;

public sealed class ArenaLoadoutOption
{
    [DefaultValue("Loadout")]
    public string Name = "Loadout";

    [Expand(true)]
    public Loadout Loadout = new();
}