using UnityEngine;

public sealed class PlayerMarkerView : MonoBehaviour
{
    public MazeScaleMapper mapper;
    public Transform trackedPlayer;
    [Min(0f)] public float miniatureLocalHeight = 0.008f;

    private void LateUpdate()
    {
        if (mapper == null || trackedPlayer == null)
            return;

        transform.position = mapper.WorldToMiniaturePosition(trackedPlayer.position, miniatureLocalHeight);
    }
}
