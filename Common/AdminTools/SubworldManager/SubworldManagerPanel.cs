using Arenas.Common.UI;
using Arenas.Core;
using System;
using System.Linq;
using Terraria.GameContent.UI.Elements;
using Terraria.Localization;
using Terraria.UI;

namespace Arenas.Common.AdminTools.SubworldManager;

internal sealed class SubworldManagerPanel : UIDraggablePanel
{
    private readonly UIList players;
    private string roster = "";

    protected override float MinResizeW => 320f;
    protected override float MinResizeH => 260f;

    public SubworldManagerPanel() : base(Language.GetTextValue("Mods.Arenas.Tools.SubworldManagerPanel.Title"))
    {
        Width.Set(560f, 0f);
        Height.Set(420f, 0f);
        HAlign = .5f;
        VAlign = .7f;
        Content.SetPadding(6f);

        Content.Append(ActionButton("Send all to main world", 0f,
            () => Request(SubworldManagerNetHandler.Action.SendAllToMainWorld)));
        Content.Append(ActionButton("Send all to arenas", .5f,
            () => Request(SubworldManagerNetHandler.Action.SendAllToArenas)));
        Content.Append(new UIText("Players", .9f) { Left = { Pixels = 4f }, Top = { Pixels = 66f } });

        players = new UIList
        {
            Top = { Pixels = 88f },
            Width = { Pixels = -24f, Percent = 1f },
            Height = { Pixels = -96f, Percent = 1f },
            ListPadding = 6f
        };
        players.SetPadding(0f);
        UIScrollbar scrollbar = new()
        {
            Top = { Pixels = 88f },
            Left = { Pixels = -20f, Percent = 1f },
            Width = { Pixels = 20f },
            Height = { Pixels = -96f, Percent = 1f }
        };
        players.SetScrollbar(scrollbar);
        Content.Append(players);
        Content.Append(scrollbar);
        RebuildPlayers();
    }

    protected override void OnClosePanelLeftClick() => ModContent.GetInstance<SubworldManagerUISystem>().Toggle();

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        if (Main.GameUpdateCount % 30 == 0 && CurrentRoster() != roster)
            RebuildPlayers();
    }

    private void RebuildPlayers()
    {
        roster = CurrentRoster();
        players.Clear();
        foreach (Player player in Main.player.Where(player => player?.active == true))
            players.Add(PlayerRow(player));
        players.Recalculate();
    }

    private static UIElement PlayerRow(Player player)
    {
        UIPanel row = new()
        {
            Width = { Percent = 1f },
            Height = { Pixels = 40f },
            BackgroundColor = new Color(10, 10, 10) * .65f,
            BorderColor = Color.Black
        };
        row.SetPadding(6f);
        row.OnMouseOver += (_, _) => row.BorderColor = Color.Yellow;
        row.OnMouseOut += (_, _) => row.BorderColor = Color.Black;
        row.Append(new UIText(player == Main.LocalPlayer ? $"{player.name} (you)" : player.name, .9f) { VAlign = .5f });

        int id = player.whoAmI;
        row.Append(Icon(Ass.IconStartGame.Value, -92f, () => $"Send {PlayerName(id)} to main world",
            () => Request(SubworldManagerNetHandler.Action.SendPlayerToMainWorld, id)));
        row.Append(Icon(Ass.IconArenas.Value, -52f, () => $"Send {PlayerName(id)} to arenas",
            () => Request(SubworldManagerNetHandler.Action.SendPlayerToArenas, id)));
        return row;
    }

    private static UIImage Icon(Texture2D texture, float left, Func<string> tooltip, Action action)
    {
        UIImage icon = new(texture)
        {
            ImageScale = .7f,
            Left = { Pixels = left, Percent = 1f },
            Top = { Pixels = -6f },
            Width = { Pixels = 40f },
            Height = { Pixels = 40f }
        };
        icon.OnMouseOver += (_, _) => Main.instance.MouseText(tooltip());
        icon.OnLeftClick += (_, _) => action();
        return icon;
    }

    private static UITextPanel<string> ActionButton(string text, float left, Action action)
    {
        UITextPanel<string> button = new(text)
        {
            Left = { Percent = left, Pixels = left == 0f ? 0f : 6f },
            Top = { Pixels = 8f },
            Width = { Percent = .5f, Pixels = -6f },
            Height = { Pixels = 44f }
        };
        button.OnMouseOver += (_, _) => button.BorderColor = Color.Yellow;
        button.OnMouseOut += (_, _) => button.BorderColor = Color.Black;
        button.OnLeftClick += (_, _) => action();
        return button;
    }

    private static string CurrentRoster() => string.Join('|', Main.player.Where(player => player?.active == true).Select(player => $"{player.whoAmI}:{player.name}"));
    private static string PlayerName(int id) => id >= 0 && id < Main.maxPlayers && Main.player[id]?.active == true ? Main.player[id].name : "player";
    private static void Request(SubworldManagerNetHandler.Action action, int player = -1) => SubworldManagerNetHandler.Request(action, player);
}
