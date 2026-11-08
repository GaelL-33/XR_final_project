using System.Collections;
using UnityEngine;

public sealed class MiniatureMechanismView : MonoBehaviour
{
    public MazeState mazeState;
    public string mechanismId;
    public Transform visual;
    public Vector3 raisedLocalPosition;
    public Vector3 loweredLocalOffset = Vector3.down * 0.05f;
    [Min(0f)] public float animationSeconds = 0.2f;

    private Coroutine movement;

    private void Start()
    {
        if (mazeState == null || visual == null)
        {
            Debug.LogError("MiniatureMechanismView needs a MazeState and visual reference.", this);
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
            visual.localPosition = destination;
            movement = null;
            return;
        }

        movement = StartCoroutine(AnimatePosition(destination));
    }

    private IEnumerator AnimatePosition(Vector3 destination)
    {
        var start = visual.localPosition;
        var elapsed = 0f;
        while (elapsed < animationSeconds)
        {
            elapsed += Time.deltaTime;
            visual.localPosition = Vector3.Lerp(start, destination, Mathf.Clamp01(elapsed / animationSeconds));
            yield return null;
        }

        visual.localPosition = destination;
        movement = null;
    }
}
