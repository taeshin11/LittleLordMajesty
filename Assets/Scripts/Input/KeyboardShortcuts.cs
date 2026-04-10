using UnityEngine;

/// <summary>
/// PC/Desktop keyboard shortcuts.
/// Only active on non-mobile platforms.
/// Attach to the same persistent object as InputHandler.
/// </summary>
public class KeyboardShortcuts : MonoBehaviour
{
#if !UNITY_ANDROID && !UNITY_IOS

    private void Update()
    {
        if (GameManager.Instance == null) return;
        var state = GameManager.Instance.CurrentState;

        // ── Global ────────────────────────────────────────────────
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (state == GameManager.GameState.Dialogue)
            {
                UIManager.Instance?.CloseDialogue();
                return;
            }
            GameManager.Instance?.TogglePause();
        }

        if (Input.GetKeyDown(KeyCode.F5))
            GameManager.Instance?.SaveGame();

        if (Input.GetKeyDown(KeyCode.F9))
            GameManager.Instance?.LoadGame();

        // ── Castle view shortcuts ──────────────────────────────────
        if (state == GameManager.GameState.Castle)
        {
            // B = Open build menu
            if (Input.GetKeyDown(KeyCode.B))
            {
                var castleUI = FindObjectOfType<CastleViewUI>();
                castleUI?.ToggleBuildingMenuFromKeyboard();
            }

            // M = World Map
            if (Input.GetKeyDown(KeyCode.M))
                GameManager.Instance?.SetGameState(GameManager.GameState.WorldMap);

            // N = NPC list
            if (Input.GetKeyDown(KeyCode.N))
            {
                var castleUI = FindObjectOfType<CastleViewUI>();
                castleUI?.ToggleNPCListFromKeyboard();
            }

            // Tab = Cycle through NPCs
            if (Input.GetKeyDown(KeyCode.Tab))
                CycleToNextNPC();
        }

        // ── World Map shortcuts ────────────────────────────────────
        if (state == GameManager.GameState.WorldMap)
        {
            if (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.Backspace))
                GameManager.Instance?.SetGameState(GameManager.GameState.Castle);
        }

        // ── Window mode toggle (PC only) ───────────────────────────
        if (Input.GetKeyDown(KeyCode.F11))
            Screen.fullScreen = !Screen.fullScreen;

        // ── Debug console ─────────────────────────────────────────
        if (Input.GetKeyDown(KeyCode.BackQuote))
            DebugConsole.Instance?.Toggle();
    }

    private void CycleToNextNPC()
    {
        var npcs = NPCManager.Instance?.GetAllNPCs();
        if (npcs == null || npcs.Count == 0) return;
        // Open the rich NPC chat panel via NPCInteractionUI (single entry point).
        // Do NOT also call UIManager.OpenDialogue — that is the separate 3D-click path
        // and calling both would cause commands to double-fire.
        var interactionUI = FindFirstObjectByType<NPCInteractionUI>(FindObjectsInactive.Include);
        interactionUI?.OpenForNPC(npcs[0].Id);
    }

#endif
}
