using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRBaseInteractable))]
public sealed class RightHandOnlyFilter : MonoBehaviour, IXRHoverFilter, IXRSelectFilter
{
    private XRBaseInteractable interactableComponent;

    public bool canProcess => isActiveAndEnabled;

    private void Awake()
    {
        interactableComponent = GetComponent<XRBaseInteractable>();
    }

    private void OnEnable()
    {
        if (interactableComponent == null)
            return;

        interactableComponent.hoverFilters.Add(this);
        interactableComponent.selectFilters.Add(this);
    }

    private void OnDisable()
    {
        if (interactableComponent == null)
            return;

        interactableComponent.hoverFilters.Remove(this);
        interactableComponent.selectFilters.Remove(this);
    }

    public bool Process(IXRHoverInteractor interactor, IXRHoverInteractable interactable)
    {
        return IsAllowedHand(interactor);
    }

    public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
    {
        return IsAllowedHand(interactor);
    }

    private static bool IsAllowedHand(IXRInteractor interactor)
    {
        if (interactor is XRBaseInteractor baseInteractor)
            return baseInteractor.handedness != InteractorHandedness.Left;

        return true;
    }
}
