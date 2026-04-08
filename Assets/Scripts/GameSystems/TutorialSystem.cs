using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// First-play tutorial system. Guides new players through core mechanics
/// with Gemini-powered contextual tips and highlights.
/// </summary>
public class TutorialSystem : MonoBehaviour
{
    public static TutorialSystem Instance { get; private set; }

    [Serializable]
    public class TutorialStep
    {
        public string StepId;
        public string TitleKey;
        public string DescriptionKey;
        public TutorialStepType Type;
        public string TargetElementName; // UI element to highlight
        public bool IsCompleted;
        public Action OnStepComplete;
    }

    public enum TutorialStepType
    {
        Highlight,    // Point to a UI element
        WaitForAction, // Wait until player does something
        Dialogue,     // Show a dialogue box
        ForcedAction  // Player must do this to proceed
    }

    private List<TutorialStep> _steps;
    private int _currentStepIndex = -1;
    private bool _tutorialActive;

    public event Action<TutorialStep> OnStepStarted;
    public event Action OnTutorialComplete;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildTutorialSteps();
    }

    private void BuildTutorialSteps()
    {
        _steps = new List<TutorialStep>
        {
            new TutorialStep
            {
                StepId = "welcome",
                TitleKey = "tutorial_welcome_title",
                DescriptionKey = "tutorial_welcome_desc",
                Type = TutorialStepType.Dialogue
            },
            new TutorialStep
            {
                StepId = "resources",
                TitleKey = "tutorial_resources_title",
                DescriptionKey = "tutorial_resources_desc",
                Type = TutorialStepType.Highlight,
                TargetElementName = "ResourceHUD"
            },
            new TutorialStep
            {
                StepId = "talk_to_aldric",
                TitleKey = "tutorial_npc_title",
                DescriptionKey = "tutorial_npc_desc",
                Type = TutorialStepType.ForcedAction,
                TargetElementName = "NPC_vassal_01"
            },
            new TutorialStep
            {
                StepId = "issue_command",
                TitleKey = "tutorial_command_title",
                DescriptionKey = "tutorial_command_desc",
                Type = TutorialStepType.WaitForAction
            },
            new TutorialStep
            {
                StepId = "build_farm",
                TitleKey = "tutorial_build_title",
                DescriptionKey = "tutorial_build_desc",
                Type = TutorialStepType.ForcedAction,
                TargetElementName = "BuildButton"
            },
            new TutorialStep
            {
                StepId = "world_map",
                TitleKey = "tutorial_worldmap_title",
                DescriptionKey = "tutorial_worldmap_desc",
                Type = TutorialStepType.Highlight,
                TargetElementName = "WorldMapButton"
            },
            new TutorialStep
            {
                StepId = "complete",
                TitleKey = "tutorial_complete_title",
                DescriptionKey = "tutorial_complete_desc",
                Type = TutorialStepType.Dialogue
            }
        };
    }

    public void StartTutorial()
    {
        if (PlayerPrefs.GetInt("TutorialCompleted", 0) == 1)
        {
            Debug.Log("[Tutorial] Already completed, skipping.");
            return;
        }

        _tutorialActive = true;
        _currentStepIndex = -1;
        AdvanceStep();
    }

    public void AdvanceStep()
    {
        _currentStepIndex++;
        if (_currentStepIndex >= _steps.Count)
        {
            CompleteTutorial();
            return;
        }

        var step = _steps[_currentStepIndex];
        OnStepStarted?.Invoke(step);
        Debug.Log($"[Tutorial] Step {_currentStepIndex + 1}/{_steps.Count}: {step.StepId}");
    }

    public void CompleteCurrentStep(string stepId)
    {
        if (!_tutorialActive) return;
        if (_currentStepIndex < 0 || _currentStepIndex >= _steps.Count) return;
        if (_steps[_currentStepIndex].StepId != stepId) return;

        _steps[_currentStepIndex].IsCompleted = true;
        AdvanceStep();
    }

    private void CompleteTutorial()
    {
        _tutorialActive = false;
        PlayerPrefs.SetInt("TutorialCompleted", 1);
        OnTutorialComplete?.Invoke();
        Debug.Log("[Tutorial] Complete!");
    }

    public void SkipTutorial()
    {
        _tutorialActive = false;
        PlayerPrefs.SetInt("TutorialCompleted", 1);
        Debug.Log("[Tutorial] Skipped.");
    }

    public bool IsTutorialActive() => _tutorialActive;
    public TutorialStep GetCurrentStep() =>
        _currentStepIndex >= 0 && _currentStepIndex < _steps.Count
            ? _steps[_currentStepIndex] : null;
}
