using Arenas.Common.DataStructures;
using PvPFramework.Common.Combat.TeamBoss;
using PvPFramework.Core.Configs.ConfigElements;
using System;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader.Config;
using FrameworkServerConfig = PvPFramework.Core.Configs.ServerConfig;

namespace Arenas.Common.Game;

internal enum BossState : byte
{
    Alive,
    Missing
}

/// <summary>Owns the NPC spawned for the current Arenas round.</summary>
internal sealed class BossManager : ModSystem
{
    private const int MissingGraceTicks = 30;

    private int bossIndex = -1;
    private int bossType;
    private int missingTicks;
    private Rectangle roundArea;

    internal int BossIndex => bossIndex;

    public override void Load() => TeamBossNPC.BossDefeatedByTeam += OnBossDefeatedByTeam;

    public override void OnWorldLoad() => EnsureTeamBossConfigured(NPCID.KingSlime);

    public override void Unload() => TeamBossNPC.BossDefeatedByTeam -= OnBossDefeatedByTeam;

    internal bool TrySpawn(BossFightPreset preset, ArenaLayout layout)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || preset?.Boss?.Type <= 0 || layout == null)
            return false;

        Cleanup();
        EnsureTeamBossConfigured(preset.Boss.Type);
        roundArea = new Rectangle(layout.ArenaBounds.X * 16, layout.ArenaBounds.Y * 16,
            layout.ArenaBounds.Width * 16, layout.ArenaBounds.Height * 16);
        ClearRoundProjectiles();

        Point spawn = layout.BossSpawn;
        int index = NPC.NewNPC(new EntitySource_Misc("ArenasRound"), spawn.X * 16 + 8,
            spawn.Y * 16 + 8, preset.Boss.Type);
        if ((uint)index >= Main.maxNPCs || Main.npc[index]?.active != true)
            return false;

        bossIndex = index;
        bossType = preset.Boss.Type;
        missingTicks = 0;
        NPC boss = Main.npc[index];
        boss.target = FindTarget();
        boss.timeLeft = Math.Max(boss.timeLeft, 3600);
        boss.netAlways = true;
        boss.netUpdate = true;
        Log.Info($"Spawned round boss type={bossType}, index={bossIndex}, tile={spawn}.");
        return true;
    }

    internal BossState Update()
    {
        if (TryGetBoss(out NPC boss))
        {
            missingTicks = 0;
            int target = FindTarget();
            if (target >= 0)
                boss.target = target;
            boss.timeLeft = Math.Max(boss.timeLeft, 3600);
            return BossState.Alive;
        }

        missingTicks++;
        return missingTicks >= MissingGraceTicks ? BossState.Missing : BossState.Alive;
    }

    internal void Cleanup()
    {
        int ownedIndex = bossIndex;
        if (ownedIndex >= 0)
        {
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                bool ownedBoss = i == ownedIndex || npc?.realLife == ownedIndex;
                bool arenaHostile = npc?.active == true && !npc.friendly && !npc.townNPC
                    && !npc.isLikeATownNPC && npc.type != NPCID.TargetDummy
                    && roundArea.Intersects(npc.Hitbox);
                if (npc?.active != true || !ownedBoss && !arenaHostile)
                    continue;

                npc.active = false;
                npc.netUpdate = true;
                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.SyncNPC, number: i);
            }
        }

        ClearRoundProjectiles();

        bossIndex = -1;
        bossType = 0;
        missingTicks = 0;
        roundArea = Rectangle.Empty;
    }

    private void ClearRoundProjectiles()
    {
        if (roundArea.Width <= 0 || roundArea.Height <= 0)
            return;

        for (int i = 0; i < Main.maxProjectiles; i++)
        {
            Projectile projectile = Main.projectile[i];
            if (projectile?.active != true || !projectile.friendly
                || projectile.owner < 0 || projectile.owner >= Main.maxPlayers
                || !roundArea.Intersects(projectile.Hitbox))
                continue;

            projectile.Kill();
            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.KillProjectile, -1, -1, null, i, projectile.owner);
        }
    }

    private void OnBossDefeatedByTeam(Player player, NPC npc, Team team)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || team == Team.None || !IsOwnedBoss(npc))
            return;

        ModContent.GetInstance<RoundManager>().NotifyBossDefeated(player, team);
    }

    private bool TryGetBoss(out NPC boss)
    {
        if ((uint)bossIndex < Main.maxNPCs && Main.npc[bossIndex]?.active == true
            && Main.npc[bossIndex].type == bossType)
        {
            boss = Main.npc[bossIndex];
            return true;
        }

        boss = null;
        return false;
    }

    private bool IsOwnedBoss(NPC npc)
    {
        if (npc == null || bossIndex < 0)
            return false;
        return npc.whoAmI == bossIndex || npc.realLife == bossIndex;
    }

    private static int FindTarget()
    {
        foreach (Player player in Main.ActivePlayers)
            if (!player.dead && (Team)player.team is Team.Red or Team.Blue)
                return player.whoAmI;
        return -1;
    }

    private static void EnsureTeamBossConfigured(int npcType)
    {
        FrameworkServerConfig config = ModContent.GetInstance<FrameworkServerConfig>();
        config.BossBalance ??= [];
        NPCDefinition definition = new(npcType);
        if (config.BossBalance.TryGetValue(definition, out FrameworkServerConfig.BossBalanceEntry entry)
            && entry != null)
        {
            // M2 is an independent team race: only the team landing a strike may
            // reduce its own virtual life pool. Preserve configured HP/damage tuning.
            entry.TeamLifeShare = 0f;
            return;
        }

        config.BossBalance[definition] = new FrameworkServerConfig.BossBalanceEntry
        {
            LifeMaxMultiplier = 1f,
            DamageMultiplier = 1f,
            TeamLifeShare = 0f
        };
        Log.Info($"Registered NPC type {npcType} for PvP Framework team-life virtualization.");
    }
}
