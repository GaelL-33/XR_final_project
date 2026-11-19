using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public sealed class ExitLeverView : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    public MazeState mazeState;
    public ProceduralMazeAudio mazeAudio;
    public XRHapticsService haptics;
    public Transform leverPivot;
    public XRSimpleInteractable handleInteractable;
    public Renderer[] indicatorRenderers;

    [Header("Lever Motion")]
    public float upAngleX = -38f;
    public float downAngleX = 42f;
    [Range(0.2f, 0.9f)] public float commitThreshold = 0.55f;
    [Min(0.05f)] public float pullAnimateSeconds = 0.42f;

    [Header("Visual Feedback")]
    public Color idleColor = new Color(0.95f, 0.72f, 0.28f, 1f);
    public Color hoverColor = new Color(0.35f, 0.88f, 1f, 1f);
    public Color pulledColor = new Color(1f, 0.9f, 0.45f, 1f);

    private MaterialPropertyBlock propertyBlock;
    private IXRSelectInteractor pullingInteractor;
    private float pullStartWorldY;
    private float pullStartTime;
    private float currentNormalizedPull;
    private bool isPulling;
    private bool isHovered;
    private bool isPulled;
    private Coroutine animateCoroutine;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        if (handleInteractable != null)
        {
            handleInteractable.hoverEntered.AddListener(OnHoverEntered);
            handleInteractable.hoverExited.AddListener(OnHoverExited);
            handleInteractable.selectEntered.AddListener(OnSelectEntered);
            handleInteractable.selectExited.AddListener(OnSelectExited);
            handleInteractable.activated.AddListener(OnActivated);
        }

        if (mazeState != null)
        {
            mazeState.ExitLeverPulled += OnExitLeverPulled;
            mazeState.MazeReset += OnMazeReset;
        }
    }

    private void Start()
    {
        isPulled = mazeState != null && mazeState.IsExitLeverPulled;
        currentNormalizedPull = isPulled ? 1f : 0f;
        ApplyLeverAngle(currentNormalizedPull);
        ApplyVisualState();
    }

    private void OnDisable()
    {
        if (handleInteractable != null)
        {
            handleInteractable.hoverEntered.RemoveListener(OnHoverEntered);
            handleInteractable.hoverExited.RemoveListener(OnHoverExited);
            handleInteractable.selectEntered.RemoveListener(OnSelectEntered);
            handleInteractable.selectExited.RemoveListener(OnSelectExited);
            handleInteractable.activated.RemoveListener(OnActivated);
        }

        if (mazeState != null)
        {
            mazeState.ExitLeverPulled -= OnExitLeverPulled;
            mazeState.MazeReset -= OnMazeReset;
        }
    }

    private void Update()
    {
        if (!isPulling || isPulled || pullingInteractor == null)
            return;

        var attach = pullingInteractor.GetAttachTransform(handleInteractable);
        var currentY = attach != null ? attach.position.y : pullingInteractor.transform.position.y;
        var drop = Mathf.Max(0f, pullStartWorldY - currentY);
        currentNormalizedPull = Mathf.Clamp01(drop / 0.34f);
        ApplyLeverAngle(currentNormalizedPull);

        if (currentNormalizedPull >= commitThreshold)
        {
            isPulling = false;
            pullingInteractor = null;
            CommitPull();
        }
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        isHovered = true;
        ApplyVisualState();
        if (haptics != null)
            haptics.PlayMiniatureHover();
    }

    private void OnHoverExited(HoverExitEventArgs args)
    {
        isHovered = false;
        ApplyVisualState();
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (isPulled)
            return;

        if (animateCoroutine != null)
        {
            StopCoroutine(animateCoroutine);
            animateCoroutine = null;
        }

        pullingInteractor = args.interactorObject;
        var attach = pullingInteractor.GetAttachTransform(handleInteractable);
        pullStartWorldY = attach != null ? attach.position.y : pullingInteractor.transform.position.y;
        pullStartTime = Time.time;
        isPulling = true;

        if (haptics != null)
            haptics.PlayMiniatureClick();
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        if (!isPulling)
            return;

        isPulling = false;
        pullingInteractor = null;

        if (isPulled)
            return;

        // Seated / far-ray friendly: a quick click or sufficient pull commits cleanly.
        var quickSelect = (Time.time - pullStartTime) < 0.45f;
        if (quickSelect || currentNormalizedPull >= commitThreshold)
        {
            CommitPull();
        }
        else
        {
            StartAngleAnimation(0f);
        }
    }

    private void OnActivated(ActivateEventArgs args)
    {
        if (!isPulled)
            CommitPull();
    }

    private void CommitPull()
    {
        if (isPulled)
            return;

        if (mazeState != null)
            mazeState.TryPullExitLever();
        else
            OnExitLeverPulled();
    }

    private void OnExitLeverPulled()
    {
        isPulled = true;
        isPulling = false;
        pullingInteractor = null;
        if (handleInteractable != null)
            handleInteractable.enabled = false;

        StartAngleAnimation(1f);
        ApplyVisualState();

        if (mazeAudio != null)
            mazeAudio.PlayOneShot3D(MazeSoundId.LeverPull, transform.position + Vector3.up * 1.1f, 0.95f);
        if (haptics != null)
            haptics.PlayVictoryPulse();
    }

    private void OnMazeReset()
    {
        if (animateCoroutine != null)
        {
            StopCoroutine(animateCoroutine);
            animateCoroutine = null;
        }

        isPulled = false;
        isPulling = false;
        isHovered = false;
        pullingInteractor = null;
        currentNormalizedPull = 0f;

        if (handleInteractable != null)
            handleInteractable.enabled = true;

        ApplyLeverAngle(0f);
        ApplyVisualState();
    }

    private void StartAngleAnimation(float targetNormalized)
    {
        if (animateCoroutine != null)
            StopCoroutine(animateCoroutine);

        if (!isActiveAndEnabled)
        {
            currentNormalizedPull = targetNormalized;
            ApplyLeverAngle(targetNormalized);
            return;
        }

        animateCoroutine = StartCoroutine(AnimatePull(targetNormalized));
    }

    private IEnumerator AnimatePull(float targetNormalized)
    {
        var start = currentNormalizedPull;
        var elapsed = 0f;
        while (elapsed < pullAnimateSeconds)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / pullAnimateSeconds));
            currentNormalizedPull = Mathf.Lerp(start, targetNormalized, t);
            ApplyLeverAngle(currentNormalizedPull);
            yield return null;
        }

        currentNormalizedPull = targetNormalized;
        ApplyLeverAngle(targetNormalized);
        animateCoroutine = null;
    }

    private void ApplyLeverAngle(float normalized)
    {
        if (leverPivot == null)
            return;

        var angle = Mathf.Lerp(upAngleX, downAngleX, Mathf.Clamp01(normalized));
        leverPivot.localRotation = Quaternion.Euler(angle, 0f, 0f);
    }

    private void ApplyVisualState()
    {
        if (indicatorRenderers == null)
            return;

        var color = isPulled ? pulledColor : isHovered ? hoverColor : idleColor;
        var emission = isPulled ? pulledColor * 1.65f : isHovered ? hoverColor * 1.0f : idleColor * 0.45f;

        propertyBlock ??= new MaterialPropertyBlock();
        for (var i = 0; i < indicatorRenderers.Length; i++)
        {
            var r = indicatorRenderers[i];
            if (r == null)
                continue;

            r.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(EmissionColorId, emission);
            r.SetPropertyBlock(propertyBlock);
        }
    }
}
