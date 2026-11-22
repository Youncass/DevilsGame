using UnityEngine;
using UnityEngine.UI;

public class CameraBehaviourView : MonoBehaviour
{
    [SerializeField] private CameraBehaviour _cb;
    [SerializeField] private Button _zoomInButton, _zoomOutButton;
    [SerializeField] private Animator _zoomInAnimator, _zoomOutAnimator;

    private const string ANIMATION_KEY = "Zoomed Out";

    private void OnEnable()
    {
        _cb.StateChanged += ApplyState;

        _zoomInButton.onClick.AddListener(_cb.ZoomIn);
        _zoomOutButton.onClick.AddListener(_cb.ZoomOut);

        ApplyState(_cb.CurrentState);
    }

    private void ApplyState(CameraBehaviour.CameraState state)
    {
        var zoomedOut = state == CameraBehaviour.CameraState.ZoomedOut;

        _zoomInAnimator.SetBool(ANIMATION_KEY, zoomedOut);
        _zoomOutAnimator.SetBool(ANIMATION_KEY, zoomedOut);
    }

    private void OnDisable()
    {
        if (_cb)
            _cb.StateChanged -= ApplyState;
    }
}
