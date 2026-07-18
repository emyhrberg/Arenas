using Arenas.Common.Rounds;
using Arenas.Core.Compat;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria.UI;

namespace Arenas.Common.UI;

[Autoload(Side = ModSide.Client)]
internal sealed class AdminUISystem : ModSystem
{
    private UserInterface ui;
    private AdminLoadoutUIState state;
    private bool wasEligible;
    private bool dismissed;

    internal bool IsActive => ui?.CurrentState == state;

    public override void OnWorldLoad()
    {
        ui = new UserInterface();
        state = new AdminLoadoutUIState();
        ui.SetState(null);
        wasEligible = false;
        dismissed = false;
    }

    public override void OnWorldUnload()
    {
        ui?.SetState(null);
        wasEligible = false;
        dismissed = false;
    }

    internal void Hide()
    {
        dismissed = true;
        ui?.SetState(null);
    }

    internal void Show()
    {
        if (!ArenaRoundSystem.IsSandboxActive)
            return;
        dismissed = false;
        ui?.SetState(state);
    }

    public override void UpdateUI(GameTime gameTime)
    {
        bool eligible = ArenaRoundSystem.IsSandboxActive && CanUseLocal();
        if (eligible && !wasEligible)
        {
            dismissed = false;
            ui?.SetState(state);
            Log.Debug("[SandboxUI0] Opened adjacent loadout and item-spawner panels.");
        }
        else if (!eligible && wasEligible)
        {
            ui?.SetState(null);
            dismissed = false;
        }

        wasEligible = eligible;
        if (eligible && !dismissed)
            ui?.Update(gameTime);
    }

    private static bool CanUseLocal()
    {
        if (Main.netMode == Terraria.ID.NetmodeID.SinglePlayer)
            return true;
        try
        {
            return ErkySSCCompat.IsPlayerAdmin(Main.LocalPlayer, out _);
        }
        catch (System.Exception exception)
        {
            Log.Warn($"Could not determine Sandbox admin UI access: {exception.Message}");
            return false;
        }
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
        if (index < 0)
            return;

        layers.Insert(index, new LegacyGameInterfaceLayer("Arenas: Sandbox Admin UI", () =>
        {
            if (IsActive)
                ui.Draw(Main.spriteBatch, Main._drawInterfaceGameTime);
            return true;
        }, InterfaceScaleType.UI));
    }
}
