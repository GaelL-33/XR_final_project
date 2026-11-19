using UnityEngine;

public sealed class PlayerMarkerView : MonoBehaviour
{
    public MazeScaleMapper mapper;
    public Transform trackedPlayer;
    [Min(0f)] public float miniatureLocalHeight = 0.009f;
    public bool syncFacingYaw = true;

    private void LateUpdate()
    {
        if (mapper == null || trackedPlayer == null)
            return;

        transform.position = mapper.WorldToMiniaturePosition(trackedPlayer.position, miniatureLocalHeight);
        if (syncFacingYaw)
            transform.rotation = mapper.WorldForwardToMiniatureRotation(trackedPlayer.forward);
    }
}
