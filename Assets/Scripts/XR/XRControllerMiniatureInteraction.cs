using System;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public sealed class XRControllerMiniatureInteraction : MonoBehaviour
{
    private const int PointerCurveSegments = 16;
    private static readonly RaycastHit[] RaycastHits = new RaycastHit[16];

    public Transform leftHandPose;
    public Transform miniatureRoot;
    public MazeState mazeState;
    public ProceduralMazeAudio mazeAudio;
    public XRHapticsService haptics;
    public NearFarInteractor rightNearFarInteractor;
    public XRRayInteractor rightTeleportInteractor;
    public LineRenderer rightWorldPointerLine;
    public LayerMask targetLayers;
    public LineRenderer pointerLine;
    public Vector3 leftHandMiniatureLocalPosition = new Vector3(0.06f, 0.01f, 0.065f);
    public Vector3 leftHandMiniatureLocalEulerOffset = new Vector3(0f, 0f, -90f);
    [Min(0.1f)] public float rayLength = 1.5f;
    [Min(0f)] public float pointerCurveSag = 0.2f;

    public event Action HintButtonPressed;

    private InputDevice leftHand;
    private InputDevice rightHand;
    private readonly Vector3[] pointerPoints = new Vector3[PointerCurveSegments + 1];
    private MiniatureWallTarget currentTarget;
    private bool previousSecondaryButton;
    private bool previousPrimaryButton;

    private void LateUpdate()
    {
        UpdateMiniaturePose();
        UpdatePointerAndInput();
    }

    private void OnDisable()
    {
        ShowPointer(null);
        previousSecondaryButton = false;
        previousPrimaryButton = false;
    }

    private void UpdateMiniaturePose()
    {
        if (miniatureRoot == null || leftHandPose == null || !IsTracked(XRNode.LeftHand, ref leftHand))
            return;

        var miniaturePosition = leftHandPose.TransformPoint(leftHandMiniatureLocalPosition);
        var miniatureRotation = leftHandPose.rotation * Quaternion.Euler(leftHandMiniatureLocalEulerOffset);
        miniatureRoot.SetPositionAndRotation(miniaturePosition, miniatureRotation);
        Physics.SyncTransforms();
    }

    private void ShowPointer(MiniatureWallTarget target)
    {
        SetCurrentTarget(target);
        if (pointerLine != null)
            pointerLine.enabled = target != null;
        if (rightWorldPointerLine != null)
            rightWorldPointerLine.forceRenderingOff = target != null;
    }

    private void UpdatePointerAndInput()
    {
        if (!IsTracked(XRNode.RightHand, ref rightHand))
        {
            ShowPointer(null);
            previousSecondaryButton = false;
            previousPrimaryButton = false;
            return;
        }

        // Optional A-button shortcut for escalating French hint
        var primaryPressed = rightHand.TryGetFeatureValue(CommonUsages.primaryButton, out var primaryButton) && primaryButton;
        if (primaryPressed && !previousPrimaryButton)
            HintButtonPressed?.Invoke();
        previousPrimaryButton = primaryPressed;

        var secondaryPressed = rightHand.TryGetFeatureValue(CommonUsages.secondaryButton, out var secondaryButton) && secondaryButton;
        var activate = secondaryPressed && !previousSecondaryButton;
        previousSecondaryButton = secondaryPressed;

        // The teleport ray and ongoing world selections own the pointer in those modes.
        if (pointerLine == null || mazeState == null || rightNearFarInteractor == null ||
            !rightNearFarInteractor.isActiveAndEnabled || rightNearFarInteractor.hasSelection ||
            (rightTeleportInteractor != null && rightTeleportInteractor.isActiveAndEnabled))
        {
            ShowPointer(null);
            return;
        }

        var hovered = TraceMiniaturePointer(rightNearFarInteractor.curveOrigin);
        ShowPointer(hovered);
        if (activate && hovered != null)
            ActivateMiniatureTarget(hovered);
    }

    private MiniatureWallTarget TraceMiniaturePointer(Transform aim)
    {
        var position = aim.position;
        var direction = aim.forward;
        for (var i = 0; i <= PointerCurveSegments; i++)
        {
            var t = i / (float)PointerCurveSegments;
            var sagProgress = Mathf.Clamp01((i - 1f) / (PointerCurveSegments - 1));
            pointerPoints[i] = position + direction * (rayLength * t) +
                               Vector3.down * (pointerCurveSag * sagProgress * sagProgress);
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
            if (segmentLength <= 0.0001f)
                continue;

            var hitCount = Physics.RaycastNonAlloc(new Ray(pointerPoints[segmentIndex], segment / segmentLength),
                RaycastHits, segmentLength, targetLayers, QueryTriggerInteraction.Collide);

            for (var hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                var candidate = RaycastHits[hitIndex].collider.GetComponentInParent<MiniatureWallTarget>();
                var pathDistance = distanceBeforeSegment + RaycastHits[hitIndex].distance;
                if (candidate == null || !candidate.isActiveAndEnabled || pathDistance >= nearestPathDistance)
                    continue;

                hovered = candidate;
                nearestPathDistance = pathDistance;
                nearestHitPoint = RaycastHits[hitIndex].point;
                nearestHitSegment = segmentIndex;
            }

            distanceBeforeSegment += segmentLength;
        }

        if (hovered != null)
        {
            pointerLine.positionCount = nearestHitSegment + 2;
            for (var i = 0; i <= nearestHitSegment; i++)
                pointerLine.SetPosition(i, pointerPoints[i]);
            pointerLine.SetPosition(nearestHitSegment + 1, nearestHitPoint);
        }

        return hovered;
    }

    private void ActivateMiniatureTarget(MiniatureWallTarget target)
    {
        var hitPos = target.transform.position;
        switch (target.targetKind)
        {
            case MiniatureTargetKind.ToggleGate:
                if (mazeState.TryToggleMechanism(target.mechanismId))
                {
                    target.PulseConfirmed();
                    if (mazeAudio != null)
                        mazeAudio.PlayOneShot3D(MazeSoundId.MiniatureClick, hitPos, 0.7f);
                    if (haptics != null)
                        haptics.PlayMiniatureClick();
                }
                else
                {
                    if (mazeAudio != null)
                        mazeAudio.PlayOneShot3D(MazeSoundId.BlockedAction, hitPos, 0.65f);
                    if (haptics != null)
                        haptics.PlayRejectionPulse();
                }
                break;

            case MiniatureTargetKind.ToggleAtriumPartition:
                if (mazeState.TryTogglePartitionPosition(out _))
                {
                    target.PulseConfirmed();
                    if (mazeAudio != null)
                        mazeAudio.PlayOneShot3D(MazeSoundId.MiniatureClick, hitPos, 0.7f);
                    if (haptics != null)
                        haptics.PlayMiniatureClick();
                }
                break;

            case MiniatureTargetKind.RequestHint:
                target.PulseConfirmed();
                HintButtonPressed?.Invoke();
                break;
        }
    }

    private static bool IsTracked(XRNode node, ref InputDevice device)
    {
        if (!device.isValid)
            device = InputDevices.GetDeviceAtXRNode(node);

        return device.TryGetFeatureValue(CommonUsages.isTracked, out var tracked) && tracked;
    }

    private void SetCurrentTarget(MiniatureWallTarget target)
    {
        if (currentTarget == target)
            return;

        if (currentTarget != null)
            currentTarget.SetHighlighted(false);

        currentTarget = target;
        if (currentTarget != null)
        {
            currentTarget.SetHighlighted(true);
            if (haptics != null)
                haptics.PlayMiniatureHover();
        }
    }
}
