using UnityEngine;

/// <summary>
/// Verified tile index reference for Kenney Tiny Town + Tiny Dungeon.
/// Each pack: 12 cols × 11 rows = 132 tiles (16x16 pixel art).
/// Indices verified visually from tilemap_packed.png 2026-04-13.
/// </summary>
public static class TinyTileset
{
    // ═══════════════════════════════════════════════════════════
    //  TINY TOWN (TT) — bright outdoor village tiles
    // ═══════════════════════════════════════════════════════════

    // ─── Ground (full 16x16, opaque) ───
    public const string TT_Grass1     = "Art/TinyTown/tile_0000"; // bright green grass
    public const string TT_Grass2     = "Art/TinyTown/tile_0001"; // grass variant
    public const string TT_Grass3     = "Art/TinyTown/tile_0002"; // grass variant (slightly yellow)

    // ─── Dirt/Sand (full, opaque) ───
    public const string TT_DirtTL     = "Art/TinyTown/tile_0024";
    public const string TT_DirtTC     = "Art/TinyTown/tile_0025";
    public const string TT_DirtTR     = "Art/TinyTown/tile_0026";
    public const string TT_DirtBL     = "Art/TinyTown/tile_0036";
    public const string TT_DirtBC     = "Art/TinyTown/tile_0037";
    public const string TT_DirtBR     = "Art/TinyTown/tile_0038";

    // ─── Path/Stone (full, opaque) ───
    public const string TT_PathLight  = "Art/TinyTown/tile_0040";
    public const string TT_PathEdge   = "Art/TinyTown/tile_0041";
    public const string TT_PathDark   = "Art/TinyTown/tile_0042";
    public const string TT_PathGreen  = "Art/TinyTown/tile_0043";

    // ─── Trees GREEN (use these!) ───
    public const string TT_TreeRound  = "Art/TinyTown/tile_0005"; // ★ BEST — round green tree
    public const string TT_TreeRound2 = "Art/TinyTown/tile_0006"; // round green variant
    public const string TT_TreeBush   = "Art/TinyTown/tile_0016"; // ★ green bush/small tree
    public const string TT_TreeDouble = "Art/TinyTown/tile_0017"; // two small trees
    public const string TT_BushSmall  = "Art/TinyTown/tile_0028"; // ★ small green bush
    public const string TT_PlantSmall = "Art/TinyTown/tile_0030"; // small green plant
    // AVOID: tile 4 (spiky dark pine), 7/8 (pine halves)

    // ─── Trees AUTUMN (orange — use sparingly) ───
    public const string TT_AutumnA    = "Art/TinyTown/tile_0009";
    public const string TT_AutumnB    = "Art/TinyTown/tile_0015";

    // ─── Decorations ───
    public const string TT_Mushroom   = "Art/TinyTown/tile_0029"; // red mushroom

    // ─── Castle Walls (blue-gray, opaque) ───
    public const string TT_WallTL     = "Art/TinyTown/tile_0048";
    public const string TT_WallTC     = "Art/TinyTown/tile_0049";
    public const string TT_WallTR     = "Art/TinyTown/tile_0050";
    public const string TT_WallH      = "Art/TinyTown/tile_0051";
    public const string TT_WallML     = "Art/TinyTown/tile_0060";
    public const string TT_WallBL     = "Art/TinyTown/tile_0061";
    public const string TT_WallBC     = "Art/TinyTown/tile_0062";
    public const string TT_WallBR     = "Art/TinyTown/tile_0063";

    // ─── Red Roofs (row 4) ───
    public const string TT_RoofRedL   = "Art/TinyTown/tile_0052";
    public const string TT_RoofRedC   = "Art/TinyTown/tile_0053";
    public const string TT_RoofRedR   = "Art/TinyTown/tile_0054";

    // ─── Blue/Gray Roofs (row 5) ───
    public const string TT_RoofBlueL  = "Art/TinyTown/tile_0064";
    public const string TT_RoofBlueC  = "Art/TinyTown/tile_0065";
    public const string TT_RoofBlueR  = "Art/TinyTown/tile_0066";

    // ─── Wood House Walls (row 6, brown/orange) ───
    public const string TT_HouseWallL = "Art/TinyTown/tile_0072";
    public const string TT_HouseWallC = "Art/TinyTown/tile_0073"; // has door
    public const string TT_HouseWallR = "Art/TinyTown/tile_0074";

    // ─── Stone House Walls (row 6, blue-gray) ───
    public const string TT_StoneWallL = "Art/TinyTown/tile_0076";
    public const string TT_StoneWallC = "Art/TinyTown/tile_0077";
    public const string TT_StoneWallR = "Art/TinyTown/tile_0078";

    // ─── Props ───
    public const string TT_Barrel     = "Art/TinyTown/tile_0044";
    public const string TT_Crate      = "Art/TinyTown/tile_0045";
    public const string TT_Sign       = "Art/TinyTown/tile_0082";
    public const string TT_Stone      = "Art/TinyTown/tile_0093";
    public const string TT_Coin       = "Art/TinyTown/tile_0094";

    // ─── Castle Gate ───
    public const string TT_GateFloor  = "Art/TinyTown/tile_0109";
    public const string TT_GateArchTL = "Art/TinyTown/tile_0111";
    public const string TT_GateArchTR = "Art/TinyTown/tile_0112";
    public const string TT_GateArchBL = "Art/TinyTown/tile_0123";
    public const string TT_GateArchBR = "Art/TinyTown/tile_0124";
    public const string TT_GateOpen   = "Art/TinyTown/tile_0125";

    // ─── Water ───
    public const string TT_WaterEdge  = "Art/TinyTown/tile_0012";
    public const string TT_WaterFull  = "Art/TinyTown/tile_0013";

    // ═══════════════════════════════════════════════════════════
    //  TINY DUNGEON (TD) — characters + props
    // ═══════════════════════════════════════════════════════════

    // ─── Characters (rows 7-10) ───
    public const string TD_Wizard     = "Art/TinyDungeon/tile_0084"; // purple wizard
    public const string TD_Knight     = "Art/TinyDungeon/tile_0085"; // armored knight
    public const string TD_Warrior    = "Art/TinyDungeon/tile_0086"; // blonde warrior
    public const string TD_Archer     = "Art/TinyDungeon/tile_0087"; // ranger
    public const string TD_Fighter    = "Art/TinyDungeon/tile_0088"; // brown fighter
    public const string TD_Villager   = "Art/TinyDungeon/tile_0089"; // NPC villager
    public const string TD_Peasant    = "Art/TinyDungeon/tile_0090"; // peasant
    public const string TD_Guard      = "Art/TinyDungeon/tile_0091"; // guard
    public const string TD_RedHair    = "Art/TinyDungeon/tile_0096"; // red-haired
    public const string TD_DarkHair   = "Art/TinyDungeon/tile_0097"; // dark-haired female
    public const string TD_BrownHair  = "Art/TinyDungeon/tile_0098"; // brown-haired
    public const string TD_Viking     = "Art/TinyDungeon/tile_0099"; // viking
    public const string TD_Noble      = "Art/TinyDungeon/tile_0100"; // noble
    public const string TD_Ghost      = "Art/TinyDungeon/tile_0108"; // ghost (green)
    public const string TD_Bard       = "Art/TinyDungeon/tile_0109"; // bard
    public const string TD_Rogue      = "Art/TinyDungeon/tile_0110"; // red rogue
    public const string TD_Elf        = "Art/TinyDungeon/tile_0111"; // elf

    // ─── Props ───
    public const string TD_Chest      = "Art/TinyDungeon/tile_0066";
    public const string TD_Barrel     = "Art/TinyDungeon/tile_0063";
    public const string TD_Sword      = "Art/TinyDungeon/tile_0113";
    public const string TD_Potion     = "Art/TinyDungeon/tile_0115";
    public const string TD_Key        = "Art/TinyDungeon/tile_0117";

    // ═══════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════

    public static string TT(int index) => $"Art/TinyTown/tile_{index:D4}";
    public static string TD(int index) => $"Art/TinyDungeon/tile_{index:D4}";

    // ─── BUILDING RECIPES ───
    // Red roof house: roof 52,53,54 on top + walls 72,73,74 on bottom
    // Blue roof house: roof 64,65,66 on top + walls 76,77,78 on bottom
    // Castle wall box: TL=48, TC=49, TR=50 / ML=60, BL=61, BC=62, BR=63
}
