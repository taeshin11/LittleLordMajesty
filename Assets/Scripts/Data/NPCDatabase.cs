using UnityEngine;

/// <summary>
/// ScriptableObject database for NPC personas.
/// Reference this from NPCManager to spawn NPCs with predefined personalities.
/// Create via: Assets/Create/LLM/NPC Database
/// </summary>
[CreateAssetMenu(fileName = "NPCDatabase", menuName = "LLM/NPC Database")]
public class NPCDatabase : ScriptableObject
{
    public NPCPersona[] Personas;

    public NPCPersona FindByName(string name)
    {
        foreach (var p in Personas)
            if (p != null && p.PersonaName == name) return p;
        return null;
    }

    public NPCPersona FindByProfession(NPCPersona.NPCProfession profession)
    {
        foreach (var p in Personas)
            if (p != null && p.Profession == profession) return p;
        return null;
    }
}
