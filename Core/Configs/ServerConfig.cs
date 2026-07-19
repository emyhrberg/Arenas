using Arenas.Core.Compat;
using Arenas.Core.Configs.ConfigElements;
using System.Collections.Generic;
using System.ComponentModel;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader.Config;

namespace Arenas.Core.Configs;

internal sealed class ServerConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ServerSide;

    [Header("BossFights")]
    [ConfigIcon(ItemID.KingSlimeBossBag)]
    [Expand(true)]
    [CustomModConfigItem(typeof(FightPresetListElement))]
    public List<BossFightPreset> FightPresets { get; set; } = ArenaDefaults.CreateFightPresets();

    [Header("RoundTiming")]
    [ConfigIcon("IconCheckOn", "IconCheckOff", grayWhenOff: true)]
    [DefaultValue(true)]
    public bool UseFreezeCountdown { get; set; } = true;

    [RequiresField(nameof(UseFreezeCountdown))]
    [ConfigIcon(ItemID.IceRod)]
    [DefaultValue(10)]
    [Range(0, 300)]
    public int FreezeCountdownSeconds { get; set; } = 10;

    [ConfigIcon(ItemID.Stopwatch)]
    [DefaultValue(30)]
    [Range(5, 300)]
    public int VotingDurationSeconds { get; set; } = 30;

    public override void OnLoaded() => EnsureSandboxPreset();

    public override void OnChanged() => EnsureSandboxPreset();

    public override bool AcceptClientChanges(ModConfig pendingConfig, int whoAmI, ref NetworkText message)
    {
        string reason = "";
        bool accepted = Main.netMode == NetmodeID.SinglePlayer || ErkySSCCompat.IsAdmin(whoAmI, out reason);
        message = NetworkText.FromLiteral(accepted ? "Saved" : reason);
        return accepted;
    }

    private void EnsureSandboxPreset()
    {
        FightPresets ??= [];
        if (!FightPresets.Exists(preset => preset?.ArenaGenerator == ArenaGeneratorKind.SandboxWorld))
        {
            FightPresets.Add(ArenaDefaults.CreateSandboxPreset());
            Log.Debug("[SandboxConfig] Added the built-in Sandbox preset to the loaded server config.");
        }
    }
}
