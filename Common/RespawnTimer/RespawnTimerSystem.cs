using Mono.Cecil.Cil;
using MonoMod.Cil;
using System;
using Terraria;
using Terraria.ModLoader;

namespace Arenas.Common.RespawnTimer;

internal sealed class RespawnTimerSystem : ModSystem
{
    public override void Load()
    {
        IL_Player.UpdateDead += EditUpdateDead;
    }

    public override void Unload()
    {
        IL_Player.UpdateDead -= EditUpdateDead;
    }

    private static void EditUpdateDead(ILContext il)
    {
        ILCursor c = new(il);
        int edits = 0;

        while (c.TryGotoNext(
            MoveType.After,
            i => i.MatchCall(typeof(Utils), nameof(Utils.Clamp)),
            i => i.MatchStfld<Player>(nameof(Player.respawnTimer))))
        {
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate(ApplyCustomRespawnTimer);
            edits++;
        }

        if (edits != 2)
            throw new InvalidOperationException($"Expected 2 respawn timer edits, found {edits}.");
    }

    private static void ApplyCustomRespawnTimer(Player player)
    {
        player.GetModPlayer<RespawnTimerPlayer>().ApplyCustomRespawnTick();
    }
}
