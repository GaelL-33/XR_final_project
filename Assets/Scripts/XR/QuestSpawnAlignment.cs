using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

[RequireComponent(typeof(XROrigin))]
public sealed class QuestSpawnAlignment : MonoBehaviour
{
    private XROrigin xrOrigin;
    private Vector3 spawnPosition;
    private Vector3 spawnForward;

    private void Awake()
    {
        xrOrigin = GetComponent<XROrigin>();
        spawnPosition = xrOrigin.Origin.transform.position;
        spawnForward = xrOrigin.Origin.transform.forward;
    }

    private void Start()
    {
        LogPose("startup");
    }

    private void LateUpdate()
    {
        var mode = xrOrigin.CurrentTrackingOriginMode;
        if (mode != TrackingOriginModeFlags.Floor && mode != TrackingOriginModeFlags.Device)
            return;

        var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        if (!head.TryGetFeatureValue(CommonUsages.isTracked, out var tracked) || !tracked ||
            !head.TryGetFeatureValue(CommonUsages.trackingState, out var state) ||
            (state & (InputTrackingState.Position | InputTrackingState.Rotation)) !=
            (InputTrackingState.Position | InputTrackingState.Rotation))
            return;

        LogPose("first tracked pose");
        AlignToSpawn();
        LogPose("aligned spawn");
        enabled = false;
    }

    private void AlignToSpawn()
    {
        // Floor tracking can begin anywhere in the guardian's reference space.
        // Place the tracked head over the spawn while retaining its real height.
        xrOrigin.MatchOriginUpCameraForward(Vector3.up, spawnForward);
        var headPosition = xrOrigin.Camera.transform.position;
        xrOrigin.MoveCameraToWorldLocation(new Vector3(spawnPosition.x, headPosition.y, spawnPosition.z));
    }

    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void LogPose(string phase)
    {
        var camera = xrOrigin.Camera.transform;
        var offset = xrOrigin.CameraFloorOffsetObject.transform;
        Debug.Log($"[Pocket Labyrinthe] {phase} scene={SceneManager.GetActiveScene().path} " +
                  $"build={Application.version}/{Application.buildGUID} tracking={xrOrigin.CurrentTrackingOriginMode} " +
                  $"rig={xrOrigin.Origin.transform.position:F3} yaw={xrOrigin.Origin.transform.eulerAngles.y:F1} " +
                  $"offsetY={offset.localPosition.y:F3} headLocal={camera.localPosition:F3} " +
                  $"camera={camera.position:F3} forward={camera.forward:F3}", this);
    }
}
