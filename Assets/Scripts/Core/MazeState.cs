using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public sealed class MazeState : MonoBehaviour
{
    [SerializeField] private MazeDefinition definition;

    private readonly Dictionary<string, bool> loweredById = new Dictionary<string, bool>(StringComparer.Ordinal);

    public event Action<string, bool> MechanismStateChanged;

    public MazeDefinition Definition
    {
        get => definition;
        set => definition = value;
    }

    private void Awake()
    {
        loweredById.Clear();
        if (definition == null || definition.mechanisms == null)
            return;

        foreach (var mechanism in definition.mechanisms)
        {
            if (mechanism == null || string.IsNullOrWhiteSpace(mechanism.id))
            {
                Debug.LogError("Maze mechanisms need a stable, non-empty ID.", this);
                continue;
            }

            if (loweredById.ContainsKey(mechanism.id))
            {
                Debug.LogError($"Maze definition contains duplicate mechanism ID '{mechanism.id}'.", this);
                continue;
            }

            loweredById.Add(mechanism.id, mechanism.startsLowered);
        }
    }

    public bool TryGetMechanismDefinition(string id, out MazeMechanismDefinition mechanism)
    {
        if (definition != null)
            return definition.TryGetMechanism(id, out mechanism);

        mechanism = null;
        return false;
    }

    public bool IsLowered(string id)
    {
        return loweredById.TryGetValue(id, out var lowered) && lowered;
    }

    public bool TryToggleMechanism(string id)
    {
        if (!loweredById.TryGetValue(id, out var lowered))
        {
            Debug.LogWarning($"No maze mechanism is registered with ID '{id}'.", this);
            return false;
        }

        var newState = !lowered;
        loweredById[id] = newState;
        MechanismStateChanged?.Invoke(id, newState);
        return true;
    }
}
