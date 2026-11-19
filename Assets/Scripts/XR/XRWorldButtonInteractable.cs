using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRSimpleInteractable))]
public sealed class XRWorldButtonInteractable : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    public Transform buttonCap;
    public Renderer[] highlightRenderers;
    public ProceduralMazeAudio mazeAudio;
    public XRHapticsService haptics;
    public Vector3 pressedLocalOffset = new Vector3(0f, -0.014f, 0f);
    public Color idleColor = new Color(0.75f, 0.58f, 0.3f, 1f);
    public Color hoverColor = new Color(0.35f, 0.85f, 1f, 1f);
    public Color pressedColor = new Color(1f, 0.88f, 0.45f, 1f);
    public Color disabledColor = new Color(0.32f, 0.34f, 0.38f, 1f);
    [Min(0.05f)] public float pressCooldownSeconds = 0.35f;
    public UnityEvent onPressed = new UnityEvent();

    public event Action Pressed;

    private XRSimpleInteractable interactable;
    private MaterialPropertyBlock propertyBlock;
    private Vector3 capRestLocalPosition;
    private bool isHovered;
    private bool isEnabledState = true;
    private float pressedVisualTimer;
    private float lastPressTime = -10f;

    private void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
        propertyBlock = new MaterialPropertyBlock();
        if (buttonCap != null)
            capRestLocalPosition = buttonCap.localPosition;
    }

    private void OnEnable()
    {
        if (interactable == null)
            return;

        interactable.hoverEntered.AddListener(OnHoverEntered);
        interactable.hoverExited.AddListener(OnHoverExited);
        interactable.selectEntered.AddListener(OnSelectEntered);
        interactable.activated.AddListener(OnActivated);
        ApplyVisualState();
    }

    private void OnDisable()
    {
        if (interactable == null)
            return;

        interactable.hoverEntered.RemoveListener(OnHoverEntered);
        interactable.hoverExited.RemoveListener(OnHoverExited);
        interactable.selectEntered.RemoveListener(OnSelectEntered);
        interactable.activated.RemoveListener(OnActivated);
    }

    private void Update()
    {
        if (pressedVisualTimer > 0f)
        {
            pressedVisualTimer -= Time.deltaTime;
            if (pressedVisualTimer <= 0f)
                ApplyVisualState();
        }
    }

    public void SetInteractableEnabled(bool enabledState)
    {
        isEnabledState = enabledState;
        if (interactable != null)
            interactable.enabled = enabledState;
        if (!enabledState)
            isHovered = false;
        ApplyVisualState();
    }

    public void TriggerPress()
    {
        if (!isEnabledState || Time.time - lastPressTime < pressCooldownSeconds)
            return;

        lastPressTime = Time.time;
        pressedVisualTimer = 0.2f;
        ApplyVisualState();

        if (mazeAudio != null)
            mazeAudio.PlayOneShot3D(MazeSoundId.MiniatureClick, transform.position, 0.75f);
        if (haptics != null)
            haptics.PlayMiniatureClick();

        Pressed?.Invoke();
        onPressed?.Invoke();
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
        TriggerPress();
    }

    private void OnActivated(ActivateEventArgs args)
    {
        TriggerPress();
    }

    private void ApplyVisualState()
    {
        var pressed = pressedVisualTimer > 0f;
        if (buttonCap != null)
            buttonCap.localPosition = capRestLocalPosition + (pressed ? pressedLocalOffset : Vector3.zero);

        if (highlightRenderers == null)
            return;

        var baseColor = !isEnabledState
            ? disabledColor
            : pressed
                ? pressedColor
                : isHovered
                    ? hoverColor
                    : idleColor;

        var emission = !isEnabledState
            ? Color.black
            : pressed
                ? pressedColor * 1.4f
                : isHovered
                    ? hoverColor * 0.75f
                    : idleColor * 0.25f;

        propertyBlock ??= new MaterialPropertyBlock();
        for (var i = 0; i < highlightRenderers.Length; i++)
        {
            var r = highlightRenderers[i];
            if (r == null)
                continue;

            r.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, baseColor);
            propertyBlock.SetColor(EmissionColorId, emission);
            r.SetPropertyBlock(propertyBlock);
        }
    }
}
