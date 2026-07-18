using System;
using Arenas.Common.Generation;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader.IO;

namespace Arenas.Common.Rounds;

internal sealed class ArenaRoundPlayer : ModPlayer
{
    private const string ErkySscTag = "ErkySSC";
    private const string StatsTag = "Arenas";

    public int Kills { get; private set; }
    public int Deaths { get; private set; }
    public long Damage { get; private set; }
    public long BossDamage { get; private set; }
    internal string CharacterKey { get; private set; } = "";

    public override void PostHurt(Player.HurtInfo info)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || !info.PvP || ArenaRoundSystem.Phase != RoundPhase.Playing) return;
        int attacker = info.DamageSource.SourcePlayerIndex;
        if (attacker != Player.whoAmI) RecordDamage(attacker, info.Damage);
    }

    public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || !ArenaRoundSystem.IsParticipant(Player.whoAmI)) return;
        Deaths++;
        int killer = damageSource.SourcePlayerIndex;
        if (pvp && killer != Player.whoAmI && ArenaRoundSystem.IsParticipant(killer)) Main.player[killer].GetModPlayer<ArenaRoundPlayer>().Kills++;
    }

    public override void SetControls()
    {
        bool controlsLocked = !ArenaWorldSystem.WorldReady
            || ArenaRoundSystem.Phase is RoundPhase.Generating or RoundPhase.FreezeCountdown;
        if (!ArenaWorldSystem.Active || !controlsLocked) return;
        Player.controlLeft = Player.controlRight = Player.controlUp = Player.controlDown = false;
        Player.controlJump = Player.controlMount = Player.controlHook = false;
        Player.controlUseItem = Player.controlUseTile = Player.controlThrow = false;
    }

    public override void PostUpdate()
    {
        if (!ArenaWorldSystem.Active) return;
        if (ArenaRoundSystem.Phase is RoundPhase.FreezeCountdown or RoundPhase.Playing
            && ArenaRoundSystem.TryGetParticipantTeam(Player.whoAmI, out _)
            && !Player.hostile)
        {
            Player.hostile = true;
            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.TogglePVP, -1, -1, null, Player.whoAmI);
        }
        if (Player.whoAmI != Main.myPlayer) return;
        Point spawn = ArenaWorldSystem.Layout?.RedSpawn ?? ArenaGeneratorRegistry.WorldSpawn;
        if (ArenaRoundSystem.Phase is RoundPhase.FreezeCountdown or RoundPhase.Playing
            && ArenaRoundSystem.TryGetParticipantTeam(Player.whoAmI, out Terraria.Enums.Team team))
            spawn = ArenaRoundSystem.TeamSpawn(team);
        Main.spawnTileX = spawn.X;
        Main.spawnTileY = spawn.Y;
    }

    public override void OnRespawn()
    {
        if (!ArenaWorldSystem.Active) return;
        Point spawn = ArenaRoundSystem.Phase is RoundPhase.FreezeCountdown or RoundPhase.Playing
            && ArenaRoundSystem.TryGetParticipantTeam(Player.whoAmI, out Terraria.Enums.Team team)
            ? ArenaRoundSystem.TeamSpawn(team)
            : ArenaWorldSystem.Layout?.RedSpawn ?? ArenaGeneratorRegistry.WorldSpawn;
        Vector2 position = new(spawn.X * 16, spawn.Y * 16 - Player.height);
        Player.Teleport(position, TeleportationStyleID.RodOfDiscord);
        Player.velocity = Vector2.Zero;
        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendData(MessageID.TeleportEntity, number: 0, number2: Player.whoAmI, number3: position.X, number4: position.Y, number5: TeleportationStyleID.RodOfDiscord);
    }

    public override void ProcessTriggers(TriggersSet triggersSet)
    {
        if (Player.whoAmI != Main.myPlayer) return;
        ModKeybind key = ModContent.GetInstance<Core.Keybinds>().Scoreboard;
        if (key?.JustPressed == true) ArenaRoundUI.SetScoreboardVisible(true);
        else if (key?.JustReleased == true) ArenaRoundUI.SetScoreboardVisible(false);
    }

    internal void ResetStats() { Kills = Deaths = 0; Damage = BossDamage = 0; }

    internal string CharacterKeyOrFallback() => string.IsNullOrEmpty(CharacterKey)
        ? $"slot:{Player.whoAmI}:{Player.name}"
        : CharacterKey;

    internal static bool ExportSscStats(Player player, string characterKey, TagCompound root)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || player == null || root == null)
            return false;

        ArenaRoundPlayer stats = player.GetModPlayer<ArenaRoundPlayer>();
        stats.CharacterKey = characterKey ?? "";

        TagCompound ssc = root.ContainsKey(ErkySscTag) ? root.GetCompound(ErkySscTag) : [];
        ssc[StatsTag] = new TagCompound
        {
            ["version"] = 1,
            ["characterKey"] = stats.CharacterKey,
            ["roundToken"] = ArenaRoundSystem.CurrentRoundToken,
            ["kills"] = stats.Kills,
            ["deaths"] = stats.Deaths,
            ["damage"] = stats.Damage,
            ["bossDamage"] = stats.BossDamage
        };

        root[ErkySscTag] = ssc;
        return true;
    }

    internal static bool ImportSscStats(Player player, string characterKey, TagCompound root)
    {
        if (player == null)
            return false;

        ArenaRoundPlayer stats = player.GetModPlayer<ArenaRoundPlayer>();
        stats.CharacterKey = characterKey ?? "";

        bool reassociated = Main.netMode != NetmodeID.MultiplayerClient
            && ArenaRoundSystem.ReassociateParticipant(player, stats.CharacterKey);

        if (Main.netMode == NetmodeID.MultiplayerClient || root == null || !root.ContainsKey(ErkySscTag))
            return true;

        TagCompound ssc = root.GetCompound(ErkySscTag);
        if (!ssc.ContainsKey(StatsTag))
            return true;

        TagCompound saved = ssc.GetCompound(StatsTag);
        string savedCharacter = saved.ContainsKey("characterKey") ? saved.GetString("characterKey") : "";
        string savedRound = saved.ContainsKey("roundToken") ? saved.GetString("roundToken") : "";

        if ((!string.IsNullOrEmpty(savedCharacter) && savedCharacter != stats.CharacterKey) ||
            string.IsNullOrEmpty(savedRound) || savedRound != ArenaRoundSystem.CurrentRoundToken ||
            !reassociated)
            return true;

        stats.Kills = Math.Max(0, saved.GetInt("kills"));
        stats.Deaths = Math.Max(0, saved.GetInt("deaths"));
        stats.Damage = Math.Max(0L, saved.Get<long>("damage"));
        stats.BossDamage = Math.Max(0L, saved.Get<long>("bossDamage"));
        ArenaRoundNetHandler.SendStateToAll();
        return true;
    }

    internal static void RecordDamage(int playerId, int damage)
    {
        if (damage <= 0 || ArenaRoundSystem.Phase != RoundPhase.Playing || !ArenaRoundSystem.IsParticipant(playerId)) return;
        Main.player[playerId].GetModPlayer<ArenaRoundPlayer>().Damage += damage;
    }

    internal static void RecordBossDamage(int playerId, int damage)
    {
        RecordDamage(playerId, damage);
        if (damage > 0 && ArenaRoundSystem.Phase == RoundPhase.Playing && ArenaRoundSystem.IsParticipant(playerId))
            Main.player[playerId].GetModPlayer<ArenaRoundPlayer>().BossDamage += damage;
    }

    public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
    {
        if (Main.netMode == NetmodeID.Server && Player.whoAmI == toWho)
            ArenaRoundNetHandler.SendState(toWho);
    }
}
