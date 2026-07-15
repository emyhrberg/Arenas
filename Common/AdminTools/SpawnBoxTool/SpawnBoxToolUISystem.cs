using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria.UI;

namespace Arenas.Common.AdminTools.SpawnBoxTool;

[Autoload(Side = ModSide.Client)]
internal sealed class SpawnBoxToolUISystem : ModSystem
{
    private UserInterface ui;
    private UIState state;

    public bool IsActive => ui?.CurrentState == state;
    public void Toggle() => ui?.SetState(IsActive ? null : state);
    public void Hide() => ui?.SetState(null);

    public override void OnWorldLoad() { ui = new(); state = new(); state.Append(new SpawnBoxPanel()); ui.SetState(null); }
    public override void OnWorldUnload() { ui = null; state = null; }
    public override void UpdateUI(GameTime gameTime) => ui?.Update(gameTime);

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
        if (index >= 0) layers.Insert(index, new LegacyGameInterfaceLayer("Arenas: Spawnbox Tool", () => { if (IsActive) ui.Draw(Main.spriteBatch, Main._drawInterfaceGameTime); return true; }, InterfaceScaleType.UI));
    }
}
