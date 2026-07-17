using Microsoft.Xna.Framework;
using Arenas.Common.Rounds;
using System;
using System.IO;
using Terraria;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Arenas.Common.Spawnbox;

[Autoload(Side = ModSide.Both)]
public sealed class SpawnBoxSystem : ModSystem
{
    public const int TileSize = 16;
    internal static readonly Team[] Teams = [Team.Red, Team.Green];

    public SpawnBoxSettings RedSettings { get; private set; } = SpawnBoxSettings.Default;
    public SpawnBoxSettings GreenSettings { get; private set; } = SpawnBoxSettings.Default;
    public bool Active => ArenaWorldSystem.Active;
    public bool CanEnter => false;
    public bool CanExit => Active && ArenaRoundSystem.Phase == RoundPhase.Playing;

    internal static SpawnBoxSettings DefaultSettings => SpawnBoxSettings.Default;
    public SpawnBoxSettings GetSettings(Team team) => IsRight(team) ? GreenSettings : RedSettings;

    public Rectangle GetTileArea(Team team)
    {
        SpawnBoxSettings s = GetSettings(team); Point center = ArenaRoundSystem.TeamSpawn(team);
        int x = center.X - s.Width / 2 + s.XOffset, y = center.Y - s.Height / 2 + s.YOffset;
        if (Main.maxTilesX > 0 && Main.maxTilesY > 0)
        {
            x = Math.Clamp(x, 0, Math.Max(0, Main.maxTilesX - s.Width));
            y = Math.Clamp(y, 0, Math.Max(0, Main.maxTilesY - s.Height));
        }
        return new Rectangle(x, y, s.Width, s.Height);
    }

    public Rectangle[] TileAreas => [GetTileArea(Team.Red), GetTileArea(Team.Green)];
    public Rectangle[] WorldAreas => [TileToWorld(GetTileArea(Team.Red)), TileToWorld(GetTileArea(Team.Green))];
    public int GetThickness(Team team) => GetSettings(team).Thickness;
    public Rectangle GetBorderOuterTileArea(Team team) => Outer(GetTileArea(team), GetThickness(team));
    public Rectangle[] GetBorderTileAreas(Team team) => Borders(GetTileArea(team), GetThickness(team));
    public Rectangle[] GetBorderWorldAreas(Team team)
    {
        Rectangle[] areas = GetBorderTileAreas(team);
        for (int i = 0; i < areas.Length; i++) areas[i] = TileToWorld(areas[i]);
        return areas;
    }

    public Rectangle[] BorderOuterTileAreas => [GetBorderOuterTileArea(Team.Red), GetBorderOuterTileArea(Team.Green)];

    public Rectangle[] BorderTileAreas
    {
        get
        {
            Rectangle[] red = GetBorderTileAreas(Team.Red), green = GetBorderTileAreas(Team.Green);
            return [red[0], red[1], red[2], red[3], green[0], green[1], green[2], green[3]];
        }
    }

    public Rectangle[] BorderWorldAreas
    {
        get
        {
            Rectangle[] red = GetBorderWorldAreas(Team.Red), green = GetBorderWorldAreas(Team.Green);
            return [red[0], red[1], red[2], red[3], green[0], green[1], green[2], green[3]];
        }
    }

    public bool CanCross(Team boxTeam, Player player) => player?.active == true && CanCross(boxTeam, (Team)player.team, player.Hitbox);
    internal bool CanCross(Team boxTeam, Team playerTeam, Rectangle playerHitbox) =>
        CanExit && IsArenaTeam(playerTeam) && Normalize(boxTeam) == Normalize(playerTeam) && TileToWorld(GetTileArea(boxTeam)).Intersects(playerHitbox);

    public bool ContainsTile(int x, int y) => Active && (GetTileArea(Team.Red).Contains(x, y) || GetTileArea(Team.Green).Contains(x, y));
    public bool ContainsTile(Point tile) => ContainsTile(tile.X, tile.Y);
    public bool TouchesWorldHitbox(Rectangle hitbox) => TouchesTileRectangle(WorldToTile(hitbox));
    public bool TouchesTileRectangle(Rectangle tiles) => Active && (GetTileArea(Team.Red).Intersects(tiles) || GetTileArea(Team.Green).Intersects(tiles));
    public bool TouchesBorderWorldHitbox(Rectangle hitbox)
    {
        foreach (Rectangle border in BorderWorldAreas)
            if (border.Intersects(hitbox))
                return true;

        return false;
    }

    public static Rectangle TileToWorld(Rectangle tiles) =>
        new(tiles.X * TileSize, tiles.Y * TileSize, tiles.Width * TileSize, tiles.Height * TileSize);
    public static Rectangle WorldToTile(Rectangle world) => new(world.Left / TileSize, world.Top / TileSize,
        (world.Right + TileSize - 1) / TileSize - world.Left / TileSize, (world.Bottom + TileSize - 1) / TileSize - world.Top / TileSize);

    private static Rectangle Outer(Rectangle area, int thickness) { area.Inflate(thickness, thickness); return area; }
    private static Rectangle[] Borders(Rectangle inner, int thickness)
    {
        Rectangle outer = Outer(inner, thickness);
        return [new(outer.X, outer.Y, outer.Width, thickness), new(outer.X, inner.Bottom, outer.Width, thickness), new(outer.X, inner.Y, thickness, inner.Height), new(inner.Right, inner.Y, thickness, inner.Height)];
    }

    internal void SetFromTool(Team team, SpawnBoxSettings settings, bool sync = false)
    {
        team = Normalize(team); settings = settings.Clamped();
        if (team == Team.Green) GreenSettings = settings; else RedSettings = settings;

        if (sync && Main.netMode == NetmodeID.Server)
            SpawnBoxNetHandler.SendSync(team, settings);
    }

    internal void ReceiveSync(Team team, SpawnBoxSettings settings) => SetFromTool(team, settings);

    public override void ClearWorld() => RedSettings = GreenSettings = DefaultSettings;

    public override void SaveWorldData(TagCompound tag)
    {
        Save(tag, "RedSpawnBox", RedSettings);
        Save(tag, "GreenSpawnBox", GreenSettings);
    }

    public override void LoadWorldData(TagCompound tag)
    {
        SpawnBoxSettings legacy = Load(tag, "SpawnBox", DefaultSettings);
        RedSettings = Load(tag, "RedSpawnBox", legacy);
        GreenSettings = Load(tag, "GreenSpawnBox", legacy);
    }

    public override void NetSend(BinaryWriter writer) { RedSettings.Write(writer); GreenSettings.Write(writer); }
    public override void NetReceive(BinaryReader reader) { RedSettings = SpawnBoxSettings.Read(reader); GreenSettings = SpawnBoxSettings.Read(reader); }

    private static bool IsRight(Team team) => team is Team.Blue or Team.Green;
    private static bool IsArenaTeam(Team team) => team is Team.Red or Team.Blue or Team.Green;
    private static Team Normalize(Team team) => IsRight(team) ? Team.Green : Team.Red;
    private static void Save(TagCompound tag, string prefix, SpawnBoxSettings s)
    {
        tag[$"{prefix}Width"] = s.Width; tag[$"{prefix}Height"] = s.Height; tag[$"{prefix}XOffset"] = s.XOffset;
        tag[$"{prefix}YOffset"] = s.YOffset; tag[$"{prefix}Thickness"] = s.Thickness;
    }
    private static SpawnBoxSettings Load(TagCompound tag, string prefix, SpawnBoxSettings fallback) => !tag.ContainsKey($"{prefix}Width") ? fallback : new SpawnBoxSettings(
        tag.GetInt($"{prefix}Width"), tag.GetInt($"{prefix}Height"), tag.GetInt($"{prefix}XOffset"), tag.GetInt($"{prefix}YOffset"),
        tag.ContainsKey($"{prefix}Thickness") ? tag.GetInt($"{prefix}Thickness") : SpawnBoxSettings.DefaultThickness).Clamped();
}
