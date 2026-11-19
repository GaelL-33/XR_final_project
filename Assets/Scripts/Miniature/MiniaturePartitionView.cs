using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public sealed class MiniaturePartitionView : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    public MazeState mazeState;
    public ProceduralMazeAudio mazeAudio;
    public XRHapticsService haptics;
    public Transform partitionTrackRoot;
    public Transform slidingVisualRoot;
    public Transform grabHandleRoot;
    public XRSimpleInteractable grabHandleInteractable;
    public Collider grabHandleCollider;
    public MiniatureWallTarget pointerTarget;
    public GameObject lockedClaspVisual;
    public Renderer entryDetentRenderer;
    public Renderer exitDetentRenderer;
    public Renderer[] handleGlowRenderers;

    [Header("Miniature Track Coordinates (1:100)")]
    public float miniRailX = 0.03f;
    public float entryMiniZ = 0.011f;
    public float exitMiniZ = -0.011f;
    [Min(0.05f)] public float slideSeconds = 0.3f;

    [Header("Feedback Colors")]
    public Color activeDetentColor = new Color(0.28f, 0.88f, 0.82f, 1f);
    public Color intendedDetentColor = new Color(1f, 0.84f, 0.34f, 1f);
    public Color inactiveDetentColor = new Color(0.32f, 0.34f, 0.38f, 1f);
    public Color handleIdleColor = new Color(0.95f, 0.78f, 0.32f, 1f);
    public Color handleGrabbedColor = new Color(0.32f, 0.88f, 1f, 1f);

    private MaterialPropertyBlock propertyBlock;
    private IXRSelectInteractor draggingInteractor;
    private float dragGrabOffsetZ;
    private float dragStartMiniZ;
    private float dragStartTime;
    private bool isDragging;
    private bool isHovered;
    private AtriumPartitionPosition dragIntendedPosition;
    private Coroutine slideCoroutine;
    private Coroutine handleRevealCoroutine;
    private Vector3 handleFullLocalScale = Vector3.one;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        if (grabHandleRoot != null)
            handleFullLocalScale = grabHandleRoot.localScale;
    }

    private void OnEnable()
    {
        if (mazeState != null)
        {
            mazeState.PartitionUnlockedChanged += OnPartitionUnlockedChanged;
            mazeState.PartitionPositionChanged += OnPartitionPositionChanged;
            mazeState.PartitionMoveBlocked += OnPartitionMoveBlocked;
            mazeState.MazeReset += OnMazeReset;
        }

        if (grabHandleInteractable != null)
        {
            grabHandleInteractable.hoverEntered.AddListener(OnHandleHoverEntered);
            grabHandleInteractable.hoverExited.AddListener(OnHandleHoverExited);
            grabHandleInteractable.selectEntered.AddListener(OnHandleSelectEntered);
            grabHandleInteractable.selectExited.AddListener(OnHandleSelectExited);
        }
    }

    private void Start()
    {
        if (mazeState != null && mazeState.Definition != null && mazeState.Definition.atriumPartition != null)
        {
            var scale = mazeState.Definition.metresToMiniatureScale;
            var def = mazeState.Definition.atriumPartition;
            miniRailX = def.railWorldX * scale;
            entryMiniZ = def.entryPositionWorldZ * scale;
            exitMiniZ = def.exitPositionWorldZ * scale;
        }

        SyncUnlockedState(mazeState != null && mazeState.IsPartitionUnlocked, false);
        MoveVisualTo(mazeState != null ? mazeState.PartitionPosition : AtriumPartitionPosition.IslandToExit, false);
        UpdateIndicators();
    }

    private void OnDisable()
    {
        if (mazeState != null)
        {
            mazeState.PartitionUnlockedChanged -= OnPartitionUnlockedChanged;
            mazeState.PartitionPositionChanged -= OnPartitionPositionChanged;
            mazeState.PartitionMoveBlocked -= OnPartitionMoveBlocked;
            mazeState.MazeReset -= OnMazeReset;
        }

        if (grabHandleInteractable != null)
        {
            grabHandleInteractable.hoverEntered.RemoveListener(OnHandleHoverEntered);
            grabHandleInteractable.hoverExited.RemoveListener(OnHandleHoverExited);
            grabHandleInteractable.selectEntered.RemoveListener(OnHandleSelectEntered);
            grabHandleInteractable.selectExited.RemoveListener(OnHandleSelectExited);
        }
    }

    private void Update()
    {
        if (!isDragging || draggingInteractor == null || partitionTrackRoot == null || slidingVisualRoot == null)
            return;

        if (mazeState == null || !mazeState.IsPartitionUnlocked)
        {
            isDragging = false;
            draggingInteractor = null;
            return;
        }

        var attachTransform = draggingInteractor.GetAttachTransform(grabHandleInteractable);
        var interactorWorldPos = attachTransform != null ? attachTransform.position : draggingInteractor.transform.position;
        var localPos = partitionTrackRoot.InverseTransformPoint(interactorWorldPos);

        var minZ = Mathf.Min(entryMiniZ, exitMiniZ);
        var maxZ = Mathf.Max(entryMiniZ, exitMiniZ);
        var clampedZ = Mathf.Clamp(localPos.z - dragGrabOffsetZ, minZ, maxZ);

        var currentLocal = slidingVisualRoot.localPosition;
        currentLocal.x = miniRailX;
        currentLocal.z = clampedZ;
        slidingVisualRoot.localPosition = currentLocal;

        var midpoint = (entryMiniZ + exitMiniZ) * 0.5f;
        var nextIntended = clampedZ >= midpoint
            ? AtriumPartitionPosition.EntryToIsland
            : AtriumPartitionPosition.IslandToExit;

        if (nextIntended != dragIntendedPosition)
        {
            dragIntendedPosition = nextIntended;
            if (mazeAudio != null)
                mazeAudio.PlayOneShot3D(MazeSoundId.PartitionDetent, grabHandleRoot != null ? grabHandleRoot.position : transform.position, 0.65f, 1.15f);
            if (haptics != null)
                haptics.PlayDetentTick();
            UpdateIndicators();
        }
    }

    private void OnPartitionUnlockedChanged(bool unlocked)
    {
        SyncUnlockedState(unlocked, true);
        UpdateIndicators();
    }

    private void OnPartitionPositionChanged(AtriumPartitionPosition position)
    {
        if (!isDragging)
            MoveVisualTo(position, true);
        UpdateIndicators();
    }

    private void OnPartitionMoveBlocked(string reason)
    {
        if (mazeState != null)
            MoveVisualTo(mazeState.PartitionPosition, true);
        UpdateIndicators();
    }

    private void OnMazeReset()
    {
        isDragging = false;
        isHovered = false;
        draggingInteractor = null;
        SyncUnlockedState(mazeState != null && mazeState.IsPartitionUnlocked, false);
        MoveVisualTo(mazeState != null ? mazeState.PartitionPosition : AtriumPartitionPosition.IslandToExit, false);
        UpdateIndicators();
    }

    private void OnHandleHoverEntered(HoverEnterEventArgs args)
    {
        isHovered = true;
        UpdateIndicators();
        if (haptics != null)
            haptics.PlayMiniatureHover();
    }

    private void OnHandleHoverExited(HoverExitEventArgs args)
    {
        isHovered = false;
        UpdateIndicators();
    }

    private void OnHandleSelectEntered(SelectEnterEventArgs args)
    {
        if (mazeState == null || !mazeState.IsPartitionUnlocked || partitionTrackRoot == null || slidingVisualRoot == null)
            return;

        if (slideCoroutine != null)
        {
            StopCoroutine(slideCoroutine);
            slideCoroutine = null;
        }

        draggingInteractor = args.interactorObject;
        var attachTransform = draggingInteractor.GetAttachTransform(grabHandleInteractable);
        var interactorWorldPos = attachTransform != null ? attachTransform.position : draggingInteractor.transform.position;
        var localInteractorPos = partitionTrackRoot.InverseTransformPoint(interactorWorldPos);

        dragStartMiniZ = slidingVisualRoot.localPosition.z;
        dragGrabOffsetZ = localInteractorPos.z - dragStartMiniZ;
        dragStartTime = Time.time;
        dragIntendedPosition = mazeState.PartitionPosition;
        isDragging = true;

        if (haptics != null)
            haptics.PlayMiniatureClick();
        UpdateIndicators();
    }

    private void OnHandleSelectExited(SelectExitEventArgs args)
    {
        if (!isDragging)
            return;

        isDragging = false;
        draggingInteractor = null;

        if (mazeState == null)
            return;

        var currentZ = slidingVisualRoot != null ? slidingVisualRoot.localPosition.z : dragStartMiniZ;
        var draggedDistance = Mathf.Abs(currentZ - dragStartMiniZ);
        var quickTap = draggedDistance < 0.0025f && (Time.time - dragStartTime) < 0.35f;

        var target = quickTap
            ? (mazeState.PartitionPosition == AtriumPartitionPosition.EntryToIsland
                ? AtriumPartitionPosition.IslandToExit
                : AtriumPartitionPosition.EntryToIsland)
            : dragIntendedPosition;

        if (!mazeState.TrySetPartitionPosition(target, out _))
        {
            // Refused (e.g., player occupying threshold): spring smoothly back to current valid state.
            MoveVisualTo(mazeState.PartitionPosition, true);
        }
        else
        {
            MoveVisualTo(mazeState.PartitionPosition, true);
            if (pointerTarget != null)
                pointerTarget.PulseConfirmed();
        }

        UpdateIndicators();
    }

    private void SyncUnlockedState(bool unlocked, bool animateReveal)
    {
        if (lockedClaspVisual != null)
            lockedClaspVisual.SetActive(!unlocked);

        if (grabHandleCollider != null)
            grabHandleCollider.enabled = unlocked;

        if (grabHandleInteractable != null)
            grabHandleInteractable.enabled = unlocked;

        if (pointerTarget != null)
            pointerTarget.SetAvailable(unlocked);

        if (grabHandleRoot == null)
            return;

        if (handleRevealCoroutine != null)
        {
            StopCoroutine(handleRevealCoroutine);
            handleRevealCoroutine = null;
        }

        if (!unlocked)
        {
            grabHandleRoot.gameObject.SetActive(false);
            grabHandleRoot.localScale = Vector3.zero;
            return;
        }

        grabHandleRoot.gameObject.SetActive(true);
        if (!animateReveal || !isActiveAndEnabled)
        {
            grabHandleRoot.localScale = handleFullLocalScale;
            return;
        }

        handleRevealCoroutine = StartCoroutine(AnimateHandleReveal());
    }

    private IEnumerator AnimateHandleReveal()
    {
        var elapsed = 0f;
        const float duration = 0.45f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            // Slight overshoot for readable tactile reveal on the miniature
            var overshoot = t + Mathf.Sin(t * Mathf.PI) * 0.14f;
            grabHandleRoot.localScale = handleFullLocalScale * overshoot;
            yield return null;
        }

        grabHandleRoot.localScale = handleFullLocalScale;
        handleRevealCoroutine = null;
    }

    private void MoveVisualTo(AtriumPartitionPosition target, bool animate)
    {
        if (slidingVisualRoot == null)
            return;

        if (slideCoroutine != null)
        {
            StopCoroutine(slideCoroutine);
            slideCoroutine = null;
        }

        var targetZ = target == AtriumPartitionPosition.EntryToIsland ? entryMiniZ : exitMiniZ;
        var destination = new Vector3(miniRailX, slidingVisualRoot.localPosition.y, targetZ);

        if (!animate || !isActiveAndEnabled || slideSeconds <= 0f)
        {
            slidingVisualRoot.localPosition = destination;
            return;
        }

        slideCoroutine = StartCoroutine(AnimateMiniatureSlide(destination));
    }

    private IEnumerator AnimateMiniatureSlide(Vector3 destination)
    {
        var start = slidingVisualRoot.localPosition;
        var elapsed = 0f;
        while (elapsed < slideSeconds)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / slideSeconds));
            slidingVisualRoot.localPosition = Vector3.Lerp(start, destination, t);
            yield return null;
        }

        slidingVisualRoot.localPosition = destination;
        slideCoroutine = null;
    }

    private void UpdateIndicators()
    {
        var unlocked = mazeState != null && mazeState.IsPartitionUnlocked;
        var currentPos = mazeState != null ? mazeState.PartitionPosition : AtriumPartitionPosition.IslandToExit;

        var entryIsActive = unlocked && currentPos == AtriumPartitionPosition.EntryToIsland;
        var exitIsActive = unlocked && currentPos == AtriumPartitionPosition.IslandToExit;
        var entryIsIntended = isDragging && dragIntendedPosition == AtriumPartitionPosition.EntryToIsland;
        var exitIsIntended = isDragging && dragIntendedPosition == AtriumPartitionPosition.IslandToExit;

        ApplyRendererColor(entryDetentRenderer,
            entryIsIntended ? intendedDetentColor : entryIsActive ? activeDetentColor : inactiveDetentColor,
            entryIsIntended ? 1.5f : entryIsActive ? 1.0f : 0f);

        ApplyRendererColor(exitDetentRenderer,
            exitIsIntended ? intendedDetentColor : exitIsActive ? activeDetentColor : inactiveDetentColor,
            exitIsIntended ? 1.5f : exitIsActive ? 1.0f : 0f);

        if (handleGlowRenderers != null)
        {
            var handleColor = (isDragging || isHovered) ? handleGrabbedColor : handleIdleColor;
            var emission = unlocked ? ((isDragging || isHovered) ? 1.35f : 0.7f) : 0f;
            for (var i = 0; i < handleGlowRenderers.Length; i++)
                ApplyRendererColor(handleGlowRenderers[i], handleColor, emission);
        }
    }

    private void ApplyRendererColor(Renderer targetRenderer, Color baseColor, float emissionMultiplier)
    {
        if (targetRenderer == null)
            return;

        propertyBlock ??= new MaterialPropertyBlock();
        targetRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(BaseColorId, baseColor);
        propertyBlock.SetColor(EmissionColorId, emissionMultiplier > 0.01f ? baseColor * emissionMultiplier : Color.black);
        targetRenderer.SetPropertyBlock(propertyBlock);
    }
}
