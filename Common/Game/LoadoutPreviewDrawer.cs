using Arenas.Common.DataStructures;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria.GameContent;
using Terraria.GameInput;
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

    private const int InventoryColumns = 10;
    private const int SlotStep = 40;
    private const int SlotSize = 36;
    private const int HeaderHeight = 60;
    private const int PreviewWidth = 96;
    private const int SidePadding = 14;
    private const int PreviewInventoryGap = 12;
    private const int InventoryEquipmentGap = 10;
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

    private const int PreviewJumpDuration = 32;
    private const float PreviewJumpHeight = 30f;

    private static int previewJumpTicks;
    private static bool previewJumpWasDown;

    private static Item[] classIcons;
    private static Player previewPlayer;
    private static int cachedPresetIndex = -1;
    private static ArenaClass localClass = ArenaClass.None;
    private static ArenaClass hoveredClass = ArenaClass.None;
    private static ArenaClass cachedDisplayClass = ArenaClass.None;
    private static bool cacheValid;
    private static UIEntranceAnimation entrance;
    private static float alpha = 1f;
    private static readonly List<SlotEntry> inventorySlots = [];
    private static readonly List<SlotEntry> equipmentSlots = [];
    private static ArenaClass DisplayClass
    {
        get
        {
            if (hoveredClass != ArenaClass.None)
                return hoveredClass;

            if (localClass != ArenaClass.None)
                return localClass;

            return Main.LocalPlayer
                .GetModPlayer<ArenaPlayer>()
                .SelectedClass;
        }
    }

    private static void InvalidateCache()
    {
        cacheValid = false;
    }

    private static int InventoryRowCount =>
        Math.Max(1, (inventorySlots.Count + InventoryColumns - 1) / InventoryColumns);

    private static int EquipmentColumnCount
    {
        get
        {
            if (equipmentSlots.Count == 0)
                return 0;

            return (equipmentSlots.Count + InventoryRowCount - 1) / InventoryRowCount;
        }
    }
    public static void Draw(int top)
    {
        RoundManager manager = ModContent.GetInstance<RoundManager>();
        if (!manager.TryGetSelectedPreset(out BossFightPreset preset))
            return;

        bool classSelect = PostMechKits.Supports(preset);
        Point mouse = new(Main.mouseX, Main.mouseY);

        entrance.Advance();
        alpha = entrance.Alpha;
        top -= entrance.SlideOffset;

        if (manager.SelectedPresetIndex != cachedPresetIndex)
        {
            localClass = Main.LocalPlayer
                .GetModPlayer<ArenaPlayer>()
                .SelectedClass;

            hoveredClass = ArenaClass.None;
            InvalidateCache();
        }

        // Resolve hover and layout. Run twice because a hovered class may have a
        // different inventory size, which can change the panel width.
        Rectangle panel = Rectangle.Empty;
        float scale = 1f;

        for (int pass = 0; pass < 3; pass++)
        {
            EnsureRebuilt(manager.SelectedPresetIndex, preset);

            int inventoryRows = InventoryRowCount;
            int equipmentColumns = EquipmentColumnCount;

            int designWidth =
                SidePadding * 2
                + PreviewWidth
                + PreviewInventoryGap
                + InventoryColumns * SlotStep
                + (equipmentColumns > 0
                    ? InventoryEquipmentGap + equipmentColumns * SlotStep
                    : 0);

            int designHeight =
                HeaderHeight
                + (classSelect ? ClassRowHeight : 0)
                + inventoryRows * SlotStep
                + SidePadding;

            scale = Math.Min(
                1f,
                Math.Min(
                    (Main.screenWidth - 12f) / designWidth,
                    (Main.screenHeight - top - 2f) / designHeight));

            panel = new Rectangle(
                (Main.screenWidth - S(designWidth)) / 2,
                top,
                S(designWidth),
                S(designHeight));

            ArenaClass newHoveredClass = classSelect
                ? GetHoveredClass(panel, panel.Y + S(HeaderHeight), S, mouse)
                : ArenaClass.None;

            if (newHoveredClass == hoveredClass)
                break;

            hoveredClass = newHoveredClass;
            InvalidateCache();
        }

        EnsureRebuilt(manager.SelectedPresetIndex, preset);

        int finalInventoryRows = InventoryRowCount;
        int finalEquipmentColumns = EquipmentColumnCount;

        int finalDesignWidth =
            SidePadding * 2
            + PreviewWidth
            + PreviewInventoryGap
            + InventoryColumns * SlotStep
            + (finalEquipmentColumns > 0
                ? InventoryEquipmentGap + finalEquipmentColumns * SlotStep
                : 0);

        int finalDesignHeight =
            HeaderHeight
            + (classSelect ? ClassRowHeight : 0)
            + finalInventoryRows * SlotStep
            + SidePadding;

        scale = Math.Min(
            1f,
            Math.Min(
                (Main.screenWidth - 12f) / finalDesignWidth,
                (Main.screenHeight - top - 2f) / finalDesignHeight));

        int S(float value) => Math.Max(1, (int)MathF.Round(value * scale));

        panel = new Rectangle(
            (Main.screenWidth - S(finalDesignWidth)) / 2,
            top,
            S(finalDesignWidth),
            S(finalDesignHeight));

        DrawPanel(panel, PanelFill, PanelEdge, S(9));

        Utils.DrawBorderStringBig(
            Main.spriteBatch,
            "Loadout",
            new Vector2(panel.Center.X, panel.Y + S(4)),
            Yellow * alpha,
            .62f * scale,
            .5f,
            0f);

        string subtitle = classSelect
            ? $"Pick your gear for {Lang.GetNPCNameValue(preset.Boss?.Type ?? 0)}!"
            : $"Your gear for {Lang.GetNPCNameValue(preset.Boss?.Type ?? 0)}!";

        Text(
            subtitle,
            new Vector2(panel.Center.X, panel.Y + S(36)),
            Color.White,
            .84f * scale,
            panel.Width - S(30));

        if (panel.Contains(mouse))
            Main.LocalPlayer.mouseInterface = true;

        int contentTop = panel.Y + S(HeaderHeight);

        if (classSelect)
        {
            DrawClassCards(panel, contentTop, S, scale, mouse);
            contentTop += S(ClassRowHeight) - 4;
        }

        int gridHeight = finalInventoryRows * S(SlotStep);

        Rectangle previewBox = new(
            panel.X + S(SidePadding),
            contentTop,
            S(PreviewWidth),
            gridHeight);

        DrawPreviewBox(previewBox, mouse);

        int inventoryOriginX = previewBox.Right + S(PreviewInventoryGap);

        DrawInventorySlots(
            inventoryOriginX,
            contentTop,
            scale,
            mouse,
            S);

        if (equipmentSlots.Count > 0)
        {
            int equipmentOriginX =
                inventoryOriginX
                + InventoryColumns * S(SlotStep)
                + S(InventoryEquipmentGap);

            DrawEquipmentSlots(
                equipmentOriginX,
                contentTop,
                finalInventoryRows,
                scale,
                mouse,
                S);
        }
    }
    private static void DrawInventorySlots(
    int originX,
    int originY,
    float scale,
    Point mouse,
    Func<float, int> S)
    {
        for (int i = 0; i < inventorySlots.Count; i++)
        {
            int column = i % InventoryColumns;
            int row = i / InventoryColumns;

            Rectangle cell = new(
                originX + column * S(SlotStep),
                originY + row * S(SlotStep),
                S(SlotSize),
                S(SlotSize));

            DrawSlot(cell, inventorySlots[i], scale, mouse);
        }
    }

    private static void DrawEquipmentSlots(
        int originX,
        int originY,
        int rowCount,
        float scale,
        Point mouse,
        Func<float, int> S)
    {
        for (int i = 0; i < equipmentSlots.Count; i++)
        {
            // Equipment fills vertically first, then expands rightward.
            int column = i / rowCount;
            int row = i % rowCount;

            Rectangle cell = new(
                originX + column * S(SlotStep),
                originY + row * S(SlotStep),
                S(SlotSize),
                S(SlotSize));

            DrawSlot(cell, equipmentSlots[i], scale, mouse);
        }
    }

    private static void DrawClassCards(
        Rectangle panel,
        int top,
        Func<float, int> S,
        float scale,
        Point mouse)
    {
        classIcons ??= BuildClassIcons();

        int gap = S(CardGap);
        int side = S(SidePadding);
        int cardWidth = (panel.Width - side * 2 - gap * 3) / 4;
        int cardHeight = S(ClassRowHeight - 16);

        for (int i = 0; i < ClassCards.Length; i++)
        {
            (ArenaClass arenaClass, string name, string subtitle) = ClassCards[i];

            Rectangle card = new(
                panel.X + side + i * (cardWidth + gap),
                top,
                cardWidth,
                cardHeight);

            bool hover = hoveredClass == arenaClass;
            bool selected = localClass == arenaClass;

            Color fill = hover && !selected
                ? RowHover
                : RowFill;

            Color edge = selected
                ? Selected
                : hover
                    ? HoverEdge
                    : DarkEdge;

            DrawPanel(card, fill, edge, S(12));

            if (hover)
            {
                Main.LocalPlayer.mouseInterface = true;

                if (Main.mouseLeft &&
                    Main.mouseLeftRelease &&
                    !selected)
                {
                    Main.mouseLeftRelease = false;

                    localClass = arenaClass;
                    ArenaPlayer.RequestClassSelect(arenaClass);

                    // The hovered preview already represents this class, so no
                    // visible rebuild is necessary until the mouse leaves.
                    InvalidateCache();
                }
            }

            Rectangle iconBox = new(
                card.Center.X - S(16),
                card.Y + S(6),
                S(32),
                S(32));

            DrawPanel(
                iconBox,
                new Color(34, 49, 111),
                DarkEdge,
                S(5));

            if (i < classIcons.Length && !classIcons[i].IsAir)
            {
                ItemSlot.DrawItemIcon(
                    classIcons[i],
                    31,
                    Main.spriteBatch,
                    iconBox.Center.ToVector2(),
                    scale * 1f,
                    iconBox.Width - 6f,
                    Color.White * alpha);
            }

            Color nameColor = selected
                ? new Color(206, 255, 142)
                : hover
                    ? Yellow
                    : Color.White;

            Text(
                name,
                new Vector2(card.Center.X, card.Y + S(40)),
                nameColor,
                .8f * scale,
                cardWidth - S(6));

            //Text(
            //    subtitle,
            //    new Vector2(card.Center.X, card.Y + S(56)),
            //    Color.White * .75f,
            //    .6f * scale,
            //    cardWidth - S(6));
        }
    }
    private static ArenaClass GetHoveredClass(
    Rectangle panel,
    int top,
    Func<float, int> S,
    Point mouse)
    {
        int gap = S(CardGap);
        int side = S(SidePadding);
        int cardWidth = (panel.Width - side * 2 - gap * 3) / 4;
        int cardHeight = S(ClassRowHeight - 10);

        for (int i = 0; i < ClassCards.Length; i++)
        {
            Rectangle card = new(
                panel.X + side + i * (cardWidth + gap),
                top,
                cardWidth,
                cardHeight);

            if (card.Contains(mouse))
                return ClassCards[i].Class;
        }

        return ArenaClass.None;
    }

    private static Item[] BuildClassIcons()
    {
        Item[] icons = new Item[ClassCards.Length];
        for (int i = 0; i < ClassCards.Length; i++)
            icons[i] = MakeItem(PostMechKits.HeadItem(ClassCards[i].Class));
        return icons;
    }

    private static void EnsureRebuilt(
    int presetIndex,
    BossFightPreset preset)
    {
        ArenaClass displayClass = DisplayClass;

        if (cacheValid &&
            presetIndex == cachedPresetIndex &&
            displayClass == cachedDisplayClass &&
            previewPlayer != null)
        {
            return;
        }

        Rebuild(presetIndex, preset, displayClass);
    }

    private static void Rebuild(
        int presetIndex,
        BossFightPreset preset,
        ArenaClass displayClass)
    {
        cachedPresetIndex = presetIndex;
        cachedDisplayClass = displayClass;
        cacheValid = true;

        Loadout loadout = null;

        if (PostMechKits.Supports(preset) &&
            displayClass != ArenaClass.None)
        {
            loadout = PostMechKits.Create(displayClass);
        }

        loadout ??= ArenaPlayer.ResolveLoadout(preset);

        equipmentSlots.Clear();
        inventorySlots.Clear();

        AddEquipment(loadout.Armor?.Head);
        AddEquipment(loadout.Armor?.Body);
        AddEquipment(loadout.Armor?.Legs);

        AddEquipment(loadout.Accessories?.Accessory1);
        AddEquipment(loadout.Accessories?.Accessory2);
        AddEquipment(loadout.Accessories?.Accessory3);
        AddEquipment(loadout.Accessories?.Accessory4);
        AddEquipment(loadout.Accessories?.Accessory5);

        AddEquipment(loadout.Equipment?.GrapplingHook);
        AddEquipment(loadout.Equipment?.Mount);

        int inventoryCount = Math.Min(
            loadout.Inventory?.Count ?? 0,
            50);

        // Always include the full hotbar. Beyond that, preserve the preset's
        // actual inventory length, including empty slots.
        int displayedInventoryCount = Math.Max(10, inventoryCount);

        for (int i = 0; i < displayedInventoryCount; i++)
        {
            LoadoutItem entry =
                i < inventoryCount
                    ? loadout.Inventory[i]
                    : null;

            int type = entry?.Item?.Type ?? 0;
            int stack = entry?.Stack ?? 1;

            inventorySlots.Add(new SlotEntry(
                MakeItem(type, stack),
                false,
                i < 10 ? i == 9 ? 10 : i + 1 : null));
        }

        previewPlayer = BuildPreviewPlayer(loadout);
    }

    private static void AddEquipment(ItemDefinition definition)
    {
        equipmentSlots.Add(new SlotEntry(
            MakeItem(definition?.Type ?? 0),
            true,
            null));
    }

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

    private static void DrawPreviewBox(Rectangle box, Point mouse)
    {
        SpriteBatch spriteBatch = Main.spriteBatch;

        Utils.DrawSplicedPanel(
            spriteBatch,
            PlayerBack,
            box.X,
            box.Y,
            box.Width,
            box.Height,
            12,
            12,
            12,
            12,
            Color.White * alpha);

        if (previewPlayer == null)
            return;

        bool animated = box.Contains(mouse);

        if (animated)
            Main.LocalPlayer.mouseInterface = true;

        Vector2 drawPosition = Main.screenPosition + new Vector2(
            box.Center.X - previewPlayer.width / 2f,
            box.Center.Y - previewPlayer.height / 2f);

        previewPlayer.velocity = Vector2.Zero;
        previewPlayer.active = true;
        previewPlayer.dead = false;
        previewPlayer.ghost = false;
        previewPlayer.isDisplayDollOrInanimate = true;

        previewPlayer.PlayerFrame();

        float jumpOffset = ApplyPreviewAnimation(animated);
        drawPosition.Y -= jumpOffset;

        previewPlayer.position = drawPosition;

        GraphicsDevice device = spriteBatch.GraphicsDevice;

        BlendState previousBlend = device.BlendState;
        SamplerState previousSampler = device.SamplerStates[0];
        DepthStencilState previousDepth = device.DepthStencilState;
        RasterizerState previousRasterizer = device.RasterizerState;

        bool wasMenu = Main.gameMenu;
        bool playerBatchActive = false;

        spriteBatch.End();

        try
        {
            Main.gameMenu = true;

            spriteBatch.Begin(
                SpriteSortMode.Immediate,
                BlendState.AlphaBlend,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone,
                null,
                Main.UIScaleMatrix);

            playerBatchActive = true;

            Main.PlayerRenderer.DrawPlayer(
                Main.Camera,
                previewPlayer,
                drawPosition,
                0f,
                Vector2.Zero,
                0f,
                1f);

            spriteBatch.End();
            playerBatchActive = false;
        }
        finally
        {
            if (playerBatchActive)
                spriteBatch.End();

            Main.gameMenu = wasMenu;

            spriteBatch.Begin(
                SpriteSortMode.Deferred,
                previousBlend ?? BlendState.AlphaBlend,
                previousSampler ?? SamplerState.LinearClamp,
                previousDepth ?? DepthStencilState.None,
                previousRasterizer ?? RasterizerState.CullNone,
                null,
                Main.UIScaleMatrix);
        }
    }

    private static float ApplyPreviewAnimation(bool hovered)
    {
        bool jumpDown = PlayerInput.Triggers.Current.Jump;
        bool jumpPressed = jumpDown && !previewJumpWasDown;
        previewJumpWasDown = jumpDown;

        previewPlayer.SetCompositeArmFront(
            false,
            Player.CompositeArmStretchAmount.Full,
            0f);

        previewPlayer.SetCompositeArmBack(
            false,
            Player.CompositeArmStretchAmount.Full,
            0f);

        previewPlayer.direction = 1;

        // Jump can be started regardless of hover state.
        if (jumpPressed && previewJumpTicks <= 0)
            previewJumpTicks = PreviewJumpDuration;

        // Continue an active jump even when the mouse is not over the preview.
        if (previewJumpTicks > 0)
        {
            float progress =
                1f - previewJumpTicks / (float)PreviewJumpDuration;

            previewJumpTicks--;

            SetPreviewBodyFrame(5);

            return MathF.Sin(progress * MathF.PI)
                * PreviewJumpHeight;
        }

        // Idle when not hovered.
        if (!hovered)
        {
            SetPreviewBodyFrame(0);
            return 0f;
        }

        // Walk while hovered.
        int frame =
            7 + (int)((Main.GameUpdateCount / 5UL) % 13UL);

        SetPreviewBodyFrame(frame);
        previewPlayer.WingFrame(false);

        return 0f;
    }

    private static void SetPreviewBodyFrame(int frame)
    {
        previewPlayer.bodyFrame.Y =
            frame * previewPlayer.bodyFrame.Height;

        previewPlayer.legFrame.Y =
            frame * previewPlayer.legFrame.Height;

        // Keep the head facing forward rather than cycling with the body.
        previewPlayer.headFrame.Y = 0;
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
