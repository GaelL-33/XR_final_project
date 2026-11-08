using UnityEngine;

public sealed class MazeScaleMapper : MonoBehaviour
{
    public Transform worldMazeRoot;
    public Transform miniatureRoot;
    [Min(0.0001f)] public float metresToMiniatureScale = 0.01f;

    public Vector3 WorldToMiniatureLocal(Vector3 worldPosition)
    {
        if (worldMazeRoot == null)
            return Vector3.zero;

        return worldMazeRoot.InverseTransformPoint(worldPosition) * metresToMiniatureScale;
    }

    public Vector3 WorldToMiniaturePosition(Vector3 worldPosition, float miniatureLocalHeight)
    {
        if (miniatureRoot == null)
            return worldPosition;

        var miniatureLocalPosition = WorldToMiniatureLocal(worldPosition);
        miniatureLocalPosition.y = miniatureLocalHeight;
        return miniatureRoot.TransformPoint(miniatureLocalPosition);
    }
}
