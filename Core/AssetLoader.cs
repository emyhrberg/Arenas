using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace Arenas.Core;

/// <summary>
/// Provides static access to texture assets owned by the Arenas mod.
/// </summary>
public static class Ass
{
    public static Asset<Texture2D> Icon_Refresh;
    public static Asset<Texture2D> Icon_Resize;
    public static Asset<Texture2D> Icon_Arenas;
    public static Asset<Texture2D> Icon_StartGame;
    public static Asset<Texture2D> Icon_EndGame;

    public static bool Initialized { get; private set; }

    static Ass()
    {
        if (Main.dedServ)
        {
            Initialized = true;
            return;
        }

        Icon_Refresh = ModContent.Request<Texture2D>("Arenas/Assets/Icon_Refresh", AssetRequestMode.AsyncLoad);
        Icon_Resize = ModContent.Request<Texture2D>("Arenas/Assets/Icon_Resize", AssetRequestMode.AsyncLoad);
        Icon_Arenas = ModContent.Request<Texture2D>("Arenas/Assets/Icon_Arenas", AssetRequestMode.AsyncLoad);
        Icon_StartGame = ModContent.Request<Texture2D>("Arenas/Assets/Icon_StartGame", AssetRequestMode.AsyncLoad);
        Icon_EndGame = ModContent.Request<Texture2D>("Arenas/Assets/Icon_EndGame", AssetRequestMode.AsyncLoad);

        Initialized = true;
    }
}

/// <summary>
/// Initializes asset loading for the mod when the client loads.
/// </summary>
public class AssetLoader : ModSystem
{
    public override void Load() => _ = Ass.Initialized;
}
