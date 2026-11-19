using System.Collections;
using UnityEngine;

public sealed class WallMechanismView : MonoBehaviour
{
    public MazeState mazeState;
    public ProceduralMazeAudio mazeAudio;
    public XRHapticsService haptics;
    public string mechanismId;
    public Vector3 raisedLocalPosition;
    public Vector3 loweredLocalOffset = Vector3.down * 5f;
    [Min(0f)] public float animationSeconds = 0.65f;
    public bool disableCollidersWhenLowered;

    private Coroutine movement;
    private Collider[] cachedColliders;
    private bool initialized;

    private void Awake()
    {
        cachedColliders = GetComponentsInChildren<Collider>(true);
    }

    private void Start()
    {
        if (mazeState == null)
        {
            Debug.LogError("WallMechanismView needs a MazeState reference.", this);
            return;
        }

        mazeState.MechanismStateChanged += OnMechanismStateChanged;
        mazeState.MazeReset += OnMazeReset;
        MoveToState(mazeState.IsLowered(mechanismId), false);
        initialized = true;
    }

    private void OnDestroy()
    {
        if (mazeState != null)
        {
            mazeState.MechanismStateChanged -= OnMechanismStateChanged;
            mazeState.MazeReset -= OnMazeReset;
        }
    }

    private void OnMechanismStateChanged(string changedId, bool lowered)
    {
        if (changedId != mechanismId)
            return;

        MoveToState(lowered, initialized);
        if (initialized)
        {
            if (mazeAudio != null)
                mazeAudio.PlayOneShot3D(MazeSoundId.StoneGateMove, transform.position + Vector3.up * 1.5f, 0.9f);
            if (haptics != null)
                haptics.PlayMechanismRumble();
        }
    }

    private void OnMazeReset()
    {
        if (mazeState != null)
            MoveToState(mazeState.IsLowered(mechanismId), false);
    }

    private void MoveToState(bool lowered, bool animate)
    {
        if (movement != null)
            StopCoroutine(movement);

        var destination = raisedLocalPosition + (lowered ? loweredLocalOffset : Vector3.zero);
        if (!animate || !isActiveAndEnabled || animationSeconds <= 0f)
        {
            transform.localPosition = destination;
            SetCollidersEnabled(!lowered || !disableCollidersWhenLowered);
            movement = null;
            return;
        }

        if (!lowered)
            SetCollidersEnabled(true);

        movement = StartCoroutine(AnimatePosition(destination, lowered));
    }

    private IEnumerator AnimatePosition(Vector3 destination, bool lowered)
    {
        var start = transform.localPosition;
        var elapsed = 0f;
        while (elapsed < animationSeconds)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / animationSeconds));
            transform.localPosition = Vector3.Lerp(start, destination, t);
            yield return null;
        }

        transform.localPosition = destination;
        if (lowered && disableCollidersWhenLowered)
            SetCollidersEnabled(false);
        movement = null;
    }

    private void SetCollidersEnabled(bool enabledState)
    {
        if (cachedColliders == null)
            return;

        for (var i = 0; i < cachedColliders.Length; i++)
        {
            if (cachedColliders[i] != null)
                cachedColliders[i].enabled = enabledState;
        }
    }
}
