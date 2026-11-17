using UnityEngine;
using UnityEngine.Events;

public class PlayerInput : MonoBehaviour
{
    public UnityEvent<Vector2> OnClick { get; private set; } = new();
    /// <summary>Пас: P на ПК, два пальца на мобильном.</summary>
    public UnityEvent OnPass { get; private set; } = new();

    private void Update()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetKeyDown(KeyCode.Mouse0))
            OnClick.Invoke(Input.mousePosition);
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
