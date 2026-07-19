using PvPFramework.Common.Combat.TeamBoss;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Arenas.Common.Rounds;

internal sealed class ArenaBossDamageIntegration : ModSystem
{
    public override void Load() => TeamBossNPC.BossDamageDealt += RecordBossDamage;

    public override void Unload() => TeamBossNPC.BossDamageDealt -= RecordBossDamage;

    private static void RecordBossDamage(Player player, uint damage, int itemType)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || player?.active != true || damage == 0
            || !ArenaRoundSystem.IsParticipant(player.whoAmI))
            return;

        player.GetModPlayer<ArenaRoundPlayer>().AddBossDamage(damage);
    }
}
