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

    public Quaternion WorldForwardToMiniatureRotation(Vector3 worldForward)
    {
        if (worldMazeRoot == null || miniatureRoot == null)
            return Quaternion.identity;

        var localDir = worldMazeRoot.InverseTransformDirection(worldForward);
        localDir.y = 0f;
        if (localDir.sqrMagnitude < 0.0001f)
            localDir = Vector3.forward;
        else
            localDir.Normalize();

        return miniatureRoot.rotation * Quaternion.LookRotation(localDir, Vector3.up);
    }

    public Vector3 MiniatureLocalToWorld(Vector3 miniatureLocalPosition)
    {
        var worldLocal = miniatureLocalPosition / Mathf.Max(0.0001f, metresToMiniatureScale);
        return worldMazeRoot != null ? worldMazeRoot.TransformPoint(worldLocal) : worldLocal;
    }
}
