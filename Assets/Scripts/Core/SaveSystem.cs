using UnityEngine;
using System;
using System.IO;
using Newtonsoft.Json;

/// <summary>
/// Handles game save/load using JSON serialization to persistent storage.
/// </summary>
public static class SaveSystem
{
    private static readonly string SavePath = Path.Combine(Application.persistentDataPath, "save.json");
    private static readonly string BackupPath = Path.Combine(Application.persistentDataPath, "save_backup.json");

    [Serializable]
    public class SaveData
    {
        public string PlayerName;
        public string LordTitle;
        public int Day;
        public int Year;
        public float PlayTimeSeconds;

        // Resources
        public int Wood;
        public int Food;
        public int Gold;
        public int Population;

        // Progress
        public int TerritoriesOwned;
        public string[] CompletedBuildings;
        public string[] ActiveQuestIds;

        // NPC states
        public NPCManager.NPCSaveData[] NPCStates;

        public string SaveTimestamp;
        public string GameVersion = "0.1.0";
    }

    public static void Save()
    {
        try
        {
            var gm = GameManager.Instance;
            var rm = gm?.ResourceManager;
            var nm = gm?.NPCManager;

            var data = new SaveData
            {
                PlayerName = gm?.PlayerName ?? "Unknown",
                LordTitle = gm?.LordTitle ?? "Little Lord",
                Day = gm?.Day ?? 1,
                Year = gm?.Year ?? 1,
                PlayTimeSeconds = gm?.PlayTimeSeconds ?? 0f,
                Wood = rm?.Wood ?? 0,
                Food = rm?.Food ?? 0,
                Gold = rm?.Gold ?? 0,
                Population = rm?.Population ?? 0,
                TerritoriesOwned = gm?.WorldMapManager?.OwnedTerritoryCount ?? 1,
                NPCStates = nm?.GetSaveData() ?? Array.Empty<NPCManager.NPCSaveData>(),
                SaveTimestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            };

            string json = JsonConvert.SerializeObject(data, Formatting.Indented);

            // Backup existing save
            if (File.Exists(SavePath))
                File.Copy(SavePath, BackupPath, overwrite: true);

            File.WriteAllText(SavePath, json);
            Debug.Log($"[SaveSystem] Game saved to {SavePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] Save failed: {e.Message}");
        }
    }

    public static void Load()
    {
        string path = File.Exists(SavePath) ? SavePath : BackupPath;
        if (!File.Exists(path))
        {
            Debug.Log("[SaveSystem] No save file found.");
            return;
        }

        try
        {
            string json = File.ReadAllText(path);
            var data = JsonConvert.DeserializeObject<SaveData>(json);
            ApplySaveData(data);
            Debug.Log($"[SaveSystem] Game loaded from {path}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] Load failed: {e.Message}");
        }
    }

    private static void ApplySaveData(SaveData data)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        gm.PlayerName = data.PlayerName;
        gm.LordTitle = data.LordTitle;
        gm.Day = data.Day;
        gm.Year = data.Year;
        gm.PlayTimeSeconds = data.PlayTimeSeconds;

        gm.ResourceManager?.SetResources(data.Wood, data.Food, data.Gold, data.Population);
        gm.NPCManager?.LoadSaveData(data.NPCStates);
    }

    public static bool HasSaveFile() => File.Exists(SavePath) || File.Exists(BackupPath);
    public static void DeleteSave()
    {
        if (File.Exists(SavePath)) File.Delete(SavePath);
        if (File.Exists(BackupPath)) File.Delete(BackupPath);
    }
}
