using Arenas.Common.Rounds;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.UI;

namespace Arenas.Common.Generation;

internal sealed class ArenaBoundaryPlayer : ModPlayer
{
    public override void PostUpdate()
    {
        if (!ArenaWorldSystem.Active || ArenaWorldSystem.Layout == null || ArenaRoundSystem.Phase is not (RoundPhase.FreezeCountdown or RoundPhase.Playing))
            return;

        ArenaLayout layout = ArenaWorldSystem.Layout;
        if (!ArenaRoundSystem.TryGetParticipantTeam(Player.whoAmI, out Team team))
        {
            Player.immune = true;
            Player.immuneTime = 2;
            Player.noFallDmg = true;
            if (ClampTo(layout.StagingLobby)) SyncCorrection();
            return;
        }
        Rectangle arena = ToWorld(layout.ArenaArea);
        float minX = arena.Left;
        float maxX = arena.Right - Player.width;
        float minY = arena.Top;
        float maxY = arena.Bottom - Player.height;

        if (team == Team.Red)
            maxX = Math.Min(maxX, layout.BossArea.Right * 16f - Player.width);
        else if (team == Team.Blue)
            minX = Math.Max(minX, layout.BossArea.Left * 16f);
        else
            return;

        Vector2 clamped = new(MathHelper.Clamp(Player.position.X, minX, maxX), MathHelper.Clamp(Player.position.Y, minY, maxY));
        bool corrected = clamped != Player.position;
        if (clamped.X != Player.position.X) Player.velocity.X = 0f;
        if (clamped.Y != Player.position.Y) Player.velocity.Y = 0f;
        Player.position = clamped;
        if (corrected) SyncCorrection();
    }

    private bool ClampTo(Rectangle tiles)
    {
        Rectangle area = ToWorld(tiles);
        Vector2 clamped = new(MathHelper.Clamp(Player.position.X, area.Left, area.Right - Player.width),
            MathHelper.Clamp(Player.position.Y, area.Top, area.Bottom - Player.height));
        bool corrected = clamped != Player.position;
        if (corrected) Player.velocity = Vector2.Zero;
        Player.position = clamped;
        return corrected;
    }

    private void SyncCorrection()
    {
        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendData(MessageID.PlayerControls, -1, -1, null, Player.whoAmI);
    }

    private static Rectangle ToWorld(Rectangle tiles) => new(tiles.X * 16, tiles.Y * 16, tiles.Width * 16, tiles.Height * 16);
}

[Autoload(Side = ModSide.Client)]
internal sealed class ArenaBoundaryDrawSystem : ModSystem
{
    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int index = layers.FindIndex(layer => layer.Name == "Vanilla: Interface Logic 1");
        if (index >= 0) layers.Insert(index + 1, new BoundaryLayer());
    }

    private sealed class BoundaryLayer() : GameInterfaceLayer("Arenas: Team Boundary", InterfaceScaleType.Game)
    {
        protected override bool DrawSelf()
        {
            ArenaLayout layout = ArenaWorldSystem.Layout;
            if (layout == null || ArenaRoundSystem.Phase is not (RoundPhase.FreezeCountdown or RoundPhase.Playing)
                || !ArenaRoundSystem.TryGetParticipantTeam(Main.myPlayer, out Team team))
                return true;

            int tileX;
            Color color;
            if (team == Team.Red) { tileX = layout.BossArea.Right; color = Main.teamColor[(int)Team.Blue]; }
            else if (team == Team.Blue) { tileX = layout.BossArea.Left; color = Main.teamColor[(int)Team.Red]; }
            else return true;

            int screenX = (int)MathF.Round(tileX * 16f - Main.screenPosition.X);
            int screenY = (int)MathF.Round(layout.ArenaArea.Top * 16f - Main.screenPosition.Y);
            int height = layout.ArenaArea.Height * 16;
            Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(screenX, screenY, 1, height), color * .9f);
            return true;
        }
    }
}
