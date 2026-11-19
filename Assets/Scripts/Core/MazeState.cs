using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public sealed class MazeState : MonoBehaviour
{
    [SerializeField] private MazeDefinition definition;

    private readonly Dictionary<string, bool> loweredById = new Dictionary<string, bool>(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> directToggleAllowedById = new Dictionary<string, bool>(StringComparer.Ordinal);

    public event Action<string, bool> MechanismStateChanged;
    public event Action<string, int> LanternDockedCorrectly;
    public event Action<string, int> LanternDockedWrong;
    public event Action<bool> PartitionUnlockedChanged;
    public event Action<AtriumPartitionPosition> PartitionPositionChanged;
    public event Action<bool> PartitionMovingChanged;
    public event Action<string> PartitionMoveBlocked;
    public event Action ExitLeverPulled;
    public event Action DemoCompleted;
    public event Action<MazeZoneId, bool> PlayerZoneChanged;
    public event Action MazeReset;

    public MazeDefinition Definition
    {
        get => definition;
        set => definition = value;
    }

    public bool IsLanternSolved { get; private set; }
    public string SolvedBayId { get; private set; } = string.Empty;
    public int SolvedNicheIndex { get; private set; } = -1;
    public bool IsPartitionUnlocked { get; private set; }
    public AtriumPartitionPosition PartitionPosition { get; private set; } = AtriumPartitionPosition.IslandToExit;
    public bool IsPartitionMoving { get; private set; }
    public bool IsExitLeverPulled { get; private set; }
    public bool IsDemoCompleted { get; private set; }
    public MazeZoneId CurrentPlayerZone { get; private set; } = MazeZoneId.StartCorridor;
    public bool IsPlayerInPartitionTravelZone { get; private set; }

    private void Awake()
    {
        InitializeFromDefinition();
    }

    private void InitializeFromDefinition()
    {
        loweredById.Clear();
        directToggleAllowedById.Clear();

        if (definition != null && definition.mechanisms != null)
        {
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
                directToggleAllowedById.Add(mechanism.id, mechanism.allowDirectMiniatureToggle);
            }
        }

        IsLanternSolved = false;
        SolvedBayId = string.Empty;
        SolvedNicheIndex = -1;

        var partitionDef = definition != null ? definition.atriumPartition : null;
        IsPartitionUnlocked = partitionDef != null && !partitionDef.startsLocked;
        PartitionPosition = partitionDef != null ? partitionDef.initialPosition : AtriumPartitionPosition.IslandToExit;
        IsPartitionMoving = false;

        IsExitLeverPulled = false;
        IsDemoCompleted = false;
        CurrentPlayerZone = MazeZoneId.StartCorridor;
        IsPlayerInPartitionTravelZone = false;
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
        return !string.IsNullOrEmpty(id) && loweredById.TryGetValue(id, out var lowered) && lowered;
    }

    public bool CanDirectlyToggleMechanism(string id)
    {
        return !string.IsNullOrEmpty(id) && directToggleAllowedById.TryGetValue(id, out var allowed) && allowed;
    }

    public bool TryToggleMechanism(string id)
    {
        if (!loweredById.TryGetValue(id, out var lowered))
        {
            Debug.LogWarning($"No maze mechanism is registered with ID '{id}'.", this);
            return false;
        }

        if (!CanDirectlyToggleMechanism(id))
            return false;

        return SetMechanismLoweredInternal(id, !lowered);
    }

    public bool TrySetMechanismLowered(string id, bool lowered)
    {
        if (!loweredById.ContainsKey(id))
        {
            Debug.LogWarning($"No maze mechanism is registered with ID '{id}'.", this);
            return false;
        }

        return SetMechanismLoweredInternal(id, lowered);
    }

    public bool TryDockLantern(string bayId, int nicheIndex)
    {
        if (IsLanternSolved)
            return string.Equals(bayId, SolvedBayId, StringComparison.Ordinal) && nicheIndex == SolvedNicheIndex;

        if (definition != null &&
            definition.TryGetCorrectLanternTarget(out var correctBayId, out var correctNicheIndex) &&
            string.Equals(bayId, correctBayId, StringComparison.Ordinal) &&
            nicheIndex == correctNicheIndex)
        {
            IsLanternSolved = true;
            SolvedBayId = bayId;
            SolvedNicheIndex = nicheIndex;
            LanternDockedCorrectly?.Invoke(bayId, nicheIndex);

            if (loweredById.ContainsKey(MazeMechanismIds.AtriumLock))
                SetMechanismLoweredInternal(MazeMechanismIds.AtriumLock, true);

            if (!IsPartitionUnlocked)
            {
                IsPartitionUnlocked = true;
                PartitionUnlockedChanged?.Invoke(true);
            }

            return true;
        }

        LanternDockedWrong?.Invoke(bayId, nicheIndex);
        return false;
    }

    public bool TrySetPartitionPosition(AtriumPartitionPosition target, out string refusalReasonFr)
    {
        refusalReasonFr = string.Empty;

        if (!IsPartitionUnlocked)
        {
            refusalReasonFr = "Le rail de l'atrium est encore verrouillé.";
            PartitionMoveBlocked?.Invoke(refusalReasonFr);
            return false;
        }

        if (IsPlayerInPartitionTravelZone)
        {
            refusalReasonFr = "Dégagez le seuil pour faire coulisser le mur.";
            PartitionMoveBlocked?.Invoke(refusalReasonFr);
            return false;
        }

        if (PartitionPosition == target)
            return true;

        PartitionPosition = target;
        SetPartitionMoving(true);
        PartitionPositionChanged?.Invoke(target);
        return true;
    }

    public bool TryTogglePartitionPosition(out string refusalReasonFr)
    {
        var next = PartitionPosition == AtriumPartitionPosition.EntryToIsland
            ? AtriumPartitionPosition.IslandToExit
            : AtriumPartitionPosition.EntryToIsland;
        return TrySetPartitionPosition(next, out refusalReasonFr);
    }

    public void SetPartitionMoving(bool moving)
    {
        if (IsPartitionMoving == moving)
            return;

        IsPartitionMoving = moving;
        PartitionMovingChanged?.Invoke(moving);
    }

    public bool TryPullExitLever()
    {
        if (IsExitLeverPulled)
            return false;

        IsExitLeverPulled = true;
        if (loweredById.ContainsKey(MazeMechanismIds.ExitGate))
            SetMechanismLoweredInternal(MazeMechanismIds.ExitGate, true);

        ExitLeverPulled?.Invoke();
        return true;
    }

    public void MarkDemoCompleted()
    {
        if (IsDemoCompleted)
            return;

        IsDemoCompleted = true;
        DemoCompleted?.Invoke();
    }

    public void UpdatePlayerOccupancy(MazeZoneId zone, bool inPartitionTravelZone)
    {
        if (CurrentPlayerZone == zone && IsPlayerInPartitionTravelZone == inPartitionTravelZone)
            return;

        CurrentPlayerZone = zone;
        IsPlayerInPartitionTravelZone = inPartitionTravelZone;
        PlayerZoneChanged?.Invoke(zone, inPartitionTravelZone);
    }

    public bool IsZoneConnectionOpen(MazeZoneId fromZone, MazeZoneId toZone)
    {
        if (fromZone == toZone)
            return true;

        // Intro gate separates StartCorridor from Vestibule.
        if ((fromZone == MazeZoneId.StartCorridor && toZone == MazeZoneId.Vestibule) ||
            (fromZone == MazeZoneId.Vestibule && toZone == MazeZoneId.StartCorridor))
            return IsLowered(MazeMechanismIds.IntroGate);

        // Atrium lock separates AtriumAntechamber from AtriumEntry.
        if ((fromZone == MazeZoneId.AtriumAntechamber && toZone == MazeZoneId.AtriumEntry) ||
            (fromZone == MazeZoneId.AtriumEntry && toZone == MazeZoneId.AtriumAntechamber))
            return IsLowered(MazeMechanismIds.AtriumLock);

        // Fixed barrier always blocks direct Entry <-> FarSide travel.
        if ((fromZone == MazeZoneId.AtriumEntry && toZone == MazeZoneId.AtriumFarSide) ||
            (fromZone == MazeZoneId.AtriumFarSide && toZone == MazeZoneId.AtriumEntry))
            return false;

        // Sliding partition governs Entry <-> Island and Island <-> FarSide.
        if ((fromZone == MazeZoneId.AtriumEntry && toZone == MazeZoneId.AtriumIsland) ||
            (fromZone == MazeZoneId.AtriumIsland && toZone == MazeZoneId.AtriumEntry))
            return !IsPartitionMoving && PartitionPosition == AtriumPartitionPosition.EntryToIsland;

        if ((fromZone == MazeZoneId.AtriumIsland && toZone == MazeZoneId.AtriumFarSide) ||
            (fromZone == MazeZoneId.AtriumFarSide && toZone == MazeZoneId.AtriumIsland))
            return !IsPartitionMoving && PartitionPosition == AtriumPartitionPosition.IslandToExit;

        // Exit gate governs AtriumFarSide <-> ExitSanctuary and StartCorridor <-> ExitSanctuary.
        if (toZone == MazeZoneId.ExitSanctuary || fromZone == MazeZoneId.ExitSanctuary)
            return IsLowered(MazeMechanismIds.ExitGate);

        return true;
    }

    public void ResetAllState()
    {
        InitializeFromDefinition();

        foreach (var kvp in loweredById)
            MechanismStateChanged?.Invoke(kvp.Key, kvp.Value);

        PartitionUnlockedChanged?.Invoke(IsPartitionUnlocked);
        PartitionPositionChanged?.Invoke(PartitionPosition);
        PartitionMovingChanged?.Invoke(false);
        PlayerZoneChanged?.Invoke(CurrentPlayerZone, IsPlayerInPartitionTravelZone);
        MazeReset?.Invoke();
    }

    private bool SetMechanismLoweredInternal(string id, bool lowered)
    {
        if (loweredById.TryGetValue(id, out var existing) && existing == lowered)
            return true;

        loweredById[id] = lowered;
        MechanismStateChanged?.Invoke(id, lowered);
        return true;
    }
}
