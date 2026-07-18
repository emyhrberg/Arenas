using Arenas.Common.Rounds;
using Arenas.Core;
using Arenas.Core.Configs.ConfigElements;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader.Config;
using Terraria.UI;

namespace Arenas.Common.UI;

internal sealed class AdminLoadoutUIState : UIState
{
    public override void OnInitialize()
    {
        Append(new AdminLoadoutPanel());
        Append(new SandboxItemSpawnerPanel());
    }
}

internal sealed class AdminLoadoutPanel : UIDraggablePanel
{
    private UIList list;
    private UIScrollbar scrollbar;

    protected override float MinResizeW => 520f;
    protected override float MinResizeH => 360f;
    protected override float MaxResizeW => 760f;
    protected override float MaxResizeH => 800f;

    public AdminLoadoutPanel() : base("Sandbox Loadouts")
    {
        Width.Set(650f, 0f);
        Height.Set(600f, 0f);
        HAlign = .5f;
        VAlign = 0f;
        Left.Set(-331f, 0f);
        Top.Set(90f, 0f);
        ContentPanel.SetPadding(8f);
        BuildList();
    }

    protected override void OnClosePanelLeftClick() => ModContent.GetInstance<AdminUISystem>().Hide();
    protected override void OnRefreshPanelLeftClick() => BuildList();

    private void BuildList()
    {
        ContentPanel.RemoveAllChildren();
        list = new UIList
        {
            Width = { Pixels = -26f, Percent = 1f },
            Height = { Percent = 1f }
        };
        list.SetPadding(0f);
        list.ListPadding = 6f;

        scrollbar = new UIScrollbar
        {
            Left = { Pixels = -20f, Percent = 1f },
            Width = { Pixels = 20f },
            Height = { Percent = 1f }
        };
        list.SetScrollbar(scrollbar);
        ContentPanel.Append(list);
        ContentPanel.Append(scrollbar);

        List<BossFightPreset> presets = ArenaRoundSystem.GetValidPresets();
        for (int i = 0; i < presets.Count; i++)
            list.Add(new SandboxLoadoutRow(i, presets[i]));

        if (presets.Count == 0)
            list.Add(new UIText("No fight preset loadouts are configured.", .9f));

        list.Recalculate();
    }
}

internal sealed class SandboxLoadoutRow : UIPanel
{
    private readonly int presetIndex;
    private readonly BossFightPreset preset;
    private readonly UITextPanel<string> equipButton;

    public SandboxLoadoutRow(int presetIndex, BossFightPreset preset)
    {
        this.presetIndex = presetIndex;
        this.preset = preset;
        Width.Set(0f, 1f);
        Height.Set(116f, 0f);
        SetPadding(6f);
        BackgroundColor = new Color(30, 43, 98) * .92f;
        BorderColor = new Color(70, 89, 165);

        Append(new SandboxPresetIcon(preset)
        {
            Left = { Pixels = 5f },
            Top = { Pixels = 5f },
            Width = { Pixels = 52f },
            Height = { Pixels = 52f }
        });

        Append(new UIText(ArenaRoundSystem.PresetName(preset), 1f)
        {
            Left = { Pixels = 66f },
            Top = { Pixels = 4f }
        });
        Append(new UIText($"{preset.MaxHealth} life  •  {preset.MaxMana} mana", .72f)
        {
            Left = { Pixels = 66f },
            Top = { Pixels = 27f },
            TextColor = new Color(190, 210, 255)
        });

        equipButton = new UITextPanel<string>("Equip", .8f)
        {
            Width = { Pixels = 82f },
            Height = { Pixels = 34f },
            Left = { Pixels = -88f, Percent = 1f },
            Top = { Pixels = 8f },
            BackgroundColor = new Color(63, 82, 151),
            BorderColor = Color.Black
        };
        equipButton.OnMouseOver += (_, _) => equipButton.BorderColor = Color.Yellow;
        equipButton.OnMouseOut += (_, _) => equipButton.BorderColor = Color.Black;
        equipButton.OnLeftClick += (_, _) => SandboxAdminNetHandler.RequestLoadout(presetIndex);
        Append(equipButton);

        AddItemPreview();
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        bool selected = Main.LocalPlayer.GetModPlayer<ArenasPlayer>().SandboxLoadoutPresetIndex == presetIndex;
        BackgroundColor = selected ? new Color(48, 86, 122) * .96f : new Color(30, 43, 98) * .92f;
        if (IsMouseHovering)
            Main.LocalPlayer.mouseInterface = true;
    }

    private void AddItemPreview()
    {
        List<(ItemDefinition definition, int stack)> items = [];
        Loadout loadout = preset.Loadout ?? new Loadout();
        Armor armor = loadout.Armor ?? new Armor();
        Accessories accessories = loadout.Accessories ?? new Accessories();
        items.Add((armor.Head, 1));
        items.Add((armor.Body, 1));
        items.Add((armor.Legs, 1));
        items.Add((accessories.Accessory1, 1));
        items.Add((accessories.Accessory2, 1));
        foreach (LoadoutItem entry in loadout.Inventory ?? [])
        {
            if (items.Count >= 14) break;
            items.Add((entry?.Item, entry?.Stack ?? 1));
        }

        for (int i = 0; i < items.Count; i++)
        {
            Item item = new();
            int type = items[i].definition?.Type ?? ItemID.None;
            if (type > ItemID.None)
            {
                item.SetDefaults(type);
                item.stack = Math.Clamp(items[i].stack, 1, Math.Max(1, item.maxStack));
            }

            Append(new SandboxItemSlot(item, interactive: false)
            {
                Left = { Pixels = 6f + i * 39f },
                Top = { Pixels = 67f }
            });
        }
    }
}

internal sealed class SandboxPresetIcon(BossFightPreset preset) : UIElement
{
    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        Rectangle box = GetDimensions().ToRectangle();
        if (ArenaRoundSystem.IsSandboxPreset(preset))
        {
            Texture2D icon = Ass.IconArenas.Value;
            float scale = Math.Min(box.Width / (float)icon.Width, box.Height / (float)icon.Height);
            spriteBatch.Draw(icon, box.Center.ToVector2(), null, Color.White, 0f, icon.Size() * .5f, scale, SpriteEffects.None, 0f);
            return;
        }
        ArenaBossVoteDrawer.DrawBossHead(preset?.Boss?.Type ?? 0, box);
    }
}

internal sealed class SandboxItemSlot : UIElement
{
    private readonly float size;
    private readonly Item item;
    private readonly bool interactive;

    internal SandboxItemSlot(Item item, bool interactive = true, float size = 36f)
    {
        this.item = item?.Clone() ?? new Item();
        this.interactive = interactive;
        this.size = size;
        Width.Set(size, 0f);
        Height.Set(size, 0f);
    }

    public override void LeftClick(UIMouseEvent evt)
    {
        base.LeftClick(evt);
        if (interactive && !item.IsAir)
            SandboxAdminNetHandler.RequestItem(item.type, Math.Max(1, item.maxStack));
    }

    public override void RightClick(UIMouseEvent evt)
    {
        base.RightClick(evt);
        if (interactive && !item.IsAir)
            SandboxAdminNetHandler.RequestItem(item.type, 1);
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        if (!IsMouseHovering || item.IsAir)
            return;
        Main.LocalPlayer.mouseInterface = true;
        Main.HoverItem = item.Clone();
        Main.hoverItemName = item.Name;
        if (interactive)
            Main.instance.MouseText($"{item.Name}\nLeft: Stack  •  Right: One");
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        Rectangle box = GetDimensions().ToRectangle();
        Texture2D background = TextureAssets.InventoryBack.Value;
        spriteBatch.Draw(background, box.Center.ToVector2(), null, Color.White, 0f, background.Size() * .5f,
            size / background.Width, SpriteEffects.None, 0f);
        if (!item.IsAir)
            ItemSlot.DrawItemIcon(item, ItemSlot.Context.InventoryItem, spriteBatch, box.Center.ToVector2(), 1f, Math.Min(24f, size), Color.White);
        if (item.stack > 1)
            Utils.DrawBorderString(spriteBatch, item.stack.ToString(), new Vector2(box.X + 3f, box.Bottom - 15f), Color.White, .55f);
    }
}
