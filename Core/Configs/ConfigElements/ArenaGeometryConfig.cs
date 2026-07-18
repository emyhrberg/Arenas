using System.ComponentModel;
using System.IO;
using Terraria.ID;
using Terraria.ModLoader.Config;

namespace Arenas.Core.Configs.ConfigElements;

/// <summary>Per-fight world size, structural bounds, spawns and logical movement borders.</summary>
public sealed class ArenaGeometryConfig
{
    [ConfigIcon("IconCheckOn", "IconCheckOff"), DefaultValue(false)]
    public bool Enabled { get; set; }

    [ConfigIcon(ItemID.Compass), DefaultValue(850), Range(700, 1600), Increment(2)]
    public int WorldWidth { get; set; } = 850;

    [ConfigIcon(ItemID.DepthMeter), DefaultValue(600), Range(500, 1000)]
    public int WorldHeight { get; set; } = 600;

    [ConfigIcon(ItemID.LihzahrdBrick), DefaultValue(120), Range(4, 700)]
    public int ArenaLeft { get; set; } = 120;

    [ConfigIcon(ItemID.LihzahrdBrick), DefaultValue(730), Range(100, 1596)]
    public int ArenaRight { get; set; } = 730;

    [ConfigIcon(ItemID.LihzahrdBrick), DefaultValue(48), Range(4, 900)]
    public int ArenaTop { get; set; } = 48;

    [ConfigIcon(ItemID.LihzahrdBrick), DefaultValue(572), Range(100, 996)]
    public int ArenaBottom { get; set; } = 572;

    [ConfigIcon(ItemID.LihzahrdBrick), DefaultValue(3), Range(1, 10)]
    public int OuterBorderThickness { get; set; } = 3;

    [ConfigIcon(ItemID.DirtBlock), DefaultValue(325), Range(4, 1500)]
    public int BossAreaX { get; set; } = 325;

    [ConfigIcon(ItemID.DirtBlock), DefaultValue(80), Range(4, 900)]
    public int BossAreaY { get; set; } = 80;

    [ConfigIcon(ItemID.DirtBlock), DefaultValue(200), Range(40, 1000)]
    public int BossAreaWidth { get; set; } = 200;

    [ConfigIcon(ItemID.DirtBlock), DefaultValue(380), Range(40, 900)]
    public int BossAreaHeight { get; set; } = 380;

    [ConfigIcon(ItemID.Ruler), DefaultValue(325), Range(4, 1500)]
    public int BlueBorderX { get; set; } = 325;

    [ConfigIcon(ItemID.Ruler), DefaultValue(525), Range(4, 1500)]
    public int RedBorderX { get; set; } = 525;

    [ConfigIcon(ItemID.Ruler), DefaultValue(3), Range(1, 10)]
    public int TeamBorderWidth { get; set; } = 3;

    [ConfigIcon(ItemID.Bed), DefaultValue(150), Range(4, 1500)]
    public int RedSpawnX { get; set; } = 150;

    [ConfigIcon(ItemID.Bed), DefaultValue(149), Range(4, 900)]
    public int RedSpawnY { get; set; } = 149;

    [ConfigIcon(ItemID.Bed), DefaultValue(699), Range(4, 1500)]
    public int BlueSpawnX { get; set; } = 699;

    [ConfigIcon(ItemID.Bed), DefaultValue(149), Range(4, 900)]
    public int BlueSpawnY { get; set; } = 149;

    [ConfigIcon(ItemID.SuspiciousLookingEye), DefaultValue(425), Range(4, 1500)]
    public int BossSpawnX { get; set; } = 425;

    [ConfigIcon(ItemID.SuspiciousLookingEye), DefaultValue(149), Range(4, 900)]
    public int BossSpawnY { get; set; } = 149;

    [ConfigIcon(ItemID.Ruler), DefaultValue(20), Range(10, 80)]
    public int SpawnRoomWidth { get; set; } = 20;

    [ConfigIcon(ItemID.Ruler), DefaultValue(16), Range(8, 50)]
    public int SpawnRoomHeight { get; set; } = 16;

    [ConfigIcon("IconCheckOn", "IconCheckOff"), DefaultValue(true)]
    public bool AutoPlaceTeamSpawns { get; set; } = true;

    [ConfigIcon("IconCheckOn", "IconCheckOff"), DefaultValue(false)]
    public bool AutoPlaceBossSpawn { get; set; }

    public ArenaGeometryConfig Clone() => (ArenaGeometryConfig)MemberwiseClone();

    internal void Write(BinaryWriter writer)
    {
        writer.Write(Enabled);
        writer.Write(WorldWidth); writer.Write(WorldHeight);
        writer.Write(ArenaLeft); writer.Write(ArenaRight); writer.Write(ArenaTop); writer.Write(ArenaBottom);
        writer.Write(OuterBorderThickness);
        writer.Write(BossAreaX); writer.Write(BossAreaY); writer.Write(BossAreaWidth); writer.Write(BossAreaHeight);
        writer.Write(BlueBorderX); writer.Write(RedBorderX); writer.Write(TeamBorderWidth);
        writer.Write(RedSpawnX); writer.Write(RedSpawnY); writer.Write(BlueSpawnX); writer.Write(BlueSpawnY);
        writer.Write(BossSpawnX); writer.Write(BossSpawnY);
        writer.Write(SpawnRoomWidth); writer.Write(SpawnRoomHeight);
        writer.Write(AutoPlaceTeamSpawns); writer.Write(AutoPlaceBossSpawn);
    }

    internal static ArenaGeometryConfig Read(BinaryReader reader) => new()
    {
        Enabled = reader.ReadBoolean(),
        WorldWidth = reader.ReadInt32(), WorldHeight = reader.ReadInt32(),
        ArenaLeft = reader.ReadInt32(), ArenaRight = reader.ReadInt32(), ArenaTop = reader.ReadInt32(), ArenaBottom = reader.ReadInt32(),
        OuterBorderThickness = reader.ReadInt32(),
        BossAreaX = reader.ReadInt32(), BossAreaY = reader.ReadInt32(), BossAreaWidth = reader.ReadInt32(), BossAreaHeight = reader.ReadInt32(),
        BlueBorderX = reader.ReadInt32(), RedBorderX = reader.ReadInt32(), TeamBorderWidth = reader.ReadInt32(),
        RedSpawnX = reader.ReadInt32(), RedSpawnY = reader.ReadInt32(), BlueSpawnX = reader.ReadInt32(), BlueSpawnY = reader.ReadInt32(),
        BossSpawnX = reader.ReadInt32(), BossSpawnY = reader.ReadInt32(),
        SpawnRoomWidth = reader.ReadInt32(), SpawnRoomHeight = reader.ReadInt32(),
        AutoPlaceTeamSpawns = reader.ReadBoolean(), AutoPlaceBossSpawn = reader.ReadBoolean()
    };
}

internal static class ArenaGeometryDefaults
{
    public static ArenaGeometryConfig Resolve(BossFightPreset preset, ArenaGeneratorKind kind) =>
        kind == ArenaGeneratorKind.SandboxWorld ? Create(kind) : preset?.Arena is { Enabled: true } custom ? custom.Clone() : Create(kind);

    public static ArenaGeometryConfig Create(ArenaGeneratorKind kind)
    {
        ArenaGeometryConfig geometry = new() { Enabled = true };
        switch (kind)
        {
            case ArenaGeneratorKind.EyeSurface:
                geometry.BossAreaX = 285; geometry.BossAreaY = 70; geometry.BossAreaWidth = 280; geometry.BossAreaHeight = 390;
                geometry.BlueBorderX = 285; geometry.RedBorderX = 565;
                geometry.BossSpawnY = 90;
                break;
            case ArenaGeneratorKind.PlanteraJungle:
                geometry.BossAreaX = 285; geometry.BossAreaY = 150; geometry.BossAreaWidth = 280; geometry.BossAreaHeight = 320;
                geometry.BlueBorderX = 285; geometry.RedBorderX = 565;
                geometry.RedSpawnY = geometry.BlueSpawnY = 329; geometry.BossSpawnY = 300;
                geometry.AutoPlaceTeamSpawns = geometry.AutoPlaceBossSpawn = false;
                break;
            case ArenaGeneratorKind.GolemTemple:
                geometry.BossAreaX = 305; geometry.BossAreaY = 270; geometry.BossAreaWidth = 240; geometry.BossAreaHeight = 230;
                geometry.BlueBorderX = 305; geometry.RedBorderX = 545;
                geometry.RedSpawnY = geometry.BlueSpawnY = 499; geometry.BossSpawnY = 440;
                geometry.AutoPlaceTeamSpawns = false;
                geometry.AutoPlaceBossSpawn = true;
                break;
            case ArenaGeneratorKind.SandboxWorld:
                geometry.Enabled = false;
                break;
        }
        return geometry;
    }
}
