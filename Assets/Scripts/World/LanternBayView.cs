using System.Collections;
using UnityEngine;

public sealed class LanternBayView : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    public MazeState mazeState;
    public ProceduralMazeAudio mazeAudio;
    public LanternItem lanternItem;
    public string bayId;
    public bool backsOntoAtriumRail;
    public int correctNicheIndex = -1;
    public Transform seamRevealOrigin;
    public LanternNicheSocket[] nicheSockets;
    public Renderer[] carvedSeamRenderers;
    public Renderer[] railConduitRenderers;

    [Header("Carved Seam Proximity Reveal")]
    [Min(0.5f)] public float revealStartDistance = 3.4f;
    [Min(0.2f)] public float revealFullDistance = 1.65f;
    public Color dormantSeamColor = new Color(0.18f, 0.17f, 0.19f, 1f);
    public Color revealSeamColor = new Color(1f, 0.72f, 0.22f, 1f);
    public Color solvedSeamColor = new Color(1f, 0.88f, 0.42f, 1f);

    private MaterialPropertyBlock propertyBlock;
    private float currentReveal;
    private bool solved;
    private bool hasPlayedRevealCue;
    private Coroutine surgeCoroutine;

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
        ApplySeamVisual(0f, false);
        ApplyConduitVisual(0f);
    }

    private void OnDisable()
    {
        if (mazeState != null)
        {
            mazeState.LanternDockedCorrectly -= OnLanternSolved;
            mazeState.MazeReset -= OnMazeReset;
        }
    }

    private void Update()
    {
        if (!backsOntoAtriumRail || solved || lanternItem == null)
            return;

        var origin = seamRevealOrigin != null ? seamRevealOrigin.position : transform.position;
        var dist = Vector3.Distance(lanternItem.transform.position, origin);
        var targetReveal = Mathf.Clamp01(Mathf.InverseLerp(revealStartDistance, revealFullDistance, dist));

        // Only reveal when the lantern is actually brought away from its pedestal
        if (lanternItem.IsRestingOnPedestal)
            targetReveal = 0f;

        currentReveal = Mathf.MoveTowards(currentReveal, targetReveal, Time.deltaTime * 2.4f);
        ApplySeamVisual(currentReveal, false);

        if (nicheSockets != null && correctNicheIndex >= 0 && correctNicheIndex < nicheSockets.Length)
        {
            var targetSocket = nicheSockets[correctNicheIndex];
            if (targetSocket != null)
                targetSocket.SetClueHighlight(currentReveal * 0.9f);
        }

        if (!hasPlayedRevealCue && currentReveal > 0.45f)
        {
            hasPlayedRevealCue = true;
            if (mazeAudio != null)
                mazeAudio.PlayOneShot3D(MazeSoundId.HintChime, origin, 0.55f, 1.05f);
        }
    }

    private void OnLanternSolved(string solvedBayId, int solvedNiche)
    {
        if (!string.Equals(solvedBayId, bayId, System.StringComparison.Ordinal))
            return;

        solved = true;
        currentReveal = 1f;
        if (surgeCoroutine != null)
            StopCoroutine(surgeCoroutine);
        surgeCoroutine = StartCoroutine(PlaySolveSurge());
    }

    private void OnMazeReset()
    {
        if (surgeCoroutine != null)
        {
            StopCoroutine(surgeCoroutine);
            surgeCoroutine = null;
        }

        solved = false;
        currentReveal = 0f;
        hasPlayedRevealCue = false;
        ApplySeamVisual(0f, false);
        ApplyConduitVisual(0f);
    }

    private IEnumerator PlaySolveSurge()
    {
        ApplySeamVisual(1f, true);
        var elapsed = 0f;
        const float duration = 0.85f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            ApplyConduitVisual(t);
            yield return null;
        }

        ApplyConduitVisual(1f);
        surgeCoroutine = null;
    }

    private void ApplySeamVisual(float strength, bool isSolvedState)
    {
        if (carvedSeamRenderers == null)
            return;

        var pulse = isSolvedState ? 1.65f + 0.15f * Mathf.Sin(Time.time * 3f) : 1.15f;
        var activeColor = isSolvedState ? solvedSeamColor : revealSeamColor;
        var baseColor = Color.Lerp(dormantSeamColor, activeColor, strength);
        var emission = strength > 0.01f ? activeColor * (strength * pulse) : Color.black;

        propertyBlock ??= new MaterialPropertyBlock();
        for (var i = 0; i < carvedSeamRenderers.Length; i++)
        {
            var r = carvedSeamRenderers[i];
            if (r == null)
                continue;

            r.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, baseColor);
            propertyBlock.SetColor(EmissionColorId, emission);
            r.SetPropertyBlock(propertyBlock);
        }
    }

    private void ApplyConduitVisual(float progress)
    {
        if (railConduitRenderers == null || railConduitRenderers.Length == 0)
            return;

        propertyBlock ??= new MaterialPropertyBlock();
        var count = railConduitRenderers.Length;
        for (var i = 0; i < count; i++)
        {
            var r = railConduitRenderers[i];
            if (r == null)
                continue;

            var threshold = i / (float)count;
            var segmentGlow = Mathf.Clamp01((progress - threshold) * count);
            var baseColor = Color.Lerp(dormantSeamColor, solvedSeamColor, segmentGlow);
            var emission = segmentGlow > 0.01f ? solvedSeamColor * (segmentGlow * 1.6f) : Color.black;

            r.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, baseColor);
            propertyBlock.SetColor(EmissionColorId, emission);
            r.SetPropertyBlock(propertyBlock);
        }
    }
}
