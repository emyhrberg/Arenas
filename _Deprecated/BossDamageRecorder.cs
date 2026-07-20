//using PvPFramework.Common.Combat.TeamBoss;
//using Terraria;
//using Terraria.ID;
//using Terraria.ModLoader;

//namespace Arenas.Common.Rounds;

//internal sealed class BossDamageRecorder : ModSystem
//{
//    public override void Load()
//    {
//        TeamBossNPC.BossDamageDealt += RecordBossDamage;
//        TeamBossNPC.BossDefeatedByTeam += RecordTeamBossDefeat;
//        TeamBossNPC.NpcKilledByPlayer += RecordNpcKill;
//    }

//    public override void Unload()
//    {
//        TeamBossNPC.BossDamageDealt -= RecordBossDamage;
//        TeamBossNPC.BossDefeatedByTeam -= RecordTeamBossDefeat;
//        TeamBossNPC.NpcKilledByPlayer -= RecordNpcKill;
//    }

//    private static void RecordBossDamage(Player player, uint damage, int itemType)
//    {
//        if (Main.netMode == NetmodeID.MultiplayerClient || player?.active != true || damage == 0
//            || !ArenaRoundSystem.IsParticipant(player.whoAmI))
//            return;

//        player.GetModPlayer<ArenaRoundPlayer>().AddBossDamage(damage);
//    }

//    private static void RecordTeamBossDefeat(Player player, NPC npc, Terraria.Enums.Team team) =>
//        ArenaRoundSystem.NotifyBossKilled(npc, player, team);

//    private static void RecordNpcKill(Player player, NPC npc) =>
//        ArenaRoundSystem.NotifyBossKilled(npc, player);
//}

///// <summary>
///// Covers deaths that have no direct-player attribution, such as DOT or scripted
///// deaths. Direct and virtual-team kills are attributed by TeamBossNPC above.
///// </summary>
//internal sealed class ArenaRoundBossKillFallback : GlobalNPC
//{
//    public override void OnKill(NPC npc)
//    {
//        if (Main.netMode != NetmodeID.MultiplayerClient)
//            ArenaRoundSystem.NotifyBossKilled(npc);
//    }
//}
