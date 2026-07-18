using Microsoft.Xna.Framework.Graphics;
using MonoMod.RuntimeDetour;
using ReLogic.Content;
using System;
using System.Collections;
using System.Reflection;
using Terraria.GameContent;
using Terraria.ModLoader.Config.UI;
using Terraria.UI;
using Terraria.UI.Chat;

namespace Arenas.Core.Configs;

[Autoload(Side = ModSide.Client)]
internal sealed class ConfigIconSystem : ModSystem
{
    private const float IconSize = 24f;
    private const float TextLeft = 40f;
    private Hook wrapHook;

    private delegate Tuple<UIElement, UIElement> WrapItOrig(
        UIElement parent, ref int top, PropertyFieldWrapper member, object item,
        int order, object list, Type arrayType, int index);
    private delegate Tuple<UIElement, UIElement> WrapItDetour(
        WrapItOrig orig, UIElement parent, ref int top, PropertyFieldWrapper member,
        object item, int order, object list, Type arrayType, int index);

    public override void Load()
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        MethodInfo method = typeof(UIModConfig).GetMethod("WrapIt", flags);
        if (method != null)
            wrapHook = new Hook(method, new WrapItDetour(WrapIt));
    }

    public override void Unload()
    {
        wrapHook?.Dispose();
        wrapHook = null;
    }

    private static Tuple<UIElement, UIElement> WrapIt(
        WrapItOrig orig, UIElement parent, ref int top, PropertyFieldWrapper member,
        object item, int order, object list, Type arrayType, int index)
    {
        Tuple<UIElement, UIElement> result = orig(parent, ref top, member, item, order, list, arrayType, index);
        ConfigIconAttribute icon = member?.MemberInfo?.GetCustomAttribute<ConfigIconAttribute>(true);
        Asset<Texture2D> on = GetTexture(icon);
        if (result?.Item2 == null || on == null)
            return result;

        ConfigElement config = FindConfig(result.Item2);
        result.Item2.Append(new ConfigIconImage(on, GetOffTexture(icon), GetBool(item, member.MemberInfo))
        {
            Left = { Pixels = 8f },
            VAlign = .5f
        });

        if (config != null)
        {
            config.DrawLabel = false;
            result.Item2.Append(new ConfigIconLabel(config));
        }
        return result;
    }

    private static Asset<Texture2D> GetTexture(ConfigIconAttribute icon) => icon switch
    {
        null => null,
        { ItemId: >= 0 } => Main.Assets.Request<Texture2D>($"Images/Item_{icon.ItemId}"),
        _ => GetAsset(icon.AssetName)
    };

    private static Asset<Texture2D> GetOffTexture(ConfigIconAttribute icon) =>
        string.IsNullOrWhiteSpace(icon?.OffAssetName) ? null : GetAsset(icon.OffAssetName);

    private static Asset<Texture2D> GetAsset(string name) =>
        typeof(Ass).GetField(name ?? "", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as Asset<Texture2D>;

    private static Func<bool> GetBool(object owner, MemberInfo member) => member switch
    {
        FieldInfo { FieldType: { } type } field when owner != null && type == typeof(bool) => () => (bool)field.GetValue(owner),
        PropertyInfo { PropertyType: { } type, CanRead: true } property when owner != null && type == typeof(bool) => () => (bool)property.GetValue(owner),
        _ => null
    };

    private static ConfigElement FindConfig(UIElement element)
    {
        if (element is ConfigElement config)
            return config;
        foreach (UIElement child in element.Children)
            if (FindConfig(child) is { } nested)
                return nested;
        return null;
    }

    private sealed class ConfigIconImage(Asset<Texture2D> on, Asset<Texture2D> off, Func<bool> enabled) : UIElement
    {
        public override void OnInitialize()
        {
            Width.Set(IconSize, 0f);
            Height.Set(IconSize, 0f);
            IgnoresMouseInteraction = true;
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            Texture2D texture = (off != null && enabled?.Invoke() == false ? off : on).Value;
            Rectangle box = GetDimensions().ToRectangle();
            float scale = Math.Min(box.Width / (float)texture.Width, box.Height / (float)texture.Height);
            spriteBatch.Draw(texture, box.Center.ToVector2(), null, Color.White * .85f, 0f, texture.Size() * .5f, scale, SpriteEffects.None, 0f);
        }
    }

    private sealed class ConfigIconLabel(ConfigElement owner) : UIElement
    {
        public override void OnInitialize()
        {
            Width.Set(0f, 1f);
            Height.Set(0f, 1f);
            IgnoresMouseInteraction = true;
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            string label = owner.TextDisplayFunction?.Invoke() ?? owner.MemberInfo?.Name ?? "";
            CalculatedStyle box = owner.GetDimensions();
            ChatManager.DrawColorCodedStringWithShadow(spriteBatch, FontAssets.ItemStack.Value, label,
                new Vector2(box.X + TextLeft, box.Y + 8f), Color.White, 0f, Vector2.Zero,
                new Vector2(.8f), Math.Max(20f, box.Width - TextLeft - 8f), 2f);
        }
    }
}
