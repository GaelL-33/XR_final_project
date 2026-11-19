using UnityEngine;

public enum MiniatureTargetKind
{
    ToggleGate = 0,
    ToggleAtriumPartition = 1,
    RequestHint = 2,
}

[RequireComponent(typeof(Collider))]
public sealed class MiniatureWallTarget : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    public MiniatureTargetKind targetKind = MiniatureTargetKind.ToggleGate;
    public string mechanismId;
    public Renderer[] highlightRenderers;
    public Color normalColor = Color.white;
    public Color availableColor = new Color(0.95f, 0.86f, 0.56f, 1f);
    public Color highlightedColor = new Color(0.28f, 0.84f, 1f, 1f);
    public Color confirmedColor = new Color(1f, 0.9f, 0.42f, 1f);
    public Color lockedColor = new Color(0.42f, 0.44f, 0.48f, 1f);

    private MaterialPropertyBlock properties;
    private bool highlighted;
    private bool available = true;
    private float confirmedTimer;

    public bool IsAvailable => available;

    private void Awake()
    {
        properties = new MaterialPropertyBlock();
        ApplyColor();
    }

    private void Update()
    {
        if (confirmedTimer > 0f)
        {
            confirmedTimer = Mathf.Max(0f, confirmedTimer - Time.deltaTime);
            ApplyColor();
        }
    }

    public void SetAvailable(bool value)
    {
        if (available == value)
            return;

        available = value;
        ApplyColor();
    }

    public void SetHighlighted(bool value)
    {
        if (highlighted == value)
            return;

        highlighted = value;
        ApplyColor();
    }

    public void PulseConfirmed()
    {
        confirmedTimer = 0.35f;
        ApplyColor();
    }

    private void ApplyColor()
    {
        if (highlightRenderers == null)
            return;

        Color baseColor;
        Color emission;

        if (!available)
        {
            baseColor = lockedColor;
            emission = Color.black;
        }
        else if (confirmedTimer > 0f)
        {
            baseColor = confirmedColor;
            emission = confirmedColor * 1.35f;
        }
        else if (highlighted)
        {
            baseColor = highlightedColor;
            emission = highlightedColor * 0.95f;
        }
        else
        {
            baseColor = targetKind == MiniatureTargetKind.ToggleGate ? normalColor : availableColor;
            emission = targetKind == MiniatureTargetKind.ToggleGate ? Color.black : availableColor * 0.35f;
        }

        properties ??= new MaterialPropertyBlock();
        foreach (var renderer in highlightRenderers)
        {
            if (renderer == null)
                continue;

            renderer.GetPropertyBlock(properties);
            properties.SetColor(BaseColorId, baseColor);
            properties.SetColor(EmissionColorId, emission);
            renderer.SetPropertyBlock(properties);
        }
    }
}
