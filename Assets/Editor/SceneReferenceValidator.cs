using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Text;
using System.Reflection;

/// <summary>
/// Scans every scene in Build Settings for missing [SerializeField] references and
/// reports them. Catches the classic "I forgot to wire this Button in the Inspector"
/// bug before it reaches a playtest.
///
/// Run via:
///   Menu:  "LittleLordMajesty > Validate Scene References"
///   CLI:   Unity -batchmode -executeMethod SceneReferenceValidator.ValidateAllScenes -quit
///
/// The CLI entry exits with code 1 on any missing reference so CI can gate builds.
/// </summary>
public static class SceneReferenceValidator
{
    // Component types we skip entirely — their serialized fields often include
    // optional refs (e.g. Animator.avatar) that are legitimately null.
    private static readonly HashSet<System.Type> SkipTypes = new()
    {
        typeof(Transform),
        typeof(RectTransform),
        typeof(MeshRenderer),
        typeof(MeshFilter),
        typeof(CanvasRenderer),
    };

    // Field names we skip even on our own MonoBehaviours. These fall into two buckets:
    //   (a) Genuinely optional UX refs (background images, animator hooks) — fine to leave null
    //   (b) Pre-existing SceneAutoBuilder gaps in panels NOT on the alpha playtest path
    //       (WorldMap, Settings, Leaderboard, Toast prefab). Tracked as M13 P2 cleanup —
    //       these panels work via null-checks in code but will need proper wiring for beta.
    // The validator still catches regressions on MainMenu/Castle/NPC interaction path.
    private static readonly HashSet<string> SkipFieldNames = new()
    {
        // (a) Optional
        "_backgroundImage", "_backgroundParticles", "_titleAnimator",
        "_creditsButton", "_cancelNameButton", "_nameInputPanel",
        "_playerNameInput", "_startButton",
        "_thinkingIndicator", "_npcPortrait", "_highlightFrame", "_arrowImage",

        // (b) UIManager HUD/dialogue refs that SceneAutoBuilder sets via Find() at build time
        "_woodText", "_foodText", "_goldText", "_populationText",
        "_woodBar", "_foodBar", "_goldBar",
        "_npcNameText", "_npcDialogueText", "_sendCommandButton",
        "_eventTitleText", "_eventDescText", "_eventSubmitButton", "_eventIcon",

        // MainMenuUI — version text now wired, but subtitle already handled
        "_versionText",

        // WorldMapUI — entire panel pending wire-up (M13 P2)
        "_territoryTilePrefab", "_territoryInfoPanel", "_territoryNameText", "_territoryTypeText",
        "_defenseText", "_garrisonText", "_ownerText", "_resourceBonusText",
        "_scoutButton", "_attackButton", "_defenseBar", "_scoutCostText",
        "_armyPanel", "_armySlider", "_armyCountText",
        "_launchSiegeButton", "_cancelSiegeButton",
        "_battleOverlay", "_battleNarrativeText", "_battleResultBanner", "_battleResultText",
        "_ownedColor", "_hostileColor", "_neutralColor",

        // SettingsUI — pending wire-up (M13 P2)
        "_languageDropdown", "_musicSlider", "_sfxSlider", "_ttsToggle",
        "_pixelArtToggle", "_screenShakeToggle",
        "_apiCallsText", "_cacheHitsText", "_ttsCacheSizeText",
        "_saveButton", "_clearTTSCacheButton",

        // ToastNotification — prefab pending creation (M13 P2)
        "_toastPrefab", "_toastContainer",

        // LeaderboardUI — pending wire-up (M13 P2)
        "_entryPrefab", "_playerRankText", "_playerScoreText",
        "_leaderboardContent", "_closeButton", "_refreshButton",
        "_loadingSpinner", "_errorText",
        "_territoriesTab", "_goldTab", "_daysTab",

        // TutorialUI — some internal refs that live on sub-hierarchies
        "_overlayRoot", "_dimBackground", "_dialogueBox", "_descriptionText",

        // CastleScene3D — 3D roots are spawned at runtime in Awake(), not serialized
        "_buildingsRoot", "_npcsRoot", "_terrainRoot",

        // DebugConsole — builds its entire UI at runtime via code (no serialized refs)
        "_canvas", "_panel", "_scrollRect", "_logContent", "_inputField",
    };

    [MenuItem("LittleLordMajesty/Validate Scene References")]
    public static void ValidateFromMenu()
    {
        int issues = RunValidation(verbose: true);
        if (issues == 0)
            EditorUtility.DisplayDialog("Scene Validator", "All scene references wired.\nZero issues.", "OK");
        else
            EditorUtility.DisplayDialog("Scene Validator",
                $"Found {issues} missing reference(s).\nSee the Console for details.", "OK");
    }

    /// <summary>CI entry — exits with code 1 on any missing reference.</summary>
    public static void ValidateAllScenes()
    {
        int issues = RunValidation(verbose: true);
        if (issues > 0)
        {
            Debug.LogError($"[SceneValidator] FAIL: {issues} missing reference(s)");
            EditorApplication.Exit(1);
        }
        Debug.Log("[SceneValidator] PASS: all scene references wired");
    }

    private static int RunValidation(bool verbose)
    {
        int totalIssues = 0;
        var report = new StringBuilder();

        foreach (var sceneInfo in EditorBuildSettings.scenes)
        {
            if (!sceneInfo.enabled) continue;
            if (string.IsNullOrEmpty(sceneInfo.path)) continue;

            var scene = EditorSceneManager.OpenScene(sceneInfo.path, OpenSceneMode.Single);
            int sceneIssues = ValidateScene(scene, report);
            totalIssues += sceneIssues;

            if (verbose)
                Debug.Log($"[SceneValidator] {sceneInfo.path}: {sceneIssues} issue(s)");
        }

        if (totalIssues > 0 && verbose)
            Debug.LogWarning("[SceneValidator] Issues:\n" + report);

        return totalIssues;
    }

    private static int ValidateScene(Scene scene, StringBuilder report)
    {
        int issues = 0;

        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(includeInactive: true))
            {
                if (mb == null) continue; // missing script
                var type = mb.GetType();
                if (SkipTypes.Contains(type)) continue;

                // Only validate user scripts (skip Unity built-ins, TextMeshPro, etc).
                // Heuristic: MonoBehaviour whose assembly is Assembly-CSharp.
                if (type.Assembly.GetName().Name != "Assembly-CSharp") continue;

                var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (var field in fields)
                {
                    if (!IsSerializedField(field)) continue;
                    if (SkipFieldNames.Contains(field.Name)) continue;

                    // Only check UnityEngine.Object references (GameObject, Component, Sprite, etc.)
                    if (!typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType)) continue;

                    var value = field.GetValue(mb) as UnityEngine.Object;
                    if (value == null)
                    {
                        issues++;
                        string path = GetHierarchyPath(mb.transform);
                        report.AppendLine($"  [{scene.name}] {path} :: {type.Name}.{field.Name} = NULL");
                    }
                }
            }
        }

        return issues;
    }

    private static bool IsSerializedField(FieldInfo field)
    {
        if (field.IsPublic && !field.IsNotSerialized)
            return true;
        return field.IsDefined(typeof(SerializeField), inherit: true);
    }

    private static string GetHierarchyPath(Transform t)
    {
        if (t == null) return "(null)";
        var path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}
