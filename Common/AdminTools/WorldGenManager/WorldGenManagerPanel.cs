using Arenas.Common.AdminTools.GameManager;
using Arenas.Common.UI;
using Arenas.Core;
using System;
using Terraria.Audio;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.Localization;
using Terraria.UI;

namespace Arenas.Common.AdminTools.WorldGenManager;

internal sealed class WorldGenManagerPanel : UIDraggablePanel
{
    private static readonly string[] Steps =
    [
        "Reset",
        "Terrain",
        "Dunes",
        "Ocean Sand",
        "Sand Patches",
        "Tunnels",
        "Mount Caves",
        "Dirt Wall Backgrounds",
        "Rocks In Dirt",
        "Dirt In Rocks",
        "Clay",
        "Small Holes",
        "Dirt Layer Caves",
        "Rock Layer Caves",
        "Surface Caves",
        "Wavy Caves",
        "Generate Ice Biome",
        "Grass",
        "Jungle",
        "Mud Caves To Grass",
        "Full Desert",
        "Floating Islands",
        "Mushroom Patches",
        "Marble",
        "Granite",
        "Dirt To Mud",
        "Silt",
        "Shinies",
        "Webs",
        "Underworld",
        "Corruption",
        "Lakes",
        "Dungeon",
        "Slush",
        "Mountain Caves",
        "Beaches",
        "Gems",
        "Gravitating Sand",
        "Create Ocean Caves",
        "Shimmer",
        "Clean Up Dirt",
        "Pyramids",
        "Dirt Rock Wall Runner",
        "Living Trees",
        "Wood Tree Walls",
        "Altars",
        "Wet Jungle",
        "Jungle Temple",
        "Hives",
        "Jungle Chests",
        "Settle Liquids",
        "Remove Water From Sand",
        "Oasis",
        "Shell Piles",
        "Smooth World",
        "Waterfalls",
        "Ice",
        "Wall Variety",
        "Life Crystals",
        "Statues",
        "Buried Chests",
        "Surface Chests",
        "Jungle Chests Placement",
        "Water Chests",
        "Spider Caves",
        "Gem Caves",
        "Moss",
        "Temple",
        "Cave Walls",
        "Jungle Trees",
        "Floating Island Houses",
        "Quick Cleanup",
        "Pots",
        "Hellforge",
        "Spreading Grass",
        "Surface Ore and Stone",
        "Place Fallen Log",
        "Traps",
        "Piles",
        "Spawn Point",
        "Grass Wall",
        "Guide",
        "Sunflowers",
        "Planting Trees",
        "Herbs",
        "Dye Plants",
        "Webs And Honey",
        "Weeds",
        "Glowing Mushrooms and Jungle Plants",
        "Jungle Plants",
        "Vines",
        "Flowers",
        "Mushrooms",
        "Gems In Ice Biome",
        "Random Gems",
        "Moss Grass",
        "Muds Walls In Jungle",
        "Larva",
        "Settle Liquids Again",
        "Cactus, Palm Trees, & Coral",
        "Tile Cleanup",
        "Lihzahrd Altars",
        "Micro Biomes",
        "Water Plants",
        "Stalac",
        "Remove Broken Traps",
        "Final Cleanup"
    ];

    private readonly bool[] completed = new bool[Steps.Length];
    private readonly UIScrollbar stepScrollbar;

    protected override float MinResizeW => 480f;
    protected override float MinResizeH => 500f;
    protected override float MaxResizeW => 620f;
    protected override float MaxResizeH => 760f;

    public WorldGenManagerPanel() : base(Language.GetTextValue("Mods.Arenas.Tools.WorldGenManagerPanel.Title"))
    {
        Width.Set(540f, 0f);
        Height.Set(620f, 0f);
        HAlign = .5f;
        Top.Set(90f, 0f);
        Content.SetPadding(6f);

        ArenaManagerSection actions = new("Actions", 116f);
        actions.Add(new ArenaManagerButton(
            () => "Clear all tiles",
            Ass.IconEndGame,
            ClearAllTiles,
            () => true,
            () => "Clear every tile and reset world generation progress",
            true), 38f);
        actions.Add(new ArenaManagerButton(
            NextStepText,
            Ass.IconStartGame,
            PerformNextStep,
            () => NextStepIndex >= 0,
            NextStepTooltip), 76f);
        Content.Append(actions);

        ArenaManagerSection steps = new("World Generation Steps", 0f)
        {
            Top = { Pixels = 126f },
            Height = { Pixels = -126f, Percent = 1f }
        };

        WorldGenStepCounter counter = new(() => CompletedCount, Steps.Length)
        {
            Left = { Pixels = -190f, Percent = 1f },
            Top = { Pixels = 5f },
            Width = { Pixels = 178f },
            Height = { Pixels = 22f }
        };
        steps.Append(counter);

        UIList stepList = new()
        {
            Left = { Pixels = 7f },
            Top = { Pixels = 36f },
            Width = { Pixels = -41f, Percent = 1f },
            Height = { Pixels = -43f, Percent = 1f },
            ListPadding = 4f
        };
        stepScrollbar = new UIScrollbar
        {
            Left = { Pixels = -27f, Percent = 1f },
            Top = { Pixels = 36f },
            Width = { Pixels = 20f },
            Height = { Pixels = -43f, Percent = 1f }
        };
        stepList.SetScrollbar(stepScrollbar);

        for (int i = 0; i < Steps.Length; i++)
        {
            int index = i;
            stepList.Add(new WorldGenStepButton(index, Steps[index], () => completed[index], () => PerformStep(index)));
        }

        steps.Append(stepList);
        steps.Append(stepScrollbar);
        Content.Append(steps);
    }

    protected override void OnClosePanelLeftClick() => ModContent.GetInstance<WorldGenManagerUISystem>().Hide();

    protected override void OnRefreshPanelLeftClick()
    {
        Main.NewText("TODO: Refresh completed world gen step state.");
        ResetProgress();
    }

    private int CompletedCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < completed.Length; i++)
                if (completed[i])
                    count++;
            return count;
        }
    }

    private int NextStepIndex => Array.FindIndex(completed, value => !value);

    private string NextStepText()
    {
        int index = NextStepIndex;
        return index < 0
            ? $"All world gen steps complete [{Steps.Length}/{Steps.Length}]"
            : $"Perform next world gen step ({Steps[index]}) [{index + 1}/{Steps.Length}]";
    }

    private string NextStepTooltip()
    {
        int index = NextStepIndex;
        return index < 0 ? "Every world generation step is complete" : $"Perform {Steps[index]}";
    }

    private void ClearAllTiles()
    {
        Main.NewText("TODO: Clear all tiles in the current world.");
        ResetProgress();
    }

    private void PerformNextStep()
    {
        int index = NextStepIndex;
        if (index >= 0)
            PerformStep(index);
    }

    private void PerformStep(int index)
    {
        string action = completed[index] ? "Re-run" : "Perform";
        Main.NewText($"TODO: {action} world gen step \"{Steps[index]}\".");
        completed[index] = true;
    }

    private void ResetProgress()
    {
        Array.Fill(completed, false);
        stepScrollbar.ViewPosition = 0f;
    }
}

internal sealed class WorldGenStepCounter(Func<int> completed, int total) : UIElement
{
    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        Rectangle box = GetDimensions().ToRectangle();
        ArenaGameManagerText.Draw(spriteBatch, $"{completed()}/{total} complete", new Vector2(box.Right, box.Y + 3f), Color.White, .64f, box.Width, 1f);
    }
}

internal sealed class WorldGenStepButton(int index, string name, Func<bool> completed, Action action) : UIElement
{
    public override void OnInitialize()
    {
        Width.Set(0f, 1f);
        Height.Set(34f, 0f);
    }

    public override void LeftClick(UIMouseEvent evt)
    {
        base.LeftClick(evt);
        SoundEngine.PlaySound(SoundID.MenuTick);
        action();
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        if (!IsMouseHovering)
            return;

        Main.LocalPlayer.mouseInterface = true;
        Main.instance.MouseText(completed() ? $"Re-run {name}" : $"Perform {name}");
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        Rectangle box = GetDimensions().ToRectangle();
        bool done = completed();
        Color background = done
            ? IsMouseHovering ? new Color(48, 150, 82) : new Color(36, 112, 64)
            : IsMouseHovering ? new Color(160, 55, 68) : new Color(120, 35, 45);
        ArenaGameManagerText.Panel(spriteBatch, box, background, IsMouseHovering ? Color.Yellow : Color.Black);
        ArenaGameManagerText.Draw(spriteBatch, $"{index + 1:000}  {name}", new Vector2(box.X + 10f, box.Y + 9f), Color.White, .7f, box.Width - 105f);
        ArenaGameManagerText.Draw(spriteBatch, done ? "DONE" : "PENDING", new Vector2(box.Right - 10f, box.Y + 10f), Color.White, .58f, 80f, 1f);
    }
}
