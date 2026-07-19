using Arenas.Common.Generation;
using SubworldLibrary;
using System.IO;
using Terraria.ID;

namespace Arenas.Common;

/// <summary>Exposes authoritative state for the reusable Arenas subworld.</summary>
internal sealed class ArenaWorldSystem : ModSystem
{
    public static ArenaLayout Layout { get; private set; }
    // Subworld Library is the authoritative world marker. During a subserver boot,
    // Main.gameMenu can remain true through OnLoad, so gating on it can permanently
    // strand the round in Generating/Idle even though the arena world is ready.
    public static bool Active => SubworldSystem.IsActive<ArenasSubworld>();
    public static bool WorldReady { get; private set; }
    public static bool MatchReady { get; private set; }
    public override void ClearWorld()
    {
        Layout = null;
        WorldReady = false;
        MatchReady = false;
    }

    public override void OnWorldLoad()
    {
        if (!Active)
        {
            Layout = null;
            WorldReady = false;
            MatchReady = false;
            return;
        }

        Layout = ArenasSubworld.GeneratedLayout;
        WorldReady = Layout != null;
        MatchReady = WorldReady && ArenaSubworldCoordinator.ActiveRequest.GenerationMode == ArenaGenerationMode.Full;
    }

    public override void OnWorldUnload()
    {
        Layout = null;
        WorldReady = false;
        MatchReady = false;
    }

    public override void NetSend(BinaryWriter writer)
    {
        writer.Write(WorldReady);
        writer.Write(MatchReady);
        writer.Write(Layout != null);
        Layout?.Write(writer);
    }

    public override void NetReceive(BinaryReader reader)
    {
        WorldReady = reader.ReadBoolean();
        MatchReady = reader.ReadBoolean();
        Layout = reader.ReadBoolean() ? ArenaLayout.Read(reader) : null;
    }

    internal static void InitializeSubworld(ArenaLayout layout, bool matchReady)
    {
        Layout = layout;
        WorldReady = layout != null;
        MatchReady = WorldReady && matchReady;
        Log.Debug($"[WorldGen4] Published generated layout to ArenaWorldSystem. ready={WorldReady}, matchReady={MatchReady}, generator={layout?.Generator.ToString() ?? "none"}.");
    }

    internal static void ApplyNetworkLayout(ArenaLayout layout)
    {
        if (!Active)
            return;
        Layout = layout;
        WorldReady = layout != null;
        MatchReady = WorldReady;
    }

}

internal sealed class ArenaSpawnSuppression : GlobalNPC
{
    public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns)
    {
        if (!ArenaWorldSystem.Active) return;
        spawnRate = int.MaxValue;
        maxSpawns = 0;
    }
}
