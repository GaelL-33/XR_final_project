using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class MiniatureWallTarget : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    public string mechanismId;
    public Renderer[] highlightRenderers;
    public Color normalColor = Color.white;
    public Color highlightedColor = new Color(0.25f, 0.8f, 1f, 1f);

    private MaterialPropertyBlock properties;
    private bool highlighted;

    private void Awake()
    {
        properties = new MaterialPropertyBlock();
        ApplyColor();
    }

    public void SetHighlighted(bool value)
    {
        if (highlighted == value)
            return;

        highlighted = value;
        ApplyColor();
    }

    private void ApplyColor()
    {
        if (highlightRenderers == null)
            return;

        properties ??= new MaterialPropertyBlock();
        foreach (var renderer in highlightRenderers)
        {
            if (renderer == null)
                continue;

            renderer.GetPropertyBlock(properties);
            properties.SetColor(BaseColorId, highlighted ? highlightedColor : normalColor);
            renderer.SetPropertyBlock(properties);
        }
    }
}
