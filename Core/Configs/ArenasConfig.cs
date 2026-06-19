using Arenas.Core.Configs.ConfigElements;
using Arenas.Core.Configs.ConfigElements.LoadoutItems;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader.Config;

namespace Arenas.Core.Configs;

internal class ArenasConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ServerSide;

    [Header("Arenas")]
    [BackgroundColor(90, 40, 110)]
    [DefaultValue(true)]
    public bool IsArenasEnabled { get; set; } = true;

    [BackgroundColor(90, 40, 110)]
    [Slider]
    [Increment(20)]
    [Range(100, 500)]
    [DefaultValue(500)]
    public int MaxHealth { get; set; } = 500;

    [BackgroundColor(90, 40, 110)]
    [Slider]
    [Increment(20)]
    [Range(20, 200)]
    [DefaultValue(200)]
    public int MaxMana { get; set; } = 200;

    [BackgroundColor(90, 40, 110)]
    [DefaultValue(false)]
    public bool RevealMap { get; set; } = false;

    [Header("Respawn")]
    [BackgroundColor(90, 40, 110)]
    [DefaultValue(false)]
    public bool EnableCustomRespawnTimer { get; set; } = false;

    [BackgroundColor(90, 40, 110)]
    [Range(1, 60)]
    [DefaultValue(5)]
    public int RespawnTimeSeconds { get; set; } = 5;

    [Header("Loadouts")]
    [BackgroundColor(90, 40, 110)]
    [CustomModConfigItem(typeof(LoadoutListElement))]
    public List<Loadout> ArenaLoadouts { get; set; } = [];

    #region Hooks / methods
    public override bool AcceptClientChanges(ModConfig pendingConfig, int whoAmI, ref NetworkText message)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
            return true;

        if (!ModLoader.TryGetMod("ErkySSC", out Mod erkySsc))
        {
            message = NetworkText.FromLiteral("Server config changes require ErkySSC admin permissions.");
            return false;
        }

        bool isAdmin = false;

        try
        {
            object result = erkySsc.Call("IsAdmin", whoAmI);

            if (result is bool value)
                isAdmin = value;
        }
        catch (Exception e)
        {
            Log.Chat($"Failed to check ErkySSC admin permission for config change. whoAmI={whoAmI}, error={e.Message}");
        }

        if (!isAdmin)
        {
            message = NetworkText.FromLiteral("You must be an ErkySSC admin to modify this config.");
            return false;
        }

        message = NetworkText.FromLiteral("Saved!");
        return true;
    }

    public override void HandleAcceptClientChangesReply(bool success, int player, NetworkText message)
    {
        Log.Chat("Server accepted changes!");
        base.HandleAcceptClientChangesReply(success, player, message);
    }
    public override void OnLoaded()
    {
        base.OnLoaded();
    }
    public override void OnChanged()
    {
        base.OnChanged();
    }
    #endregion
}

