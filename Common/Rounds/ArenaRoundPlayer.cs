using SubworldLibrary;
using Terraria.DataStructures;
using Terraria.ID;

namespace Arenas.Common.Rounds;

internal sealed class ArenaRoundPlayer : ModPlayer
{
    public int Kills { get; private set; }
    public int Deaths { get; private set; }
    public long Damage { get; private set; }

    public override void PostHurt(Player.HurtInfo info)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || !info.PvP || ArenaRoundSystem.Phase != RoundPhase.Playing) return;
        int attacker = info.DamageSource.SourcePlayerIndex;
        if (attacker != Player.whoAmI) RecordDamage(attacker, info.Damage);
    }

    public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || !ArenaRoundSystem.IsParticipant(Player.whoAmI)) return;
        Deaths++;
        int killer = damageSource.SourcePlayerIndex;
        if (pvp && killer != Player.whoAmI && ArenaRoundSystem.IsParticipant(killer)) Main.player[killer].GetModPlayer<ArenaRoundPlayer>().Kills++;
    }

    public override void SetControls()
    {
        if (!SubworldSystem.IsActive<ArenasSubworld>() || ArenaRoundSystem.Phase != RoundPhase.FreezeCountdown) return;
        Player.controlLeft = Player.controlRight = Player.controlUp = Player.controlDown = false;
        Player.controlJump = Player.controlMount = Player.controlHook = false;
        Player.controlUseItem = Player.controlUseTile = Player.controlThrow = false;
    }

    internal void ResetStats() { Kills = Deaths = 0; Damage = 0; }

    internal static void RecordDamage(int playerId, int damage)
    {
        if (damage <= 0 || ArenaRoundSystem.Phase != RoundPhase.Playing || !ArenaRoundSystem.IsParticipant(playerId)) return;
        Main.player[playerId].GetModPlayer<ArenaRoundPlayer>().Damage += damage;
    }

    public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
    {
        if (Main.netMode == NetmodeID.Server && Player.whoAmI == toWho)
            ArenaRoundNetHandler.SendState(toWho);
    }
}
