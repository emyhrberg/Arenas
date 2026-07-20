using Arenas.Common.DataStructures;
using Arenas.Core.Compat;
using PvPFramework.Core.Configs.ConfigElements;
using System.Collections.Generic;
using System.ComponentModel;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader.Config;

namespace Arenas.Core.Configs;

internal sealed class ServerConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ServerSide;

    #region Fields
    [Header("BossFights")]
    [ConfigIcon(ItemID.KingSlimeBossBag)]
    [Expand(true)]
    //[CustomModConfigItem(typeof(FightPresetListElement))]
    public List<BossFightPreset> FightPresets = ServerConfigDefaults.CreateFightPresets();

    [Header("RoundTime")]

    [ConfigIcon(ItemID.Stopwatch), DefaultValue(600), Range(1, 3600)]
    public int RoundDurationSeconds = 600;

    [ConfigIcon("IconCheckOn", "IconCheckOff", grayWhenOff: true)]
    [DefaultValue(true)]
    public bool UseFreezeCountdown;

    [RequiresField(nameof(UseFreezeCountdown))]
    [ConfigIcon(ItemID.IceRod)]
    [DefaultValue(10)]
    [Range(0, 300)]
    public int FreezeCountdownSeconds;

    [ConfigIcon(ItemID.Stopwatch)]
    [DefaultValue(30)]
    [Range(5, 300)]
    public int VotingDurationSeconds;
    #endregion

    #region NestedConfigTypes
    public sealed class BossFightPreset
    {
        [ConfigIcon(ItemID.SuspiciousLookingEye)]
        public NPCDefinition Boss = new();

        [ConfigIcon(ItemID.LifeCrystal), DefaultValue(500), Range(1, 10000)]
        public int MaxHealth;

        [ConfigIcon(ItemID.ManaCrystal), DefaultValue(200), Range(0, 1000)]
        public int MaxMana;

        [ConfigIcon(ItemID.GoldChest)]
        public Loadout Loadout = new();

        [ConfigIcon("IconGem"), DefaultValue(25), Range(0, 10000)]
        public int VictoryGemReward = 25;

    }

    #endregion

    #region Hooks
    public override void OnLoaded() => EnsureSandboxPreset();

    public override void OnChanged() => EnsureSandboxPreset();

    public override bool AcceptClientChanges(ModConfig pendingConfig, int whoAmI, ref NetworkText message)
    {
        string reason = "";
        bool accepted = Main.netMode == NetmodeID.SinglePlayer || ErkySSCCompat.IsAdmin(whoAmI, out reason);
        message = NetworkText.FromLiteral(accepted ? "Saved" : reason);
        return accepted;
    }
    #endregion

    private void EnsureSandboxPreset()
    {
        // TODO
    }
}
