using UnityEngine;
using UnityEngine.Events;

public class PlayerInput : MonoBehaviour
{
    /// <summary>Клик (тап) — отпускание без существенного движения.</summary>
    public UnityEvent<Vector2> OnClick { get; private set; } = new();

    /// <summary>Дельта перемещения пальца/мыши за кадр (в пикселях). Вызывается каждый кадр, пока зажато.</summary>
    public UnityEvent<Vector2> OnDrag { get; private set; } = new();

    [SerializeField] private float _clickMaxMovement = 10f; // пикселей — до этого считается кликом
    [SerializeField] private float _dragThreshold = 3f;     // пикселей — отсечка дрожания для драга

    private Vector2 _pressPosition;
    private Vector2 _lastPosition;
    private bool _isPressing;
    private bool _isDragging;
    private bool _pointerActive; // true если в этом кадре уже обработали мышь ИЛИ тач (защита от двойного события)

    private void Update()
    {
        _pointerActive = false;

        HandleTouch(); // на мобилке — приоритет тачу
        if (!_pointerActive)
            HandleMouse(); // на ПК — мышь
    }

    private void BeginPress(Vector2 position)
    {
        _pressPosition = position;
        _lastPosition = position;
        _isPressing = true;
        _isDragging = false;
        _pointerActive = true;
    }

    private void UpdatePress(Vector2 position)
    {
        _pointerActive = true;
        if (!_isPressing) return;

        Vector2 frameDelta = position - _lastPosition;

        // Драг начинается, как только общее смещение превысило кликовый порог
        if (!_isDragging && (position - _pressPosition).sqrMagnitude > _clickMaxMovement * _clickMaxMovement)
            _isDragging = true;

        if (_isDragging && frameDelta.sqrMagnitude > _dragThreshold * _dragThreshold)
            OnDrag.Invoke(frameDelta);

        _lastPosition = position;
    }

    private void EndPress(Vector2 position)
    {
        _pointerActive = true;
        if (!_isPressing) return;

        _isPressing = false;

        if (!_isDragging)
            OnClick.Invoke(position);

        _isDragging = false;
    }

    private void HandleMouse()
    {
        if (Input.GetMouseButtonDown(0))
            BeginPress(Input.mousePosition);

        if (Input.GetMouseButton(0))
            UpdatePress(Input.mousePosition);

        if (Input.GetMouseButtonUp(0))
            EndPress(Input.mousePosition);
    }

    private void HandleTouch()
    {
        if (Input.touchCount == 0) return;

        Touch touch = Input.GetTouch(0);

        switch (touch.phase)
        {
            case TouchPhase.Began:
                BeginPress(touch.position);
                break;
            case TouchPhase.Moved:
            case TouchPhase.Stationary:
                UpdatePress(touch.position);
                break;
            case TouchPhase.Ended:
                EndPress(touch.position);
                break;
            case TouchPhase.Canceled:
                _pointerActive = true;
                _isPressing = false;
                _isDragging = false;
                break;
        }
    }
}
