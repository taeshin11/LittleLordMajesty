using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Overlay UI for the tutorial system. Shows step dialogue boxes,
/// highlights target elements, and handles skip/advance buttons.
/// </summary>
public class TutorialUI : MonoBehaviour
{
    public static TutorialUI Instance { get; private set; }

    [Header("Overlay")]
    [SerializeField] private GameObject _overlayRoot;
    [SerializeField] private Image _dimBackground;

    [Header("Dialogue Box")]
    [SerializeField] private GameObject _dialogueBox;
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private Button _nextButton;
    [SerializeField] private TextMeshProUGUI _nextButtonText;
    [SerializeField] private Button _skipButton;

    [Header("Highlight")]
    [SerializeField] private GameObject _highlightFrame;
    [SerializeField] private Image _arrowImage;

    private TutorialSystem.TutorialStep _currentStep;
    private Coroutine _typewriterCoroutine;
    private readonly Dictionary<string, GameObject> _uiElementCache = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        // DO NOT call _overlayRoot.SetActive(false) here. _overlayRoot is THIS
        // gameObject (set in BuildTutorialPanel), and TutorialSystem.StartTutorial
        // explicitly SetActive(true)'s the panel to make Awake/Start run. If this
        // Awake then deactivates itself again, we get a self-recursion that has
        // caused wasm "null function" runtime errors on WebGL builds.
        // Initial inactive state is already set in SceneAutoBuilder at scene-build
        // time (BuildTutorialPanel: `panel.SetActive(false)`).
    }

    private void Start()
    {
        if (TutorialSystem.Instance != null)
        {
            TutorialSystem.Instance.OnStepStarted += ShowStep;
            TutorialSystem.Instance.OnTutorialComplete += OnTutorialComplete;
        }

        if (_nextButton != null)
            _nextButton.onClick.AddListener(OnNextClicked);
        if (_skipButton != null)
            _skipButton.onClick.AddListener(OnSkipClicked);
    }

    private void OnDestroy()
    {
        if (TutorialSystem.Instance != null)
        {
            TutorialSystem.Instance.OnStepStarted -= ShowStep;
            TutorialSystem.Instance.OnTutorialComplete -= OnTutorialComplete;
        }
        if (_nextButton != null) _nextButton.onClick.RemoveListener(OnNextClicked);
        if (_skipButton != null) _skipButton.onClick.RemoveListener(OnSkipClicked);
    }

    private void ShowStep(TutorialSystem.TutorialStep step)
    {
        _currentStep = step;
        if (_overlayRoot != null) _overlayRoot.SetActive(true);

        // Resolve localized text with fallback
        string title = Localize(step.TitleKey);
        string desc = Localize(step.DescriptionKey);

        if (_titleText != null) _titleText.text = title;

        // Typewriter effect for description
        if (_typewriterCoroutine != null) StopCoroutine(_typewriterCoroutine);
        if (_descriptionText != null)
            _typewriterCoroutine = StartCoroutine(TypewriterEffect(desc));

        // Configure based on step type
        bool showNext = step.Type == TutorialSystem.TutorialStepType.Dialogue
                     || step.Type == TutorialSystem.TutorialStepType.Highlight;

        if (_nextButton != null) _nextButton.gameObject.SetActive(showNext);
        if (_nextButtonText != null)
        {
            var loc = LocalizationManager.Instance;
            _nextButtonText.text = step.StepId == "complete"
                ? (loc?.Get("btn_tutorial_start") ?? "Start!")
                : (loc?.Get("btn_tutorial_next") ?? "Next");
        }

        // Highlight target element
        UpdateHighlight(step);

        // Dim background for dialogue steps
        if (_dimBackground != null)
        {
            Color c = _dimBackground.color;
            c.a = step.Type == TutorialSystem.TutorialStepType.Dialogue ? 0.6f : 0.3f;
            _dimBackground.color = c;
        }
    }

    private void UpdateHighlight(TutorialSystem.TutorialStep step)
    {
        if (_highlightFrame == null) return;

        if (string.IsNullOrEmpty(step.TargetElementName))
        {
            _highlightFrame.SetActive(false);
            return;
        }

        // Find target UI element by name
        var target = FindUIElement(step.TargetElementName);
        if (target != null)
        {
            _highlightFrame.SetActive(true);
            var rt = _highlightFrame.GetComponent<RectTransform>();
            var targetRT = target.GetComponent<RectTransform>();
            if (rt != null && targetRT != null)
            {
                rt.position = targetRT.position;
                rt.sizeDelta = targetRT.sizeDelta + new Vector2(20f, 20f);
            }
        }
        else
        {
            _highlightFrame.SetActive(false);
        }
    }

    private GameObject FindUIElement(string elementName)
    {
        // Unity fake-null: destroyed objects pass C# null check but fail Unity ==
        if (_uiElementCache.TryGetValue(elementName, out var cached))
        {
            if (cached != null) return cached;
            _uiElementCache.Remove(elementName); // Stale entry after scene reload
        }

        foreach (var obj in FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (obj.gameObject.name == elementName)
            {
                _uiElementCache[elementName] = obj.gameObject;
                return obj.gameObject;
            }
        }
        return null;
    }

    private IEnumerator TypewriterEffect(string fullText)
    {
        if (_descriptionText == null) yield break;
        _descriptionText.text = fullText;
        _descriptionText.maxVisibleCharacters = 0;
        for (int i = 1; i <= fullText.Length; i++)
        {
            _descriptionText.maxVisibleCharacters = i;
            yield return new WaitForSecondsRealtime(0.02f);
        }
    }

    private void OnNextClicked()
    {
        if (_currentStep == null) return;
        TutorialSystem.Instance?.CompleteCurrentStep(_currentStep.StepId);
    }

    private void OnSkipClicked()
    {
        TutorialSystem.Instance?.SkipTutorial();
        if (_overlayRoot != null) _overlayRoot.SetActive(false);
    }

    private void OnTutorialComplete()
    {
        if (_overlayRoot != null) _overlayRoot.SetActive(false);
    }

    private string Localize(string key)
    {
        // All tutorial strings now live in Resources/Localization/{lang}.json.
        // LocalizationManager.Get() already handles English fallback internally.
        return LocalizationManager.Instance?.Get(key) ?? key;
    }
}
