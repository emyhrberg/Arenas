using Microsoft.Xna.Framework.Graphics;
using MonoMod.RuntimeDetour;
using ReLogic.Content;
using System;
using System.Collections;
using System.Reflection;
using Terraria.GameContent;
using Terraria.Localization;
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
        MemberInfo memberInfo = member?.MemberInfo;
        ConfigIconAttribute icon = memberInfo?.GetCustomAttribute<ConfigIconAttribute>(true);
        Asset<Texture2D> on = GetTexture(icon);
        if (result?.Item2 == null)
            return result;

        if (on != null)
        {
            ConfigElement config = FindConfig(result.Item2);
            result.Item2.Append(new ConfigIconImage(on, GetOffTexture(icon), GetBool(item, memberInfo), icon.GrayWhenOff)
            {
                Left = { Pixels = 8f },
                VAlign = .5f
            });

            if (config != null)
            {
                config.DrawLabel = false;
                result.Item2.Append(new ConfigIconLabel(config));
            }
        }

        if (memberInfo?.GetCustomAttribute<RequiresFieldAttribute>(true) is { } requires
            && GetBool(item, requires.FieldName) is { } requiredEnabled)
        {
            result.Item2.Append(new RequiresFieldOverlay(requiredEnabled, GetRequiredLabel(item?.GetType(), requires.FieldName)));
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

    private static Func<bool> GetBool(object owner, string memberName)
    {
        if (owner == null || string.IsNullOrWhiteSpace(memberName))
            return null;

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
        Type type = owner.GetType();
        if (type.GetField(memberName, flags) is { FieldType: { } fieldType } field && fieldType == typeof(bool))
            return () => (bool)field.GetValue(owner);
        if (type.GetProperty(memberName, flags) is { PropertyType: { } propertyType, CanRead: true } property && propertyType == typeof(bool))
            return () => (bool)property.GetValue(owner);
        return null;
    }

    private static string GetRequiredLabel(Type ownerType, string memberName)
    {
        string key = $"Mods.Arenas.Configs.{ownerType?.Name}.{memberName}.Label";
        string localized = Language.GetTextValue(key);
        if (!string.IsNullOrWhiteSpace(localized) && localized != key)
            return localized;

        string label = "";
        foreach (char character in memberName ?? "")
        {
            if (label.Length > 0 && char.IsUpper(character) && !char.IsWhiteSpace(label[^1]))
                label += ' ';
            label += character;
        }
        return label;
    }

    private static ConfigElement FindConfig(UIElement element)
    {
        if (element is ConfigElement config)
            return config;
        foreach (UIElement child in element.Children)
            if (FindConfig(child) is { } nested)
                return nested;
        return null;
    }

    private sealed class ConfigIconImage(Asset<Texture2D> on, Asset<Texture2D> off, Func<bool> enabled, bool grayWhenOff) : UIElement
    {
        public override void OnInitialize()
        {
            Width.Set(IconSize, 0f);
            Height.Set(IconSize, 0f);
            IgnoresMouseInteraction = true;
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            bool isOff = enabled?.Invoke() == false;
            Texture2D texture = (off != null && isOff ? off : on).Value;
            Rectangle box = GetDimensions().ToRectangle();
            float scale = Math.Min(box.Width / (float)texture.Width, box.Height / (float)texture.Height);
            Color color = isOff && grayWhenOff ? Color.Gray : Color.White;
            spriteBatch.Draw(texture, box.Center.ToVector2(), null, color * .85f, 0f, texture.Size() * .5f, scale, SpriteEffects.None, 0f);
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
            string label = CollapseDuplicateWords(owner.TextDisplayFunction?.Invoke() ?? owner.MemberInfo?.Name ?? "");
            CalculatedStyle box = owner.GetDimensions();
            ChatManager.DrawColorCodedStringWithShadow(spriteBatch, FontAssets.ItemStack.Value, label,
                new Vector2(box.X + TextLeft, box.Y + 8f), Color.White, 0f, Vector2.Zero,
                new Vector2(.8f), Math.Max(20f, box.Width - TextLeft - 8f), 2f);
        }

        private static string CollapseDuplicateWords(string value)
        {
            string[] words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 2)
                return value;

            int write = 1;
            for (int read = 1; read < words.Length; read++)
            {
                if (!string.Equals(words[read], words[write - 1], StringComparison.OrdinalIgnoreCase))
                    words[write++] = words[read];
            }

            for (int phraseLength = 1; phraseLength * 2 <= write; phraseLength++)
            {
                bool repeated = true;
                for (int i = 0; i < phraseLength; i++)
                    repeated &= string.Equals(words[i], words[i + phraseLength], StringComparison.OrdinalIgnoreCase);
                if (!repeated)
                    continue;

                for (int i = phraseLength * 2; i < write; i++)
                    words[i - phraseLength] = words[i];
                write -= phraseLength;
                break;
            }

            return string.Join(' ', words, 0, write);
        }
    }

    private sealed class RequiresFieldOverlay(Func<bool> enabled, string requiredLabel) : UIElement
    {
        public override void OnInitialize()
        {
            Width.Set(0f, 1f);
            Height.Set(0f, 1f);
            IgnoresMouseInteraction = IsEnabled();
        }

        public override void Update(GameTime gameTime)
        {
            IgnoresMouseInteraction = IsEnabled();
            if (!IgnoresMouseInteraction && ContainsPoint(Main.MouseScreen))
                UIModConfig.Tooltip = $"[c/FF8080:Locked: Requires {requiredLabel} to be enabled]";
            base.Update(gameTime);
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            if (IsEnabled())
                return;

            Rectangle box = GetDimensions().ToRectangle();
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, box, Color.Black * .48f);
        }

        private bool IsEnabled()
        {
            try
            {
                return enabled();
            }
            catch
            {
                return true;
            }
        }
    }
}
