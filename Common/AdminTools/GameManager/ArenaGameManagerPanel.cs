using Arenas.Common.AdminTools.UI;
using Terraria.GameContent.UI.Elements;
using Terraria.Localization;

namespace Arenas.Common.AdminTools.GameManager;

internal sealed class ArenaGameManagerPanel : UIDraggablePanel
{
    protected override float MinResizeW => 480f;
    protected override float MinResizeH => 500f;
    protected override float MaxResizeW => 620f;
    protected override float MaxResizeH => 760f;

    public ArenaGameManagerPanel() : base(Language.GetTextValue("Mods.Arenas.Tools.ArenaGameManagerPanel.Title"))
    {
        Width.Set(540, 0);
        Height.Set(620, 0);
        HAlign = .5f;
        Top.Set(90, 0);
        Content.SetPadding(6);

        UIScrollbar scrollbar = new() { Left = { Pixels = -20, Percent = 1 }, Width = { Pixels = 20 }, Height = { Percent = 1 } };
        UIList list = new() { Width = { Pixels = -26, Percent = 1 }, Height = { Percent = 1 }, ListPadding = 10 };
        list.SetScrollbar(scrollbar);
        Content.Append(list);
        Content.Append(scrollbar);
    }

    protected override void OnClosePanelLeftClick() => ModContent.GetInstance<ArenaGameManagerUISystem>().Close();

    protected override void OnRefreshPanelLeftClick()
    {
    }
}
