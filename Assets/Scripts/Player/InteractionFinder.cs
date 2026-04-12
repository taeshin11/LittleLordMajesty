using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Finds the nearest interactable NPC in a radius around the player
/// and surfaces an "E to Talk" prompt. Distance check on XY plane
/// (2D top-down world).
///
/// Ticks at 10 Hz (every 0.1 s). Uses the static RegisteredNPCs list
/// maintained by NPCIdentity.OnEnable/OnDisable.
/// </summary>
public class InteractionFinder : MonoBehaviour
{
    [Header("Search")]
    [SerializeField] private float _searchRadius = 3f;
    [Tooltip("Seconds between proximity ticks. 0.1 = 10 Hz.")]
    [SerializeField] private float _tickInterval = 0.1f;

    [Header("Input")]
    [SerializeField] private KeyCode _interactKey = KeyCode.E;

    private InteractPromptUI _currentPrompt;
    private NPCIdentity      _currentTarget;
    private float            _nextTickTime;

    /// <summary>
    /// Process-wide registry maintained by NPCIdentity.OnEnable/OnDisable.
    /// </summary>
    public static readonly List<NPCIdentity> RegisteredNPCs = new();

    private void Update()
    {
        if (Time.time >= _nextTickTime)
        {
            _nextTickTime = Time.time + _tickInterval;
            RefreshTarget();
        }

        if (_currentTarget != null)
        {
            if (Input.GetKeyDown(_interactKey))
                TriggerInteract();
            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began &&
                    touch.position.x > Screen.width * 0.4f)
                    TriggerInteract();
            }
        }
    }

    private void RefreshTarget()
    {
        float bestDistSq = _searchRadius * _searchRadius;
        NPCIdentity best = null;

        Vector3 myPos = transform.position;

        for (int i = 0; i < RegisteredNPCs.Count; i++)
        {
            var id = RegisteredNPCs[i];
            if (id == null) continue;

            // XY plane distance (2D isometric world)
            Vector3 delta = id.transform.position - myPos;
            float d2 = delta.x * delta.x + delta.y * delta.y;

            if (d2 < bestDistSq)
            {
                bestDistSq = d2;
                best = id;
            }
        }

        if (best == _currentTarget) return;

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
