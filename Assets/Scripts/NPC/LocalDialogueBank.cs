using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

/// <summary>
/// Loads pre-generated NPC dialogue lines from
/// Assets/Resources/Dialogue/dialogue_lines.json (built offline by
/// tools/dialogue_gen/generate.py against a local EXAONE 3.5 model).
///
/// Used as a zero-cost local fallback (or default opener) so the game
/// doesn't have to hit Gemini for every NPC tap.
/// </summary>
public static class LocalDialogueBank
{
    public enum Context { Greeting, Idle, Accept, Refuse, GoodNews, BadNews }

    private static Dictionary<string, Dictionary<string, List<string>>> _bank;
    private static bool _loaded;

    private static readonly System.Random _rng = new System.Random();

    private static readonly Dictionary<NPCPersona.NPCProfession, string> _professionToRole = new()
    {
        { NPCPersona.NPCProfession.Vassal,   "vassal"   },
        { NPCPersona.NPCProfession.Soldier,  "soldier"  },
        { NPCPersona.NPCProfession.Farmer,   "farmer"   },
        { NPCPersona.NPCProfession.Merchant, "merchant" },
    };

    private static readonly Dictionary<Context, string> _contextKeys = new()
    {
        { Context.Greeting, "greeting"  },
        { Context.Idle,     "idle"      },
        { Context.Accept,   "accept"    },
        { Context.Refuse,   "refuse"    },
        { Context.GoodNews, "good_news" },
        { Context.BadNews,  "bad_news"  },
    };

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        var asset = Resources.Load<TextAsset>("Dialogue/dialogue_lines");
        if (asset == null)
        {
            Debug.LogWarning("[LocalDialogueBank] dialogue_lines.json not found in Resources/Dialogue/. " +
                             "Run tools/dialogue_gen/generate.py to populate it.");
            return;
        }
        try
        {
            _bank = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, List<string>>>>(asset.text);
            int total = 0;
            if (_bank != null)
                foreach (var role in _bank.Values)
                    foreach (var slot in role.Values)
                        total += slot.Count;
            Debug.Log($"[LocalDialogueBank] Loaded {total} pre-generated lines for {(_bank?.Count ?? 0)} roles");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LocalDialogueBank] Parse failed: {e.Message}");
            _bank = null;
        }
    }

    /// <summary>
    /// Returns a random line for the given profession + context, or null if
    /// no lines are available (caller should fall back to Gemini or a
    /// hard-coded English string).
    /// </summary>
    public static string GetRandom(NPCPersona.NPCProfession profession, Context context)
    {
        EnsureLoaded();
        if (_bank == null) return null;
        if (!_professionToRole.TryGetValue(profession, out string roleId)) return null;
        if (!_bank.TryGetValue(roleId, out var slots)) return null;
        if (!_contextKeys.TryGetValue(context, out string ctxKey)) return null;
        if (!slots.TryGetValue(ctxKey, out var lines) || lines.Count == 0) return null;
        return lines[_rng.Next(lines.Count)];
    }

    /// <summary>True iff the bank has at least one line for the given slot.</summary>
    public static bool HasLines(NPCPersona.NPCProfession profession, Context context)
    {
        EnsureLoaded();
        if (_bank == null) return false;
        return _professionToRole.TryGetValue(profession, out var roleId)
            && _bank.TryGetValue(roleId, out var slots)
            && _contextKeys.TryGetValue(context, out var ctxKey)
            && slots.TryGetValue(ctxKey, out var lines)
            && lines.Count > 0;
    }
}
