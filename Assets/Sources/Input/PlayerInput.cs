using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class PlayerInput : MonoBehaviour
{
    /// <summary>Клик (тап) — отпускание без существенного движения.</summary>
    public UnityEvent<Vector2> OnClick { get; private set; } = new();
    /// <summary>Пас: P на ПК, два пальца на мобильном.</summary>
    public UnityEvent OnPass { get; private set; } = new();

    /// <summary>Дельта перемещения пальца/мыши за кадр (в пикселях). Вызывается каждый кадр, пока зажато.</summary>
    public UnityEvent<Vector2> OnDrag { get; private set; } = new();

    [SerializeField] private float _clickMaxMovement = 10f; // пикселей — до этого считается кликом
    [SerializeField] private float _dragThreshold = 3f;     // пикселей — отсечка дрожания для драга

    private Vector2 _pressPosition;
    private Vector2 _lastPosition;
    private bool _isPressing;
    private bool _isDragging;
    private bool _pointerActive; // true если в этом кадре уже обработали мышь ИЛИ тач (защита от двойного события)
    private int _pointerId = -1; // -1 — мышь, иначе fingerId

    private void Update()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetKeyDown(KeyCode.P))
            OnPass.Invoke();
#else
        if (Input.touchCount >= 2)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                if (Input.GetTouch(i).phase == TouchPhase.Ended)
                {
                    OnPass.Invoke();
                    return;
                }
            }
        }
#endif
        _pointerActive = false;

        HandleTouch(); // на мобилке — приоритет тачу
        if (!_pointerActive)
            HandleMouse(); // на ПК — мышь
    }

    private void BeginPress(Vector2 position, int pointerId)
    {
        // Нажатие началось на UI — доска его не получает (ни клик, ни драг).
        if (IsPointerOverUi(pointerId))
        {
            _isPressing = false;
            _isDragging = false;
            return;
        }

        _pointerId = pointerId;
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

        if (!_isDragging && !IsPointerOverUi(_pointerId))
            OnClick.Invoke(position);

        _isDragging = false;
        _pointerId = -1;
    }

    private static bool IsPointerOverUi(int pointerId)
    {
        EventSystem system = EventSystem.current;
        if (system == null) return false;

        return pointerId < 0
            ? system.IsPointerOverGameObject()
            : system.IsPointerOverGameObject(pointerId);
    }

    private void HandleMouse()
    {
        if (Input.GetMouseButtonDown(0))
            BeginPress(Input.mousePosition, -1);

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
                BeginPress(touch.position, touch.fingerId);
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
                _pointerId = -1;
                break;
        }
    }
}
