using Arenas.Core.Configs.ConfigElements;
using System;
using System.IO;
using Terraria.Enums;

namespace Arenas.Common.Generation;

internal sealed class ArenaLayout
{
    public const int BorderClearanceThickness = 3;
    public ArenaGeneratorKind Generator { get; init; }
    public int Seed { get; init; }
    public int WorldWidth { get; init; }
    public int WorldHeight { get; init; }
    public Rectangle ArenaArea { get; init; }
    public Rectangle BossArea { get; init; }
    public Rectangle RedSpawnClearance { get; internal set; }
    public Rectangle BlueSpawnClearance { get; internal set; }
    public Point RedSpawn { get; internal set; }
    public Point BlueSpawn { get; internal set; }
    public Point BossSpawn { get; internal set; }
    public Rectangle StagingLobby { get; init; }
    public int OuterBorderThickness { get; init; }
    public int TeamBorderWidth { get; init; }
    public int BlueBorderX { get; init; }
    public int RedBorderX { get; init; }
    public bool AutoPlaceTeamSpawns { get; init; }
    public bool AutoPlaceBossSpawn { get; init; }
    public int MirrorRight => WorldWidth - 1;
    public int MirrorRightX => WorldWidth / 2;
    public int CenterX => WorldWidth / 2;

    public Point TeamSpawn(Team team) => team == Team.Blue ? BlueSpawn : RedSpawn;

    public bool IsProtectedTile(int x, int y)
    {
        if (Generator == ArenaGeneratorKind.SandboxWorld)
            return false;

        Point point = new(x, y);
        Rectangle outer = ArenaArea;
        outer.Inflate(OuterBorderThickness + BorderClearanceThickness,
            OuterBorderThickness + BorderClearanceThickness);
        Rectangle inner = ArenaArea;
        inner.Inflate(-BorderClearanceThickness, -BorderClearanceThickness);
        return outer.Contains(point) && !inner.Contains(point);
    }

    public void Validate(int worldWidth, int worldHeight)
    {
        if (WorldWidth != worldWidth || WorldHeight != worldHeight)
            throw new InvalidOperationException($"Layout world size {WorldWidth}x{WorldHeight} does not match the loaded Tilemap {worldWidth}x{worldHeight}");
        if (WorldWidth != ArenasSubworld.FixedWidth || WorldHeight != ArenasSubworld.FixedHeight)
            throw new InvalidOperationException($"The reusable Arenas world must be exactly {ArenasSubworld.FixedWidth}x{ArenasSubworld.FixedHeight}, got {WorldWidth}x{WorldHeight}");
        Rectangle world = new(0, 0, worldWidth, worldHeight);
        Rectangle outer = ArenaArea;
        outer.Inflate(OuterBorderThickness + BorderClearanceThickness,
            OuterBorderThickness + BorderClearanceThickness);
        if (Generator != ArenaGeneratorKind.SandboxWorld && OuterBorderThickness != 3)
            throw new InvalidOperationException($"The Arenas perimeter must be exactly 3 tiles thick, got {OuterBorderThickness}");
        if (TeamBorderWidth is < 1 or > 10)
            throw new InvalidOperationException($"Team border thickness must be 1..10 tiles, got {TeamBorderWidth}");
        if (ArenaArea.Width < 100 || ArenaArea.Height < 100 || BossArea.Width < 40 || BossArea.Height < 40
            || RedSpawnClearance.Width < 10 || RedSpawnClearance.Height < 3 || BlueSpawnClearance.Width < 10 || BlueSpawnClearance.Height < 3)
            throw new InvalidOperationException($"Arena, boss area, or spawn room is too small: arena={ArenaArea}, boss={BossArea}, redRoom={RedSpawnClearance}, blueRoom={BlueSpawnClearance}");
        if (!Contains(world, outer) || !Contains(world, StagingLobby)) throw new InvalidOperationException($"Arena {ArenaArea}, outer frame {outer}, or staging lobby {StagingLobby} is outside {WorldWidth}x{WorldHeight}");
        if (!Contains(ArenaArea, BossArea) || !Contains(ArenaArea, RedSpawnClearance) || !Contains(ArenaArea, BlueSpawnClearance)) throw new InvalidOperationException($"Boss area or spawn room lies outside arena {ArenaArea}");
        if (Generator != ArenaGeneratorKind.SandboxWorld && (BossArea.Top != ArenaArea.Top || BossArea.Bottom != ArenaArea.Bottom))
            throw new InvalidOperationException($"Boss area must span the full arena height: arena={ArenaArea}, boss={BossArea}");
        if (!RedSpawnClearance.Contains(RedSpawn) || !BlueSpawnClearance.Contains(BlueSpawn) || !BossArea.Contains(BossSpawn)) throw new InvalidOperationException("Red, Blue, or boss spawn lies outside its configured room");
        if (RedSpawnClearance.Intersects(BossArea) || BlueSpawnClearance.Intersects(BossArea) || RedSpawnClearance.Intersects(BlueSpawnClearance)) throw new InvalidOperationException("Spawn rooms and boss area may not overlap");
        if (BlueBorderX < ArenaArea.Left || RedBorderX > ArenaArea.Right || BlueBorderX >= RedBorderX)
            throw new InvalidOperationException($"Team border X values must satisfy arenaLeft <= blueBorder < redBorder <= arenaRight, got {ArenaArea.Left} <= {BlueBorderX} < {RedBorderX} <= {ArenaArea.Right}");
        if (RedSpawn.X >= RedBorderX || BlueSpawn.X < BlueBorderX)
            throw new InvalidOperationException($"Red spawn X {RedSpawn.X} must be left of Red Border X {RedBorderX}, and Blue spawn X {BlueSpawn.X} must be at or right of Blue Border X {BlueBorderX}");
    }

    private static bool Contains(Rectangle outer, Rectangle inner) => inner.Left >= outer.Left && inner.Top >= outer.Top && inner.Right <= outer.Right && inner.Bottom <= outer.Bottom;

    public void Write(BinaryWriter writer)
    {
        writer.Write((byte)Generator);
        writer.Write(Seed);
        writer.Write(WorldWidth); writer.Write(WorldHeight);
        Write(writer, ArenaArea); Write(writer, BossArea); Write(writer, RedSpawnClearance); Write(writer, BlueSpawnClearance);
        Write(writer, RedSpawn); Write(writer, BlueSpawn); Write(writer, BossSpawn); Write(writer, StagingLobby);
        writer.Write(OuterBorderThickness); writer.Write(TeamBorderWidth); writer.Write(BlueBorderX); writer.Write(RedBorderX);
        writer.Write(AutoPlaceTeamSpawns); writer.Write(AutoPlaceBossSpawn);
    }

    public static ArenaLayout Read(BinaryReader reader) => new()
    {
        Generator = (ArenaGeneratorKind)reader.ReadByte(),
        Seed = reader.ReadInt32(),
        WorldWidth = reader.ReadInt32(), WorldHeight = reader.ReadInt32(),
        ArenaArea = ReadRectangle(reader), BossArea = ReadRectangle(reader),
        RedSpawnClearance = ReadRectangle(reader), BlueSpawnClearance = ReadRectangle(reader),
        RedSpawn = ReadPoint(reader), BlueSpawn = ReadPoint(reader), BossSpawn = ReadPoint(reader),
        StagingLobby = ReadRectangle(reader),
        OuterBorderThickness = reader.ReadInt32(), TeamBorderWidth = reader.ReadInt32(),
        BlueBorderX = reader.ReadInt32(), RedBorderX = reader.ReadInt32(),
        AutoPlaceTeamSpawns = reader.ReadBoolean(), AutoPlaceBossSpawn = reader.ReadBoolean()
    };

    private static void Write(BinaryWriter writer, Rectangle value) { writer.Write(value.X); writer.Write(value.Y); writer.Write(value.Width); writer.Write(value.Height); }
    private static void Write(BinaryWriter writer, Point value) { writer.Write(value.X); writer.Write(value.Y); }
    private static Rectangle ReadRectangle(BinaryReader reader) => new(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
    private static Point ReadPoint(BinaryReader reader) => new(reader.ReadInt32(), reader.ReadInt32());
}
