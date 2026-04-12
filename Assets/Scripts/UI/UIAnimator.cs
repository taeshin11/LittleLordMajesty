using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;

/// <summary>
/// Smooth, charming UI animations for the medieval kingdom aesthetic.
/// Panels slide in/out, buttons bounce on press, notifications float up.
/// Attach to any UI element and call the static helpers or component methods.
/// </summary>
public class UIAnimator : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    //  PANEL TRANSITIONS
    // ─────────────────────────────────────────────────────────────

    public static void ShowPanel(GameObject panel, PanelAnim anim = PanelAnim.FadeSlideUp,
        float duration = 0.25f, Action onDone = null)
    {
        if (panel == null) return;
        panel.SetActive(true);
        var comp = panel.GetComponent<UIAnimator>() ?? panel.AddComponent<UIAnimator>();
        comp.StartCoroutine(comp.AnimateIn(anim, duration, onDone));
    }

    public static void HidePanel(GameObject panel, PanelAnim anim = PanelAnim.FadeSlideDown,
        float duration = 0.20f, Action onDone = null)
    {
        if (panel == null || !panel.activeSelf) return;
        var comp = panel.GetComponent<UIAnimator>() ?? panel.AddComponent<UIAnimator>();
        comp.StartCoroutine(comp.AnimateOut(anim, duration, () =>
        {
            panel.SetActive(false);
            onDone?.Invoke();
        }));
    }

    public enum PanelAnim
    {
        FadeSlideUp,    // Fades in while sliding up (menu, dialog)
        FadeSlideDown,  // Fades out sliding down
        FadeScale,      // Pops in with slight scale (alerts)
        SlideFromRight, // Slides in from right (side panels)
        SlideFromLeft,
        Fade            // Pure fade
    }

    private IEnumerator AnimateIn(PanelAnim anim, float duration, Action onDone)
    {
        var cg = GetOrAddCanvasGroup();
        var rt = GetComponent<RectTransform>();

        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;

        Vector2 targetPos = rt.anchoredPosition;
        Vector2 startOffset = anim switch
        {
            PanelAnim.FadeSlideUp   => new Vector2(0, -30f),
            PanelAnim.SlideFromRight=> new Vector2(100f, 0),
            PanelAnim.SlideFromLeft => new Vector2(-100f, 0),
            _                        => Vector2.zero
        };
        float startScale = anim == PanelAnim.FadeScale ? 0.90f : 1f;

        rt.anchoredPosition = targetPos + startOffset;
        transform.localScale = Vector3.one * startScale;

        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float ease = EaseOutCubic(t);

            cg.alpha = ease;
            rt.anchoredPosition = Vector2.Lerp(targetPos + startOffset, targetPos, ease);
            if (anim == PanelAnim.FadeScale)
                transform.localScale = Vector3.Lerp(Vector3.one * startScale, Vector3.one, ease);

            yield return null;
        }

        cg.alpha = 1f;
        rt.anchoredPosition = targetPos;
        transform.localScale = Vector3.one;
        cg.interactable = true;
        cg.blocksRaycasts = true;
        onDone?.Invoke();
    }

    private IEnumerator AnimateOut(PanelAnim anim, float duration, Action onDone)
    {
        var cg = GetOrAddCanvasGroup();
        var rt = GetComponent<RectTransform>();
        Vector2 startPos = rt.anchoredPosition;
        Vector2 endOffset = anim switch
        {
            PanelAnim.FadeSlideDown => new Vector2(0, -30f),
            PanelAnim.SlideFromLeft => new Vector2(-100f, 0),
            _ => Vector2.zero
        };

        cg.interactable = false;
        cg.blocksRaycasts = false;

        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t  = Mathf.Clamp01(elapsed / duration);
            float ease = EaseInCubic(t);
            cg.alpha = 1f - ease;
            rt.anchoredPosition = Vector2.Lerp(startPos, startPos + endOffset, ease);
            yield return null;
        }

        cg.alpha = 1f;
        rt.anchoredPosition = startPos;
        onDone?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────
    //  BUTTON BOUNCE
    // ─────────────────────────────────────────────────────────────

    public static void BounceButton(GameObject btn, float intensity = 0.08f)
    {
        if (btn == null) return;
        var comp = btn.GetComponent<UIAnimator>() ?? btn.AddComponent<UIAnimator>();
        comp.StartCoroutine(comp.DoBounce(intensity));
    }

    private IEnumerator DoBounce(float intensity)
    {
        Vector3 orig = transform.localScale;
        Vector3 small = orig * (1f - intensity);

        float elapsed = 0; float d1 = 0.08f;
        while (elapsed < d1) {
            elapsed += Time.unscaledDeltaTime;
            transform.localScale = Vector3.Lerp(orig, small, elapsed / d1);
            yield return null;
        }
        elapsed = 0;
        while (elapsed < d1 * 1.5f) {
            elapsed += Time.unscaledDeltaTime;
            transform.localScale = Vector3.Lerp(small, orig * (1f + intensity * 0.5f), elapsed / (d1 * 1.5f));
            yield return null;
        }
        elapsed = 0;
        while (elapsed < d1) {
            elapsed += Time.unscaledDeltaTime;
            transform.localScale = Vector3.Lerp(orig * (1f + intensity * 0.5f), orig, elapsed / d1);
            yield return null;
        }
        transform.localScale = orig;
    }

    // ─────────────────────────────────────────────────────────────
    //  FLOATING TEXT (damage numbers, +gold, etc.)
    // ─────────────────────────────────────────────────────────────

    public static void FloatText(Transform parent, string text, Color color,
        float duration = 1.2f, float rise = 60f)
    {
        var go = new GameObject("FloatText");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = Vector2.zero;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.color = color;
        tmp.fontSize = 32;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;

        var anim = go.AddComponent<UIAnimator>();
        anim.StartCoroutine(anim.DoFloatText(tmp, rt, rise, duration));
    }

    private IEnumerator DoFloatText(TextMeshProUGUI tmp, RectTransform rt, float rise, float duration)
    {
        float elapsed = 0;
        Vector2 startPos = rt.anchoredPosition;
        Color startColor = tmp.color;
        Vector3 startScale = transform.localScale;

        // Pop in
        transform.localScale = Vector3.one * 0.5f;
        while (elapsed < 0.1f) {
            elapsed += Time.unscaledDeltaTime;
            transform.localScale = Vector3.Lerp(Vector3.one * 0.5f, Vector3.one * 1.2f, elapsed / 0.1f);
            yield return null;
        }
        transform.localScale = Vector3.one;
        elapsed = 0;

        while (elapsed < duration) {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            rt.anchoredPosition = startPos + Vector2.up * (rise * t);
            float alpha = t < 0.6f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.6f) / 0.4f);
            tmp.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            yield return null;
        }
        Destroy(gameObject);
    }

    // ─────────────────────────────────────────────────────────────
    //  SHAKE (error, low health, etc.)
    // ─────────────────────────────────────────────────────────────

    public static void Shake(RectTransform rt, float intensity = 8f, float duration = 0.3f)
    {
        if (rt == null) return;
        var comp = rt.GetComponent<UIAnimator>() ?? rt.gameObject.AddComponent<UIAnimator>();
        comp.StartCoroutine(comp.DoShake(rt, intensity, duration));
    }

    private IEnumerator DoShake(RectTransform rt, float intensity, float duration)
    {
        Vector2 orig = rt.anchoredPosition;
        float elapsed = 0;
        while (elapsed < duration) {
            elapsed += Time.unscaledDeltaTime;
            float decay = 1f - (elapsed / duration);
            rt.anchoredPosition = orig + UnityEngine.Random.insideUnitCircle * intensity * decay;
            yield return null;
        }
        rt.anchoredPosition = orig;
    }

    // ─────────────────────────────────────────────────────────────
    //  COUNT-UP NUMBER ANIMATION
    // ─────────────────────────────────────────────────────────────

    public static void CountUp(TextMeshProUGUI label, int from, int to,
        float duration = 0.5f, string prefix = "", string suffix = "")
    {
        if (label == null) return;
        var comp = label.GetComponent<UIAnimator>() ?? label.gameObject.AddComponent<UIAnimator>();
        comp.StartCoroutine(comp.DoCountUp(label, from, to, duration, prefix, suffix));
    }

    private IEnumerator DoCountUp(TextMeshProUGUI label, int from, int to,
        float duration, string prefix, string suffix)
    {
        float elapsed = 0;
        while (elapsed < duration) {
            elapsed += Time.unscaledDeltaTime;
            float t = EaseOutCubic(Mathf.Clamp01(elapsed / duration));
            int current = Mathf.RoundToInt(Mathf.Lerp(from, to, t));
            label.text = prefix + current.ToString("N0") + suffix;
            yield return null;
        }
        label.text = prefix + to.ToString("N0") + suffix;
    }

    // ─────────────────────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────────────────────

    private CanvasGroup GetOrAddCanvasGroup()
    {
        var cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
        return cg;
    }

    private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
    private static float EaseInCubic(float t)  => t * t * t;
}
