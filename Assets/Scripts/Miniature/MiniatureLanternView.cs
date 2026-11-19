using System;
using UnityEngine;

[Serializable]
public sealed class MiniatureLandmarkEntry
{
    public string landmarkId;
    public Renderer[] renderers;
    public Color baseColor = Color.white;
}

public sealed class MiniatureLanternView : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    public MazeState mazeState;
    public MazeScaleMapper mapper;
    public Transform trackedLantern;
    public Transform miniatureLanternDot;
    [Min(0f)] public float lanternLocalHeight = 0.014f;

    [Header("Miniature Rail & Route Feedback")]
    public Renderer[] miniatureRailRenderers;
    public Renderer[] miniatureSolvedRouteRenderers;
    public MiniatureLandmarkEntry[] landmarks;

    public Color dormantRailColor = new Color(0.55f, 0.44f, 0.28f, 1f);
    public Color solvedRailColor = new Color(0.32f, 0.9f, 0.96f, 1f);
    public Color routeDormantColor = new Color(0.24f, 0.25f, 0.28f, 1f);
    public Color routeLitColor = new Color(1f, 0.84f, 0.34f, 1f);
    public Color hintHighlightColor = new Color(1f, 0.9f, 0.35f, 1f);

    private MaterialPropertyBlock propertyBlock;
    private string activeHintLandmarkId = string.Empty;
    private float hintHighlightTimer;
    private float routeProgress;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        if (mazeState != null)
        {
            mazeState.LanternDockedCorrectly += OnLanternSolved;
            mazeState.MazeReset += OnMazeReset;
        }
    }

    private void Start()
    {
        ApplyRailVisual(mazeState != null && mazeState.IsLanternSolved);
        SetRouteProgress(0f);
    }

    private void OnDisable()
    {
        if (mazeState != null)
        {
            mazeState.LanternDockedCorrectly -= OnLanternSolved;
            mazeState.MazeReset -= OnMazeReset;
        }
    }

    private void LateUpdate()
    {
        if (mapper != null && trackedLantern != null && miniatureLanternDot != null)
            miniatureLanternDot.position = mapper.WorldToMiniaturePosition(trackedLantern.position, lanternLocalHeight);

        if (hintHighlightTimer > 0f)
        {
            hintHighlightTimer = Mathf.Max(0f, hintHighlightTimer - Time.deltaTime);
            UpdateHintHighlightVisual();
        }
    }

    public void PulseLandmark(string landmarkId, float durationSeconds = 3.5f)
    {
        ClearHintHighlightVisual();
        activeHintLandmarkId = landmarkId ?? string.Empty;
        hintHighlightTimer = Mathf.Max(0.1f, durationSeconds);
        UpdateHintHighlightVisual();
    }

    public void SetRouteProgress(float normalizedProgress)
    {
        routeProgress = Mathf.Clamp01(normalizedProgress);
        if (miniatureSolvedRouteRenderers == null || miniatureSolvedRouteRenderers.Length == 0)
            return;

        propertyBlock ??= new MaterialPropertyBlock();
        var count = miniatureSolvedRouteRenderers.Length;
        for (var i = 0; i < count; i++)
        {
            var r = miniatureSolvedRouteRenderers[i];
            if (r == null)
                continue;

            var threshold = i / (float)count;
            var glow = Mathf.Clamp01((routeProgress - threshold) * count);
            var active = glow > 0.01f;
            r.enabled = active;
            if (!active)
                continue;

            r.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, Color.Lerp(routeDormantColor, routeLitColor, glow));
            propertyBlock.SetColor(EmissionColorId, routeLitColor * (glow * 1.65f));
            r.SetPropertyBlock(propertyBlock);
        }
    }

    private void OnLanternSolved(string bayId, int nicheIndex)
    {
        ApplyRailVisual(true);
    }

    private void OnMazeReset()
    {
        ClearHintHighlightVisual();
        activeHintLandmarkId = string.Empty;
        hintHighlightTimer = 0f;
        ApplyRailVisual(false);
        SetRouteProgress(0f);
    }

    private void ApplyRailVisual(bool solved)
    {
        if (miniatureRailRenderers == null)
            return;

        propertyBlock ??= new MaterialPropertyBlock();
        var color = solved ? solvedRailColor : dormantRailColor;
        var emission = solved ? solvedRailColor * 1.15f : dormantRailColor * 0.35f;

        for (var i = 0; i < miniatureRailRenderers.Length; i++)
        {
            var r = miniatureRailRenderers[i];
            if (r == null)
                continue;

            r.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(EmissionColorId, emission);
            r.SetPropertyBlock(propertyBlock);
        }
    }

    private void UpdateHintHighlightVisual()
    {
        if (landmarks == null || string.IsNullOrEmpty(activeHintLandmarkId))
            return;

        var pulse = hintHighlightTimer > 0f ? 0.55f + 0.45f * Mathf.Sin(Time.time * 8f) : 0f;
        propertyBlock ??= new MaterialPropertyBlock();

        for (var i = 0; i < landmarks.Length; i++)
        {
            var entry = landmarks[i];
            if (entry == null || entry.renderers == null ||
                !string.Equals(entry.landmarkId, activeHintLandmarkId, StringComparison.Ordinal))
                continue;

            for (var rIndex = 0; rIndex < entry.renderers.Length; rIndex++)
            {
                var r = entry.renderers[rIndex];
                if (r == null)
                    continue;

                r.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(BaseColorId, Color.Lerp(entry.baseColor, hintHighlightColor, pulse));
                propertyBlock.SetColor(EmissionColorId, pulse > 0.01f ? hintHighlightColor * (pulse * 1.6f) : Color.black);
                r.SetPropertyBlock(propertyBlock);
            }
        }
    }

    private void ClearHintHighlightVisual()
    {
        if (landmarks == null || string.IsNullOrEmpty(activeHintLandmarkId))
            return;

        propertyBlock ??= new MaterialPropertyBlock();
        for (var i = 0; i < landmarks.Length; i++)
        {
            var entry = landmarks[i];
            if (entry == null || entry.renderers == null ||
                !string.Equals(entry.landmarkId, activeHintLandmarkId, StringComparison.Ordinal))
                continue;

            for (var rIndex = 0; rIndex < entry.renderers.Length; rIndex++)
            {
                var r = entry.renderers[rIndex];
                if (r == null)
                    continue;

                r.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(BaseColorId, entry.baseColor);
                propertyBlock.SetColor(EmissionColorId, Color.black);
                r.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
