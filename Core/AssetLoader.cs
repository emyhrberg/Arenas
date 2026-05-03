using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;

namespace Arenas.Core;

/// <summary>
/// Provides static access to miscallaneous texture assets within the PvPAdventure mod.
/// Automatically initializes when the mod system loads.
/// All asset fields are intended for global access throughout the mod.
/// </summary>
public static class Ass
{
    // --- Arenas assets ---
    public static Asset<Texture2D> Icon_Dead; // 32x32

    /// --- Special Initialization flag, do not touch ---
    public static bool Initialized { get; set; }

    /// <summary>
    /// Initializes static assets
    /// Automatically runs once the mod system loads via <see cref="AssetLoader"/>
    /// </summary>
    static Ass()
    {
        if (Main.dedServ)
        {
            Initialized = true;
            return;
        }

        // Initialize Assets/Custom/MapBGs
        MapBG = new Asset<Texture2D>[42];
        for (int i = 1; i <= 42; i++)
            MapBG[i - 1] = ModContent.Request<Texture2D>($"PvPAdventure/Assets/Custom/MapBGs/MapBG{i}", AssetRequestMode.AsyncLoad);

        // Initialize Assets/Custom and Assets/Shop
        var fields = typeof(Ass).GetFields(BindingFlags.Public | BindingFlags.Static);
        foreach (FieldInfo f in fields)
        {
            if (f.FieldType != typeof(Asset<Texture2D>))
                continue;

            string[] folders = ["Custom", "Shop"];
            foreach (string folder in folders)
            {
                string path = $"PvPAdventure/Assets/{folder}/{f.Name}";
                if (ModContent.HasAsset(path))
                {
                    f.SetValue(null, ModContent.Request<Texture2D>(path, AssetRequestMode.AsyncLoad));
                    break;
                }
            }
        }

        Icon_Refresh ??= Icon_Reset;

        Initialized = true;
    }
}

/// <summary>
/// Initializes asset loading for the mod when the system is loaded with all assets in <see cref="Ass"/>
/// </summary>
public class AssetLoader : ModSystem
{
    public override void Load() => _ = Ass.Initialized;
}
