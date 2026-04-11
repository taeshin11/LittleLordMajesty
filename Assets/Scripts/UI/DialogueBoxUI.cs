using UnityEngine;

/// <summary>
/// M16-06 placeholder: bottom-of-screen RPG dialogue box will replace the
/// fullscreen card chat (NPCInteractionUI). This stub only establishes the
/// singleton pattern so InteractionFinder can route to `DialogueBoxUI.Instance`
/// today; if no instance is wired yet, the finder falls back to the legacy
/// NPCInteractionUI so gameplay still works during the incremental pivot.
///
/// Real implementation lives in research_history/milestone_16_roaming_pivot_plan.md
/// step 6 — portrait panel on the left, name + typewriter text on the right,
/// 4 QuickAction buttons + free-text input along the bottom, typewriter,
/// input lock, Korean-safe TMP font stack.
/// </summary>
public class DialogueBoxUI : MonoBehaviour
{
    public static DialogueBoxUI Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>
    /// Open the dialogue box for an NPC id. Until M16-06 builds out the real
    /// UI, this delegates to the legacy NPCInteractionUI — the one currently
    /// visible on the deploy as a fullscreen chat scroll. Same Gemini flow,
    /// same LocalDialogueBank greeting, just the old card layout.
    /// </summary>
    public void Open(string npcId)
    {
        var legacy = FindFirstObjectByType<NPCInteractionUI>(FindObjectsInactive.Include);
        if (legacy != null) legacy.OpenForNPC(npcId);
        else Debug.LogWarning("[DialogueBoxUI] No fallback NPCInteractionUI in scene; dialogue skipped");
    }
}
