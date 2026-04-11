using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 스타듀밸리 스타일 NPC 일과 시스템.
/// NPC들이 시간에 따라 성 안을 돌아다니며 실제로 일을 함.
/// 밭을 갈거나, 대장간에서 망치질하거나, 빵집 앞을 서성이거나.
/// 영주가 언제든 말을 걸 수 있음 — 위치가 말해줌 ("빵집에서 만났구나")
/// </summary>
public class NPCDailyRoutine : MonoBehaviour
{
    [Serializable]
    public class RoutineStop
    {
        public string LocationName;      // "밀밭", "대장간", "광장"
        public Vector3 WorldPosition;    // 이동할 3D 위치
        public int     ArriveAtHour;     // 도착 시간 (0-23)
        // Internal metadata only — not currently read by player-visible UI.
        // When this feature is wired to a player-facing location/activity hint,
        // convert to ActivityDescKey and resolve via LocalizationManager.Get().
        // Keys already exist in en.json/ko.json under "activity_*" (see Resources/Localization).
        public string  ActivityDesc;     // Gemini가 대화에 활용하는 현재 활동 설명
        public string  Animation;        // "harvest", "hammer", "idle_chat" 등
        public bool    CanBeInterrupted; // 영주가 말 걸 수 있는지
    }

    [Header("NPC Info")]
    public string NpcId;

    [Header("Daily Schedule")]
    public List<RoutineStop> DailySchedule = new();

    [Header("Movement")]
    public float MoveSpeed = 2.5f;

    private RoutineStop _currentStop;
    private int         _currentStopIndex;
    private bool        _isMoving;
    private int         _currentHour = 6; // 게임 내 시간

    public event Action<RoutineStop> OnArrivedAtLocation;
    public event Action<string>      OnActivityStarted;

    // ─────────────────────────────────────────────────────────────
    //  DEFAULT SCHEDULES PER PROFESSION
    // ─────────────────────────────────────────────────────────────

    public static List<RoutineStop> GetDefaultSchedule(NPCPersona.NPCProfession profession)
    {
        return profession switch
        {
            NPCPersona.NPCProfession.Farmer => new List<RoutineStop>
            {
                new() { LocationName="Wheat Field",   ArriveAtHour= 6, Animation="harvest",
                        ActivityDesc="harvesting wheat in the morning mist",
                        WorldPosition=new Vector3(-5, 0,  4), CanBeInterrupted=true },
                new() { LocationName="Granary",       ArriveAtHour=10, Animation="carry",
                        ActivityDesc="storing the morning harvest in the granary",
                        WorldPosition=new Vector3(-3, 0,  6), CanBeInterrupted=true },
                new() { LocationName="Town Square",   ArriveAtHour=12, Animation="idle_eat",
                        ActivityDesc="eating lunch in the square",
                        WorldPosition=new Vector3( 0, 0, -1), CanBeInterrupted=true },
                new() { LocationName="Vegetable Plot",ArriveAtHour=13, Animation="dig",
                        ActivityDesc="tending the vegetable plots",
                        WorldPosition=new Vector3(-6, 0,  2), CanBeInterrupted=true },
                new() { LocationName="Well",          ArriveAtHour=17, Animation="idle_drink",
                        ActivityDesc="fetching water at the well before sunset",
                        WorldPosition=new Vector3( 2, 0,  3), CanBeInterrupted=true },
                new() { LocationName="Barracks",      ArriveAtHour=19, Animation="idle_sit",
                        ActivityDesc="resting after a long day",
                        WorldPosition=new Vector3( 4, 0, -4), CanBeInterrupted=false },
            },

            NPCPersona.NPCProfession.Soldier => new List<RoutineStop>
            {
                new() { LocationName="Training Grounds",ArriveAtHour= 5, Animation="train_sword",
                        ActivityDesc="training with the sword at dawn",
                        WorldPosition=new Vector3( 5, 0,  2), CanBeInterrupted=true },
                new() { LocationName="Castle Gate",    ArriveAtHour= 8, Animation="patrol",
                        ActivityDesc="guarding the main gate",
                        WorldPosition=new Vector3( 0, 0, -7), CanBeInterrupted=true },
                new() { LocationName="Barracks",       ArriveAtHour=12, Animation="idle_eat",
                        ActivityDesc="eating with the other soldiers",
                        WorldPosition=new Vector3( 4, 0, -4), CanBeInterrupted=true },
                new() { LocationName="Watchtower",     ArriveAtHour=14, Animation="patrol",
                        ActivityDesc="watching for threats from the watchtower",
                        WorldPosition=new Vector3(-7, 0, -7), CanBeInterrupted=true },
                new() { LocationName="Training Grounds",ArriveAtHour=16, Animation="train_archery",
                        ActivityDesc="practising archery in the afternoon",
                        WorldPosition=new Vector3( 5, 0,  2), CanBeInterrupted=true },
                new() { LocationName="Barracks",       ArriveAtHour=20, Animation="idle_sit",
                        ActivityDesc="polishing armour before sleep",
                        WorldPosition=new Vector3( 4, 0, -4), CanBeInterrupted=false },
            },

            NPCPersona.NPCProfession.Merchant => new List<RoutineStop>
            {
                new() { LocationName="Market Stall",   ArriveAtHour= 7, Animation="arrange_goods",
                        ActivityDesc="setting up the market stall for the day",
                        WorldPosition=new Vector3( 1, 0,  5), CanBeInterrupted=true },
                new() { LocationName="Market Stall",   ArriveAtHour= 8, Animation="sell",
                        ActivityDesc="selling goods and haggling with customers",
                        WorldPosition=new Vector3( 1, 0,  5), CanBeInterrupted=true },
                new() { LocationName="Castle Gate",    ArriveAtHour=11, Animation="idle_chat",
                        ActivityDesc="negotiating with an incoming trade caravan",
                        WorldPosition=new Vector3( 0, 0, -7), CanBeInterrupted=true },
                new() { LocationName="Market Stall",   ArriveAtHour=13, Animation="sell",
                        ActivityDesc="managing the afternoon rush at the market",
                        WorldPosition=new Vector3( 1, 0,  5), CanBeInterrupted=true },
                new() { LocationName="Counting House", ArriveAtHour=17, Animation="write",
                        ActivityDesc="counting the day's earnings",
                        WorldPosition=new Vector3( 3, 0,  3), CanBeInterrupted=true },
                new() { LocationName="Tavern",         ArriveAtHour=19, Animation="idle_drink",
                        ActivityDesc="relaxing at the tavern — suspiciously chatty tonight",
                        WorldPosition=new Vector3(-1, 0, -3), CanBeInterrupted=true },
            },

            NPCPersona.NPCProfession.Vassal => new List<RoutineStop>
            {
                new() { LocationName="Keep Study",     ArriveAtHour= 6, Animation="write",
                        ActivityDesc="reviewing the castle's accounts before anyone else is awake",
                        WorldPosition=new Vector3( 0, 2,  0), CanBeInterrupted=false },
                new() { LocationName="Town Square",    ArriveAtHour= 9, Animation="idle_chat",
                        ActivityDesc="hearing petitions from the townsfolk",
                        WorldPosition=new Vector3( 0, 0, -1), CanBeInterrupted=true },
                new() { LocationName="Building Site",  ArriveAtHour=11, Animation="supervise",
                        ActivityDesc="supervising construction progress",
                        WorldPosition=new Vector3(-4, 0,  5), CanBeInterrupted=true },
                new() { LocationName="Keep Study",     ArriveAtHour=13, Animation="write",
                        ActivityDesc="drafting resource reports for your lordship",
                        WorldPosition=new Vector3( 0, 2,  0), CanBeInterrupted=true },
                new() { LocationName="Castle Walls",   ArriveAtHour=15, Animation="inspect",
                        ActivityDesc="inspecting the wall defences",
                        WorldPosition=new Vector3( 8, 0.5f, 0), CanBeInterrupted=true },
                new() { LocationName="Keep Hall",      ArriveAtHour=18, Animation="idle_stand",
                        ActivityDesc="awaiting the evening report",
                        WorldPosition=new Vector3( 0, 1,  1), CanBeInterrupted=true },
            },

            NPCPersona.NPCProfession.Scholar => new List<RoutineStop>
            {
                new() { LocationName="Library",       ArriveAtHour= 6, Animation="write",
                        ActivityDesc="studying ancient manuscripts before dawn",
                        WorldPosition=new Vector3(-2, 2, 1), CanBeInterrupted=false },
                new() { LocationName="Town Square",   ArriveAtHour=10, Animation="idle_chat",
                        ActivityDesc="discussing philosophy with the townsfolk",
                        WorldPosition=new Vector3( 0, 0,-1), CanBeInterrupted=true },
                new() { LocationName="Library",       ArriveAtHour=13, Animation="write",
                        ActivityDesc="transcribing records and writing reports",
                        WorldPosition=new Vector3(-2, 2, 1), CanBeInterrupted=true },
                new() { LocationName="Keep Study",    ArriveAtHour=18, Animation="idle_stand",
                        ActivityDesc="advising the lord on matters of history and law",
                        WorldPosition=new Vector3( 0, 2, 0), CanBeInterrupted=true },
            },

            NPCPersona.NPCProfession.Priest => new List<RoutineStop>
            {
                new() { LocationName="Chapel",        ArriveAtHour= 6, Animation="idle_pray",
                        ActivityDesc="conducting morning prayers at the chapel",
                        WorldPosition=new Vector3( 3, 0, 4), CanBeInterrupted=false },
                new() { LocationName="Town Square",   ArriveAtHour= 9, Animation="idle_chat",
                        ActivityDesc="blessing the people and listening to confessions",
                        WorldPosition=new Vector3( 0, 0,-1), CanBeInterrupted=true },
                new() { LocationName="Barracks",      ArriveAtHour=12, Animation="idle_chat",
                        ActivityDesc="tending to the wounded soldiers",
                        WorldPosition=new Vector3( 4, 0,-4), CanBeInterrupted=true },
                new() { LocationName="Chapel",        ArriveAtHour=19, Animation="idle_pray",
                        ActivityDesc="conducting evening vespers",
                        WorldPosition=new Vector3( 3, 0, 4), CanBeInterrupted=false },
            },

            NPCPersona.NPCProfession.Spy => new List<RoutineStop>
            {
                new() { LocationName="Tavern",        ArriveAtHour= 8, Animation="idle_drink",
                        ActivityDesc="gathering information from loose-tongued merchants",
                        WorldPosition=new Vector3(-1, 0,-3), CanBeInterrupted=true },
                new() { LocationName="Castle Gate",   ArriveAtHour=11, Animation="idle_chat",
                        ActivityDesc="watching the comings and goings at the gate",
                        WorldPosition=new Vector3( 0, 0,-7), CanBeInterrupted=true },
                new() { LocationName="Town Square",   ArriveAtHour=14, Animation="idle_chat",
                        ActivityDesc="blending in with the crowd, watching and listening",
                        WorldPosition=new Vector3( 0, 0,-1), CanBeInterrupted=true },
                new() { LocationName="Watchtower",    ArriveAtHour=20, Animation="patrol",
                        ActivityDesc="making a final sweep of the perimeter",
                        WorldPosition=new Vector3(-7, 0,-7), CanBeInterrupted=false },
            },

            _ => new List<RoutineStop>
            {
                new() { LocationName="Town Square", ArriveAtHour= 9, Animation="idle_chat",
                        ActivityDesc="chatting in the town square",
                        WorldPosition=new Vector3(UnityEngine.Random.Range(-3,3), 0,
                                                  UnityEngine.Random.Range(-3,3)), CanBeInterrupted=true },
            }
        };
    }

    // ─────────────────────────────────────────────────────────────
    //  LIFECYCLE
    // ─────────────────────────────────────────────────────────────

    private void Start()
    {
        if (DailySchedule.Count == 0)
        {
            var npc = NPCManager.Instance?.GetNPC(NpcId);
            if (npc != null) DailySchedule = GetDefaultSchedule(npc.Profession);
        }

        NPCManager.Instance?.RegisterRoutine(this);

        if (GameManager.Instance != null)
            GameManager.Instance.OnDayChanged += OnDayChanged;

        // Start at correct position for current in-game hour
        _currentHour = GetCurrentGameHour();
        SnapToCurrentLocation();
        StartCoroutine(RoutineLoop());
    }

    private void OnDestroy()
    {
        NPCManager.Instance?.UnregisterRoutine(this);
        if (GameManager.Instance != null)
            GameManager.Instance.OnDayChanged -= OnDayChanged;
    }

    private void OnDayChanged(int day)
    {
        _currentHour = 0; // Reset to midnight
    }

    // ─────────────────────────────────────────────────────────────
    //  ROUTINE LOOP
    // ─────────────────────────────────────────────────────────────

    private IEnumerator RoutineLoop()
    {
        // Guard against running on a destroyed or disabled MonoBehaviour — Unity
        // will silently continue scheduling `WaitForSeconds` even after the
        // GameObject is destroyed, which burns CPU and can touch freed state.
        while (this != null && enabled && gameObject != null && gameObject.activeInHierarchy)
        {
            _currentHour = GetCurrentGameHour();

            // Find current scheduled stop
            RoutineStop targetStop = GetCurrentScheduledStop(_currentHour);
            if (targetStop != null && targetStop != _currentStop)
            {
                yield return MoveToStop(targetStop);
            }

            yield return new WaitForSeconds(30f); // Check every 30s real time
        }
    }

    private IEnumerator MoveToStop(RoutineStop stop)
    {
        _isMoving = true;
        Vector3 startPos = transform.position;
        Vector3 endPos   = stop.WorldPosition;

        float distance = Vector3.Distance(startPos, endPos);
        float duration = distance / MoveSpeed;
        float elapsed  = 0;

        // Face direction of travel
        if (distance > 0.1f)
            transform.LookAt(new Vector3(endPos.x, transform.position.y, endPos.z));

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(startPos, endPos, elapsed / duration);
            yield return null;
        }

        transform.position = endPos;
        _currentStop = stop;
        _isMoving = false;

        OnArrivedAtLocation?.Invoke(stop);
        OnActivityStarted?.Invoke(stop.ActivityDesc);
    }

    private void SnapToCurrentLocation()
    {
        _currentStop = GetCurrentScheduledStop(_currentHour);
        if (_currentStop != null)
            transform.position = _currentStop.WorldPosition;
    }

    private RoutineStop GetCurrentScheduledStop(int hour)
    {
        if (DailySchedule.Count == 0) return null;

        RoutineStop current = DailySchedule[0];
        foreach (var stop in DailySchedule)
        {
            if (stop.ArriveAtHour <= hour)
                current = stop;
        }
        return current;
    }

    private int GetCurrentGameHour()
    {
        // GameManager uses 5-minute real day = 1 in-game day (300s/day)
        // Map to 0-23 hour based on elapsed day fraction
        float dayProgress = (Time.realtimeSinceStartup % 300f) / 300f;
        return Mathf.FloorToInt(dayProgress * 24f);
    }

    // ─────────────────────────────────────────────────────────────
    //  INTERACTION
    // ─────────────────────────────────────────────────────────────

    public bool CanInteract() => _currentStop?.CanBeInterrupted ?? true;

    /// <summary>
    /// Returns context string injected into NPC's Gemini system prompt.
    /// Makes conversations location-aware.
    /// </summary>
    public string GetCurrentActivityContext()
    {
        if (_currentStop == null) return "";
        return $"[You are currently {_currentStop.ActivityDesc} at the {_currentStop.LocationName}. " +
               $"It is {_currentHour:D2}:00. The lord has just approached you here.]";
    }
}
