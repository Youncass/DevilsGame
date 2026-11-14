using UnityEngine;
using UnityEngine.Events;

public class PlayerInput : MonoBehaviour
{
    public UnityEvent<Vector2> OnClick { get; private set; } = new();

    private void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.Mouse0))
            OnClick.Invoke(Input.mousePosition);
#else
        for (int i = 0; i < Input.touchCount; i++)
        {
            var touch = Input.GetTouch(i);
            if (touch.phase == TouchPhase.Ended)
            {
                OnClick.Invoke(touch.position);
                break;
            }
        }
#endif
    }
}
