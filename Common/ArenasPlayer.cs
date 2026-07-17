using System;
using Arenas.Common.Rounds;
using Arenas.Core.Configs.ConfigElements;

namespace Arenas.Common;

internal sealed class ArenasPlayer : ModPlayer
{
    public override void ModifyMaxStats(out StatModifier health, out StatModifier mana)
    {
        base.ModifyMaxStats(out health, out mana);

        if (!ArenaWorldSystem.Active || !ArenaRoundSystem.TryGetCurrentPreset(out BossFightPreset preset))
            return;

        health.Base = Math.Max(1, preset.MaxHealth) - Player.statLifeMax;
        mana.Base = Math.Max(0, preset.MaxMana) - Player.statManaMax;
    }
}
