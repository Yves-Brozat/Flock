using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BoidObstacle : MonoBehaviour
{
    public enum ObstacleShape
    {
        Sphere = 0,
        Box = 1,
        Capsule = 2
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GpuData
    {
        public Vector4 positionShape;
        public Vector4 rotation;
        public Vector4 dimensionsAvoidance;
        public Vector4 parameters;
    }

    private static readonly List<BoidObstacle> ActiveObstacles = new();

    [Header("Obstacle")]
    [SerializeField] private ObstacleShape shape = ObstacleShape.Sphere;
    [Tooltip("Distance avant la surface à partir de laquelle l'évitement commence.")]
    [SerializeField, Min(0.01f)] private float avoidanceDistance = 3f;
    [Tooltip("Priorité de l'évitement face aux autres comportements de steering.")]
    [SerializeField, Min(0f)] private float avoidanceStrength = 4f;
    [Tooltip("Anticipation en secondes selon la vélocité du boid.")]
    [SerializeField, Min(0f)] private float lookAheadTime = 0.35f;

    [Header("Sphere")]
    [SerializeField, Min(0.01f)] private float sphereRadius = 2f;

    [Header("Box")]
    [SerializeField] private Vector3 boxSize = new(4f, 4f, 4f);

    [Header("Capsule (local Y axis)")]
    [SerializeField, Min(0.01f)] private float capsuleRadius = 1.5f;
    [SerializeField, Min(0.02f)] private float capsuleHeight = 6f;

    public static IReadOnlyList<BoidObstacle> Active => ActiveObstacles;
    public ObstacleShape Shape => shape;

    private void OnEnable()
    {
        if (!ActiveObstacles.Contains(this))
        {
            ActiveObstacles.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveObstacles.Remove(this);
    }

    private void OnValidate()
    {
        avoidanceDistance = Mathf.Max(0.01f, avoidanceDistance);
        avoidanceStrength = Mathf.Max(0f, avoidanceStrength);
        lookAheadTime = Mathf.Max(0f, lookAheadTime);
        sphereRadius = Mathf.Max(0.01f, sphereRadius);
        boxSize.x = Mathf.Max(0.01f, boxSize.x);
        boxSize.y = Mathf.Max(0.01f, boxSize.y);
        boxSize.z = Mathf.Max(0.01f, boxSize.z);
        capsuleRadius = Mathf.Max(0.01f, capsuleRadius);
        capsuleHeight = Mathf.Max(capsuleRadius * 2f, capsuleHeight);
    }

    public bool TryGetGpuData(out GpuData data)
    {
        data = default;
        if (!isActiveAndEnabled || avoidanceStrength <= 0f)
        {
            return false;
        }

        Vector3 scale = transform.lossyScale;
        scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        Vector3 dimensions = shape switch
        {
            ObstacleShape.Sphere => new Vector3(
                sphereRadius * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z)),
                0f,
                0f),
            ObstacleShape.Box => Vector3.Scale(boxSize * 0.5f, scale),
            _ => GetCapsuleDimensions(scale)
        };

        Quaternion rotationValue = transform.rotation;
        data.positionShape = new Vector4(
            transform.position.x,
            transform.position.y,
            transform.position.z,
            (float)shape);
        data.rotation = new Vector4(
            rotationValue.x,
            rotationValue.y,
            rotationValue.z,
            rotationValue.w);
        data.dimensionsAvoidance = new Vector4(
            dimensions.x,
            dimensions.y,
            dimensions.z,
            avoidanceDistance);
        data.parameters = new Vector4(avoidanceStrength, lookAheadTime, 0f, 0f);
        return true;
    }

    private Vector3 GetCapsuleDimensions(Vector3 scale)
    {
        float scaledRadius = capsuleRadius * Mathf.Max(scale.x, scale.z);
        float scaledHalfHeight = capsuleHeight * scale.y * 0.5f;
        float segmentHalfLength = Mathf.Max(0f, scaledHalfHeight - scaledRadius);
        return new Vector3(scaledRadius, segmentHalfLength, 0f);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.15f, 0.8f);
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Vector3 scale = transform.lossyScale;
        scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

        switch (shape)
        {
            case ObstacleShape.Sphere:
                Gizmos.DrawWireSphere(
                    Vector3.zero,
                    Mathf.Max(0.01f, sphereRadius * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z))));
                break;
            case ObstacleShape.Box:
                Gizmos.DrawWireCube(Vector3.zero, Vector3.Scale(boxSize, scale));
                break;
            case ObstacleShape.Capsule:
                Vector3 dimensions = GetCapsuleDimensions(scale);
                DrawCapsuleGizmo(dimensions.x, dimensions.y);
                break;
        }

        Gizmos.matrix = previousMatrix;
    }

    private static void DrawCapsuleGizmo(float radiusValue, float halfSegment)
    {
        Vector3 top = Vector3.up * halfSegment;
        Vector3 bottom = Vector3.down * halfSegment;
        Gizmos.DrawWireSphere(top, radiusValue);
        Gizmos.DrawWireSphere(bottom, radiusValue);
        Gizmos.DrawLine(top + Vector3.right * radiusValue, bottom + Vector3.right * radiusValue);
        Gizmos.DrawLine(top - Vector3.right * radiusValue, bottom - Vector3.right * radiusValue);
        Gizmos.DrawLine(top + Vector3.forward * radiusValue, bottom + Vector3.forward * radiusValue);
        Gizmos.DrawLine(top - Vector3.forward * radiusValue, bottom - Vector3.forward * radiusValue);
    }
}
