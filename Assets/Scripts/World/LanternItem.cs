using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRGrabInteractable), typeof(Rigidbody))]
public sealed class LanternItem : MonoBehaviour
{
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    public MazeState mazeState;
    public MazeSessionState sessionState;
    public ProceduralMazeAudio mazeAudio;
    public XRHapticsService haptics;
    public Transform homeTransform;
    public Light lanternLight;
    public Renderer[] flameRenderers;

    [Header("Safe Recovery")]
    public float fallRecallHeightY = -0.25f;
    public float outOfBoundsRecallSeconds = 0.8f;
    public float unreachableZoneRecallSeconds = 1.6f;

    [Header("Flame Visuals")]
    public Color flameWarmColor = new Color(1f, 0.72f, 0.28f, 1f);
    public float restIntensity = 1.75f;
    public float heldIntensity = 2.35f;
    public float solvedIntensity = 2.75f;

    public event Action<LanternItem> RecalledToPedestal;

    private XRGrabInteractable grabInteractable;
    private Rigidbody body;
    private MaterialPropertyBlock flameProperties;
    private float outOfBoundsTimer;
    private float unreachableTimer;
    private bool isLockedInSolvedSocket;
    private bool isRestingOnPedestal = true;

    public bool IsLockedInSolvedSocket => isLockedInSolvedSocket;
    public bool IsRestingOnPedestal => isRestingOnPedestal;
    public bool IsHeldByHand => grabInteractable != null && grabInteractable.isSelected &&
                                !(grabInteractable.firstInteractorSelecting is XRSocketInteractor);
    public bool IsDockedInSocket => grabInteractable != null && grabInteractable.isSelected &&
                                    grabInteractable.firstInteractorSelecting is XRSocketInteractor;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        body = GetComponent<Rigidbody>();
        flameProperties = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnSelectEntered);
            grabInteractable.selectExited.AddListener(OnSelectExited);
        }

        if (mazeState != null)
            mazeState.MazeReset += OnMazeReset;
    }

    private void Start()
    {
        SnapToHomeInternal(false);
        ApplyFlameGlow(restIntensity);
    }

    private void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
            grabInteractable.selectExited.RemoveListener(OnSelectExited);
        }

        if (mazeState != null)
            mazeState.MazeReset -= OnMazeReset;
    }

    private void Update()
    {
        // Gentle living flame pulse
        var targetBase = isLockedInSolvedSocket
            ? solvedIntensity
            : IsHeldByHand
                ? heldIntensity
                : restIntensity;
        var pulse = 1f + 0.05f * Mathf.Sin(Time.time * 4.2f);
        ApplyFlameGlow(targetBase * pulse);

        if (isLockedInSolvedSocket || isRestingOnPedestal || IsHeldByHand || IsDockedInSocket)
        {
            outOfBoundsTimer = 0f;
            unreachableTimer = 0f;
            return;
        }

        EvaluateSafeRecovery();
    }

    public void LockInSolvedSocket()
    {
        isLockedInSolvedSocket = true;
        isRestingOnPedestal = false;
        if (grabInteractable != null)
            grabInteractable.enabled = false;
        if (body != null)
        {
            body.isKinematic = true;
            body.useGravity = false;
        }

        ApplyFlameGlow(solvedIntensity);
    }

    public void RecallToPedestal(bool playFeedback = true)
    {
        if (isLockedInSolvedSocket)
            return;

        ReleaseAllSelections();
        SnapToHomeInternal(playFeedback);
    }

    private void OnMazeReset()
    {
        isLockedInSolvedSocket = false;
        if (grabInteractable != null)
            grabInteractable.enabled = true;

        ReleaseAllSelections();
        SnapToHomeInternal(false);
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        outOfBoundsTimer = 0f;
        unreachableTimer = 0f;

        if (args.interactorObject is XRSocketInteractor)
        {
            isRestingOnPedestal = false;
            return;
        }

        isRestingOnPedestal = false;
        if (body != null)
        {
            body.isKinematic = false;
            body.useGravity = true;
        }

        if (sessionState != null)
            sessionState.NotifyLanternPickedUp();

        if (mazeAudio != null)
            mazeAudio.PlayOneShot3D(MazeSoundId.LanternPickup, transform.position, 0.75f);
        if (haptics != null)
            haptics.PlayMiniatureClick();
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        if (isLockedInSolvedSocket)
            return;

        if (args.interactorObject is XRSocketInteractor)
            return;

        if (body != null && !IsDockedInSocket)
        {
            body.isKinematic = false;
            body.useGravity = true;
        }
    }

    private void EvaluateSafeRecovery()
    {
        var pos = transform.position;
        var outOfBounds = pos.y < fallRecallHeightY;
        if (!outOfBounds && mazeState != null && mazeState.Definition != null)
        {
            var min = mazeState.Definition.mazeBoundsMin;
            var max = mazeState.Definition.mazeBoundsMax;
            outOfBounds = pos.x < min.x || pos.x > max.x || pos.y < min.y || pos.y > max.y || pos.z < min.z || pos.z > max.z;
        }

        if (outOfBounds)
        {
            outOfBoundsTimer += Time.deltaTime;
            if (outOfBoundsTimer >= outOfBoundsRecallSeconds)
            {
                RecallToPedestal(true);
                return;
            }
        }
        else
        {
            outOfBoundsTimer = 0f;
        }

        // Also recover if dropped inside a locked area (Atrium before unlocked, or Exit Sanctuary before unlocked)
        var inLockedAtrium = mazeState != null && !mazeState.IsLowered(MazeMechanismIds.AtriumLock) &&
                             pos.x > -1.5f && pos.z > -4.5f && pos.z < 4.5f;
        var inLockedExit = mazeState != null && !mazeState.IsLowered(MazeMechanismIds.ExitGate) &&
                           pos.x > -1.5f && pos.z <= -4.5f;

        if (inLockedAtrium || inLockedExit)
        {
            unreachableTimer += Time.deltaTime;
            if (unreachableTimer >= unreachableZoneRecallSeconds)
                RecallToPedestal(true);
        }
        else
        {
            unreachableTimer = 0f;
        }
    }

    private void ReleaseAllSelections()
    {
        if (grabInteractable == null || grabInteractable.interactionManager == null)
            return;

        var manager = grabInteractable.interactionManager;
        var interactableInterface = (IXRSelectInteractable)grabInteractable;
        for (var i = grabInteractable.interactorsSelecting.Count - 1; i >= 0; i--)
        {
            var interactor = grabInteractable.interactorsSelecting[i];
            if (interactor != null)
                manager.SelectCancel(interactor, interactableInterface);
        }
    }

    private void SnapToHomeInternal(bool playFeedback)
    {
        outOfBoundsTimer = 0f;
        unreachableTimer = 0f;
        isRestingOnPedestal = true;

        if (body != null)
        {
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            body.isKinematic = true;
            body.useGravity = false;
        }

        if (homeTransform != null)
        {
            transform.SetPositionAndRotation(homeTransform.position, homeTransform.rotation);
        }
        else if (mazeState != null && mazeState.Definition != null)
        {
            transform.SetPositionAndRotation(mazeState.Definition.lanternPedestalWorldPosition, Quaternion.identity);
        }

        if (playFeedback)
        {
            if (mazeAudio != null)
                mazeAudio.PlayOneShot3D(MazeSoundId.LanternRecall, transform.position, 0.8f);
            if (haptics != null)
                haptics.PlayMiniatureHover();
            RecalledToPedestal?.Invoke(this);
        }
    }

    private void ApplyFlameGlow(float intensity)
    {
        if (lanternLight != null)
            lanternLight.intensity = intensity;

        if (flameRenderers == null)
            return;

        flameProperties ??= new MaterialPropertyBlock();
        var emission = flameWarmColor * (intensity * 1.15f);
        for (var i = 0; i < flameRenderers.Length; i++)
        {
            var r = flameRenderers[i];
            if (r == null)
                continue;

            r.GetPropertyBlock(flameProperties);
            flameProperties.SetColor(EmissionColorId, emission);
            r.SetPropertyBlock(flameProperties);
        }
    }
}
