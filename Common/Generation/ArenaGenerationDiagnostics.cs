using Arenas.Core.Configs.ConfigElements;
using System;
using System.Collections.Generic;
using Terraria.ID;
using Terraria.WorldBuilding;

namespace Arenas.Common.Generation;

internal static class ArenaGenerationDiagnostics
{
    public static void LogSnapshot(string stage, ArenaLayout layout)
    {
        Metrics metrics = Measure(layout);
        Log.Chat($"[WorldGenAudit/{stage}] {metrics.Summary}");
    }

    public static void ValidateOrThrow(ArenaLayout layout, IArenaGenerator generator)
    {
        Metrics m = Measure(layout, generator.WorldSurface, generator.RockLayer);
        List<string> failures = [];
        Log.Chat($"[WorldGenAudit/Final] generator={layout.Generator} seed={layout.Seed} {m.Summary}");

        if (m.Active < 50_000)
            failures.Add($"active tiles={m.Active} < 50000; Terrain/Jungle initialization did not fill the arena");
        if (m.CaveAir < 3_000)
            failures.Add($"underground air={m.CaveAir} < 3000; Small Holes/Dirt Layer Caves/Rock Layer Caves did not carve enough terrain");

        switch (layout.Generator)
        {
            case ArenaGeneratorKind.KingSlimeSurface:
            case ArenaGeneratorKind.EyeSurface:
                ValidateSurface(m, failures);
                break;
            case ArenaGeneratorKind.PlanteraJungle:
                ValidateJungle(m, layout, failures, requireTemple: false);
                break;
            case ArenaGeneratorKind.GolemTemple:
                ValidateJungle(m, layout, failures, requireTemple: true);
                break;
        }

        if (failures.Count == 0)
        {
            Log.Chat($"[WorldGenAudit/PASS] {layout.Generator} seed={layout.Seed} passed {m.CheckCount(layout.Generator)} generation invariants");
            return;
        }

        foreach (string failure in failures)
            Log.Chat($"[WorldGenAudit/FAIL] {failure}");
        throw new InvalidOperationException($"Arena generation audit failed for {layout.Generator} seed={layout.Seed}: {string.Join(" | ", failures)}. Metrics: {m.Summary}");
    }

    private static void ValidateSurface(Metrics m, List<string> failures)
    {
        if (m.Grass < 60) failures.Add($"grass tiles={m.Grass} < 60; Grass or Planting Trees prerequisites failed");
        if (m.Tree < 20) failures.Add($"tree trunk tiles={m.Tree} < 20; Planting Trees produced no usable forest");
        if (m.Sand < 800) failures.Add($"sand-family tiles={m.Sand} < 800; Ocean Sand/Full Desert did not create a desert and coasts");
        if (m.Snow < 800) failures.Add($"snow/ice tiles={m.Snow} < 800; Generate Ice Biome did not cover the configured compact snow origin");
        if (m.LeftOceanLiquid < 150) failures.Add($"left ocean liquid={m.LeftOceanLiquid} < 150; Beaches did not fill the accessible left coast");
        if (m.RightOceanLiquid < 150) failures.Add($"right ocean liquid={m.RightOceanLiquid} < 150; Beaches did not fill the accessible right coast");
        if (m.SkySolid < 150) failures.Add($"floating sky solids={m.SkySolid} < 150; vanilla FloatingIsland structures were not generated above terrain");
        if (m.Platforms > 0) failures.Add($"platform tiles={m.Platforms}; surface arenas must not contain pre-created platform rows in the sky");
    }

    private static void ValidateJungle(Metrics m, ArenaLayout layout, List<string> failures, bool requireTemple)
    {
        if (m.Mud + m.JungleGrass < 40_000) failures.Add($"mud+jungle grass={m.Mud + m.JungleGrass} < 40000; the arena is not a dense Jungle mass");
        if (m.Dirt + m.Stone > 5_000) failures.Add($"dirt+stone={m.Dirt + m.Stone} > 5000; compact Jungle initialization leaked too much generic underground terrain");
        if (m.JungleGrass < 800) failures.Add($"jungle grass={m.JungleGrass} < 800; Mud Caves To Grass did not expose enough Jungle surfaces");
        if (m.JungleWall < 10_000) failures.Add($"unsafe Jungle walls={m.JungleWall} < 10000; Jungle wall generation is incomplete");
        if (m.Liquid < 80) failures.Add($"liquid tiles={m.Liquid} < 80; wet cave generation or Settle Liquids did not survive");
        if (m.BossSolidRatio < .15 || m.BossSolidRatio > .88)
            failures.Add($"boss-area solid ratio={m.BossSolidRatio:P1}; expected natural cave density between 15% and 88%, not an empty rectangle or solid block");
        if (!requireTemple) return;
        if (GenVars.tRooms < 10) failures.Add($"Temple rooms={GenVars.tRooms} < 10; makeTemple was not run with vanilla small-world scaling");
        if (m.LihzahrdBrick < 2_500) failures.Add($"Lihzahrd Brick tiles={m.LihzahrdBrick} < 2500; Temple body is incomplete");
        if (m.TempleWall < 500) failures.Add($"unsafe Temple walls={m.TempleWall} < 500; Temple rooms/pathing are incomplete");
        if (m.Altar < 1) failures.Add("Lihzahrd Altar tiles=0; the vanilla Lihzahrd Altars pass did not finish the Temple");
        if (GenVars.tRight - GenVars.tLeft < 150 || GenVars.tBottom - GenVars.tTop < 70)
            failures.Add($"Temple bounds={GenVars.tLeft},{GenVars.tTop}..{GenVars.tRight},{GenVars.tBottom}; expected at least 150x70 tiles");
        if (!layout.BossArea.Contains(layout.BossSpawn)) failures.Add($"resolved Golem spawn {layout.BossSpawn} lies outside BossArea {layout.BossArea}");
    }

    private static Metrics Measure(ArenaLayout layout, double? worldSurface = null, double? rockLayer = null)
    {
        Rectangle area = layout.ArenaArea;
        int surface = (int)(worldSurface ?? Main.worldSurface);
        int rock = (int)(rockLayer ?? Main.rockLayer);
        Metrics m = new();
        for (int x = area.Left; x < area.Right; x++)
            for (int y = area.Top; y < area.Bottom; y++)
            {
                Tile tile = Main.tile[x, y];
                if (tile.HasTile)
                {
                    m.Active++;
                    ushort type = tile.TileType;
                    if (type == TileID.Grass) m.Grass++;
                    if (type == TileID.Dirt) m.Dirt++;
                    if (type == TileID.Stone) m.Stone++;
                    if (type == TileID.Mud) m.Mud++;
                    if (type == TileID.JungleGrass) m.JungleGrass++;
                    if (type == TileID.Hive) m.Hive++;
                    if (type == TileID.LihzahrdBrick) m.LihzahrdBrick++;
                    if (type == TileID.LihzahrdAltar) m.Altar++;
                    if (type == TileID.Platforms) m.Platforms++;
                    if (TileID.Sets.IsATreeTrunk[type]) m.Tree++;
                    if (TileID.Sets.Conversion.Sand[type] || TileID.Sets.Conversion.HardenedSand[type] || TileID.Sets.Conversion.Sandstone[type]) m.Sand++;
                    if (type is TileID.SnowBlock or TileID.IceBlock or TileID.BreakableIce) m.Snow++;
                    if (y < Math.Max(area.Top, surface - 10) && x > area.Left + 70 && x < area.Right - 70) m.SkySolid++;
                }
                else
                {
                    m.Air++;
                    if (y > surface + 20 && y < area.Bottom - 10) m.CaveAir++;
                }
                if (tile.LiquidAmount > 0)
                {
                    m.Liquid++;
                    if (x < area.Left + 55) m.LeftOceanLiquid++;
                    if (x >= area.Right - 55) m.RightOceanLiquid++;
                }
                if (tile.WallType == WallID.JungleUnsafe) m.JungleWall++;
                if (tile.WallType == WallID.LihzahrdBrickUnsafe) m.TempleWall++;
                if (layout.BossArea.Contains(x, y))
                {
                    m.BossTiles++;
                    if (tile.HasTile) m.BossSolid++;
                }
            }
        m.Surface = surface;
        m.Rock = rock;
        return m;
    }

    private sealed class Metrics
    {
        public int Active, Air, CaveAir, Liquid, LeftOceanLiquid, RightOceanLiquid, Grass, Dirt, Stone, Mud, JungleGrass, Hive, JungleWall;
        public int Tree, Sand, Snow, SkySolid, Platforms, LihzahrdBrick, TempleWall, Altar, BossTiles, BossSolid, Surface, Rock;
        public double BossSolidRatio => BossTiles == 0 ? 0 : BossSolid / (double)BossTiles;
        public string Summary => $"active={Active} air={Air} caveAir={CaveAir} liquid={Liquid} oceans={LeftOceanLiquid}/{RightOceanLiquid} grass={Grass} trees={Tree} sand={Sand} snow={Snow} skySolid={SkySolid} platforms={Platforms} dirt={Dirt} stone={Stone} mud={Mud} jungleGrass={JungleGrass} hive={Hive} jungleWall={JungleWall} templeBrick={LihzahrdBrick} templeWall={TempleWall} altar={Altar} bossSolid={BossSolidRatio:P1} surface={Surface} rock={Rock}";
        public int CheckCount(ArenaGeneratorKind kind) => kind is ArenaGeneratorKind.KingSlimeSurface or ArenaGeneratorKind.EyeSurface ? 10 : kind == ArenaGeneratorKind.GolemTemple ? 14 : 8;
    }
}
