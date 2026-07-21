using Arenas.Common.DataStructures;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader.Config;
using Terraria.UI;
using static Arenas.Common.DataStructures.FightPresets;

namespace Arenas.Common.Game;

/// <summary>
/// Shows the selected preset's loadout below the scoreline during the freeze countdown:
/// a player preview wearing the gear next to the full slot grid (armor, accessories,
/// equipment, numbered hotbar and remaining inventory). Presets with class kits show a
/// clickable class selector row that re-equips the local player.
/// </summary>
internal static class LoadoutPreviewDrawer
{
    private readonly record struct SlotEntry(Item Item, bool Equip, int? HotbarNumber);

    private const int Columns = 10, SlotStep = 40, SlotSize = 36, HeaderHeight = 60, PreviewWidth = 96, SidePadding = 14;
    private const int ClassRowHeight = 78, CardGap = 8;
    private static readonly Color PanelFill = new(45, 61, 132), PanelEdge = new(70, 89, 165), Yellow = new(246, 216, 72);
    private static readonly Color RowFill = new(30, 43, 98), RowHover = new(68, 86, 158);
    private static readonly Color DarkEdge = new(6, 12, 38), HoverEdge = new(244, 209, 74), Selected = new(104, 222, 72);

    private static readonly (ArenaClass Class, string Name, string Subtitle)[] ClassCards =
    [
        (ArenaClass.Melee, "Melee", "Blade & plate"),
        (ArenaClass.Ranger, "Ranged", "Bow & arrows"),
        (ArenaClass.Mage, "Mage", "Gems & robes"),
        (ArenaClass.Summoner, "Summoner", "Whips & minions")
    ];

    private static Texture2D PanelBackground => Main.Assets.Request<Texture2D>("Images/UI/PanelBackground").Value;
    private static Texture2D PanelBorder => Main.Assets.Request<Texture2D>("Images/UI/PanelBorder").Value;
    private static Texture2D PlayerBack => Main.Assets.Request<Texture2D>("Images/UI/PlayerBackground").Value;

    private static readonly List<SlotEntry> slots = [];
    private static Item[] classIcons;
    private static Player previewPlayer;
    private static int cachedPresetIndex = -1;
    private static ArenaClass cachedClass = ArenaClass.None;
    private static ArenaClass localClass = ArenaClass.None;
    private static UIEntranceAnimation entrance;
    private static float alpha = 1f;

    public static void Draw(int top)
    {
        RoundManager manager = ModContent.GetInstance<RoundManager>();
        if (!manager.TryGetSelectedPreset(out BossFightPreset preset))
            return;

        if (manager.SelectedPresetIndex != cachedPresetIndex)
            localClass = ArenaClass.None;
        if (manager.SelectedPresetIndex != cachedPresetIndex || localClass != cachedClass || previewPlayer == null)
            Rebuild(manager.SelectedPresetIndex, preset);
        if (slots.Count == 0)
            return;

        entrance.Advance();
        alpha = entrance.Alpha;
        top -= entrance.SlideOffset;

        bool classSelect = PostMechKits.Supports(preset);
        int rows = (slots.Count + Columns - 1) / Columns;
        int designWidth = SidePadding * 2 + PreviewWidth + 12 + Columns * SlotStep;
        int designHeight = HeaderHeight + (classSelect ? ClassRowHeight : 0) + rows * SlotStep + SidePadding;
        float scale = Math.Min(1f, Math.Min((Main.screenWidth - 12f) / designWidth,
            (Main.screenHeight - top - 2f) / designHeight));
        int S(float value) => Math.Max(1, (int)MathF.Round(value * scale));

        Rectangle panel = new((Main.screenWidth - S(designWidth)) / 2, top, S(designWidth), S(designHeight));
        DrawPanel(panel, PanelFill, PanelEdge, S(9));
        Utils.DrawBorderStringBig(Main.spriteBatch, "Loadout",
            new Vector2(panel.Center.X, panel.Y + S(12) - 2), Yellow * alpha, .62f * scale, .5f, 0f);
        string subtitle = classSelect
            ? $"Pick your gear for {Lang.GetNPCNameValue(preset.Boss?.Type ?? 0)}!"
            : $"Your gear for {Lang.GetNPCNameValue(preset.Boss?.Type ?? 0)}!";
        Text(subtitle, new Vector2(panel.Center.X, panel.Y + S(38)), Color.White, .84f * scale, panel.Width - S(30));

        Point mouse = new(Main.mouseX, Main.mouseY);
        if (panel.Contains(mouse))
            Main.LocalPlayer.mouseInterface = true;

        int contentTop = panel.Y + S(HeaderHeight);
        if (classSelect)
        {
            DrawClassCards(panel, contentTop, S, scale, mouse);
            contentTop += S(ClassRowHeight);
        }

        Rectangle previewBox = new(panel.X + S(SidePadding), contentTop,
            S(PreviewWidth), panel.Bottom - S(SidePadding) - contentTop);
        DrawPreviewBox(previewBox);

        int originX = previewBox.Right + S(12);
        for (int i = 0; i < slots.Count; i++)
        {
            Rectangle cell = new(originX + i % Columns * S(SlotStep), contentTop + i / Columns * S(SlotStep),
                S(SlotSize), S(SlotSize));
            DrawSlot(cell, slots[i], scale, mouse);
        }
    }

    private static void DrawClassCards(Rectangle panel, int top, Func<float, int> S, float scale, Point mouse)
    {
        classIcons ??= BuildClassIcons();
        int gap = S(CardGap), side = S(SidePadding);
        int cardWidth = (panel.Width - side * 2 - gap * 3) / 4;
        int cardHeight = S(ClassRowHeight - 10);

        for (int i = 0; i < ClassCards.Length; i++)
        {
            (ArenaClass arenaClass, string name, string subtitle) = ClassCards[i];
            Rectangle card = new(panel.X + side + i * (cardWidth + gap), top, cardWidth, cardHeight);
            bool hover = card.Contains(mouse), selected = localClass == arenaClass;
            DrawPanel(card, hover && !selected ? RowHover : RowFill,
                selected ? Selected : hover ? HoverEdge : DarkEdge, S(6));

            if (hover)
            {
                Main.LocalPlayer.mouseInterface = true;
                if (Main.mouseLeft && Main.mouseLeftRelease && !selected)
                {
                    localClass = arenaClass;
                    ArenaPlayer.RequestClassSelect(arenaClass);
                }
            }

            Rectangle iconBox = new(card.Center.X - S(16), card.Y + S(6), S(32), S(32));
            DrawPanel(iconBox, new Color(34, 49, 111), DarkEdge, S(5));
            if (i < classIcons.Length && !classIcons[i].IsAir)
                ItemSlot.DrawItemIcon(classIcons[i], 31, Main.spriteBatch, iconBox.Center.ToVector2(),
                    scale * .8f, iconBox.Width - 6f, Color.White * alpha);

            Color nameColor = selected ? new Color(206, 255, 142) : hover ? Yellow : Color.White;
            Text(name, new Vector2(card.Center.X, card.Y + S(40)), nameColor, .8f * scale, cardWidth - S(6));
            Text(subtitle, new Vector2(card.Center.X, card.Y + S(56)), Color.White * .75f, .6f * scale, cardWidth - S(6));
        }
    }

    private static Item[] BuildClassIcons()
    {
        Item[] icons = new Item[ClassCards.Length];
        for (int i = 0; i < ClassCards.Length; i++)
            icons[i] = MakeItem(PostMechKits.HeadItem(ClassCards[i].Class));
        return icons;
    }

    private static void Rebuild(int presetIndex, BossFightPreset preset)
    {
        cachedPresetIndex = presetIndex;
        cachedClass = localClass;
        Loadout loadout = null;
        if (PostMechKits.Supports(preset) && localClass != ArenaClass.None)
            loadout = PostMechKits.Create(localClass);
        loadout ??= ArenaPlayer.ResolveLoadout(preset);
        slots.Clear();

        AddEquip(loadout.Armor?.Head);
        AddEquip(loadout.Armor?.Body);
        AddEquip(loadout.Armor?.Legs);
        AddEquip(loadout.Accessories?.Accessory1);
        AddEquip(loadout.Accessories?.Accessory2);
        AddEquip(loadout.Accessories?.Accessory3);
        AddEquip(loadout.Accessories?.Accessory4);
        AddEquip(loadout.Accessories?.Accessory5);
        AddEquip(loadout.Equipment?.GrapplingHook);
        AddEquip(loadout.Equipment?.Mount);

        int inventoryCount = loadout.Inventory?.Count ?? 0;
        for (int i = 0; i < 10; i++)
        {
            LoadoutItem entry = i < inventoryCount ? loadout.Inventory[i] : null;
            slots.Add(new SlotEntry(MakeItem(entry?.Item?.Type ?? 0, entry?.Stack ?? 1), false, i == 9 ? 10 : i + 1));
        }

        for (int i = 10; i < inventoryCount && i < 50; i++)
        {
            LoadoutItem entry = loadout.Inventory[i];
            if ((entry?.Item?.Type ?? 0) <= 0)
                continue;
            slots.Add(new SlotEntry(MakeItem(entry.Item.Type, entry.Stack), false, null));
        }

        previewPlayer = BuildPreviewPlayer(loadout);
    }

    private static void AddEquip(ItemDefinition definition) =>
        slots.Add(new SlotEntry(MakeItem(definition?.Type ?? 0), true, null));

    private static Item MakeItem(int type, int stack = 1)
    {
        Item item = new();
        if (type > 0)
        {
            item.SetDefaults(type);
            item.stack = Math.Max(1, stack);
        }
        return item;
    }

    private static Player BuildPreviewPlayer(Loadout loadout)
    {
        Player preview = new()
        {
            active = true
        };
        Player source = Main.LocalPlayer;
        if (source != null)
        {
            preview.skinVariant = source.skinVariant;
            preview.Male = source.Male;
            preview.hair = source.hair;
            preview.hairColor = source.hairColor;
            preview.skinColor = source.skinColor;
            preview.eyeColor = source.eyeColor;
            preview.shirtColor = source.shirtColor;
            preview.underShirtColor = source.underShirtColor;
            preview.pantsColor = source.pantsColor;
            preview.shoeColor = source.shoeColor;
        }

        void Set(int slot, ItemDefinition definition)
        {
            if ((definition?.Type ?? 0) > 0 && slot < preview.armor.Length)
                preview.armor[slot].SetDefaults(definition.Type);
        }

        Set(0, loadout.Armor?.Head);
        Set(1, loadout.Armor?.Body);
        Set(2, loadout.Armor?.Legs);
        Set(3, loadout.Accessories?.Accessory1);
        Set(4, loadout.Accessories?.Accessory2);
        Set(5, loadout.Accessories?.Accessory3);
        Set(6, loadout.Accessories?.Accessory4);
        Set(7, loadout.Accessories?.Accessory5);

        preview.direction = 1;
        preview.ResetEffects();
        preview.ResetVisibleAccessories();
        preview.DisplayDollUpdate();
        preview.PlayerFrame();
        return preview;
    }

    private static void DrawPreviewBox(Rectangle box)
    {
        Utils.DrawSplicedPanel(Main.spriteBatch, PlayerBack, box.X, box.Y, box.Width, box.Height,
            12, 12, 12, 12, Color.White * alpha);
        if (previewPlayer == null)
            return;

        previewPlayer.position = Main.screenPosition
            + new Vector2(box.Center.X - previewPlayer.width / 2f, box.Bottom - previewPlayer.height - 8f);
        previewPlayer.velocity = Vector2.Zero;

        // Menu mode makes the renderer skip world lighting, which would otherwise draw the
        // preview pitch black (invisible) whenever the UI happens to sit over unlit tiles.
        bool wasMenu = Main.gameMenu;
        Main.gameMenu = true;
        try
        {
            Main.PlayerRenderer.DrawPlayer(Main.Camera, previewPlayer, previewPlayer.position,
                0f, previewPlayer.fullRotationOrigin, 0f, 1f);
        }
        finally
        {
            Main.gameMenu = wasMenu;
        }
    }

    private static void DrawSlot(Rectangle cell, SlotEntry entry, float uiScale, Point mouse)
    {
        Texture2D back = (entry.Equip ? TextureAssets.InventoryBack3 : TextureAssets.InventoryBack).Value;
        float backScale = cell.Width / (float)back.Width;
        Main.spriteBatch.Draw(back, cell.Center.ToVector2(), null, Color.White * alpha, 0f,
            back.Size() / 2f, backScale, SpriteEffects.None, 0f);

        if (!entry.Item.IsAir)
            ItemSlot.DrawItemIcon(entry.Item, 31, Main.spriteBatch, cell.Center.ToVector2(),
                uiScale * .85f, cell.Width - 8f, Color.White * alpha);

        float textScale = .62f * Math.Max(.75f, uiScale);
        if (entry.HotbarNumber.HasValue)
            Utils.DrawBorderString(Main.spriteBatch,
                entry.HotbarNumber.Value == 10 ? "0" : entry.HotbarNumber.Value.ToString(),
                new Vector2(cell.X + 4, cell.Y + 2), Color.White * alpha, textScale);

        if (entry.Item.stack > 1)
            Utils.DrawBorderString(Main.spriteBatch, entry.Item.stack.ToString(),
                new Vector2(cell.X + 5, cell.Bottom - 16f * Math.Max(.75f, uiScale)),
                Color.White * alpha, textScale);

        if (!entry.Item.IsAir && cell.Contains(mouse))
        {
            Main.LocalPlayer.mouseInterface = true;
            Main.HoverItem = entry.Item.Clone();
            Main.hoverItemName = entry.Item.Name;
        }
    }

    private static void DrawPanel(Rectangle rectangle, Color fill, Color edge, int corner)
    {
        Utils.DrawSplicedPanel(Main.spriteBatch, PanelBackground, rectangle.X, rectangle.Y,
            rectangle.Width, rectangle.Height, corner, corner, corner, corner, fill * alpha);
        Utils.DrawSplicedPanel(Main.spriteBatch, PanelBorder, rectangle.X, rectangle.Y,
            rectangle.Width, rectangle.Height, corner, corner, corner, corner, edge * alpha);
    }

    private static void Text(string value, Vector2 position, Color color, float scale, float maxWidth)
    {
        float width = FontAssets.MouseText.Value.MeasureString(value).X * scale;
        if (width > maxWidth)
            scale *= maxWidth / width;
        Utils.DrawBorderString(Main.spriteBatch, value, position, color * alpha, scale, .5f);
    }
}
