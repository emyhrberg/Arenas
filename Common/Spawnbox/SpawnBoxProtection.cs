using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;

namespace Arenas.Common.Spawnbox;

internal static class SpawnBoxCollision
{
    public static Vector2 Resolve(Player player, SpawnBoxSystem box)
    {
        Vector2 velocity = player.velocity;
        foreach (Team team in SpawnBoxSystem.Teams)
            if (!box.CanCross(team, (Team)player.team, player.Hitbox))
                velocity = CollideWithBorder(player.position, velocity, player.width, player.height, SpawnBoxSystem.TileToWorld(box.GetTileArea(team)), box.GetThickness(team) * SpawnBoxSystem.TileSize);
        return velocity;
    }

    private static Vector2 CollideWithBorder(Vector2 position, Vector2 velocity, int width, int height, Rectangle inner, int thickness)
    {
        Rectangle outer = inner; outer.Inflate(thickness, thickness);
        Span<Rectangle> borders = stackalloc Rectangle[4]
        {
            new(outer.X, outer.Y, outer.Width, thickness), new(outer.X, inner.Bottom, outer.Width, thickness),
            new(outer.X, inner.Y, thickness, inner.Height), new(inner.Right, inner.Y, thickness, inner.Height)
        };
        Rectangle source = new((int)position.X, (int)position.Y, width, height);
        float x = velocity.X;

        if (x != 0f)
        {
            foreach (Rectangle border in borders)
            {
                if (source.Bottom <= border.Top || source.Top >= border.Bottom)
                    continue;

                if (x > 0f && source.Right <= border.Left && source.Right + x > border.Left)
                    x = Math.Min(x, border.Left - source.Right);
                else if (x < 0f && source.Left >= border.Right && source.Left + x < border.Right)
                    x = Math.Max(x, border.Right - source.Left);
            }
        }

        Rectangle afterX = new((int)(position.X + x), source.Y, width, height);
        float y = velocity.Y;

        if (y != 0f)
        {
            foreach (Rectangle border in borders)
            {
                if (afterX.Right <= border.Left || afterX.Left >= border.Right)
                    continue;

                if (y > 0f && afterX.Bottom <= border.Top && afterX.Bottom + y > border.Top)
                    y = Math.Min(y, border.Top - afterX.Bottom);
                else if (y < 0f && afterX.Top >= border.Bottom && afterX.Top + y < border.Bottom)
                    y = Math.Max(y, border.Bottom - afterX.Top);
            }
        }

        return new Vector2(x, y);
    }
}

internal sealed class SpawnBoxPlayer : ModPlayer
{
    public override void Load()
    {
        On_Player.PlaceThing_Tiles += PlaceTiles;
        On_Player.PlaceThing_Walls += PlaceWalls;
        On_Player.ItemCheck_UseMiningTools += UseMiningTools;
        On_Player.ItemCheck_UseTeleportRod += UseTeleportRod;
        On_Player.ItemCheck_UseWiringTools += UseWiringTools;
        On_Player.ItemCheck_CutTiles += CutTiles;
    }

    public override void Unload()
    {
        On_Player.PlaceThing_Tiles -= PlaceTiles;
        On_Player.PlaceThing_Walls -= PlaceWalls;
        On_Player.ItemCheck_UseMiningTools -= UseMiningTools;
        On_Player.ItemCheck_UseTeleportRod -= UseTeleportRod;
        On_Player.ItemCheck_UseWiringTools -= UseWiringTools;
        On_Player.ItemCheck_CutTiles -= CutTiles;
    }

    public override void PostUpdateMiscEffects()
    {
        if (ModContent.GetInstance<SpawnBoxSystem>().TouchesWorldHitbox(Player.Hitbox))
        {
            Player.AddBuff(BuffID.NoBuilding, 2);
        }
    }

    public override void PreUpdateMovement()
    {
        SpawnBoxSystem box = ModContent.GetInstance<SpawnBoxSystem>();
        if (box.Active) Player.velocity = SpawnBoxCollision.Resolve(Player, box);
    }

    public override void OnRespawn()
    {
        SpawnBoxSystem box = ModContent.GetInstance<SpawnBoxSystem>(); Team team = (Team)Player.team;
        if (!box.Active || team is not (Team.Red or Team.Blue or Team.Green)) return;
        Rectangle area = SpawnBoxSystem.TileToWorld(box.GetTileArea(team));
        Vector2 position = area.Center.ToVector2() - Player.Size * .5f;
        Player.Teleport(position, TeleportationStyleID.RodOfDiscord); Player.velocity = Vector2.Zero;
        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendData(MessageID.TeleportEntity, number: 0, number2: Player.whoAmI, number3: position.X, number4: position.Y, number5: TeleportationStyleID.RodOfDiscord);
    }

    public override bool CanHitPvp(Item item, Player target) => CanFight(Player, target);
    public override bool CanHitPvpWithProj(Projectile proj, Player target) => CanFight(Player, target);

    private static bool CanFight(Player a, Player b)
    {
        SpawnBoxSystem box = ModContent.GetInstance<SpawnBoxSystem>();
        return !box.TouchesWorldHitbox(a.Hitbox) && !box.TouchesWorldHitbox(b.Hitbox);
    }

    private static bool CanModifyTarget() => !ModContent.GetInstance<SpawnBoxSystem>().ContainsTile(Player.tileTargetX, Player.tileTargetY);
    private static void PlaceTiles(On_Player.orig_PlaceThing_Tiles orig, Player self) { if (CanModifyTarget()) orig(self); }
    private static void PlaceWalls(On_Player.orig_PlaceThing_Walls orig, Player self) { if (CanModifyTarget()) orig(self); }
    private static void UseMiningTools(On_Player.orig_ItemCheck_UseMiningTools orig, Player self, Item item) { if (CanModifyTarget()) orig(self, item); }
    private static void UseTeleportRod(On_Player.orig_ItemCheck_UseTeleportRod orig, Player self, Item item) { if (CanModifyTarget()) orig(self, item); }
    private static void UseWiringTools(On_Player.orig_ItemCheck_UseWiringTools orig, Player self, Item item) { if (CanModifyTarget()) orig(self, item); }
    private static void CutTiles(On_Player.orig_ItemCheck_CutTiles orig, Player self, Item item, Rectangle itemRectangle, bool[] shouldIgnore)
    {
        if (!ModContent.GetInstance<SpawnBoxSystem>().TouchesTileRectangle(SpawnBoxSystem.WorldToTile(itemRectangle)))
            orig(self, item, itemRectangle, shouldIgnore);
    }
}

public sealed class SpawnBoxTile : GlobalTile
{
    public override bool CanKillTile(int i, int j, int type, ref bool blockDamaged) => CanModify(i, j);
    public override bool CanExplode(int i, int j, int type) => CanModify(i, j);
    public override bool CanPlace(int i, int j, int type) => CanModify(i, j);
    public override bool CanReplace(int i, int j, int type, int tileTypeBeingPlaced) => CanModify(i, j);
    private static bool CanModify(int i, int j) => !ModContent.GetInstance<SpawnBoxSystem>().ContainsTile(i, j);
}

internal sealed class SpawnBoxProjectile : GlobalProjectile
{
    public override bool PreAI(Projectile projectile)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || !ModContent.GetInstance<SpawnBoxSystem>().TouchesWorldHitbox(projectile.Hitbox))
            return true;

        int identity = projectile.identity;
        int owner = projectile.owner;
        projectile.Kill();

        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendData(MessageID.KillProjectile, -1, -1, null, identity, owner);

        return false;
    }

    public override bool? CanCutTiles(Projectile projectile) =>
        ModContent.GetInstance<SpawnBoxSystem>().TouchesWorldHitbox(projectile.Hitbox) ? false : null;
}

internal sealed class SpawnBoxItem : GlobalItem
{
    public override bool CanUseItem(Item item, Player player) =>
        item.shoot <= ProjectileID.None || !ModContent.GetInstance<SpawnBoxSystem>().TouchesWorldHitbox(player.Hitbox);

    public override void Update(Item item, ref float gravity, ref float maxFallSpeed)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || !ModContent.GetInstance<SpawnBoxSystem>().TouchesWorldHitbox(item.Hitbox))
            return;

        item.TurnToAir();
        item.active = false;

        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendData(MessageID.SyncItem, -1, -1, null, item.whoAmI);
    }
}

internal sealed class SpawnBoxLiquidCleaner : ModSystem
{
    public override void PostUpdateWorld()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        SpawnBoxSystem box = ModContent.GetInstance<SpawnBoxSystem>(); if (!box.Active) return;
        foreach (Rectangle area in box.TileAreas)
            for (int x = area.Left; x < area.Right; x++)
                for (int y = area.Top; y < area.Bottom; y++)
                    if (WorldGen.InWorld(x, y) && Main.tile[x, y].LiquidAmount > 0)
                    {
                        Main.tile[x, y].LiquidAmount = 0;
                        if (Main.netMode == NetmodeID.Server) NetMessage.sendWater(x, y);
                    }
    }
}
