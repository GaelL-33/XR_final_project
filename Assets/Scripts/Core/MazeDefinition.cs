using System;
using UnityEngine;

[CreateAssetMenu(fileName = "MazeDefinition", menuName = "Pocket Labyrinthe/Maze Definition")]
public sealed class MazeDefinition : ScriptableObject
{
    [Header("Scale & Spawn")]
    [Min(0.0001f)] public float metresToMiniatureScale = 0.01f;
    public Vector3 spawnWorldPosition = new Vector3(-3f, 0f, -6f);
    public float spawnYawDegrees = 0f;
    public Vector3 lanternPedestalWorldPosition = new Vector3(-3f, 0.96f, 0f);
    public Vector3 mazeBoundsMin = new Vector3(-7.8f, -0.5f, -7.8f);
    public Vector3 mazeBoundsMax = new Vector3(7.8f, 5.0f, 7.8f);

    [Header("Lowering Mechanisms")]
    public MazeMechanismDefinition[] mechanisms = Array.Empty<MazeMechanismDefinition>();

    [Header("Lantern Gallery (6 Bays x 6 Niches)")]
    public LanternBayDefinition[] lanternBays = Array.Empty<LanternBayDefinition>();

    [Header("Atrium Sliding Partition")]
    public AtriumPartitionDefinition atriumPartition = new AtriumPartitionDefinition();

    [Header("Escalating Hints (French)")]
    public HintStageDefinition[] hints = Array.Empty<HintStageDefinition>();

    public bool TryGetMechanism(string id, out MazeMechanismDefinition mechanism)
    {
        if (mechanisms != null)
        {
            foreach (var candidate in mechanisms)
            {
                if (candidate != null && string.Equals(candidate.id, id, StringComparison.Ordinal))
                {
                    mechanism = candidate;
                    return true;
                }
            }
        }

        mechanism = null;
        return false;
    }

    public bool TryGetLanternBay(string bayId, out LanternBayDefinition bay)
    {
        if (lanternBays != null)
        {
            foreach (var candidate in lanternBays)
            {
                if (candidate != null && string.Equals(candidate.id, bayId, StringComparison.Ordinal))
                {
                    bay = candidate;
                    return true;
                }
            }
        }

        bay = null;
        return false;
    }

    public bool TryGetCorrectLanternTarget(out string bayId, out int nicheIndex)
    {
        if (lanternBays != null)
        {
            foreach (var candidate in lanternBays)
            {
                if (candidate != null && candidate.backsOntoAtriumRail && candidate.correctNicheIndex >= 0)
                {
                    bayId = candidate.id;
                    nicheIndex = candidate.correctNicheIndex;
                    return true;
                }
            }
        }

        bayId = string.Empty;
        nicheIndex = -1;
        return false;
    }

    public bool TryGetHint(ProgressionStage stage, out HintStageDefinition hint)
    {
        if (hints != null)
        {
            foreach (var candidate in hints)
            {
                if (candidate != null && candidate.stage == stage)
                {
                    hint = candidate;
                    return true;
                }
            }
        }

        hint = null;
        return false;
    }
}

[Serializable]
public sealed class MazeMechanismDefinition
{
    public string id;
    public bool startsLowered;
    public bool allowDirectMiniatureToggle = true;
    [Min(0f)] public float lowerDistance = 5f;
    [Min(0f)] public float animationSeconds = 0.65f;
}

[Serializable]
public sealed class LanternBayDefinition
{
    public string id;
    public string numeral = "I";
    public string displayNameFr = "Baie";
    public BayEmblemKind emblemKind;
    public Color emblemColor = new Color(0.86f, 0.72f, 0.45f, 1f);
    public bool backsOntoAtriumRail;
    public int nicheCount = 6;
    public int correctNicheIndex = -1;
    public Vector3 worldCenter;
    public Vector3 inwardNormal = Vector3.forward;
}

[Serializable]
public sealed class AtriumPartitionDefinition
{
    public string id = MazeMechanismIds.AtriumPartition;
    public bool startsLocked = true;
    public AtriumPartitionPosition initialPosition = AtriumPartitionPosition.IslandToExit;
    public float railWorldX = 3.0f;
    public float entryPositionWorldZ = 1.1f;
    public float exitPositionWorldZ = -1.1f;
    [Min(0.05f)] public float slideSeconds = 0.75f;
    public Vector3 travelSafetyMin = new Vector3(2.42f, -0.5f, -2.25f);
    public Vector3 travelSafetyMax = new Vector3(3.58f, 4.0f, 2.25f);
}

[Serializable]
public sealed class HintStageDefinition
{
    public ProgressionStage stage;
    public string highlightLandmarkId;
    [TextArea(1, 3)] public string tier1Fr;
    [TextArea(1, 3)] public string tier2Fr;
    [TextArea(1, 3)] public string tier3Fr;
}
