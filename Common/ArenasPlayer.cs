using Arenas.Core.Configs;
using SubworldLibrary;
using Terraria;
using Terraria.ModLoader;

namespace Arenas.Common;

internal class ArenasPlayer : ModPlayer
{
    public override void ResetEffects()
    {
        if (!SubworldSystem.IsActive<ArenasSubworld>())
            return;

        var config = ModContent.GetInstance<ArenasConfig>();
        if (config == null) return;

        Player.statLifeMax = Player.statLifeMax2 = config.MaxHealth;
        Player.statManaMax = Player.statManaMax2 = config.MaxMana;
    }
    public override void ModifyMaxStats(out StatModifier health, out StatModifier mana)
    {
        base.ModifyMaxStats(out health, out mana);

        if (!SubworldSystem.IsActive<ArenasSubworld>())
            return;

        var config = ModContent.GetInstance<ArenasConfig>();
        if (config == null)
            return;

        health.Base = config.MaxHealth - Player.statLifeMax;
        mana.Base = config.MaxMana - Player.statManaMax;
    }
}
