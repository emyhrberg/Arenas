//using Arenas.Core;
//using DragonLens.Core.Systems.ThemeSystem;
//using DragonLens.Core.Systems.ToolbarSystem;
//using Microsoft.Xna.Framework.Graphics;
//using ReLogic.Content;
//using Terraria;
//using Terraria.ModLoader;

//namespace Arenas.Common.AdminTools.DragonLens;

//[JITWhenModsEnabled("DragonLens")]
//[ExtendsFromMod("DragonLens")]
//internal sealed class DLToolIcons : ModSystem
//{
//    public static string ArenasAdminKey => "ArenasAdmin";

//    public static Asset<Texture2D> GlowAlpha { get; private set; }

//    public override bool IsLoadingEnabled(Mod mod) => !Main.dedServ && ModLoader.HasMod("DragonLens");

//    public override void Load()
//    {
//        if (Main.dedServ)
//            return;

//        GlowAlpha = ModContent.Request<Texture2D>("DragonLens/Assets/Misc/GlowAlpha");
//    }

//    public override void Unload()
//    {
//        GlowAlpha = null;
//    }

//    public override void PostSetupContent()
//    {
//        if (Main.dedServ)
//            return;

//        if (!ModLoader.TryGetMod("DragonLens", out _))
//            return;

//        if (ThemeHandler.allIconProviders == null || ThemeHandler.allIconProviders.Count == 0)
//            return;

//        foreach (var provider in ThemeHandler.allIconProviders.Values)
//        {
//            if (provider == null || provider.icons == null)
//                continue;

//            provider.icons[ArenasAdminKey] = Ass.Icon_Arenas.Value;
//        }

//        ModContent.GetInstance<ToolbarHandler>().OnModLoad();
//    }
//}
