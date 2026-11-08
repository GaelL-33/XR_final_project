using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR;

public sealed class XRControllerMiniatureInteraction : MonoBehaviour
{
    private const int PointerCurveSegments = 16;
    private static readonly RaycastHit[] RaycastHits = new RaycastHit[16];

    public Transform trackingOrigin;
    public Transform miniatureRoot;
    public MazeState mazeState;
    public LayerMask targetLayers;
    public LineRenderer pointerLine;
    public Vector3 leftHandMiniatureLocalPosition = new Vector3(0.025f, 0.012f, 0.01f);
    public Vector3 leftHandMiniatureLocalEulerOffset = new Vector3(0f, 0f, -90f);
    [Min(0.1f)] public float rayLength = 1.5f;
    [Min(0f)] public float pointerCurveSag = 0.2f;

    private InputDevice leftHand;
    private InputDevice rightHand;
    private readonly Vector3[] pointerPoints = new Vector3[PointerCurveSegments + 1];
    private MiniatureWallTarget currentTarget;
    private bool previousSecondaryButton;

    private void Awake()
    {
        HideRightHandStraightRay();
    }

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

        var miniaturePosition = position + rotation * leftHandMiniatureLocalPosition;
        var miniatureRotation = rotation * Quaternion.Euler(leftHandMiniatureLocalEulerOffset);
        miniatureRoot.SetPositionAndRotation(miniaturePosition, miniatureRotation);
        Physics.SyncTransforms();
    }

    private void HideRightHandStraightRay()
    {
        if (trackingOrigin == null)
            return;

        foreach (var lineVisual in trackingOrigin.GetComponentsInChildren<XRInteractorLineVisual>(true))
        {
            if (!IsUnderRightController(lineVisual.transform))
                continue;

            var rayInteractor = lineVisual.GetComponent<XRRayInteractor>();
            if (rayInteractor == null || rayInteractor.lineType != XRRayInteractor.LineType.StraightLine)
                continue;

            lineVisual.enabled = false;
            var lineRenderer = lineVisual.GetComponent<LineRenderer>();
            if (lineRenderer != null)
                lineRenderer.enabled = false;
        }
    }

    private bool IsUnderRightController(Transform target)
    {
        for (var current = target; current != null && current != trackingOrigin; current = current.parent)
        {
            if (current.name == "Right Controller")
                return true;
        }

        return false;
    }

    private void UpdatePointerAndInput()
    {
        if (pointerLine == null || mazeState == null ||
            !TryGetPose(XRNode.RightHand, ref rightHand, out var position, out var rotation))
        {
            SetCurrentTarget(null);
            if (pointerLine != null)
                pointerLine.enabled = false;
            previousSecondaryButton = false;
            return;
        }

        pointerLine.enabled = true;
        var direction = rotation * Vector3.forward;
        for (var i = 0; i <= PointerCurveSegments; i++)
        {
            var t = i / (float)PointerCurveSegments;
            pointerPoints[i] = position + direction * (rayLength * t) + Vector3.down * (pointerCurveSag * t * t);
        }

        MiniatureWallTarget hovered = null;
        var nearestPathDistance = float.MaxValue;
        var nearestHitPoint = pointerPoints[PointerCurveSegments];
        var nearestHitSegment = -1;
        var distanceBeforeSegment = 0f;

        for (var segmentIndex = 0; segmentIndex < PointerCurveSegments; segmentIndex++)
        {
            var segment = pointerPoints[segmentIndex + 1] - pointerPoints[segmentIndex];
            var segmentLength = segment.magnitude;
            var hitCount = Physics.RaycastNonAlloc(new Ray(pointerPoints[segmentIndex], segment / segmentLength),
                RaycastHits, segmentLength, targetLayers, QueryTriggerInteraction.Collide);

            for (var hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                var candidate = RaycastHits[hitIndex].collider.GetComponentInParent<MiniatureWallTarget>();
                var pathDistance = distanceBeforeSegment + RaycastHits[hitIndex].distance;
                if (candidate == null || pathDistance >= nearestPathDistance)
                    continue;

                hovered = candidate;
                nearestPathDistance = pathDistance;
                nearestHitPoint = RaycastHits[hitIndex].point;
                nearestHitSegment = segmentIndex;
            }

            distanceBeforeSegment += segmentLength;
        }

        SetCurrentTarget(hovered);
        var linePointCount = nearestHitSegment >= 0 ? nearestHitSegment + 2 : PointerCurveSegments + 1;
        pointerLine.positionCount = linePointCount;
        for (var i = 0; i < linePointCount; i++)
        {
            var point = nearestHitSegment >= 0 && i == linePointCount - 1 ? nearestHitPoint : pointerPoints[i];
            pointerLine.SetPosition(i, point);
        }

        var pressed = rightHand.TryGetFeatureValue(CommonUsages.secondaryButton, out var secondaryButton) && secondaryButton;
        if (pressed && !previousSecondaryButton && currentTarget != null)
            mazeState.TryToggleMechanism(currentTarget.mechanismId);

        previousSecondaryButton = pressed;
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
