using Arenas.Common.Generation;
using SubworldLibrary;
using System.IO;
using Terraria.ID;

namespace Arenas.Common;

/// <summary>Exposes arena state only while the disposable Arenas subworld is active.</summary>
internal sealed class ArenaWorldSystem : ModSystem
{
    public static ArenaLayout Layout { get; private set; }
    // Subworld Library is the authoritative world marker. During a subserver boot,
    // Main.gameMenu can remain true through OnLoad, so gating on it can permanently
    // strand the round in Generating/Idle even though the arena world is ready.
    public static bool Active => SubworldSystem.IsActive<ArenasSubworld>();
    public static bool WorldReady { get; private set; }
    public static bool IsClearing => false;
    public static float ClearingProgress => 0f;

    public override void ClearWorld()
    {
        Layout = null;
        WorldReady = false;
    }

    public override void OnWorldLoad()
    {
        if (!Active)
        {
            Layout = null;
            WorldReady = false;
            return;
        }

        Layout = ArenasSubworld.GeneratedLayout;
        WorldReady = Layout != null;
    }

    public override void OnWorldUnload()
    {
        Layout = null;
        WorldReady = false;
    }

    public override void NetSend(BinaryWriter writer)
    {
        writer.Write(WorldReady);
        writer.Write(Layout != null);
        Layout?.Write(writer);
    }

    public override void NetReceive(BinaryReader reader)
    {
        WorldReady = reader.ReadBoolean();
        Layout = reader.ReadBoolean() ? ArenaLayout.Read(reader) : null;
    }

    internal static void InitializeSubworld(ArenaLayout layout)
    {
        Layout = layout;
        WorldReady = layout != null;
        Log.Debug($"[WorldGen4] Published generated layout to ArenaWorldSystem. ready={WorldReady}, generator={layout?.Generator.ToString() ?? "none"}.");
    }

    internal static void ApplyNetworkLayout(ArenaLayout layout)
    {
        if (!Active)
            return;
        Layout = layout;
        WorldReady = layout != null;
    }

    internal static void ApplyNetworkClearing(bool clearing, float progress)
    {
        // World clearing is intentionally unsupported. Arena tiles exist only in ArenasSubworld.
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
