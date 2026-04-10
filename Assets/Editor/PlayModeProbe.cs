using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections;
using System.IO;

/// <summary>
/// Runs Unity in batch play mode for a few seconds on Bootstrap.unity and captures
/// the state of the main menu: Canvas scale, EventSystem, raycaster, button listeners.
/// Writes a report file and exits.
///
/// Run: Unity -batchmode -executeMethod PlayModeProbe.Start -quit
/// </summary>
public static class PlayModeProbe
{
    private const string REPORT_PATH = "../playmode_probe_report.txt";

    [InitializeOnLoadMethod]
    private static void OnLoad()
    {
        // Hook play mode state change so we can capture state after ~3 seconds
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    public static void Start()
    {
        // Open Bootstrap scene and enter play mode
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity", OpenSceneMode.Single);
        Debug.Log("[Probe] Opened Bootstrap.unity, entering play mode...");
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            Debug.Log("[Probe] Play mode entered, scheduling capture...");
            var probeGO = new GameObject("PlayModeProbe_Runner");
            probeGO.AddComponent<ProbeRunner>();
            UnityEngine.Object.DontDestroyOnLoad(probeGO);
        }
    }

    private class ProbeRunner : MonoBehaviour
    {
        private void Start() { StartCoroutine(CaptureAndExit()); }

        private IEnumerator CaptureAndExit()
        {
            // Wait ~3 seconds for Bootstrap → Game scene transition and UIManager.Awake
            yield return new WaitForSeconds(3f);

            var report = new System.Text.StringBuilder();
            report.AppendLine($"[Probe] Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");

            var canvas = GameObject.Find("MainCanvas");
            if (canvas != null)
            {
                var rt = canvas.GetComponent<RectTransform>();
                report.AppendLine($"[Probe] MainCanvas.localScale = {rt.localScale}");
                report.AppendLine($"[Probe] MainCanvas.sizeDelta  = {rt.sizeDelta}");
                report.AppendLine($"[Probe] MainCanvas.rect       = {rt.rect}");
                var c = canvas.GetComponent<Canvas>();
                if (c != null)
                    report.AppendLine($"[Probe] Canvas.scaleFactor  = {c.scaleFactor}, pixelRect={c.pixelRect}");

                var raycaster = canvas.GetComponent<GraphicRaycaster>();
                report.AppendLine($"[Probe] GraphicRaycaster enabled = {raycaster?.enabled}");
            }
            else
            {
                report.AppendLine("[Probe] MainCanvas NOT FOUND");
            }

            var eventSystem = UnityEngine.Object.FindObjectOfType<EventSystem>();
            report.AppendLine($"[Probe] EventSystem: {(eventSystem != null ? "FOUND" : "NULL")}, current={EventSystem.current}");

            // Enumerate all active panels under MainCanvas
            if (canvas != null)
            {
                foreach (Transform child in canvas.transform)
                {
                    if (!child.gameObject.activeSelf) continue;
                    var img = child.GetComponent<Image>();
                    report.AppendLine($"[Probe] Active child '{child.name}' raycastTarget={img?.raycastTarget}, worldPos={child.position}, rect={child.GetComponent<RectTransform>().rect}");
                }
            }

            // Find MainMenu buttons and their world positions
            var mainMenu = GameObject.Find("MainMenuPanel");
            if (mainMenu != null)
            {
                foreach (var btn in mainMenu.GetComponentsInChildren<Button>(true))
                {
                    var brt = btn.GetComponent<RectTransform>();
                    var corners = new Vector3[4];
                    brt.GetWorldCorners(corners);
                    report.AppendLine($"[Probe] Button '{btn.name}' active={btn.gameObject.activeSelf} interactable={btn.interactable} listeners={btn.onClick.GetPersistentEventCount()}");
                    report.AppendLine($"        worldCorners: BL={corners[0]} TR={corners[2]}");
                }
            }

            // Try to raycast at the center of the screen where the New Game button should be
            if (eventSystem != null && canvas != null)
            {
                var pointerData = new PointerEventData(eventSystem)
                {
                    position = new Vector2(Screen.width / 2f, Screen.height / 2f)
                };
                var results = new System.Collections.Generic.List<RaycastResult>();
                eventSystem.RaycastAll(pointerData, results);
                report.AppendLine($"[Probe] Raycast at screen center ({pointerData.position}) hit {results.Count} graphics:");
                foreach (var r in results)
                    report.AppendLine($"        -> {r.gameObject.name} (module={r.module.GetType().Name})");
            }

            Debug.Log(report.ToString());
            try { File.WriteAllText(REPORT_PATH, report.ToString()); } catch { }

            yield return null;
            EditorApplication.ExitPlaymode();
            yield return new WaitForSeconds(0.5f);
            EditorApplication.Exit(0);
        }
    }
}
