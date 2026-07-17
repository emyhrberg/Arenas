using System;
using System.Diagnostics;
using Terraria.DataStructures;
using Terraria.GameContent.Events;
using Terraria.ID;

namespace Arenas.Common.Generation;

/// <summary>Turns any loaded world into the empty canvas Arenas expects before rounds can start.</summary>
internal sealed class ArenaWorldClearJob
{
    private const int MutationBudget = 8000;
    private const double MillisecondBudget = 8d;
    private int cursor;
    private bool initialized;

    public bool IsComplete { get; private set; }
    public Exception Error { get; private set; }
    public float Progress => cursor / (float)Math.Max(1, Main.maxTilesX * Main.maxTilesY);

    public void Tick()
    {
        if (IsComplete || Error != null) return;
        try
        {
            if (!initialized)
            {
                ClearEntities();
                initialized = true;
            }

            Stopwatch watch = Stopwatch.StartNew();
            int mutations = 0;
            int total = Main.maxTilesX * Main.maxTilesY;
            while (cursor < total && mutations < MutationBudget && watch.Elapsed.TotalMilliseconds < MillisecondBudget)
            {
                int x = cursor % Main.maxTilesX, y = cursor / Main.maxTilesX;
                cursor++;
                Main.tile[x, y].ClearEverything();
                mutations++;
            }

            if (cursor < total) return;
            FinalizeEmptyWorld();
            IsComplete = true;
        }
        catch (Exception exception)
        {
            Error = exception;
        }
    }

    private static void ClearEntities()
    {
        for (int i = 0; i < Main.maxNPCs; i++)
        {
            if (!Main.npc[i].active) continue;
            Main.npc[i].active = false;
            if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, number: i);
        }
        for (int i = 0; i < Main.maxProjectiles; i++)
        {
            Projectile projectile = Main.projectile[i];
            if (!projectile.active) continue;
            int identity = projectile.identity, owner = projectile.owner;
            projectile.active = false;
            if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.KillProjectile, number: identity, number2: owner);
        }
        for (int i = 0; i < Main.maxItems; i++)
        {
            if (!Main.item[i].active) continue;
            Main.item[i].TurnToAir();
            Main.item[i].active = false;
            if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncItem, number: i);
        }
        for (int i = 0; i < Main.maxChests; i++) Main.chest[i] = null;
        for (int i = 0; i < Main.sign.Length; i++) Main.sign[i] = null;
        TileEntity.ByID.Clear();
        TileEntity.ByPosition.Clear();
    }

    private static void FinalizeEmptyWorld()
    {
        PrepareWorldSpawn();
        Main.worldSurface = Math.Min(520d, Main.maxTilesY - 80d);
        Main.rockLayer = Math.Min(550d, Main.maxTilesY - 40d);
        Main.raining = false;
        Main.maxRaining = 0f;
        Main.windSpeedCurrent = Main.windSpeedTarget = 0f;
        Main.slimeRain = Main.bloodMoon = Main.eclipse = false;
        Main.pumpkinMoon = Main.snowMoon = false;
        Main.fastForwardTimeToDawn = Main.fastForwardTimeToDusk = false;
        Sandstorm.Happening = false;
        if (BirthdayParty.PartyIsUp) BirthdayParty.ToggleManualParty();
        Main.invasionType = Main.invasionSize = Main.invasionSizeStart = 0;
        Liquid.ReInit();
    }

    internal static void PrepareWorldSpawn()
    {
        Point spawn = ArenaGeneratorRegistry.WorldSpawn;
        Main.spawnTileX = spawn.X;
        Main.spawnTileY = spawn.Y;
        const int halfWidth = 30;
        for (int x = spawn.X - halfWidth; x < spawn.X + halfWidth; x++)
        {
            if (!WorldGen.InWorld(x, spawn.Y + 1, 2)) continue;
            for (int y = spawn.Y + 1; y <= spawn.Y + 4; y++)
            {
                Tile tile = Main.tile[x, y];
                tile.ClearEverything();
                tile.HasTile = true;
                tile.TileType = y == spawn.Y + 1 ? TileID.Grass : TileID.Dirt;
            }
        }
        for (int x = spawn.X - halfWidth; x < spawn.X + halfWidth; x++)
            for (int y = spawn.Y + 1; y <= spawn.Y + 4; y++)
                if (WorldGen.InWorld(x, y, 2)) WorldGen.SquareTileFrame(x, y, true);
    }
}
