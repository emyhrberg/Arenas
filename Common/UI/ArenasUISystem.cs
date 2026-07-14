using Arenas.Core;
using Arenas.Core.Configs;
using Microsoft.Xna.Framework;
using SubworldLibrary;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace Arenas.Common.UI;

[Autoload(Side = ModSide.Client)]
public sealed class ArenasUISystem : ModSystem
{
    // UI
    private static UserInterface Interface;
    private static ArenasJoinUIState JoinUIState;

    // Enabled check
    public static bool IsEnabled
    {
        get
        {
            var config = ModContent.GetInstance<ArenasConfig>();
            if (config == null)
            {
                Log.Warn("ServerConfig not loaded – Arenas disabled by default");
                return false;
            }

            return config.IsArenasEnabled;
        }
    }

    public override void OnWorldLoad()
    {
        Interface = new();
        JoinUIState = new();

#if DEBUG
        // Show in SP for testing.
#else
        // Don't show in SP.
    if (Main.netMode == NetmodeID.SinglePlayer)
                return;
#endif

        if (IsEnabled)
        {
            Toggle();
        }
    }

    public static void Toggle()
    {
        // Preset kits are server-owned; there is no manual loadout selector in Arenas.
        if (SubworldSystem.IsActive<ArenasSubworld>())
        {
            Close();
            return;
        }

        if (Interface?.CurrentState == null)
            Interface?.SetState(JoinUIState);
        else
            Interface?.SetState(null);
    }

    public static void Close()
    {

        if (Interface?.CurrentState != null)
            Interface?.SetState(null);
    }

    public override void UpdateUI(GameTime gameTime)
    {
        //var ss = ModContent.GetInstance<SpawnSelector.SpawnSystem>();
        //if (ss.ui.CurrentState != null)
        //{
        //    if (Interface.CurrentState != null)
        //    {
        //        Interface.SetState(null);
        //    }
        //}

        Interface?.Update(gameTime);
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        if (Interface?.CurrentState == null)
            return;

        int index = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
        if (index == -1)
            return;

        layers.Insert(index, new LegacyGameInterfaceLayer(
            "Arenas: UI",
            () =>
            {
                Interface.Draw(Main.spriteBatch, new GameTime());
                return true;
            },
            InterfaceScaleType.UI));
    }
}
