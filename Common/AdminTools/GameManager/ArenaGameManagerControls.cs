using Arenas.Common.Game;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Linq;
using Terraria.Audio;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.UI;

namespace Arenas.Common.AdminTools.GameManager;

internal sealed class ArenaGameStatusPanel : UIPanel
{
    internal ArenaGameStatusPanel()
    {
        SetPadding(0f);
        BackgroundColor = new Color(20, 27, 62) * .95f;
        BorderColor = new Color(78, 104, 190) * .8f;
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        base.DrawSelf(spriteBatch);
        Rectangle panel = GetDimensions().ToRectangle();
        RoundManager manager = ModContent.GetInstance<RoundManager>();
        string phase = manager.CurrentPhase == RoundManager.RoundPhase.WaitingForPlayers && manager.IsIdleHeld
            ? "Waiting"
            : PhaseName(manager.CurrentPhase);
        if (manager.IsTimerPaused)
            phase += " (paused)";

        const float scale = .72f;
        const string label = "Current phase:";
        Utils.DrawBorderString(spriteBatch, label, new Vector2(panel.X + 11, panel.Y + 8),
            new Color(174, 216, 226), scale);
        float labelWidth = FontAssets.MouseText.Value.MeasureString(label).X * scale;
        DrawText(spriteBatch, phase, new Vector2(panel.X + 11 + labelWidth + 6, panel.Y + 8), Color.White,
            scale, panel.Width - labelWidth - 106f);

        if (!IsTimed(manager.CurrentPhase))
            return;

        int seconds = Math.Max(0, (int)Math.Ceiling(manager.RemainingTicks / 60f));
        DrawText(spriteBatch, $"{seconds / 60}:{seconds % 60:00}",
            new Vector2(panel.Right - 11, panel.Y + 8), Color.White, scale, 68f, 1f);
    }

    private static string PhaseName(RoundManager.RoundPhase phase) => phase switch
    {
        RoundManager.RoundPhase.WaitingForPlayers => "Waiting for players",
        RoundManager.RoundPhase.VotingOrEndScreen => "Voting",
        RoundManager.RoundPhase.Generating => "Preparing arena",
        RoundManager.RoundPhase.FreezeCountdown => "Starting round",
        RoundManager.RoundPhase.Playing => "Round in progress",
        _ => "Waiting"
    };

    private static bool IsTimed(RoundManager.RoundPhase phase) =>
        phase is RoundManager.RoundPhase.VotingOrEndScreen
            or RoundManager.RoundPhase.FreezeCountdown
            or RoundManager.RoundPhase.Playing;

    private static void DrawText(SpriteBatch spriteBatch, string text, Vector2 position, Color color,
        float scale, float maxWidth, float anchor = 0f)
    {
        float measured = FontAssets.MouseText.Value.MeasureString(text).X * scale;
        if (measured > maxWidth)
            scale *= maxWidth / measured;
        Utils.DrawBorderString(spriteBatch, text, position, color, scale, anchor);
    }
}

internal sealed class ArenaTeamBalancePanel : UIPanel
{
    internal ArenaTeamBalancePanel()
    {
        SetPadding(0f);
        BackgroundColor = new Color(20, 27, 62) * .95f;
        BorderColor = new Color(78, 104, 190) * .8f;
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        base.DrawSelf(spriteBatch);
        Rectangle panel = GetDimensions().ToRectangle();
        Player[] active = Main.player.Where(player => player?.active == true).ToArray();
        Player[] red = active.Where(player => (Team)player.team == Team.Red).ToArray();
        Player[] blue = active.Where(player => (Team)player.team == Team.Blue).ToArray();

        DrawLine(spriteBatch, $"Player Count: {active.Length}", panel.X + 10, panel.Y + 5,
            new Color(174, 216, 226), panel.Width - 20);
        DrawLine(spriteBatch, TeamText("Red Team", red), panel.X + 10, panel.Y + 24,
            Main.teamColor[(int)Team.Red], panel.Width - 20);
        DrawLine(spriteBatch, TeamText("Blue Team", blue), panel.X + 10, panel.Y + 43,
            Main.teamColor[(int)Team.Blue], panel.Width - 20);
    }

    private static string TeamText(string label, Player[] players)
    {
        string names = players.Length == 0 ? "None" : string.Join(", ", players.Select(player => player.name));
        return $"{label}: {names} ({players.Length})";
    }

    private static void DrawLine(SpriteBatch spriteBatch, string text, int x, int y, Color color, float maxWidth)
    {
        float scale = .64f;
        float width = FontAssets.MouseText.Value.MeasureString(text).X * scale;
        if (width > maxWidth)
            scale *= maxWidth / width;
        Utils.DrawBorderString(spriteBatch, text, new Vector2(x, y), color, scale);
    }
}

internal sealed class ArenaGameCommandButton : UIPanel
{
    private readonly Func<string> label;
    private readonly Func<string> tooltip;
    private readonly Func<bool> enabled;
    private readonly Func<bool> danger;
    private readonly Action action;

    internal ArenaGameCommandButton(Func<string> label, Func<string> tooltip,
        Func<bool> enabled, Func<bool> danger, Action action)
    {
        this.label = label;
        this.tooltip = tooltip;
        this.enabled = enabled;
        this.danger = danger;
        this.action = action;
        SetPadding(0f);
    }

    public override void LeftClick(UIMouseEvent evt)
    {
        base.LeftClick(evt);
        if (!Enabled)
            return;

        SoundEngine.PlaySound(SoundID.MenuTick);
        action?.Invoke();
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        if (!IsMouseHovering)
            return;

        Main.LocalPlayer.mouseInterface = true;
        string value = tooltip?.Invoke();
        if (!string.IsNullOrWhiteSpace(value))
            Main.instance.MouseText(value);
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        bool active = Enabled;
        bool hovered = active && IsMouseHovering;
        bool destructive = active && (danger?.Invoke() ?? false);
        BackgroundColor = !active
            ? new Color(45, 45, 55) * .72f
            : destructive
                ? hovered ? new Color(170, 45, 60) : new Color(120, 35, 45)
                : hovered ? new Color(73, 94, 171) : new Color(55, 74, 140);
        BorderColor = hovered ? Color.Yellow : Color.Black;
        base.DrawSelf(spriteBatch);

        Rectangle panel = GetDimensions().ToRectangle();
        string text = label?.Invoke() ?? "";
        float scale = .78f;
        Vector2 size = FontAssets.MouseText.Value.MeasureString(text) * scale;
        if (size.X > panel.Width - 14f)
        {
            scale *= (panel.Width - 14f) / size.X;
            size = FontAssets.MouseText.Value.MeasureString(text) * scale;
        }

        Utils.DrawBorderString(spriteBatch, text,
            new Vector2(panel.Center.X, panel.Center.Y - size.Y / 2f + 1f),
            active ? Color.White : Color.Gray, scale, .5f);
    }

    private bool Enabled => enabled?.Invoke() ?? true;
}
