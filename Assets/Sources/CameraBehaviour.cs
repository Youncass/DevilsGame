using UnityEngine;
using PrimeTween;

public class CameraBehaviour : MonoBehaviour
{
    public enum CameraState { CloseUp, ZoomedOut }
    public CameraState CurrentState { get; private set; } = CameraState.CloseUp;

    [SerializeField] private Transform _camera;
    [SerializeField] private Transform _point1;
    [SerializeField] private Transform _point2;
    [SerializeField] private GameObject _zoomOutButton;
    [SerializeField] private GameObject _boardClickArea;

    [Header("Rotation")]
    [SerializeField] private float _rotationSpeed = 0.2f;
    [SerializeField] private float _minPitch = -20f;
    [SerializeField] private float _maxPitch = 60f;
    [SerializeField] private float time = 1f;

    private PlayerInput _input;

    private float _yaw;
    private float _pitch;
    private bool _isTransitioning;

    // Хэндлы активных твинов (rotation для прерывания, position для отмены)
    private Tween _rotationTween;
    private Tween _positionTween;

    private void Awake()
    {
        _input = FindObjectOfType<PlayerInput>();
        _input.OnDrag.AddListener(HandleDrag);
    }

    private void Start()
    {
        _camera.position = _point1.position;
        _camera.rotation = _point1.rotation;
        _zoomOutButton.SetActive(true);
        if (_boardClickArea != null) _boardClickArea.SetActive(false);

        SyncAnglesFromTransform();
    }

    private void SyncAnglesFromTransform()
    {
        Vector3 e = _camera.eulerAngles;
        _yaw = e.y;
        _pitch = NormalizeAngle(e.x);
    }

    private static float NormalizeAngle(float a)
    {
        a %= 360f;
        if (a > 180f) a -= 360f;
        return a;
    }

    private void HandleDrag(Vector2 frameDelta)
    {
        if (CurrentState != CameraState.ZoomedOut) return;

        // Если летит rotation-твин — прерываем его и подхватываем текущий угол.
        // Дальше камера ведётся вручную от того места, где твин остановился.
        if (_isTransitioning)
            InterruptRotationTween();

        _yaw += frameDelta.x * _rotationSpeed;
        _pitch -= frameDelta.y * _rotationSpeed;
        _pitch = Mathf.Clamp(_pitch, _minPitch, _maxPitch);

        _camera.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
    }

    private void InterruptRotationTween()
    {
        if (_rotationTween.isAlive)
            _rotationTween.Stop();

        // Подхватываем текущий (промежуточный) угол камеры в _yaw/_pitch
        SyncAnglesFromTransform();
        _isTransitioning = false;
    }

    public void ZoomOut()
    {
        if (CurrentState == CameraState.ZoomedOut) return;
        CurrentState = CameraState.ZoomedOut;
        _zoomOutButton.SetActive(false);
        if (_boardClickArea != null) _boardClickArea.SetActive(true);

        _isTransitioning = true;

        // Позиция всегда доезжает до _point2 (вращение этому не мешает)
        _positionTween = Tween.Position(_camera, _point2.position, time, Ease.OutQuad);

        // Вращение — только если пользователь не начал драг. Иначе он управляет сам.
        _rotationTween = Tween.Rotation(_camera, _point2.rotation, time, Ease.OutQuad)
            .OnComplete(() =>
            {
                // Если твин дожил до конца — синхронизируем углы с финальным поворотом
                if (_isTransitioning)
                {
                    SyncAnglesFromTransform();
                    _isTransitioning = false;
                }
            });
    }

    public void ZoomIn()
    {
        if (CurrentState == CameraState.CloseUp) return;
        CurrentState = CameraState.CloseUp;

        // Если пользователь всё ещё вращает в момент возврата — прерываем rotation,
        // чтобы твин возврата взял управление с текущего угла, а не с «планового».
        if (_isTransitioning)
            InterruptRotationTween();

        _isTransitioning = true;

        _positionTween = Tween.Position(_camera, _point1.position, time, Ease.OutQuad);
        _rotationTween = Tween.Rotation(_camera, _point1.rotation, time, Ease.OutQuad)
            .OnComplete(() =>
            {
                if (_isTransitioning)
                {
                    SyncAnglesFromTransform();
                    _isTransitioning = false;
                }
            });

        _zoomOutButton.SetActive(true);
        if (_boardClickArea != null) _boardClickArea.SetActive(false);
    }
}
