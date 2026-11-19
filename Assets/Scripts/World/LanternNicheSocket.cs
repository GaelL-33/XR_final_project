using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRSocketInteractor))]
public sealed class LanternNicheSocket : MonoBehaviour, IXRHoverFilter, IXRSelectFilter
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    public MazeState mazeState;
    public ProceduralMazeAudio mazeAudio;
    public XRHapticsService haptics;
    public string bayId;
    public int nicheIndex;
    public Renderer ringRenderer;

    [Header("Visual States")]
    public Color idleRingColor = new Color(0.28f, 0.25f, 0.23f, 1f);
    public Color hoverRingColor = new Color(0.95f, 0.76f, 0.35f, 1f);
    public Color clueRingColor = new Color(1f, 0.72f, 0.25f, 1f);
    public Color wrongRingColor = new Color(0.85f, 0.25f, 0.18f, 1f);
    public Color solvedRingColor = new Color(1f, 0.86f, 0.38f, 1f);

    public event Action<LanternNicheSocket> WrongPlacementTriggered;
    public event Action<LanternNicheSocket> CorrectPlacementTriggered;

    private XRSocketInteractor socket;
    private MaterialPropertyBlock ringProperties;
    private bool isHovered;
    private bool isSolved;
    private float clueHighlightStrength;
    private float wrongFlashTimer;

    public bool canProcess => isActiveAndEnabled;

    private void Awake()
    {
        socket = GetComponent<XRSocketInteractor>();
        ringProperties = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        if (socket != null)
        {
            socket.hoverFilters.Add(this);
            socket.selectFilters.Add(this);
            socket.hoverEntered.AddListener(OnHoverEntered);
            socket.hoverExited.AddListener(OnHoverExited);
            socket.selectEntered.AddListener(OnSelectEntered);
            socket.selectExited.AddListener(OnSelectExited);
        }

        if (mazeState != null)
            mazeState.MazeReset += OnMazeReset;

        ApplyRingVisual();
    }

    private void OnDisable()
    {
        if (socket != null)
        {
            socket.hoverFilters.Remove(this);
            socket.selectFilters.Remove(this);
            socket.hoverEntered.RemoveListener(OnHoverEntered);
            socket.hoverExited.RemoveListener(OnHoverExited);
            socket.selectEntered.RemoveListener(OnSelectEntered);
            socket.selectExited.RemoveListener(OnSelectExited);
        }

        if (mazeState != null)
            mazeState.MazeReset -= OnMazeReset;
    }

    private void Update()
    {
        if (wrongFlashTimer > 0f)
        {
            wrongFlashTimer = Mathf.Max(0f, wrongFlashTimer - Time.deltaTime);
            ApplyRingVisual();
        }
    }

    public bool Process(IXRHoverInteractor interactor, IXRHoverInteractable interactable)
    {
        return IsLanternInteractable(interactable);
    }

    public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
    {
        return IsLanternInteractable(interactable);
    }

    public void SetClueHighlight(float strength)
    {
        var clamped = Mathf.Clamp01(strength);
        if (Mathf.Approximately(clueHighlightStrength, clamped))
            return;

        clueHighlightStrength = clamped;
        ApplyRingVisual();
    }

    private bool IsLanternInteractable(IXRInteractable interactable)
    {
        if (isSolved || interactable == null || interactable.transform == null)
            return false;

        var lantern = interactable.transform.GetComponent<LanternItem>();
        return lantern != null && !lantern.IsLockedInSolvedSocket;
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        isHovered = true;
        ApplyRingVisual();
        if (haptics != null)
            haptics.PlayMiniatureHover();
    }

    private void OnHoverExited(HoverExitEventArgs args)
    {
        isHovered = false;
        ApplyRingVisual();
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (mazeState == null || args.interactableObject == null || args.interactableObject.transform == null)
            return;

        var lantern = args.interactableObject.transform.GetComponent<LanternItem>();
        if (lantern == null)
            return;

        var solved = mazeState.TryDockLantern(bayId, nicheIndex);
        if (solved)
        {
            isSolved = true;
            wrongFlashTimer = 0f;
            lantern.LockInSolvedSocket();
            ApplyRingVisual();

            if (mazeAudio != null)
                mazeAudio.PlayOneShot3D(MazeSoundId.LanternSolvedChime, transform.position, 0.95f);
            if (haptics != null)
                haptics.PlayLanternChime();

            CorrectPlacementTriggered?.Invoke(this);
        }
        else
        {
            // Immediate, non-punitive rejection feedback; player can pull the lantern right back out with zero delay.
            wrongFlashTimer = 1.15f;
            ApplyRingVisual();

            if (mazeAudio != null)
                mazeAudio.PlayOneShot3D(MazeSoundId.LanternWrongNiche, transform.position, 0.85f);
            if (haptics != null)
                haptics.PlayRejectionPulse();

            WrongPlacementTriggered?.Invoke(this);
        }
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        if (!isSolved)
        {
            wrongFlashTimer = 0f;
            ApplyRingVisual();
        }
    }

    private void OnMazeReset()
    {
        isSolved = false;
        isHovered = false;
        clueHighlightStrength = 0f;
        wrongFlashTimer = 0f;

        if (socket != null && socket.interactionManager != null)
        {
            var interactorInterface = (IXRSelectInteractor)socket;
            for (var i = socket.interactablesSelected.Count - 1; i >= 0; i--)
            {
                var selected = socket.interactablesSelected[i];
                if (selected != null)
                    socket.interactionManager.SelectCancel(interactorInterface, selected);
            }
        }

        ApplyRingVisual();
    }

    private void ApplyRingVisual()
    {
        if (ringRenderer == null)
            return;

        Color baseColor;
        Color emission;

        if (isSolved)
        {
            baseColor = solvedRingColor;
            emission = solvedRingColor * 1.9f;
        }
        else if (wrongFlashTimer > 0f)
        {
            var flash = Mathf.Clamp01(wrongFlashTimer / 1.15f);
            baseColor = Color.Lerp(idleRingColor, wrongRingColor, flash);
            emission = wrongRingColor * (flash * 1.1f);
        }
        else if (isHovered)
        {
            baseColor = hoverRingColor;
            emission = hoverRingColor * 0.95f;
        }
        else if (clueHighlightStrength > 0.01f)
        {
            baseColor = Color.Lerp(idleRingColor, clueRingColor, clueHighlightStrength);
            emission = clueRingColor * (clueHighlightStrength * 1.15f);
        }
        else
        {
            baseColor = idleRingColor;
            emission = Color.black;
        }

        ringProperties ??= new MaterialPropertyBlock();
        ringRenderer.GetPropertyBlock(ringProperties);
        ringProperties.SetColor(BaseColorId, baseColor);
        ringProperties.SetColor(EmissionColorId, emission);
        ringRenderer.SetPropertyBlock(ringProperties);
    }
}
