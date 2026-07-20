//using System;

//namespace Arenas.Common.Generation;

///// <summary>Keeps the 3-clear / 3-brick / 3-clear perimeter envelope intact.</summary>
//internal sealed class ArenaStructuralProtectionPlayer : ModPlayer
//{
//    public override void Load()
//    {
//        On_Player.PlaceThing_Tiles += PlaceTiles;
//        On_Player.PlaceThing_Walls += PlaceWalls;
//        On_Player.ItemCheck_UseMiningTools += UseMiningTools;
//        On_Player.ItemCheck_UseWiringTools += UseWiringTools;
//        On_Player.ItemCheck_CutTiles += CutTiles;
//    }

//    public override void Unload()
//    {
//        On_Player.PlaceThing_Tiles -= PlaceTiles;
//        On_Player.PlaceThing_Walls -= PlaceWalls;
//        On_Player.ItemCheck_UseMiningTools -= UseMiningTools;
//        On_Player.ItemCheck_UseWiringTools -= UseWiringTools;
//        On_Player.ItemCheck_CutTiles -= CutTiles;
//    }

//    private static bool CanModifyTarget() => !IsProtected(Player.tileTargetX, Player.tileTargetY);
//    private static void PlaceTiles(On_Player.orig_PlaceThing_Tiles orig, Player self) { if (CanModifyTarget()) orig(self); }
//    private static void PlaceWalls(On_Player.orig_PlaceThing_Walls orig, Player self) { if (CanModifyTarget()) orig(self); }
//    private static void UseMiningTools(On_Player.orig_ItemCheck_UseMiningTools orig, Player self, Item item) { if (CanModifyTarget()) orig(self, item); }
//    private static void UseWiringTools(On_Player.orig_ItemCheck_UseWiringTools orig, Player self, Item item) { if (CanModifyTarget()) orig(self, item); }
//    private static void CutTiles(On_Player.orig_ItemCheck_CutTiles orig, Player self, Item item, Rectangle itemRectangle, bool[] shouldIgnore)
//    {
//        if (!TouchesProtectedTile(itemRectangle)) orig(self, item, itemRectangle, shouldIgnore);
//    }

//    private static bool TouchesProtectedTile(Rectangle worldArea)
//    {
//        int left = Math.Max(0, worldArea.Left / 16);
//        int top = Math.Max(0, worldArea.Top / 16);
//        int right = Math.Min(Main.maxTilesX, (worldArea.Right + 15) / 16);
//        int bottom = Math.Min(Main.maxTilesY, (worldArea.Bottom + 15) / 16);
//        for (int x = left; x < right; x++)
//            for (int y = top; y < bottom; y++)
//                if (IsProtected(x, y)) return true;
//        return false;
//    }

//    private static bool IsProtected(int x, int y) => ArenaWorldSystem.Layout?.IsProtectedTile(x, y) == true;
//}

//internal sealed class ArenaStructuralProtectionTile : GlobalTile
//{
//    public override bool CanKillTile(int i, int j, int type, ref bool blockDamaged) => CanModify(i, j);
//    public override bool CanExplode(int i, int j, int type) => CanModify(i, j);
//    public override bool CanPlace(int i, int j, int type) => CanModify(i, j);
//    public override bool CanReplace(int i, int j, int type, int tileTypeBeingPlaced) => CanModify(i, j);
//    private static bool CanModify(int i, int j) => ArenaWorldSystem.Layout?.IsProtectedTile(i, j) != true;
//}
