using UnityEngine;
using UnityEngine.InputSystem;
using System;
using TouchPhase = UnityEngine.TouchPhase;

/// <summary>
/// Centralised input handler using Unity's New Input System.
/// Routes touch, click, and keyboard events to the appropriate game systems.
/// Attach to a persistent "InputHandler" GameObject in the Bootstrap scene.
/// </summary>
public class InputHandler : MonoBehaviour
{
    public static InputHandler Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private float _pinchZoomSensitivity  = 0.02f;
    [SerializeField] private float _swipeMinDistance      = 60f;   // pixels
    [SerializeField] private float _tapMaxDuration        = 0.25f; // seconds
    [SerializeField] private float _longPressThreshold    = 0.6f;  // seconds

    // ── Events ───────────────────────────────────────────────────
    /// Single tap/click - world position
    public event Action<Vector2> OnTap;
    /// Long press - world position
    public event Action<Vector2> OnLongPress;
    /// Swipe - direction vector (normalized)
    public event Action<Vector2> OnSwipe;
    /// Pinch - delta scale (positive = expand, negative = shrink)
    public event Action<float> OnPinchZoom;
    /// Escape / back button
    public event Action OnBack;

    // ── State ────────────────────────────────────────────────────
    private bool    _isTouching;
    private Vector2 _touchStartPos;
    private float   _touchStartTime;
    private bool    _longPressTriggered;
    private float   _prevPinchDistance;

    private Camera _mainCam;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        _mainCam = Camera.main;
    }

    private void Update()
    {
        HandleEscapeKey();

        if (Application.isMobilePlatform || Application.isEditor)
            HandleTouchInput();
        else
            HandleMouseInput();
    }

    // ─────────────────────────────────────────────────────────────
    //  TOUCH (mobile + editor simulation)
    // ─────────────────────────────────────────────────────────────

    private void HandleTouchInput()
    {
        int touchCount = Input.touchCount;

        if (touchCount == 0)
        {
            _isTouching = false;
            _longPressTriggered = false;
            return;
        }

        if (touchCount == 1)
        {
            Touch t = Input.GetTouch(0);
            HandleSingleTouch(t);
        }
        else if (touchCount == 2)
        {
            HandlePinch(Input.GetTouch(0), Input.GetTouch(1));
        }
    }

    private void HandleSingleTouch(Touch touch)
    {
        switch (touch.phase)
        {
            case TouchPhase.Began:
                _touchStartPos  = touch.position;
                _touchStartTime = Time.realtimeSinceStartup;
                _isTouching     = true;
                _longPressTriggered = false;
                break;

            case TouchPhase.Stationary:
            case TouchPhase.Moved:
                if (_isTouching && !_longPressTriggered)
                {
                    float held = Time.realtimeSinceStartup - _touchStartTime;
                    float moved = Vector2.Distance(touch.position, _touchStartPos);
                    if (held >= _longPressThreshold && moved < 20f)
                    {
                        _longPressTriggered = true;
                        OnLongPress?.Invoke(ScreenToWorld(touch.position));
                    }
                }
                break;

            case TouchPhase.Ended:
                if (!_isTouching) break;

                float duration = Time.realtimeSinceStartup - _touchStartTime;
                float distance = Vector2.Distance(touch.position, _touchStartPos);

                if (!_longPressTriggered)
                {
                    if (duration <= _tapMaxDuration && distance < 20f)
                    {
                        OnTap?.Invoke(ScreenToWorld(touch.position));
                    }
                    else if (distance >= _swipeMinDistance)
                    {
                        OnSwipe?.Invoke((touch.position - _touchStartPos).normalized);
                    }
                }

                _isTouching = false;
                _longPressTriggered = false;
                break;
        }
    }

    private void HandlePinch(Touch t0, Touch t1)
    {
        float currentDist = Vector2.Distance(t0.position, t1.position);

        if (t0.phase == TouchPhase.Began || t1.phase == TouchPhase.Began)
        {
            _prevPinchDistance = currentDist;
            return;
        }

        float delta = (currentDist - _prevPinchDistance) * _pinchZoomSensitivity;
        if (Mathf.Abs(delta) > 0.001f)
            OnPinchZoom?.Invoke(delta);

        _prevPinchDistance = currentDist;
    }

    // ─────────────────────────────────────────────────────────────
    //  MOUSE (editor / desktop)
    // ─────────────────────────────────────────────────────────────

    private void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            _touchStartPos  = Input.mousePosition;
            _touchStartTime = Time.realtimeSinceStartup;
            _longPressTriggered = false;
        }

        if (Input.GetMouseButton(0) && !_longPressTriggered)
        {
            float held  = Time.realtimeSinceStartup - _touchStartTime;
            float moved = Vector2.Distance(Input.mousePosition, _touchStartPos);
            if (held >= _longPressThreshold && moved < 10f)
            {
                _longPressTriggered = true;
                OnLongPress?.Invoke(ScreenToWorld(Input.mousePosition));
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            float duration = Time.realtimeSinceStartup - _touchStartTime;
            float distance = Vector2.Distance(Input.mousePosition, _touchStartPos);

            if (!_longPressTriggered)
            {
                if (duration <= _tapMaxDuration && distance < 10f)
                    OnTap?.Invoke(ScreenToWorld(Input.mousePosition));
                else if (distance >= _swipeMinDistance)
                    OnSwipe?.Invoke(((Vector2)Input.mousePosition - _touchStartPos).normalized);
            }
            _longPressTriggered = false;
        }

        // Mouse wheel zoom
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
            OnPinchZoom?.Invoke(scroll * 5f);
    }

    // ─────────────────────────────────────────────────────────────
    //  KEYBOARD
    // ─────────────────────────────────────────────────────────────

    private void HandleEscapeKey()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            OnBack?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────────────────────

    private Vector2 ScreenToWorld(Vector2 screenPos)
    {
        if (_mainCam == null) _mainCam = Camera.main;
        if (_mainCam == null) return screenPos;
        return _mainCam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, _mainCam.nearClipPlane));
    }
}
