using System;
using UnityEngine;
using PrimeTween;

public class CameraBehaviour : MonoBehaviour
{
    public enum CameraState { CloseUp, ZoomedOut }

    [Header("References")]
    [SerializeField] private PlayerInput _input;
    [SerializeField] private Transform _point1;
    [SerializeField] private Transform _point2;

    [Header("Orbit")]
    [SerializeField] private float _rotationSpeed = 0.2f;
    [SerializeField] private float _minPitch = -20f;
    [SerializeField] private float _maxPitch = 60f;

    [Header("Transition")]
    [SerializeField, Min(0.01f)] private float _transitionDuration = 0.5f;

    private float _yaw;
    private float _pitch;
    private bool _isTransitioning;
    private CameraState _currentState = CameraState.CloseUp;

    private Tween _rotationTween;
    private Tween _positionTween;

    public CameraState CurrentState => _currentState;

    public bool IsBoardInteractive => _currentState == CameraState.CloseUp && !_isTransitioning;

    public event Action<CameraState> StateChanged;

    private void Awake()
    {
        if (!ValidateReferences())
        {
            enabled = false;
            return;
        }
    }

    private void Start()
    {
        _input.OnDrag.AddListener(HandleDrag);
    }

    private bool ValidateReferences()
    {
        if (_point1 == null) Debug.LogError("[Camera] Не назначен _point1", this);
        if (_point2 == null) Debug.LogError("[Camera] Не назначен _point2", this);

        return _point1 != null && _point2 != null;
    }

    public void ZoomOut() => TransitionTo(CameraState.ZoomedOut);

    public void ZoomIn() => TransitionTo(CameraState.CloseUp);

    private void TransitionTo(CameraState target)
    {
        if (_currentState == target)
            return;

        Transform point = target == CameraState.ZoomedOut ? _point2 : _point1;

        _currentState = target;
        StateChanged?.Invoke(target);
        Debug.Log($"[Camera] {target}", this);

        _positionTween.Stop();
        _rotationTween.Stop();

        _isTransitioning = true;
        _positionTween = Tween.Position(transform, point.position, _transitionDuration, Ease.OutQuad);
        _rotationTween = Tween.Rotation(transform, point.rotation, _transitionDuration, Ease.OutQuad)
            .OnComplete(FinishTransition);
    }

    private void FinishTransition()
    {
        _isTransitioning = false;
        SyncAnglesFromTransform();
    }

    private void HandleDrag(Vector2 frameDelta)
    {
        if (_currentState != CameraState.ZoomedOut)
            return;

        if (_isTransitioning)
        {
            _rotationTween.Stop();
            _isTransitioning = false;
            SyncAnglesFromTransform();
        }

        _yaw += frameDelta.x * _rotationSpeed;
        _pitch = Mathf.Clamp(_pitch - frameDelta.y * _rotationSpeed, _minPitch, _maxPitch);

        transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
    }

    private void SyncAnglesFromTransform()
    {
        Vector3 e = transform.eulerAngles;
        _yaw = e.y;
        _pitch = NormalizeAngle(e.x);


        static float NormalizeAngle(float angle)
        {
            angle %= 360f;
            if (angle > 180f) angle -= 360f;
            return angle;
        }
    }

    private void OnDestroy()
    {
        if (_input)
            _input.OnDrag.RemoveListener(HandleDrag);
    }

#if UNITY_EDITOR
    [ContextMenu("Zoom In")]
    private void ZoomIn_Inspector() => transform.SetPositionAndRotation(_point1.position, _point1.rotation);

    [ContextMenu("Zoom Out")]
    private void ZoomOut_Inspector() => transform.SetPositionAndRotation(_point2.position, _point2.rotation);
#endif
}
