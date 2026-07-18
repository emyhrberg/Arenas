using Arenas.Core.Configs.ConfigElements;
using Arenas.Core.Compat;
using System.Collections.Generic;
using System.ComponentModel;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader.Config;

namespace Arenas.Core.Configs;

internal sealed class ArenasConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ServerSide;

    [Header("FightPresets"), ConfigIcon(ItemID.KingSlimeBossBag), Expand(true)]
    [CustomModConfigItem(typeof(FightPresetListElement))]
    public List<BossFightPreset> FightPresets { get; set; } = ArenaDefaults.CreateFightPresets();

    [Header("RoundTiming"), ConfigIcon("IconCheckOn", "IconCheckOff"), DefaultValue(true)]
    public bool UseFreezeCountdown { get; set; } = true;

    [ConfigIcon(ItemID.IceRod), DefaultValue(10), Range(0, 300)]
    public int FreezeCountdownSeconds { get; set; } = 10;

    [ConfigIcon(ItemID.Stopwatch), DefaultValue(30), Range(5, 300)]
    public int VotingDurationSeconds { get; set; } = 30;

    public override void OnLoaded()
    {
        EnsureSandboxPreset();
        MigrateLegacySideBorders();
    }

    public override void OnChanged()
    {
        EnsureSandboxPreset();
    }

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

    private void MigrateLegacySideBorders()
    {
        int migrated = 0;
        foreach (BossFightPreset preset in FightPresets)
        {
            ArenaGeometryConfig arena = preset?.Arena;
            if (arena?.WorldWidth != 850 || arena.ArenaLeft != 28 || arena.ArenaRight != 822)
                continue;

            arena.ArenaLeft = 120;
            arena.ArenaRight = 730;
            migrated++;
        }

        if (migrated > 0)
            Log.Debug($"[ArenaConfig] Moved legacy side borders inward for {migrated} fight preset(s)");
    }
}

