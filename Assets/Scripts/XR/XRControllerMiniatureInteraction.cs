using UnityEngine;
using UnityEngine.XR;

public sealed class XRControllerMiniatureInteraction : MonoBehaviour
{
    private static readonly RaycastHit[] RaycastHits = new RaycastHit[16];

    public Transform trackingOrigin;
    public Transform miniatureRoot;
    public MazeState mazeState;
    public LayerMask targetLayers;
    public LineRenderer pointerLine;
    public Vector3 leftHandMiniatureOffset = new Vector3(0f, 0.1f, 0.18f);
    [Min(0.1f)] public float rayLength = 1.5f;

    private InputDevice leftHand;
    private InputDevice rightHand;
    private MiniatureWallTarget currentTarget;
    private bool previousPrimaryButton;

    private void Update()
    {
        UpdateMiniaturePose();
        UpdatePointerAndInput();
    }

    private void OnDisable()
    {
        SetCurrentTarget(null);
        if (pointerLine != null)
            pointerLine.enabled = false;
    }

    private void UpdateMiniaturePose()
    {
        if (miniatureRoot == null || !TryGetPose(XRNode.LeftHand, ref leftHand, out var position, out var rotation))
            return;

        miniatureRoot.SetPositionAndRotation(position + rotation * leftHandMiniatureOffset, rotation);
    }

    private void UpdatePointerAndInput()
    {
        if (pointerLine == null || mazeState == null ||
            !TryGetPose(XRNode.RightHand, ref rightHand, out var position, out var rotation))
        {
            SetCurrentTarget(null);
            if (pointerLine != null)
                pointerLine.enabled = false;
            previousPrimaryButton = false;
            return;
        }

        pointerLine.enabled = true;
        var direction = rotation * Vector3.forward;
        var ray = new Ray(position, direction);
        var hitCount = Physics.RaycastNonAlloc(ray, RaycastHits, rayLength, targetLayers, QueryTriggerInteraction.Collide);
        MiniatureWallTarget hovered = null;
        var nearestDistance = float.MaxValue;
        var pointerEnd = position + direction * rayLength;

        for (var i = 0; i < hitCount; i++)
        {
            var candidate = RaycastHits[i].collider.GetComponentInParent<MiniatureWallTarget>();
            if (candidate == null || RaycastHits[i].distance >= nearestDistance)
                continue;

            hovered = candidate;
            nearestDistance = RaycastHits[i].distance;
            pointerEnd = RaycastHits[i].point;
        }

        SetCurrentTarget(hovered);
        pointerLine.SetPosition(0, position);
        pointerLine.SetPosition(1, pointerEnd);

        var pressed = rightHand.TryGetFeatureValue(CommonUsages.primaryButton, out var primaryButton) && primaryButton;
        if (pressed && !previousPrimaryButton && currentTarget != null)
            mazeState.TryToggleMechanism(currentTarget.mechanismId);

        previousPrimaryButton = pressed;
    }

    private bool TryGetPose(XRNode node, ref InputDevice device, out Vector3 worldPosition, out Quaternion worldRotation)
    {
        worldPosition = default;
        worldRotation = Quaternion.identity;
        if (trackingOrigin == null)
            return false;

        if (!device.isValid)
            device = InputDevices.GetDeviceAtXRNode(node);

        if (!device.isValid ||
            !device.TryGetFeatureValue(CommonUsages.devicePosition, out var localPosition) ||
            !device.TryGetFeatureValue(CommonUsages.deviceRotation, out var localRotation))
            return false;

        worldPosition = trackingOrigin.TransformPoint(localPosition);
        worldRotation = trackingOrigin.rotation * localRotation;
        return true;
    }

    private void SetCurrentTarget(MiniatureWallTarget target)
    {
        if (currentTarget == target)
            return;

        if (currentTarget != null)
            currentTarget.SetHighlighted(false);

        currentTarget = target;
        if (currentTarget != null)
            currentTarget.SetHighlighted(true);
    }
}
