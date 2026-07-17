using Arenas.Core.Configs.ConfigElements;
using System;
using System.Collections.Generic;
using Terraria.Localization;
using Terraria.ID;
using Terraria.ModLoader.Config;

namespace Arenas.Core.Configs;

internal class ArenasConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ServerSide;

    [Header("FightPresets")]
    [Expand(true)]
    [CustomModConfigItem(typeof(FightPresetListElement))]
    public List<BossFightPreset> FightPresets { get; set; } = ArenaDefaults.CreateFightPresets();

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

