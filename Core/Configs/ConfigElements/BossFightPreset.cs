using System;
using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace Arenas.Core.Configs.ConfigElements;

public enum FightTime
{
    Unchanged,
    Day,
    Night
}

public sealed class TilePoint
{
    [DefaultValue(-1)] public int X { get; set; } = -1;
    [DefaultValue(-1)] public int Y { get; set; } = -1;

    public Point ToPoint() => new(X, Y);
}

public sealed class TileRectangle
{
    [DefaultValue(0)] public int X { get; set; }
    [DefaultValue(0)] public int Y { get; set; }
    [DefaultValue(1)] [Range(1, 10000)] public int Width { get; set; } = 1;
    [DefaultValue(1)] [Range(1, 10000)] public int Height { get; set; } = 1;

    public Rectangle ToRectangle() => new(X, Y, Math.Max(1, Width), Math.Max(1, Height));
}

public sealed class BossFightPreset
{
    public string Name { get; set; } = "";
    public NPCDefinition Boss { get; set; } = new();

    [DefaultValue(500)]
    [Range(1, 10000)]
    public int MaxHealth { get; set; } = 500;

    [DefaultValue(200)]
    [Range(0, 1000)]
    public int MaxMana { get; set; } = 200;

    [DefaultValue(600)]
    [Range(1, 3600)]
    public int RoundDurationSeconds { get; set; } = 600;

    [DefaultValue(10)]
    [Range(0, 300)]
    public int FreezeCountdownSeconds { get; set; } = 10;

    [DefaultValue(30)]
    [Range(5, 300)]
    public int VotingDurationSeconds { get; set; } = 30;

    [DefaultValue(FightTime.Unchanged)]
    public FightTime Time { get; set; } = FightTime.Unchanged;

    public TilePoint RedSpawn { get; set; } = new() { X = 107, Y = 189 };
    public TilePoint BlueSpawn { get; set; } = new() { X = 688, Y = 189 };
    public TilePoint BossSpawn { get; set; } = new();
    public TileRectangle BossArea { get; set; } = new() { X = 167, Y = 458, Width = 205, Height = 63 };
    public Loadout Loadout { get; set; } = new();
}
