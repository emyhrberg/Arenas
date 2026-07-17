using Arenas.Core.Configs.ConfigElements;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria.Enums;

namespace Arenas.Common.Generation;

public sealed class ArenaLayout
{
    public const int MirrorRight = 849;

    public ArenaGeneratorKind Generator { get; init; }
    public int Seed { get; init; }
    public Rectangle ArenaArea { get; init; }
    public Rectangle BossArea { get; init; }
    public Rectangle RedSpawnClearance { get; init; }
    public Rectangle BlueSpawnClearance { get; init; }
    public Point RedSpawn { get; init; }
    public Point BlueSpawn { get; init; }
    public Point BossSpawn { get; init; }
    public Rectangle StagingLobby { get; init; } = ArenaGeneratorRegistry.StagingLobby;
    public Rectangle LeftBossEntrance { get; init; }
    public Rectangle RightBossEntrance { get; init; }

    public IReadOnlyList<Rectangle> EntranceGaps => [LeftBossEntrance, RightBossEntrance];
    public IReadOnlyList<Rectangle> StructuralRegions
    {
        get
        {
            int outer = ArenaGeneratorRegistry.OuterBorderThickness;
            return
            [
                new Rectangle(ArenaArea.Left - outer, ArenaArea.Top - outer, ArenaArea.Width + outer * 2, outer),
                new Rectangle(ArenaArea.Left - outer, ArenaArea.Bottom, ArenaArea.Width + outer * 2, outer),
                new Rectangle(ArenaArea.Left - outer, ArenaArea.Top, outer, ArenaArea.Height),
                new Rectangle(ArenaArea.Right, ArenaArea.Top, outer, ArenaArea.Height),
                new Rectangle(BossArea.Left - 1, BossArea.Top - 1, BossArea.Width + 2, 1),
                new Rectangle(BossArea.Left - 1, BossArea.Bottom, BossArea.Width + 2, 1),
                new Rectangle(BossArea.Left - 1, BossArea.Top, 1, LeftBossEntrance.Top - BossArea.Top),
                new Rectangle(BossArea.Left - 1, LeftBossEntrance.Bottom, 1, BossArea.Bottom - LeftBossEntrance.Bottom),
                new Rectangle(BossArea.Right, BossArea.Top, 1, RightBossEntrance.Top - BossArea.Top),
                new Rectangle(BossArea.Right, RightBossEntrance.Bottom, 1, BossArea.Bottom - RightBossEntrance.Bottom)
            ];
        }
    }

    public Point TeamSpawn(Team team) => team == Team.Blue ? BlueSpawn : RedSpawn;

    public bool IsProtectedTile(int x, int y)
    {
        Point point = new(x, y);
        Rectangle outer = ArenaArea;
        outer.Inflate(ArenaGeneratorRegistry.OuterBorderThickness, ArenaGeneratorRegistry.OuterBorderThickness);
        if (outer.Contains(point) && !ArenaArea.Contains(point))
            return true;

        Rectangle bossOuter = BossArea;
        bossOuter.Inflate(ArenaGeneratorRegistry.BossBorderThickness, ArenaGeneratorRegistry.BossBorderThickness);
        return bossOuter.Contains(point) && !BossArea.Contains(point)
            && !LeftBossEntrance.Contains(point) && !RightBossEntrance.Contains(point);
    }

    public void Validate(int worldWidth, int worldHeight)
    {
        Rectangle world = new(0, 0, worldWidth, worldHeight);
        Rectangle outer = ArenaArea; outer.Inflate(ArenaGeneratorRegistry.OuterBorderThickness, ArenaGeneratorRegistry.OuterBorderThickness);
        if (!Contains(world, outer) || !Contains(world, StagingLobby)) throw new InvalidOperationException("Arena or staging lobby is outside the loaded world.");
        if (!Contains(ArenaArea, BossArea) || !Contains(ArenaArea, RedSpawnClearance) || !Contains(ArenaArea, BlueSpawnClearance)) throw new InvalidOperationException("A generated combat region is outside the arena.");
        if (!RedSpawnClearance.Contains(RedSpawn) || !BlueSpawnClearance.Contains(BlueSpawn) || !BossArea.Contains(BossSpawn)) throw new InvalidOperationException("A generated spawn point is outside its clearance region.");
        if (RedSpawnClearance.Intersects(BossArea) || BlueSpawnClearance.Intersects(BossArea) || RedSpawnClearance.Intersects(BlueSpawnClearance)) throw new InvalidOperationException("Generated spawn clearances and boss regions overlap.");
        if (BlueSpawn.X != MirrorRight - RedSpawn.X || BlueSpawn.Y != RedSpawn.Y || BlueSpawnClearance.X != MirrorRight + 1 - RedSpawnClearance.Right
            || BlueSpawnClearance.Y != RedSpawnClearance.Y || BlueSpawnClearance.Size() != RedSpawnClearance.Size()) throw new InvalidOperationException("Team spawn geometry is not exactly mirrored.");
        if (BossArea.Center.X != 425 || ArenaArea.Center.X != 425 || BossSpawn.X != 425
            || BossArea.Left != MirrorRight + 1 - BossArea.Right || ArenaArea.Left != MirrorRight + 1 - ArenaArea.Right
            || StagingLobby.Left != MirrorRight + 1 - StagingLobby.Right)
            throw new InvalidOperationException("Arena geometry must use the fixed mirror axis between columns 424 and 425.");
        if (LeftBossEntrance.Width != 1 || RightBossEntrance.Width != 1 || LeftBossEntrance.Height != 40 || RightBossEntrance.Height != 40
            || LeftBossEntrance.X != BossArea.Left - 1 || RightBossEntrance.X != BossArea.Right
            || RightBossEntrance.X != MirrorRight - LeftBossEntrance.X || LeftBossEntrance.Y != RightBossEntrance.Y
            || LeftBossEntrance.Top < BossArea.Top || LeftBossEntrance.Bottom > BossArea.Bottom)
            throw new InvalidOperationException("Boss-area entrances are invalid.");
    }

    private static bool Contains(Rectangle outer, Rectangle inner) => inner.Left >= outer.Left && inner.Top >= outer.Top && inner.Right <= outer.Right && inner.Bottom <= outer.Bottom;

    public void Write(BinaryWriter writer)
    {
        writer.Write((byte)Generator);
        writer.Write(Seed);
        Write(writer, ArenaArea); Write(writer, BossArea); Write(writer, RedSpawnClearance); Write(writer, BlueSpawnClearance);
        Write(writer, RedSpawn); Write(writer, BlueSpawn); Write(writer, BossSpawn); Write(writer, StagingLobby);
        Write(writer, LeftBossEntrance); Write(writer, RightBossEntrance);
    }

    public static ArenaLayout Read(BinaryReader reader) => new()
    {
        Generator = (ArenaGeneratorKind)reader.ReadByte(),
        Seed = reader.ReadInt32(),
        ArenaArea = ReadRectangle(reader), BossArea = ReadRectangle(reader),
        RedSpawnClearance = ReadRectangle(reader), BlueSpawnClearance = ReadRectangle(reader),
        RedSpawn = ReadPoint(reader), BlueSpawn = ReadPoint(reader), BossSpawn = ReadPoint(reader),
        StagingLobby = ReadRectangle(reader), LeftBossEntrance = ReadRectangle(reader), RightBossEntrance = ReadRectangle(reader)
    };

    private static void Write(BinaryWriter writer, Rectangle value) { writer.Write(value.X); writer.Write(value.Y); writer.Write(value.Width); writer.Write(value.Height); }
    private static void Write(BinaryWriter writer, Point value) { writer.Write(value.X); writer.Write(value.Y); }
    private static Rectangle ReadRectangle(BinaryReader reader) => new(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
    private static Point ReadPoint(BinaryReader reader) => new(reader.ReadInt32(), reader.ReadInt32());
}
