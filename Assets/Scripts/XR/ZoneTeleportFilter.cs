using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

[RequireComponent(typeof(BaseTeleportationInteractable))]
public sealed class ZoneTeleportFilter : MonoBehaviour, IXRHoverFilter, IXRSelectFilter
{
    public AtriumCrossingController crossingController;
    public MazeZoneId targetZone = MazeZoneId.Unknown;

    private BaseTeleportationInteractable teleportInteractable;

    public bool canProcess => isActiveAndEnabled;

    private void Awake()
    {
        teleportInteractable = GetComponent<BaseTeleportationInteractable>();
    }

    private void OnEnable()
    {
        if (teleportInteractable == null)
            return;

        teleportInteractable.hoverFilters.Add(this);
        teleportInteractable.selectFilters.Add(this);
    }

    private void OnDisable()
    {
        if (teleportInteractable == null)
            return;

        teleportInteractable.hoverFilters.Remove(this);
        teleportInteractable.selectFilters.Remove(this);
    }

    public bool Process(IXRHoverInteractor interactor, IXRHoverInteractable interactable)
    {
        return IsTeleportAllowed();
    }

    public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
    {
        return IsTeleportAllowed();
    }

    private bool IsTeleportAllowed()
    {
        if (crossingController == null || targetZone == MazeZoneId.Unknown)
            return true;

        return crossingController.CanTeleportToZone(targetZone);
    }
}
