//using Arenas.Common.Rounds;
//using Microsoft.Xna.Framework;
//using System.Collections.Generic;
//using Terraria.UI;

//namespace Arenas.Common.Sandbox;

//[Autoload(Side = ModSide.Client)]
//internal sealed class SandboxUISystem : ModSystem
//{
//    private UserInterface ui;
//    private SandboxUIState state;
//    private bool wasEligible;

//    internal bool IsActive => ui?.CurrentState == state;

//    public override void OnWorldLoad()
//    {
//        ui = new UserInterface();
//        state = new SandboxUIState();
//        ui.SetState(null);
//        wasEligible = false;
//    }

//    public override void OnWorldUnload()
//    {
//        ui?.SetState(null);
//        wasEligible = false;
//    }

//    internal void Hide() => ui?.SetState(null);

//    internal void Show()
//    {
//        if (!ArenaRoundSystem.IsSandboxActive)
//            return;
//        ui?.SetState(state);
//    }

//    public override void UpdateUI(GameTime gameTime)
//    {
//        bool eligible = ArenaRoundSystem.IsSandboxActive;
//        if (eligible && !wasEligible)
//        {
//            ui?.SetState(state);
//            Log.Debug("[SandboxUI0] Opened loadout and item spawner panels");
//        }
//        else if (!eligible && wasEligible)
//            ui?.SetState(null);

//        wasEligible = eligible;
//        if (eligible && ModContent.GetInstance<Core.Keybinds>().SandboxMenu?.JustPressed == true)
//        {
//            if (IsActive) Hide(); else Show();
//        }
//        if (eligible && IsActive)
//            ui?.Update(gameTime);
//    }

//    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
//    {
//        int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
//        if (index < 0)
//            return;

//        layers.Insert(index, new LegacyGameInterfaceLayer("Arenas: Sandbox UI", () =>
//        {
//            if (IsActive)
//                ui.Draw(Main.spriteBatch, Main._drawInterfaceGameTime);
//            return true;
//        }, InterfaceScaleType.UI));
//    }
//}
