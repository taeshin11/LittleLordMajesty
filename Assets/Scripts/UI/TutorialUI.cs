using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

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

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (_overlayRoot != null) _overlayRoot.SetActive(false);
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
        if (_nextButton != null) _nextButton.onClick.RemoveAllListeners();
        if (_skipButton != null) _skipButton.onClick.RemoveAllListeners();
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
        if (_nextButtonText != null) _nextButtonText.text = step.StepId == "complete" ? "Start!" : "Next";

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
        // Search all active GameObjects by name
        var allObjects = FindObjectsOfType<RectTransform>(true);
        foreach (var obj in allObjects)
        {
            if (obj.gameObject.name == elementName)
                return obj.gameObject;
        }
        return null;
    }

    private IEnumerator TypewriterEffect(string fullText)
    {
        if (_descriptionText == null) yield break;
        _descriptionText.text = "";
        foreach (char c in fullText)
        {
            _descriptionText.text += c;
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
        if (LocalizationManager.Instance != null)
        {
            string val = LocalizationManager.Instance.Get(key);
            if (!string.IsNullOrEmpty(val) && val != key) return val;
        }
        // Fallback to readable defaults
        return key switch
        {
            "tutorial_welcome_title" => "Welcome, Young Lord!",
            "tutorial_welcome_desc" => "You have inherited a small territory. Your people look to you for guidance. Let me show you the basics of ruling your domain.",
            "tutorial_resources_title" => "Your Resources",
            "tutorial_resources_desc" => "These are your resources: Wood, Food, and Gold. Keep them balanced to grow your territory and keep your people happy.",
            "tutorial_npc_title" => "Meet Your Vassal",
            "tutorial_npc_desc" => "Tap on Aldric, your loyal vassal. He can carry out your commands and provide counsel.",
            "tutorial_command_title" => "Issue a Command",
            "tutorial_command_desc" => "Type a command for your vassal. Try something like 'Gather wood from the forest' or 'Scout the nearby lands'.",
            "tutorial_build_title" => "Build Your Territory",
            "tutorial_build_desc" => "Tap the Build button to construct buildings. A Farm will produce food for your people each day.",
            "tutorial_worldmap_title" => "The World Beyond",
            "tutorial_worldmap_desc" => "The World Map shows neighboring territories. As you grow stronger, you can expand through diplomacy or conquest.",
            "tutorial_complete_title" => "You Are Ready!",
            "tutorial_complete_desc" => "You now know the basics. Rule wisely, Little Lord. Your decisions shape the fate of your people.",
            _ => key
        };
    }
}
