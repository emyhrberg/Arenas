using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace Arenas.Core;

internal static class DebugLog
{
    private static Mod Mod => ModContent.GetInstance<global::Arenas.Arenas>();

    public static void Debug(string message) => Mod.Logger.Debug(message);
    public static void Info(string message) => Mod.Logger.Info(message);
    public static void Warn(string message) => Mod.Logger.Warn(message);
    public static void Error(string message) => Mod.Logger.Error(message);

    public static void Chat(string message, Color? color = null)
    {
        if (Main.dedServ)
        {
            Info(message);
            return;
        }

        Main.NewText(message, color ?? Color.White);
    }
}
