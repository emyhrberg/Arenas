//using Arenas.Common.AdminTools.GameManager;
//using Arenas.Common.Generation;
//using Arenas.Common.UI;
//using Arenas.Core;
//using System;
//using Terraria.Audio;
//using Terraria.GameContent.UI.Elements;
//using Terraria.ID;
//using Terraria.Localization;
//using Terraria.UI;

//namespace Arenas.Common.AdminTools.WorldGenManager;

//internal sealed class WorldGenManagerPanel : UIDraggablePanel
//{
//    private static string[] Steps => ArenaWorldGenerationCatalog.Steps;
//    private readonly UIScrollbar stepScrollbar;

//    protected override float MinResizeW => 480f;
//    protected override float MinResizeH => 500f;
//    protected override float MaxResizeW => 620f;
//    protected override float MaxResizeH => 760f;

//    public WorldGenManagerPanel() : base(Language.GetTextValue("Mods.Arenas.Tools.WorldGenManagerPanel.Title"))
//    {
//        Width.Set(540f, 0f);
//        Height.Set(620f, 0f);
//        HAlign = .5f;
//        Top.Set(90f, 0f);
//        Content.SetPadding(6f);

//        ArenaManagerSection actions = new("Actions", 192f);
//        actions.Add(new ArenaManagerButton(
//            () => "Clear all tiles",
//            Ass.IconEndGame,
//            ClearAllTiles,
//            () => !WorldGenManagerNetHandler.Busy,
//            () => "Evacuate Arenas, clear every tile/entity, and leave every player in Main",
//            true), 38f);
//        actions.Add(new ArenaManagerButton(
//            NextStepText,
//            Ass.IconStartGame,
//            PerformNextStep,
//            () => NextStepIndex >= 0 && !WorldGenManagerNetHandler.Busy,
//            NextStepTooltip), 76f);
//        actions.Add(new ArenaManagerButton(
//            () => "Generate complete match world",
//            Ass.IconRefresh,
//            WorldGenManagerNetHandler.RequestCompleteWorld,
//            () => !WorldGenManagerNetHandler.Busy,
//            () => "Run Terraria's complete pipeline plus all Arenas passes; players remain in Main"), 114f);
//        actions.Add(new ArenaManagerButton(
//            () => ArenaWorldSystem.Active ? "Return to Main" : "Enter generated preview",
//            Ass.IconArenas,
//            WorldGenManagerNetHandler.RequestPreviewTransition,
//            () => !WorldGenManagerNetHandler.Busy && (ArenaWorldSystem.Active || WorldGenManagerNetHandler.ServerAvailable),
//            () => ArenaWorldSystem.Active ? "Return this admin to Main" : "Inspect the current generated prefix without regenerating it"), 152f);
//        Content.Append(actions);

//        ArenaManagerSection steps = new("World Generation Steps", 0f)
//        {
//            Top = { Pixels = 202f },
//            Height = { Pixels = -202f, Percent = 1f }
//        };

//        WorldGenStepCounter counter = new(() => CompletedCount, Steps.Length, () => WorldGenManagerNetHandler.Status)
//        {
//            Left = { Pixels = 7f },
//            Top = { Pixels = 5f },
//            Width = { Pixels = -14f, Percent = 1f },
//            Height = { Pixels = 42f }
//        };
//        steps.Append(counter);

//        UIList stepList = new()
//        {
//            Left = { Pixels = 7f },
//            Top = { Pixels = 56f },
//            Width = { Pixels = -41f, Percent = 1f },
//            Height = { Pixels = -63f, Percent = 1f },
//            ListPadding = 4f
//        };
//        stepScrollbar = new UIScrollbar
//        {
//            Left = { Pixels = -27f, Percent = 1f },
//            Top = { Pixels = 56f },
//            Width = { Pixels = 20f },
//            Height = { Pixels = -63f, Percent = 1f }
//        };
//        stepList.SetScrollbar(stepScrollbar);

//        for (int i = 0; i < Steps.Length; i++)
//        {
//            int index = i;
//            stepList.Add(new WorldGenStepButton(index, Steps[index], () => index <= WorldGenManagerNetHandler.CompletedStep, () => PerformStep(index)));
//        }

//        steps.Append(stepList);
//        steps.Append(stepScrollbar);
//        Content.Append(steps);
//    }

//    protected override void OnClosePanelLeftClick() => ModContent.GetInstance<WorldGenManagerUISystem>().Hide();

//    protected override void OnRefreshPanelLeftClick()
//    {
//        WorldGenManagerNetHandler.RequestState();
//    }

//    private int CompletedCount
//    {
//        get
//        {
//            return Math.Clamp(WorldGenManagerNetHandler.CompletedStep + 1, 0, Steps.Length);
//        }
//    }

//    private int NextStepIndex => CompletedCount >= Steps.Length ? -1 : CompletedCount;

//    private string NextStepText()
//    {
//        int index = NextStepIndex;
//        return index < 0
//            ? $"All world gen steps complete [{Steps.Length}/{Steps.Length}]"
//            : $"Perform next world gen step ({Steps[index]}) [{index + 1}/{Steps.Length}]";
//    }

//    private string NextStepTooltip()
//    {
//        int index = NextStepIndex;
//        return index < 0 ? "Every world generation step is complete" : $"Perform {Steps[index]}";
//    }

//    private void ClearAllTiles()
//    {
//        WorldGenManagerNetHandler.RequestClear();
//    }

//    private void PerformNextStep()
//    {
//        int index = NextStepIndex;
//        if (index >= 0)
//            PerformStep(index);
//    }

//    private void PerformStep(int index)
//    {
//        if (WorldGenManagerNetHandler.Busy || !ArenaWorldGenerationCatalog.IsValidIndex(index))
//            return;
//        Main.NewText($"Rebuilding Arenas from Reset through \"{Steps[index]}\". All players will remain in Main.", Color.Orange);
//        WorldGenManagerNetHandler.RequestThroughStep(index);
//    }
//}

//internal sealed class WorldGenStepCounter(Func<int> completed, int total, Func<string> status) : UIElement
//{
//    protected override void DrawSelf(SpriteBatch spriteBatch)
//    {
//        Rectangle box = GetDimensions().ToRectangle();
//        ArenaGameManagerText.Draw(spriteBatch, $"{completed()}/{total} complete", new Vector2(box.Right, box.Y + 3f), Color.White, .64f, box.Width, 1f);
//        ArenaGameManagerText.Draw(spriteBatch, status(), new Vector2(box.Right, box.Y + 22f), Color.LightGray, .52f, box.Width, 1f);
//    }
//}

//internal sealed class WorldGenStepButton(int index, string name, Func<bool> completed, Action action) : UIElement
//{
//    public override void OnInitialize()
//    {
//        Width.Set(0f, 1f);
//        Height.Set(34f, 0f);
//    }

//    public override void LeftClick(UIMouseEvent evt)
//    {
//        base.LeftClick(evt);
//        SoundEngine.PlaySound(SoundID.MenuTick);
//        action();
//    }

//    public override void Update(GameTime gameTime)
//    {
//        base.Update(gameTime);
//        if (!IsMouseHovering)
//            return;

//        Main.LocalPlayer.mouseInterface = true;
//        Main.instance.MouseText(completed() ? $"Re-run {name}" : $"Perform {name}");
//    }

//    protected override void DrawSelf(SpriteBatch spriteBatch)
//    {
//        Rectangle box = GetDimensions().ToRectangle();
//        bool done = completed();
//        Color background = done
//            ? IsMouseHovering ? new Color(48, 150, 82) : new Color(36, 112, 64)
//            : IsMouseHovering ? new Color(160, 55, 68) : new Color(120, 35, 45);
//        ArenaGameManagerText.Panel(spriteBatch, box, background, IsMouseHovering ? Color.Yellow : Color.Black);
//        ArenaGameManagerText.Draw(spriteBatch, $"{index + 1:000}  {name}", new Vector2(box.X + 10f, box.Y + 9f), Color.White, .7f, box.Width - 105f);
//        ArenaGameManagerText.Draw(spriteBatch, done ? "DONE" : "PENDING", new Vector2(box.Right - 10f, box.Y + 10f), Color.White, .58f, 80f, 1f);
//    }
//}
