using Arenas.Core.Configs.ConfigElements;
using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace Arenas.Core.Configs;

internal sealed class ClientConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;

    [Header("UI")]
    [ConfigIcon("IconCheckOn", "IconCheckOff", grayWhenOff: true)]
    [DefaultValue(true)]
    public bool ShowTopScoreboard { get; set; } = true;
}
