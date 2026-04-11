using UnityEngine;

/// <summary>
/// M16 roaming pivot — tiny identity component on each world NPC.
///
/// InteractionFinder uses this to resolve the NPC id and display name
/// without pulling on NPCManager during its 10 Hz tick. Sits on the same
/// GameObject as NPCDailyRoutine + NPCBillboard.
/// </summary>
public class NPCIdentity : MonoBehaviour
{
    [SerializeField] private string _npcId;
    [SerializeField] private string _displayName;

    public string NpcId       => _npcId;
    public string DisplayName => _displayName;

    public void SetIdentity(string id, string displayName)
    {
        _npcId = id;
        _displayName = displayName;
    }

    // Self-register so InteractionFinder doesn't have to iterate all scene
    // GameObjects or configure a physics layer mask. NPCs already are a
    // small set (~20 in castle view) — a simple list scan at 10 Hz is
    // cheaper than layer bookkeeping.
    private void OnEnable()
    {
        if (!InteractionFinder.RegisteredNPCs.Contains(this))
            InteractionFinder.RegisteredNPCs.Add(this);
    }

    private void OnDisable()
    {
        InteractionFinder.RegisteredNPCs.Remove(this);
    }
}
