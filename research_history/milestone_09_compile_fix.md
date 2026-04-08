# Milestone 09 — Zero Compile Errors + CI Foundation

**Date:** 2026-04-09

## What Was Done

### Compile Error Fixes (8 categories)
All 54 C# scripts now compile cleanly under Unity 2022.3.62f1.

| Error | File | Fix |
|-------|------|-----|
| `[Serializable]` missing `using System` | ToastNotification.cs | Added `using System;` |
| `WarningOrange` not found | ToastNotification.cs | Fixed to `WarningAmber` (matches UITheme field) |
| `ToastNotification.Instance?.Show()` — static method called on instance | 8 files (Warfare/Multiplayer) | Replaced with `ToastNotification.Show()` |
| `NPCProfession` missing Scholar, Priest, Spy | NPCPersonaSystem.cs | Added 3 values to enum |
| `OnNPCTapped` not defined | CastleViewUI.cs | Fixed to `FindObjectOfType<NPCInteractionUI>()?.OpenForNPC()` |
| Yield inside try/catch | SpyInfiltrationSystem.cs | Moved JSON parse before yield, kept error handling |
| `TouchPhase` ambiguous (InputSystem vs UnityEngine) | InputHandler.cs | Added `using TouchPhase = UnityEngine.TouchPhase;` |
| `new()` inference fails for Dictionary ValueCollection | ResearchSystem.cs | Changed to `new List<Technology>(...)` explicit |
| `NPCSpriteController` class missing (removed in 3D cleanup) | AssetCreator.cs (Editor) | Removed the AddComponent block, left comment |

### Unity License Status
- License activated: Unity Personal (User ID: 14569738874928)
- License file: `C:\Users\gangd\AppData\Local\Unity\licenses\UnityEntitlementLicense.xml`
- New XML format (not old .ulf)

### CI Pipeline Status
- **Validate job**: Runs on every push to master — PASSING
- **Build jobs**: Still gated behind `UNITY_BUILD_ENABLED=true` + secrets
- **Remaining manual step**: Set `UNITY_EMAIL` + `UNITY_PASSWORD` as GitHub secrets
  - Go to: https://github.com/taeshin11/LittleLordMajesty/settings/secrets/actions
  - Add `UNITY_EMAIL` (your Unity account email)
  - Add `UNITY_PASSWORD` (your Unity account password)
  - Then run activate-unity.yml → enable-builds

## Script File Count
- 54 total C# scripts
- 0 compile errors
- 4 harmless unused-variable warnings (CS0414, CS0067)

## Commits
- `5f6615a` — Fix all compile errors: ToastNotification, NPCProfession, TouchPhase, yield/try
- `6a06e1b` — Fix AssetCreator.cs: remove NPCSpriteController ref (removed in 3D cleanup)

## Next Up
- Enable Unity CI builds (needs UNITY_EMAIL + UNITY_PASSWORD secrets)
- Open Unity Editor and run SceneAutoBuilder + AssetCreator editor scripts
- Add missing game scenes in Unity Editor
- Begin playtesting with Gemini API integration
