using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

public sealed class CompletionSequenceView : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    public MazeState mazeState;
    public MazeSessionState sessionState;
    public ProceduralMazeAudio mazeAudio;
    public XRHapticsService haptics;
    public MiniatureLanternView miniatureLanternView;
    public CompletionBoardView completionBoardView;
    public TeleportationProvider teleportationProvider;
    public XROrigin xrOrigin;
    public Transform worldMazeRoot;

    [Header("Solved Route Beacons (Ordered Start -> Exit)")]
    public Renderer[] routeBeaconRenderers;
    public Light sanctuaryBeaconLight;
    public Transform sanctuaryHaloTransform;
    public Renderer[] sanctuaryAccentRenderers;

    [Header("Lighting Cascade Settings")]
    [Min(0.05f)] public float beaconStepDelaySeconds = 0.22f;
    public float dormantBeaconLightIntensity = 0.8f;
    public float unlockedBeaconLightIntensity = 2.4f;
    public float completedBeaconLightIntensity = 3.2f;
    public Color dormantRouteColor = new Color(0.24f, 0.23f, 0.26f, 1f);
    public Color litRouteColor = new Color(1f, 0.82f, 0.34f, 1f);

    private MaterialPropertyBlock propertyBlock;
    private Coroutine cascadeCoroutine;
    private Vector3 haloRestLocalPosition;
    private bool hasCompleted;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        if (sanctuaryHaloTransform != null)
            haloRestLocalPosition = sanctuaryHaloTransform.localPosition;
    }

    private void OnEnable()
    {
        if (mazeState != null)
        {
            mazeState.ExitLeverPulled += OnExitLeverPulled;
            mazeState.DemoCompleted += OnDemoCompleted;
            mazeState.MazeReset += OnMazeReset;
        }

        if (sessionState != null)
            sessionState.ReplayRequested += OnReplayRequested;
    }

    private void Start()
    {
        ResetVisuals();
    }

    private void OnDisable()
    {
        if (mazeState != null)
        {
            mazeState.ExitLeverPulled -= OnExitLeverPulled;
            mazeState.DemoCompleted -= OnDemoCompleted;
            mazeState.MazeReset -= OnMazeReset;
        }

        if (sessionState != null)
            sessionState.ReplayRequested -= OnReplayRequested;
    }

    private void Update()
    {
        if (sanctuaryHaloTransform != null)
        {
            var floatSpeed = hasCompleted ? 2.0f : 1.1f;
            var floatAmp = hasCompleted ? 0.08f : 0.03f;
            sanctuaryHaloTransform.localPosition = haloRestLocalPosition + Vector3.up * (Mathf.Sin(Time.time * floatSpeed) * floatAmp);
            sanctuaryHaloTransform.Rotate(Vector3.up, (hasCompleted ? 28f : 10f) * Time.deltaTime, Space.Self);
        }
    }

    private void OnExitLeverPulled()
    {
        if (cascadeCoroutine != null)
            StopCoroutine(cascadeCoroutine);
        cascadeCoroutine = StartCoroutine(PlayRouteLightingCascade());
    }

    private void OnDemoCompleted()
    {
        hasCompleted = true;
        if (sanctuaryBeaconLight != null)
            sanctuaryBeaconLight.intensity = completedBeaconLightIntensity;

        ApplySanctuaryAccents(1.75f);

        var chimePos = sanctuaryHaloTransform != null ? sanctuaryHaloTransform.position : transform.position;
        if (mazeAudio != null)
            mazeAudio.PlayOneShot3D(MazeSoundId.CompletionChord, chimePos, 0.95f);
        if (haptics != null)
            haptics.PlayVictoryPulse();

        if (completionBoardView != null)
            completionBoardView.ShowCompletion(sessionState != null ? sessionState.FormatElapsedTime() : "00:00");
    }

    private void OnMazeReset()
    {
        ResetVisuals();
    }

    private void OnReplayRequested()
    {
        TeleportPlayerToSpawn();
    }

    private IEnumerator PlayRouteLightingCascade()
    {
        var count = routeBeaconRenderers != null ? routeBeaconRenderers.Length : 0;
        for (var i = 0; i < count; i++)
        {
            var r = routeBeaconRenderers[i];
            if (r != null)
            {
                ApplyBeaconRenderer(r, true);
                if (mazeAudio != null)
                    mazeAudio.PlayRouteNote3D(r.transform.position, i, 0.65f);
            }

            if (miniatureLanternView != null)
                miniatureLanternView.SetRouteProgress((i + 1) / (float)Mathf.Max(1, count));

            yield return new WaitForSeconds(beaconStepDelaySeconds);
        }

        if (miniatureLanternView != null)
            miniatureLanternView.SetRouteProgress(1f);

        if (sanctuaryBeaconLight != null)
            sanctuaryBeaconLight.intensity = unlockedBeaconLightIntensity;
        ApplySanctuaryAccents(1.2f);
        cascadeCoroutine = null;
    }

    private void ResetVisuals()
    {
        if (cascadeCoroutine != null)
        {
            StopCoroutine(cascadeCoroutine);
            cascadeCoroutine = null;
        }

        hasCompleted = false;
        if (routeBeaconRenderers != null)
        {
            for (var i = 0; i < routeBeaconRenderers.Length; i++)
                ApplyBeaconRenderer(routeBeaconRenderers[i], false);
        }

        if (sanctuaryBeaconLight != null)
            sanctuaryBeaconLight.intensity = dormantBeaconLightIntensity;

        ApplySanctuaryAccents(0.35f);

        if (completionBoardView != null)
            completionBoardView.HideCompletion();
    }

    private void TeleportPlayerToSpawn()
    {
        var localSpawn = new Vector3(-3f, 0f, -6f);
        var spawnYaw = 0f;
        if (mazeState != null && mazeState.Definition != null)
        {
            localSpawn = mazeState.Definition.spawnWorldPosition;
            spawnYaw = mazeState.Definition.spawnYawDegrees;
        }

        var worldPos = worldMazeRoot != null ? worldMazeRoot.TransformPoint(localSpawn) : localSpawn;
        var worldRot = (worldMazeRoot != null ? worldMazeRoot.rotation : Quaternion.identity) * Quaternion.Euler(0f, spawnYaw, 0f);

        if (teleportationProvider != null)
        {
            var request = new TeleportRequest
            {
                destinationPosition = worldPos,
                destinationRotation = worldRot,
                matchOrientation = MatchOrientation.TargetUpAndForward,
                requestTime = Time.time,
            };
            teleportationProvider.QueueTeleportRequest(request);
        }
        else if (xrOrigin != null)
        {
            xrOrigin.MoveCameraToWorldLocation(worldPos + Vector3.up * xrOrigin.CameraInOriginSpaceHeight);
            xrOrigin.MatchOriginUpCameraForward(Vector3.up, worldRot * Vector3.forward);
        }
    }

    private void ApplyBeaconRenderer(Renderer r, bool lit)
    {
        if (r == null)
            return;

        propertyBlock ??= new MaterialPropertyBlock();
        r.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(BaseColorId, lit ? litRouteColor : dormantRouteColor);
        propertyBlock.SetColor(EmissionColorId, lit ? litRouteColor * 1.45f : Color.black);
        r.SetPropertyBlock(propertyBlock);
    }

    private void ApplySanctuaryAccents(float emissionMultiplier)
    {
        if (sanctuaryAccentRenderers == null)
            return;

        propertyBlock ??= new MaterialPropertyBlock();
        for (var i = 0; i < sanctuaryAccentRenderers.Length; i++)
        {
            var r = sanctuaryAccentRenderers[i];
            if (r == null)
                continue;

            r.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, litRouteColor);
            propertyBlock.SetColor(EmissionColorId, litRouteColor * emissionMultiplier);
            r.SetPropertyBlock(propertyBlock);
        }
    }
}
