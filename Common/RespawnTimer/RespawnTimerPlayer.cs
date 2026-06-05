using Arenas.Core.Configs;
using SubworldLibrary;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace Arenas.Common.RespawnTimer;

public sealed class RespawnTimerPlayer : ModPlayer
{
    public int CustomRespawnTicks { get; private set; }

    internal static bool OwnsRespawnTimer => SubworldSystem.IsActive<ArenasSubworld>();

    public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource)
    {
        if (!IsCustomRespawnTimerEnabled())
        {
            ClearCustomRespawnTimer();
            return;
        }

        ArenasConfig config = ModContent.GetInstance<ArenasConfig>();
        CustomRespawnTicks = Math.Max(1, config.RespawnTimeSeconds * 60);
        Player.respawnTimer = CustomRespawnTicks;
    }

    public override void UpdateDead()
    {
        if (Player.respawnTimer <= 0)
            ClearCustomRespawnTimer();
    }

    public override void OnRespawn()
    {
        ClearCustomRespawnTimer();
    }

    internal void ApplyCustomRespawnTick()
    {
        if (CustomRespawnTicks <= 0)
            return;

        if (!IsCustomRespawnTimerEnabled())
        {
            ClearCustomRespawnTimer();
            return;
        }

        CustomRespawnTicks = Math.Max(0, CustomRespawnTicks - 1);

        if (CustomRespawnTicks > 0)
            Player.respawnTimer = CustomRespawnTicks;
    }

    private void ClearCustomRespawnTimer()
    {
        CustomRespawnTicks = 0;
    }

    private static bool IsCustomRespawnTimerEnabled()
    {
        if (!SubworldSystem.IsActive<ArenasSubworld>())
            return false;

        ArenasConfig config = ModContent.GetInstance<ArenasConfig>();
        return config.EnableCustomRespawnTimer;
    }
}
