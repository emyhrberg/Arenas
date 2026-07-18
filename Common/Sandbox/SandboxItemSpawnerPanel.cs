using Arenas.Common.UI;
using Arenas.Core;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria.GameContent.UI.Elements;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader.UI;
using Terraria.ModLoader.UI.Elements;
using Terraria.UI;

namespace Arenas.Common.Sandbox;

internal sealed class SandboxItemSpawnerPanel : UIDraggablePanel
{
    private enum Filter : byte { All, Weapons, Melee, Ranged, Magic, Summon, Armor, Vanity, Accessories, Potions, Blocks, Misc }
    private readonly List<(Item Item, SandboxItemSlot Slot)> items = [];
    private readonly List<SpawnerFilterButton> filters = [], mods = [];
    private readonly UIGrid grid = new() { ListPadding = 0f };
    private readonly UIScrollbar scroll = new();
    private readonly SpawnerSearchBox search = new("Search");
    private readonly UIText count = new("", .65f);
    private Filter filter; private string mod = "";
    protected override float MinResizeW => 390f; protected override float MinResizeH => 420f;
    protected override float MaxResizeW => 620f; protected override float MaxResizeH => 800f;

    public SandboxItemSpawnerPanel() : base("Item Spawner")
    {
        Width.Set(450, 0); Height.Set(600, 0); HAlign = .5f; Top.Set(90, 0); Left.Set(231, 0); Content.SetPadding(8);
        foreach (Item item in ContentSamples.ItemsByType.OrderBy(x => x.Key).Select(x => x.Value).Where(x => x?.IsAir == false)) items.Add((item.Clone(), new SandboxItemSlot(item, size: 44)));
        Asset<Texture2D>[] icons = [Ass.FilterAll, Ass.FilterMelee, Ass.FilterMelee, Ass.FilterRanged, Ass.FilterMagic, Ass.FilterSummon, Ass.FilterArmor, Ass.FilterVanity, Ass.FilterAccessories, Ass.FilterPotion, Ass.FilterPlaceables, Ass.FilterMisc];
        for (int i = 0; i < icons.Length; i++) AddFilter(icons[i], (Filter)i, i * 24); AddModFilters();
        search.Top.Set(0, 0); search.Left.Set(-140, 1); search.Width.Set(140, 0); search.Height.Set(24, 0); search.OnChanged = Rebuild; Content.Append(search);
        grid.Top.Set(60, 0); grid.Width.Set(-26, 1); grid.Height.Set(-84, 1); grid.SetScrollbar(scroll); Content.Append(grid);
        scroll.Top.Set(60, 0); scroll.Left.Set(-20, 1); scroll.Width.Set(20, 0); scroll.Height.Set(-84, 1); Content.Append(scroll);
        count.Top.Set(-18, 1); count.HAlign = .5f; Content.Append(count); Rebuild();
    }

    protected override void OnClosePanelLeftClick() { search.Unfocus(); ModContent.GetInstance<SandboxUISystem>().Hide(); }
    protected override void OnRefreshPanelLeftClick() { search.SetText(""); mod = ""; filter = Filter.All; Rebuild(); }
    private void AddFilter(Asset<Texture2D> icon, Filter value, float left) { SpawnerFilterButton b = new(icon, value.ToString(), left); b.OnLeftClick += (_, _) => { filter = value; Rebuild(); }; filters.Add(b); Content.Append(b); }
    private void AddModFilters()
    {
        Mod[] owners = [.. ModLoader.Mods.Where(x => x.GetContent<ModItem>().Any())]; string[] names = ["", .. owners.Select(x => x.Name)]; float left = 0;
        foreach (string name in names) { Mod owner = owners.FirstOrDefault(x => x.Name == name); SpawnerFilterButton b = new(Ass.FilterAll, owner?.DisplayNameClean ?? "All Items", left, name); b.OnLeftClick += (_, _) => { mod = name; Rebuild(); }; mods.Add(b); Content.Append(b); left += 24; }
    }
    private void Rebuild()
    {
        string text = search.Text.Trim(); var found = items.Where(x => (text.Length == 0 || x.Item.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase)) && (mod.Length == 0 || x.Item.ModItem?.Mod.Name == mod) && Matches(x.Item)).ToList();
        grid.Clear(); grid.AddRange(found.Select(x => x.Slot)); scroll.ViewPosition = 0; count.SetText($"{found.Count} Items"); for (int i = 0; i < filters.Count; i++) filters[i].Active = i == (int)filter; foreach (SpawnerFilterButton b in mods) b.Active = b.Mod == mod;
    }
    private bool Matches(Item x) => filter switch
    {
        Filter.All => true, Filter.Weapons => x.damage > 0, Filter.Melee => Weapon(x, DamageClass.Melee), Filter.Ranged => Weapon(x, DamageClass.Ranged), Filter.Magic => Weapon(x, DamageClass.Magic), Filter.Summon => Weapon(x, DamageClass.Summon),
        Filter.Armor => x.defense > 0 && (x.headSlot >= 0 || x.bodySlot >= 0 || x.legSlot >= 0), Filter.Vanity => x.vanity, Filter.Accessories => x.accessory, Filter.Potions => x.potion || x.consumable && x.buffType > 0, Filter.Blocks => x.createTile >= TileID.Dirt || x.createWall >= 0,
        Filter.Misc => x.damage <= 0 && !x.vanity && !x.accessory && !x.potion && !(x.consumable && x.buffType > 0) && x.createTile < TileID.Dirt && x.createWall < 0 && !(x.defense > 0 && (x.headSlot >= 0 || x.bodySlot >= 0 || x.legSlot >= 0)), _ => false
    };
    private static bool Weapon(Item item, DamageClass type) => item.damage > 0 && item.DamageType == type;
}

internal sealed class SpawnerFilterButton(Asset<Texture2D> icon, string tip, float left, string mod = "") : UIElement
{
    internal bool Active; internal string Mod => mod;
    public override void OnInitialize() { Left.Set(left, 0); Top.Set(mod.Length == 0 && tip != "All Items" ? 0 : 32, 0); Width.Set(21, 0); Height.Set(21, 0); }
    protected override void DrawSelf(SpriteBatch batch) { Texture2D tex = mod.Length > 0 && ModContent.RequestIfExists($"{mod}/icon", out Asset<Texture2D> a) ? a.Value : icon.Value; Rectangle box = GetDimensions().ToRectangle(); batch.Draw(tex, box, null, Active ? Color.White : Color.White * (IsMouseHovering ? .9f : .65f)); if (IsMouseHovering) UICommon.TooltipMouseText(tip); }
}

internal sealed class SpawnerSearchBox(string hint) : UIPanel
{
    private string text = ""; private bool focused; internal Action OnChanged; internal string Text => text;
    internal void SetText(string value) { text = value ?? ""; OnChanged?.Invoke(); }
    internal void Unfocus() => focused = Main.blockInput = false;
    public override void OnInitialize() { SetPadding(0); BackgroundColor = Color.White; BorderColor = Color.Black; }
    public override void LeftClick(UIMouseEvent evt) { Main.clrInput(); Main.blockInput = focused = true; }
    public override void Update(GameTime time) { base.Update(time); if (focused && !IsMouseHovering && (Main.mouseLeft || Main.mouseRight)) Unfocus(); if (IsMouseHovering) PlayerInput.LockVanillaMouseScroll("Arenas/ItemSearch"); }
    protected override void DrawSelf(SpriteBatch batch) { base.DrawSelf(batch); if (focused) { PlayerInput.WritingText = true; Main.instance.HandleIME(); string next = Main.GetInputText(text); if (next != text) { text = next.Length > 30 ? next[..30] : next; OnChanged?.Invoke(); } } string shown = text.Length == 0 && !focused ? hint : text + (focused && Main.GameUpdateCount / 20 % 2 == 0 ? "|" : ""); Utils.DrawBorderString(batch, shown, GetDimensions().Position() + new Vector2(7, 5), text.Length == 0 && !focused ? Color.Gray : Color.Black, .72f); }
}
