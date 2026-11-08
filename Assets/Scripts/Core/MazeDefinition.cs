using System;
using UnityEngine;

[CreateAssetMenu(fileName = "MazeDefinition", menuName = "Pocket Labyrinthe/Maze Definition")]
public sealed class MazeDefinition : ScriptableObject
{
    [Min(0.0001f)] public float metresToMiniatureScale = 0.01f;
    public MazeMechanismDefinition[] mechanisms = Array.Empty<MazeMechanismDefinition>();

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
}

[Serializable]
public sealed class MazeMechanismDefinition
{
    public string id;
    public bool startsLowered;
    [Min(0f)] public float lowerDistance = 5f;
    [Min(0f)] public float animationSeconds = 0.45f;
}
