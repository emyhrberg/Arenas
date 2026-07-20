//using System;
//using Arenas.Common.Generation;
//using Arenas.Common.LoadoutSelector;
//using PvPFramework.Common.Scoreboard;
//using Terraria.DataStructures;
//using Terraria.ID;
//using Terraria.ModLoader.IO;

//namespace Arenas.Common.Rounds;

//internal sealed class ArenaRoundPlayer : ModPlayer
//{
//    private const string ErkySscTag = "ErkySSC";
//    private const string StatsTag = "Arenas";

//    internal ArenaClass SelectedClass { get; private set; }
//    internal string CharacterKey { get; private set; } = "";
//    internal long BossDamage { get; private set; }

//    public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource)
//    {
//        if (Main.netMode == NetmodeID.MultiplayerClient || !ArenaRoundSystem.IsParticipant(Player.whoAmI)) return;
//        if (ArenaRoundSystem.RequiresClassSelection)
//        {
//            SelectedClass = ArenaClass.None;
//            if (Main.netMode == NetmodeID.Server)
//                RoundNetHandler.SendClassState(Player.whoAmI, ArenaClass.None, ArenaRoundSystem.CurrentPresetIndex);
//        }
//    }

//    public override void PreUpdate()
//    {
//        bool participant = Main.netMode == NetmodeID.MultiplayerClient
//            ? ArenaRoundSystem.IsLocalParticipant
//            : ArenaRoundSystem.IsParticipant(Player.whoAmI);
//        if (ArenaWorldSystem.Active && Player.dead && SelectedClass == ArenaClass.None
//            && ArenaRoundSystem.RequiresClassSelection && participant)
//            Player.respawnTimer = 2;
//    }

//    public override void SetControls()
//    {
//        bool controlsLocked = !ArenaWorldSystem.WorldReady
//            || ArenaRoundSystem.Phase is RoundPhase.Generating or RoundPhase.FreezeCountdown;
//        if (!ArenaWorldSystem.Active || !controlsLocked) return;
//        Player.controlLeft = Player.controlRight = Player.controlUp = Player.controlDown = false;
//        Player.controlJump = Player.controlMount = Player.controlHook = false;
//        Player.controlUseItem = Player.controlUseTile = Player.controlThrow = false;
//    }

//    public override void PostUpdate()
//    {
//        if (!ArenaWorldSystem.Active) return;
//        if (ArenaRoundSystem.Phase is RoundPhase.FreezeCountdown or RoundPhase.Playing
//            && ArenaRoundSystem.TryGetParticipantTeam(Player.whoAmI, out _)
//            && !Player.hostile)
//        {
//            Player.hostile = true;
//            if (Main.netMode == NetmodeID.Server)
//                NetMessage.SendData(MessageID.TogglePVP, -1, -1, null, Player.whoAmI);
//        }
//        if (Player.whoAmI != Main.myPlayer) return;
//        Point spawn = ArenaWorldSystem.Layout?.RedSpawn ?? new Point(Math.Max(1, Main.maxTilesX / 2), Math.Max(1, Main.maxTilesY / 2));
//        if (ArenaRoundSystem.Phase is RoundPhase.Ready or RoundPhase.FreezeCountdown or RoundPhase.Playing
//            && ArenaRoundSystem.TryGetParticipantTeam(Player.whoAmI, out Terraria.Enums.Team team))
//            spawn = ArenaRoundSystem.TeamSpawn(team);
//        Main.spawnTileX = spawn.X;
//        Main.spawnTileY = spawn.Y;
//    }

//    public override void OnRespawn()
//    {
//        if (!ArenaWorldSystem.Active) return;
//        Point spawn = ArenaRoundSystem.Phase is RoundPhase.Ready or RoundPhase.FreezeCountdown or RoundPhase.Playing
//            && ArenaRoundSystem.TryGetParticipantTeam(Player.whoAmI, out Terraria.Enums.Team team)
//            ? ArenaRoundSystem.TeamSpawn(team)
//            : ArenaWorldSystem.Layout?.RedSpawn ?? new Point(Math.Max(1, Main.maxTilesX / 2), Math.Max(1, Main.maxTilesY / 2));
//        Vector2 position = new(spawn.X * 16, spawn.Y * 16 - Player.height);
//        Player.Teleport(position, TeleportationStyleID.RodOfDiscord);
//        Player.velocity = Vector2.Zero;
//        if (Main.netMode == NetmodeID.Server)
//            NetMessage.SendData(MessageID.TeleportEntity, number: 0, number2: Player.whoAmI, number3: position.X, number4: position.Y, number5: TeleportationStyleID.RodOfDiscord);
//    }

//    internal void ResetStats()
//    {
//        ScoreboardService.ResetPlayer(Player);
//        BossDamage = 0;
//    }

//    internal void SetBossDamage(long damage) => BossDamage = Math.Max(0L, damage);
//    internal void AddBossDamage(uint damage) =>
//        BossDamage = BossDamage > long.MaxValue - damage ? long.MaxValue : BossDamage + damage;
//    internal void SetSelectedClass(ArenaClass arenaClass) => SelectedClass = arenaClass;

//    internal string CharacterKeyOrFallback() => string.IsNullOrEmpty(CharacterKey)
//        ? $"slot:{Player.whoAmI}:{Player.name}"
//        : CharacterKey;

//    internal static bool ExportSscStats(Player player, string characterKey, TagCompound root)
//    {
//        if (Main.netMode == NetmodeID.MultiplayerClient || player == null || root == null)
//            return false;

//        ArenaRoundPlayer arenaPlayer = player.GetModPlayer<ArenaRoundPlayer>();
//        arenaPlayer.CharacterKey = characterKey ?? "";
//        ScoreboardEntry stats = ScoreboardService.GetPlayerStats(player);

//        TagCompound ssc = root.ContainsKey(ErkySscTag) ? root.GetCompound(ErkySscTag) : [];
//        ssc[StatsTag] = new TagCompound
//        {
//            ["version"] = 1,
//            ["characterKey"] = arenaPlayer.CharacterKey,
//            ["roundToken"] = ArenaRoundSystem.CurrentRoundToken,
//            ["kills"] = stats.Kills,
//            ["deaths"] = stats.Deaths,
//            ["damage"] = stats.Damage,
//            ["bossDamage"] = arenaPlayer.BossDamage
//        };

//        root[ErkySscTag] = ssc;
//        return true;
//    }

//    internal static bool ImportSscStats(Player player, string characterKey, TagCompound root)
//    {
//        if (player == null)
//            return false;

//        ArenaRoundPlayer stats = player.GetModPlayer<ArenaRoundPlayer>();
//        stats.CharacterKey = characterKey ?? "";

//        bool reassociated = Main.netMode != NetmodeID.MultiplayerClient
//            && ArenaRoundSystem.ReassociateParticipant(player, stats.CharacterKey);

//        if (Main.netMode == NetmodeID.MultiplayerClient || root == null || !root.ContainsKey(ErkySscTag))
//            return true;

//        TagCompound ssc = root.GetCompound(ErkySscTag);
//        if (!ssc.ContainsKey(StatsTag))
//            return true;

//        TagCompound saved = ssc.GetCompound(StatsTag);
//        string savedCharacter = saved.ContainsKey("characterKey") ? saved.GetString("characterKey") : "";
//        string savedRound = saved.ContainsKey("roundToken") ? saved.GetString("roundToken") : "";

//        if ((!string.IsNullOrEmpty(savedCharacter) && savedCharacter != stats.CharacterKey) ||
//            string.IsNullOrEmpty(savedRound) || savedRound != ArenaRoundSystem.CurrentRoundToken ||
//            !reassociated)
//            return true;

//        ScoreboardService.SetPlayerStats(player, saved.GetInt("kills"), saved.GetInt("deaths"), saved.Get<long>("damage"));
//        stats.SetBossDamage(saved.Get<long>("bossDamage"));
//        RoundNetHandler.SendStateToAll();
//        return true;
//    }

//    public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
//    {
//        if (Main.netMode == NetmodeID.Server && Player.whoAmI == toWho)
//            RoundNetHandler.SendState(toWho);
//    }
//}
