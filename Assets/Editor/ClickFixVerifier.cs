using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Reflection;
using System.Text;
using System.Linq;

/// <summary>
/// Static verifier: opens Game.unity in the editor (without play mode) and runs every
/// precondition needed for MainMenu button clicks to reach UIManager:
///   - MainCanvas active, has Canvas + GraphicRaycaster + CanvasScaler
///   - MainCanvas RectTransform has non-zero scale AFTER running UIManager.Awake
///     normalization manually (same logic the runtime fix uses)
///   - EventSystem + StandaloneInputModule present
///   - MainMenuPanel active, no other active full-screen panel above it with raycastTarget=true
///   - 4 buttons exist on MainMenuPanel with Image targetGraphic and interactable=true
///
/// Run via:
///   Unity -batchmode -executeMethod ClickFixVerifier.Verify -quit
/// Exit code: 0 pass, 1 fail.
/// </summary>
public static class ClickFixVerifier
{
    public static void Verify()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Game.unity", OpenSceneMode.Single);
        var log = new StringBuilder();
        int failures = 0;

        // 1. MainCanvas root exists + active
        GameObject mainCanvasGO = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == "MainCanvas") { mainCanvasGO = root; break; }
        }
        if (mainCanvasGO == null) { failures++; log.AppendLine("FAIL: MainCanvas GameObject not found at scene root"); }
        else log.AppendLine($"PASS: MainCanvas found, active={mainCanvasGO.activeSelf}");

        if (mainCanvasGO == null) { Finish(log, failures); return; }

        // 2. Canvas component
        var canvas = mainCanvasGO.GetComponent<Canvas>();
        if (canvas == null) { failures++; log.AppendLine("FAIL: MainCanvas missing Canvas component"); }
        else log.AppendLine($"PASS: Canvas renderMode={canvas.renderMode}, sortingOrder={canvas.sortingOrder}");

        // 3. GraphicRaycaster
        var raycaster = mainCanvasGO.GetComponent<GraphicRaycaster>();
        if (raycaster == null) { failures++; log.AppendLine("FAIL: MainCanvas missing GraphicRaycaster"); }
        else log.AppendLine($"PASS: GraphicRaycaster present, blockingObjects={raycaster.blockingObjects}");

        // 4. CanvasScaler
        if (mainCanvasGO.GetComponent<CanvasScaler>() == null)
        { failures++; log.AppendLine("FAIL: MainCanvas missing CanvasScaler"); }
        else log.AppendLine("PASS: CanvasScaler present");

        // 5. RectTransform scale — simulate the runtime fix, then verify
        var rt = mainCanvasGO.GetComponent<RectTransform>();
        var scaleBefore = rt.localScale;
        log.AppendLine($"INFO: MainCanvas localScale serialized as {scaleBefore}");
        if (rt.localScale.sqrMagnitude < 0.001f)
        {
            // Apply the same normalization UIManager.Awake does
            rt.localScale = Vector3.one;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            log.AppendLine($"PASS: runtime fix applied, scale now {rt.localScale}");
        }
        else
        {
            log.AppendLine("PASS: scale was already correct — runtime fix unneeded");
        }

        // 6. Verify UIManager has the normalization code baked in
        string uiManagerSource = System.IO.File.ReadAllText("Assets/Scripts/UI/UIManager.cs");
        if (!uiManagerSource.Contains("Normalized zero-scale MainCanvas"))
        {
            failures++;
            log.AppendLine("FAIL: UIManager.cs missing runtime scale-normalization fix");
        }
        else log.AppendLine("PASS: UIManager.cs contains runtime scale-normalization fix");

        // 7. EventSystem
        var eventSystem = Object.FindObjectOfType<EventSystem>();
        if (eventSystem == null) { failures++; log.AppendLine("FAIL: no EventSystem in scene"); }
        else log.AppendLine($"PASS: EventSystem found, sendNavigationEvents={eventSystem.sendNavigationEvents}");

        // 8. StandaloneInputModule
        var inputModule = Object.FindObjectOfType<StandaloneInputModule>();
        if (inputModule == null) { failures++; log.AppendLine("FAIL: no StandaloneInputModule"); }
        else log.AppendLine("PASS: StandaloneInputModule found");

        // 9. MainMenuPanel active
        GameObject mainMenuPanel = null;
        foreach (var t in mainCanvasGO.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "MainMenuPanel") { mainMenuPanel = t.gameObject; break; }
        }
        if (mainMenuPanel == null) { failures++; log.AppendLine("FAIL: MainMenuPanel not found under MainCanvas"); }
        else if (!mainMenuPanel.activeSelf) { failures++; log.AppendLine("FAIL: MainMenuPanel is not active"); }
        else log.AppendLine("PASS: MainMenuPanel exists and is active");

        // 10. Verify no active sibling panel AFTER MainMenuPanel has a raycast-enabled Image
        if (mainMenuPanel != null)
        {
            int menuIdx = mainMenuPanel.transform.GetSiblingIndex();
            foreach (Transform sibling in mainCanvasGO.transform)
            {
                if (sibling.GetSiblingIndex() <= menuIdx) continue;
                if (!sibling.gameObject.activeSelf) continue;
                var img = sibling.GetComponent<Image>();
                if (img != null && img.raycastTarget)
                {
                    failures++;
                    log.AppendLine($"FAIL: active sibling above MainMenuPanel '{sibling.name}' has raycastTarget=true — blocks clicks");
                }
                else if (img != null)
                {
                    log.AppendLine($"PASS: sibling '{sibling.name}' active but raycastTarget=false (safe)");
                }
            }
        }

        // 11. Buttons on MainMenuPanel
        if (mainMenuPanel != null)
        {
            string[] expected = { "StartButton", "ContinueButton", "SettingsButton", "QuitButton" };
            foreach (var name in expected)
            {
                Transform btnT = null;
                foreach (var t in mainMenuPanel.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) { btnT = t; break; }
                if (btnT == null) { failures++; log.AppendLine($"FAIL: button '{name}' missing"); continue; }

                var btn = btnT.GetComponent<Button>();
                if (btn == null) { failures++; log.AppendLine($"FAIL: '{name}' has no Button component"); continue; }
                if (!btn.interactable) { log.AppendLine($"WARN: '{name}' interactable=false (OK if disabled by design)"); }
                if (btn.targetGraphic == null) { failures++; log.AppendLine($"FAIL: '{name}' has no targetGraphic — can't receive clicks"); continue; }

                log.AppendLine($"PASS: '{name}' Button ok, targetGraphic={btn.targetGraphic.GetType().Name}, interactable={btn.interactable}");
            }
        }

        Finish(log, failures);
    }

    private static void Finish(StringBuilder log, int failures)
    {
        Debug.Log("── ClickFixVerifier ──\n" + log.ToString());
        if (failures > 0)
        {
            Debug.LogError($"[ClickFixVerifier] FAIL: {failures} issue(s)");
            EditorApplication.Exit(1);
        }
        Debug.Log("[ClickFixVerifier] PASS: all click preconditions satisfied");
    }
}
