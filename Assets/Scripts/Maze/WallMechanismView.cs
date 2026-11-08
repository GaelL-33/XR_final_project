using System.Collections;
using UnityEngine;

public sealed class WallMechanismView : MonoBehaviour
{
    public MazeState mazeState;
    public string mechanismId;
    public Vector3 raisedLocalPosition;
    public Vector3 loweredLocalOffset = Vector3.down * 5f;
    [Min(0f)] public float animationSeconds = 0.45f;

    private Coroutine movement;

    private void Start()
    {
        if (mazeState == null)
        {
            Debug.LogError("WallMechanismView needs a MazeState reference.", this);
            return;
        }

        mazeState.MechanismStateChanged += OnMechanismStateChanged;
        MoveToState(mazeState.IsLowered(mechanismId), false);
    }

    private void OnDestroy()
    {
        if (mazeState != null)
            mazeState.MechanismStateChanged -= OnMechanismStateChanged;
    }

    private void OnMechanismStateChanged(string changedId, bool lowered)
    {
        if (changedId == mechanismId)
            MoveToState(lowered, true);
    }

    private void MoveToState(bool lowered, bool animate)
    {
        if (movement != null)
            StopCoroutine(movement);

        var destination = raisedLocalPosition + (lowered ? loweredLocalOffset : Vector3.zero);
        if (!animate || !isActiveAndEnabled || animationSeconds <= 0f)
        {
            transform.localPosition = destination;
            movement = null;
            return;
        }

        movement = StartCoroutine(AnimatePosition(destination));
    }

    private IEnumerator AnimatePosition(Vector3 destination)
    {
        var start = transform.localPosition;
        var elapsed = 0f;
        while (elapsed < animationSeconds)
        {
            elapsed += Time.deltaTime;
            transform.localPosition = Vector3.Lerp(start, destination, Mathf.Clamp01(elapsed / animationSeconds));
            yield return null;
        }

        transform.localPosition = destination;
        movement = null;
    }
}
