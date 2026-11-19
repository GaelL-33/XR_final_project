using System.Collections;
using UnityEngine;

public sealed class AtriumPartitionWorldView : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    public MazeState mazeState;
    public ProceduralMazeAudio mazeAudio;
    public XRHapticsService haptics;
    public Transform slidingPartitionRoot;
    public Renderer[] archTrimRenderers;
    public Renderer[] railTrackRenderers;

    [Header("Snap Coordinates (Maze Local)")]
    public float railLocalX = 3.0f;
    public float entryPositionLocalZ = 1.1f;
    public float exitPositionLocalZ = -1.1f;
    [Min(0.05f)] public float slideSeconds = 0.75f;

    [Header("Visual Feedback")]
    public Color lockedColor = new Color(0.28f, 0.25f, 0.26f, 1f);
    public Color unlockedColor = new Color(0.32f, 0.84f, 0.92f, 1f);
    public Color movingColor = new Color(1f, 0.78f, 0.3f, 1f);
    public Color blockedColor = new Color(1f, 0.36f, 0.18f, 1f);

    private MaterialPropertyBlock propertyBlock;
    private Coroutine slideCoroutine;
    private float blockedFlashTimer;
    private bool initialized;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        if (mazeState == null)
            return;

        mazeState.PartitionUnlockedChanged += OnPartitionUnlockedChanged;
        mazeState.PartitionPositionChanged += OnPartitionPositionChanged;
        mazeState.PartitionMoveBlocked += OnPartitionMoveBlocked;
        mazeState.MazeReset += OnMazeReset;
    }

    private void Start()
    {
        if (mazeState != null && mazeState.Definition != null && mazeState.Definition.atriumPartition != null)
        {
            var def = mazeState.Definition.atriumPartition;
            railLocalX = def.railWorldX;
            entryPositionLocalZ = def.entryPositionWorldZ;
            exitPositionLocalZ = def.exitPositionWorldZ;
            slideSeconds = def.slideSeconds;
        }

        if (mazeState != null)
            MoveToPosition(mazeState.PartitionPosition, false);

        ApplyVisualState();
        initialized = true;
    }

    private void OnDisable()
    {
        if (mazeState == null)
            return;

        mazeState.PartitionUnlockedChanged -= OnPartitionUnlockedChanged;
        mazeState.PartitionPositionChanged -= OnPartitionPositionChanged;
        mazeState.PartitionMoveBlocked -= OnPartitionMoveBlocked;
        mazeState.MazeReset -= OnMazeReset;
    }

    private void Update()
    {
        if (blockedFlashTimer > 0f)
        {
            blockedFlashTimer = Mathf.Max(0f, blockedFlashTimer - Time.deltaTime);
            ApplyVisualState();
        }
    }

    private void OnPartitionUnlockedChanged(bool unlocked)
    {
        ApplyVisualState();
        if (initialized && unlocked && mazeAudio != null)
        {
            var soundPos = slidingPartitionRoot != null ? slidingPartitionRoot.position + Vector3.up * 2f : transform.position;
            mazeAudio.PlayOneShot3D(MazeSoundId.PartitionDetent, soundPos, 0.85f, 0.9f);
        }
    }

    private void OnPartitionPositionChanged(AtriumPartitionPosition target)
    {
        MoveToPosition(target, initialized);
    }

    private void OnPartitionMoveBlocked(string reason)
    {
        blockedFlashTimer = 0.75f;
        ApplyVisualState();

        var soundPos = slidingPartitionRoot != null ? slidingPartitionRoot.position + Vector3.up * 1.5f : transform.position;
        if (mazeAudio != null)
            mazeAudio.PlayOneShot3D(MazeSoundId.BlockedAction, soundPos, 0.8f);
        if (haptics != null)
            haptics.PlayRejectionPulse();
    }

    private void OnMazeReset()
    {
        blockedFlashTimer = 0f;
        if (mazeState != null)
            MoveToPosition(mazeState.PartitionPosition, false);
        ApplyVisualState();
    }

    private void MoveToPosition(AtriumPartitionPosition target, bool animate)
    {
        if (slidingPartitionRoot == null)
            return;

        if (slideCoroutine != null)
        {
            StopCoroutine(slideCoroutine);
            slideCoroutine = null;
        }

        var targetZ = target == AtriumPartitionPosition.EntryToIsland ? entryPositionLocalZ : exitPositionLocalZ;
        var destination = new Vector3(railLocalX, slidingPartitionRoot.localPosition.y, targetZ);

        if (!animate || !isActiveAndEnabled || slideSeconds <= 0f)
        {
            slidingPartitionRoot.localPosition = destination;
            if (mazeState != null)
                mazeState.SetPartitionMoving(false);
            ApplyVisualState();
            return;
        }

        slideCoroutine = StartCoroutine(AnimateSlide(destination));
    }

    private IEnumerator AnimateSlide(Vector3 destination)
    {
        if (mazeState != null)
            mazeState.SetPartitionMoving(true);
        ApplyVisualState();

        if (mazeAudio != null)
            mazeAudio.PlayOneShot3D(MazeSoundId.PartitionSlide, slidingPartitionRoot.position + Vector3.up * 1.8f, 0.92f);
        if (haptics != null)
            haptics.PlayMechanismRumble();

        var start = slidingPartitionRoot.localPosition;
        var elapsed = 0f;
        while (elapsed < slideSeconds)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / slideSeconds));
            slidingPartitionRoot.localPosition = Vector3.Lerp(start, destination, t);
            yield return null;
        }

        slidingPartitionRoot.localPosition = destination;
        if (mazeState != null)
            mazeState.SetPartitionMoving(false);

        if (mazeAudio != null)
            mazeAudio.PlayOneShot3D(MazeSoundId.PartitionDetent, slidingPartitionRoot.position + Vector3.up * 1.8f, 0.85f);
        if (haptics != null)
            haptics.PlayDetentTick();

        ApplyVisualState();
        slideCoroutine = null;
    }

    private void ApplyVisualState()
    {
        var unlocked = mazeState != null && mazeState.IsPartitionUnlocked;
        var moving = mazeState != null && mazeState.IsPartitionMoving;

        Color activeColor;
        float emissionStrength;
        if (blockedFlashTimer > 0f)
        {
            activeColor = blockedColor;
            emissionStrength = 1.4f;
        }
        else if (!unlocked)
        {
            activeColor = lockedColor;
            emissionStrength = 0f;
        }
        else if (moving)
        {
            activeColor = movingColor;
            emissionStrength = 1.35f;
        }
        else
        {
            activeColor = unlockedColor;
            emissionStrength = 0.85f;
        }

        ApplyRenderers(archTrimRenderers, activeColor, emissionStrength);
        ApplyRenderers(railTrackRenderers, unlocked ? activeColor : lockedColor, unlocked ? emissionStrength * 0.65f : 0f);
    }

    private void ApplyRenderers(Renderer[] renderers, Color color, float emissionMultiplier)
    {
        if (renderers == null)
            return;

        propertyBlock ??= new MaterialPropertyBlock();
        var emission = emissionMultiplier > 0.01f ? color * emissionMultiplier : Color.black;
        for (var i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null)
                continue;

            r.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(EmissionColorId, emission);
            r.SetPropertyBlock(propertyBlock);
        }
    }
}
