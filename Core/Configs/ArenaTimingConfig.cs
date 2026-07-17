using System.ComponentModel;
using Terraria.ID;
using Terraria.ModLoader.Config;

namespace Arenas.Core.Configs;

internal sealed class ArenaTimingConfig : ArenasServerConfig
{
    [Header("RoundTiming")]
    [HeaderIcon(ItemID.Stopwatch)]
    [ConfigIcon("IconCheckOn", "IconCheckOff", grayWhenOff: true)]
    [DefaultValue(true)]
    public bool UseFreezeCountdown { get; set; } = true;

    [ConfigIcon(ItemID.IceRod)]
    [RequiresField(nameof(UseFreezeCountdown))]
    [DefaultValue(10)]
    [Range(0, 300)]
    public int FreezeCountdownSeconds { get; set; } = 10;

    [ConfigIcon(ItemID.Stopwatch)]
    [DefaultValue(30)]
    [Range(5, 300)]
    public int VotingDurationSeconds { get; set; } = 30;
}
