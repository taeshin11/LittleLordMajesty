using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Modern toast notification system. Shows brief, non-blocking messages at bottom of screen.
/// Queues multiple notifications with smooth slide-in/out animations.
/// </summary>
public class ToastNotification : MonoBehaviour
{
    public static ToastNotification Instance { get; private set; }

    public enum ToastType
    {
        Info,
        Success,
        Warning,
        Error,
        Resource  // Shows resource change
    }

    [Serializable]
    private class ToastData
    {
        public string Message;
        public ToastType Type;
        public float Duration;
        public Sprite Icon;
    }

    [Header("Toast Prefab")]
    [SerializeField] private GameObject _toastPrefab;
    [SerializeField] private Transform _toastContainer;
    [SerializeField] private int _maxVisibleToasts = 3;

    private Queue<ToastData> _queue = new Queue<ToastData>();
    private List<GameObject> _activeToasts = new List<GameObject>();
    private bool _isProcessing;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public static void Show(string message, ToastType type = ToastType.Info, float duration = 3f)
    {
        Instance?.ShowToast(message, type, duration);
    }

    public static void ShowResource(ResourceManager.ResourceType resource, int amount)
    {
        string sign = amount >= 0 ? "+" : "";
        string icon = resource switch
        {
            ResourceManager.ResourceType.Wood => "🪵",
            ResourceManager.ResourceType.Food => "🌾",
            ResourceManager.ResourceType.Gold => "💰",
            ResourceManager.ResourceType.Population => "👥",
            _ => ""
        };
        ToastType type = amount >= 0 ? ToastType.Success : ToastType.Warning;
        Instance?.ShowToast($"{icon} {sign}{amount}", type, 2f);
    }

    private void ShowToast(string message, ToastType type, float duration)
    {
        _queue.Enqueue(new ToastData { Message = message, Type = type, Duration = duration });
        if (!_isProcessing) StartCoroutine(ProcessQueue());
    }

    private IEnumerator ProcessQueue()
    {
        _isProcessing = true;

        while (_queue.Count > 0)
        {
            // Limit active toasts
            while (_activeToasts.Count >= _maxVisibleToasts)
                yield return new WaitForSeconds(0.1f);

            var data = _queue.Dequeue();
            yield return StartCoroutine(ShowToastCoroutine(data));
            yield return new WaitForSeconds(0.1f);
        }

        _isProcessing = false;
    }

    private IEnumerator ShowToastCoroutine(ToastData data)
    {
        if (_toastPrefab == null || _toastContainer == null) yield break;

        var toast = Instantiate(_toastPrefab, _toastContainer);
        _activeToasts.Add(toast);

        // Configure toast visuals
        var text = toast.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null) text.text = data.Message;

        var bg = toast.GetComponent<Image>();
        if (bg != null)
        {
            var theme = UITheme.Load();
            bg.color = data.Type switch
            {
                ToastType.Success => theme?.SuccessGreen ?? Color.green,
                ToastType.Warning => theme?.WarningOrange ?? Color.yellow,
                ToastType.Error => theme?.DangerRed ?? Color.red,
                ToastType.Resource => new Color(0.2f, 0.2f, 0.3f, 0.9f),
                _ => new Color(0.2f, 0.2f, 0.4f, 0.9f)
            };
        }

        // Slide in
        var rt = toast.GetComponent<RectTransform>();
        if (rt != null)
        {
            Vector2 startPos = rt.anchoredPosition + new Vector2(300, 0);
            Vector2 endPos = rt.anchoredPosition;
            yield return AnimatePosition(rt, startPos, endPos, 0.3f);
        }

        // Wait
        yield return new WaitForSeconds(data.Duration);

        // Slide out
        if (rt != null && toast != null)
        {
            Vector2 startPos = rt.anchoredPosition;
            Vector2 endPos = rt.anchoredPosition + new Vector2(300, 0);
            yield return AnimatePosition(rt, startPos, endPos, 0.2f);
        }

        _activeToasts.Remove(toast);
        if (toast != null) Destroy(toast);
    }

    private IEnumerator AnimatePosition(RectTransform rt, Vector2 from, Vector2 to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (rt == null) yield break;
            elapsed += Time.deltaTime;
            rt.anchoredPosition = Vector2.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        if (rt != null) rt.anchoredPosition = to;
    }
}
