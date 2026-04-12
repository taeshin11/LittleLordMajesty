using UnityEngine;

/// <summary>
/// Static tile index mapping for Kenney Tiny Town + Tiny Dungeon packs.
/// Both tilemaps are 12 columns x 11 rows, 16x16 pixels each.
/// Index = row * 12 + col, tile files are tile_NNNN.png.
/// Resource path: Art/TinyTown/tile_NNNN or Art/TinyDungeon/tile_NNNN
///
/// INDICES VERIFIED against annotated tilemap images 2026-04-12.
/// </summary>
public static class TinyTileset
{
    // ---------------------------------------------------------------
    //  TINY TOWN — Overworld / Village tiles
    //  Resource prefix: "Art/TinyTown/"
    // ---------------------------------------------------------------

    // --- Ground (row 0, indices 0-3) ---
    public const string TT_Grass           = "Art/TinyTown/tile_0000"; // solid bright green
    public const string TT_GrassAlt1       = "Art/TinyTown/tile_0001"; // grass + small detail
    public const string TT_GrassAlt2       = "Art/TinyTown/tile_0002"; // grass variant
    public const string TT_GrassFlower     = "Art/TinyTown/tile_0003"; // yellow flower/star

    // --- Trees (indices 4-11) ---
    public const string TT_TreeSmall       = "Art/TinyTown/tile_0004"; // round green tree (small)
    public const string TT_TreeMedL        = "Art/TinyTown/tile_0005"; // round green tree (medium, left)
    public const string TT_TreeMedR        = "Art/TinyTown/tile_0006"; // round green tree (medium, right)
    public const string TT_PineL           = "Art/TinyTown/tile_0007"; // tall pine (left)
    public const string TT_PineR           = "Art/TinyTown/tile_0008"; // tall pine (right)
    public const string TT_AutumnSmall     = "Art/TinyTown/tile_0009"; // autumn tree (orange, small)
    public const string TT_AutumnPair      = "Art/TinyTown/tile_0010"; // autumn tree pair
    public const string TT_AutumnTall      = "Art/TinyTown/tile_0011"; // autumn tree (tall)

    // --- More nature (row 1) ---
    public const string TT_BushSmall       = "Art/TinyTown/tile_0015"; // small bush
    public const string TT_TreesCluster    = "Art/TinyTown/tile_0016"; // small trees cluster

    // --- Dirt/Path (rows 2-3) ---
    public const string TT_DirtTL          = "Art/TinyTown/tile_0024"; // dirt ground top-left corner
    public const string TT_DirtCenter      = "Art/TinyTown/tile_0025"; // dirt ground center
    public const string TT_DirtTR          = "Art/TinyTown/tile_0026"; // dirt ground top-right corner
    public const string TT_BushShrub       = "Art/TinyTown/tile_0028"; // small bush/shrub
    public const string TT_Mushroom        = "Art/TinyTown/tile_0029"; // red mushroom
    public const string TT_SmallPlant      = "Art/TinyTown/tile_0030"; // small plant
    public const string TT_DirtBL          = "Art/TinyTown/tile_0036"; // dirt bottom-left
    public const string TT_DirtBC          = "Art/TinyTown/tile_0037"; // dirt bottom-center
    public const string TT_DirtBR          = "Art/TinyTown/tile_0038"; // dirt bottom-right
    public const string TT_StonePath       = "Art/TinyTown/tile_0040"; // stone path (light)
    public const string TT_GrassStone      = "Art/TinyTown/tile_0041"; // grass-stone transition
    public const string TT_Cobblestone     = "Art/TinyTown/tile_0042"; // cobblestone path
    public const string TT_GreenPath       = "Art/TinyTown/tile_0043"; // green path tile

    // --- Props (scattered indices) ---
    public const string TT_Barrel          = "Art/TinyTown/tile_0045"; // chest/barrel
    public const string TT_Table           = "Art/TinyTown/tile_0046"; // table/wood surface

    // --- Castle walls — gray stone (rows 4-5) ---
    public const string TT_StoneWallTL     = "Art/TinyTown/tile_0048"; // stone wall top-left
    public const string TT_StoneWallTop    = "Art/TinyTown/tile_0049"; // stone wall top
    public const string TT_StoneWallTR     = "Art/TinyTown/tile_0050"; // stone wall top-right
    public const string TT_StoneWallH      = "Art/TinyTown/tile_0051"; // stone wall horizontal
    public const string TT_StonePattern1   = "Art/TinyTown/tile_0052"; // stone pattern
    public const string TT_StonePattern2   = "Art/TinyTown/tile_0053"; // stone pattern
    public const string TT_StonePattern3   = "Art/TinyTown/tile_0054"; // stone pattern
    public const string TT_StonePattern4   = "Art/TinyTown/tile_0055"; // stone pattern
    public const string TT_StoneWallL      = "Art/TinyTown/tile_0060"; // stone wall left
    public const string TT_StoneWallBL     = "Art/TinyTown/tile_0061"; // stone wall bottom-left
    public const string TT_StoneWallBot    = "Art/TinyTown/tile_0062"; // stone wall bottom
    public const string TT_StoneWallBR     = "Art/TinyTown/tile_0063"; // stone wall bottom-right

    // --- Houses — wood + colored roofs (rows 5-6) ---
    public const string TT_RedRoofL        = "Art/TinyTown/tile_0064"; // red roof left
    public const string TT_RedRoofC        = "Art/TinyTown/tile_0065"; // red roof center
    public const string TT_RedRoofR        = "Art/TinyTown/tile_0066"; // red roof right
    public const string TT_BlueRoofL       = "Art/TinyTown/tile_0067"; // blue/gray roof left
    public const string TT_BlueRoofC       = "Art/TinyTown/tile_0068"; // blue/gray roof center
    public const string TT_WoodWallWinL    = "Art/TinyTown/tile_0072"; // wood wall left (window)
    public const string TT_WoodWallDoor    = "Art/TinyTown/tile_0073"; // wood wall center (door)
    public const string TT_WoodWallR       = "Art/TinyTown/tile_0074"; // wood wall right
    public const string TT_WoodWallL2      = "Art/TinyTown/tile_0075"; // wood wall left
    public const string TT_WoodWallOpen    = "Art/TinyTown/tile_0076"; // wood wall (open)
    public const string TT_WoodWallC2      = "Art/TinyTown/tile_0077"; // wood wall center
    public const string TT_WoodWallR2      = "Art/TinyTown/tile_0078"; // wood wall right
    public const string TT_StoneWallBlue   = "Art/TinyTown/tile_0079"; // stone wall (blue roof)

    // --- More props ---
    public const string TT_BeamH           = "Art/TinyTown/tile_0080"; // horizontal beam
    public const string TT_BeamLong        = "Art/TinyTown/tile_0081"; // long beam
    public const string TT_SignPost        = "Art/TinyTown/tile_0082"; // sign post
    public const string TT_SmallStone      = "Art/TinyTown/tile_0093"; // small stone
    public const string TT_KeyCoin         = "Art/TinyTown/tile_0105"; // key/coin

    // --- Castle gate (rows 9-10) ---
    public const string TT_CastleFloor     = "Art/TinyTown/tile_0108"; // castle floor (gray)
    public const string TT_CastleFloor2    = "Art/TinyTown/tile_0109"; // castle floor
    public const string TT_CastleFloor3    = "Art/TinyTown/tile_0110"; // castle floor
    public const string TT_GateArchTL      = "Art/TinyTown/tile_0112"; // gate arch (top-left)
    public const string TT_GateArchTR      = "Art/TinyTown/tile_0113"; // gate arch (top-right)
    public const string TT_GateArchBL      = "Art/TinyTown/tile_0114"; // gate arch (bottom-left)
    public const string TT_GateArchBR      = "Art/TinyTown/tile_0115"; // gate arch (bottom-right)
    public const string TT_Well            = "Art/TinyTown/tile_0116"; // well
    public const string TT_Tool            = "Art/TinyTown/tile_0117"; // tool
    public const string TT_GateBaseL       = "Art/TinyTown/tile_0124"; // gate base left
    public const string TT_GateBaseR       = "Art/TinyTown/tile_0125"; // gate base right
    public const string TT_GateOpenL       = "Art/TinyTown/tile_0126"; // gate opening left
    public const string TT_GateOpenR       = "Art/TinyTown/tile_0127"; // gate opening right

    // ---------------------------------------------------------------
    //  TINY DUNGEON — Characters
    //  Resource prefix: "Art/TinyDungeon/"
    // ---------------------------------------------------------------

    // Row 7: characters (indices 84-87)
    public const string TD_Wizard          = "Art/TinyDungeon/tile_0084"; // wizard (purple hat)
    public const string TD_Knight          = "Art/TinyDungeon/tile_0085"; // warrior/knight (helmet)
    public const string TD_Blonde          = "Art/TinyDungeon/tile_0086"; // blonde character
    public const string TD_Archer          = "Art/TinyDungeon/tile_0087"; // archer/ranger

    // Row 8: characters (indices 96-99)
    public const string TD_RedHair         = "Art/TinyDungeon/tile_0096"; // red-hair character
    public const string TD_DarkFemale      = "Art/TinyDungeon/tile_0097"; // dark-hair female
    public const string TD_BrownHair       = "Art/TinyDungeon/tile_0098"; // brown-hair character
    public const string TD_LightChar       = "Art/TinyDungeon/tile_0099"; // light character

    // Row 9: characters (indices 108-111)
    public const string TD_Ghost           = "Art/TinyDungeon/tile_0108"; // ghost/special
    public const string TD_GreenCreature   = "Art/TinyDungeon/tile_0109"; // green creature
    public const string TD_RedFemale       = "Art/TinyDungeon/tile_0110"; // red-hair female
    public const string TD_Bard            = "Art/TinyDungeon/tile_0111"; // bard/special

    // Dungeon environment tiles (kept for compatibility)
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

    /// <summary>
    /// Convenience: build a TinyTown resource path from a tile index.
    /// TT(64) => "Art/TinyTown/tile_0064"
    /// </summary>
    public static string TT(int index) => $"Art/TinyTown/tile_{index:D4}";

    /// <summary>
    /// Convenience: build a TinyDungeon resource path from a tile index.
    /// TD(84) => "Art/TinyDungeon/tile_0084"
    /// </summary>
    public static string TD(int index) => $"Art/TinyDungeon/tile_{index:D4}";
}
