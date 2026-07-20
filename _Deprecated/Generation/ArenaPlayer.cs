//using System;
//using Arenas.Common.Rounds;
//using Arenas.Core.Configs.ConfigElements;

//namespace Arenas.Common;

//internal sealed class ArenaPlayer : ModPlayer
//{
//    internal int SandboxLoadoutPresetIndex { get; set; } = -1;

//    public override void OnEnterWorld()
//    {
//        if (!ArenaRoundSystem.IsSandboxActive)
//            SandboxLoadoutPresetIndex = -1;
//    }

//    public override void ModifyMaxStats(out StatModifier health, out StatModifier mana)
//    {
//        base.ModifyMaxStats(out health, out mana);

//        if (!ArenaWorldSystem.Active || !ArenaRoundSystem.TryGetCurrentPreset(out BossFightPreset preset))
//            return;

//        if (ArenaRoundSystem.IsSandboxPreset(preset))
//        {
//            var presets = ArenaRoundSystem.GetValidPresets();
//            if (SandboxLoadoutPresetIndex >= 0 && SandboxLoadoutPresetIndex < presets.Count)
//                preset = presets[SandboxLoadoutPresetIndex];
//        }

//        health.Base = Math.Max(1, preset.MaxHealth) - Player.statLifeMax;
//        mana.Base = Math.Max(0, preset.MaxMana) - Player.statManaMax;
//    }
//}
