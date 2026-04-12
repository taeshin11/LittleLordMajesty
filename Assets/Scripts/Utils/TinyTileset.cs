using UnityEngine;

/// <summary>
/// Static tile index mapping for Kenney Tiny Town + Tiny Dungeon packs.
/// Both tilemaps are 12 columns x 11 rows, 16x16 pixels each.
/// Index = row * 12 + col, tile files are tile_NNNN.png.
/// Resource path: Art/TinyTown/tile_NNNN or Art/TinyDungeon/tile_NNNN
/// </summary>
public static class TinyTileset
{
    // ---------------------------------------------------------------
    //  TINY TOWN — Overworld / Village tiles
    //  Resource prefix: "Art/TinyTown/"
    // ---------------------------------------------------------------

    // Row 0: Grass and path tiles
    public const string TT_Grass           = "Art/TinyTown/tile_0000";
    public const string TT_GrassAlt1       = "Art/TinyTown/tile_0001";
    public const string TT_GrassAlt2       = "Art/TinyTown/tile_0002";
    public const string TT_GrassFlower1    = "Art/TinyTown/tile_0003";
    public const string TT_GrassFlower2    = "Art/TinyTown/tile_0004";
    public const string TT_GrassDirt       = "Art/TinyTown/tile_0005";
    public const string TT_PathHoriz       = "Art/TinyTown/tile_0006";
    public const string TT_PathVert        = "Art/TinyTown/tile_0007";
    public const string TT_PathCross       = "Art/TinyTown/tile_0008";
    public const string TT_PathCornerTL    = "Art/TinyTown/tile_0009";
    public const string TT_PathCornerTR    = "Art/TinyTown/tile_0010";
    public const string TT_PathCornerBL    = "Art/TinyTown/tile_0011";

    // Row 1: More ground, trees
    public const string TT_PathCornerBR    = "Art/TinyTown/tile_0012";
    public const string TT_PathTeeDown     = "Art/TinyTown/tile_0013";
    public const string TT_PathTeeUp       = "Art/TinyTown/tile_0014";
    public const string TT_PathTeeRight    = "Art/TinyTown/tile_0015";
    public const string TT_PathTeeLeft     = "Art/TinyTown/tile_0016";
    public const string TT_PathEnd         = "Art/TinyTown/tile_0017";
    public const string TT_TreeGreen       = "Art/TinyTown/tile_0018";
    public const string TT_TreeGreenAlt    = "Art/TinyTown/tile_0019";
    public const string TT_TreeAutumn      = "Art/TinyTown/tile_0020";
    public const string TT_TreeAutumnAlt   = "Art/TinyTown/tile_0021";
    public const string TT_TreeDead        = "Art/TinyTown/tile_0022";
    public const string TT_TreePine        = "Art/TinyTown/tile_0023";

    // Row 2: Bushes, flowers, stumps, rocks
    public const string TT_BushGreen       = "Art/TinyTown/tile_0024";
    public const string TT_BushGreenAlt    = "Art/TinyTown/tile_0025";
    public const string TT_BushAutumn      = "Art/TinyTown/tile_0026";
    public const string TT_FlowerRed       = "Art/TinyTown/tile_0027";
    public const string TT_FlowerYellow    = "Art/TinyTown/tile_0028";
    public const string TT_FlowerBlue      = "Art/TinyTown/tile_0029";
    public const string TT_Stump           = "Art/TinyTown/tile_0030";
    public const string TT_RockSmall       = "Art/TinyTown/tile_0031";
    public const string TT_RockLarge       = "Art/TinyTown/tile_0032";
    public const string TT_LogHoriz        = "Art/TinyTown/tile_0033";
    public const string TT_LogVert         = "Art/TinyTown/tile_0034";
    public const string TT_Mushroom        = "Art/TinyTown/tile_0035";

    // Row 3: House walls (bottom), doors
    public const string TT_HouseWallL      = "Art/TinyTown/tile_0036";
    public const string TT_HouseWallM      = "Art/TinyTown/tile_0037";
    public const string TT_HouseWallR      = "Art/TinyTown/tile_0038";
    public const string TT_HouseDoor       = "Art/TinyTown/tile_0039";
    public const string TT_HouseWindow     = "Art/TinyTown/tile_0040";
    public const string TT_HouseWallAlt    = "Art/TinyTown/tile_0041";
    public const string TT_HouseWallL2     = "Art/TinyTown/tile_0042";
    public const string TT_HouseWallM2     = "Art/TinyTown/tile_0043";
    public const string TT_HouseWallR2     = "Art/TinyTown/tile_0044";
    public const string TT_HouseDoor2      = "Art/TinyTown/tile_0045";
    public const string TT_HouseWindow2    = "Art/TinyTown/tile_0046";
    public const string TT_HouseWallAlt2   = "Art/TinyTown/tile_0047";

    // Row 4: House roofs (orange)
    public const string TT_RoofL           = "Art/TinyTown/tile_0048";
    public const string TT_RoofM           = "Art/TinyTown/tile_0049";
    public const string TT_RoofR           = "Art/TinyTown/tile_0050";
    public const string TT_RoofPeak        = "Art/TinyTown/tile_0051";
    public const string TT_RoofChimney     = "Art/TinyTown/tile_0052";
    public const string TT_RoofAlt         = "Art/TinyTown/tile_0053";
    public const string TT_RoofL2          = "Art/TinyTown/tile_0054";
    public const string TT_RoofM2          = "Art/TinyTown/tile_0055";
    public const string TT_RoofR2          = "Art/TinyTown/tile_0056";
    public const string TT_RoofPeak2       = "Art/TinyTown/tile_0057";
    public const string TT_RoofChimney2    = "Art/TinyTown/tile_0058";
    public const string TT_RoofAlt2        = "Art/TinyTown/tile_0059";

    // Row 5: Castle / stone walls
    public const string TT_StoneWallL      = "Art/TinyTown/tile_0060";
    public const string TT_StoneWallM      = "Art/TinyTown/tile_0061";
    public const string TT_StoneWallR      = "Art/TinyTown/tile_0062";
    public const string TT_StoneGate       = "Art/TinyTown/tile_0063";
    public const string TT_StoneTowerL     = "Art/TinyTown/tile_0064";
    public const string TT_StoneTowerR     = "Art/TinyTown/tile_0065";
    public const string TT_StoneWallTop    = "Art/TinyTown/tile_0066";
    public const string TT_CastleFlag      = "Art/TinyTown/tile_0067";
    public const string TT_CastleTower     = "Art/TinyTown/tile_0068";
    public const string TT_CastleWallL     = "Art/TinyTown/tile_0069";
    public const string TT_CastleWallR     = "Art/TinyTown/tile_0070";
    public const string TT_CastleDoor      = "Art/TinyTown/tile_0071";

    // Row 6: More castle parts
    public const string TT_CastleRoofL     = "Art/TinyTown/tile_0072";
    public const string TT_CastleRoofM     = "Art/TinyTown/tile_0073";
    public const string TT_CastleRoofR     = "Art/TinyTown/tile_0074";
    public const string TT_CastleTop       = "Art/TinyTown/tile_0075";
    public const string TT_CastleWindow    = "Art/TinyTown/tile_0076";
    public const string TT_CastleArch      = "Art/TinyTown/tile_0077";
    public const string TT_StoneFloor      = "Art/TinyTown/tile_0078";
    public const string TT_StoneFloorAlt   = "Art/TinyTown/tile_0079";
    public const string TT_StoneStairs     = "Art/TinyTown/tile_0080";
    public const string TT_WallTorch       = "Art/TinyTown/tile_0081";
    public const string TT_Tile82          = "Art/TinyTown/tile_0082";
    public const string TT_Tile83          = "Art/TinyTown/tile_0083";

    // Row 7: Water, bridges
    public const string TT_Water           = "Art/TinyTown/tile_0084";
    public const string TT_WaterEdgeN      = "Art/TinyTown/tile_0085";
    public const string TT_WaterEdgeS      = "Art/TinyTown/tile_0086";
    public const string TT_WaterEdgeW      = "Art/TinyTown/tile_0087";
    public const string TT_WaterEdgeE      = "Art/TinyTown/tile_0088";
    public const string TT_WaterCornerTL   = "Art/TinyTown/tile_0089";
    public const string TT_WaterCornerTR   = "Art/TinyTown/tile_0090";
    public const string TT_WaterCornerBL   = "Art/TinyTown/tile_0091";
    public const string TT_WaterCornerBR   = "Art/TinyTown/tile_0092";
    public const string TT_BridgeHoriz     = "Art/TinyTown/tile_0093";
    public const string TT_BridgeVert      = "Art/TinyTown/tile_0094";
    public const string TT_BridgeEnd       = "Art/TinyTown/tile_0095";

    // Row 8: Fences, more water
    public const string TT_FenceHoriz      = "Art/TinyTown/tile_0096";
    public const string TT_FenceVert       = "Art/TinyTown/tile_0097";
    public const string TT_FenceCornerTL   = "Art/TinyTown/tile_0098";
    public const string TT_FenceCornerTR   = "Art/TinyTown/tile_0099";
    public const string TT_FenceCornerBL   = "Art/TinyTown/tile_0100";
    public const string TT_FenceCornerBR   = "Art/TinyTown/tile_0101";
    public const string TT_FenceGate       = "Art/TinyTown/tile_0102";
    public const string TT_FencePost       = "Art/TinyTown/tile_0103";
    public const string TT_WaterLily       = "Art/TinyTown/tile_0104";
    public const string TT_Tile105         = "Art/TinyTown/tile_0105";
    public const string TT_Tile106         = "Art/TinyTown/tile_0106";
    public const string TT_Tile107         = "Art/TinyTown/tile_0107";

    // Row 9: Signs, items, decorations
    public const string TT_SignPost        = "Art/TinyTown/tile_0108";
    public const string TT_SignBoard       = "Art/TinyTown/tile_0109";
    public const string TT_Well            = "Art/TinyTown/tile_0110";
    public const string TT_Crate           = "Art/TinyTown/tile_0111";
    public const string TT_Barrel          = "Art/TinyTown/tile_0112";
    public const string TT_Pot             = "Art/TinyTown/tile_0113";
    public const string TT_Lamp            = "Art/TinyTown/tile_0114";
    public const string TT_Bench           = "Art/TinyTown/tile_0115";
    public const string TT_Cart            = "Art/TinyTown/tile_0116";
    public const string TT_Wheelbarrow     = "Art/TinyTown/tile_0117";
    public const string TT_Hay             = "Art/TinyTown/tile_0118";
    public const string TT_Scarecrow       = "Art/TinyTown/tile_0119";

    // Row 10: More items
    public const string TT_Crop1           = "Art/TinyTown/tile_0120";
    public const string TT_Crop2           = "Art/TinyTown/tile_0121";
    public const string TT_Crop3           = "Art/TinyTown/tile_0122";
    public const string TT_AnimalChicken   = "Art/TinyTown/tile_0123";
    public const string TT_AnimalCow       = "Art/TinyTown/tile_0124";
    public const string TT_AnimalPig       = "Art/TinyTown/tile_0125";
    public const string TT_Fence2          = "Art/TinyTown/tile_0126";
    public const string TT_Tile127         = "Art/TinyTown/tile_0127";
    public const string TT_Tile128         = "Art/TinyTown/tile_0128";
    public const string TT_Tile129         = "Art/TinyTown/tile_0129";
    public const string TT_Tile130         = "Art/TinyTown/tile_0130";
    public const string TT_Tile131         = "Art/TinyTown/tile_0131";

    // ---------------------------------------------------------------
    //  TINY DUNGEON — Characters, items, dungeon tiles
    //  Resource prefix: "Art/TinyDungeon/"
    // ---------------------------------------------------------------

    // Row 6-7: Character sprites (front-facing)
    // Based on tilemap analysis: characters start around tile_0072
    public const string TD_KnightM         = "Art/TinyDungeon/tile_0084";
    public const string TD_KnightF         = "Art/TinyDungeon/tile_0085";
    public const string TD_Mage            = "Art/TinyDungeon/tile_0086";
    public const string TD_Rogue           = "Art/TinyDungeon/tile_0087";
    public const string TD_Ranger          = "Art/TinyDungeon/tile_0088";
    public const string TD_Priest          = "Art/TinyDungeon/tile_0089";
    public const string TD_Dwarf           = "Art/TinyDungeon/tile_0090";
    public const string TD_Elf             = "Art/TinyDungeon/tile_0091";
    public const string TD_Viking          = "Art/TinyDungeon/tile_0092";
    public const string TD_King            = "Art/TinyDungeon/tile_0093";
    public const string TD_Queen           = "Art/TinyDungeon/tile_0094";
    public const string TD_Peasant         = "Art/TinyDungeon/tile_0095";

    // Row 8: More characters
    public const string TD_Skeleton        = "Art/TinyDungeon/tile_0096";
    public const string TD_Goblin          = "Art/TinyDungeon/tile_0097";
    public const string TD_Orc             = "Art/TinyDungeon/tile_0098";
    public const string TD_Demon           = "Art/TinyDungeon/tile_0099";
    public const string TD_Ghost           = "Art/TinyDungeon/tile_0100";
    public const string TD_Slime           = "Art/TinyDungeon/tile_0101";
    public const string TD_Bat             = "Art/TinyDungeon/tile_0102";
    public const string TD_Spider          = "Art/TinyDungeon/tile_0103";
    public const string TD_Rat             = "Art/TinyDungeon/tile_0104";
    public const string TD_Snake           = "Art/TinyDungeon/tile_0105";
    public const string TD_Witch           = "Art/TinyDungeon/tile_0106";
    public const string TD_Necromancer     = "Art/TinyDungeon/tile_0107";

    // Row 9-10: Items, weapons, shields
    public const string TD_Sword           = "Art/TinyDungeon/tile_0108";
    public const string TD_Axe             = "Art/TinyDungeon/tile_0109";
    public const string TD_Bow             = "Art/TinyDungeon/tile_0110";
    public const string TD_Staff           = "Art/TinyDungeon/tile_0111";
    public const string TD_Shield          = "Art/TinyDungeon/tile_0112";
    public const string TD_Helmet          = "Art/TinyDungeon/tile_0113";
    public const string TD_Armor           = "Art/TinyDungeon/tile_0114";
    public const string TD_PotionRed       = "Art/TinyDungeon/tile_0115";
    public const string TD_PotionBlue      = "Art/TinyDungeon/tile_0116";
    public const string TD_PotionGreen     = "Art/TinyDungeon/tile_0117";
    public const string TD_Key             = "Art/TinyDungeon/tile_0118";
    public const string TD_Coin            = "Art/TinyDungeon/tile_0119";
    public const string TD_Gem             = "Art/TinyDungeon/tile_0120";
    public const string TD_Ring            = "Art/TinyDungeon/tile_0121";
    public const string TD_Scroll          = "Art/TinyDungeon/tile_0122";
    public const string TD_Book            = "Art/TinyDungeon/tile_0123";

    // Dungeon environment tiles (rows 0-5)
    public const string TD_FloorStone      = "Art/TinyDungeon/tile_0000";
    public const string TD_FloorStoneAlt   = "Art/TinyDungeon/tile_0001";
    public const string TD_FloorDirt       = "Art/TinyDungeon/tile_0002";
    public const string TD_WallTop         = "Art/TinyDungeon/tile_0012";
    public const string TD_WallMid         = "Art/TinyDungeon/tile_0024";
    public const string TD_WallBottom      = "Art/TinyDungeon/tile_0036";
    public const string TD_DoorClosed      = "Art/TinyDungeon/tile_0038";
    public const string TD_DoorOpen        = "Art/TinyDungeon/tile_0039";
    public const string TD_ChestClosed     = "Art/TinyDungeon/tile_0044";
    public const string TD_ChestOpen       = "Art/TinyDungeon/tile_0045";
    public const string TD_Torch           = "Art/TinyDungeon/tile_0046";
    public const string TD_Barrel          = "Art/TinyDungeon/tile_0048";
    public const string TD_Crate           = "Art/TinyDungeon/tile_0049";
    public const string TD_Table           = "Art/TinyDungeon/tile_0050";
    public const string TD_Chair           = "Art/TinyDungeon/tile_0051";
    public const string TD_Bookshelf       = "Art/TinyDungeon/tile_0052";
    public const string TD_Ladder          = "Art/TinyDungeon/tile_0053";

    // ---------------------------------------------------------------
    //  HELPER: Load a sprite by resource path
    // ---------------------------------------------------------------

    public static Sprite Load(string resourcePath)
    {
        var sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite == null)
            Debug.LogWarning($"[TinyTileset] Sprite not found: {resourcePath}");
        return sprite;
    }
}
