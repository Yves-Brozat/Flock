using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BoidForceZone : MonoBehaviour
{
    public enum ForceType
    {
        CurlNoise = 0,
        DirectionalCurrent = 1,
        Vortex = 2,
        Radial = 3
    }

    public enum LocalAxis
    {
        X,
        Y,
        Z
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GpuData
    {
        public Vector4 positionRadius;
        public Vector4 directionStrength;
        public Vector4 parameters;
    }

    private static readonly List<BoidForceZone> ActiveZones = new();

    [Header("Zone")]
    [SerializeField] private ForceType forceType = ForceType.DirectionalCurrent;
    [SerializeField, Min(0.01f)] private float radius = 10f;
    [Tooltip("1 = atténuation linéaire. Une valeur plus élevée concentre la force près du centre.")]
    [SerializeField, Min(0.1f)] private float falloffExponent = 1f;
    [Tooltip("Accélération appliquée au centre de la zone. Une valeur négative inverse le sens.")]
    [SerializeField] private float strength = 3f;

    [Header("Directional current")]
    [Tooltip("Direction locale du courant. Elle suit la rotation du GameObject.")]
    [SerializeField] private Vector3 localDirection = Vector3.forward;

    [Header("Curl noise")]
    [Tooltip("Fréquence spatiale de la turbulence. Plus élevée = tourbillons plus petits.")]
    [SerializeField, Min(0.001f)] private float noiseScale = 0.25f;
    [Tooltip("Vitesse d'animation du champ turbulent.")]
    [SerializeField] private float noiseSpeed = 0.5f;

    [Header("Vortex")]
    [Tooltip("Axe local autour duquel les boids tournent.")]
    [SerializeField] private LocalAxis vortexAxis = LocalAxis.Y;
    [Tooltip("Attraction supplémentaire vers l'axe. 0 produit seulement une force tangentielle.")]
    [SerializeField, Min(0f)] private float vortexInwardStrength = 0.5f;

    public static IReadOnlyList<BoidForceZone> Active => ActiveZones;
    public ForceType Type => forceType;
    public float Radius => radius;

    private void OnEnable()
    {
        if (!ActiveZones.Contains(this))
        {
            ActiveZones.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveZones.Remove(this);
    }

    private void OnValidate()
    {
        radius = Mathf.Max(0.01f, radius);
        falloffExponent = Mathf.Max(0.1f, falloffExponent);
        noiseScale = Mathf.Max(0.001f, noiseScale);
        vortexInwardStrength = Mathf.Max(0f, vortexInwardStrength);
    }

    public bool TryGetGpuData(out GpuData data)
    {
        data = default;
        if (!isActiveAndEnabled || radius <= 0f || Mathf.Approximately(strength, 0f))
        {
            return false;
        }

        Vector3 direction = forceType switch
        {
            ForceType.DirectionalCurrent => transform.TransformDirection(SafeDirection(localDirection)),
            ForceType.Vortex => transform.TransformDirection(GetLocalAxis(vortexAxis)),
            _ => Vector3.zero
        };

        float typeSpecificValue = forceType switch
        {
            ForceType.CurlNoise => noiseScale,
            ForceType.Vortex => vortexInwardStrength,
            _ => 0f
        };

        float animationSpeed = forceType == ForceType.CurlNoise ? noiseSpeed : 0f;
        data.positionRadius = new Vector4(
            transform.position.x,
            transform.position.y,
            transform.position.z,
            radius);
        data.directionStrength = new Vector4(direction.x, direction.y, direction.z, strength);
        data.parameters = new Vector4(
            (float)forceType,
            falloffExponent,
            typeSpecificValue,
            animationSpeed);
        return true;
    }

    private static Vector3 SafeDirection(Vector3 direction)
    {
        return direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector3.forward;
    }

    private static Vector3 GetLocalAxis(LocalAxis axis)
    {
        return axis switch
        {
            LocalAxis.X => Vector3.right,
            LocalAxis.Y => Vector3.up,
            _ => Vector3.forward
        };
    }

    private void OnDrawGizmos()
    {
        Color color = forceType switch
        {
            ForceType.CurlNoise => new Color(0.75f, 0.25f, 1f, 0.8f),
            ForceType.DirectionalCurrent => new Color(0.15f, 0.75f, 1f, 0.8f),
            ForceType.Vortex => new Color(1f, 0.45f, 0.1f, 0.8f),
            _ => new Color(0.25f, 1f, 0.35f, 0.8f)
        };

        Gizmos.color = color;
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.01f, radius));

        if (forceType == ForceType.DirectionalCurrent)
        {
            Vector3 direction = transform.TransformDirection(SafeDirection(localDirection));
            Gizmos.DrawLine(transform.position, transform.position + direction * radius * 0.75f);
        }
        else if (forceType == ForceType.Vortex)
        {
            Vector3 axis = transform.TransformDirection(GetLocalAxis(vortexAxis)).normalized;
            Gizmos.DrawLine(
                transform.position - axis * radius * 0.5f,
                transform.position + axis * radius * 0.5f);
        }
        else if (forceType == ForceType.Radial)
        {
            float innerRadius = radius * 0.2f;
            float outerRadius = radius * 0.75f;
            Vector3[] axes =
            {
                Vector3.right,
                Vector3.up,
                Vector3.forward,
                Vector3.left,
                Vector3.down,
                Vector3.back
            };

            foreach (Vector3 axis in axes)
            {
                Vector3 innerPoint = transform.position + axis * innerRadius;
                Vector3 outerPoint = transform.position + axis * outerRadius;
                Gizmos.DrawLine(innerPoint, outerPoint);
            }
        }
    }
}
