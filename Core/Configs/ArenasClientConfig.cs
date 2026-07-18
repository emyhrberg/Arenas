using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace Arenas.Core.Configs;

internal sealed class ArenasClientConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;

    [ConfigIcon("IconCheckOn", "IconCheckOff"), DefaultValue(true)]
    public bool ShowTopScoreboard { get; set; } = true;
}
