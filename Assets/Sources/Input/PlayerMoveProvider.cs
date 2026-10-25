using System;
using System.Collections;
using UnityEngine;

public class PlayerMoveProvider
{
    private readonly PlayerInput _input;
    private readonly BoardRaycaster _raycaster;

    public PlayerMoveProvider(PlayerInput input, BoardRaycaster raycaster)
    {
        _input = input;
        _raycaster = raycaster;
    }

    public IEnumerator WaitForMove(Action<Vector2Int> onMove)
    {
        bool clicked = false;
        Vector2 lastScreenPosition = default;

        void OnClick(Vector2 position)
        {
            lastScreenPosition = position;
            clicked = true;
        }

        _input.OnClick.AddListener(OnClick);
        try
        {
            yield return new WaitUntil(() => clicked);
        }
        finally
        {
            _input.OnClick.RemoveListener(OnClick);
        }

        if (_raycaster.TryGetCell(lastScreenPosition, out Vector2Int cell))
            onMove?.Invoke(cell);
    }
}
