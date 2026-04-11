using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// M16 roaming pivot — finds the nearest interactable NPC in a sphere around
/// the player and surfaces an "E to Talk" prompt over that NPC's head.
///
/// Ticks at 10 Hz (every 0.1 s) — enough to feel responsive, cheap enough
/// that we don't burn frames on physics queries. Uses Physics.OverlapSphere
/// against a configurable "NPC" layer mask so we never include walls,
/// buildings, or the player itself.
///
/// Pressing the interact key while a target is highlighted routes to
/// DialogueBoxUI (if present) to open the NPC conversation. If no dialogue
/// UI is wired yet, we fire GameManager.SetGameState(Dialogue) so the
/// existing state graph at least blocks input — lets us develop the finder
/// and the dialogue box in separate commits without temporarily breaking
/// the player controller.
/// </summary>
public class InteractionFinder : MonoBehaviour
{
    [Header("Search")]
    [SerializeField] private float _searchRadius = 2.2f;
    [Tooltip("Seconds between proximity ticks. 0.1 = 10 Hz.")]
    [SerializeField] private float _tickInterval = 0.1f;

    [Header("Input")]
    [SerializeField] private KeyCode _interactKey = KeyCode.E;

    private InteractPromptUI _currentPrompt;
    private NPCIdentity      _currentTarget;
    private float            _nextTickTime;

    /// <summary>
    /// Process-wide registry maintained by NPCIdentity.OnEnable/OnDisable.
    /// Avoids any dependency on Unity physics layers (they're a pain to
    /// configure at runtime) and keeps the proximity tick free of
    /// GetComponent calls — we can iterate a live list of ~20 NPCs cheaply.
    /// </summary>
    public static readonly List<NPCIdentity> RegisteredNPCs = new();

    private void Update()
    {
        if (Time.time >= _nextTickTime)
        {
            _nextTickTime = Time.time + _tickInterval;
            RefreshTarget();
        }

        if (_currentTarget != null && Input.GetKeyDown(_interactKey))
            TriggerInteract();
    }

    private void RefreshTarget()
    {
        float bestDistSq = _searchRadius * _searchRadius;
        NPCIdentity best = null;
        for (int i = 0; i < RegisteredNPCs.Count; i++)
        {
            var id = RegisteredNPCs[i];
            if (id == null) continue;
            float d2 = (id.transform.position - transform.position).sqrMagnitude;
            if (d2 < bestDistSq)
            {
                bestDistSq = d2;
                best = id;
            }
        }

        if (best == _currentTarget) return;

        // Target changed: hide old prompt, show new one.
        if (_currentPrompt != null) _currentPrompt.Hide();
        _currentTarget = best;
        if (_currentTarget != null)
        {
            _currentPrompt = _currentTarget.GetComponentInChildren<InteractPromptUI>(true);
            if (_currentPrompt != null) _currentPrompt.Show(_currentTarget.DisplayName);
        }
        else _currentPrompt = null;
    }

    private void TriggerInteract()
    {
        string npcId = _currentTarget.NpcId;
        // Prefer the dialogue box UI if it's present in the scene, otherwise
        // fall back to the legacy NPCInteractionUI (still alive until M16-06
        // ships its replacement). Either path ends in the same Gemini flow.
        var box = DialogueBoxUI.Instance;
        if (box != null) box.Open(npcId);
        else
        {
            var legacy = FindFirstObjectByType<NPCInteractionUI>(FindObjectsInactive.Include);
            if (legacy != null) legacy.OpenForNPC(npcId);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.25f);
        Gizmos.DrawSphere(transform.position, _searchRadius);
    }
}
