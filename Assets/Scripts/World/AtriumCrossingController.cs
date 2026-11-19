using UnityEngine;

public sealed class AtriumCrossingController : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    public MazeState mazeState;
    public Transform worldMazeRoot;
    public Transform trackedHead;
    public Renderer[] northCrossingGuideRenderers;
    public Renderer[] southCrossingGuideRenderers;
    public Renderer[] thresholdWarningRenderers;

    [Header("Guide Colors")]
    public Color openGuideColor = new Color(0.28f, 0.88f, 0.78f, 1f);
    public Color closedGuideColor = new Color(0.28f, 0.22f, 0.2f, 1f);
    public Color movingGuideColor = new Color(0.95f, 0.68f, 0.22f, 1f);
    public Color warningColor = new Color(1f, 0.42f, 0.16f, 1f);

    private MaterialPropertyBlock propertyBlock;
    private float warningFlashTimer;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        if (mazeState != null)
            mazeState.PartitionMoveBlocked += OnPartitionMoveBlocked;
    }

    private void OnDisable()
    {
        if (mazeState != null)
            mazeState.PartitionMoveBlocked -= OnPartitionMoveBlocked;
    }

    private void Update()
    {
        UpdatePlayerZone();

        if (warningFlashTimer > 0f)
            warningFlashTimer = Mathf.Max(0f, warningFlashTimer - Time.deltaTime);

        UpdateCrossingGuides();
    }

    public MazeZoneId EvaluateZoneForLocalPoint(Vector3 localPos, out bool inPartitionTravelZone)
    {
        inPartitionTravelZone = IsInsidePartitionTravelSafety(localPos);

        // Exit Sanctuary: South-East alcove (X in [-1.5, +4.5], Z < -4.5)
        if (localPos.z < -4.5f)
            return localPos.x < -1.5f ? MazeZoneId.StartCorridor : MazeZoneId.ExitSanctuary;

        // North Gallery & West Gallery
        if (localPos.z >= 4.5f || (localPos.x < -4.5f && localPos.z >= -1.5f))
            return MazeZoneId.LanternGallery;

        // West corridor spine: Vestibule & Atrium Antechamber
        if (localPos.x < -1.5f)
            return localPos.z >= 1.5f ? MazeZoneId.AtriumAntechamber : MazeZoneId.Vestibule;

        // Atrium region: X >= -1.5, Z in [-4.5, +4.5]
        if (inPartitionTravelZone)
            return MazeZoneId.AtriumThreshold;

        if (localPos.x > 3.58f)
            return MazeZoneId.AtriumIsland;

        return localPos.z >= 0f ? MazeZoneId.AtriumEntry : MazeZoneId.AtriumFarSide;
    }

    public bool CanTeleportToZone(MazeZoneId targetZone)
    {
        if (mazeState == null || targetZone == MazeZoneId.Unknown)
            return true;

        var current = mazeState.CurrentPlayerZone;
        if (targetZone == current)
            return true;

        switch (targetZone)
        {
            case MazeZoneId.StartCorridor:
                if (current == MazeZoneId.Vestibule)
                    return mazeState.IsLowered(MazeMechanismIds.IntroGate);
                if (current == MazeZoneId.ExitSanctuary)
                    return mazeState.IsLowered(MazeMechanismIds.ExitGate);
                return false;

            case MazeZoneId.Vestibule:
                if (current == MazeZoneId.StartCorridor)
                    return mazeState.IsLowered(MazeMechanismIds.IntroGate);
                return current == MazeZoneId.LanternGallery || current == MazeZoneId.AtriumAntechamber;

            case MazeZoneId.LanternGallery:
                return current == MazeZoneId.Vestibule || current == MazeZoneId.AtriumAntechamber;

            case MazeZoneId.AtriumAntechamber:
                if (current == MazeZoneId.AtriumEntry)
                    return mazeState.IsLowered(MazeMechanismIds.AtriumLock);
                return current == MazeZoneId.Vestibule || current == MazeZoneId.LanternGallery;

            case MazeZoneId.AtriumEntry:
                if (current == MazeZoneId.AtriumAntechamber)
                    return mazeState.IsLowered(MazeMechanismIds.AtriumLock);
                if (current == MazeZoneId.AtriumThreshold)
                    return mazeState.PartitionPosition == AtriumPartitionPosition.EntryToIsland;
                if (current == MazeZoneId.AtriumIsland)
                    return !mazeState.IsPartitionMoving && mazeState.PartitionPosition == AtriumPartitionPosition.EntryToIsland;
                return false;

            case MazeZoneId.AtriumIsland:
                if (current == MazeZoneId.AtriumThreshold)
                    return true;
                if (current == MazeZoneId.AtriumEntry)
                    return !mazeState.IsPartitionMoving && mazeState.PartitionPosition == AtriumPartitionPosition.EntryToIsland;
                if (current == MazeZoneId.AtriumFarSide)
                    return !mazeState.IsPartitionMoving && mazeState.PartitionPosition == AtriumPartitionPosition.IslandToExit;
                return false;

            case MazeZoneId.AtriumFarSide:
                if (current == MazeZoneId.AtriumThreshold)
                    return mazeState.PartitionPosition == AtriumPartitionPosition.IslandToExit;
                if (current == MazeZoneId.AtriumIsland)
                    return !mazeState.IsPartitionMoving && mazeState.PartitionPosition == AtriumPartitionPosition.IslandToExit;
                if (current == MazeZoneId.ExitSanctuary)
                    return mazeState.IsLowered(MazeMechanismIds.ExitGate);
                return false;

            case MazeZoneId.ExitSanctuary:
                if (current == MazeZoneId.AtriumFarSide || current == MazeZoneId.StartCorridor)
                    return mazeState.IsLowered(MazeMechanismIds.ExitGate);
                return false;

            default:
                return false;
        }
    }

    private void UpdatePlayerZone()
    {
        if (mazeState == null || trackedHead == null)
            return;

        var localPos = worldMazeRoot != null
            ? worldMazeRoot.InverseTransformPoint(trackedHead.position)
            : trackedHead.position;

        var zone = EvaluateZoneForLocalPoint(localPos, out var inTravelZone);
        mazeState.UpdatePlayerOccupancy(zone, inTravelZone);
    }

    private bool IsInsidePartitionTravelSafety(Vector3 localPos)
    {
        var min = new Vector3(2.42f, -0.5f, -2.25f);
        var max = new Vector3(3.58f, 4.0f, 2.25f);
        if (mazeState != null && mazeState.Definition != null && mazeState.Definition.atriumPartition != null)
        {
            min = mazeState.Definition.atriumPartition.travelSafetyMin;
            max = mazeState.Definition.atriumPartition.travelSafetyMax;
        }

        return localPos.x >= min.x && localPos.x <= max.x &&
               localPos.z >= min.z && localPos.z <= max.z;
    }

    private void OnPartitionMoveBlocked(string reason)
    {
        warningFlashTimer = 1.25f;
    }

    private void UpdateCrossingGuides()
    {
        if (mazeState == null)
            return;

        var unlocked = mazeState.IsPartitionUnlocked;
        var moving = mazeState.IsPartitionMoving;
        var northOpen = unlocked && !moving && mazeState.PartitionPosition == AtriumPartitionPosition.EntryToIsland;
        var southOpen = unlocked && !moving && mazeState.PartitionPosition == AtriumPartitionPosition.IslandToExit;

        ApplyGuideRenderers(northCrossingGuideRenderers,
            moving ? movingGuideColor : northOpen ? openGuideColor : closedGuideColor,
            moving ? 0.9f : northOpen ? 1.15f : 0f);

        ApplyGuideRenderers(southCrossingGuideRenderers,
            moving ? movingGuideColor : southOpen ? openGuideColor : closedGuideColor,
            moving ? 0.9f : southOpen ? 1.15f : 0f);

        var showWarning = mazeState.IsPlayerInPartitionTravelZone || warningFlashTimer > 0f;
        var warnIntensity = showWarning ? 1.1f + 0.35f * Mathf.Sin(Time.time * 9f) : 0f;
        ApplyGuideRenderers(thresholdWarningRenderers, showWarning ? warningColor : closedGuideColor, warnIntensity);
    }

    private void ApplyGuideRenderers(Renderer[] renderers, Color color, float emissionMultiplier)
    {
        if (renderers == null)
            return;

        propertyBlock ??= new MaterialPropertyBlock();
        var emission = emissionMultiplier > 0.01f ? color * emissionMultiplier : Color.black;
        for (var i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null)
                continue;

            r.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(EmissionColorId, emission);
            r.SetPropertyBlock(propertyBlock);
        }
    }
}
